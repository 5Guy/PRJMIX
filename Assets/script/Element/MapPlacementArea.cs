using UnityEngine;

// 이미 만들어 둔 맵(예: StageScene 3의 "ground-1 (1)") 위에 조합창의 원소를 설치할 수 있게 한다.
//
// StageWorldBuilder는 테스트용 도로를 새로 깔지만, 이 컴포넌트는 씬에 이미 있는 진짜 맵의
// 크기를 재서 그 위에 격자(PlacementGrid)만 얹고 PlacementSystem에 물려 준다.
// 맵 자체는 건드리지 않는다.
//
// 칸 크기는 도로 폭에서 나온다.
//   도로 폭 ÷ 칸 수(1 또는 3) = 칸 크기
// 그래서 도로 위에는 칸이 정확히 1개 또는 3개만 들어가고, 어디에 놓아야 하는지가 분명해진다.
// 격자는 맵 전체를 덮으므로 도로 밖(인도, 건물 위)에도 그대로 놓을 수 있다.
public class MapPlacementArea : MonoBehaviour
{
    [Header("맵")]
    [Tooltip("격자를 깔 맵 오브젝트. 비워두면 아래 이름으로 씬에서 찾는다")]
    [SerializeField] private Transform ground;
    [SerializeField] private string groundObjectName = "ground-1 (1)";

    [Header("칸 크기 = 도로 폭 ÷ 칸 수")]
    [Tooltip("칸 크기의 기준이 되는 도로. 비워두면 아래 이름으로 씬에서 찾는다")]
    [SerializeField] private Transform road;
    [Tooltip("플레이어가 걸어가는 도로 오브젝트 이름.\n" +
             "Stage_01에서는 'Road_Lane_01'(폭 9.48m, 길이 130m)이 그 도로다.\n" +
             "'Road_Lane_02.001'은 도로망 전체(160x130m)라 기준으로 쓰면 안 된다")]
    [SerializeField] private string roadObjectName = "Road_Lane_01";
    [Tooltip("도로 폭에 칸이 몇 개 들어가게 할지. 도로 위 놓을 자리가 그만큼 생긴다")]
    [SerializeField] private PlacementGridLayout.RoadCells cellsAcrossRoad = PlacementGridLayout.RoadCells.Three;
    [Tooltip("도로 폭을 직접 정한다(m). 0이면 위 도로 오브젝트를 재서 자동으로 구한다.\n" +
             "자동으로 잰 값이 엉뚱하면(도로 메시가 여러 갈래로 붙어 있는 경우) 여기에 실제 폭을 적는다")]
    [SerializeField] private float roadWidthOverride = 0f;

    [Tooltip("칸을 도로 한가운데에서 옆으로 밀어 준다(m). 진행 방향 기준 오른쪽이 +.\n" +
             "도로 메시의 크기와 눈에 보이는 도로가 조금 어긋날 때 여기서 맞춘다")]
    [SerializeField] private float roadCenterOffset = 0f;

    [Header("격자")]
    [Tooltip("도로를 찾지 못했을 때 쓸 칸 크기")]
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
        if (!TryBuildLayout(out PlacementGridLayout.Result layout, out string failure))
        {
            Debug.LogError($"{name}: {failure}", this);
            enabled = false;
            return;
        }

        GameObject gridObject = new GameObject("PlacementGrid");
        gridObject.transform.SetParent(transform, false);
        gridObject.transform.position = layout.Origin;

        grid = gridObject.AddComponent<PlacementGrid>();
        grid.Configure(layout.CellSize, layout.Columns, layout.Rows);

        Debug.Log($"{name}: 배치 격자 - {layout.Explanation}", this);

