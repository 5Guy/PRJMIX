using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// CraftingPanelUI.AutoFillData()는 UNITY_EDITOR 전용이라 빌드에는 포함되지 않는다.
// 그래서 에디터에서 플레이할 때는 Awake마다 Assets/Data/Nomal, Assets/Data/Combination을
// 다시 스캔해 항상 최신이지만, 실제 빌드는 프리팹에 마지막으로 저장된 목록만 그대로 쓴다.
// 조합식을 늘려 놓고 프리팹 저장을 잊으면 빌드에서만 조합이 안 되는 사고가 나므로,
// 빌드 직전에 이 스캔을 대신 실행해서 프리팹에 최신 상태로 구워 넣는다.
public class CraftingDataBuildPreprocessor : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        int updated = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:CraftingPanelUI"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            CraftingPanelUI panel = AssetDatabase.LoadAssetAtPath<CraftingPanelUI>(path);
            if (panel == null)
            {
                continue;
            }

            panel.AutoFillData();
            EditorUtility.SetDirty(panel);
            updated++;
        }

        if (updated > 0)
        {
            AssetDatabase.SaveAssets();
        }

        Debug.Log($"[CraftingDataBuildPreprocessor] CraftingPanelUI 프리팹 {updated}개의 원소/조합 데이터를 빌드 전에 갱신했습니다.");

        CheckForStaleSceneOverrides();
    }

    // 씬 안의 CraftingPanelUI 인스턴스에서 실수로 "자동 채우기"를 실행하면 combinations/
    // startingElements가 그 씬만의 프리팹 오버라이드로 고정돼서, 이후 프리팹을 갱신해도
    // (위 로직) 그 씬에는 반영되지 않는다(Stage_03에서 실제로 이 사고가 나서 조합이
    // 용암 하나만 되는 문제가 있었다). 빌드에 조용히 실려 나가지 않도록, 이런 오버라이드가
    // 남아 있으면 빌드를 막고 어느 씬인지 알려준다.
    private static void CheckForStaleSceneOverrides()
    {
        List<string> offendingScenes = new List<string>();

        foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
        {
            if (!buildScene.enabled || !File.Exists(buildScene.path))
            {
                continue;
            }

            string text = File.ReadAllText(buildScene.path);
            if (text.Contains("combinations.Array") || text.Contains("startingElements.Array"))
            {
                offendingScenes.Add(buildScene.path);
            }
        }

        if (offendingScenes.Count == 0)
        {
            return;
        }

        StringBuilder message = new StringBuilder();
        message.AppendLine("다음 씬의 CraftingPanelUI에 combinations/startingElements가 프리팹 오버라이드로 고정되어 있습니다.");
        message.AppendLine("씬을 열어 해당 오브젝트를 선택하고, 인스펙터에서 combinations/startingElements 항목을 우클릭 → Revert 해주세요.");
        foreach (string scene in offendingScenes)
        {
            message.AppendLine($" - {scene}");
        }

        throw new BuildFailedException(message.ToString());
    }
}
