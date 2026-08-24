using System.Collections;
using UnityEngine;

// 쓰나미로 불 함정을 끌 때의 연출.
//
// 다른 상쇄 연출이 그 칸에서 끝나는 것과 달리, 쓰나미는 도로를 통째로 쓸고 지나간다.
// 파도가 플레이어 뒤에서 솟아 그를 태우고 앞으로 밀려가며, 가는 길의 장애물을 밀어내고
// 함정의 불을 끈 뒤 조금 더 나아가 잦아든다.
//
// 파도가 함정 오브젝트보다 오래 살아야 하므로 이동은 전부 CounterEffectRunner 위에서 돈다.
// (함정은 꺼질 때 연출 뿌리를 통째로 비활성화한다)
//
// 쓰나미 원소를 맨땅에 놓았을 때 밀려오는 파도는 TsunamiWave가 따로 맡는다.
// 이쪽은 "불 함정에 쓰나미를 올려 상쇄했을 때"만 돈다.
public class TsunamiCounterEffect : TrapCounterEffect
{
    // 파도가 어디서 솟아오를지.
    public enum WaveOrigin
    {
        // 함정 너머(플레이어 반대편)에서 솟아 플레이어를 마주 보고 밀려온다.
        BeyondTrap,

        // 플레이어 뒤에서 솟아 플레이어를 태우고 함정 쪽으로 밀려간다.
        BehindPlayer
    }

    [Header("파도")]
    [Tooltip("쓸 파도 모델. 비워 두면 물마루 덩어리를 직접 만들어 쓴다")]
    [SerializeField] private GameObject wavePrefab;
    [Tooltip("파도가 어디서 솟아오를지")]
    [SerializeField] private WaveOrigin origin = WaveOrigin.BehindPlayer;
    [Tooltip("밀려가는 속도(m/s)")]
    [SerializeField] private float waveSpeed = 22f;
    [Tooltip("함정 너머에서 솟을 때, 함정에서 이만큼 떨어진 곳에서 시작한다(미터)")]
    [SerializeField] private float spawnBeyondTrap = 20f;
    [Tooltip("플레이어 뒤에서 솟을 때, 플레이어에서 이만큼 떨어진 곳에서 시작한다(미터)")]
    [SerializeField] private float spawnBehindPlayer = 12f;
    [Tooltip("함정을 지나 이만큼 더 나아간 뒤 잦아든다(미터)")]
    [SerializeField] private float travelPastTrap = 25f;
    [Tooltip("파도 덩어리의 크기(미터). x=도로를 가로지르는 폭, y=높이, z=앞뒤 두께")]
    [SerializeField] private Vector3 waveSize = new Vector3(14f, 5f, 3f);
    [SerializeField] private Color waveColor = new Color(0.22f, 0.6f, 0.95f, 0.72f);
    [Tooltip("잦아드는 데 걸리는 시간(초)")]
    [SerializeField] private float fadeDuration = 0.8f;

    [Header("플레이어 태우기")]
    [Tooltip("파도에 닿은 플레이어를 태워 함께 밀고 간다. 끄면 파도가 그냥 통과한다")]
    [SerializeField] private bool carryPlayer = true;
    [Tooltip("파도에 실린 플레이어가 밀려가는 속도(m/s)")]
    [SerializeField] private float ridingSpeed = 20f;
    [Tooltip("최대 몇 초까지 실려 갈지. 0이면 파도가 잦아들 때까지 계속 실려 간다")]
    [SerializeField] private float maxRideSeconds = 0f;

    [Header("장애물 쓸어내기")]
    [Tooltip("가는 길의 장애물을 플레이어 반대쪽으로 밀어낸다")]
    [SerializeField] private bool blowAwayFromPlayer = true;
    [Tooltip("파도 앞쪽 이 거리 안의 장애물을 쓸어낸다(미터)")]
    [SerializeField] private float sweepReach = 4f;
    [Tooltip("장애물이 앞으로 날아가는 속도(m/s)")]
    [SerializeField] private float blowForwardSpeed = 18f;
    [Tooltip("장애물이 위로 뜨는 속도(m/s)")]
    [SerializeField] private float blowUpSpeed = 8f;
    [Tooltip("이보다 큰 것은 쓸어내지 않는다(경계 상자 한 변, 미터). 지형·벽까지 날아가는 것을 막는다")]
    [SerializeField] private float maxObstacleSize = 8f;
    [Tooltip("쓸어낼 대상으로 볼 레이어")]
    [SerializeField] private LayerMask sweepLayers = ~0;

