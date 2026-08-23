using System.Collections;
using UnityEngine;

// 자동차(ChargingCar)가 파훼당하는 순간 한 번만 보여 주는 연출 모음.
//
// 흑요석 벽에 부딪혀 터지거나 늪에 빠질 때처럼, 자동차 본체와 상관없이 잠깐 나왔다 사라지는
// 파티클·잔해를 만든다. 만들어 낸 오브젝트는 전부 CounterEffectRunner에 맡겨 두어서
// 스테이지가 처음 상태로 돌아갈 때(시작 버튼 / 사망 리스폰) 같이 치워진다.
//
// 자동차 위에서 코루틴을 돌리면 자동차가 되돌려지는 순간 끊기므로,
// 시간이 걸리는 부분은 여기서도 CounterEffectRunner 위에서 돌린다.
public static class CarWreckEffects
{
    // 자동차가 그 자리에서 터진다. size는 차체 크기(미터)로, 연출 규모를 여기에 맞춘다.
    public static void Explosion(Vector3 center, float size, float lifetime)
    {
        float scale = Mathf.Max(0.5f, size);

        BuildFlash(center, scale, 0.35f);
        BuildFireBurst(center, scale);
        BuildSmoke(center, scale, lifetime);
        BuildDebris(center, scale, lifetime);
    }

    // 늪에 차가 빠질 때 퍼지는 물결과 올라오는 거품.
    public static void SwampSplash(Vector3 center, Color color, float diameter, float duration)
    {
        GameObject host = CounterEffectRunner.Track(new GameObject("SwampSplash"));
        host.transform.position = center;

        ParticleSystem particles = host.AddComponent<ParticleSystem>();
        particles.Stop();

        ParticleSystem.MainModule main = particles.main;
        main.startColor = new Color(color.r + 0.15f, color.g + 0.2f, color.b + 0.1f, 0.75f);
        main.startSpeed = 1.4f;
        main.startLifetime = 1.1f;
        main.startSize = diameter * 0.28f;
        main.gravityModifier = 0.4f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 160;
        main.duration = Mathf.Max(0.2f, duration);
        main.loop = false;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 45f;

        // 위로만 뽀글거리도록 각이 좁은 원뿔을 쓴다(원형 Shape은 옆으로 방사형으로 뿜는다).
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 18f;
        shape.radius = diameter * 0.4f;
        shape.radiusThickness = 1f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        host.GetComponent<ParticleSystemRenderer>().sharedMaterial =
            WorldVisual.CreateTransparentLit(new Color(color.r + 0.15f, color.g + 0.2f, color.b + 0.1f, 0.75f));

        particles.Play();
        Object.Destroy(host, main.duration + 2f);
    }

    // 터지는 순간의 섬광. 짧게 확 밝아졌다가 꺼진다.
    private static void BuildFlash(Vector3 center, float scale, float duration)
    {
        GameObject host = CounterEffectRunner.Track(new GameObject("ExplosionFlash"));
        host.transform.position = center;

        Light light = host.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.72f, 0.32f);
        light.range = scale * 6f;
        light.intensity = 12f;

