using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 머리 위에 뜨는 3D 화살표 표시(TargetArrowMarker)를 만들어 주는 에디터 도구.
//
//  Tools > Molra > 화살표 표시 프리팹 만들기            : Assets/Prefab/ArrowMarker.prefab을 만든다.
//                                                        만들고 나면 하이어라키의 아무 오브젝트 위에
//                                                        끌어다 놓기만 하면 붙는다 (가장 손쉬운 방법)
//  Tools > Molra > 플레이어·함정 프리팹에 화살표 붙이기 : Player / FireTrab / FireTrap에 한 번에 붙인다
//  GameObject > Molra > 화살표 표시 붙이기              : 씬에서 고른 오브젝트에 붙인다
//  GameObject > Molra > 화살표 표시 떼어내기            : 붙여 둔 화살표를 지운다
//
// 화살표 모양은 원뿔 + 자루로 직접 만들어 Assets/Mesh에 저장하고, 머티리얼은 Assets/Material에
// 하나만 만들어 모든 화살표가 같이 쓴다. 크기·높이·색은 TargetArrowMarker가 붙는 순간
// 대상을 보고 알아서 잡으므로 이 도구가 따로 맞춰 줄 것은 없다.
public static class ArrowMarkerCreator
{
    private const string ArrowObjectName = "ArrowMarker";

    private const string MaterialFolder = "Assets/Material";
    private const string MeshFolder = "Assets/Mesh";
    private const string PrefabFolder = "Assets/Prefab";
    private const string MaterialPath = MaterialFolder + "/ArrowMarker.mat";
    private const string MeshPath = MeshFolder + "/ArrowMarker_Down.asset";
    private const string PrefabPath = PrefabFolder + "/ArrowMarker.prefab";

    // "한 번에 붙이기"가 손봐 줄 프리팹들.
    private static readonly string[] DefaultTargets =
    {
        "Assets/Prefab/Player.prefab",
        "Assets/Prefab/FireTrab.prefab",
        "Assets/Prefab/FireTrap.prefab",
    };

    // 화살표 모양 치수. 끝(뾰족한 쪽)이 원점이고 몸통이 +Y로 뻗는다. 전체 높이는 1.
    private const int Segments = 12;
    private const float HeadRadius = 0.26f;
    private const float HeadHeight = 0.42f;
    private const float ShaftRadius = 0.09f;
    private const float ShaftHeight = 0.58f;

    [MenuItem("Tools/Molra/화살표 표시 프리팹 만들기", false, 10)]
    private static void CreateArrowPrefab()
    {
        GameObject temp = BuildArrowObject();

        EnsureFolder(PrefabFolder);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);
        Object.DestroyImmediate(temp);

