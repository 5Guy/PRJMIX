using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 배치 모드에서 스테이지 상태를 한눈에 찍어 보는 진단 도구.
// Unity -batchmode -executeMethod MolraBatchReport.ReportAllStages
public static class MolraBatchReport
{
    private static readonly string[] Stages =
    {
        "Assets/Scenes/Stage_01.unity",
        "Assets/Scenes/Stage_02.unity",
        "Assets/Scenes/Stage_03.unity",
    };

    // 스테이지마다 "함정이 칸 한가운데인지 / 깃발이 칸 한가운데인지"를 한 번에 확인한다.
    // StageGridDiagnostics의 검사들은 지금 열려 있는 씬만 보므로, 씬을 하나씩 열어 주어야 한다.
    public static void VerifyAllStages()
    {
        foreach (string path in Stages)
        {
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Debug.Log($"=========== {path}");

            StageGridDiagnostics.VerifyTrapsOnCells();
            StageGridDiagnostics.VerifyGoalOnCell();
        }

        Debug.Log("[MolraBatchReport] 칸 맞춤 검사 끝");
    }

    public static void ReportAllStages()
    {
        StringBuilder report = new StringBuilder();

        foreach (string path in Stages)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            report.AppendLine("=========== " + scene.name);

            Transform player = TrapPlacement.TryGetPathDirection(out Vector3 dir, out Transform p) ? p : null;
            report.AppendLine($"  path direction {dir}  player {(player != null ? player.position.ToString() : "?")}");

            StageGoalFlag goal = Object.FindFirstObjectByType<StageGoalFlag>();
            report.AppendLine($"  goal {(goal != null ? goal.transform.position.ToString() : "?")}");

            MapPlacementArea area = Object.FindFirstObjectByType<MapPlacementArea>();
            PlacementGridLayout.Result layout = default;
            string failure = "MapPlacementArea가 없습니다.";

            if (area != null && area.TryBuildLayout(out layout, out failure))
            {
                report.AppendLine($"  grid origin {layout.Origin} cell {layout.CellSize:F3} {layout.Columns}x{layout.Rows}");
                report.AppendLine("  " + layout.Explanation);
            }
            else
            {
                report.AppendLine("  grid: " + failure);
            }

            foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour is not IElementCounterTrap)
                {
                    continue;
                }

                Bounds bounds = ElementVisual.MeasureBounds(behaviour.gameObject);
                Transform root = behaviour.transform.root;
                report.AppendLine(
                    $"  TRAP {behaviour.GetType().Name} on '{behaviour.name}' (root '{root.name}', active={behaviour.gameObject.activeInHierarchy}) " +
                    $"pos {behaviour.transform.position} bounds c{bounds.center} s{bounds.size}");
            }
        }

        Debug.Log(report.ToString());
        Debug.Log("[MolraBatchReport] 끝");
    }
}
