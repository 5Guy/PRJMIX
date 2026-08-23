using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 모래바람 장애물 한 벌(연출 + 휩쓸림 판정 + 물 상쇄 칸)을 한 번에 세워 주는 에디터 도구.
// GameObject > Molra > 모래바람 장애물 만들기 로 실행한다.
//
// 만들어지는 것
//   Sandstorm            : EzTornado 연출 + Sandstorm + 휩쓸림 판정(SandstormKillZone)
//   SandstormCounterPad  : 모래바람과 같은 칸 위에 뜬 상쇄 칸. 물 계열 원소를 올리면 모래바람이 꺼진다
//
// 모래바람은 불 함정처럼 처음부터 맵에 서 있다. 트리거로 뒤늦게 나타나지 않는다.
// 위치는 플레이어의 진행 방향을 기준으로 대충 잡아 두므로, 만든 뒤 손으로 옮겨 맞추면 된다.
public static class SandstormCreator
{
    private const string TornadoPrefabPath = "Assets/EzTornado/Prefabs/TornadoWithWindEfc.prefab";
    private const string WindLoopPath = "Assets/EzTornado/Audio/windloop.wav";

    private const float StormAhead = 20f;      // 플레이어 앞 몇 미터에 모래바람을 세울지
    private const float PadHeight = 2.6f;      // 상쇄 칸이 도로 위에 떠 있는 높이(플레이어가 밑으로 지나간다)
    private const float VisualScale = 0.03f;   // Sandstorm의 기본 연출 크기와 같은 값

    [MenuItem("GameObject/Molra/모래바람 장애물 만들기", false, 12)]
    private static void CreateSandstorm()
    {
        if (!TryGetPlayerAxis(out Vector3 playerPosition, out Vector3 forward))
        {
            Debug.LogWarning("씬에서 PlayerAutoWalker를 찾지 못했습니다. 모래바람을 원점에 만들었으니 직접 옮겨 주세요.");
            playerPosition = Vector3.zero;
            forward = Vector3.forward;
        }

        // 도로 격자가 있으면 칸 한가운데에 세운다. 상쇄 칸도 같은 칸에 얹어야 물을 올릴 수 있다.
        Vector3 ground = SnapToGridCell(GroundPoint(playerPosition + forward * StormAhead, playerPosition.y));

        GameObject storm = BuildStorm(ground, forward);
        Sandstorm sandstorm = storm.GetComponent<Sandstorm>();

        ElementTrapCube counterPad = BuildCounterPad(ground, storm.transform);

        // 상쇄 칸을 물로 껐을 때 모래바람이 몰아치지 않도록 연결해 둔다.
        SerializedObject stormObject = new SerializedObject(sandstorm);
        stormObject.FindProperty("counterTrap").objectReferenceValue = counterPad;
        stormObject.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeGameObject = storm;
        SceneView.lastActiveSceneView?.FrameSelected();

        Debug.Log(
            $"{storm.name}: 모래바람 장애물을 만들었습니다.\n" +
            "  - 처음부터 맵에 서 있고, 닿으면 시스템 다운과 함께 사망합니다.\n" +
            "  - 같은 칸(SandstormCounterPad)에 물을 올려 두고 시작 버튼을 누르면 모래가 가라앉아 꺼집니다.\n" +
            "  - 몰려오는 모래바람으로 만들려면 Sandstorm의 이동 속도를 올리고 SandstormTriggerZone을 붙이면 됩니다.",
            storm);
    }

