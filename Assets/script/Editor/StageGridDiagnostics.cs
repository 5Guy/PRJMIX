using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 스테이지의 도로 폭과 격자 칸 크기가 실제로 얼마인지 찍어 보는 진단 도구.
// Tools > Molra > 진단 : 도로 폭 재기
public static class StageGridDiagnostics
{
    public static readonly string[] StageScenes =
    {
        "Assets/Scenes/Stage_01.unity",
        "Assets/Scenes/Stage_02.unity",
        "Assets/Scenes/Stage_03.unity",
    };

    // 배치 모드에서 Stage_01을 열고 재 본다. (-executeMethod StageGridDiagnostics.ReportStage01)
    public static void ReportStage01()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Stage_01.unity", OpenSceneMode.Single);
        Report();
    }

    // 배치 모드에서 모든 스테이지를 차례로 재 본다. (-executeMethod StageGridDiagnostics.ReportAllStages)
    public static void ReportAllStages()
    {
        foreach (string path in StageScenes)
        {
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Debug.Log("──────────────────────────────────────────");
            Report();
            VerifyTrapsOnCells();
            VerifyGoalOnCell();
            VerifyTrapsReachable();
        }

        VerifyHighlightMaterial();
    }

    // 배치 모드용: Stage_01에 함정을 깔고, 칸에 맞추고, 결과를 검사한다.
    // (-executeMethod StageGridDiagnostics.SetupStage01)
    public static void SetupStage01()
    {
        Scene opened = EditorSceneManager.OpenScene("Assets/Scenes/Stage_01.unity", OpenSceneMode.Single);
        Report();

        // 깃발 자리가 곧 동선 길이라서, 함정을 깔기 전에 먼저 세워 두어야 간격이 제대로 나온다.
        StageGoalPlacer.MoveGoalAheadDefault();
        EditorSceneManager.MarkSceneDirty(opened);
        EditorSceneManager.SaveScene(opened);

        Stage01TrapSetup.Build();

        // 새로 깔린 함정 외에 손으로 놓아 둔 함정이 남아 있을 수 있으니 한 번 더 칸에 맞춘다.
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Stage_01.unity", OpenSceneMode.Single);
        TrapCellAligner.AlignOpenScene();
        EditorSceneManager.SaveScene(scene);

        EditorSceneManager.OpenScene("Assets/Scenes/Stage_01.unity", OpenSceneMode.Single);
        VerifyTrapsOnCells();
    }

    // 배치 모드용: 칸마다 지형 높이가 제대로 잡히는지 확인한다.
    // (-executeMethod StageGridDiagnostics.ProbeStage01Surfaces)
    public static void ProbeStage01Surfaces()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Stage_01.unity", OpenSceneMode.Single);
        ProbeSurfaces();
    }

    // PlacementSystem이 배치할 때 쓰는 것과 같은 방법으로 칸마다 지형 제일 윗면을 재 본다.
    // 도로 위와 도로 밖(인도·건물)의 높이가 서로 다르게 나와야 원소가 지형 위에 얹힌다.
    [MenuItem("Tools/Molra/진단 : 칸마다 지형 높이 재기")]
    public static void ProbeSurfaces()
    {
        if (!TrapPlacement.TryResolveGrid(out Vector3 origin, out float cellSize, out int columns, out int rows))
        {
            Debug.LogError("[높이] 격자를 계산하지 못했습니다.");
            return;
        }

        Transform player = PlayerLocator.FindPlayer();
        if (player == null)
        {
            Debug.LogError("[높이] 플레이어를 찾지 못했습니다.");
            return;
        }

        int centerX = Mathf.FloorToInt((player.position.x - origin.x) / cellSize);
        int startZ = Mathf.FloorToInt((player.position.z - origin.z) / cellSize);

        Debug.Log($"[높이] 격자 평면 y={origin.y:0.##}, 칸 크기 {cellSize:0.###}m. " +
                  $"플레이어 칸=({centerX},{startZ})");

        // 도로를 가로지르는 다섯 칸(가운데 3칸이 도로, 바깥 2칸은 도로 밖)을
        // 진행 방향으로 몇 칸씩 나아가며 재 본다.
        for (int stepZ = 0; stepZ <= 6; stepZ += 2)
        {
            string line = $"[높이] z칸 {startZ + stepZ}: ";

            for (int dx = -2; dx <= 2; dx++)
            {
                Vector3 center = new Vector3(
                    origin.x + (centerX + dx + 0.5f) * cellSize,
                    origin.y,
                    origin.z + (startZ + stepZ + 0.5f) * cellSize);

                bool hit = TrySampleTop(center, origin.y, out float y, out string what);
                line += hit
                    ? $"[x{centerX + dx} y={y:0.00} {what}] "
                    : $"[x{centerX + dx} 바닥없음] ";
            }

            Debug.Log(line);
        }
    }

    // 배치 모드용. (-executeMethod StageGridDiagnostics.ScanAllStages)
    public static void ScanAllStages()
    {
        foreach (string path in StageScenes)
        {
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Debug.Log("──────────────────────────────────────────");
            Debug.Log($"[훑기] 씬: {path}");
            ScanRoad();
        }
    }

    // 진행 방향에 수직으로 촘촘히 광선을 쏘아 "실제로 밟을 수 있는 바닥"의 폭을 잰다.
    //
    // Renderer 크기는 콜라이더와 다를 수 있고, 도로 메시가 아예 없는 스테이지도 있다.
    // 칸을 길에 맞추려면 결국 플레이어가 실제로 딛는 면이 어디까지인지가 기준이어야 한다.
    [MenuItem("Tools/Molra/진단 : 길 바닥 훑기")]
    public static void ScanRoad()
    {
        if (!TrapPlacement.TryGetPathDirection(out Vector3 direction, out Transform player))
        {
            Debug.LogError("[훑기] 플레이어를 찾지 못했습니다.");
            return;
        }

        Vector3 across = Vector3.Cross(Vector3.up, direction).normalized;
        float surfaceY = player.position.y;

        const float step = 0.25f;
        const float reach = 14f;

        for (int forward = 0; forward <= 3; forward++)
        {
            Vector3 lineStart = player.position + direction * (forward * 4f);
            string line = $"[훑기] 앞으로 {forward * 4}m : ";
            string previous = "-";
            float firstGround = float.NaN;
            float lastGround = float.NaN;

            for (float offset = -reach; offset <= reach; offset += step)
            {
                Vector3 point = lineStart + across * offset;
                TrySampleTop(point, surfaceY, out float y, out string what);
                string label = what ?? "없음";

                if (label != previous)
                {
                    line += $"{offset:0.00}→{label}  ";
                    previous = label;
                }

                // 플레이어 발밑 높이와 비슷한 면만 "걸을 수 있는 바닥"으로 센다.
                if (what != null && Mathf.Abs(y - surfaceY) <= 1.5f)
                {
                    if (float.IsNaN(firstGround))
                    {
                        firstGround = offset;
                    }

                    lastGround = offset;
                }
            }

            string width = float.IsNaN(firstGround)
                ? "바닥 없음"
                : $"걸을 수 있는 폭 {(lastGround - firstGround + step):0.00}m ({firstGround:0.00} ~ {lastGround:0.00})";

            Debug.Log($"{line}| {width}");
        }
    }

    // 배치 모드용. (-executeMethod StageGridDiagnostics.MeasureStage01Road)
    public static void MeasureStage01Road()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Stage_01.unity", OpenSceneMode.Single);
        MeasureRoadPrecisely();
    }

    // 도로가 정확히 어디서 시작해 어디서 끝나는지 5cm 간격으로 잰다.
    //
    // Renderer 크기(AABB)는 실제로 보이는 도로와 다를 수 있다. 메시에 안 쓰는 정점이
    // 남아 있거나 가장자리 선이 삐져나와 있으면 그만큼 크게 잡힌다.
    // 칸을 도로에 딱 맞추려면 눈에 보이는 도로 면이 어디까지인지가 기준이어야 한다.
    [MenuItem("Tools/Molra/진단 : 도로 폭 정밀 측정")]
    public static void MeasureRoadPrecisely()
    {
        if (!TrapPlacement.TryGetPathDirection(out Vector3 direction, out Transform player))
        {
            Debug.LogError("[정밀] 플레이어를 찾지 못했습니다.");
            return;
        }

        bool alongIsZ = PlacementGridLayout.IsAlongZ(direction);
        Vector3 across = Vector3.Cross(Vector3.up, direction).normalized;

        // 비교 대상: Renderer 크기로 잰 값.
        foreach (string name in new[] { "Road_Lane_01", "Road_Lane_02.001" })
        {
            GameObject found = GameObject.Find(name);
            if (found == null || !PlacementGridLayout.TryMeasureRenderers(found, out Bounds b))
            {
                continue;
            }

            PlacementGridLayout.MeasureAcross(b, alongIsZ, out float w, out float c);
            float min = alongIsZ ? b.min.x : b.min.z;
            float max = alongIsZ ? b.max.x : b.max.z;
            Debug.Log($"[정밀] Renderer '{name}': 폭 {w:0.###}m 한가운데 {c:0.###} 범위 {min:0.###} ~ {max:0.###}");
        }

        // 실제 면: 진행 방향에 수직으로 5cm씩 훑는다.
        const float step = 0.05f;
        const float reach = 10f;
        float surfaceY = player.position.y;

        for (int forward = 0; forward <= 3; forward++)
        {
            Vector3 lineStart = player.position + direction * (forward * 6f);
            string previous = null;
            string transitions = "";

            for (float offset = -reach; offset <= reach; offset += step)
            {
                Vector3 point = lineStart + across * offset;
                TrySampleTop(point, surfaceY, out float _, out string what);
                string label = what ?? "없음";

                if (label != previous)
                {
                    // 절대 좌표도 같이 찍어야 격자 원점과 맞춰 볼 수 있다.
                    float absolute = alongIsZ ? point.x : point.z;
                    transitions += $"{offset:0.00}(={absolute:0.00})→{label}  ";
                    previous = label;
                }
            }

            Debug.Log($"[정밀] 앞으로 {forward * 6}m : {transitions}");
        }
    }

    // PlacementSystem.SampleSurfaceY와 같은 방식: 위에서 아래로 쏘아 제일 높은 면을 고른다.
    private static bool TrySampleTop(Vector3 point, float surfaceY, out float y, out string what)
    {
        const float up = 60f;
        const float down = 20f;

        RaycastHit[] hits = Physics.RaycastAll(
            new Vector3(point.x, surfaceY + up, point.z), Vector3.down, up + down, ~0, QueryTriggerInteraction.Ignore);

        y = surfaceY;
        what = null;

        foreach (RaycastHit hit in hits)
        {
            // PlacementSystem과 똑같이 플레이어는 지형에서 뺀다.
            if (PlayerLocator.FindPlayerRoot(hit.collider.transform) != null)
            {
                continue;
            }

            if (what == null || hit.point.y > y)
            {
                y = hit.point.y;
                what = hit.collider.name;
            }
        }

        return what != null;
    }

    // 배치 모드용: 길 폭 후보마다 함정이 어느 칸에 들어가는지 미리 계산해 본다.
    // (-executeMethod StageGridDiagnostics.PreviewAllStages)
    //
    // 칸 크기를 잘못 잡으면 서로 다른 함정이 같은 칸에 뭉쳐서, 원소를 어디에 놓아야 할지
    // 오히려 더 모호해진다. 그래서 씬을 고치기 전에 후보를 먼저 재 본다.
    public static void PreviewAllStages()
    {
        float[] candidates = { 3f, 4.5f, 6f, 9f, 15.5f };

        foreach (string path in StageScenes)
        {
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Debug.Log("──────────────────────────────────────────");
            Debug.Log($"[미리보기] 씬: {path}");

            foreach (float width in candidates)
            {
                PreviewLaneWidth(width);
            }
        }
    }

    // 길 폭을 잠시 이 값으로 바꿔 놓고 격자를 계산해, 함정이 어느 칸에 들어가는지 본다.
    // 씬은 저장하지 않으므로 원래 값으로 되돌려 놓는다.
    private static void PreviewLaneWidth(float laneWidth)
    {
        MapPlacementArea area = Object.FindFirstObjectByType<MapPlacementArea>();
        if (area == null)
        {
            Debug.LogWarning("[미리보기] MapPlacementArea가 없습니다.");
            return;
        }

        SerializedObject so = new SerializedObject(area);
        SerializedProperty widthProperty = so.FindProperty("roadWidthOverride");
        float original = widthProperty.floatValue;

        widthProperty.floatValue = laneWidth;
        so.ApplyModifiedPropertiesWithoutUndo();

        try
        {
            ReportLaneWidth(laneWidth);
        }
        finally
        {
            widthProperty.floatValue = original;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void ReportLaneWidth(float laneWidth)
    {
        if (!TrapPlacement.TryResolveGrid(out Vector3 origin, out float cellSize, out int columns, out int rows))
        {
            Debug.LogWarning($"[미리보기] 폭 {laneWidth}m: 격자를 계산하지 못했습니다.");
            return;
        }

        TrapPlacement.TryGetPathDirection(out Vector3 direction, out Transform player);
        bool alongIsZ = PlacementGridLayout.IsAlongZ(direction);

        StageGoalFlag flag = Object.FindFirstObjectByType<StageGoalFlag>();
        float pathCells = 0f;

        if (player != null && flag != null)
        {
            pathCells = Vector3.Dot(flag.transform.position - player.position, direction) / cellSize;
        }

        System.Collections.Generic.Dictionary<Vector2Int, string> taken =
            new System.Collections.Generic.Dictionary<Vector2Int, string>();

        string report = "";
        string clash = "";

        foreach (Transform trap in TrapCellAligner.CollectTrapRoots(SceneManager.GetActiveScene()))
        {
            Collider anchor = TrapPlacement.ResolveAnchorCollider(trap.gameObject);
            Vector3 point = anchor != null ? anchor.bounds.center : trap.position;

            Vector2Int cell = new Vector2Int(
                Mathf.FloorToInt((point.x - origin.x) / cellSize),
                Mathf.FloorToInt((point.z - origin.z) / cellSize));

            int alongCell = alongIsZ ? cell.y : cell.x;
            report += $"{trap.name}={alongCell}칸 ";

            if (taken.TryGetValue(cell, out string other))
            {
                clash += $"[{other} + {trap.name}가 같은 칸 {cell}] ";
            }
            else
            {
                taken[cell] = trap.name;
            }
        }

        Debug.Log(
            $"[미리보기] 길 폭 {laneWidth:0.##}m → 칸 {cellSize:0.###}m, 격자 {columns}x{rows}, " +
            $"동선 {pathCells:0.#}칸 | {report}{(clash.Length > 0 ? "⚠ " + clash : "겹침 없음")}");
    }

    // 배치 모드용: 손으로 끌었을 때처럼 함정·깃발을 엉뚱한 자리에 옮겨 놓고,
    // 칸 스냅이 제대로 도로 붙여 주는지 확인한다. (-executeMethod StageGridDiagnostics.TestSnapStage01)
    public static void TestSnapStage01()
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Stage_01.unity", OpenSceneMode.Single);

        Debug.Log("[스냅 검사] 손으로 끈 것처럼 어긋나게 옮긴 뒤 다시 붙여 봅니다.");

        // 칸 경계를 넘나들도록 제각각 다른 만큼 밀어 둔다.
        Vector3[] nudges =
        {
            new Vector3(0.7f, 0f, 1.3f),
            new Vector3(-1.1f, 0f, 0.4f),
            new Vector3(1.9f, 0f, -1.7f),
            new Vector3(-0.3f, 0f, 2.2f),
        };

        int index = 0;

        foreach (Transform trap in TrapCellAligner.CollectTrapRoots(scene))
        {
            trap.position += nudges[index % nudges.Length];
            index++;
        }

        StageGoalFlag flag = Object.FindFirstObjectByType<StageGoalFlag>();
        if (flag != null)
        {
            flag.transform.position += new Vector3(1.4f, 0f, -0.9f);
        }

        Debug.Log($"[스냅 검사] 함정 {index}개와 깃발을 어긋나게 옮겼습니다. 이제 스냅을 부릅니다.");

        int snapped = 0;

        foreach (Transform trap in TrapCellAligner.CollectTrapRoots(scene))
        {
            if (CellSnapDragging.SnapObject(trap))
            {
                snapped++;
            }
        }

        if (flag != null && CellSnapDragging.SnapObject(flag.transform))
        {
            snapped++;
        }

        Debug.Log($"[스냅 검사] {snapped}개를 칸에 붙였습니다. 결과를 확인합니다.");

        VerifyTrapsOnCells();
        VerifyGoalOnCell();

        // 검사용으로 흔들어 놓은 것이므로 저장하지 않는다. 씬은 그대로 둔다.
    }

    // 배치 모드용: 모든 스테이지에서 "그 칸에 원소를 놓으면 그 함정이 잡히는지"를 확인한다.
    // (-executeMethod StageGridDiagnostics.VerifyAllStagesReachable)
    public static void VerifyAllStagesReachable()
    {
        foreach (string path in StageScenes)
        {
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Debug.Log("──────────────────────────────────────────");
            Debug.Log($"[도달] 씬: {path}");
            VerifyTrapsReachable();
        }
    }

    // PlacementSystem이 칸 위의 함정을 찾을 때 쓰는 것과 똑같은 방식(OverlapBox)으로
    // 함정마다 "그 함정의 칸"을 훑어, 정말 그 함정이 잡히는지 본다.
    //
    // 칸만 맞아서는 부족하다. 판정 콜라이더가 칸을 벗어나 있거나 너무 높이 떠 있으면
    // 원소를 제자리에 놓아도 함정을 찾지 못한다.
    [MenuItem("Tools/Molra/진단 : 칸에서 함정이 잡히는지 확인")]
    public static void VerifyTrapsReachable()
    {
        if (!TrapPlacement.TryResolveGrid(out Vector3 origin, out float cellSize, out int _, out int _))
        {
            Debug.LogError("[도달] 격자를 계산하지 못했습니다.");
            return;
        }

        // PlacementSystem의 기본값과 같은 값이다. (trapSearchHeight = 4, 아래로 1m)
        const float searchHeight = 4f;
        const float below = 1f;

        float height = searchHeight + below;
        float half = height * 0.5f;

        int ok = 0;
        int missed = 0;

        foreach (Transform trap in TrapCellAligner.CollectTrapRoots(SceneManager.GetActiveScene()))
        {
            // 자동차·황소는 원소를 올려 끄는 함정이 아니라 흙 벽으로 막는 함정이다.
            // 칸 위에서 찾아질 이유가 없으므로 검사에서 뺀다.
            if (trap.GetComponentInChildren<ElementTrapCube>(true) == null &&
                trap.GetComponentInChildren<WaterTrap>(true) == null)
            {
                Debug.Log($"[도달] 해당없음 {trap.name}: 원소를 올려 끄는 함정이 아니라 벽으로 막는 함정입니다.", trap);
                continue;
            }

            Collider anchorCollider = TrapPlacement.ResolveAnchorCollider(trap.gameObject);
            Vector3 point = anchorCollider != null ? anchorCollider.bounds.center : trap.position;

            Vector2Int cell = new Vector2Int(
                Mathf.FloorToInt((point.x - origin.x) / cellSize),
                Mathf.FloorToInt((point.z - origin.z) / cellSize));

            Vector3 cellCenter = new Vector3(
                origin.x + (cell.x + 0.5f) * cellSize,
                origin.y,
                origin.z + (cell.y + 0.5f) * cellSize);

            // PlacementSystem은 그 칸의 지형 윗면을 기준으로 위아래를 훑는다.
            TrySampleTop(cellCenter, origin.y, out float surfaceY, out string _);
            Vector3 boxCenter = new Vector3(cellCenter.x, surfaceY + half - below, cellCenter.z);

            Collider[] hits = Physics.OverlapBox(
                boxCenter,
                new Vector3(cellSize * 0.5f, half, cellSize * 0.5f),
                Quaternion.identity,
                ~0,
                QueryTriggerInteraction.Collide);

            bool found = false;

            foreach (Collider hit in hits)
            {
                // 실행 중에 PlacementSystem이 쓰는 것과 같은 방법으로 함정을 거슬러 올라간다.
                if (hit.GetComponentInParent<IElementCounterTrap>() != null &&
                    hit.transform.IsChildOf(trap))
                {
                    found = true;
                    break;
                }
            }

            // 물 함정은 규약을 들고 있는 중계기(WaterTrapTriggerRelay)를 Awake에서 붙이므로
            // 에디터에서는 아직 없다. 콜라이더가 칸 안에 있는지만 확인한다.
            bool isWaterTrap = trap.GetComponentInChildren<WaterTrap>(true) != null;

            if (!found && isWaterTrap && anchorCollider != null)
            {
                Bounds b = anchorCollider.bounds;
                found = b.min.x <= cellCenter.x + cellSize * 0.5f && b.max.x >= cellCenter.x - cellSize * 0.5f
                     && b.min.z <= cellCenter.z + cellSize * 0.5f && b.max.z >= cellCenter.z - cellSize * 0.5f;
            }

            if (found)
            {
                ok++;
                Debug.Log($"[도달] OK   {trap.name}: 칸({cell.x},{cell.y})에서 잡힙니다." +
                          (isWaterTrap ? " (물 함정은 콜라이더 범위로 확인)" : ""), trap);
            }
            else
            {
                missed++;
                Debug.LogWarning($"[도달] 못 찾음 {trap.name}: 칸({cell.x},{cell.y})을 훑었는데 이 함정이 안 잡힙니다. " +
                                 $"판정 콜라이더 중심 {point}, 칸 한가운데 {cellCenter}", trap);
            }
        }

        Debug.Log($"[도달] 잡힘 {ok}개 / 못 찾음 {missed}개.");
    }

    // 배치 모드용: 놓을 자리 표시가 실제로 "비쳐 보이는" 재질로 만들어지는지 확인한다.
    // (-executeMethod StageGridDiagnostics.VerifyHighlightMaterial)
    //
    // URP Unlit 셰이더가 프로젝트에 없으면 표시가 불투명하게 칠해지거나 아예 안 보인다.
    // 눈으로만 확인하면 놓치기 쉬워서 셰이더와 렌더 큐를 직접 찍어 본다.
    [MenuItem("Tools/Molra/진단 : 놓을 자리 표시 재질 확인")]
    public static void VerifyHighlightMaterial()
    {
        Material opaque = WorldVisual.CreateUnlit(new Color(0.35f, 1f, 0.5f, 1f));
        Material pale = WorldVisual.CreateTransparentUnlit(new Color(0.35f, 1f, 0.5f, 0.4f));

        Debug.Log($"[표시] 불투명 Unlit : 셰이더={opaque.shader.name} 렌더큐={opaque.renderQueue}");
        Debug.Log($"[표시] 옅은 Unlit   : 셰이더={pale.shader.name} 렌더큐={pale.renderQueue} " +
                  $"색={pale.GetColor("_BaseColor")}");

        bool transparent = pale.renderQueue >= (int)UnityEngine.Rendering.RenderQueue.Transparent;

        if (transparent)
        {
            Debug.Log("[표시] OK — 놓을 자리 표시가 비쳐 보이는 재질로 만들어집니다.");
        }
        else
        {
            Debug.LogWarning("[표시] 렌더 큐가 불투명입니다. 표시가 바닥을 가릴 수 있습니다. " +
                             "Project Settings > Graphics > Always Included Shaders에 " +
                             "'Universal Render Pipeline/Unlit'을 넣어 주세요.");
        }
    }

    // 깃발도 칸 한가운데에 있는지 확인한다.
    [MenuItem("Tools/Molra/진단 : 깃발이 칸 한가운데인지 확인")]
    public static void VerifyGoalOnCell()
    {
        StageGoalFlag flag = Object.FindFirstObjectByType<StageGoalFlag>();
        if (flag == null)
        {
            Debug.LogWarning("[검사] 도착 깃발을 찾지 못했습니다.");
            return;
        }

        if (!TrapPlacement.TryResolveGrid(out Vector3 origin, out float cellSize, out int _, out int _))
        {
            Debug.LogError("[검사] 격자를 계산하지 못했습니다.");
            return;
        }

        Vector3 point = flag.transform.position;
        Vector3 center = TrapPlacement.SnapToCellCenter(point, origin, cellSize);

        float offX = Mathf.Abs(point.x - center.x);
        float offZ = Mathf.Abs(point.z - center.z);

        int cellX = Mathf.FloorToInt((point.x - origin.x) / cellSize);
        int cellZ = Mathf.FloorToInt((point.z - origin.z) / cellSize);

        Debug.Log($"[검사] {(offX < 0.01f && offZ < 0.01f ? "OK  " : "어긋남")} 깃발 {flag.name}: {point} " +
                  $"칸({cellX},{cellZ}) 어긋난 정도 x={offX:0.###} z={offZ:0.###}", flag);
    }

    // 깔아 둔 함정의 판정 콜라이더 한가운데가 정말 칸 한가운데에 있는지 확인한다.
    [MenuItem("Tools/Molra/진단 : 함정이 칸 한가운데인지 확인")]
    public static void VerifyTrapsOnCells()
    {
        if (!TrapPlacement.TryResolveGrid(out Vector3 origin, out float cellSize, out int columns, out int rows))
        {
            Debug.LogError("[검사] 격자를 계산하지 못했습니다.");
            return;
        }

        Debug.Log($"[검사] 격자 origin={origin} cellSize={cellSize:0.###} {columns}x{rows}");

        int checkedCount = 0;
        int offCell = 0;
        int clashes = 0;

        // 서로 다른 함정이 같은 칸에 들어가면 어느 함정을 끄려고 원소를 놓는 것인지 알 수 없다.
        System.Collections.Generic.Dictionary<Vector2Int, string> taken =
            new System.Collections.Generic.Dictionary<Vector2Int, string>();

        foreach (Transform trap in TrapCellAligner.CollectTrapRoots(SceneManager.GetActiveScene()))
        {
            Collider anchor = TrapPlacement.ResolveAnchorCollider(trap.gameObject);
            Vector3 point = anchor != null ? anchor.bounds.center : trap.position;
            Vector3 center = TrapPlacement.SnapToCellCenter(point, origin, cellSize);

            float offX = Mathf.Abs(point.x - center.x);
            float offZ = Mathf.Abs(point.z - center.z);
            bool ok = offX < 0.01f && offZ < 0.01f;

            if (!ok)
            {
                offCell++;
            }

            Vector2Int cell = new Vector2Int(
                Mathf.FloorToInt((point.x - origin.x) / cellSize),
                Mathf.FloorToInt((point.z - origin.z) / cellSize));

            if (taken.TryGetValue(cell, out string other))
            {
                clashes++;
                Debug.LogWarning($"[검사] 겹침 {trap.name}이(가) {other}와 같은 칸 {cell}에 있습니다.", trap);
            }
            else
            {
                taken[cell] = trap.name;
            }

            Debug.Log($"[검사] {(ok ? "OK  " : "어긋남")} {trap.name}: 판정 중심 {point} 칸({cell.x},{cell.y}) " +
                      $"어긋난 정도 x={offX:0.###} z={offZ:0.###}", trap);
            checkedCount++;
        }

        Debug.Log($"[검사] 함정 {checkedCount}개 확인 — 칸에서 어긋난 것 {offCell}개, 같은 칸에 겹친 것 {clashes}개.");
    }

    [MenuItem("Tools/Molra/진단 : 도로 폭 재기")]
    public static void Report()
    {
        Scene scene = SceneManager.GetActiveScene();
        Debug.Log($"[진단] 씬: {scene.path}");

        Transform player = null;
        Vector3 direction = Vector3.forward;

        if (TrapPlacement.TryGetPathDirection(out Vector3 dir, out Transform p))
        {
            direction = dir;
            player = p;
            Debug.Log($"[진단] 플레이어 {player.name} pos={player.position} 진행방향={direction}");
        }
        else
        {
            Debug.LogWarning("[진단] 플레이어를 찾지 못했습니다.");
        }

        StageGoalFlag goal = Object.FindFirstObjectByType<StageGoalFlag>();
        if (goal != null)
        {
            Debug.Log($"[진단] 깃발 pos={goal.transform.position}");
        }

        MapPlacementArea area = Object.FindFirstObjectByType<MapPlacementArea>();
        if (area != null)
        {
            SerializedObject so = new SerializedObject(area);
            Debug.Log($"[진단] MapPlacementArea cellSize={so.FindProperty("cellSize").floatValue} " +
                      $"groundObjectName={so.FindProperty("groundObjectName").stringValue}");
        }

        if (TrapPlacement.TryResolveGrid(out Vector3 origin, out float cellSize, out int columns, out int rows))
        {
            Debug.Log($"[진단] 격자 origin={origin} cellSize={cellSize} {columns}x{rows}");
        }

        // 씬 뿌리에 무엇이 있는지 먼저 훑는다. 어떤 것이 맵이고 어떤 것이 도로인지 감을 잡기 위해서다.
        string roots = "";
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            roots += rootObject.name + " / ";
        }

        Debug.Log($"[진단] 씬 뿌리: {roots}");

        // 바닥이 될 만한(넓고 납작한) 오브젝트를 전부 재 본다.
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string lower = t.name.ToLowerInvariant();
            bool interesting = lower.Contains("road") || lower.Contains("lane")
                || lower.Contains("ground") || lower.Contains("plane") || lower.Contains("map")
                || lower.Contains("path") || lower.Contains("floor");

            if (!interesting)
            {
                continue;
            }

            if (!TrapPlacement.TryMeasureVisualBounds(t.gameObject, out Bounds b))
            {
                Debug.Log($"[진단] {t.name}: Renderer 없음");
                continue;
            }

            // 진행 방향에 수직인 폭이 곧 도로 폭이다.
            float across = Mathf.Abs(direction.z) > Mathf.Abs(direction.x) ? b.size.x : b.size.z;
            float along = Mathf.Abs(direction.z) > Mathf.Abs(direction.x) ? b.size.z : b.size.x;

            Debug.Log($"[진단] 도로 후보 '{t.name}' center={b.center} size={b.size} " +
                      $"→ 폭(가로지르는 쪽)={across:0.00}m 길이={along:0.00}m");
        }
    }
}