    // 붙이자마자 element가 Tsunami로 되어 있어야 하므로 기본값을 못박아 둔다.
    private void Reset()
    {
        element = ElementType.Tsunami;
        extinguishDelay = 0f;
    }

    protected override float OnPlay(ElementTrapCube trap)
    {
        Vector3 trapGround = GroundUnder(trap.transform.position);
        Transform player = PlayerLocator.FindPlayer();

        // 파도가 나아갈 방향. 플레이어가 없으면 방향을 정할 수 없으므로 연출을 건너뛰고
        // 함정은 예전처럼 곧바로 꺼지게 한다.
        if (player == null)
        {
            Debug.LogWarning($"{name}: 플레이어를 찾지 못해 파도 방향을 정할 수 없습니다. 불만 끕니다.", this);
            return Mathf.Max(0f, extinguishDelay);
        }

        Vector3 advance = Vector3.ProjectOnPlane(player.forward, Vector3.up);
        if (advance.sqrMagnitude < 0.0001f)
        {
            advance = Vector3.forward;
        }

        advance.Normalize();

        // 흐르는 방향과 시작 지점은 origin에 따라 갈린다.
        Vector3 flow;
        Vector3 start;

        if (origin == WaveOrigin.BehindPlayer)
        {
            // 플레이어 뒤에서 솟아 함정 쪽으로(= 플레이어가 가는 쪽으로) 밀려간다.
            flow = advance;
            start = GroundUnder(player.position - advance * Mathf.Max(0.1f, spawnBehindPlayer));
        }
        else
        {
            // 함정 너머에서 솟아 플레이어를 마주 보고 밀려온다.
            flow = -advance;
            start = GroundUnder(trapGround + advance * Mathf.Max(0.1f, spawnBeyondTrap));
        }

        // 시작점에서 함정까지 흐름 방향으로 잰 거리. 이만큼 가야 불에 닿는다.
        float distanceToTrap = Mathf.Max(0f, Vector3.Dot(trapGround - start, flow));
        float reachTime = distanceToTrap / Mathf.Max(1f, waveSpeed);

        Transform wave = BuildWave(start, flow);
        CounterEffectRunner.Track(wave.gameObject);
        CounterEffectRunner.Run(SurgeRoutine(wave, flow, distanceToTrap, player));

        return reachTime + Mathf.Max(0f, extinguishDelay);
    }

    // ───────────────────────────── 밀려가기 ─────────────────────────────

