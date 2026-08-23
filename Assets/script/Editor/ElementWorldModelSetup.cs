using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// Assets/Data/Nomal 안의 원소 ScriptableObject마다 "월드" 칸(worldModel)에 3D 모델을 물려 준다.
// Tools > Molra > 원소 월드 모델 채우기 로 실행한다.
//
// worldModel이 비어 있으면 맵에 놓았을 때 원소 색 구만 나오므로, 원소마다 어울리는 프리팹을 지정한다.
// 크기와 피벗은 ElementVisual.CreateWorldModel이 칸에 맞춰 자동으로 맞추므로 여기서는 무엇을 쓸지만 정한다.
//
// 이미 손으로 넣어 둔 원소는 건드리지 않는다(덮어쓰려면 아래 '전부 다시 채우기'를 쓴다).
public static class ElementWorldModelSetup
{
    private const string ElementFolder = "Assets/Data/Nomal";
    private const string Pandazole = "Assets/Pandazole_Ultimate_Pack/Pandazole Nature Environment Pack/Prefabs";

    // 원소 에셋 이름 -> 물려 줄 프리팹 경로.
    // 마음에 안 드는 모습은 인스펙터에서 바로 다른 프리팹으로 바꿔도 된다.
    private static readonly Dictionary<string, string> ModelByElement = new Dictionary<string, string>
    {
        // 기본 원소
        { "Water",      Pandazole + "/TileWater_01.prefab" },
        { "Fire",       "Assets/WoodsLifestyle/Prefabs/Miscellaneous/CampFire_01.prefab" },
        { "Wood",       Pandazole + "/Log_01.prefab" },
        { "Iron",       Pandazole + "/MineralNode_01_A.prefab" },

        // 물 계열
        { "Steam",      "Assets/PolyMount/LowpolyVegetationPackFree/Prefabs/Cloud_3.prefab" },
        { "Cloud",      "Assets/PolyMount/LowpolyVegetationPackFree/Prefabs/Cloud_3.prefab" },
        { "Tsunami",    Pandazole + "/TileWater_20.prefab" },
        { "Swamp",      Pandazole + "/TileWater_15.prefab" },
        { "Mud",        Pandazole + "/TileGround_10.prefab" },

        // 불 계열
        { "Lava",       Pandazole + "/HardRock_31.prefab" },
        { "Volcano",    Pandazole + "/HardRock_60.prefab" },
        { "Ash",        Pandazole + "/SoftRock_01.prefab" },
        { "Obsidian",   Pandazole + "/Jem_31.prefab" },

        // 나무 계열
        { "Forest",     Pandazole + "/Tree_01_Spring.prefab" },
        { "Grass",      Pandazole + "/Grass_01.prefab" },
        { "WoodBridge", "Assets/Bridge_02.prefab" },

        // 철·돌 계열
        { "Ore",        Pandazole + "/Jem_01.prefab" },
        { "Steel",      Pandazole + "/MineralNode_03_B.prefab" },
        { "Rust",       Pandazole + "/MineralNode_07_C.prefab" },
        { "Mountain",   Pandazole + "/HardRock_58.prefab" },
        { "Tool",       Pandazole + "/Stick_01.prefab" },
        { "Forge",      "Assets/WoodsLifestyle/Prefabs/Cabins/Cabin_01.prefab" },

        // 바람 계열 — 놓는 순간 도는 연출이 곧 그 원소의 모습이다
        { "Wind",       "Assets/EzTornado/Prefabs/ToonTornadoEfc.prefab" },
        { "tornado",    "Assets/EzTornado/Prefabs/TornadoEfc.prefab" },
    };

    [MenuItem("Tools/Molra/원소 월드 모델 채우기")]
    public static void FillEmpty()
    {
        Apply(false);
    }

    [MenuItem("Tools/Molra/원소 월드 모델 전부 다시 채우기")]
    public static void FillAll()
    {
        if (!EditorUtility.DisplayDialog(
                "원소 월드 모델",
                "이미 넣어 둔 모델까지 표의 값으로 덮어씁니다. 계속할까요?",
                "덮어쓰기", "취소"))
        {
            return;
        }

        Apply(true);
    }

    private static void Apply(bool overwrite)
    {
        StringBuilder report = new StringBuilder();
        int filled = 0;
        int kept = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:ElementData", new[] { ElementFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ElementData element = AssetDatabase.LoadAssetAtPath<ElementData>(path);
            if (element == null)
            {
                continue;
            }

            SerializedObject serialized = new SerializedObject(element);
            SerializedProperty worldModel = serialized.FindProperty("worldModel");

            if (worldModel.objectReferenceValue != null && !overwrite)
            {
                kept++;
                continue;
            }

            if (!ModelByElement.TryGetValue(element.name, out string modelPath))
            {
                report.AppendLine($"— {element.name}: 표에 없어서 건너뜀");
                continue;
            }

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                report.AppendLine($"⚠ {element.name}: 프리팹을 찾지 못함 — {modelPath}");
                continue;
            }

            worldModel.objectReferenceValue = model;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(element);

            report.AppendLine($"✔ {element.name} ← {model.name}");
            filled++;
        }

        AssetDatabase.SaveAssets();

        Debug.Log($"원소 월드 모델 {filled}개 지정, {kept}개는 이미 있어 그대로 둠\n{report}");
    }
}
