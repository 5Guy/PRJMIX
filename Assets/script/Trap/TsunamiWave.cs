using System.Collections;
using UnityEngine;

// 쓰나미 원소를 놓으면 밀려오는 파도.
//
// 어느 칸에 놓든(함정 칸이든 맨땅이든) 상관없이, 플레이어가 나아가는 쪽 앞에서 솟아올라
// 플레이어를 마주 보고 밀려온다. 놓는 자리는 "언제 오느냐"에만 쓰이고,
// 파도가 어디서 시작할지는 언제나 플레이어 기준으로 정한다.
//
// 파도 모양은 여기서 직접 만든다. 도로 폭을 가로지르는 물마루 몇 개를 겹쳐
// 앞이 높고 뒤로 갈수록 낮아지는 덩어리를 만들고, 그것을 통째로 밀고 온다.
public class TsunamiWave : MonoBehaviour
{
    [Header("파도 모양")]
    [Tooltip("쓸 파도 모델. 비워 두면 물마루를 직접 만들어 쓴다")]
    [SerializeField] private GameObject wavePrefab;
    [Tooltip("도로를 가로지르는 파도의 폭(미터)")]
    [SerializeField] private float waveWidth = 14f;
    [Tooltip("제일 높은 물마루의 높이(미터)")]
    [SerializeField] private float waveHeight = 4.5f;
    [Tooltip("앞뒤 두께(미터). 물마루가 이 안에 줄지어 선다")]
    [SerializeField] private float waveDepth = 4f;
    [Tooltip("겹쳐 세울 물마루 개수. 많을수록 두툼해진다")]
    [Range(2, 12)]
    [SerializeField] private int crestCount = 6;
    [SerializeField] private Color waveColor = new Color(0.22f, 0.6f, 0.95f, 0.72f);
    [Tooltip("물마루 꼭대기에 얹는 흰 포말의 색")]
    [SerializeField] private Color foamColor = new Color(0.92f, 0.97f, 1f, 0.85f);

    [Header("몰려오기")]
    [Tooltip("플레이어 앞 이만큼 떨어진 곳에서 솟아오른다(미터)")]
    [SerializeField] private float spawnAhead = 26f;
    [Tooltip("밀려오는 속도(m/s)")]
    [SerializeField] private float speed = 18f;
    [Tooltip("플레이어를 지나 이만큼 더 나아간 뒤 잦아든다(미터)")]
    [SerializeField] private float travelPastPlayer = 20f;
    [Tooltip("잦아드는 데 걸리는 시간(초)")]
    [SerializeField] private float fadeDuration = 0.8f;

    [Header("휩쓸림")]
    [Tooltip("파도에 부딪힌 플레이어가 밀려 날아가는 속도(m/s)")]
    [SerializeField] private float blowSpeed = 20f;
    [Tooltip("위로 띄우는 정도(m/s)")]
    [SerializeField] private float blowLift = 8f;
    [Tooltip("휩쓸리면 사망 처리한다. 끄면 날아가기만 하고 죽지는 않는다")]
    [SerializeField] private bool killPlayer = true;
    [SerializeField] private string deathAnimationTriggerName = "Die";

    [Header("사운드")]
    [SerializeField] private AudioClip surgeSound;
    [Range(0f, 1f)]
    [SerializeField] private float surgeVolume = 1f;

    // 파도를 부른다. 놓은 칸은 쓰지 않는다 — 시작점은 언제나 플레이어 앞이다.
    public void Surge()
    {
        Transform player = PlayerLocator.FindPlayer();
        if (player == null)
        {
            Debug.LogWarning($"{name}: 플레이어를 찾지 못해 파도 방향을 정할 수 없습니다.", this);
            return;
        }

        // 플레이어가 나아가는 방향.
        Vector3 advance = Vector3.ProjectOnPlane(player.forward, Vector3.up);
        if (advance.sqrMagnitude < 0.0001f)
        {
            advance = Vector3.forward;
        }

        advance.Normalize();

        // 파도는 플레이어가 가는 쪽 앞에서 솟아, 그를 마주 보고 밀려온다.
        Vector3 flow = -advance;
        Vector3 start = GroundUnder(player.position + advance * Mathf.Max(0.1f, spawnAhead));

        Transform wave = BuildWave(start, flow);

        SfxPlayer.PlayAt(surgeSound, start, surgeVolume);

        // 연출이 도는 동안 이 오브젝트가 꺼지거나 지워져도 끊기지 않도록 전용 러너에 맡긴다.
        CounterEffectRunner.Track(wave.gameObject);
        CounterEffectRunner.Run(SurgeRoutine(wave, flow));
    }

