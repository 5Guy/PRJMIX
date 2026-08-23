using UnityEngine;

// 자동차(ChargingCar)를 파훼하는 설치물을 도로 한 칸에 세운다.
//
// 흙 원소로 세우는 돌 벽은 PlacementSystem이 직접 만들지만, Stage 3의 세 가지 파훼는
// 생김새도 동작도 제각각이라 여기로 모아 두었다. PlacementSystem은 "이 원소가 설치물인가"만 묻고,
// 무엇이 어떻게 생기는지는 전부 이 클래스가 정한다.
//
//   흑요석(Obsidian) : 검은 결정 벽. 부딪힌 차가 터진다        → ObsidianWall
//   바람(Wind)       : 돌풍. 들어온 차가 뒤로 날아간다          → WindGust
//   늪(Swamp)        : 웅덩이. 들어온 차가 앞부터 박혀 가라앉는다 → SwampPit
//
// 만든 결과는 PlacedElementView에 3D 모습으로 넘겨진다.
// 탑뷰에서는 렌더러만 꺼지고 콜라이더는 살아 있으므로 판정은 그대로 돌아간다.
public static class CarCounterBuilder
{
    private static readonly Color ObsidianColor = new Color(0.05f, 0.045f, 0.07f);
    private static readonly Color SwampColor = new Color(0.17f, 0.23f, 0.14f);
    private static readonly Color WindColor = new Color(0.78f, 0.88f, 0.95f, 0.45f);

    // EzTornado 프리팹은 사막 데모 기준(반경 16m)이라 이 스테이지에서는 이만큼 줄여야 한 칸에 맞는다.
    private const float WindPrefabScale = 0.03f;

    // 이 원소를 빈 칸에 놓으면 자동차 파훼 설치물이 세워지는가.
    public static bool Handles(ElementData data)
    {
        if (data == null)
        {
            return false;
        }

        return data.ElementType == ElementType.Obsidian
            || data.ElementType == ElementType.Wind
            || data.ElementType == ElementType.Swamp;
    }

    // 설치물을 세우고 3D 모습의 뿌리를 돌려준다. Handles가 false면 null.
    // windPrefab을 넘기면(EzTornado 같은 바람 연출) 바람 파훼의 모습으로 그것을 쓴다.
    public static Transform Build(ElementData data, Transform parent, Vector3 groundPosition, float cellSize, GameObject windPrefab)
    {
        if (!Handles(data))
        {
            return null;
        }

        switch (data.ElementType)
        {
            case ElementType.Obsidian:
                return BuildObsidianWall(parent, groundPosition, cellSize);
            case ElementType.Wind:
                return BuildWindGust(parent, groundPosition, cellSize, windPrefab);
            case ElementType.Swamp:
                return BuildSwampPit(parent, groundPosition, cellSize);
        }

        return null;
    }