    private IEnumerator SurgeRoutine(Transform wave, Vector3 flow, float distanceToTrap, Transform player)
    {
        PlayerAutoWalker walker = player != null ? player.GetComponentInChildren<PlayerAutoWalker>() : null;

        float distance = distanceToTrap + Mathf.Max(0f, travelPastTrap);
        float travelled = 0f;
        bool riding = false;
        float rideElapsed = 0f;

        while (travelled < distance)
        {
            if (wave == null)
            {
                EndRide(walker, ref riding);
                yield break;
            }

            float step = Mathf.Max(1f, waveSpeed) * Time.deltaTime;
            wave.position += flow * step;
            travelled += step;

            if (blowAwayFromPlayer)
            {
                SweepObstacles(wave.position, flow);
            }

            // 파도가 플레이어를 따라잡으면 태운다. 태우고 나면 플레이어도 같이 밀려가므로
            // 파도보다 살짝 느리게 두어(ridingSpeed) 파도 안에 잠긴 채로 실려 간다.
            if (carryPlayer && walker != null && !walker.IsDead)
            {
                if (!riding && Overlaps(wave, walker.transform))
                {
                    walker.BeginRide(Mathf.Max(0f, ridingSpeed), flow);
                    riding = true;
                }
                else if (riding)
                {
                    rideElapsed += Time.deltaTime;

                    // maxRideSeconds가 0이면 파도가 잦아들 때까지 계속 실려 간다.
                    if (maxRideSeconds > 0f && rideElapsed >= maxRideSeconds)
                    {
                        EndRide(walker, ref riding);
                    }
                }
            }

            yield return null;
        }

        // 다 지나갔으면 잦아든다. 계속 밀려가면서 낮아져 물러가는 것처럼 보인다.
        Vector3 fullScale = wave != null ? wave.localScale : Vector3.one;
        float fade = Mathf.Max(0.01f, fadeDuration);

        for (float elapsed = 0f; elapsed < fade; elapsed += Time.deltaTime)
        {
            if (wave == null)
            {
                break;
            }

            float k = 1f - elapsed / fade;
            wave.position += flow * (Mathf.Max(1f, waveSpeed) * 0.4f * Time.deltaTime);
            wave.localScale = new Vector3(fullScale.x, fullScale.y * k, fullScale.z);
            yield return null;
        }

        // 파도가 사라지기 전에 내려놓아야 플레이어가 걷기 상태로 돌아온다.
        EndRide(walker, ref riding);

        if (wave != null)
        {
            Destroy(wave.gameObject);
        }
    }

    private static void EndRide(PlayerAutoWalker walker, ref bool riding)
    {
        if (!riding)
        {
            return;
        }

        riding = false;

        if (walker != null)
        {
            walker.EndRide();
        }
    }

    // 파도 덩어리 안에 대상이 들어왔는지. 파도에는 판정용 콜라이더를 따로 두지 않으므로
    // 흐름 방향의 두께(waveSize.z)를 기준으로 직접 잰다.
    private bool Overlaps(Transform wave, Transform target)
    {
        Vector3 offset = target.position - wave.position;
        Vector3 local = wave.InverseTransformDirection(offset);

        return Mathf.Abs(local.z) <= Mathf.Max(0.1f, waveSize.z) * 0.5f
            && Mathf.Abs(local.x) <= Mathf.Max(0.1f, waveSize.x) * 0.5f;
    }

    // 파도 앞머리에 있는 장애물을 앞쪽 위로 밀어낸다.
    // Rigidbody가 없는 것(지형·고정 벽)은 건드리지 않는다.
    private void SweepObstacles(Vector3 wavePosition, Vector3 flow)
    {
        float reach = Mathf.Max(0.1f, sweepReach);
        Vector3 center = wavePosition + flow * (Mathf.Max(0.1f, waveSize.z) * 0.5f + reach * 0.5f)
            + Vector3.up * (Mathf.Max(0.1f, waveSize.y) * 0.5f);

        Vector3 halfExtents = new Vector3(
            Mathf.Max(0.1f, waveSize.x) * 0.5f,
            Mathf.Max(0.1f, waveSize.y) * 0.5f,
            reach * 0.5f);

        Collider[] hits = Physics.OverlapBox(
            center,
            halfExtents,
            Quaternion.LookRotation(flow, Vector3.up),
            sweepLayers,
            QueryTriggerInteraction.Ignore);

        foreach (Collider hit in hits)
        {
            Rigidbody body = hit.attachedRigidbody;
            if (body == null || body.isKinematic)
            {
                continue;
            }

            // 플레이어는 태워 가는 쪽이지 날려 버리는 쪽이 아니다.
            if (PlayerLocator.IsPlayer(body.transform))
            {
                continue;
            }

            // 지형이나 커다란 구조물까지 날아가면 스테이지가 망가진다.
            Bounds bounds = hit.bounds;
            if (Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)) > Mathf.Max(0.1f, maxObstacleSize))
            {
                continue;
            }

            // 한 번 뜬 것을 매 프레임 다시 밀면 하늘로 쏘아 올려진다. 이미 뜬 것은 건너뛴다.
            if (body.linearVelocity.y > blowUpSpeed * 0.5f)
            {
                continue;
            }

