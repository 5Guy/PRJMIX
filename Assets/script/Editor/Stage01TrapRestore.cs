using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Stage_01을 원래 함정 구성으로 되돌린다.
//
// "Tools > Molra > Stage_01 에 함정 깔기"는 씬에 있던 함정을 꺼 두고 Traps 아래에 네 개를
// 새로 깐다. 원래 스테이지는 FireTrab과 WaterTrap 1 두 개짜리였으므로 그 상태로 되돌린다.
//
// 되돌린 뒤에는 함정을 새 격자 칸 한가운데에 맞춘다. 칸 크기가 도로 폭에서 나오도록 바뀌었기
// 때문에, 예전 자리 그대로 두면 칸 경계에 걸쳐서 원소를 어디에 올려야 할지 애매해진다.
public static class Stage01TrapRestore
{
    private const string ScenePath = "Assets/Scenes/Stage_01.unity";
    private const string GeneratedRootName = "Traps";

    // 원래 씬에 있던 함정들. 함정 깔기 도구가 꺼 두었던 것들이다.
    private static readonly string[] OriginalTraps = { "FireTrab", "WaterTrap 1" };

    [MenuItem("Tools/Molra/Stage_01 함정을 원래대로 되돌리기")]
    public static void RestoreFromMenu()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Restore();
    }

    // 배치 모드용. (-executeMethod Stage01TrapRestore.Restore)
    public static void Restore()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        int removed = RemoveGeneratedTraps(scene);
        int revived = ReviveOriginalTraps(scene);

        if (revived == 0)
        {
            Debug.LogError(
                "[복원] 원래 함정을 찾지 못했습니다. 씬을 저장하지 않고 멈춥니다. " +
                $"찾던 이름: {string.Join(", ", OriginalTraps)}");
            return;
        }

        // 칸 크기가 바뀌었으므로 되살린 함정을 칸 한가운데에 맞춘다. 높이는 건드리지 않는다.
        TrapCellAligner.AlignOpenScene();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"[복원] 새로 깔았던 함정 {removed}개를 지우고 원래 함정 {revived}개를 되살렸습니다.");

        StageGridDiagnostics.VerifyTrapsOnCells();
        StageGridDiagnostics.VerifyGoalOnCell();
        StageGridDiagnostics.VerifyTrapsReachable();

        WarnIfOffPath();
    }

    private static int RemoveGeneratedTraps(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != GeneratedRootName)
            {
                continue;
            }

            int count = root.transform.childCount;
            Object.DestroyImmediate(root);
            return count;
        }

        return 0;
    }

    private static int ReviveOriginalTraps(Scene scene)
    {
        int revived = 0;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (System.Array.IndexOf(OriginalTraps, root.name) < 0)
            {
                continue;
            }

            if (!root.activeSelf)
            {
                root.SetActive(true);
                EditorUtility.SetDirty(root);
            }

            revived++;
        }

        return revived;
    }

    // 되살린 함정이 플레이어 동선 위에 있는지 확인한다.
    //
    // 원래 씬의 FireTrab은 출발 지점보다 한참 뒤(그리고 공중)에 서 있었다.
    // 그대로 되살리면 플레이어가 영영 만나지 못하므로, 조용히 넘어가지 않고 알린다.
    private static void WarnIfOffPath()
    {
        if (!TrapPlacement.TryGetPathDirection(out Vector3 direction, out Transform player))
        {
            return;
        }

        StageGoalFlag goal = Object.FindFirstObjectByType<StageGoalFlag>();
        float goalDistance = goal != null
            ? Vector3.Dot(goal.transform.position - player.position, direction)
            : float.MaxValue;

        foreach (Transform trap in TrapCellAligner.CollectTrapRoots(SceneManager.GetActiveScene()))
        {
            Vector3 point = TrapPlacement.ResolveAnchorPoint(trap.gameObject);

            float ahead = Vector3.Dot(point - player.position, direction);
            float height = point.y - player.position.y;

            List<string> problems = new List<string>();

            if (ahead < 0f)
            {
                problems.Add($"출발 지점보다 {-ahead:0.0}m 뒤에 있습니다");
            }
            else if (ahead > goalDistance + 5f)
            {
                problems.Add($"깃발보다 {ahead - goalDistance:0.0}m 더 멀리 있습니다");
            }

            if (height > 3f)
            {
                problems.Add($"플레이어 발밑보다 {height:0.0}m 높이 떠 있습니다");
            }

            if (problems.Count > 0)
            {
                Debug.LogWarning(
                    $"[복원] '{trap.name}'은(는) 동선 밖입니다 — {string.Join(", ", problems)}. " +
                    "플레이어가 이 함정을 만나지 못합니다.",
                    trap);
            }
            else
            {
                Debug.Log($"[복원] '{trap.name}': 출발 지점에서 {ahead:0.0}m 앞, 동선 위에 있습니다.", trap);
            }
        }
    }
}
