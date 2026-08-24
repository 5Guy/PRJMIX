using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 이미 깔아 둔 함정을 배치 칸(PlacementGrid의 칸) 한가운데에 다시 맞춰 주는 도구.
//
// 파훼 원소는 칸 단위로 놓인다. 함정이 칸 경계에 걸쳐 서 있으면 어느 칸에 올려야 하는지
// 애매해지고, 옆 칸에 놓았는데 먹히는 일도 생긴다. 그것을 한 번에 없애는 것이 이 도구다.
//
// 칸 크기는 도로 폭 ÷ 칸 수(1 또는 3)라서 도로 폭이 바뀌거나 칸 수를 바꾸면 격자가 달라진다.
// 그때마다 다시 돌려 주어야 함정과 칸이 계속 맞는다.
//
// 높이는 건드리지 않는다(x/z만 옮긴다). 함정 높이는 함정 배치 창이 따로 맞춘다.
public static class TrapCellAligner
{
    private static readonly string[] StageScenes =
    {
        "Assets/Scenes/Stage_01.unity",
        "Assets/Scenes/Stage_02.unity",
        "Assets/Scenes/Stage_03.unity",
    };

    [MenuItem("Tools/Molra/함정을 배치 칸에 맞추기 (열린 씬)")]
    public static void AlignOpenScene()
    {
        Scene scene = SceneManager.GetActiveScene();

        if (!Align(scene, out int moved, out int skipped, out string reason))
        {
            Debug.LogWarning($"[배치 칸 맞춤] {scene.name}: {reason}");
            return;
        }

        if (moved > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }

        Debug.Log($"[배치 칸 맞춤] {scene.name}: 함정 {moved}개를 칸 한가운데로 옮겼습니다. " +
                  $"자동으로 맞출 수 없어 건너뛴 것 {skipped}개. (저장은 직접 해 주세요)");
    }

    [MenuItem("Tools/Molra/함정을 배치 칸에 맞추기 (모든 스테이지)")]
    public static void AlignAllStages()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        int total = 0;

        foreach (string path in StageScenes)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            if (!Align(scene, out int moved, out string reason))
            {
                Debug.LogWarning($"[배치 칸 맞춤] {scene.name}: {reason}");
                continue;
            }

            total += moved;

            if (moved > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log($"[배치 칸 맞춤] {scene.name}: 함정 {moved}개를 칸 한가운데로 옮겼습니다.");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[배치 칸 맞춤] 스테이지 {StageScenes.Length}개에서 함정 {total}개를 맞추고 저장했습니다.");
    }

    private static bool Align(Scene scene, out int moved, out string reason)
    {
        return Align(scene, out moved, out int _, out reason);
    }

    private static bool Align(Scene scene, out int moved, out int skipped, out string reason)
    {
        moved = 0;
        skipped = 0;
        reason = null;

        if (!TrapPlacement.TryResolveGrid(out Vector3 origin, out float cellSize, out int _, out int _))
        {
            reason = "MapPlacementArea의 격자를 계산하지 못했습니다. 격자를 깔 맵 오브젝트가 물려 있는지 확인하세요.";
            return false;
        }

        List<Transform> traps = CollectTrapRoots(scene);

        if (traps.Count == 0)
        {
            reason = "칸에 맞출 함정이 없습니다. (아직 함정을 깔지 않은 스테이지면 정상입니다)";
            return false;
        }

        foreach (Transform trap in traps)
        {
            // 함정이 서 있는 자리가 지금 어느 칸인지 보고, 그 칸의 한가운데로 옮긴다.
            Vector3 current = AnchorPoint(trap);
            Vector3 target = TrapPlacement.SnapToCellCenter(current, origin, cellSize);

            if (IsAt(current, target))
            {
                continue;
            }

            Vector3 before = trap.position;

            Undo.RecordObject(trap, "Align Trap To Cell");
            TrapPlacement.AlignHorizontally(trap, target);

            // 옮겼는데도 판정 자리가 따라오지 않으면 판정 콜라이더가 이 오브젝트 밖에 있는 것이다.
            //
            // Stage_01의 물 함정이 그렇다. 관리 스크립트(WaterTrapManagement)와 실제 물 모델
            // (WaterTrapModel)이 부모-자식이 아니라 형제라, 관리 쪽만 옮기면 판정은 제자리에 남는다.
            // 이럴 때 억지로 옮기면 함정이 두 조각으로 갈라지므로 되돌리고 알린다.
            if (!IsAt(AnchorPoint(trap), target))
            {
                trap.position = before;
                Physics.SyncTransforms();

                Debug.LogWarning(
                    $"[배치 칸 맞춤] '{trap.name}'은(는) 판정 콜라이더가 이 오브젝트 밖에 있어 자동으로 맞출 수 없습니다. " +
                    "함정이 여러 오브젝트로 쪼개져 있는 경우입니다. 손으로 함께 옮겨 주세요.",
                    trap);

                skipped++;
                continue;
            }

            EditorUtility.SetDirty(trap);
            moved++;
        }

        return true;
    }

    // 이 함정이 서 있는 자리. 계산은 TrapPlacement 한 곳에만 둔다.
    private static Vector3 AnchorPoint(Transform trap)
    {
        return TrapPlacement.ResolveAnchorPoint(trap.gameObject);
    }

    // 가로 위치가 목표와 같은지. 높이는 보지 않는다.
    private static bool IsAt(Vector3 point, Vector3 target)
    {
        return Mathf.Abs(point.x - target.x) < 0.01f && Mathf.Abs(point.z - target.z) < 0.01f;
    }