            body.AddForce(
                flow * Mathf.Max(0f, blowForwardSpeed) + Vector3.up * Mathf.Max(0f, blowUpSpeed),
                ForceMode.VelocityChange);
        }
    }

    // ───────────────────────────── 파도 만들기 ─────────────────────────────

    // 물마루를 앞뒤로 줄지어 세워 파도 덩어리를 만든다.
    // 로컬 +Z가 밀려가는 방향, +X가 도로를 가로지르는 폭이다.
    private Transform BuildWave(Vector3 groundPosition, Vector3 flow)
    {
        float width = Mathf.Max(0.1f, waveSize.x);
        float height = Mathf.Max(0.1f, waveSize.y);
        float depth = Mathf.Max(0.1f, waveSize.z);

        Quaternion facing = Quaternion.LookRotation(flow, Vector3.up);

        // 물려 둔 모델이 있으면 그것을 쓴다. 크기만 도로 폭에 맞춘다.
        if (wavePrefab != null)
        {
            GameObject model = Instantiate(wavePrefab, groundPosition, facing);
            model.name = "TsunamiCounterWave";

            Bounds modelBounds = ElementVisual.MeasureBounds(model);
            float widest = Mathf.Max(modelBounds.size.x, modelBounds.size.z);
            if (widest > 0.0001f)
            {
                model.transform.localScale *= width / widest;
            }

            // 모델에 딸려 온 콜라이더는 길을 막고 플레이어를 튕겨 낸다.
            // 태우기·쓸어내기는 전부 위치로 재므로 콜라이더는 필요 없다.
            foreach (Collider surf in model.GetComponentsInChildren<Collider>(true))
            {
                Destroy(surf);
            }

            return model.transform;
        }

        GameObject root = new GameObject("TsunamiCounterWave");
        root.transform.SetPositionAndRotation(groundPosition, facing);

        Material water = WorldVisual.CreateTransparentLit(waveColor);
        Material foam = WorldVisual.CreateTransparentLit(new Color(0.92f, 0.97f, 1f, 0.85f));

        // 두께에 맞춰 물마루 개수를 정한다. 얇은 파도에 여섯 개를 세우면 서로 겹쳐 뭉개진다.
        int crests = Mathf.Clamp(Mathf.RoundToInt(depth * 2f), 2, 12);

        for (int i = 0; i < crests; i++)
        {
            // 0 = 제일 앞(높은 마루), 1 = 제일 뒤(낮게 깔리는 물).
            float t = crests > 1 ? i / (float)(crests - 1) : 0f;

            // 앞이 높고 뒤로 갈수록 낮아진다. 덮쳐 오는 벽처럼 보이게 한다.
            float crestHeight = Mathf.Lerp(height, height * 0.35f, t);
            float z = Mathf.Lerp(depth * 0.5f, -depth * 0.5f, t);

            GameObject crest = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crest.name = $"Crest_{i}";
            crest.transform.SetParent(root.transform, false);
            crest.transform.localPosition = new Vector3(0f, crestHeight * 0.5f, z);

            // 가로로 길게 늘인 납작한 구. 여러 개가 겹쳐 물결 덩어리가 된다.
            crest.transform.localScale = new Vector3(width, crestHeight, depth / crests * 2.2f);
            crest.GetComponent<Renderer>().sharedMaterial = water;

            // 쓸고 지나가는 연출이라 길을 막거나 플레이어를 밀어내면 안 된다.
            Destroy(crest.GetComponent<Collider>());
        }

        // 제일 앞 마루 꼭대기에 흰 포말을 얹어 부서지는 느낌을 준다.
        GameObject crestFoam = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        crestFoam.name = "Foam";
        crestFoam.transform.SetParent(root.transform, false);
        crestFoam.transform.localPosition = new Vector3(0f, height * 0.9f, depth * 0.45f);
        crestFoam.transform.localScale = new Vector3(width * 0.98f, height * 0.28f, depth * 0.5f);
        crestFoam.GetComponent<Renderer>().sharedMaterial = foam;
        Destroy(crestFoam.GetComponent<Collider>());

        return root.transform;
    }
}
