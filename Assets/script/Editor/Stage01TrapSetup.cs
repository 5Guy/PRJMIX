using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Assets/Prefab/Trab 의 함정을 Stage_01 의 플레이어 동선 위에 한 번에 깔아 주는 도구.
// Tools > Molra > Stage_01 에 함정 깔기 로 실행한다.
//
// StageScene 5_dst 와 똑같이 "닿으면 죽고, 맞는 원소를 올리면 파훼되는" 상태로 놓는다.
//   FireTrab      : 물
//   StormTrabSet  : 상쇄 칸에 물 (모래바람 + 상쇄 칸 한 묶음)
//   WaterTrap     : 용암 / 나무다리 / 시멘트
//   Pickup        : 파훼가 아니라 흙 원소로 벽을 세워 막는다
//
// 위치는 눈대중이 아니라 계산해서 잡는다(TrapPlacement).
//   - 플레이어에서 깃발까지의 거리를 재서 고르게 나눈다
//   - 파훼 원소를 놓는 격자(PlacementGrid)의 칸 한가운데에 세운다
//   - 프리팹마다 다른 뿌리/모델 높이 차를 보정해 "보이는 바닥"을 지면에 붙인다
//
// 깔아 둔 다음 미세 조정은 Tools > Molra > 함정 배치 창 에서 한다.
public static class Stage01TrapSetup
{
    private const string ScenePath = "Assets/Scenes/Stage_01.unity";
    private const string TrapRootName = "Traps";

    // 함정 하나를 어디에 어떻게 세울지.
    private readonly struct Placement
    {
        public readonly string PrefabName;
        public readonly float Distance;      // 플레이어에서 진행 방향으로 몇 m 앞인지
        public readonly float SideOffset;    // 진행 방향 기준 오른쪽이 +
        public readonly bool FacePlayer;     // 플레이어 쪽을 바라보게 돌린다 (돌진하는 자동차)
        public readonly string Counter;      // 로그로 안내할 파훼 방법

        public Placement(string prefabName, float distance, float sideOffset, bool facePlayer, string counter)
        {
            PrefabName = prefabName;
            Distance = distance;
            SideOffset = sideOffset;
            FacePlayer = facePlayer;
            Counter = counter;
        }
    }

    // 거리는 "플레이어 → 깃발" 구간 길이를 1로 본 비율이 아니라 실제 미터다.
    // 구간이 이보다 짧으면 아래에서 비율로 줄인다.
    //
    // 첫 함정을 8m까지 미룬 이유: 함정 앞 접근 트리거는 함정에서 3.5m 앞에 선다.
    // 출발 지점에 너무 가까우면 플레이어가 처음부터 그 트리거 안에 서 있게 되어
    // 원소를 올리자마자 함정이 꺼져 버린다.
    //
    // 마지막 Pickup은 깃발 너머에서 플레이어 쪽으로 달려온다. 출발 트리거가 차보다
    // 8.8m 앞(= 플레이어 쪽)에 붙어 있어서, 물 함정과 깃발 사이에서 발동한다.
    private static readonly Placement[] Layout =
    {
        new Placement("FireTrab",     8f,  0f, false, "비구름 / 진흙 / 쓰나미"),
        new Placement("StormTrabSet", 14f, 0f, false, "상쇄 칸에 수증기 / 늪 / 바람"),
        new Placement("WaterTrap",    20f, 0f, false, "용암 / 나무다리 / 시멘트"),
        new Placement("Pickup",       27f, 0f, true,  "흑요석 / 바람 / 늪 (또는 흙으로 벽 세워 막기)"),
    };

    // 위 거리는 이 정도 길이의 동선을 기준으로 잡았다. 실제 동선이 짧으면 비율로 줄인다.
    private const float ReferencePathLength = 22f;

    // 함정 앞 접근 트리거(기본 3.5m)가 앞 함정에 걸치지 않도록 지키는 최소 간격.
    private const float MinimumGap = 4f;

