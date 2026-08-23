using System.Collections;
using UnityEngine;

// 늪(Swamp)으로 모래바람을 끌 때의 연출.
//
// 모래바람 발밑에 늪이 번지고, 소용돌이가 그 안으로 빨려 들어가 잠긴다.
// 다만 늪은 사라지지 않고 그 자리에 남는다 — 플레이어가 밟으면 같이 빨려 들어가 판이 끝난다.
// (함정을 끄는 데는 성공하지만 새 함정을 하나 만드는 셈이다)
public class SwampCounterEffect : TrapCounterEffect
{
    [Header("늪")]
    [Tooltip("늪이 번지는 데 걸리는 시간(초)")]
    [SerializeField] private float spreadDuration = 0.5f;
    [Tooltip("다 번졌을 때의 지름(미터)")]
    [SerializeField] private float swampDiameter = 3.4f;
    [Tooltip("늪의 두께(미터)")]
    [SerializeField] private float swampThickness = 0.14f;
    [SerializeField] private Color swampColor = new Color(0.20f, 0.24f, 0.14f, 1f);

    [Header("빨아들이기")]
    [Tooltip("모래바람이 늪 속으로 잠기는 데 걸리는 시간(초)")]
    [SerializeField] private float swallowDuration = 1.1f;
    [Tooltip("잠길 때 얼마나 깊이 내려가는지(미터)")]
    [SerializeField] private float swallowDepth = 3f;
    [SerializeField] private AudioClip swallowSound;
    [Range(0f, 1f)]
    [SerializeField] private float swallowVolume = 1f;

    [Header("남는 늪")]
    [Tooltip("모래바람을 삼킨 뒤에도 늪을 남겨 둔다. 플레이어가 밟으면 빨려 들어간다")]
    [SerializeField] private bool leaveSwampBehind = true;

    private void Reset()
    {
        element = ElementType.Swamp;
    }

    // 모래바람이 스스로 이 연출을 달아 둘 때 쓴다(Reset은 인스펙터에서 붙일 때만 불린다).
    public void Setup()
    {
        element = ElementType.Swamp;
    }

    protected override float OnPlay(ElementTrapCube trap)
    {
        Sandstorm storm = transform.root.GetComponentInChildren<Sandstorm>(true);

        if (storm == null)
        {
            Debug.LogWarning($"{name}: 삼킬 모래바람을 찾지 못해 늪 연출을 건너뜁니다.", this);
            return extinguishDelay;
        }

        Vector3 ground = GroundUnder(storm.transform.position);
        Transform swamp = BuildSwamp(ground);

        CounterEffectRunner.Run(SwallowRoutine(swamp, storm.transform, ground));

        return Mathf.Max(0f, spreadDuration) + Mathf.Max(0f, swallowDuration);
    }

    // 바닥에 깔리는 늪. 물 함정의 웅덩이와 같은 납작한 원기둥이다.
    private Transform BuildSwamp(Vector3 ground)
    {
        Material material = WorldVisual.CreateLit(swampColor);

        // 늪은 젖은 진창이라 살짝 번들거려야 한다.
        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.55f);
        }

        // 원기둥 프리미티브는 세로 2칸짜리라 scale.y가 곧 반두께다.
        GameObject swampObject = WorldVisual.CreateCylinder(
            "Swamp",
            null,
            Vector3.zero,
            new Vector3(swampDiameter, Mathf.Max(0.01f, swampThickness) * 0.5f, swampDiameter),
            material);

        swampObject.transform.position = ground;

        // 연출이 도는 동안 함정이 통째로 꺼져도 늪은 남아야 한다.
        CounterEffectRunner.Track(swampObject);

        // 프리미티브에 딸려 오는 콜라이더는 길을 막는다. 밟힘 판정은 SwampPit이 따로 만든다.
        Collider solid = swampObject.GetComponent<Collider>();
        if (solid != null)
        {
            Destroy(solid);
        }

        return swampObject.transform;
    }

    private IEnumerator SwallowRoutine(Transform swamp, Transform storm, Vector3 ground)
    {
        // 1) 발밑에서 늪이 번진다.
        Vector3 fullScale = swamp.localScale;
        float spread = Mathf.Max(0.01f, spreadDuration);

        for (float elapsed = 0f; elapsed < spread; elapsed += Time.deltaTime)
        {
            if (swamp == null)
            {
                yield break;
            }

            // 가운데서 바깥으로 퍼진다. 두께는 그대로 두고 지름만 키운다.
            float k = elapsed / spread;
            swamp.localScale = new Vector3(fullScale.x * k, fullScale.y, fullScale.z * k);
            yield return null;
        }

        if (swamp == null)
        {
            yield break;
        }

        swamp.localScale = fullScale;

        // 2) 모래바람이 늪 속으로 빨려 들어간다.
        SfxPlayer.PlayAt(swallowSound, ground, swallowVolume);
        WindStormVisual.StopEmitting(storm);

        Vector3 stormStart = storm != null ? storm.position : ground;
        Vector3 stormScale = storm != null ? storm.localScale : Vector3.one;
        float swallow = Mathf.Max(0.01f, swallowDuration);

        for (float elapsed = 0f; elapsed < swallow; elapsed += Time.deltaTime)
        {
            if (storm == null)
            {
                break;
            }

            // 아래로 잠기면서 오므라들고, 잠길수록 빨리 빨려 든다.
            float k = elapsed / swallow;
            float pull = k * k;

            storm.position = stormStart + Vector3.down * (swallowDepth * pull);
            storm.localScale = stormScale * (1f - pull);

            // 소용돌이가 빨려 들어가는 것처럼 늪도 함께 돈다.
            swamp.Rotate(Vector3.up, 180f * Time.deltaTime, Space.World);
            yield return null;
        }

        // 모래바람은 되돌릴 수 있어야 한다. 자리와 크기를 돌려놓고 끄는 것은 함정에게 맡긴다.
        if (storm != null)
        {
            storm.position = stormStart;
            storm.localScale = stormScale;
        }

        // 3) 늪은 남는다. 이제부터 밟으면 빨려 들어간다.
        if (leaveSwampBehind)
        {
            BuildPit(ground);
        }
        else
        {
            Destroy(swamp.gameObject);
        }
    }

    // 밟힘 판정을 세운다.
    //
    // 빨려 들어가는 처리(사망 → 콜라이더를 트리거로 → 늪 속도로 가라앉히기 → 실패 패널)는
    // 이미 SwampPit이 다 하고 있으므로 그대로 쓴다. 늪 원소를 도로에 놓았을 때와 같은 최후다.
    //
    // 늪 연출은 지름에 맞춰 크기를 조절해 두어서, 그 자식으로 두면 판정까지 같이 찌그러진다.
    // 크기가 걸리지 않은 별도 오브젝트로 세운다.
    private void BuildPit(Vector3 ground)
    {
        GameObject pit = CounterEffectRunner.Track(new GameObject("SwampPit"));
        pit.transform.position = ground;

        // SwampPit은 Collider를 요구하므로 먼저 달아 준다.
        SphereCollider zone = pit.AddComponent<SphereCollider>();
        zone.isTrigger = true;
        zone.radius = Mathf.Max(0.1f, swampDiameter) * 0.5f;

        pit.AddComponent<SwampPit>();
    }
}
