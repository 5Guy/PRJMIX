using System.Collections;
using UnityEngine;

// 흙으로 불 함정을 끌 때의 연출.
//
// 함정 위에서 마른 흙이 쏟아져 내려 불을 덮는다.
// 산소가 끊긴 불은 흙먼지를 일으키며 사그라들고,
// 꺼진 자리에는 흙 무더기가 봉긋하게 남아 여기를 무엇으로 껐는지 보이게 한다.
//
// 물·비구름이 적셔서 끈다면, 흙은 덮어서 끈다. 그래서 김이 아니라 먼지가 인다.
public class EarthCounterEffect : TrapCounterEffect
{
    [Header("흙 뿌리기")]
    [Tooltip("함정 바닥에서 흙이 쏟아지기 시작하는 높이(미터)")]
    [SerializeField] private float sprinkleHeight = 3.5f;
    [Tooltip("쏟아지는 흙기둥의 굵기(미터)")]
    [SerializeField] private float sprinkleWidth = 2.2f;
    [Tooltip("흙알갱이가 떨어지는 속도(m/s)")]
    [SerializeField] private float fallSpeed = 9f;
    [Tooltip("1초에 뿌리는 흙알갱이 수")]
    [SerializeField] private float grainRate = 400f;
    [Tooltip("흙알갱이 하나의 크기(미터)")]
    [SerializeField] private float grainSize = 0.16f;
    [SerializeField] private Color soilColor = new Color(0.42f, 0.3f, 0.18f);
    [Tooltip("불이 꺼진 뒤에도 이만큼 더 쏟아진다(초)")]
    [SerializeField] private float sprinkleAfterExtinguish = 0.7f;

    [Header("흙 무더기")]
    [Tooltip("다 쌓였을 때의 지름(미터)")]
    [SerializeField] private float moundDiameter = 2.6f;
    [Tooltip("다 쌓였을 때의 높이(미터)")]
    [SerializeField] private float moundHeight = 0.45f;
    [SerializeField] private Color moundColor = new Color(0.36f, 0.26f, 0.15f);

    [Header("흙먼지")]
    [Tooltip("불이 덮이면서 피어오르는 흙먼지")]
    [SerializeField] private bool showDust = true;
    [SerializeField] private Color dustColor = new Color(0.62f, 0.52f, 0.38f, 0.5f);

    // 붙이자마자 element가 Earth로 되어 있어야 하므로 기본값을 못박아 둔다.
    private void Reset()
    {
        element = ElementType.Earth;
    }

    protected override float OnPlay(ElementTrapCube trap)
    {
        Vector3 ground = GroundUnder(trap.transform.position);

        // 흙이 함정 높이까지 떨어지는 데 걸리는 시간. 이만큼 지나야 불을 덮는다.
        float fallTime = Mathf.Max(0.1f, sprinkleHeight) / Mathf.Max(1f, fallSpeed);

        GameObject sprinkle = BuildSprinkle(ground + Vector3.up * Mathf.Max(0.1f, sprinkleHeight));
        CounterEffectRunner.Run(BuryRoutine(sprinkle, ground, fallTime));

        return fallTime + Mathf.Max(0f, extinguishDelay);
    }

    private IEnumerator BuryRoutine(GameObject sprinkle, Vector3 ground, float fallTime)
    {
        // 1. 흙이 불까지 내려가 불이 덮일 때까지 기다린다.
        yield return new WaitForSeconds(fallTime + Mathf.Max(0f, extinguishDelay));

        if (showDust)
        {
            BuildDust(ground);
        }

        // 2. 불이 꺼지는 동안 무더기가 쌓인다. 옆으로 퍼지는 웅덩이와 달리 위로도 함께 자란다.
        Transform mound = BuildMound(ground).transform;

        float pile = Mathf.Max(0.01f, sprinkleAfterExtinguish);
        for (float t = 0f; t < pile; t += Time.deltaTime)
        {
            if (mound == null)
            {
                yield break;
            }

            SetMoundSize(mound, ground, Mathf.Lerp(0.15f, 1f, t / pile));
            yield return null;
        }

        if (mound != null)
        {
            SetMoundSize(mound, ground, 1f);
        }

        // 3. 흙 뿌리기를 멈춘다. 이미 떨어지던 알갱이는 제 수명대로 마저 떨어진다.
        if (sprinkle == null)
        {
            yield break;
        }

        foreach (ParticleSystem particles in sprinkle.GetComponentsInChildren<ParticleSystem>())
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        Destroy(sprinkle, fallTime + 1f);
    }