    // 물마루를 앞뒤로 줄지어 세워 파도 덩어리를 만든다.
    // 로컬 +Z가 밀려가는 방향, +X가 도로를 가로지르는 폭이다.
    private Transform BuildWave(Vector3 groundPosition, Vector3 flow)
    {
        float width = Mathf.Max(0.1f, waveWidth);
        float depth = Mathf.Max(0.1f, waveDepth);
        float height = Mathf.Max(0.1f, waveHeight);

        // 물려 둔 모델이 있으면 그것을 쓴다. 크기만 도로 폭에 맞춘다.
        if (wavePrefab != null)
        {
            GameObject model = Instantiate(wavePrefab, groundPosition, Quaternion.LookRotation(flow, Vector3.up));
            model.name = "Tsunami";

            Bounds modelBounds = ElementVisual.MeasureBounds(model);
            float widest = Mathf.Max(modelBounds.size.x, modelBounds.size.z);
            if (widest > 0.0001f)
            {
                model.transform.localScale *= width / widest;
            }

            // 모델에 딸려 온 콜라이더는 길을 막는다. 휩쓸림 판정은 아래에서 트리거로 따로 세운다.
            foreach (Collider surf in model.GetComponentsInChildren<Collider>(true))
            {
                Destroy(surf);
            }

            AttachSweepZone(model, width, height, depth, flow);
            return model.transform;
        }

        GameObject root = new GameObject("Tsunami");
        root.transform.SetPositionAndRotation(groundPosition, Quaternion.LookRotation(flow, Vector3.up));

        Material water = WorldVisual.CreateTransparentLit(waveColor);
        Material foam = WorldVisual.CreateTransparentLit(foamColor);

        int crests = Mathf.Max(2, crestCount);

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

        AttachSweepZone(root, width, height, depth, flow);
        return root.transform;
    }

    // 파도 전체를 감싸는 판정 하나를 트리거로 세운다.
    // 물마루나 모델 조각마다 붙이면 한 번 스칠 때 여러 번 맞는다.
    private void AttachSweepZone(GameObject wave, float width, float height, float depth, Vector3 flow)
    {
        BoxCollider hitBox = wave.AddComponent<BoxCollider>();
        hitBox.isTrigger = true;

        // 모델 쪽은 크기를 이미 조절해 두었으므로, 미터로 적은 값이 그대로 유지되게 되돌린다.
        Vector3 lossy = wave.transform.lossyScale;
        float inverseX = Mathf.Approximately(lossy.x, 0f) ? 1f : 1f / lossy.x;
        float inverseY = Mathf.Approximately(lossy.y, 0f) ? 1f : 1f / lossy.y;
        float inverseZ = Mathf.Approximately(lossy.z, 0f) ? 1f : 1f / lossy.z;

        hitBox.size = new Vector3(width * inverseX, height * inverseY, depth * inverseZ);
        hitBox.center = new Vector3(0f, height * 0.5f * inverseY, 0f);

        wave.AddComponent<TsunamiWaveZone>().Setup(flow, blowSpeed, blowLift, killPlayer, deathAnimationTriggerName);
    }

    private IEnumerator SurgeRoutine(Transform wave, Vector3 flow)
    {
        float distance = Mathf.Max(0.1f, spawnAhead + travelPastPlayer);
        float travelled = 0f;

        while (travelled < distance)
        {
            if (wave == null)
            {
                yield break;
            }

            float step = Mathf.Max(0.1f, speed) * Time.deltaTime;
            wave.position += flow * step;
            travelled += step;
            yield return null;
        }

        // 다 지나갔으면 잦아든다. 계속 밀려가면서 낮아져 물러가는 것처럼 보인다.
        Vector3 fullScale = wave != null ? wave.localScale : Vector3.one;
        float fade = Mathf.Max(0.01f, fadeDuration);

        for (float elapsed = 0f; elapsed < fade; elapsed += Time.deltaTime)
        {
            if (wave == null)
            {
                yield break;
            }

            float k = 1f - elapsed / fade;
            wave.position += flow * (Mathf.Max(0.1f, speed) * 0.4f * Time.deltaTime);
            wave.localScale = new Vector3(fullScale.x, fullScale.y * k, fullScale.z);
            yield return null;
        }

        if (wave != null)
        {
            Destroy(wave.gameObject);
        }
    }

    // 파도가 지면을 따라 오도록 바닥 높이를 찾는다.
    private static Vector3 GroundUnder(Vector3 position, float searchHeight = 20f)
    {
        Vector3 from = position + Vector3.up * searchHeight;

        if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, searchHeight * 2f, ~0, QueryTriggerInteraction.Ignore))
        {
            return hit.point;
        }

        return position;
    }
}
