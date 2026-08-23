using System.Collections;
using UnityEngine;

// 진흙으로 불 함정을 끌 때의 연출.
//
// 진흙 덩이가 불 위로 퍼지면서 불과 뒤섞여 불이 꺼지고,
// 그 뒤 열기를 머금은 진흙이 원래의 어두운 색에서 밝은 색으로 서서히 변한다.
// 꺼진 자리에는 밝아진 진흙 웅덩이가 그대로 남아, 여기를 무엇으로 껐는지 보이게 한다.
public class MudCounterEffect : TrapCounterEffect
{
    [Header("진흙 웅덩이")]
    [Tooltip("비워 두면 납작한 원반으로 웅덩이를 직접 만들어 쓴다")]
    [SerializeField] private GameObject mudPrefab;
    [Tooltip("다 퍼졌을 때의 지름(미터)")]
    [SerializeField] private float spreadDiameter = 3.2f;
    [Tooltip("웅덩이의 두께(미터)")]
    [SerializeField] private float thickness = 0.18f;
    [Tooltip("진흙이 불 위로 퍼지는 데 걸리는 시간(초)")]
    [SerializeField] private float spreadDuration = 0.45f;

    [Header("색 변화")]
    [Tooltip("퍼질 때의 색 — 기존 진흙과 같은 어두운 색")]
    [SerializeField] private Color darkColor = new Color(0.28f, 0.19f, 0.11f);
    [Tooltip("불을 삼킨 뒤 밝아진 색")]
    [SerializeField] private Color brightColor = new Color(0.78f, 0.62f, 0.38f);
    [Tooltip("어두운 색에서 밝은 색으로 변하는 데 걸리는 시간(초)")]
    [SerializeField] private float brightenDuration = 1.2f;

    [Header("보글거림")]
    [Tooltip("불이 꺼지며 진흙이 끓어오르는 김을 낸다")]
    [SerializeField] private bool showBubbles = true;

    private Material mudMaterial;

    protected override float OnPlay(ElementTrapCube trap)
    {
        Vector3 ground = GroundUnder(trap.transform.position);
        GameObject puddle = BuildPuddle(ground);

        CounterEffectRunner.Run(MergeRoutine(puddle.transform, ground));

        // 진흙이 불을 덮은 다음에 꺼진다.
        return Mathf.Max(0f, spreadDuration) + Mathf.Max(0f, extinguishDelay);
    }

    private IEnumerator MergeRoutine(Transform puddle, Vector3 ground)
    {
        // 1. 진흙이 불 위로 퍼진다.
        float spread = Mathf.Max(0.01f, spreadDuration);
        for (float t = 0f; t < spread; t += Time.deltaTime)
        {
            if (puddle == null)
            {
                yield break;
            }

            float k = t / spread;
            SetPuddleSize(puddle, Mathf.Lerp(0.2f, 1f, k));
            yield return null;
        }

        if (puddle == null)
        {
            yield break;
        }

        SetPuddleSize(puddle, 1f);

        // 2. 불이 꺼질 때까지 기다렸다가 보글거리기 시작한다.
        yield return new WaitForSeconds(Mathf.Max(0f, extinguishDelay));

        if (showBubbles)
        {
            BuildBubbles(ground);
        }

        // 3. 불을 삼킨 진흙이 서서히 밝아진다.
        float brighten = Mathf.Max(0.01f, brightenDuration);
        for (float t = 0f; t < brighten; t += Time.deltaTime)
        {
            if (puddle == null)
            {
                yield break;
            }

            WorldVisual.SetMaterialColor(mudMaterial, Color.Lerp(darkColor, brightColor, t / brighten));
            yield return null;
        }

        WorldVisual.SetMaterialColor(mudMaterial, brightColor);
    }

    // 퍼지는 정도(0~1)에 맞춰 웅덩이 크기를 정한다. 두께는 그대로 두고 옆으로만 넓어진다.
    private void SetPuddleSize(Transform puddle, float ratio)
    {
        float diameter = Mathf.Max(0.1f, spreadDiameter) * ratio;
        puddle.localScale = new Vector3(diameter, Mathf.Max(0.02f, thickness), diameter);
    }

    private GameObject BuildPuddle(Vector3 ground)
    {
        if (mudPrefab != null)
        {
            return CounterEffectRunner.Track(Instantiate(mudPrefab, ground, Quaternion.identity));
        }

        mudMaterial = WorldVisual.CreateLit(darkColor);

        // 원기둥은 기본 높이가 2라서 y 스케일을 절반으로 잡아야 두께와 맞는다.
        GameObject puddle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        puddle.name = "MudPuddle";
        puddle.transform.position = ground + Vector3.up * (thickness * 0.5f);
        puddle.transform.localScale = new Vector3(0.2f, Mathf.Max(0.01f, thickness * 0.5f), 0.2f);
        puddle.GetComponent<Renderer>().sharedMaterial = mudMaterial;
        Destroy(puddle.GetComponent<Collider>());

        return CounterEffectRunner.Track(puddle);
    }

    // 불이 꺼지며 진흙이 끓어오르는 김.
    private void BuildBubbles(Vector3 ground)
    {
        GameObject bubbleObject = CounterEffectRunner.Track(new GameObject("MudBubbles"));
        bubbleObject.transform.position = ground + Vector3.up * thickness;

        ParticleSystem bubbles = bubbleObject.AddComponent<ParticleSystem>();

        // duration/loop은 재생 중에는 바꿀 수 없다. 설정을 마친 뒤 다시 튼다.
        bubbles.Stop();

        ParticleSystem.MainModule main = bubbles.main;
        main.startColor = new Color(0.75f, 0.66f, 0.5f, 0.6f);
        main.startSpeed = 1.1f;
        main.startLifetime = 1.1f;
        main.startSize = 0.55f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 200;
        main.duration = Mathf.Max(0.1f, brightenDuration);
        main.loop = false;

        ParticleSystem.EmissionModule emission = bubbles.emission;
        emission.rateOverTime = 60f;

        // Circle은 옆으로 방사형으로 뿜는다. 축 방향으로만 올리려면 각도가 좁은 Cone을 쓴다.
        ParticleSystem.ShapeModule shape = bubbles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = spreadDiameter * 0.45f;
        shape.radiusThickness = 1f;
        shape.rotation = new Vector3(-90f, 0f, 0f);   // 축을 위로 돌린다

        bubbleObject.GetComponent<ParticleSystemRenderer>().sharedMaterial =
            WorldVisual.CreateTransparentLit(new Color(0.75f, 0.66f, 0.5f, 0.6f));

        bubbles.Play();
        Destroy(bubbleObject, Mathf.Max(0.1f, brightenDuration) + 2f);
    }
}