    // 쌓인 정도(0~1)에 맞춰 무더기 크기를 정한다.
    // 반구 대신 납작하게 누른 구를 반쯤 땅에 묻어 봉긋한 흙더미처럼 보이게 한다.
    private void SetMoundSize(Transform mound, Vector3 ground, float ratio)
    {
        float diameter = Mathf.Max(0.1f, moundDiameter) * ratio;
        float height = Mathf.Max(0.05f, moundHeight) * ratio;

        mound.localScale = new Vector3(diameter, height * 2f, diameter);
        mound.position = ground;   // 구의 한가운데를 지면에 두면 위쪽 절반만 솟는다
    }

    // 함정 위에서 아래로 쏟아지는 흙기둥.
    private GameObject BuildSprinkle(Vector3 top)
    {
        GameObject sprinkleObject = CounterEffectRunner.Track(new GameObject("SoilSprinkle"));
        sprinkleObject.transform.position = top;
        sprinkleObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // 축을 아래로 돌린다

        ParticleSystem sprinkle = sprinkleObject.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = sprinkle.main;
        main.startColor = soilColor;
        main.startSpeed = Mathf.Max(1f, fallSpeed);
        main.startLifetime = Mathf.Max(0.1f, sprinkleHeight) / Mathf.Max(1f, fallSpeed) + 0.3f;
        main.gravityModifier = 0.8f;   // 물보다 무겁게 떨어진다
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 2500;

        // 알갱이마다 크기를 조금씩 달리해야 고운 가루가 아니라 퍼 담은 흙처럼 보인다.
        float grain = Mathf.Max(0.02f, grainSize);
        main.startSize = new ParticleSystem.MinMaxCurve(grain * 0.6f, grain * 1.4f);

        ParticleSystem.EmissionModule emission = sprinkle.emission;
        emission.rateOverTime = Mathf.Max(1f, grainRate);

        // Circle은 원 안에서 바깥쪽으로 방사형으로 뿜어서 흙이 옆으로 날아간다.
        // 각도가 좁은 Cone은 원판 전체에서 거의 아래로만 쏟아진다.
        ParticleSystem.ShapeModule shape = sprinkle.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 6f;   // 살짝 벌어지게 해서 쏟아붓는 느낌을 준다
        shape.radius = Mathf.Max(0.05f, sprinkleWidth * 0.5f);
        shape.radiusThickness = 1f;

        // 흙은 물줄기와 달리 늘이지 않는다. 알갱이가 그대로 보여야 흙처럼 읽힌다.
        sprinkleObject.GetComponent<ParticleSystemRenderer>().sharedMaterial = WorldVisual.CreateLit(soilColor);

        return sprinkleObject;
    }

    private GameObject BuildMound(Vector3 ground)
    {
        GameObject mound = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        mound.name = "SoilMound";
        mound.transform.position = ground;
        mound.transform.localScale = new Vector3(0.2f, 0.1f, 0.2f);
        mound.GetComponent<Renderer>().sharedMaterial = WorldVisual.CreateLit(moundColor);

        // 꺼진 자리에 남는 장식이라 길을 막으면 안 된다.
        Destroy(mound.GetComponent<Collider>());

        return CounterEffectRunner.Track(mound);
    }

    // 불이 덮이면서 피어오르는 흙먼지.
    private void BuildDust(Vector3 ground)
    {
        GameObject dustObject = CounterEffectRunner.Track(new GameObject("SoilDust"));
        dustObject.transform.position = ground + Vector3.up * 0.15f;

        ParticleSystem dust = dustObject.AddComponent<ParticleSystem>();

        // duration/loop은 재생 중에는 바꿀 수 없다. 설정을 마친 뒤 다시 튼다.
        dust.Stop();

        ParticleSystem.MainModule main = dust.main;
        main.startColor = dustColor;
        main.startSpeed = 1.6f;
        main.startLifetime = 1.4f;
        main.startSize = 1.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;
        main.duration = Mathf.Max(0.1f, sprinkleAfterExtinguish) + 0.5f;
        main.loop = false;

        ParticleSystem.EmissionModule emission = dust.emission;
        emission.rateOverTime = 110f;

        // 김과 달리 곧게 솟기보다 바닥을 따라 옆으로 퍼져야 흙먼지처럼 보인다.
        ParticleSystem.ShapeModule shape = dust.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 55f;
        shape.radius = Mathf.Max(0.1f, moundDiameter * 0.5f);
        shape.radiusThickness = 1f;
        shape.rotation = new Vector3(-90f, 0f, 0f);   // 축을 위로 돌린다

        dustObject.GetComponent<ParticleSystemRenderer>().sharedMaterial = WorldVisual.CreateTransparentLit(dustColor);

        dust.Play();
        Destroy(dustObject, Mathf.Max(0.1f, sprinkleAfterExtinguish) + 3f);
    }
}
