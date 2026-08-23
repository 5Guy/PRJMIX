using UnityEditor;
using UnityEngine;

// 돌진하는 황소 한 벌을 한 번에 세워 주는 에디터 도구.
// GameObject > Molra > 황소 장애물 만들기 로 실행한다.
//
// 만들어지는 구조
//   Bull                 : 빈 오브젝트. Rigidbody + ChargingBull + 몸통 BoxCollider
//    ├ Model             : bullsOneShot 모델 + Animator
//    ├ BullTriggerZone   : 빈 상자(Empty Cube) 트리거. 플레이어가 들어오면 돌진 시작
//    └ BullKillZone      : 몸통 앞쪽 받힘 판정. 플레이어를 날리고 시스템 다운 화면을 띄운다
//
// 모델을 자식으로 내리는 것이 중요하다. 모델과 본체가 같은 오브젝트에 있으면
// 애니메이션 클립이 트랜스폼을 건드릴 때 돌진 방향과 판정 상자까지 같이 끌려다닌다.
//
// 위치는 플레이어의 진행 방향을 기준으로 대충 잡아 두므로, 만든 뒤 손으로 옮겨 맞추면 된다.
// 이미 씬에 놓아 둔 황소가 있으면 "선택한 오브젝트를 황소로 만들기"를 쓰면 된다.
public static class BullCreator
{
    private const string BullModelPath = "Assets/Prefab/bullsOneShot.fbx";

    private const float BullAhead = 22f;        // 플레이어 앞 몇 미터에 황소를 세울지
    private const float TriggerLength = 14f;    // 감지 범위가 황소 앞으로 뻗는 거리
    private const float TriggerWidth = 4f;
    private const float TriggerHeight = 3f;
    private const float KillZoneScale = 0.9f;   // 몸통 대비 받힘 판정 크기

    [MenuItem("GameObject/Molra/황소 장애물 만들기", false, 14)]
    private static void CreateBull()
    {
        if (!TryGetPlayerAxis(out Vector3 playerPosition, out Vector3 forward))
        {
            Debug.LogWarning("씬에서 PlayerAutoWalker를 찾지 못했습니다. 황소를 원점에 만들었으니 직접 옮겨 주세요.");
            playerPosition = Vector3.zero;
            forward = Vector3.forward;
        }

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(BullModelPath);
        if (model == null)
        {
            Debug.LogError($"{BullModelPath} 를 찾지 못했습니다.");
            return;
        }

        // 본체는 빈 오브젝트로 두고 모델은 자식으로 넣는다.
        GameObject bull = new GameObject("Bull");
        Undo.RegisterCreatedObjectUndo(bull, "황소 만들기");
        bull.transform.position = GroundPoint(playerPosition + forward * BullAhead, playerPosition.y);

        // 오는 플레이어를 마주 보게 세운다. 돌진은 이 방향(-forward)으로 나아간다.
        bull.transform.rotation = Quaternion.LookRotation(-forward, Vector3.up);

        GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model, bull.transform);
        visual.name = "Model";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;

        Setup(bull);

        Selection.activeGameObject = bull;
        SceneView.lastActiveSceneView?.FrameSelected();

