using System.Collections;
using UnityEngine;

// 칼(Sword)로 모래바람을 벨 때의 연출.
//
// 상쇄 칸에 올려 둔 칼이 모래바람 앞으로 떠올라 크게 한 번 휘둘러지고,
// 그 궤적을 따라 모래바람이 좌우로 반 갈라지며 흩어져 사라진다.
//
// 휘두르는 동작은 칼 모델에 Animator가 붙어 있으면 그 트리거를 쏘고,
// 없으면 여기서 직접 호를 그리며 돌린다. (애니메이션을 나중에 붙여도 그대로 얹힌다)
public class SwordCounterEffect : TrapCounterEffect
{
    [Header("칼")]
    [Tooltip("휘두를 칼 모델. 비워두면 올려 둔 칼 원소의 World Model을 쓴다")]
    [SerializeField] private GameObject swordPrefab;
    [Tooltip("칼의 크기를 이 길이(미터)에 맞춘다")]
    [SerializeField] private float swordLength = 1.6f;
    [Tooltip("바닥에서 칼이 떠오르는 높이(미터)")]
    [SerializeField] private float swingHeight = 1.4f;
    [Tooltip("모래바람 앞 이만큼 떨어진 곳에 칼이 선다(미터). 상쇄 칸이 어디든 여기로 온다")]
    [SerializeField] private float frontDistance = 2f;

    [Header("휘두르기")]
    [Tooltip("칼 모델에 Animator가 있으면 이 트리거를 쏜다. 없으면 직접 호를 그린다")]
    [SerializeField] private string swingTriggerName = "Swing";
    [Tooltip("칼이 모래바람 앞으로 떠오르는 데 걸리는 시간(초)")]
    [SerializeField] private float riseDuration = 0.45f;
    [Tooltip("한 번 휘두르는 데 걸리는 시간(초). 애니메이션 길이에 맞춰 두면 된다")]
    [SerializeField] private float swingDuration = 0.55f;
    [Tooltip("직접 휘두를 때 그리는 호의 각도(도)")]
    [SerializeField] private float swingArc = 170f;
    [SerializeField] private AudioClip swingSound;
    [Range(0f, 1f)]
    [SerializeField] private float swingVolume = 1f;

    [Header("가르기")]
    [Tooltip("갈라진 두 쪽이 서로 벌어지는 거리(미터)")]
    [SerializeField] private float splitDistance = 3f;
    [Tooltip("갈라져 흩어지는 데 걸리는 시간(초)")]
    [SerializeField] private float splitDuration = 0.8f;
    [SerializeField] private AudioClip splitSound;
    [Range(0f, 1f)]
    [SerializeField] private float splitVolume = 1f;

    // 상쇄 칸에 올려 둔 칼. 발동하면 이 칼이 그대로 떠올라 휘둘러진다.
    private Transform placedSword;

    private void Reset()
    {
        element = ElementType.Sword;
    }

    // 모래바람이 스스로 이 연출을 달아 둘 때 쓴다.
    // Reset()은 인스펙터에서 손으로 붙일 때만 불리므로, 코드로 붙일 때는 여기서 맞춰 준다.
    public void Setup()
    {
        element = ElementType.Sword;
    }

    private Sandstorm ResolveStorm()
    {
        return transform.root.GetComponentInChildren<Sandstorm>(true);
    }

    // 상쇄 칸에 칼을 올려 두면, 칸이 어디든 칼은 언제나 모래바람 "앞"에 선다.
    // 벨 대상 앞에 자리를 잡고 있어야 다음에 무슨 일이 일어날지 한눈에 보인다.
    public override Transform CreatePlacedVisual(Vector3 trapPosition)
    {
        Sandstorm storm = ResolveStorm();
        Vector3 spot = storm != null
            ? GroundUnder(storm.transform.position + FrontOfStorm(storm.transform.position) * Mathf.Max(0f, frontDistance))
            : GroundUnder(trapPosition);

        placedSword = BuildSword(spot);

        // 칼끝이 모래바람을 겨누게 세워 둔다.
        if (placedSword != null && storm != null)
        {
            Vector3 toStorm = Vector3.ProjectOnPlane(storm.transform.position - placedSword.position, Vector3.up);
            if (toStorm.sqrMagnitude > 0.0001f)
            {
                placedSword.rotation = Quaternion.LookRotation(toStorm.normalized, Vector3.up);
            }
        }

        return placedSword;
    }