        BindPlacement();
    }

    // 격자를 어디에 어떻게 깔지 계산한다.
    //
    // 실행 중(Awake)과 에디터 도구(TrapPlacement.TryResolveGrid)가 똑같이 이것을 부른다.
    // 계산도, 계산에 넣을 값을 모으는 것도 여기 한 곳뿐이라 양쪽이 어긋날 수 없다.
    // 씬을 건드리지 않으므로 에디터에서 불러도 안전하다.
    public bool TryBuildLayout(out PlacementGridLayout.Result layout, out string failure)
    {
        layout = default;
        failure = null;

        Transform groundTransform = Resolve(ground, groundObjectName);

        if (groundTransform == null)
        {
            failure = $"격자를 깔 맵 오브젝트('{groundObjectName}')를 찾지 못했습니다.";
            return false;
        }

        if (!PlacementGridLayout.TryMeasureRenderers(groundTransform.gameObject, out Bounds bounds))
        {
            failure = $"'{groundTransform.name}' 하위에 Renderer가 없어 맵 크기를 잴 수 없습니다.";
            return false;
        }

        Transform player = FindPlayer();

        PlacementGridLayout.Settings settings = new PlacementGridLayout.Settings
        {
            GroundBounds = bounds,
            SurfaceY = groundTransform.position.y + surfaceOffset,
            EdgePadding = edgePadding,
            FallbackCellSize = cellSize,
            CellsAcrossRoad = cellsAcrossRoad,
            AlongIsZ = PlacementGridLayout.IsAlongZ(player != null ? player.forward : Vector3.forward),
        };

        if (player != null)
        {
            settings.HasPlayer = true;
            settings.PlayerAlong = settings.AlongIsZ ? player.position.z : player.position.x;
        }

        FillRoadSettings(ref settings, player);

        layout = PlacementGridLayout.Build(settings);
        return true;
    }

    // Inspector에 물려 둔 것을 먼저 쓰고, 비어 있으면 이름으로 씬에서 찾는다.
    // 찾은 것을 필드에 적어 두지는 않는다. 에디터에서 불렀을 때 씬이 더러워지지 않게 하기 위해서다.
    private static Transform Resolve(Transform assigned, string objectName)
    {
        if (assigned != null)
        {
            return assigned;
        }

        if (string.IsNullOrEmpty(objectName))
        {
            return null;
        }

        GameObject found = GameObject.Find(objectName);
        return found != null ? found.transform : null;
    }

    // 도로 폭을 채워 넣는다. 직접 적어 둔 값이 있으면 그것을 믿고, 없으면 도로를 재서 구한다.
    private void FillRoadSettings(ref PlacementGridLayout.Settings settings, Transform player)
    {
        Transform roadTransform = Resolve(road, roadObjectName);

        bool measured = roadTransform != null
            && PlacementGridLayout.TryMeasureRenderers(roadTransform.gameObject, out Bounds roadBounds);

        // 폭을 직접 적어 두었으면 그것을 쓴다. 자동으로 잰 값이 엉뚱할 때 쓰는 탈출구다.
        if (roadWidthOverride > 0.01f)
        {
            settings.HasRoad = true;
            settings.RoadWidth = roadWidthOverride;
            settings.RoadCenterAcross =
                ResolveRoadCenter(roadTransform, measured, settings.AlongIsZ, player) + roadCenterOffset;
            return;
        }

        if (roadTransform == null)
        {
            Debug.LogWarning(
                $"{name}: 칸 크기의 기준이 될 도로('{roadObjectName}')를 찾지 못해 기본 칸 크기 {cellSize}m를 씁니다. " +
                "도로 오브젝트를 물려 주거나 '도로 폭 직접 지정'에 폭을 적어 주세요.",
                this);
            return;
        }

        if (!measured)
        {
            Debug.LogWarning($"{name}: '{roadTransform.name}' 하위에 Renderer가 없어 도로 폭을 잴 수 없습니다.", this);
            return;
        }

        PlacementGridLayout.TryMeasureRenderers(roadTransform.gameObject, out Bounds bounds);
        PlacementGridLayout.MeasureAcross(bounds, settings.AlongIsZ, out float width, out float centerAcross);

        // 도로 메시가 교차로까지 한 덩어리면 폭이 맵만큼 크게 잡힌다.
        // 그대로 두면 칸 하나가 도시만 해지므로 눈에 띄게 알린다.
        float mapAcross = settings.AlongIsZ ? settings.GroundBounds.size.x : settings.GroundBounds.size.z;
        if (mapAcross > 0.01f && width > mapAcross * 0.3f)
        {
            Debug.LogWarning(
                $"{name}: 도로 '{roadTransform.name}'의 폭이 {width:0.##}m로 맵 폭({mapAcross:0.##}m)에 비해 너무 넓습니다. " +
                "도로 메시가 교차로까지 한 덩어리일 수 있습니다. " +
                "'도로 폭 직접 지정'에 실제 차선 폭을 적어 주세요.",
                this);
        }

        settings.HasRoad = true;
        settings.RoadWidth = width;
        settings.RoadCenterAcross = centerAcross + roadCenterOffset;
    }

    // 도로 한가운데의 가로지르는 축 좌표. 도로를 재지 못했으면 플레이어가 걷는 선을 한가운데로 본다.
    private static float ResolveRoadCenter(Transform roadTransform, bool measured, bool alongIsZ, Transform player)
    {
        if (measured && PlacementGridLayout.TryMeasureRenderers(roadTransform.gameObject, out Bounds bounds))
        {
            PlacementGridLayout.MeasureAcross(bounds, alongIsZ, out float _, out float center);
            return center;
        }

        if (player == null)
        {
            return 0f;
        }

        return alongIsZ ? player.position.x : player.position.z;
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

    // Player 태그는 옛날 테스트용 오브젝트에도 붙어 있어서, 실제로 걸어다니는 플레이어를 먼저 찾는다.
    // (진행 방향을 잘못 잡으면 도로 폭을 세로로 재게 되어 칸 크기가 통째로 어긋난다)
    private Transform FindPlayer()
    {
        return PlayerLocator.FindPlayer();
    }
}