    [MenuItem("Tools/Molra/Stage_01 에 함정 깔기")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        if (!TrapPlacement.TryGetPathDirection(out Vector3 direction, out Transform player))
        {
            Debug.LogError("[Stage_01 함정] Player 태그가 붙은 오브젝트를 찾지 못해 동선을 알 수 없습니다.");
            return;
        }

        float pathLength = ResolvePathLength(player, direction);
        Debug.Log($"[Stage_01 함정] 동선 길이 {pathLength:0.0}m, 진행 방향 {direction}");

        if (!TryPrepareRoot(scene, out Transform root))
        {
            return;
        }

        DeactivateExistingTraps(scene, root);

        bool hasGrid = TrapPlacement.TryResolveGrid(out Vector3 gridOrigin, out float cellSize, out int _, out int _);
        if (!hasGrid)
        {
            Debug.LogWarning("[Stage_01 함정] MapPlacementArea의 격자를 계산하지 못해 칸 맞춤을 건너뜁니다. 함정 위에 원소를 올리기 어려울 수 있습니다.");
        }

        // 배치 거리는 실제 동선 안에 들어오도록 줄인다. (마지막 자동차는 깃발 너머라 예외)
        float scale = Mathf.Min(1f, pathLength / ReferencePathLength);

        int placed = 0;
        List<Vector3> used = new List<Vector3>();

        foreach (Placement entry in Layout)
        {
            GameObject prefab = LoadPrefab(entry.PrefabName);
            if (prefab == null)
            {
                Debug.LogWarning($"[Stage_01 함정] {TrapPlacement.TrapFolder}/{entry.PrefabName}.prefab 을 찾지 못해 건너뜁니다.");
                continue;
            }

            Vector3 point = player.position + direction * (entry.Distance * scale);
            point += Vector3.Cross(Vector3.up, direction).normalized * entry.SideOffset;

            if (hasGrid)
            {
                point = TrapPlacement.SnapToCellCenter(point, gridOrigin, cellSize);
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.SetParent(root, true);

            // 회전을 먼저 잡아야 판정 콜라이더의 위치(=뿌리와의 어긋남)가 제대로 계산된다.
            instance.transform.rotation = Quaternion.LookRotation(entry.FacePlayer ? -direction : direction, Vector3.up);
            instance.transform.position = point;

            TrapPlacement.AlignHorizontally(instance.transform, point);
            TrapPlacement.SnapToGround(instance.transform, TrapPlacement.SampleGroundY(point, player.position.y));

            WarnIfCrowded(instance, used);
            used.Add(instance.transform.position);

            placed++;
            Debug.Log($"[Stage_01 함정] {entry.PrefabName} → {instance.transform.position} (파훼: {entry.Counter})", instance);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"[Stage_01 함정] 함정 {placed}개를 '{TrapRootName}' 아래에 깔고 씬을 저장했습니다.");
    }

    // 플레이어에서 깃발까지, 진행 방향으로 잰 거리. 깃발이 없으면 넉넉한 기본값.
    private static float ResolvePathLength(Transform player, Vector3 direction)
    {
        StageGoalFlag goal = Object.FindFirstObjectByType<StageGoalFlag>();
        if (goal == null)
        {
            Debug.LogWarning("[Stage_01 함정] 도착 깃발(StageGoalFlag)을 찾지 못해 동선 길이를 25m로 봅니다.");
            return 25f;
        }

        return Mathf.Max(1f, Vector3.Dot(goal.transform.position - player.position, direction));
    }

    private static GameObject LoadPrefab(string prefabName)
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>($"{TrapPlacement.TrapFolder}/{prefabName}.prefab");
    }

    // 이미 한 번 깔아 두었으면 지우고 다시 깐다. 사용자가 거절하면 아무것도 하지 않는다.
    private static bool TryPrepareRoot(Scene scene, out Transform root)
    {
        root = null;

        foreach (GameObject candidate in scene.GetRootGameObjects())
        {
            if (candidate.name != TrapRootName)
            {
                continue;
            }

            bool rebuild = EditorUtility.DisplayDialog(
                "함정 다시 깔기",
                $"'{TrapRootName}' 아래에 이미 함정이 깔려 있습니다.\n지우고 다시 깔까요?",
                "다시 깔기",
                "취소");

            if (!rebuild)
            {
                return false;
            }

            Object.DestroyImmediate(candidate);
            break;
        }

        root = new GameObject(TrapRootName).transform;
        return true;
    }

    // 씬에 흩어져 있던 예전 함정을 끈다. 지우지 않고 비활성화만 하므로 언제든 되살릴 수 있다.
    //
    // Stage_01 의 불 함정은 도로 길이만 한(z 약 73m) 트리거를 달고 바닥 아래(y 약 -4.9)에 서 있어서,
    // 그대로 두면 출발하자마자 죽고 파훼할 기회도 없다.
    private static void DeactivateExistingTraps(Scene scene, Transform newRoot)
    {
        List<GameObject> stale = new List<GameObject>();

        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            if (rootObject.transform == newRoot)
            {
                continue;
            }

            bool isTrap =
                rootObject.GetComponentInChildren<ElementTrapCube>(true) != null ||
                rootObject.GetComponentInChildren<WaterTrap>(true) != null ||
                rootObject.GetComponentInChildren<Sandstorm>(true) != null ||
                rootObject.GetComponentInChildren<ChargingCar>(true) != null;

            if (isTrap)
            {
                stale.Add(rootObject);
            }
        }

        if (stale.Count == 0)
        {
            return;
        }

        string names = string.Join(", ", stale.ConvertAll(item => item.name));
        bool disable = EditorUtility.DisplayDialog(
            "예전 함정 정리",
            $"씬에 이미 함정이 {stale.Count}개 있습니다.\n\n{names}\n\n" +
            "새로 깐 함정과 겹치지 않도록 이것들을 꺼 둘까요? (지우지 않고 비활성화만 합니다)",
            "꺼 두기",
            "그대로 두기");

        if (!disable)
        {
            return;
        }

        foreach (GameObject item in stale)
        {
            Undo.RecordObject(item, "예전 함정 끄기");
            item.SetActive(false);
            EditorUtility.SetDirty(item);
        }

        Debug.Log($"[Stage_01 함정] 예전 함정 {stale.Count}개를 껐습니다: {names}");
    }

    private static void WarnIfCrowded(GameObject instance, List<Vector3> used)
    {
        foreach (Vector3 other in used)
        {
            float gap = Vector3.Distance(
                new Vector3(other.x, 0f, other.z),
                new Vector3(instance.transform.position.x, 0f, instance.transform.position.z));

            if (gap < MinimumGap)
            {
                Debug.LogWarning(
                    $"[Stage_01 함정] '{instance.name}'이 앞 함정과 {gap:0.0}m밖에 안 떨어져 있습니다. " +
                    "접근 트리거가 겹칠 수 있으니 배치 창에서 벌려 주세요.",
                    instance);
                return;
            }
        }
    }
}