    // 이미 씬에 세워 둔 빈 오브젝트(예: FireTrab 맞은편에 만든 StormTrab)를 모래바람으로 채운다.
    // 위치·회전·스케일은 손대지 않고, 그 자리에 필요한 것만 붙인다.
    [MenuItem("GameObject/Molra/선택한 오브젝트에 모래바람 연결하기", false, 13)]
    private static void AttachToSelection()
    {
        GameObject target = Selection.activeGameObject;

        // 아무것도 안 골랐으면 이름으로 찾아 준다. StormTrab 하나만 있으면 바로 걸린다.
        if (target == null)
        {
            target = GameObject.Find("StormTrab");
        }

        if (target == null)
        {
            Debug.LogWarning("모래바람으로 만들 오브젝트를 하이어라키에서 골라 주세요. (StormTrab 이름이면 자동으로 찾습니다)");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(target, "모래바람 연결하기");

        Sandstorm sandstorm = target.GetComponent<Sandstorm>();
        if (sandstorm == null)
        {
            sandstorm = Undo.AddComponent<Sandstorm>(target);
        }

        // 이미 붙여 둔 연출이 있으면 그것을 쓰고, 없으면 EzTornado를 새로 붙인다.
        // 어느 쪽이든 visualRoot로 물려 줘야 실행할 때 크기를 맞출 수 있다.
        Transform visual = FindVisualRoot(target.transform);
        if (visual != null)
        {
            NormalizeVisual(visual, target.transform);
        }
        else
        {
            visual = AttachTornadoVisual(target.transform);
        }

        if (target.GetComponentInChildren<SandstormKillZone>(true) == null)
        {
            AttachKillZone(target.transform);
        }

        // 상쇄 칸은 모래바람과 같은 칸에, 형제로 만든다.
        // 자식으로 넣으면 이 오브젝트에 걸린 스케일(StormTrab은 0.4배)까지 같이 먹어 칸이 어긋난다.
        ElementTrapCube counterPad = FindExistingPad(target.transform.parent);

        if (counterPad == null)
        {
            Vector3 ground = SnapToGridCell(target.transform.position);
            counterPad = BuildCounterPad(ground, target.transform);
            counterPad.transform.SetParent(target.transform.parent, true);
        }

        SerializedObject serialized = new SerializedObject(sandstorm);
        serialized.FindProperty("counterTrap").objectReferenceValue = counterPad;

        if (visual != null)
        {
            serialized.FindProperty("visualRoot").objectReferenceValue = visual;
        }
        else
        {
            // 연출이 이 오브젝트 자신에게 붙어 있으면 판정·표식까지 같이 줄어들어서 따로 떼어낼 수 없다.
            Debug.LogWarning(
                $"{target.name}: 연출을 담은 자식을 찾지 못해 visualRoot를 비워 뒀습니다. " +
                "소용돌이 파티클은 자식 오브젝트에 넣어 주세요.",
                target);
        }

        AudioClip wind = AssetDatabase.LoadAssetAtPath<AudioClip>(WindLoopPath);
        if (wind != null)
        {
            serialized.FindProperty("windLoop").objectReferenceValue = wind;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(target);
        Selection.activeGameObject = target;

        Debug.Log(
            $"{target.name}: 모래바람을 연결했습니다.\n" +
            "  - 소용돌이 연출 + 휩쓸림 판정(SandstormKillZone)이 붙었습니다.\n" +
            $"  - 같은 칸의 {counterPad.name} 에 물을 올리고 시작 버튼을 누르면 모래가 가라앉습니다.\n" +
            "  - 씬을 저장해 주세요.",
            target);
    }

    // 같은 칸에 이미 만들어 둔 상쇄 칸이 있으면 다시 만들지 않는다.
    private static ElementTrapCube FindExistingPad(Transform parent)
    {
        foreach (ElementTrapCube trap in Object.FindObjectsByType<ElementTrapCube>(FindObjectsSortMode.None))
        {
            if (trap.name == "SandstormCounterPad" && trap.transform.parent == parent)
            {
                return trap;
            }
        }

        return null;
    }

    private static GameObject BuildStorm(Vector3 position, Vector3 forward)
    {
        GameObject storm = new GameObject("Sandstorm");
        Undo.RegisterCreatedObjectUndo(storm, "모래바람 만들기");
        storm.transform.position = position;

        // 오는 플레이어를 마주 보게 세운다. 이동 속도를 주면 이 방향으로 밀려온다.
        storm.transform.rotation = Quaternion.LookRotation(-forward, Vector3.up);

        Transform visual = AttachTornadoVisual(storm.transform);

        Sandstorm sandstorm = storm.AddComponent<Sandstorm>();

        SerializedObject serialized = new SerializedObject(sandstorm);

        // 어느 오브젝트를 줄일지 미리 물려 둔다. 실행 중에 찾아 헤매지 않아도 된다.
        if (visual != null)
        {
            serialized.FindProperty("visualRoot").objectReferenceValue = visual;
        }

        AudioClip wind = AssetDatabase.LoadAssetAtPath<AudioClip>(WindLoopPath);
        if (wind != null)
        {
            serialized.FindProperty("windLoop").objectReferenceValue = wind;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();

        AttachKillZone(storm.transform);

        return storm;
    }

    // 휩쓸림 판정은 Sandstorm이 실행 중에도 자동으로 만들지만,
    // 씬에서 크기를 눈으로 보고 조절할 수 있도록 미리 만들어 둔다.
    private static void AttachKillZone(Transform parent)
    {
        GameObject zoneObject = new GameObject("SandstormKillZone");
        Undo.RegisterCreatedObjectUndo(zoneObject, "휩쓸림 판정 만들기");
        zoneObject.transform.SetParent(parent, false);

        // 아래 반지름·높이는 미터 단위다. 부모가 줄여져 있어도(StormTrab은 0.4배) 그대로 지킨다.
        zoneObject.transform.localScale = InverseScaleOf(parent);

        CapsuleCollider capsule = zoneObject.AddComponent<CapsuleCollider>();
        capsule.isTrigger = true;
        capsule.direction = 1;
        capsule.radius = 0.6f;
        capsule.height = 2f;
        capsule.center = new Vector3(0f, 1f, 0f);

        zoneObject.AddComponent<SandstormKillZone>();
    }

    private static Vector3 InverseScaleOf(Transform target)
    {
        Vector3 lossy = target.lossyScale;
        return new Vector3(
            Mathf.Approximately(lossy.x, 0f) ? 1f : 1f / lossy.x,
            Mathf.Approximately(lossy.y, 0f) ? 1f : 1f / lossy.y,
            Mathf.Approximately(lossy.z, 0f) ? 1f : 1f / lossy.z);
    }

    // EzTornado 프리팹을 연출로 붙인다. 프리팹이 없으면 판정만 있는 채로 두고 경고한다.
    private static Transform AttachTornadoVisual(Transform parent)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TornadoPrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"{TornadoPrefabPath} 를 찾지 못했습니다. 모래바람 연출은 직접 넣어 주세요.");
            return null;
        }

        GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;

        NormalizeVisual(visual.transform, parent);
        return visual.transform;
    }

