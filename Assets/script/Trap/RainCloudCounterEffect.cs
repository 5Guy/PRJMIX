using System.Collections;
using UnityEngine;

// 비구름으로 불 함정을 끌 때의 연출.
//
// 함정 바로 위에 먹구름이 뭉게뭉게 나타나 비를 뿌린다.
// 빗줄기가 불에 닿으면 불길이 잦아들면서 흰 김이 피어오르고,
// 다 꺼지고 나면 구름도 흩어져 사라진다.
public class RainCloudCounterEffect : TrapCounterEffect
{
    [Header("비구름")]
    [Tooltip("비워 두면 먹구름 덩어리를 직접 만들어 쓴다")]
    [SerializeField] private GameObject cloudPrefab;
    [Tooltip("함정 바닥에서 구름까지의 높이(미터)")]
    [SerializeField] private float cloudHeight = 6f;
    [Tooltip("구름의 폭(미터)")]
    [SerializeField] private float cloudWidth = 5f;
    [SerializeField] private Color cloudColor = new Color(0.32f, 0.35f, 0.42f, 0.92f);
    [Tooltip("구름이 부풀어 오르는 데 걸리는 시간(초)")]
    [SerializeField] private float gatherDuration = 0.5f;

    [Header("비")]
    [SerializeField] private Color rainColor = new Color(0.62f, 0.8f, 1f, 0.85f);
    [Tooltip("빗줄기가 떨어지는 속도(m/s)")]
    [SerializeField] private float rainSpeed = 16f;
    [Tooltip("1초에 뿌리는 빗방울 수")]
    [SerializeField] private float rainRate = 350f;
    [Tooltip("불이 꺼진 뒤에도 이만큼 더 비가 내린다(초)")]
    [SerializeField] private float rainAfterExtinguish = 1.2f;

    [Header("김(수증기)")]
    [Tooltip("불이 꺼질 때 피어오르는 흰 김")]
    [SerializeField] private bool showSteam = true;
    [SerializeField] private Color steamColor = new Color(0.95f, 0.95f, 0.95f, 0.55f);

    protected override float OnPlay(ElementTrapCube trap)
    {
        Vector3 ground = GroundUnder(trap.transform.position);
        Vector3 cloudPosition = ground + Vector3.up * Mathf.Max(1f, cloudHeight);

        GameObject cloud = BuildCloud(cloudPosition);
        CounterEffectRunner.Run(RainRoutine(cloud.transform, ground));

        // 구름이 뭉치고, 빗줄기가 불까지 떨어질 시간을 준 뒤에 꺼진다.
        float fallTime = Mathf.Max(1f, cloudHeight) / Mathf.Max(1f, rainSpeed);
        return Mathf.Max(0f, gatherDuration) + fallTime + Mathf.Max(0f, extinguishDelay);
    }

    private IEnumerator RainRoutine(Transform cloud, Vector3 ground)
    {
        // 작게 나타나 부풀어 오른다.
        float gather = Mathf.Max(0.01f, gatherDuration);
        for (float t = 0f; t < gather; t += Time.deltaTime)
        {
            if (cloud == null)
            {
                yield break;
            }

            cloud.localScale = Vector3.one * Mathf.Lerp(0.2f, 1f, t / gather);
            yield return null;
        }

        if (cloud == null)
        {
            yield break;
        }

        cloud.localScale = Vector3.one;

        // 불이 꺼질 때쯤 김이 피어오른다.
        float fallTime = Mathf.Max(1f, cloudHeight) / Mathf.Max(1f, rainSpeed);
        yield return new WaitForSeconds(fallTime + Mathf.Max(0f, extinguishDelay));

        if (showSteam)
        {
            BuildSteam(ground);
        }

        yield return new WaitForSeconds(Mathf.Max(0f, rainAfterExtinguish));

        // 비를 멈추고 구름을 흩뜨린다.
        if (cloud == null)
        {
            yield break;
        }

        foreach (ParticleSystem particles in cloud.GetComponentsInChildren<ParticleSystem>())
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        const float scatter = 1f;
        for (float t = 0f; t < scatter; t += Time.deltaTime)
        {
            if (cloud == null)
            {
                yield break;
            }

            float k = 1f - t / scatter;
            cloud.localScale = Vector3.one * Mathf.Lerp(1.3f, 1f, k);

            foreach (Renderer renderer in cloud.GetComponentsInChildren<Renderer>())
            {
                Color color = renderer.sharedMaterial != null ? renderer.sharedMaterial.color : Color.white;
                WorldVisual.SetMaterialColor(renderer.sharedMaterial, new Color(color.r, color.g, color.b, cloudColor.a * k));
            }

            yield return null;
        }

        if (cloud != null)
        {
            Destroy(cloud.gameObject);
        }
    }