        CounterEffectRunner.Run(FadeLight(light, duration));
    }

    private static IEnumerator FadeLight(Light light, float duration)
    {
        float life = Mathf.Max(0.05f, duration);
        float start = light != null ? light.intensity : 0f;

        for (float t = 0f; t < life; t += Time.deltaTime)
        {
            if (light == null)
            {
                yield break;
            }

            light.intensity = Mathf.Lerp(start, 0f, t / life);
            yield return null;
        }

        if (light != null)
        {
            Object.Destroy(light.gameObject);
        }
    }

    // 사방으로 확 퍼지는 불덩이.
    private static void BuildFireBurst(Vector3 center, float scale)
    {
        GameObject host = CounterEffectRunner.Track(new GameObject("ExplosionFire"));
        host.transform.position = center;

        ParticleSystem particles = host.AddComponent<ParticleSystem>();
        particles.Stop();

        ParticleSystem.MainModule main = particles.main;
        main.startColor = new Color(1f, 0.55f, 0.18f, 0.95f);
        main.startSpeed = scale * 4.5f;
        main.startLifetime = 0.55f;
        main.startSize = scale * 0.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 120;
        main.duration = 0.2f;
        main.loop = false;

        // 한 번에 터져 나와야 하므로 계속 뿜는 대신 시작할 때 한 뭉치만 낸다.
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)60) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = scale * 0.25f;

        // 불덩이는 퍼지면서 사그라든다.
        ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
        fade.enabled = true;
        fade.color = BurnGradient();

        host.GetComponent<ParticleSystemRenderer>().sharedMaterial =
            WorldVisual.CreateTransparentLit(new Color(1f, 0.55f, 0.18f, 0.95f));

        particles.Play();
        Object.Destroy(host, 3f);
    }

    // 불덩이가 노랑 → 주황 → 검은 연기로 사그라드는 색 변화.
    private static ParticleSystem.MinMaxGradient BurnGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.92f, 0.55f), 0f),
                new GradientColorKey(new Color(1f, 0.45f, 0.12f), 0.4f),
                new GradientColorKey(new Color(0.2f, 0.18f, 0.16f), 1f),
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.8f, 0.6f),
                new GradientAlphaKey(0f, 1f),
            });

        return new ParticleSystem.MinMaxGradient(gradient);
    }

    // 터진 뒤 한동안 피어오르는 검은 연기.
    private static void BuildSmoke(Vector3 center, float scale, float duration)
    {
        GameObject host = CounterEffectRunner.Track(new GameObject("ExplosionSmoke"));
        host.transform.position = center;

        ParticleSystem particles = host.AddComponent<ParticleSystem>();
        particles.Stop();

        ParticleSystem.MainModule main = particles.main;
        main.startColor = new Color(0.18f, 0.17f, 0.16f, 0.55f);
        main.startSpeed = scale * 1.1f;
        main.startLifetime = 1.8f;
        main.startSize = scale * 0.8f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 150;
        main.duration = Mathf.Max(0.3f, duration);
        main.loop = false;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 25f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 25f;
        shape.radius = scale * 0.3f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        host.GetComponent<ParticleSystemRenderer>().sharedMaterial =
            WorldVisual.CreateTransparentLit(new Color(0.18f, 0.17f, 0.16f, 0.55f));

        particles.Play();
        Object.Destroy(host, main.duration + 3f);
    }

    // 사방으로 튀는 잔해 조각.
    //
    // 콜라이더는 떼어 낸다. 플레이어나 도로에 걸려 튕기면 오히려 눈에 거슬리고,
    // 지나가는 플레이어를 밀어 버릴 수도 있기 때문이다. 중력만 받아 포물선으로 날아간다.
    private static void BuildDebris(Vector3 center, float scale, float lifetime)
    {
        Material material = WorldVisual.CreateLit(new Color(0.16f, 0.15f, 0.15f));

        for (int i = 0; i < 8; i++)
        {
            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = "Debris";
            piece.transform.position = center + Random.insideUnitSphere * (scale * 0.25f);
            piece.transform.rotation = Random.rotation;
            piece.transform.localScale = Vector3.one * (scale * Random.Range(0.06f, 0.14f));
            piece.GetComponent<Renderer>().sharedMaterial = material;

            Object.Destroy(piece.GetComponent<Collider>());

            Rigidbody body = piece.AddComponent<Rigidbody>();
            body.linearVelocity = (Random.onUnitSphere + Vector3.up * 1.5f).normalized * (scale * Random.Range(2.5f, 5f));
            body.angularVelocity = Random.onUnitSphere * 12f;

            CounterEffectRunner.Track(piece);
            Object.Destroy(piece, lifetime);
        }
    }
}