    // 이미 씬에 넣어 둔 소용돌이 연출에서, 모래바람 바로 아래 자식(= 크기를 조절할 뿌리)을 찾는다.
    // 연출이 모래바람 오브젝트 자신에게 붙어 있으면 따로 줄일 수 없으므로 null을 준다.
    private static Transform FindVisualRoot(Transform target)
    {
        ParticleSystem particle = target.GetComponentInChildren<ParticleSystem>(true);
        if (particle == null)
        {
            return null;
        }

        Transform root = particle.transform;
        while (root.parent != null && root.parent != target)
        {
            root = root.parent;
        }

        return root == target ? null : root;
    }

    private static void NormalizeVisual(Transform visual, Transform parent)
    {
        // 이 프리팹의 파티클은 Scaling Mode가 Local이라 부모 스케일을 무시한다.
        // Hierarchy로 바꿔야 아래에서 준 Scale이 입자 크기까지 먹는다.
        // (실행 중에도 Sandstorm이 다시 맞추지만, 씬 뷰에서 바로 보이도록 여기서도 해 준다)
        foreach (ParticleSystem particle in visual.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = particle.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }

        // VisualScale은 프리팹 원본 대비 최종 크기다. 부모가 줄여져 있어도 결과가 같도록 되돌린다.
        visual.localScale = Vector3.Scale(Vector3.one * VisualScale, InverseScaleOf(parent));

        // 연출에 딸린 콜라이더가 있으면 플레이어가 걸려 멈추므로 판정은 KillZone에만 맡긴다.
        foreach (Collider collider in visual.GetComponentsInChildren<Collider>())
        {
            collider.isTrigger = true;
        }
    }

