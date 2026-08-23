using UnityEngine;

// 플레이어와 함정의 머리 위에 떠서 아래(↓)를 가리키는 3D 화살표 표시.
//
// 카메라를 뒤로 빼거나 탑뷰로 내려다보면 걸어가는 플레이어와 길 위의 함정이
// 배경 지형에 묻혀 잘 보이지 않는다. 이 표시가 정수리 위에서 위아래로 살살 떠다니기 때문에
// 어느 시점에서도 눈에 띈다.
//
// 표시할 대상의 "자식"으로 두기만 하면 된다. 위치·회전은 부모-자식 관계로 자동으로 따라가고,
// 높이·크기·색은 이 스크립트가 붙는 순간 대상을 보고 알아서 잡는다.
// 그래서 Assets/Prefab/ArrowMarker.prefab을 하이어라키의 아무 오브젝트 위에 끌어다 놓으면 끝이다.
//
// 프리팹을 만들거나 여러 오브젝트에 한 번에 붙이려면 ArrowMarkerCreator(Tools > Molra)를 쓴다.
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshRenderer))]
public class TargetArrowMarker : MonoBehaviour
{
    [Header("높이")]
    [Tooltip("켜면 대상(부모)의 렌더러 크기를 재서 정수리 높이를 자동으로 잡는다")]
    [SerializeField] private bool autoHeight = true;
    [Tooltip("정수리에서 화살표 끝까지 띄울 거리(미터)")]
    [SerializeField] private float gap = 0.4f;
    [Tooltip("autoHeight를 끄거나 렌더러를 못 찾았을 때 쓸 높이(미터)")]
    [SerializeField] private float manualHeight = 2.2f;

    [Header("크기")]
    [Tooltip("부모가 줄어 있어도(FireTrap은 0.28배) 화살표가 늘 이 크기(미터)로 보이게 맞춘다. " +
             "0으로 두면 손으로 정한 Scale을 그대로 쓴다")]
    [SerializeField] private float worldSize = 0.7f;

    [Header("위아래 움직임")]
    [Tooltip("위아래로 떠다니는 폭(미터). 0이면 가만히 있는다")]
    [SerializeField] private float bobDistance = 0.15f;
    [Tooltip("한 번 오르내리는 빠르기. 작을수록 살살 움직인다")]
    [SerializeField] private float bobSpeed = 1.6f;
    [Tooltip("끝에서 잠깐 머무르며 부드럽게 방향을 바꾼다. 끄면 일정한 속도로 오르내린다")]
    [SerializeField] private bool easeBob = true;

    [Header("회전")]
    [Tooltip("초당 도는 각도. 0이면 돌지 않고 위아래로만 움직인다")]
    [SerializeField] private float spinSpeed = 0f;

    [Header("색")]
    [Tooltip("켜면 대상을 보고 자동으로 색을 고른다 (플레이어=노랑, 함정=빨강, 그 밖=하늘). " +
             "아래 색을 인스펙터에서 직접 정하려면 이 옵션을 꺼야 한다. 켜져 있으면 아래 색은 계속 자동 색으로 되돌아간다")]
    [SerializeField] private bool autoColor = true;
    [Tooltip("이 화살표의 색. autoColor가 켜져 있는 동안은 여기서 바꿔도 곧바로 자동 색으로 덮어써진다")]
    [SerializeField] private Color color = new Color(1f, 0.82f, 0.15f);
    [Tooltip("자체발광 세기. 그늘에 들어가도 색이 죽지 않게 한다")]
    [Range(0f, 4f)]
    [SerializeField] private float emission = 1.2f;

    [Header("자동 숨김")]
    [Tooltip("함정이나 모래폭풍이 원소에 상쇄되어 꺼지면 화살표도 감춘다")]
    [SerializeField] private bool hideWhenTrapCountered = true;
    [Tooltip("플레이어가 죽어 있는 동안 화살표를 감춘다")]
    [SerializeField] private bool hideWhenPlayerDead = true;

