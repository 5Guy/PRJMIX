using UnityEditor;
using UnityEngine;

// 함정을 맵에 "정교하게" 놓기 위한 공용 계산.
//
// 눈대중으로 끌어다 놓으면 두 가지가 자주 어긋난다.
//   - 높이 : 프리팹마다 뿌리와 실제 모델의 높이 차가 달라서(물 함정은 모델이 2m 위에 있다)
//            뿌리를 바닥에 맞추면 물이 공중에 뜬다. 눈에 보이는 크기(Renderer)를 재서 맞춰야 한다.
//   - 칸   : 파훼 원소는 PlacementGrid의 칸 단위로 놓인다. 함정이 칸 경계에 걸쳐 있으면
//            어느 칸에 원소를 놓아야 할지 애매해진다. 칸 한가운데에 세워야 한다.
//
// 이 두 가지를 여기서 한 번만 풀어 두고, 배치 창(TrapPlacementWindow)과
// Stage_01 자동 배치(Stage01TrapSetup)가 같은 계산을 나눠 쓴다.
public static class TrapPlacement
{
    public const string TrapFolder = "Assets/Prefab/Trab";

    // ───────────────────────────── 높이 ─────────────────────────────

    // 눈에 보이는 크기. 파티클은 프리뷰 상태에 따라 경계가 들쭉날쭉해서 빼고 잰다.
    public static bool TryMeasureVisualBounds(GameObject target, out Bounds bounds)
    {
        bounds = default;
        bool any = false;

        foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || renderer is ParticleSystemRenderer)
            {
                continue;
            }

            if (!any)
            {
                bounds = renderer.bounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return any;
    }

    // ───────────────────────────── 판정 위치 ─────────────────────────────

    // 함정 프리팹은 뿌리와 실제 판정 위치가 크게 어긋나 있다.
    // FireTrab은 판정 큐브가 뿌리에서 7.5m 뒤 / 7.7m 위에 있고, WaterTrap은 물 모델이 1.8m 뒤에 있다.
    // 뿌리를 목표 지점에 두면 정작 죽는 자리는 엉뚱한 곳이 된다. 판정 콜라이더를 기준으로 삼아야 한다.
    public static Collider ResolveAnchorCollider(GameObject target)
    {
        // 모래바람은 상쇄 칸이 공중(2.6m 위)에 떠 있으므로, 바닥에 맞출 것은 휩쓸림 판정 쪽이다.
        SandstormKillZone stormZone = target.GetComponentInChildren<SandstormKillZone>(true);
        if (stormZone != null)
        {
            return stormZone.GetComponent<Collider>();
        }

        WaterTrap water = target.GetComponentInChildren<WaterTrap>(true);
        if (water != null)
        {
            SerializedProperty property = new SerializedObject(water).FindProperty("trapCollider");
            if (property.objectReferenceValue is Collider assigned)
            {
                return assigned;
            }

            return target.GetComponentInChildren<CapsuleCollider>(true);
        }

        ElementTrapCube cube = target.GetComponentInChildren<ElementTrapCube>(true);
        if (cube != null)
        {
            return cube.GetComponent<Collider>();
        }

        // 자동차는 차체 콜라이더를 실행 중에 만들기 때문에 에디터에서는 잡을 것이 없다.
        // 뿌리와 차체가 거의 같은 자리라서 뿌리 기준으로 두어도 어긋나지 않는다.
        return null;
    }

    // 판정 콜라이더가 목표 지점의 x/z 위에 오도록 뿌리를 옮긴다. 높이는 건드리지 않는다.
    public static void AlignHorizontally(Transform root, Vector3 target)
    {
        // Collider.bounds는 물리 쪽이 들고 있는 값이라, 방금 트랜스폼을 옮겼어도 바로 따라오지 않는다.
        // 동기화하지 않고 읽으면 "옮기기 전" 위치로 어긋난 정도를 계산해서
        // 함정이 목표한 칸이 아니라 엉뚱한 칸에 가서 선다. (실제로 1m 가까이 어긋났다)
        Physics.SyncTransforms();

        Collider anchor = ResolveAnchorCollider(root.gameObject);
        Vector3 offset = anchor != null
            ? anchor.bounds.center - root.position
            : Vector3.zero;

        root.position = new Vector3(target.x - offset.x, root.position.y, target.z - offset.z);

        // 다음에 bounds를 읽는 쪽(높이 맞춤·검사)도 방금 옮긴 위치를 보게 한다.
        Physics.SyncTransforms();
    }