    // 물을 올려 모래바람을 끄는 칸. 모래바람이 서 있는 칸 위에 얹는다.
    // 배치 시스템(PlacementSystem)이 이미 ElementTrapCube를 상쇄 대상으로 다루므로 그대로 쓴다.
    // 도로에서 살짝 띄워 두는 건, 물을 올리지 않았을 때 판정이 겹쳐 헷갈리지 않게 하려는 것이다.
    private static ElementTrapCube BuildCounterPad(Vector3 ground, Transform stormRoot)
    {
        GameObject pad = new GameObject("SandstormCounterPad");
        Undo.RegisterCreatedObjectUndo(pad, "모래바람 상쇄 칸 만들기");
        pad.transform.position = ground + Vector3.up * PadHeight;

        BoxCollider box = pad.AddComponent<BoxCollider>();
        box.isTrigger = true;

        // 격자는 실행할 때 만들어지므로(MapPlacementArea) 에디터에서는 칸에 딱 맞출 수 없다.
        // 칸(1m)보다 작게 잡아서, 자리가 조금 어긋나도 옆 칸까지 물리지 않게 한다.
        box.size = Vector3.one * 0.6f;

        // 어느 칸인지는 모래바람 자신이 알려 준다.
        // (탑뷰에서는 소용돌이가 숨고 그 자리에 납작한 모래빛 표식이 남는다)
        ElementTrapCube trap = pad.AddComponent<ElementTrapCube>();

        SerializedObject serialized = new SerializedObject(trap);

        // 상쇄되면 모래바람 연출을 통째로 끈다. 되살리는 것도 ElementTrapCube가 맡는다.
        serialized.FindProperty("effectRoot").objectReferenceValue = stormRoot;
        serialized.FindProperty("deactivateEffect").boolValue = true;
        serialized.FindProperty("waitForStageStart").boolValue = true;

        SerializedProperty counters = serialized.FindProperty("counterElements");
        counters.ClearArray();

        List<ElementData> waterElements = FindElementsOfType(ElementType.Water, ElementType.Tsunami);
        for (int i = 0; i < waterElements.Count; i++)
        {
            counters.InsertArrayElementAtIndex(i);
            counters.GetArrayElementAtIndex(i).objectReferenceValue = waterElements[i];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();

        if (waterElements.Count == 0)
        {
            Debug.LogWarning($"{pad.name}: 물 계열 ElementData를 찾지 못했습니다. 인스펙터의 '상쇄' 목록을 직접 채워 주세요.", pad);
        }

        return trap;
    }

    // 도로 격자가 있으면 칸 한가운데로 맞춘다. 없으면 그대로 둔다.
    private static Vector3 SnapToGridCell(Vector3 position)
    {
        PlacementGrid grid = Object.FindFirstObjectByType<PlacementGrid>();
        if (grid == null)
        {
            return position;
        }

        Vector2Int cell = grid.WorldToCell(position);
        if (!grid.Contains(cell))
        {
            return position;
        }

        Vector3 snapped = grid.CellToWorld(cell);
        return new Vector3(snapped.x, position.y, snapped.z);
    }

    private static List<ElementData> FindElementsOfType(params ElementType[] types)
    {
        List<ElementData> found = new List<ElementData>();

        foreach (string guid in AssetDatabase.FindAssets("t:ElementData"))
        {
            ElementData data = AssetDatabase.LoadAssetAtPath<ElementData>(AssetDatabase.GUIDToAssetPath(guid));
            if (data == null)
            {
                continue;
            }

            foreach (ElementType type in types)
            {
                if (data.ElementType == type && !found.Contains(data))
                {
                    found.Add(data);
                }
            }
        }

        return found;
    }

    private static bool TryGetPlayerAxis(out Vector3 position, out Vector3 forward)
    {
        PlayerAutoWalker walker = Object.FindFirstObjectByType<PlayerAutoWalker>();
        if (walker == null)
        {
            position = Vector3.zero;
            forward = Vector3.forward;
            return false;
        }

        position = walker.transform.position;
        forward = SnapToCardinal(walker.transform.forward);
        return true;
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

    // 바닥 높이는 위에서 아래로 쏜 레이로 맞춘다. 못 맞히면 넘겨받은 높이를 쓴다.
    private static Vector3 GroundPoint(Vector3 position, float fallbackY)
    {
        if (Physics.Raycast(position + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 200f))
        {
            return new Vector3(position.x, hit.point.y, position.z);
        }

        return new Vector3(position.x, fallbackY, position.z);
    }
}