    private static readonly Color PlayerColor = new Color(1f, 0.82f, 0.15f);
    private static readonly Color TrapColor = new Color(1f, 0.3f, 0.22f);
    private static readonly Color OtherColor = new Color(0.4f, 0.8f, 1f);

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private MeshRenderer view;
    private Transform host;              // 표시 대상 = 부모
    // 불 함정(ElementTrapCube)뿐 아니라 물 함정(WaterTrap)까지 함께 보려면 구체 타입이 아니라 규약을 봐야 한다.
    private IElementCounterTrap trap;
    private Sandstorm sandstorm;         // 모래폭풍은 상쇄 칸을 자식으로 들고 있어 위로 훑어서는 못 찾는다
    private IPlayerKillable killable;
    private Vector3 anchorLocal;         // 대상의 정수리 (부모 로컬 좌표)
    private float inverseHostScaleY = 1f;
    private float spinAngle;

    // 끌어다 놓는 순간, 부모가 바뀌는 순간, 인스펙터를 만지는 순간마다 대상을 다시 재서 자리를 잡는다.
    // 에디터에서 붙이자마자 제자리에 보여야 하므로 [ExecuteAlways]가 필요하다.
    private void OnEnable()
    {
        Refresh();
    }

    private void OnTransformParentChanged()
    {
        Refresh();
    }

    private void Reset()
    {
        Refresh();
    }

    private void OnValidate()
    {
        if (isActiveAndEnabled)
        {
            Refresh();
        }
    }

    private void LateUpdate()
    {
        // 에디터에서까지 매 프레임 트랜스폼을 만지면 씬이 계속 "수정됨"으로 바뀐다.
        // 편집 중에는 Refresh가 잡아 준 자리에 가만히 세워 둔다.
        if (!Application.isPlaying || host == null)
        {
            return;
        }

        UpdateVisibility();

        if (!view.enabled)
        {
            return;
        }

        transform.localPosition = anchorLocal + Vector3.up * ((gap + CurrentBob()) * inverseHostScaleY);

        if (spinSpeed != 0f)
        {
            spinAngle = Mathf.Repeat(spinAngle + spinSpeed * Time.unscaledDeltaTime, 360f);
            transform.localRotation = Quaternion.Euler(0f, spinAngle, 0f);
        }
    }

    // 위아래로 떠다니는 지금의 높이차.
    // 시간을 늦추거나 멈춰도 표시는 계속 움직여야 눈에 띄므로 unscaled를 쓴다.
    private float CurrentBob()
    {
        if (bobDistance <= 0f)
        {
            return 0f;
        }

        float wave = Mathf.Sin(Time.unscaledTime * bobSpeed);

        // 사인을 한 번 더 씌우면 위아래 끝에서 더 오래 머문다.
        // 기계적으로 오르내리지 않고 공중에 떠 있는 것처럼 보이게 하는 장치다.
        if (easeBob)
        {
            wave = Mathf.Sin(wave * Mathf.PI * 0.5f);
        }

        return wave * bobDistance;
    }

    // 대상을 다시 재서 크기·높이·색을 잡는다.
    public void Refresh()
    {
#if UNITY_EDITOR
        // 디스크에 있는 프리팹 파일 자체를 임포트하는 중에는 트랜스폼을 건드릴 수 없다.
        // (프리팹 편집 모드나 씬 위의 인스턴스는 persistent가 아니므로 그대로 진행한다)
        if (!Application.isPlaying && UnityEditor.EditorUtility.IsPersistent(this))
        {
            return;
        }
#endif

        Cache();

        if (host == null)
        {
            // 프리팹 에셋을 열어 놓은 상태처럼 부모가 없을 때. 크기만 맞춰 두면 미리보기가 제대로 나온다.
            ApplyWorldSize(Vector3.one);
            ApplyColor();
            return;
        }

        Vector3 hostScale = host.lossyScale;
        inverseHostScaleY = 1f / Mathf.Max(0.0001f, Mathf.Abs(hostScale.y));

        ApplyWorldSize(hostScale);

        // 정수리는 월드 기준으로 재고 부모 로컬 좌표로 바꿔 저장한다.
        // 이렇게 해 두면 부모가 돌거나 크기가 달라도 화살표가 제자리에 붙어 있는다.
        anchorLocal = autoHeight && TryGetTopPoint(out Vector3 top)
            ? host.InverseTransformPoint(top)
            : new Vector3(0f, manualHeight * inverseHostScaleY, 0f);

        transform.localPosition = anchorLocal + Vector3.up * (gap * inverseHostScaleY);
        ApplyColor();
    }