    // 검은 흑요석 벽. 네모난 흙 벽과 구별되도록 기울어진 결정 조각을 몇 개 세운다.
    private static Transform BuildObsidianWall(Transform parent, Vector3 groundPosition, float cellSize)
    {
        GameObject wall = new GameObject("ObsidianWall");
        wall.transform.SetParent(parent, false);
        wall.transform.position = groundPosition;

        Material material = WorldVisual.CreateLit(ObsidianColor);

        // 유리질이라 빛을 받으면 반질반질하게 보인다.
        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.85f);
        }

        float width = cellSize * 0.9f;
        float height = cellSize * 1.6f;

        // 가운데 큰 기둥 하나 + 양옆으로 기운 조각 둘.
        AddShard(wall.transform, material, new Vector3(0f, height * 0.5f, 0f),
            new Vector3(width, height, width * 0.55f), new Vector3(0f, 12f, 4f));
        AddShard(wall.transform, material, new Vector3(-width * 0.32f, height * 0.32f, 0f),
            new Vector3(width * 0.45f, height * 0.65f, width * 0.45f), new Vector3(6f, -20f, -14f));
        AddShard(wall.transform, material, new Vector3(width * 0.34f, height * 0.28f, 0f),
            new Vector3(width * 0.4f, height * 0.55f, width * 0.4f), new Vector3(-5f, 24f, 16f));

        // 차가 이 벽을 알아채고 터지도록 한다. 조각의 콜라이더에서 부모를 타고 찾아온다.
        wall.AddComponent<ObsidianWall>();

        return wall.transform;
    }

    private static void AddShard(Transform parent, Material material, Vector3 localPosition, Vector3 size, Vector3 euler)
    {
        GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shard.name = "Shard";
        shard.transform.SetParent(parent, false);
        shard.transform.localPosition = localPosition;
        shard.transform.localRotation = Quaternion.Euler(euler);
        shard.transform.localScale = size;
        shard.GetComponent<Renderer>().sharedMaterial = material;
    }

    // 돌풍. 프리팹을 넘기면 그 연출을, 없으면 위로 휘몰아치는 파티클을 직접 만든다.
    private static Transform BuildWindGust(Transform parent, Vector3 groundPosition, float cellSize, GameObject windPrefab)
    {
        GameObject gust = new GameObject("WindGust");
        gust.transform.SetParent(parent, false);
        gust.transform.position = groundPosition;

        if (windPrefab != null)
        {
            AttachWindPrefab(gust.transform, cellSize, windPrefab);
        }
        else
        {
            BuildWindParticles(gust.transform, cellSize);
        }

        // 차가 지나가는 칸 전체를 덮는 기둥 모양 판정.
        BoxCollider zone = gust.AddComponent<BoxCollider>();
        zone.isTrigger = true;
        zone.size = new Vector3(cellSize, cellSize * 3f, cellSize);
        zone.center = new Vector3(0f, cellSize * 1.5f, 0f);

        gust.AddComponent<WindGust>();

        return gust.transform;
    }

    // EzTornado 같은 기성 연출을 붙인다.
    //
    // 그 프리팹들은 사막 데모 기준(반경 십수 미터)이라 그대로 놓으면 화면을 다 덮는다.
    // 파티클이 Local 스케일 모드면 부모를 줄여도 입자 크기가 그대로이므로,
    // Sandstorm과 같은 방식으로 Hierarchy로 바꾼 뒤 크기를 줄인다.
    private static void AttachWindPrefab(Transform parent, float cellSize, GameObject windPrefab)
    {
        GameObject visual = Object.Instantiate(windPrefab, parent.position, Quaternion.identity, parent);
        visual.name = "WindVisual";

        foreach (ParticleSystem particle in visual.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = particle.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }

        // 프리팹 원본이 반경 십수 미터라 한 칸(보통 1m)에 맞추려면 크게 줄여야 한다.
        // 0.03배는 같은 프리팹을 쓰는 모래바람(Sandstorm)에서 이 스테이지 크기에 맞춰 잡아 둔 값이다.
        visual.transform.localScale = Vector3.one * Mathf.Max(0.005f, WindPrefabScale * cellSize);

        // 연출에 딸려 온 콜라이더는 도로 위 장애물이 되어 버리므로 떼어 낸다.
        // 판정은 이 설치물이 따로 가진 트리거 하나로만 낸다.
        foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
        {
            Object.Destroy(collider);
        }
    }

    // 프리팹을 물려 두지 않았을 때 쓰는 기본 바람. 바닥에서 위로 휘감아 올라간다.
    private static void BuildWindParticles(Transform parent, float cellSize)
    {
        GameObject host = new GameObject("WindVisual");
        host.transform.SetParent(parent, false);
        host.transform.localPosition = Vector3.zero;

        ParticleSystem particles = host.AddComponent<ParticleSystem>();
        particles.Stop();

        ParticleSystem.MainModule main = particles.main;
        main.startColor = WindColor;
        main.startSpeed = cellSize * 3.5f;
        main.startLifetime = 1.1f;
        main.startSize = cellSize * 0.28f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 220;
        main.loop = true;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 70f;

        // 축이 위를 보는 좁은 원뿔에서 뿜어 올린다.
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 14f;
        shape.radius = cellSize * 0.45f;
        shape.radiusThickness = 1f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        // 올라가면서 축을 중심으로 돌아 소용돌이처럼 보이게 한다.
        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.orbitalY = new ParticleSystem.MinMaxCurve(cellSize * 2.5f);

        host.GetComponent<ParticleSystemRenderer>().sharedMaterial = WorldVisual.CreateTransparentLit(WindColor);

        particles.Play();
    }

    // 늪 웅덩이. 도로에 파인 것처럼 살짝 내려앉은 어두운 원반을 깐다.
    private static Transform BuildSwampPit(Transform parent, Vector3 groundPosition, float cellSize)
    {
        GameObject pit = new GameObject("SwampPit");
        pit.transform.SetParent(parent, false);
        pit.transform.position = groundPosition;

        Material surface = WorldVisual.CreateLit(SwampColor);
        if (surface.HasProperty("_Smoothness"))
        {
            surface.SetFloat("_Smoothness", 0.6f);
        }

        float diameter = cellSize * 1.5f;

        // 원기둥 프리미티브는 높이가 2라서 y 스케일을 두께의 절반으로 잡아야 한다.
        // 수면은 도로보다 살짝 내려앉게 둔다.
        const float thickness = 0.12f;
        GameObject water = WorldVisual.CreateCylinder(
            "SwampWater",
            pit.transform,
            new Vector3(0f, -thickness * 0.25f, 0f),
            new Vector3(diameter, thickness * 0.5f, diameter),
            surface);

        Object.Destroy(water.GetComponent<Collider>());

        // 가장자리에 둔덕을 둘러 웅덩이처럼 보이게 한다.
        Material rim = WorldVisual.CreateLit(new Color(0.13f, 0.16f, 0.1f));
        GameObject bank = WorldVisual.CreateCylinder(
            "SwampBank",
            pit.transform,
            new Vector3(0f, -thickness * 0.6f, 0f),
            new Vector3(diameter * 1.12f, thickness * 0.5f, diameter * 1.12f),
            rim);

        Object.Destroy(bank.GetComponent<Collider>());

        // 차가 빠지는 판정. 웅덩이 위를 지나가면 걸리도록 칸 크기로 덮는다.
        BoxCollider zone = pit.AddComponent<BoxCollider>();
        zone.isTrigger = true;
        zone.size = new Vector3(diameter, cellSize * 1.2f, diameter);
        zone.center = new Vector3(0f, cellSize * 0.4f, 0f);

        pit.AddComponent<SwampPit>();

        return pit.transform;
    }
}
