using System.Collections;
using UnityEngine;

// 바람(Wind)으로 모래바람을 끌 때의 연출.
//
// 옆에서 흰 폭풍이 불어와 모래바람과 겹치고, 둘이 만나는 순간 서로를 지우며 함께 잦아든다.
// (상쇄 칸의 '대상 원소'를 Wind로 맞춰 두어야 이 연출이 걸린다)
public class WindCounterEffect : TrapCounterEffect
{
    [Header("흰 폭풍")]
    [Tooltip("색을 베낄 모래바람. 비워두면 함정이 속한 뿌리에서 찾는다")]
    [SerializeField] private Sandstorm targetStorm;
    [Tooltip("흰 폭풍이 나타나는 거리. 모래바람에서 옆으로 이만큼 떨어진 곳에서 불어온다(미터)")]
    [SerializeField] private float approachDistance = 7f;
    [Tooltip("불어와서 모래바람과 겹치기까지 걸리는 시간(초)")]
    [SerializeField] private float approachDuration = 1f;
    [Tooltip("원본 모래바람 대비 흰 폭풍의 크기")]
    [SerializeField] private float windScale = 1f;
    [Tooltip("흰 폭풍의 색. 모래 텍스처가 진하면 1보다 밝게 올리면 더 하얘진다")]
    [ColorUsage(true, true)]
    [SerializeField] private Color windColor = Color.white;

    [Header("사그라들기")]
    [Tooltip("둘이 만난 뒤 함께 잦아드는 데 걸리는 시간(초)")]
    [SerializeField] private float fadeDuration = 0.9f;

    // 컴포넌트를 붙이는 순간 대상 원소를 바람으로 맞춰 둔다. 기본값(Water)인 채로 두면 연출이 안 걸린다.
    private void Reset()
    {
        element = ElementType.Storm;
    }

    // 모래바람이 스스로 이 연출을 달아 둘 때 쓴다.
    // 씬에서 손으로 붙이고 '대상 원소'까지 맞춰 주지 않아도 바람 상쇄가 동작해야 한다.
    public void Setup(Sandstorm storm)
    {
        element = ElementType.Storm;
        targetStorm = storm;
    }

    // 올려 둔 표시로 쓰이는 흰 폭풍. 발동할 때 이것이 그대로 모래바람에게 불어간다.
    private Transform placedVisual;

    private Sandstorm ResolveStorm()
    {
        if (targetStorm != null)
        {
            return targetStorm;
        }

        return transform.root.GetComponentInChildren<Sandstorm>(true);
    }

    // 상쇄 칸에 바람을 올려 두면 그 자리에 흰 폭풍이 선다(기본 구 대신).
    public override Transform CreatePlacedVisual(Vector3 trapPosition)
    {
        Sandstorm storm = ResolveStorm();
        if (storm == null)
        {
            return null;
        }

        placedVisual = WindStormVisual.CreateWhiteCopy(storm, GroundUnder(trapPosition), windScale, windColor);
        return placedVisual;
    }

    protected override float OnPlay(ElementTrapCube trap)
    {
        Sandstorm storm = ResolveStorm();

        if (storm == null)
        {
            Debug.LogWarning($"{name}: 끌 모래바람을 찾지 못해 흰 폭풍 연출을 건너뜁니다.", this);
            return extinguishDelay;
        }

        Vector3 meetingPoint = storm.transform.position;

        // 올려 둔 흰 폭풍이 있으면 그것이 그대로 불어간다. 없을 때만 옆에서 새로 불러온다.
        // (죽어서 표시가 지워졌다면 placedVisual은 파괴된 참조라 null로 걸러진다)
        Transform wind = placedVisual;

        if (wind != null)
        {
            // 상쇄 표시는 곧 지워진다. 그 자식으로 남아 있으면 폭풍도 같이 사라진다.
            wind.SetParent(null, true);
            placedVisual = null;
        }
        else
        {
            Vector3 from = meetingPoint + SideDirection(meetingPoint) * Mathf.Max(0.1f, approachDistance);
            wind = WindStormVisual.CreateWhiteCopy(storm, from, windScale, windColor);
        }

        if (wind == null)
        {
            return extinguishDelay;
        }

        CounterEffectRunner.Track(wind.gameObject);
        CounterEffectRunner.Run(MeetRoutine(wind, storm.transform, meetingPoint));

        // 흰 폭풍이 닿고 둘이 함께 잦아들 때까지 모래바람을 남겨 둔다.
        return Mathf.Max(0f, approachDuration) + Mathf.Max(0f, fadeDuration);
    }

    // 플레이어가 보는 쪽을 가로지르며 들어오도록, 플레이어→모래바람 축의 직각 방향에서 불어온다.
    // 플레이어 뒤나 정면에서 오면 원근 때문에 다가오는 것이 잘 보이지 않는다.
    private static Vector3 SideDirection(Vector3 stormPosition)
    {
        Transform player = PlayerLocator.FindPlayer();

        if (player != null)
        {
            Vector3 toStorm = Vector3.ProjectOnPlane(stormPosition - player.position, Vector3.up);

            if (toStorm.sqrMagnitude > 0.0001f)
            {
                return Vector3.Cross(Vector3.up, toStorm.normalized);
            }
        }

        return Vector3.right;
    }

    private IEnumerator MeetRoutine(Transform wind, Transform storm, Vector3 meetingPoint)
    {
        Vector3 from = wind.position;
        float approach = Mathf.Max(0.01f, approachDuration);

        // 멀리서 밀려온다. 가까워질수록 빨라져 덮치는 느낌을 준다.
        for (float elapsed = 0f; elapsed < approach; elapsed += Time.deltaTime)
        {
            if (wind == null)
            {
                yield break;
            }

            float k = elapsed / approach;
            wind.position = Vector3.Lerp(from, meetingPoint, k * k);
            yield return null;
        }

        if (wind == null)
        {
            yield break;
        }

        wind.position = meetingPoint;

        // 만났다. 둘 다 새로 뿜는 것을 멈추고 함께 잦아든다.
        WindStormVisual.StopEmitting(wind);
        WindStormVisual.StopEmitting(storm);

        Vector3 windScaleStart = wind.localScale;
        Vector3 stormScaleStart = storm != null ? storm.localScale : Vector3.one;
        float fade = Mathf.Max(0.01f, fadeDuration);

        for (float elapsed = 0f; elapsed < fade; elapsed += Time.deltaTime)
        {
            float k = 1f - elapsed / fade;

            if (wind != null)
            {
                wind.localScale = windScaleStart * k;
            }

            // 모래바람은 곧 함정이 통째로 끄지만, 그 전까지 같이 오므라들어야 서로 지운 것처럼 보인다.
            if (storm != null)
            {
                storm.localScale = stormScaleStart * k;
            }

            yield return null;
        }

        // 모래바람은 되돌릴 수 있어야 하므로 크기만 원래대로 돌려놓는다. 끄는 것은 함정이 맡는다.
        if (storm != null)
        {
            storm.localScale = stormScaleStart;
        }

        if (wind != null)
        {
            Destroy(wind.gameObject);
        }
    }
}