    // ───────────────────────────── 구름 만들기 ─────────────────────────────

    private GameObject BuildCloud(Vector3 position)
    {
        if (cloudPrefab != null)
        {
            return CounterEffectRunner.Track(Instantiate(cloudPrefab, position, Quaternion.identity));
        }

        GameObject cloud = CounterEffectRunner.Track(new GameObject("RainCloud"));
        cloud.transform.position = position;
        cloud.transform.localScale = Vector3.one * 0.2f;

        Material puffMaterial = WorldVisual.CreateTransparentLit(cloudColor);
        int puffCount = Mathf.Max(5, Mathf.RoundToInt(cloudWidth * 1.5f));

        // 납작한 구를 여러 개 겹쳐 뭉게구름처럼 보이게 한다.
        for (int i = 0; i < puffCount; i++)
        {
            float angle = i / (float)puffCount * Mathf.PI * 2f;
            float radius = cloudWidth * 0.5f * Random.Range(0.35f, 0.9f);
            float scale = cloudWidth * Random.Range(0.35f, 0.6f);

            GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            puff.name = $"Puff_{i}";
            puff.transform.SetParent(cloud.transform, false);
            puff.transform.localPosition = new Vector3(
                Mathf.Cos(angle) * radius,
                Random.Range(-0.2f, 0.35f),
                Mathf.Sin(angle) * radius);
            puff.transform.localScale = new Vector3(scale, scale * 0.55f, scale);
            puff.GetComponent<Renderer>().sharedMaterial = puffMaterial;
            Destroy(puff.GetComponent<Collider>());
        }

        BuildRain(cloud.transform);
        return cloud;
    }

    private void BuildRain(Transform cloud)
    {
        GameObject rainObject = new GameObject("Rain");
        rainObject.transform.SetParent(cloud, false);
        rainObject.transform.localPosition = Vector3.down * 0.3f;
        rainObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // 아래로 뿌린다

        ParticleSystem rain = rainObject.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = rain.main;
        main.startColor = rainColor;
        main.startSpeed = Mathf.Max(1f, rainSpeed);
        main.startLifetime = Mathf.Max(1f, cloudHeight) / Mathf.Max(1f, rainSpeed) + 0.3f;
        main.startSize = 0.12f;
        main.gravityModifier = 0.4f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 2000;

        ParticleSystem.EmissionModule emission = rain.emission;
        emission.rateOverTime = Mathf.Max(1f, rainRate);

        // Circle은 원 안에서 "바깥쪽으로" 방사형으로 뿜어서 비가 옆으로 날아간다.
        // 각도 0짜리 Cone은 원판 전체에서 축 방향(= 이 오브젝트의 +Z, 곧 아래쪽)으로만 쏜다.
        ParticleSystem.ShapeModule shape = rain.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 0f;
        shape.radius = cloudWidth * 0.5f;
        shape.radiusThickness = 1f;   // 테두리만이 아니라 원판 전체에서 떨어지게

        // 빗방울은 떨어지는 방향으로 길게 늘여야 빗줄기처럼 보인다.
        ParticleSystemRenderer renderer = rainObject.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 6f;
        renderer.sharedMaterial = WorldVisual.CreateTransparentLit(rainColor);
    }

    // 불이 꺼지면서 피어오르는 흰 김.
    private void BuildSteam(Vector3 ground)
    {
        GameObject steamObject = CounterEffectRunner.Track(new GameObject("Steam"));
        steamObject.transform.position = ground + Vector3.up * 0.2f;

        ParticleSystem steam = steamObject.AddComponent<ParticleSystem>();

        // duration/loop은 재생 중에는 바꿀 수 없다. 설정을 마친 뒤 다시 튼다.
        steam.Stop();

        ParticleSystem.MainModule main = steam.main;
        main.startColor = steamColor;
        main.startSpeed = 2.2f;
        main.startLifetime = 1.6f;
        main.startSize = 1.4f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;
        main.duration = 1.5f;
        main.loop = false;

        ParticleSystem.EmissionModule emission = steam.emission;
        emission.rateOverTime = 120f;

        // 비와 같은 이유로 Cone을 쓴다. 살짝 벌어지게 해서 김이 퍼지며 오른다.
        ParticleSystem.ShapeModule shape = steam.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 18f;
        shape.radius = 1.2f;
        shape.radiusThickness = 1f;
        shape.rotation = new Vector3(-90f, 0f, 0f);   // 축을 위로 돌린다

        steamObject.GetComponent<ParticleSystemRenderer>().sharedMaterial = WorldVisual.CreateTransparentLit(steamColor);

        steam.Play();
        Destroy(steamObject, 4f);
    }
}
