using System.Collections.Generic;
using UnityEngine;

// 원소를 설치하는 탑뷰에서 함정의 3D 모습을 감추고, 바닥에 눕힌 간단한 이미지로 대신 보여 준다.
//
// 왜 필요한가
//   불기둥·소용돌이·물웅덩이 같은 연출은 위에서 내려다보면 도로를 통째로 가려서
//   어느 칸에 원소를 놓아야 하는지 보이지 않는다. 탑뷰에서는 연출의 렌더러만 끄고
//   (파티클과 판정 콜라이더는 그대로 돌게 두고) 함정 자리에 납작한 표시를 하나 띄운다.
//
// 어떻게 붙는가
//   TrapTopViewInstaller가 씬의 함정마다 하나씩 자동으로 달아 준다.
//   손으로 미리 붙여 두면 그 값을 그대로 쓰고, 컴포넌트의 체크를 꺼 두면 그 함정은 건너뛴다.
//
// 배치한 원소와의 순서
//   표시는 배치한 원소 아이콘(ElementVisual.PlacedIconSortingOrder)보다 낮은 정렬 순서로 그린다.
//   그래서 함정 위에 원소를 올리면 언제나 원소가 함정 표시를 덮는다.
[DisallowMultipleComponent]
public class TrapTopViewIcon : MonoBehaviour
{
    [Header("숨길 대상")]
    [Tooltip("탑뷰에서 감출 3D 연출의 뿌리. 비워두면 이 오브젝트 자신")]
    [SerializeField] private Transform hideRoot;
    [Tooltip("전구(Light)도 같이 끈다. 끄지 않으면 불기둥은 사라졌는데 바닥만 벌겋게 남는다")]
    [SerializeField] private bool hideLights = true;
    [Tooltip("머리 위 화살표 표시(TargetArrowMarker)는 탑뷰에서도 그대로 둔다")]
    [SerializeField] private bool keepArrowMarker = true;

    [Header("표시할 이미지")]
    [Tooltip("끄면 표시를 그리지 않고 3D 모습만 감춘다.\n" +
             "바로 옆 함정(모래바람의 상쇄 칸 등)이 이미 그 자리를 표시하고 있어 표시가 두 개로 겹칠 때 쓴다")]
    [SerializeField] private bool showMarker = true;
    [Tooltip("함정을 대신할 그림. 비워두면 함정의 원소 아이콘을 쓰고, 그것도 없으면 원반만 그린다")]
    [SerializeField] private Sprite icon;
    [SerializeField] private Color iconColor = Color.white;
    [Tooltip("아이콘 뒤에 깔아 함정이 차지한 자리를 알려 주는 원반")]
    [SerializeField] private bool showFootprint = true;
    [SerializeField] private Color footprintColor = new Color(1f, 0.3f, 0.22f, 0.5f);
    [Tooltip("원반 크기 대비 아이콘 지름")]
    [Range(0.2f, 1f)]
    [SerializeField] private float iconRatio = 0.6f;

    [Header("크기")]
    [Tooltip("표시의 지름(미터). 0이면 함정 크기를 재서 자동으로 잡는다")]
    [SerializeField] private float diameter;
    [Tooltip("켜면 함정이 차지한 가로·세로를 그대로 따라 눌린 원반이 된다(길게 누운 물웅덩이 등)")]
    [SerializeField] private bool matchFootprint = true;
    [SerializeField] private float minDiameter = 0.8f;
    [SerializeField] private float maxDiameter = 3f;
    [Tooltip("크기를 잴 수 없을 때(연출이 전부 파티클일 때) 쓸 지름(미터)")]
    [SerializeField] private float fallbackDiameter = 1.2f;
    [Tooltip("바닥에서 이만큼 띄운다(미터). 도로에 파묻히지 않을 만큼만")]
    [SerializeField] private float groundClearance = 0.015f;

    private Transform iconRoot;
    private bool built;
    private bool hiding;
    private Vector3 anchorLocal;
    private Vector2 footprintSize;

    private readonly List<Renderer> hiddenRenderers = new List<Renderer>();
    private readonly List<Light> hiddenLights = new List<Light>();

