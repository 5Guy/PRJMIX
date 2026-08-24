using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 모든 스테이지의 배치 격자를 한 번에 맞춰 주는 도구.
//
// 칸 크기는 "길 폭 ÷ 칸 수(1 또는 3)"로 정해지는데, 스테이지마다 길의 생김새가 다르다.
//   Stage_01 : 아스팔트 도로를 걷는다. 폭 6.24m에 칸 3개가 들어간다.
//   Stage_02 : 숲속 흙길을 걷는다. 폭이 2.3m뿐이라 3칸으로 쪼개면 칸이 0.77m가 된다.
//              사람 한 명 설 자리도 안 되므로 길 하나를 통째로 1칸으로 본다.
//   Stage_03 : Stage_02와 같은 흙길이라 같은 규칙을 쓴다.
public static class AllStagesSetup
{
    private readonly struct StageConfig
    {
        public readonly string ScenePath;

        // 폭을 잴 도로 오브젝트 이름. 비어 있으면 아래 LaneWidth를 그대로 쓴다.
        public readonly string RoadObjectName;

        // 걸어다니는 길의 폭(m). 0이면 도로 오브젝트를 재서 구한다.
        public readonly float LaneWidth;

        // 길 폭에 칸을 몇 개 넣을지.
        public readonly PlacementGridLayout.RoadCells CellsAcross;

        // 길 한가운데를 옆으로 미는 값(m).
        public readonly float CenterOffset;

        // 도착 깃발을 플레이어 앞 몇 칸에 세울지. null이면 자리는 그대로 두고 칸에만 맞춘다.
        public readonly int? GoalCellsAhead;

        public readonly string Why;

        public StageConfig(
            string scenePath, string roadObjectName, float laneWidth,
            PlacementGridLayout.RoadCells cellsAcross, float centerOffset, int? goalCellsAhead, string why)
        {
            ScenePath = scenePath;
            RoadObjectName = roadObjectName;
            LaneWidth = laneWidth;
            CellsAcross = cellsAcross;
            CenterOffset = centerOffset;
            GoalCellsAhead = goalCellsAhead;
            Why = why;
        }
    }

    // 길 폭은 눈대중이 아니라 위에서 내려다본 그림을 찍어 픽셀로 잰 값이다.
    // (Tools > Molra > 진단 : 도로에 칸이 맞는지 그림으로 저장)
    //
    // 메시 크기(Renderer AABB)로 재면 안 된다. Stage_01의 Road_Lane_01은 인도까지 품고 있어서
    // 9.48m로 잡히는데, 눈에 보이는 아스팔트는 6.24m뿐이라 칸이 인도까지 삐져나갔다.
    private static readonly StageConfig[] Stages =
    {
        new StageConfig(
            "Assets/Scenes/Stage_01.unity",
            "Road_Lane_01",
            6.24f,
            PlacementGridLayout.RoadCells.Three,
            0f,
            12,
            "아스팔트 폭 6.24m를 3칸으로 → 칸 2.08m. 도로에 3칸이 딱 들어온다. " +
            "깃발은 12칸(약 25m) 앞으로 물린다"),

        new StageConfig(
            "Assets/Scenes/Stage_02.unity",
            "",
            2.303f,
            PlacementGridLayout.RoadCells.One,
            -0.176f,
            null,
            "흙길 폭 2.303m를 1칸으로 → 칸 2.303m. 길이 좁아 3칸으로 쪼개면 칸이 0.77m밖에 안 된다"),

        new StageConfig(
            "Assets/Scenes/Stage_03.unity",
            "",
            2.32f,
            PlacementGridLayout.RoadCells.One,
            0.044f,
            null,
            "흙길 폭 2.32m를 1칸으로 → 칸 2.32m. Stage_02와 같은 이유"),
    };

    [MenuItem("Tools/Molra/모든 스테이지 격자 맞추기")]
    public static void ApplyFromMenu()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Apply();
    }

    // 배치 모드용. (-executeMethod AllStagesSetup.Apply)
    public static void Apply()
    {
        foreach (StageConfig config in Stages)
        {
            Debug.Log("──────────────────────────────────────────");
            Debug.Log($"[격자 맞추기] {config.ScenePath} — {config.Why}");

            Scene scene = EditorSceneManager.OpenScene(config.ScenePath, OpenSceneMode.Single);

            if (!ApplyGridSettings(config))
            {
                continue;
            }

            // 격자가 바뀌었으니 함정과 깃발을 새 칸에 다시 맞춘다.
            // 높이는 건드리지 않는다. 이미 세워 둔 스테이지의 연출 높이가 어긋나면 안 되기 때문이다.
            TrapCellAligner.AlignOpenScene();
            // 깃발은 옮겨 달라고 한 스테이지만 건드린다.
            //
            // 자리를 그대로 두고 칸에만 맞추면, 깃발이 칸 경계 근처에 있을 때 옆 칸으로 끌려가
            // 함정과 같은 칸에 겹칠 수 있다. (Stage_03에서 실제로 모래바람 칸으로 끌려갔다)
            // 깃발은 칸에 맞아야 할 이유가 없으므로 요청받은 것만 옮긴다.
            if (config.GoalCellsAhead.HasValue)
            {
                StageGoalPlacer.Place(config.GoalCellsAhead, snapToGround: false);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            StageGridDiagnostics.VerifyTrapsOnCells();
            StageGridDiagnostics.VerifyGoalOnCell();
        }

        AssetDatabase.SaveAssets();
        Debug.Log("──────────────────────────────────────────");
        Debug.Log($"[격자 맞추기] 스테이지 {Stages.Length}개를 맞추고 저장했습니다.");
    }

    // MapPlacementArea의 길 관련 값을 스테이지에 맞게 적어 넣는다.
    private static bool ApplyGridSettings(StageConfig config)
    {
        MapPlacementArea area = Object.FindFirstObjectByType<MapPlacementArea>();

        if (area == null)
        {
            Debug.LogWarning($"[격자 맞추기] {config.ScenePath}에 MapPlacementArea가 없어 건너뜁니다.");
            return false;
        }

        SerializedObject so = new SerializedObject(area);

        so.FindProperty("roadObjectName").stringValue = config.RoadObjectName;
        so.FindProperty("roadWidthOverride").floatValue = config.LaneWidth;
        so.FindProperty("roadCenterOffset").floatValue = config.CenterOffset;
        so.FindProperty("cellsAcrossRoad").intValue = (int)config.CellsAcross;

        so.ApplyModifiedPropertiesWithoutUndo();

        if (!area.TryBuildLayout(out PlacementGridLayout.Result layout, out string failure))
        {
            Debug.LogError($"[격자 맞추기] {config.ScenePath}: {failure}", area);
            return false;
        }

        Debug.Log($"[격자 맞추기] {area.name}: {layout.Explanation}", area);
        return true;
    }
}
