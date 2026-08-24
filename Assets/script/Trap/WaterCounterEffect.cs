using System.Collections;
using UnityEngine;

// 물로 불 함정을 끌 때의 연출.
//
// 함정 바로 위에서 물줄기가 쏟아져 내려 불을 때린다.
// 물이 불에 닿으면 흰 김이 피어오르며 불이 잦아들고,
// 다 꺼진 자리에는 얕은 물웅덩이가 남아 여기를 무엇으로 껐는지 보이게 한다.
//
// 비구름(RainCloudCounterEffect)이 넓게 흩뿌리는 비라면, 이쪽은 한 점에 내리꽂는 물줄기다.
public class WaterCounterEffect : TrapCounterEffect
{
    [Header("물줄기")]
    [Tooltip("함정 바닥에서 물이 쏟아지기 시작하는 높이(미터)")]
    [SerializeField] private float pourHeight = 4f;
    [Tooltip("물줄기의 굵기(미터)")]
    [SerializeField] private float streamWidth = 1.4f;
    [SerializeField] private Color waterColor = new Color(0.35f, 0.7f, 1f, 0.8f);
    [Tooltip("물이 떨어지는 속도(m/s)")]
    [SerializeField] private float fallSpeed = 14f;
    [Tooltip("1초에 뿌리는 물방울 수")]
    [SerializeField] private float pourRate = 500f;
    [Tooltip("불이 꺼진 뒤에도 이만큼 더 쏟아진다(초)")]
    [SerializeField] private float pourAfterExtinguish = 0.8f;

    [Header("물웅덩이")]
    [Tooltip("다 고였을 때의 지름(미터)")]
    [SerializeField] private float puddleDiameter = 2.8f;
    [Tooltip("웅덩이의 두께(미터)")]
    [SerializeField] private float puddleThickness = 0.08f;
    [SerializeField] private Color puddleColor = new Color(0.25f, 0.55f, 0.85f, 0.75f);

    [Header("김(수증기)")]
    [Tooltip("불이 꺼질 때 피어오르는 흰 김")]
    [SerializeField] private bool showSteam = true;
    [SerializeField] private Color steamColor = new Color(0.95f, 0.95f, 0.95f, 0.6f);

    // 붙이자마자 element가 Water로 되어 있어야 하지만, 기본값이 바뀌어도 어긋나지 않게 못박아 둔다.
    private void Reset()
    {
        element = ElementType.Water;
    }

    protected override float OnPlay(ElementTrapCube trap)
    {
        Vector3 ground = GroundUnder(trap.transform.position);

        // 물이 함정 높이까지 떨어지는 데 걸리는 시간. 이만큼 지나야 불에 닿는다.
        float fallTime = Mathf.Max(0.1f, pourHeight) / Mathf.Max(1f, fallSpeed);

        GameObject stream = BuildStream(ground + Vector3.up * Mathf.Max(0.1f, pourHeight));
        CounterEffectRunner.Run(PourRoutine(stream, ground, fallTime));

        return fallTime + Mathf.Max(0f, extinguishDelay);
    }

    private IEnumerator PourRoutine(GameObject stream, Vector3 ground, float fallTime)
    {
        // 1. 물줄기가 불까지 내려가 불이 꺼질 때까지 기다린다.
        yield return new WaitForSeconds(fallTime + Mathf.Max(0f, extinguishDelay));

        if (showSteam)
        {
            BuildSteam(ground);
        }

        // 2. 불이 꺼지는 동안 웅덩이가 고이기 시작한다.
        Transform puddle = BuildPuddle(ground).transform;

        float pour = Mathf.Max(0.01f, pourAfterExtinguish);
        for (float t = 0f; t < pour; t += Time.deltaTime)
        {
            if (puddle == null)
            {
                yield break;
            }

            SetPuddleSize(puddle, Mathf.Lerp(0.2f, 1f, t / pour));
            yield return null;
        }

        if (puddle != null)
        {
            SetPuddleSize(puddle, 1f);
        }

        // 3. 물줄기를 잠근다. 이미 떨어지던 물방울은 제 수명대로 마저 떨어진다.
        if (stream == null)
        {
            yield break;
        }

        foreach (ParticleSystem particles in stream.GetComponentsInChildren<ParticleSystem>())
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        Destroy(stream, fallTime + 1f);
    }