    // 모래바람의 "앞" = 플레이어가 보는 쪽. 뒤에 세우면 소용돌이에 가려 보이지 않는다.
    private static Vector3 FrontOfStorm(Vector3 stormPosition)
    {
        Transform player = PlayerLocator.FindPlayer();

        if (player != null)
        {
            Vector3 toPlayer = Vector3.ProjectOnPlane(player.position - stormPosition, Vector3.up);

            if (toPlayer.sqrMagnitude > 0.0001f)
            {
                return toPlayer.normalized;
            }
        }

        return Vector3.back;
    }

    protected override float OnPlay(ElementTrapCube trap)
    {
        Sandstorm storm = ResolveStorm();

        if (storm == null)
        {
            Debug.LogWarning($"{name}: 벨 모래바람을 찾지 못해 칼 연출을 건너뜁니다.", this);
            return extinguishDelay;
        }

        // 올려 둔 칼이 있으면 그것이 그대로 떠오른다. 없을 때만 새로 만든다.
        // (죽어서 표시가 지워졌다면 placedSword는 파괴된 참조라 null로 걸러진다)
        Transform sword = placedSword;

        if (sword != null)
        {
            // 상쇄 표시는 곧 지워진다. 그 자식으로 남아 있으면 칼도 같이 사라진다.
            sword.SetParent(null, true);
            placedSword = null;
        }
        else
        {
            sword = BuildSword(GroundUnder(trap.transform.position));
        }

        if (sword == null)
        {
            return extinguishDelay;
        }

        CounterEffectRunner.Track(sword.gameObject);
        CounterEffectRunner.Run(SlashRoutine(sword, storm.transform));

        return Mathf.Max(0f, riseDuration) + Mathf.Max(0f, swingDuration) + Mathf.Max(0f, splitDuration);
    }

    // ───────────────────────────── 칼 만들기 ─────────────────────────────

    private Transform BuildSword(Vector3 groundPosition)
    {
        GameObject model = swordPrefab;

        // 인스펙터에 따로 물려 두지 않았으면 칼 원소가 들고 있는 모델을 쓴다.
        // 그래야 "칼은 이렇게 생겼다"를 ElementData 한 곳에만 적어 두면 된다.
        if (model == null)
        {
            model = FindSwordModel();
        }

        if (model == null)
        {
            Debug.LogWarning(
                $"{name}: 휘두를 칼 모델이 없습니다. swordPrefab 칸에 sword.fbx를 물리거나, " +
                "Sword 원소(ElementData)의 World Model을 채워 주세요.", this);
            return null;
        }

        GameObject sword = Instantiate(model);
        sword.name = "SwordSlash";
        sword.transform.SetPositionAndRotation(groundPosition, Quaternion.identity);
        sword.transform.localScale = Vector3.one;

        // fbx는 단위에 따라 크기가 제각각이다. 제일 긴 축을 칼 길이로 보고 맞춘다.
        Bounds bounds = ElementVisual.MeasureBounds(sword);
        float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (longest > 0.0001f)
        {
            sword.transform.localScale = Vector3.one * (Mathf.Max(0.1f, swordLength) / longest);
        }

        foreach (Collider blade in sword.GetComponentsInChildren<Collider>(true))
        {
            Destroy(blade);
        }

        return sword.transform;
    }

    // 상쇄 칸이 받아 주는 원소 중 칼을 찾아 그 모델을 꺼내 쓴다.
    private GameObject FindSwordModel()
    {
        foreach (CraftingPanelUI panel in FindObjectsByType<CraftingPanelUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            ElementData sword = panel.FindElementOfType(ElementType.Sword);
            if (sword != null && sword.WorldModel != null)
            {
                return sword.WorldModel;
            }
        }

        return null;
    }