    // 부모가 0.28배로 줄어 있어도 화살표는 늘 worldSize 크기로 보이게 한다.
    private void ApplyWorldSize(Vector3 hostScale)
    {
        if (worldSize <= 0f)
        {
            return;
        }

        transform.localScale = new Vector3(
            worldSize / Mathf.Max(0.0001f, Mathf.Abs(hostScale.x)),
            worldSize / Mathf.Max(0.0001f, Mathf.Abs(hostScale.y)),
            worldSize / Mathf.Max(0.0001f, Mathf.Abs(hostScale.z)));
    }

    private void Cache()
    {
        if (view == null)
        {
            view = GetComponent<MeshRenderer>();
        }

        host = transform.parent;

        if (host == null)
        {
            trap = null;
            sandstorm = null;
            killable = null;
            return;
        }

        trap = host.GetComponentInParent<IElementCounterTrap>();

        // 모래폭풍은 상쇄 칸(ElementTrapCube)을 자식으로 들고 있어서 위로 훑는 것만으로는 못 찾는다.
        // 여기서 같이 잡아 두지 않으면 폭풍이 상쇄되어 꺼져도 화살표만 허공에 남는다.
        sandstorm = host.GetComponentInParent<Sandstorm>();

        killable = host.GetComponentInParent<IPlayerKillable>();

        if (autoColor)
        {
            color = PickColor();
        }
    }

    // 무엇 위에 붙었는지 보고 색을 고른다. 끌어다 붙이기만 해도 알맞은 색이 나오게 한다.
    private Color PickColor()
    {
        if (killable != null || host.GetComponentInParent<PlayerAutoWalker>() != null)
        {
            return PlayerColor;
        }

        // 모래폭풍도 파훼 대상인 함정이므로 불·물 함정과 같은 빨강으로 맞춘다.
        return trap != null || sandstorm != null ? TrapColor : OtherColor;
    }

    // 대상과 그 자식 렌더러를 모두 감싸는 상자의 꼭대기 가운데 점.
    private bool TryGetTopPoint(out Vector3 point)
    {
        point = Vector3.zero;

        bool found = false;
        Bounds total = default;

        foreach (Renderer renderer in host.GetComponentsInChildren<Renderer>())
        {
            // 화살표 자신은 대상의 일부가 아니다.
            if (renderer.transform == transform || renderer.transform.IsChildOf(transform))
            {
                continue;
            }

            // 불꽃·연기 파티클은 크기가 매 프레임 달라져서 기준으로 쓸 수 없다.
            if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
            {
                continue;
            }

            if (!found)
            {
                total = renderer.bounds;
                found = true;
            }
            else
            {
                total.Encapsulate(renderer.bounds);
            }
        }

        if (!found)
        {
            return false;
        }

        point = new Vector3(total.center.x, total.max.y, total.center.z);
        return true;
    }

    private void UpdateVisibility()
    {
        bool show = true;

        if (hideWhenTrapCountered && trap != null && trap.IsCountered)
        {
            show = false;
        }

        if (hideWhenTrapCountered && sandstorm != null && sandstorm.IsCountered)
        {
            show = false;
        }

        if (hideWhenPlayerDead && killable != null && killable.IsDead)
        {
            show = false;
        }

        if (view.enabled != show)
        {
            view.enabled = show;
        }
    }

    // 머티리얼은 모든 화살표가 하나를 같이 쓰고, 색만 화살표마다 덮어쓴다.
    private void ApplyColor()
    {
        if (view == null)
        {
            view = GetComponent<MeshRenderer>();
        }

        if (view == null)
        {
            return;
        }

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        view.GetPropertyBlock(block);
        block.SetColor(BaseColorId, color);   // URP Lit
        block.SetColor(ColorId, color);       // Built-in Standard
        block.SetColor(EmissionColorId, color * emission);
        view.SetPropertyBlock(block);
    }
}