        Debug.Log(
            $"{bull.name}: 황소 장애물을 만들었습니다.\n" +
            "  - BullTriggerZone 범위에 플레이어가 들어오면 돌진합니다.\n" +
            "  - 부딪히는 Rigidbody는 전부 날아가고, 플레이어는 날아간 뒤 시스템 다운 화면과 함께 리스폰합니다.\n" +
            "  - 돌 벽(CarBlocker)으로 막을 수 있습니다. 흑요석이 생기면 ChargingBull.IsBlocker()만 고치면 됩니다.\n" +
            "  - 감지 범위와 속도는 인스펙터에서 맞춰 주세요.",
            bull);
    }

    // 이미 씬에 놓아 둔 황소 모델에 필요한 것만 붙인다. 서 있는 자리는 그대로 둔다.
    // 고른 것이 모델 그 자체이면(Animator가 붙어 있으면) 빈 부모를 씌워 모델을 자식으로 내린다.
    [MenuItem("GameObject/Molra/선택한 오브젝트를 황소로 만들기", false, 15)]
    private static void AttachToSelection()
    {
        GameObject target = Selection.activeGameObject;
        if (target == null)
        {
            Debug.LogWarning("황소로 만들 오브젝트를 하이어라키에서 골라 주세요.");
            return;
        }

        GameObject root = EnsureModelIsChild(target);

        Undo.RegisterFullObjectHierarchyUndo(root, "황소로 만들기");
        Setup(root);

        EditorUtility.SetDirty(root);
        Selection.activeGameObject = root;
        Debug.Log($"{root.name}: 황소로 만들었습니다. 씬을 저장해 주세요.", root);
    }

    // 모델과 본체가 같은 오브젝트에 있으면 애니메이션이 돌진 방향과 판정 상자를 흔든다.
    // 같은 자리·같은 회전의 빈 부모를 만들어 그쪽을 본체로 삼고, 모델은 자식으로 내린다.
    private static GameObject EnsureModelIsChild(GameObject target)
    {
        if (target.GetComponent<Animator>() == null)
        {
            return target;
        }

        GameObject root = new GameObject(target.name);
        Undo.RegisterCreatedObjectUndo(root, "황소 본체 만들기");

        root.transform.SetParent(target.transform.parent, false);
        root.transform.SetPositionAndRotation(target.transform.position, target.transform.rotation);
        root.transform.localScale = Vector3.one;

        // 모델의 크기는 그대로 지키면서 자리만 옮긴다.
        Undo.SetTransformParent(target.transform, root.transform, "모델을 자식으로 내리기");
        target.name = "Model";

        // 지난번에 모델에 직접 붙여 둔 것들은 본체로 옮겨야 한다.
        // 모델에 남겨 두면 애니메이션이 판정 상자를 끌고 다닌다.
        MoveZonesToRoot<BullTriggerZone>(target.transform, root.transform);
        MoveZonesToRoot<BullKillZone>(target.transform, root.transform);

        RemoveComponent<ChargingBull>(target);
        RemoveComponent<Rigidbody>(target);

        // 지난번에 모델에 붙인 몸통 콜라이더도 걷어낸다. 본체 쪽에 새로 만든다.
        BoxCollider stale = target.GetComponent<BoxCollider>();
        if (stale != null && !stale.isTrigger)
        {
            Undo.DestroyObjectImmediate(stale);
        }

        Debug.Log($"{root.name}: 모델을 자식('{target.name}')으로 내리고 빈 부모를 본체로 삼았습니다.", root);
        return root;
    }

    private static void MoveZonesToRoot<T>(Transform model, Transform root) where T : Component
    {
        foreach (T zone in model.GetComponentsInChildren<T>(true))
        {
            Undo.SetTransformParent(zone.transform, root, "판정 상자를 본체로 옮기기");
        }
    }

    private static void RemoveComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component != null)
        {
            Undo.DestroyObjectImmediate(component);
        }
    }

    private static void Setup(GameObject bull)
    {
        Rigidbody body = bull.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = Undo.AddComponent<Rigidbody>(bull);
        }

        body.mass = 400f;                 // 무언가를 들이받아도 밀리지 않게 무겁게 둔다
        body.useGravity = true;
        body.constraints |= RigidbodyConstraints.FreezeRotation;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (bull.GetComponent<ChargingBull>() == null)
        {
            Undo.AddComponent<ChargingBull>(bull);
        }

        Bounds local = LocalBounds(bull.transform);

        BoxCollider bodyBox = EnsureBodyCollider(bull, local);

        if (bull.GetComponentInChildren<BullTriggerZone>(true) == null)
        {
            BuildTriggerZone(bull.transform, local);
        }

        if (bull.GetComponentInChildren<BullKillZone>(true) == null)
        {
            BuildKillZone(bull.transform, bodyBox);
        }
    }

    // 실행 중에는 ChargingBull이 알아서 만들지만, 씬에서 크기를 눈으로 보고 맞출 수 있게 미리 만든다.
    private static BoxCollider EnsureBodyCollider(GameObject bull, Bounds local)
    {
        foreach (Collider existing in bull.GetComponentsInChildren<Collider>())
        {
            if (!existing.isTrigger)
            {
                return existing as BoxCollider;
            }
        }

        BoxCollider box = Undo.AddComponent<BoxCollider>(bull);
        box.center = local.center;
        box.size = local.size;
        return box;
    }

    // 플레이어를 감지하는 빈 상자. 황소의 자식이라 황소를 옮기면 같이 따라온다.
    private static void BuildTriggerZone(Transform parent, Bounds local)
    {
        GameObject zoneObject = new GameObject("BullTriggerZone");
        Undo.RegisterCreatedObjectUndo(zoneObject, "황소 감지 범위 만들기");
        zoneObject.transform.SetParent(parent, false);

        // 아래 크기는 미터 단위다. 황소 모델에 걸린 스케일에 휩쓸리지 않게 되돌린다.
        zoneObject.transform.localScale = InverseScaleOf(parent);

        BoxCollider box = zoneObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(TriggerWidth, TriggerHeight, TriggerLength);

        // 몸통 앞쪽으로 뻗는다. 황소가 바라보는 쪽에서 플레이어가 걸어온다.
        float front = Mathf.Abs(local.size.z) * 0.5f * parent.lossyScale.z;
        box.center = new Vector3(0f, TriggerHeight * 0.5f, front + TriggerLength * 0.5f);

        zoneObject.AddComponent<BullTriggerZone>();
    }

    // 받힘 판정. 몸통을 감싸는 크기로 만든다.
    private static void BuildKillZone(Transform parent, BoxCollider bodyBox)
    {
        GameObject zoneObject = new GameObject("BullKillZone");
        Undo.RegisterCreatedObjectUndo(zoneObject, "황소 받힘 판정 만들기");
        zoneObject.transform.SetParent(parent, false);

        BoxCollider box = zoneObject.AddComponent<BoxCollider>();
        box.isTrigger = true;

        if (bodyBox != null)
        {
            box.center = bodyBox.center;
            box.size = bodyBox.size * KillZoneScale;
        }
        else
        {
            box.size = Vector3.one;
        }

        zoneObject.AddComponent<BullKillZone>();
    }

    // 모델 Renderer들의 크기를 자기 좌표계 기준으로 잰다. 회전이 있어도 박스가 몸통에 맞는다.
    private static Bounds LocalBounds(Transform root)
    {
        Bounds local = new Bounds(Vector3.zero, Vector3.zero);
        bool started = false;

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
        {
            Bounds world = renderer.bounds;
            Vector3 center = root.InverseTransformPoint(world.center);
            Vector3 extents = root.InverseTransformVector(world.extents);
            Bounds piece = new Bounds(center, new Vector3(
                Mathf.Abs(extents.x) * 2f,
                Mathf.Abs(extents.y) * 2f,
                Mathf.Abs(extents.z) * 2f));

            if (!started)
            {
                local = piece;
                started = true;
            }
            else
            {
                local.Encapsulate(piece);
            }
        }

        if (!started)
        {
            Debug.LogWarning($"{root.name}: Renderer가 없어 크기를 재지 못했습니다. 1m 상자로 대신합니다.", root);
            local = new Bounds(Vector3.up * 0.5f, Vector3.one);
        }

        return local;
    }

    private static Vector3 InverseScaleOf(Transform target)
    {
        Vector3 lossy = target.lossyScale;
        return new Vector3(
            Mathf.Approximately(lossy.x, 0f) ? 1f : 1f / lossy.x,
            Mathf.Approximately(lossy.y, 0f) ? 1f : 1f / lossy.y,
            Mathf.Approximately(lossy.z, 0f) ? 1f : 1f / lossy.z);
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