    // ───────────────────────────── 연출 ─────────────────────────────

    private IEnumerator SlashRoutine(Transform sword, Transform storm)
    {
        // 플레이어가 보는 쪽을 가로지르며 베도록, 플레이어→모래바람 축의 직각을 칼날이 지나는 방향으로 삼는다.
        Vector3 slashAxis = SideDirection(storm.position);
        Vector3 swingSpot = storm.position + Vector3.up * Mathf.Max(0.1f, swingHeight);

        // 1) 모래바람 앞으로 떠오른다.
        Vector3 from = sword.position;
        Quaternion ready = Quaternion.LookRotation(slashAxis, Vector3.up);
        float rise = Mathf.Max(0.01f, riseDuration);

        for (float elapsed = 0f; elapsed < rise; elapsed += Time.deltaTime)
        {
            if (sword == null)
            {
                yield break;
            }

            float k = elapsed / rise;
            sword.position = Vector3.Lerp(from, swingSpot, Mathf.SmoothStep(0f, 1f, k));
            sword.rotation = Quaternion.Slerp(Quaternion.identity, ready, k);
            yield return null;
        }

        if (sword == null)
        {
            yield break;
        }

        sword.SetPositionAndRotation(swingSpot, ready);

        // 2) 휘두른다. 준비된 애니메이션이 있으면 그것을 재생하고, 없으면 직접 호를 그린다.
        SfxPlayer.PlayAt(swingSound, swingSpot, swingVolume);
        yield return SwingRoutine(sword, slashAxis);

        // 3) 벤 자리를 따라 모래바람이 반으로 갈라진다.
        SfxPlayer.PlayAt(splitSound, storm.position, splitVolume);
        yield return SplitRoutine(storm, slashAxis);

        if (sword != null)
        {
            Destroy(sword.gameObject);
        }
    }

    private IEnumerator SwingRoutine(Transform sword, Vector3 slashAxis)
    {
        float duration = Mathf.Max(0.01f, swingDuration);
        Animator animator = sword.GetComponentInChildren<Animator>();

        // 칼 모델에 준비된 휘두르기 애니메이션이 있으면 그쪽에 맡긴다.
        if (animator != null && !string.IsNullOrEmpty(swingTriggerName))
        {
            animator.SetTrigger(swingTriggerName);
            yield return new WaitForSeconds(duration);
            yield break;
        }

        // 없으면 여기서 직접 휘두른다.
        //
        // 칼 자신을 제자리에서 돌리면 원점(보통 칼자루)에서 빙그르 도는 것으로만 보여
        // "휘둘렀다"는 느낌이 나지 않는다. 손목에 해당하는 축을 하나 세우고 칼을 거기 매달아
        // 축을 돌려야 칼끝이 큰 호를 그린다.
        Vector3 pivotAxis = Vector3.Cross(slashAxis, Vector3.up).normalized;
        if (pivotAxis.sqrMagnitude < 0.0001f)
        {
            pivotAxis = Vector3.forward;
        }

        GameObject pivotObject = CounterEffectRunner.Track(new GameObject("SwordPivot"));
        Transform pivot = pivotObject.transform;
        pivot.position = sword.position;

        Transform originalParent = sword.parent;
        sword.SetParent(pivot, true);

        // 칼자루를 축에 걸고 칼날이 바깥을 향하게 밀어 둔다. 그래야 호가 칼 길이만큼 커진다.
        sword.localPosition = Vector3.up * (Mathf.Max(0.1f, swordLength) * 0.6f);

        float half = Mathf.Max(0.1f, swingArc) * 0.5f;
        Quaternion start = pivot.rotation;

        // 살짝 뜸을 들여 뒤로 젖혔다가, 나머지 시간 동안 앞으로 후려친다.
        const float windUpShare = 0.3f;
        float windUp = duration * windUpShare;
        float strike = duration - windUp;

        for (float elapsed = 0f; elapsed < windUp; elapsed += Time.deltaTime)
        {
            if (sword == null)
            {
                yield break;
            }

            pivot.rotation = Quaternion.AngleAxis(Mathf.Lerp(0f, -half, elapsed / windUp), pivotAxis) * start;
            yield return null;
        }

        for (float elapsed = 0f; elapsed < strike; elapsed += Time.deltaTime)
        {
            if (sword == null)
            {
                yield break;
            }

            // 후려치는 쪽은 가속한다. 끝에서 탁 멈춰야 베인 느낌이 난다.
            float k = elapsed / strike;
            pivot.rotation = Quaternion.AngleAxis(Mathf.Lerp(-half, half, k * k), pivotAxis) * start;
            yield return null;
        }

        // 다 휘둘렀으면 칼을 축에서 떼어 낸다. 축은 여기서 할 일이 끝났다.
        if (sword != null)
        {
            sword.SetParent(originalParent, true);
        }

        if (pivotObject != null)
        {
            Destroy(pivotObject);
        }
    }

