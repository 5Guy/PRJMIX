using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 쓰나미 원소를 놓으면 밀려오는 파도.
//
// 어느 칸에 놓든(함정 칸이든 맨땅이든) 상관없이, 플레이어가 나아가는 쪽 앞에서 솟아올라
// 플레이어를 마주 보고 밀려온다. 놓는 자리는 "언제 오느냐"에만 쓰이고,
// 파도가 어디서 시작할지는 언제나 플레이어 기준으로 정한다.
//
// 파도 모양은 여기서 직접 만든다. 놓아 둔 쓰나미 원소와 같은 Wave 도형(ElementMeshFactory)으로
// 앞으로 말려 넘어가는 물마루를 세우고, 뒤에 낮은 물살을 붙여 두툼하게 만든 뒤 통째로 밀고 온다.
//
// 예전에는 늘인 구를 앞뒤로 줄지어 세웠다. 옆에서 보면 물결이 아니라
// 비눗방울을 늘어놓은 것 같아서, 원소로 놓은 파도와 전혀 다른 물건으로 보였다.
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
    [Tooltip("마루 뒤에 붙는 낮은 물살의 개수. 많을수록 두툼해진다")]
    [Range(0, 6)]
    [SerializeField] private int crestCount = 1;
    [Tooltip("마루가 앞으로 말려 넘어가는 정도. 앞뒤 두께에 대한 비율")]
    [Range(0f, 1.2f)]
    [SerializeField] private float waveCurl = 0.75f;
    [Tooltip("양 끝이 스러지는 정도. 0이면 도로 폭 그대로 곧게 선 물벽이 된다")]
    [Range(0f, 0.9f)]
    [SerializeField] private float waveTaper = 0.25f;
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

        // 앞으로 말려 넘어가는 물마루. Wave 도형의 +Z가 말리는 쪽이고 여기서는 그게 곧 밀려가는 쪽이다.
        Mesh crestMesh = SharedWave("Crest", width, height, depth * waveCurl, 20, waveTaper, height * 0.30f);
        AddPiece(root, "Crest", crestMesh, new Vector3(0f, 0f, -depth * 0.25f), water);

        // 마루 뒤를 받치는 낮은 물살. 뒤로 갈수록 낮고 좁아진다.
        int swells = Mathf.Max(0, crestCount);

        for (int i = 0; i < swells; i++)
        {
            float t = (i + 1) / (float)(swells + 1);

            Mesh swellMesh = SharedWave(
                "Swell", width * Mathf.Lerp(0.98f, 0.86f, t), height * Mathf.Lerp(0.45f, 0.22f, t),
                depth * waveCurl * 0.3f, 12, waveTaper + 0.15f, height * 0.22f);

            // 마루 바로 뒤에 바짝 붙인다. 띄워 놓으면 물살이 아니라 널빤지를 세워 놓은 것처럼 보인다.
            AddPiece(root, $"Swell{i}", swellMesh, new Vector3(0f, 0f, -depth * (0.32f + t * 0.35f)), water);
        }

        // 말려 넘어가는 입술에서 부서지는 거품. 폭을 가로질러 크기가 제각각인 덩어리를 물려 놓는다.
        float lipY = height * 0.92f;
        float lipZ = -depth * 0.25f + depth * waveCurl;

        for (int i = 0; i < 9; i++)
        {
            float across = (i / 8f - 0.5f) * width * 0.92f;

            // 가운데가 크고 양 끝으로 갈수록 작다. 마루도 양 끝이 낮으므로 같이 내려 준다.
            float falloff = 1f - Mathf.Abs(i / 8f - 0.5f) * 2f;

            // 넉넉히 키운다. 물벽 위에 얹힌 둥근 덩어리가 있어야 판때기로 안 보인다.
            float size = height * Mathf.Lerp(0.17f, 0.36f, falloff);

            GameObject lump = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lump.name = $"Foam_{i}";
            lump.transform.SetParent(root.transform, false);
            lump.transform.localPosition = new Vector3(across, lipY - height * 0.18f * (1f - falloff), lipZ + size * 0.2f);
            lump.transform.localScale = new Vector3(size, size * 0.72f, size * 0.86f);
            lump.GetComponent<Renderer>().sharedMaterial = foam;

            // 쓸고 지나가는 연출이라 길을 막거나 플레이어를 밀어내면 안 된다.
            Destroy(lump.GetComponent<Collider>());
        }

        AttachSweepZone(root, width, height, depth, flow);
        return root.transform;
    }

    // 파도는 한 판에 여러 번 밀려온다. 그때마다 메시를 새로 만들면 쓰고 버린 메시가 계속 쌓인다
    // (메시는 GameObject를 지워도 같이 사라지지 않는다). 크기가 같으면 만들어 둔 것을 나눠 쓴다.
    private static readonly Dictionary<string, Mesh> WaveMeshes = new Dictionary<string, Mesh>();

    private static Mesh SharedWave(string role, float width, float height, float curl, int steps, float taper, float thickness)
    {
        string key = $"TsunamiWave_{role}_{width:0.00}_{height:0.00}_{curl:0.00}_{steps}_{taper:0.00}_{thickness:0.00}";

        if (WaveMeshes.TryGetValue(key, out Mesh cached) && cached != null)
        {
            return cached;
        }

        Mesh mesh = ElementMeshFactory.Wave(key, width, height, curl, steps, taper, thickness);
        WaveMeshes[key] = mesh;
        return mesh;
    }

    // 직접 만든 메시 한 조각을 파도에 붙인다.
    private static void AddPiece(GameObject root, string name, Mesh mesh, Vector3 localPosition, Material material)
    {
        GameObject piece = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        piece.transform.SetParent(root.transform, false);
        piece.transform.localPosition = localPosition;

        piece.GetComponent<MeshFilter>().sharedMesh = mesh;
        piece.GetComponent<MeshRenderer>().sharedMaterial = material;
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