    // 오브젝트의 바닥이 groundY + lift 에 닿도록 내린다.
    //
    // 기준은 눈에 보이는 크기(Renderer)다. 연출이 전부 파티클이라 잴 것이 없으면
    // (FireTrab, StormTrab이 그렇다) 판정 콜라이더의 바닥을 대신 쓴다.
    // 둘 다 없을 때만 뿌리를 그 높이에 둔다.
    public static void SnapToGround(Transform target, float groundY, float lift = 0f)
    {
        // 판정 콜라이더로 바닥을 잴 수도 있으므로 물리 쪽 위치를 먼저 맞춰 둔다.
        Physics.SyncTransforms();

        Vector3 position = target.position;

        if (TryMeasureVisualBounds(target.gameObject, out Bounds bounds))
        {
            position.y += (groundY + lift) - bounds.min.y;
        }
        else if (ResolveAnchorCollider(target.gameObject) is Collider anchor)
        {
            position.y += (groundY + lift) - anchor.bounds.min.y;
        }
        else
        {
            position.y = groundY + lift;
        }

        target.position = position;
    }

    // 그 지점의 바닥 높이. 콜라이더가 있으면 위에서 아래로 쏘아 재고, 없으면 fallback을 쓴다.
    //
    // 맨 위에 맞은 것을 그냥 쓰면 안 된다. Stage_01은 건물이 빽빽한 도시라
    // 위에서 쏜 광선이 지붕에 먼저 맞고, 함정이 건물 옥상에 올라앉아 버린다.
    // 플레이어가 서 있는 높이(fallbackY)에 가장 가까운 면을 도로로 본다.
    public static float SampleGroundY(Vector3 worldPoint, float fallbackY)
    {
        Vector3 origin = new Vector3(worldPoint.x, fallbackY + 200f, worldPoint.z);
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 400f, ~0, QueryTriggerInteraction.Ignore);

        float best = fallbackY;
        float bestGap = float.MaxValue;

        foreach (RaycastHit hit in hits)
        {
            float gap = Mathf.Abs(hit.point.y - fallbackY);
            if (gap < bestGap)
            {
                bestGap = gap;
                best = hit.point.y;
            }
        }

        // 어느 면도 발밑 근처가 아니면(전부 지붕이거나 지하) 차라리 fallback을 믿는다.
        return bestGap <= 2f ? best : fallbackY;
    }

    // ───────────────────────────── 칸 ─────────────────────────────

    // MapPlacementArea가 실행 중에 까는 격자와 똑같은 것을 에디터에서 미리 계산한다.
    // 계산은 MapPlacementArea.TryBuildLayout 한 곳뿐이라 실행 중과 어긋날 수 없다.
    // (예전에는 같은 식을 양쪽에 적어 두어서 한쪽만 고치면 배치가 반 칸씩 어긋났다)
    public static bool TryResolveGrid(out Vector3 origin, out float cellSize, out int columns, out int rows)
    {
        origin = Vector3.zero;
        cellSize = 1f;
        columns = 0;
        rows = 0;

        MapPlacementArea area = Object.FindFirstObjectByType<MapPlacementArea>();
        if (area == null)
        {
            return false;
        }

        // 실행 중에 격자를 까는 것과 완전히 같은 코드를 그대로 부른다.
        if (!area.TryBuildLayout(out PlacementGridLayout.Result layout, out string failure))
        {
            Debug.LogWarning($"[격자] {area.name}: {failure}", area);
            return false;
        }

        origin = layout.Origin;
        cellSize = layout.CellSize;
        columns = layout.Columns;
        rows = layout.Rows;
        return true;
    }

    // 월드 좌표를 가장 가까운 칸 한가운데로 옮긴다. 높이는 건드리지 않는다.
    public static Vector3 SnapToCellCenter(Vector3 world, Vector3 gridOrigin, float cellSize)
    {
        int x = Mathf.FloorToInt((world.x - gridOrigin.x) / cellSize);
        int z = Mathf.FloorToInt((world.z - gridOrigin.z) / cellSize);

        return new Vector3(
            gridOrigin.x + (x + 0.5f) * cellSize,
            world.y,
            gridOrigin.z + (z + 0.5f) * cellSize);
    }

    // 격자를 못 찾았을 때 쓰는 단순 격자 맞춤.
    public static Vector3 SnapToStep(Vector3 world, float step)
    {
        if (step <= 0f)
        {
            return world;
        }

        return new Vector3(
            Mathf.Round(world.x / step) * step,
            world.y,
            Mathf.Round(world.z / step) * step);
    }

    // ───────────────────────────── 진행 방향 ─────────────────────────────

    // 플레이어가 걸어가는 방향(수평). 플레이어를 못 찾으면 false.
    public static bool TryGetPathDirection(out Vector3 direction, out Transform player)
    {
        player = null;
        direction = Vector3.forward;

        player = PlayerLocator.FindPlayer();
        if (player == null)
        {
            return false;
        }

        // 플레이어는 자기 forward로 자동으로 걸어간다(PlayerAutoWalker).
        Vector3 forward = Vector3.ProjectOnPlane(player.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        direction = forward.normalized;
        return true;
    }
}