    // 모래바람을 좌우 두 쪽으로 복제해 서로 벌어지게 하고, 원본은 감춘다.
    // 파티클을 진짜로 자를 수는 없으니, 같은 연출 두 개가 갈라져 흩어지는 것으로 대신한다.
    private IEnumerator SplitRoutine(Transform storm, Vector3 slashAxis)
    {
        Sandstorm source = storm.GetComponent<Sandstorm>();

        // 흰 폭풍과 같은 복제 헬퍼를 쓴다. 흰색을 곱하면 원본 색이 그대로 나온다.
        Transform left = WindStormVisual.CreateWhiteCopy(source, storm.position, 0.55f, Color.white);
        Transform right = WindStormVisual.CreateWhiteCopy(source, storm.position, 0.55f, Color.white);

        // 원본은 감춘다. 함정이 곧 통째로 끄지만, 갈라지는 동안에는 두 쪽만 보여야 한다.
        SetRenderersEnabled(storm, false);

        if (left != null)
        {
            CounterEffectRunner.Track(left.gameObject);
            WindStormVisual.StopEmitting(left);
        }

        if (right != null)
        {
            CounterEffectRunner.Track(right.gameObject);
            WindStormVisual.StopEmitting(right);
        }

        Vector3 apart = Vector3.Cross(slashAxis, Vector3.up).normalized * Mathf.Max(0.1f, splitDistance) * 0.5f;
        Vector3 center = storm.position;
        float duration = Mathf.Max(0.01f, splitDuration);
        Vector3 leftScale = left != null ? left.localScale : Vector3.one;
        Vector3 rightScale = right != null ? right.localScale : Vector3.one;

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float k = elapsed / duration;

            // 벌어지면서 기울고, 점점 작아지며 흩어진다.
            if (left != null)
            {
                left.position = center - apart * k;
                left.localScale = leftScale * (1f - k);
                left.rotation = Quaternion.AngleAxis(-25f * k, slashAxis);
            }

            if (right != null)
            {
                right.position = center + apart * k;
                right.localScale = rightScale * (1f - k);
                right.rotation = Quaternion.AngleAxis(25f * k, slashAxis);
            }

            yield return null;
        }

        if (left != null)
        {
            Destroy(left.gameObject);
        }

        if (right != null)
        {
            Destroy(right.gameObject);
        }

        // 원본은 되살릴 수 있어야 하므로 렌더러를 도로 켜 둔다. 끄는 것은 함정이 맡는다.
        SetRenderersEnabled(storm, true);
    }

    private static void SetRenderersEnabled(Transform root, bool enabled)
    {
        if (root == null)
        {
            return;
        }

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = enabled;
        }
    }

    // 플레이어가 보는 쪽을 가로지르는 방향. 정면이나 뒤로 베면 갈라지는 것이 보이지 않는다.
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
}