    // 고인 정도(0~1)에 맞춰 웅덩이 크기를 정한다. 두께는 그대로 두고 옆으로만 넓어진다.
    private void SetPuddleSize(Transform puddle, float ratio)
    {
        float diameter = Mathf.Max(0.1f, puddleDiameter) * ratio;

        // 원기둥은 기본 높이가 2라서 y 스케일을 절반으로 잡아야 두께와 맞는다.
        puddle.localScale = new Vector3(diameter, Mathf.Max(0.01f, puddleThickness * 0.5f), diameter);
    }

    // 함정 위에서 아래로 내리꽂는 물줄기.
    private GameObject BuildStream(Vector3 top)
    {
        GameObject streamObject = CounterEffectRunner.Track(new GameObject("WaterStream"));
        streamObject.transform.position = top;
        streamObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // 축을 아래로 돌린다

        ParticleSystem stream = streamObject.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = stream.main;
        main.startColor = waterColor;
        main.startSpeed = Mathf.Max(1f, fallSpeed);
        main.startLifetime = Mathf.Max(0.1f, pourHeight) / Mathf.Max(1f, fallSpeed) + 0.3f;
        main.startSize = 0.14f;
        main.gravityModifier = 0.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 3000;

        ParticleSystem.EmissionModule emission = stream.emission;
        emission.rateOverTime = Mathf.Max(1f, pourRate);

        // Circle은 원 안에서 바깥쪽으로 방사형으로 뿜어서 물이 옆으로 흩어진다.
        // 각도 0짜리 Cone은 원판 전체에서 축 방향(= 이 오브젝트의 +Z, 곧 아래쪽)으로만 쏜다.
        ParticleSystem.ShapeModule shape = stream.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 0f;
        shape.radius = Mathf.Max(0.05f, streamWidth * 0.5f);
        shape.radiusThickness = 1f;

        // 물방울은 떨어지는 방향으로 길게 늘여야 물줄기처럼 보인다.
        ParticleSystemRenderer renderer = streamObject.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 5f;
        renderer.sharedMaterial = WorldVisual.CreateTransparentLit(waterColor);

        return streamObject;
    }

    private GameObject BuildPuddle(Vector3 ground)
    {
        GameObject puddle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        puddle.name = "WaterPuddle";
        puddle.transform.position = ground + Vector3.up * (puddleThickness * 0.5f);
        puddle.transform.localScale = new Vector3(0.2f, Mathf.Max(0.01f, puddleThickness * 0.5f), 0.2f);
        puddle.GetComponent<Renderer>().sharedMaterial = WorldVisual.CreateTransparentLit(puddleColor);

        // 꺼진 자리에 남는 장식이라 길을 막으면 안 된다.
        Destroy(puddle.GetComponent<Collider>());

        return CounterEffectRunner.Track(puddle);
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
        main.startSpeed = 2.4f;
        main.startLifetime = 1.6f;
        main.startSize = 1.3f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;
        main.duration = Mathf.Max(0.1f, pourAfterExtinguish) + 0.6f;
        main.loop = false;

        ParticleSystem.EmissionModule emission = steam.emission;
        emission.rateOverTime = 140f;

        // 물줄기와 같은 이유로 Cone을 쓴다. 살짝 벌어지게 해서 김이 퍼지며 오른다.
        ParticleSystem.ShapeModule shape = steam.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 20f;
        shape.radius = Mathf.Max(0.1f, streamWidth * 0.6f);
        shape.radiusThickness = 1f;
        shape.rotation = new Vector3(-90f, 0f, 0f);   // 축을 위로 돌린다

        steamObject.GetComponent<ParticleSystemRenderer>().sharedMaterial = WorldVisual.CreateTransparentLit(steamColor);

        steam.Play();
        Destroy(steamObject, main.duration + 2.5f);
    }
}