    // 표시는 함정의 자식으로 두지 않는다.
    //
    // 함정 오브젝트는 크기가 축마다 제각각인 경우가 흔하다(FireTrab의 연출은 3.6 × 3.6 × 0.055다).
    // 그 밑에 표시를 넣고 바닥에 눕히면, 눕히면서 돌아간 축이 부모의 눌린 축에 얹혀
    // 스프라이트가 머리카락처럼 납작해져 화면에서 사라진다. 부모 크기를 거꾸로 곱해도
    // 돌아간 채로는 축이 서로 어긋나 되돌려지지 않는다.
    // 크기가 1인 공용 상자 밑에 모아 두고 위치만 따라가게 하면 이런 일이 없다.
    private static Transform iconContainer;

    private static Transform Container
    {
        get
        {
            if (iconContainer == null)
            {
                iconContainer = new GameObject("TopViewIcons").transform;
            }

            return iconContainer;
        }
    }

    // 정적 값은 플레이 세션을 넘어 남는다. 지난 판의 상자를 가리킨 채로 시작하지 않게 비운다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        iconContainer = null;
    }

    // 탑뷰에서 감출 연출의 뿌리.
    private Transform Root => hideRoot != null ? hideRoot : transform;

    // 스크립트에서 달아 줄 때 값을 한 번에 정한다(모래바람처럼 스스로 붙이는 함정이 쓴다).
    // null이나 0을 넘긴 항목은 인스펙터에 적힌 값을 그대로 둔다.
    public void Configure(Transform root, Sprite symbol, Color footprint, float markerDiameter)
    {
        hideRoot = root;
        footprintColor = footprint;

        if (symbol != null)
        {
            icon = symbol;
        }

        if (markerDiameter > 0f)
        {
            diameter = markerDiameter;
            matchFootprint = false;
        }
    }

    // 표시는 그리지 않고 3D 모습을 감추는 일만 맡는다.
    //
    // 바로 옆의 다른 함정이 이미 그 자리를 표시하고 있을 때 쓴다.
    // 모래바람이 그렇다 — 원소를 올리는 자리는 옆에 놓인 상쇄 칸이고 그쪽에 표시가 달리므로,
    // 소용돌이 한가운데에 하나 더 그리면 두 개가 겹쳐 보인다.
    // 표시를 만들기 전(첫 LateUpdate 전)에 불러야 한다.
    public void HideWithoutMarker()
    {
        showMarker = false;
    }

    private void OnEnable()
    {
        CameraViewController.TopViewChanged += Apply;

        // 아직 만들기 전이라면 첫 LateUpdate에서 만들고 그때 맞춘다.
        // 여기서 미리 만들면 함정이 Start에서 제 모습을 갖추기도 전에 크기를 재게 된다.
        if (built)
        {
            Apply(CameraViewController.IsTopView);
        }
    }

    // 표시가 함정의 자식이 아니므로, 함정이 꺼지면(상쇄되어 통째로 비활성화되면)
    // 표시도 여기서 직접 내려야 한다. 그러지 않으면 사라진 함정 자리에 표시만 남는다.
    private void OnDisable()
    {
        CameraViewController.TopViewChanged -= Apply;

        if (iconRoot != null)
        {
            iconRoot.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (iconRoot != null)
        {
            Destroy(iconRoot.gameObject);
        }
    }

    private void LateUpdate()
    {
        if (!built)
        {
            Build();
            Apply(CameraViewController.IsTopView);
            return;
        }

        if (iconRoot == null || !iconRoot.gameObject.activeSelf)
        {
            return;
        }

        PlaceIcon();
    }

    private void Apply(bool topView)
    {
        // 만들기 전에 시점이 먼저 바뀌었더라도 표시가 나와야 한다.
        if (!built)
        {
            Build();
        }

        if (topView)
        {
            HideVisual();
        }
        else
        {
            ShowVisual();
        }

        if (iconRoot == null)
        {
            return;
        }

        iconRoot.gameObject.SetActive(topView);

        if (topView)
        {
            PlaceIcon();
        }
    }

    // ───────────────────────── 3D 모습 끄고 켜기 ─────────────────────────

    // GameObject를 통째로 끄지 않고 렌더러만 끈다.
    // 함정의 판정 콜라이더와 파티클은 그대로 돌아야 하고, 상쇄 처리도 계속 살아 있어야 한다.
    private void HideVisual()
    {
        if (hiding)
        {
            return;
        }

        hiding = true;
        hiddenRenderers.Clear();
        hiddenLights.Clear();

        foreach (Renderer renderer in Root.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer.enabled || IsExcluded(renderer.transform))
            {
                continue;
            }

            renderer.enabled = false;
            hiddenRenderers.Add(renderer);
        }

        if (!hideLights)
        {
            return;
        }

        foreach (Light light in Root.GetComponentsInChildren<Light>(true))
        {
            if (!light.enabled || IsExcluded(light.transform))
            {
                continue;
            }

            light.enabled = false;
            hiddenLights.Add(light);
        }
    }

    // 우리가 끈 것만 되돌린다. 다른 이유로 이미 꺼져 있던 렌더러는 건드리지 않는다.
    private void ShowVisual()
    {
        hiding = false;

        foreach (Renderer renderer in hiddenRenderers)
        {
            if (renderer != null)
            {
                renderer.enabled = true;
            }
        }

        foreach (Light light in hiddenLights)
        {
            if (light != null)
            {
                light.enabled = true;
            }
        }

        hiddenRenderers.Clear();
        hiddenLights.Clear();
    }

    // 함정의 일부가 아니라서 탑뷰에서도 그대로 두어야 하는 것들.
    private bool IsExcluded(Transform target)
    {
        // 이 표시 자신.
        if (iconRoot != null && (target == iconRoot || target.IsChildOf(iconRoot)))
        {
            return true;
        }

        // 함정 위에 올려 둔 원소. 2D/3D 전환은 PlacedElementView가 따로 맡는다.
        // 여기서 같이 끄면 원소를 회수하거나 시점을 되돌릴 때 서로 다른 판단이 부딪힌다.
        if (target.GetComponentInParent<PlacedElementView>() != null ||
            target.GetComponentInParent<PlacedElement>() != null)
        {
            return true;
        }

        // 머리 위 화살표는 탑뷰에서 함정을 찾으라고 있는 표시다. 같이 끄면 본말이 뒤집힌다.
        if (keepArrowMarker && target.GetComponentInParent<TargetArrowMarker>() != null)
        {
            return true;
        }

        return false;
    }

    // ───────────────────────── 표시 만들기 ─────────────────────────

    private void Build()
    {
        built = true;

        // 표시를 그리지 않기로 했으면 3D를 감추는 일만 한다. 잴 것도 만들 것도 없다.
        if (!showMarker)
        {
            return;
        }

        MeasureTrap();

        // 하이어라키에서 어느 함정의 표시인지 알아볼 수 있게 이름에 함정을 적어 둔다.
        iconRoot = new GameObject($"TopViewIcon ({name})").transform;
        iconRoot.SetParent(Container, false);

        if (showFootprint)
        {
            // 함정이 차지한 자리를 알려 주는 원반과 그 테두리.
            ElementVisual.CreateFlatSprite(
                "Footprint", iconRoot, ElementVisual.Circle, footprintColor,
                footprintSize, ElementVisual.TrapIconSortingOrder);

            ElementVisual.CreateFlatSprite(
                "Outline", iconRoot, ElementVisual.Ring, OutlineColor(footprintColor),
                footprintSize, ElementVisual.TrapIconSortingOrder + 1);
        }

        Sprite symbol = ResolveIcon();
        if (symbol != null)
        {
            float size = Mathf.Min(footprintSize.x, footprintSize.y) * iconRatio;

            ElementVisual.CreateFlatSprite(
                "Symbol", iconRoot, symbol, iconColor,
                new Vector2(size, size), ElementVisual.TrapIconSortingOrder + 2);
        }

        iconRoot.gameObject.SetActive(false);
    }

    // 테두리는 원반과 같은 색을 진하게 쓴다. 함정마다 색을 따로 정하지 않아도 서로 어울린다.
    private static Color OutlineColor(Color fill)
    {
        return new Color(fill.r * 0.72f, fill.g * 0.72f, fill.b * 0.72f, Mathf.Clamp01(fill.a + 0.4f));
    }

    // 인스펙터에 넣어 둔 그림이 있을 때만 그린다.
    //
    // 예전에는 비어 있으면 함정의 원소 아이콘(불 함정 → 불)을 대신 썼는데,
    // 그 아이콘이 조합창에서 끌어다 놓는 원소 아이콘과 똑같이 생겨서
    // "불 함정 위에 불 원소가 이미 올라가 있다"로 읽혔다.
    // 어떤 함정인지는 원반 색(불=주황, 물=파랑)으로 이미 알 수 있으므로 그림은 넣지 않는다.
    private Sprite ResolveIcon()
    {
        return icon;
    }

    // 함정이 도로에서 차지하는 자리(가로·세로)와 바닥 높이를 잰다.
    //
    // 불꽃·연기 파티클은 매 프레임 크기가 달라져서 기준으로 쓸 수 없다.
    // 잴 것이 하나도 없는 함정(연출이 전부 파티클인 불기둥 등)은 정해 둔 지름으로 그린다.
    private void MeasureTrap()
    {
        float min = Mathf.Max(0.1f, minDiameter);
        float max = Mathf.Max(min, maxDiameter);
        bool measured = TryMeasureBounds(out Bounds bounds);

        if (diameter > 0f)
        {
            footprintSize = Vector2.one * diameter;
        }
        else if (measured)
        {
            footprintSize = matchFootprint
                ? new Vector2(Mathf.Clamp(bounds.size.x, min, max), Mathf.Clamp(bounds.size.z, min, max))
                : Vector2.one * Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.z), min, max);
        }
        else
        {
            footprintSize = Vector2.one * Mathf.Max(0.1f, fallbackDiameter);
        }

        Vector3 anchor = measured
            ? new Vector3(bounds.center.x, bounds.min.y, bounds.center.z)
            : Root.position;

        anchor.y = GroundY(anchor.y, measured, bounds) + groundClearance;
        anchorLocal = transform.InverseTransformPoint(anchor);
    }

    // 표시를 띄울 바닥 높이.
    //
    // 함정 모델의 밑동을 그대로 쓰면 도로에 살짝 파묻혀 있는 함정(물웅덩이 등)에서는
    // 표시가 도로 아래로 들어가 아무것도 안 보인다. 배치한 원소 아이콘과 같은 평면
    // (= 원소를 놓는 격자)에 맞춰 두면 둘이 같은 높이에서 정렬 순서대로 겹친다.
    private static float GroundY(float fallback, bool measured, Bounds bounds)
    {
        PlacementGrid grid = FindFirstObjectByType<PlacementGrid>();
        if (grid == null)
        {
            return fallback;
        }

        // 도로에서 멀찍이 떨어진 곳(고가 위 등)에 세운 함정까지 도로 높이로 끌어내리지는 않는다.
        if (measured && (grid.SurfaceY < bounds.min.y - 1f || grid.SurfaceY > bounds.max.y + 1f))
        {
            return fallback;
        }

        return grid.SurfaceY;
    }

    private bool TryMeasureBounds(out Bounds bounds)
    {
        bounds = default;
        bool found = false;

        foreach (Renderer renderer in Root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
            {
                continue;
            }

            if (IsExcluded(renderer.transform))
            {
                continue;
            }

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return found;
    }

    // 함정이 움직이거나 돌아도(밀려오는 모래바람, 달려오는 자동차) 표시는 그 자리를 따라가며
    // 늘 바닥에 납작하게 눕는다. 자식이 아니라 위치만 베끼는 것이라, 함정이 아무리 찌그러져 있어도
    // 표시는 적어 둔 지름 그대로 나온다.
    private void PlaceIcon()
    {
        iconRoot.SetPositionAndRotation(
            transform.TransformPoint(anchorLocal),
            Quaternion.Euler(90f, 0f, 0f));
    }
}
