using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// StageScene 5_dst 에 손으로 세워 둔 함정 요소들을 Assets/Prefab/Trab 밑으로 프리팹화하는 에디터 도구.
// Tools > Molra > 5_dst 함정 프리팹으로 뽑기 로 실행한다.
//
// 뽑아내는 것
//   FireTrab            : 불 함정 연출 + ElementTrapCube(물로 끄는 판정)
//   StormTrab           : 모래바람 연출 + Sandstorm + SandstormKillZone
//   SandstormCounterPad : 모래바람을 끄는 상쇄 칸
//   Pickup              : 돌진하는 차(ChargingCar) + CarTrigger(CarTriggerZone)
//   StormTrabSet        : StormTrab + SandstormCounterPad 를 한 덩어리로 묶은 것.
//                         둘은 서로를 가리키는데 씬에서는 남남인 루트라 따로 뽑으면 그 연결이 끊긴다.
//                         이 묶음 프리팹은 연결까지 살려 두었으니 맵에 놓을 때는 이쪽을 쓰면 된다.
//
// 씬에 있던 원본은 프리팹 인스턴스로 이어 붙이고 씬을 저장한다.
public static class TrapPrefabExtractor
{
    private const string ScenePath = "Assets/Scenes/StageScene 5_dst.unity";
    private const string TargetFolder = "Assets/Prefab/Trab";

    // 씬 루트 이름 -> 프리팹 이름. 이름을 바꿔 저장하고 싶으면 여기만 고치면 된다.
    private static readonly Dictionary<string, string> Targets = new Dictionary<string, string>
    {
        { "FireTrab", "FireTrab" },
        { "StormTrab", "StormTrab" },
        { "SandstormCounterPad", "SandstormCounterPad" },
        { "Pickup", "Pickup" },
    };

    [MenuItem("Tools/Molra/5_dst 함정 프리팹으로 뽑기")]
    public static void Extract()
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

        EnsureFolder(TargetFolder);

        var saved = new Dictionary<string, GameObject>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (!Targets.TryGetValue(root.name, out string prefabName) || saved.ContainsKey(root.name))
            {
                continue;
            }

            // 이미 있으면 GUID 를 지키면서 덮어쓴다(다시 뽑아도 참조가 안 끊긴다).
            string path = $"{TargetFolder}/{prefabName}.prefab";
            GameObject asset = PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.AutomatedAction);
            if (asset == null)
            {
                Debug.LogError($"[TrapPrefabExtractor] {root.name} 프리팹 저장 실패");
                continue;
            }

            saved.Add(root.name, asset);
            Debug.Log($"[TrapPrefabExtractor] {root.name} -> {path}");
        }

        foreach (string name in Targets.Keys)
        {
            if (!saved.ContainsKey(name))
            {
                Debug.LogWarning($"[TrapPrefabExtractor] 씬 루트에서 {name} 을(를) 찾지 못했다");
            }
        }

        if (saved.TryGetValue("StormTrab", out GameObject stormAsset) &&
            saved.TryGetValue("SandstormCounterPad", out GameObject padAsset))
        {
            BuildStormSet(scene, stormAsset, padAsset);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // 모래바람과 상쇄 칸을 한 루트 밑에 묶고, 서로를 가리키던 참조를 프리팹 안에서 다시 이어 준다.
    private static void BuildStormSet(Scene scene, GameObject stormAsset, GameObject padAsset)
    {
        GameObject sceneStorm = FindRoot(scene, "StormTrab");
        GameObject scenePad = FindRoot(scene, "SandstormCounterPad");
        if (sceneStorm == null || scenePad == null)
        {
            return;
        }

        var root = new GameObject("StormTrabSet");
        try
        {
            root.transform.position = sceneStorm.transform.position;
            root.transform.rotation = sceneStorm.transform.rotation;

            var storm = (GameObject)PrefabUtility.InstantiatePrefab(stormAsset);
            var pad = (GameObject)PrefabUtility.InstantiatePrefab(padAsset);

            // 씬에 놓여 있던 월드 배치를 그대로 옮겨 담는다.
            storm.transform.SetParent(root.transform, false);
            storm.transform.SetPositionAndRotation(sceneStorm.transform.position, sceneStorm.transform.rotation);
            storm.transform.localScale = sceneStorm.transform.localScale;

            pad.transform.SetParent(root.transform, false);
            pad.transform.SetPositionAndRotation(scenePad.transform.position, scenePad.transform.rotation);
            pad.transform.localScale = scenePad.transform.localScale;

            LinkStormAndPad(storm, pad);

            string path = $"{TargetFolder}/StormTrabSet.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log($"[TrapPrefabExtractor] StormTrabSet -> {path}");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // Sandstorm.counterTrap 과 ElementTrapCube.effectRoot 는 둘 다 private 직렬화 필드라 SerializedObject 로 넣는다.
    private static void LinkStormAndPad(GameObject storm, GameObject pad)
    {
        var sandstorm = storm.GetComponent<Sandstorm>();
        var trapCube = pad.GetComponent<ElementTrapCube>();
        if (sandstorm == null || trapCube == null)
        {
            Debug.LogWarning("[TrapPrefabExtractor] StormTrabSet 참조 연결을 건너뛴다(컴포넌트 없음)");
            return;
        }

        SetReference(sandstorm, "counterTrap", trapCube);
        SetReference(trapCube, "effectRoot", storm.transform);
    }

    private static void SetReference(Object target, string propertyName, Object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop == null)
        {
            Debug.LogWarning($"[TrapPrefabExtractor] {target.GetType().Name}.{propertyName} 필드를 찾지 못했다");
            return;
        }

        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name)
            {
                return root;
            }
        }

        return null;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