        AssetDatabase.SaveAssets();
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);

        Debug.Log($"'{PrefabPath}'를 만들었습니다. 하이어라키에서 표시하고 싶은 오브젝트 위에 " +
                  "끌어다 놓으면 높이·크기·색이 알아서 맞춰집니다.", prefab);
    }

    [MenuItem("Tools/Molra/플레이어·함정 프리팹에 화살표 붙이기", false, 11)]
    private static void AttachToDefaultPrefabs()
    {
        List<string> done = new List<string>();

        foreach (string path in DefaultTargets)
        {
            if (AttachToPrefabAsset(path))
            {
                done.Add(path);
            }
        }

        if (done.Count == 0)
        {
            Debug.LogWarning("화살표를 붙일 프리팹을 하나도 찾지 못했습니다. 경로를 확인해 주세요.");
            return;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"프리팹 {done.Count}개에 화살표 표시를 붙였습니다:\n - {string.Join("\n - ", done)}");
    }

    [MenuItem("GameObject/Molra/화살표 표시 붙이기", false, 12)]
    private static void AttachToSelection(MenuCommand command)
    {
        GameObject target = command.context as GameObject ?? Selection.activeGameObject;
        if (target == null)
        {
            return;
        }

        if (PrefabUtility.IsPartOfPrefabAsset(target))
        {
            string path = AssetDatabase.GetAssetPath(target);
            if (AttachToPrefabAsset(path))
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"'{path}'에 화살표 표시를 붙였습니다.");
            }

            return;
        }

        GameObject arrow = Attach(target.transform, true);
        Selection.activeGameObject = arrow;
        Debug.Log($"'{target.name}' 위에 화살표 표시를 붙였습니다.", arrow);
    }

    [MenuItem("GameObject/Molra/화살표 표시 붙이기", true)]
    private static bool AttachToSelectionValidate()
    {
        return Selection.activeGameObject != null;
    }

    [MenuItem("GameObject/Molra/화살표 표시 떼어내기", false, 13)]
    private static void RemoveFromSelection(MenuCommand command)
    {
        GameObject target = command.context as GameObject ?? Selection.activeGameObject;
        if (target == null)
        {
            return;
        }

        if (PrefabUtility.IsPartOfPrefabAsset(target))
        {
            string path = AssetDatabase.GetAssetPath(target);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                RemoveExisting(root.transform, false);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"'{path}'의 화살표 표시를 떼어냈습니다.");
            return;
        }

        RemoveExisting(target.transform, true);
        Debug.Log($"'{target.name}'의 화살표 표시를 떼어냈습니다.", target);
    }

    [MenuItem("GameObject/Molra/화살표 표시 떼어내기", true)]
    private static bool RemoveFromSelectionValidate()
    {
        return Selection.activeGameObject != null;
    }

    // 프리팹 파일은 직접 열 수 없으므로 사본을 열어 고친 뒤 다시 저장한다.
    private static bool AttachToPrefabAsset(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
        {
            Debug.LogWarning($"'{path}' 프리팹을 찾지 못해 건너뜁니다.");
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        if (root == null)
        {
            Debug.LogWarning($"'{path}' 프리팹을 열지 못해 건너뜁니다.");
            return false;
        }

        try
        {
            // 사본 씬 안이라 Undo를 걸 수 없다(걸어도 되돌릴 대상이 사라진다).
            Attach(root.transform, false);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return true;
    }

    private static GameObject Attach(Transform host, bool registerUndo)
    {
        // 두 번 붙여서 화살표가 겹치지 않도록 이미 있던 것은 지우고 새로 만든다.
        RemoveExisting(host, registerUndo);

        GameObject arrow = BuildArrowObject();

        if (registerUndo)
        {
            Undo.RegisterCreatedObjectUndo(arrow, "화살표 표시 붙이기");
        }

        arrow.transform.SetParent(host, false);
        arrow.layer = host.gameObject.layer;

        // 부모가 정해진 뒤에 다시 재야 정수리 높이와 크기가 제대로 잡힌다.
        arrow.GetComponent<TargetArrowMarker>().Refresh();

        return arrow;
    }

    // 화살표 오브젝트 한 개. 프리팹으로 저장할 때도, 오브젝트에 바로 붙일 때도 이걸 쓴다.
    private static GameObject BuildArrowObject()
    {
        GameObject arrow = new GameObject(ArrowObjectName);
        arrow.AddComponent<MeshFilter>().sharedMesh = GetOrCreateMesh();

        MeshRenderer renderer = arrow.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = GetOrCreateMaterial();
        // 머리 위 표시가 바닥에 그림자를 떨구면 지저분하다. 빛 계산도 필요 없다.
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        arrow.AddComponent<TargetArrowMarker>();
        return arrow;
    }

    private static void RemoveExisting(Transform host, bool registerUndo)
    {
        // 이름이 바뀌었을 수도 있으므로 컴포넌트로도 찾는다. 자식 한 단계만 본다.
        for (int i = host.childCount - 1; i >= 0; i--)
        {
            Transform child = host.GetChild(i);
            if (child.name != ArrowObjectName && child.GetComponent<TargetArrowMarker>() == null)
            {
                continue;
            }

            if (registerUndo)
            {
                Undo.DestroyObjectImmediate(child.gameObject);
            }
            else
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    private static Material GetOrCreateMaterial()
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (existing != null)
        {
            return existing;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material material = new Material(shader);

        // 색은 화살표마다 다르므로 머티리얼에는 흰색만 넣어 두고
        // TargetArrowMarker가 MaterialPropertyBlock으로 덮어쓴다.
        // 자체발광은 키워드를 켜 두어야 Block으로 넣은 _EmissionColor가 먹는다.
        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

        EnsureFolder(MaterialFolder);
        AssetDatabase.CreateAsset(material, MaterialPath);
        return material;
    }

    private static Mesh GetOrCreateMesh()
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (existing != null)
        {
            return existing;
        }

        Mesh mesh = BuildArrowMesh();
        EnsureFolder(MeshFolder);
        AssetDatabase.CreateAsset(mesh, MeshPath);
        return mesh;
    }

    // 아래를 가리키는 화살표. 뾰족한 끝이 원점이고 자루가 +Y로 뻗는다.
    // 면마다 꼭짓점을 따로 두고 법선을 다시 계산해서, 게임의 로우폴리 느낌에 맞게 각지게 보인다.
    private static Mesh BuildArrowMesh()
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        void AddTriangle(Vector3 a, Vector3 b, Vector3 c)
        {
            triangles.Add(vertices.Count);
            triangles.Add(vertices.Count + 1);
            triangles.Add(vertices.Count + 2);
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
        }

        Vector3 tip = Vector3.zero;
        float headTop = HeadHeight;
        float shaftTop = HeadHeight + ShaftHeight;
        Vector3 headCenter = new Vector3(0f, headTop, 0f);
        Vector3 shaftCenter = new Vector3(0f, shaftTop, 0f);

        for (int i = 0; i < Segments; i++)
        {
            float a0 = (float)i / Segments * Mathf.PI * 2f;
            float a1 = (float)(i + 1) / Segments * Mathf.PI * 2f;

            Vector3 head0 = new Vector3(Mathf.Cos(a0) * HeadRadius, headTop, Mathf.Sin(a0) * HeadRadius);
            Vector3 head1 = new Vector3(Mathf.Cos(a1) * HeadRadius, headTop, Mathf.Sin(a1) * HeadRadius);
            Vector3 low0 = new Vector3(Mathf.Cos(a0) * ShaftRadius, headTop, Mathf.Sin(a0) * ShaftRadius);
            Vector3 low1 = new Vector3(Mathf.Cos(a1) * ShaftRadius, headTop, Mathf.Sin(a1) * ShaftRadius);
            Vector3 high0 = new Vector3(Mathf.Cos(a0) * ShaftRadius, shaftTop, Mathf.Sin(a0) * ShaftRadius);
            Vector3 high1 = new Vector3(Mathf.Cos(a1) * ShaftRadius, shaftTop, Mathf.Sin(a1) * ShaftRadius);

            AddTriangle(tip, head0, head1);            // 원뿔 옆면
            AddTriangle(headCenter, head1, head0);     // 원뿔 밑면 (위를 본다)
            AddTriangle(low0, high0, high1);           // 자루 옆면
            AddTriangle(low0, high1, low1);
            AddTriangle(shaftCenter, high1, high0);    // 자루 윗뚜껑
        }

        Mesh mesh = new Mesh { name = "ArrowMarker_Down" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        int split = folder.LastIndexOf('/');
        AssetDatabase.CreateFolder(folder.Substring(0, split), folder.Substring(split + 1));
    }
}