    // 함정 하나 = 프리팹 인스턴스 하나로 본다.
    //
    // 모래바람처럼 함정 하나가 여러 부품(휩쓸림 판정 + 공중에 뜬 상쇄 칸)으로 되어 있는데,
    // 부품을 따로 옮기면 함정이 통째로 흐트러진다. 함정 부품을 품고 있는 가장 바깥
    // 오브젝트만 골라서 통째로 옮긴다.
    public static List<Transform> CollectTrapRoots(Scene scene)
    {
        List<Transform> roots = new List<Transform>();
        HashSet<Transform> seen = new HashSet<Transform>();

        // 함정 부품을 먼저 찾고, 거기서 프리팹 인스턴스의 뿌리까지 거슬러 올라간다.
        // 프리팹이 아니면(손으로 만든 함정) 부품이 붙은 오브젝트를 그대로 쓴다.
        foreach (Component part in CollectTrapParts())
        {
            if (part == null || !part.gameObject.activeInHierarchy || part.gameObject.scene != scene)
            {
                continue;
            }

            Transform root = ResolveMoveRoot(part);

            if (seen.Add(root))
            {
                roots.Add(root);
            }
        }

        return roots;
    }

    // 씬 뷰에서 고른 오브젝트가 함정의 일부라면, 통째로 옮겨야 할 바깥 뿌리를 돌려준다.
    // 함정이 아니면 null.
    public static Transform ResolveTrapRoot(Transform selected)
    {
        if (selected == null)
        {
            return null;
        }

        Component part = FindTrapPart(selected);
        if (part == null)
        {
            return null;
        }

        return ResolveMoveRoot(part);
    }

    // 함정 하나를 통째로 옮기려면 어느 오브젝트를 잡아야 하는지.
    //
    // 프리팹 인스턴스면 그 뿌리가 곧 함정 하나다.
    // 프리팹이 아니면(손으로 짜 맞춘 함정) 부품이 붙은 오브젝트만 잡아서는 안 된다.
    // Stage_01의 물 함정이 그렇다.
    //
    //   WaterTrap 1            <- 실제로 옮겨야 하는 오브젝트
    //   ├─ WaterTrapModel      <- 판정 콜라이더가 여기 있다
    //   └─ WaterTrapManagement <- WaterTrap 스크립트가 여기 있다
    //
    // 스크립트가 붙은 쪽만 옮기면 판정은 제자리에 남아 함정이 두 조각으로 갈라진다.
    // 그래서 "같은 함정만 품고 있는" 동안 부모로 계속 올라간다.
    // 부모가 다른 함정까지 품기 시작하면(Traps 같은 그릇) 거기서 멈춘다.
    private static Transform ResolveMoveRoot(Component part)
    {
        GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(part.gameObject);
        if (prefabRoot != null)
        {
            return prefabRoot.transform;
        }

        Transform root = part.transform;
        int parts = CountTrapParts(root);

        while (root.parent != null && CountTrapParts(root.parent) == parts)
        {
            root = root.parent;
        }

        return root;
    }

    // 이 오브젝트 아래에 함정 부품이 몇 개 있는지. 부모로 올라가도 될지 판단하는 데 쓴다.
    private static int CountTrapParts(Transform node)
    {
        return node.GetComponentsInChildren<ElementTrapCube>(true).Length
            + node.GetComponentsInChildren<WaterTrap>(true).Length
            + node.GetComponentsInChildren<Sandstorm>(true).Length
            + node.GetComponentsInChildren<ChargingCar>(true).Length
            + node.GetComponentsInChildren<ChargingBull>(true).Length;
    }

    // 고른 오브젝트가 함정 부품이거나, 함정 부품을 품고 있거나, 함정 부품의 자식인지 본다.
    private static Component FindTrapPart(Transform selected)
    {
        return FindTrapPartInParents(selected) ?? FindTrapPartInChildren(selected);
    }

    private static Component FindTrapPartInParents(Transform selected)
    {
        return (Component)selected.GetComponentInParent<ElementTrapCube>()
            ?? (Component)selected.GetComponentInParent<WaterTrap>()
            ?? (Component)selected.GetComponentInParent<Sandstorm>()
            ?? (Component)selected.GetComponentInParent<ChargingCar>()
            ?? selected.GetComponentInParent<ChargingBull>();
    }

    private static Component FindTrapPartInChildren(Transform selected)
    {
        return (Component)selected.GetComponentInChildren<ElementTrapCube>(true)
            ?? (Component)selected.GetComponentInChildren<WaterTrap>(true)
            ?? (Component)selected.GetComponentInChildren<Sandstorm>(true)
            ?? (Component)selected.GetComponentInChildren<ChargingCar>(true)
            ?? selected.GetComponentInChildren<ChargingBull>(true);
    }

    private static IEnumerable<Component> CollectTrapParts()
    {
        List<Component> parts = new List<Component>();

        parts.AddRange(Object.FindObjectsByType<ElementTrapCube>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        parts.AddRange(Object.FindObjectsByType<WaterTrap>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        parts.AddRange(Object.FindObjectsByType<Sandstorm>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        parts.AddRange(Object.FindObjectsByType<ChargingCar>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        parts.AddRange(Object.FindObjectsByType<ChargingBull>(FindObjectsInactive.Include, FindObjectsSortMode.None));

        return parts;
    }
}
