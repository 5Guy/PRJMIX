using UnityEngine;

// 이미 만들어 둔 맵(예: StageScene 3의 "ground-1 (1)") 위에 조합창의 원소를 설치할 수 있게 한다.
//
// StageWorldBuilder는 테스트용 도로를 새로 깔지만, 이 컴포넌트는 씬에 이미 있는 진짜 맵의
// 크기를 재서 그 위에 격자(PlacementGrid)만 얹고 PlacementSystem에 물려 준다.
// 맵 자체는 건드리지 않는다.
public class MapPlacementArea : MonoBehaviour
{
    [Header("맵")]
    [Tooltip("격자를 깔 맵 오브젝트. 비워두면 아래 이름으로 씬에서 찾는다")]
    [SerializeField] private Transform ground;
    [SerializeField] private string groundObjectName = "ground-1 (1)";

    [Header("격자")]
    [SerializeField] private float cellSize = 1f;
    [Tooltip("맵 가장자리에서 안쪽으로 줄일 여백(월드 단위)")]
    [SerializeField] private float edgePadding = 0.5f;
    [Tooltip("바닥 높이 보정. 격자 평면은 맵 오브젝트의 y + 이 값에 놓인다")]
    [SerializeField] private float surfaceOffset = 0f;

    [Header("배치")]
    [Tooltip("비워두면 씬에서 찾고, 그래도 없으면 새로 만든다")]
    [SerializeField] private PlacementSystem placement;
    [Tooltip("체크하면 배치할 때 카메라를 탑뷰로 내린다. 씬 카메라를 그대로 두려면 끈다")]
    [SerializeField] private bool useTopViewCamera = false;

    private PlacementGrid grid;

    public PlacementGrid Grid => grid;

    private void Awake()
    {
        if (!ResolveGround())
        {
            enabled = false;
            return;
        }

        BuildGrid();
        BindPlacement();
    }

    private bool ResolveGround()
    {
        if (ground == null && !string.IsNullOrEmpty(groundObjectName))
        {
            GameObject found = GameObject.Find(groundObjectName);
            if (found != null)
            {
                ground = found.transform;
            }
        }

        if (ground == null)
        {
            Debug.LogError($"{name}: 격자를 깔 맵 오브젝트('{groundObjectName}')를 찾지 못했습니다.", this);
            return false;
        }

        return true;
    }

    // 맵에 붙은 Renderer들의 월드 AABB를 합쳐 맵이 차지하는 가로·세로를 잰다.
    private bool TryMeasureGround(out Bounds bounds)
    {
        Renderer[] renderers = ground.GetComponentsInChildren<Renderer>();
        bounds = default;
        bool any = false;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            if (!any)
            {
                bounds = renderer.bounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return any;
    }

    private void BuildGrid()
    {
        if (!TryMeasureGround(out Bounds bounds))
        {
            Debug.LogError($"{name}: '{ground.name}' 하위에 Renderer가 없어 맵 크기를 잴 수 없습니다.", this);
            enabled = false;
            return;
        }

        float width = bounds.size.x - edgePadding * 2f;
        float depth = bounds.size.z - edgePadding * 2f;
        float size = Mathf.Max(0.01f, cellSize);

        int columns = Mathf.Max(1, Mathf.FloorToInt(width / size));
        int rows = Mathf.Max(1, Mathf.FloorToInt(depth / size));

        // 남는 자투리는 양쪽에 반씩 나눠 격자를 맵 한가운데에 맞춘다.
        float marginX = (bounds.size.x - columns * size) * 0.5f;
        float marginZ = (bounds.size.z - rows * size) * 0.5f;

        GameObject gridObject = new GameObject("PlacementGrid");
        gridObject.transform.SetParent(transform, false);
        gridObject.transform.position = new Vector3(
            bounds.min.x + marginX,
            ground.position.y + surfaceOffset,
            bounds.min.z + marginZ);

        grid = gridObject.AddComponent<PlacementGrid>();
        grid.Configure(size, columns, rows);
    }

    private void BindPlacement()
    {
        if (placement == null)
        {
            placement = FindFirstObjectByType<PlacementSystem>();
        }

        if (placement == null)
        {
            GameObject placementObject = new GameObject("PlacementSystem");
            placementObject.transform.SetParent(transform, false);
            placement = placementObject.AddComponent<PlacementSystem>();
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            Debug.LogError($"{name}: MainCamera 태그가 붙은 카메라가 없어 배치 위치를 계산할 수 없습니다.", this);
        }

        // 씬에 이미 있는 카메라를 그대로 쓰고 싶으면 시점 전환은 넘긴다.
        CameraViewController view = null;
        if (useTopViewCamera && camera != null)
        {
            view = camera.GetComponent<CameraViewController>();
            if (view == null)
            {
                view = camera.gameObject.AddComponent<CameraViewController>();
            }

            view.SetTarget(FindPlayer());
            view.SetTopView(false);
            view.SnapToCurrentView();
        }

        placement.Setup(grid, FindPlayer(), view, camera);
    }

    private Transform FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag(PlayerLocator.PlayerTag);
        return player != null ? player.transform : null;
    }
}
