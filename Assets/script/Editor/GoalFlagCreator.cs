using UnityEditor;
using UnityEngine;

// 도착 깃발(StageGoalFlag)을 맵 끝에 세워 주는 에디터 도구.
// GameObject > Molra > 도착 깃발 만들기 로 실행한다.
// 깃발 위치는 "플레이어가 바라보는 방향으로 가장 멀리 있는 지형"을 기준으로 잡고,
// 정확한 자리는 만들어진 뒤 손으로 옮기면 된다.
public static class GoalFlagCreator
{
    private const string MaterialFolder = "Assets/Material";
    private const float FlagHeight = 3f;
    private const float EndInset = 2f;   // 맵 끝에서 안쪽으로 살짝 들여놓는 거리

    [MenuItem("GameObject/Molra/도착 깃발 만들기", false, 10)]
    private static void CreateGoalFlag(MenuCommand command)
    {
        GameObject flag = new GameObject("GoalFlag");
        Undo.RegisterCreatedObjectUndo(flag, "도착 깃발 만들기");
        GameObjectUtility.SetParentAndAlign(flag, command.context as GameObject);

        BuildVisual(flag.transform);

        // 플레이어가 지나가면서 닿는 판정 영역. 도로 폭에 맞춰 손으로 조절하면 된다.
        BoxCollider trigger = flag.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(6f, 4f, 1f);
        trigger.center = new Vector3(0f, 2f, 0f);

        flag.AddComponent<StageGoalFlag>();

        PlaceAtMapEnd(flag.transform);

        Selection.activeGameObject = flag;
        SceneView.lastActiveSceneView?.FrameSelected();

        Debug.Log($"{flag.name}: 맵 끝에 도착 깃발을 세웠습니다. 인스펙터의 '다음 씬'에 넘어갈 씬을 지정해 주세요.", flag);
    }

    [MenuItem("GameObject/Molra/선택한 오브젝트를 맵 끝으로 옮기기", false, 11)]
    private static void SnapSelectionToMapEnd()
    {
        Transform target = Selection.activeTransform;
        Undo.RecordObject(target, "맵 끝으로 옮기기");
        PlaceAtMapEnd(target);
    }

    [MenuItem("GameObject/Molra/선택한 오브젝트를 맵 끝으로 옮기기", true)]
    private static bool SnapSelectionToMapEndValidate()
    {
        return Selection.activeTransform != null;
    }

    private static void BuildVisual(Transform parent)
    {
        Material poleMaterial = GetOrCreateMaterial("GoalFlag_Pole", new Color(0.82f, 0.82f, 0.86f));
        Material clothMaterial = GetOrCreateMaterial("GoalFlag_Cloth", new Color(0.2f, 0.85f, 0.45f));

        GameObject pole = CreatePart(PrimitiveType.Cylinder, "Pole", parent, poleMaterial);
        pole.transform.localScale = new Vector3(0.08f, FlagHeight * 0.5f, 0.08f);
        pole.transform.localPosition = new Vector3(0f, FlagHeight * 0.5f, 0f);

        GameObject cloth = CreatePart(PrimitiveType.Cube, "Cloth", parent, clothMaterial);
        cloth.transform.localScale = new Vector3(1.2f, 0.7f, 0.04f);
        cloth.transform.localPosition = new Vector3(0.6f, FlagHeight - 0.45f, 0f);

        GameObject baseStone = CreatePart(PrimitiveType.Cylinder, "Base", parent, poleMaterial);
        baseStone.transform.localScale = new Vector3(0.5f, 0.05f, 0.5f);
        baseStone.transform.localPosition = new Vector3(0f, 0.05f, 0f);
    }

    // 깃발 모양은 보이기만 하면 되므로 프리미티브에 딸려 오는 콜라이더는 지운다.
    // (남겨 두면 플레이어가 깃발에 부딪혀 멈춘다)
    private static GameObject CreatePart(PrimitiveType type, string name, Transform parent, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.GetComponent<Renderer>().sharedMaterial = material;

        Collider collider = part.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }

        return part;
    }

    private static Material GetOrCreateMaterial(string assetName, Color color)
    {
        string path = $"{MaterialFolder}/{assetName}.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            return existing;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material material = new Material(shader);
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        material.color = color;

        if (!AssetDatabase.IsValidFolder(MaterialFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Material");
        }

        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    // 플레이어의 진행 방향으로 가장 멀리 있는 지형 앞에 세운다.
    private static void PlaceAtMapEnd(Transform target)
    {
        PlayerAutoWalker walker = Object.FindFirstObjectByType<PlayerAutoWalker>();
        if (walker == null)
        {
            Debug.LogWarning("씬에서 PlayerAutoWalker를 찾지 못해 맵 끝을 계산하지 못했습니다. 깃발을 직접 옮겨 주세요.");
            return;
        }

        Vector3 start = walker.transform.position;
        Vector3 forward = SnapToCardinal(walker.transform.forward);

        float farthest = float.MinValue;
        foreach (MeshRenderer renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            // 플레이어 자신과 지금 옮기는 깃발은 맵의 일부가 아니므로 제외한다.
            if (renderer.transform.IsChildOf(walker.transform) || renderer.transform.IsChildOf(target))
            {
                continue;
            }

            Bounds bounds = renderer.bounds;
            float projected = Vector3.Dot(bounds.center + Vector3.Scale(bounds.extents, forward), forward);
            farthest = Mathf.Max(farthest, projected);
        }

        if (Mathf.Approximately(farthest, float.MinValue))
        {
            Debug.LogWarning("씬에서 지형 렌더러를 찾지 못해 맵 끝을 계산하지 못했습니다. 깃발을 직접 옮겨 주세요.");
            return;
        }

        // 플레이어 위치에서 진행 방향으로만 밀어내고, 좌우 위치는 플레이어 라인을 그대로 쓴다.
        float distance = farthest - Vector3.Dot(start, forward) - EndInset;
        Vector3 position = start + forward * distance;

        // 바닥 높이는 위에서 아래로 쏜 레이로 맞춘다. 못 맞히면 플레이어 높이를 쓴다.
        if (Physics.Raycast(position + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 200f))
        {
            position.y = hit.point.y;
        }
        else
        {
            position.y = start.y;
        }

        target.position = position;
        target.rotation = Quaternion.LookRotation(-forward, Vector3.up);   // 깃발이 오는 플레이어를 마주 보게
    }

    // 맵이 축에 맞춰 놓여 있으므로 진행 방향을 가장 가까운 축으로 정리한다.
    private static Vector3 SnapToCardinal(Vector3 direction)
    {
        Vector3 flat = new Vector3(direction.x, 0f, direction.z);
        if (flat.sqrMagnitude < 0.0001f)
        {
            return Vector3.forward;
        }

        return Mathf.Abs(flat.x) > Mathf.Abs(flat.z)
            ? new Vector3(Mathf.Sign(flat.x), 0f, 0f)
            : new Vector3(0f, 0f, Mathf.Sign(flat.z));
    }
}
