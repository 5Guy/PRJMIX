using UnityEngine;

// 배치 격자(PlacementGrid)를 어디에, 어떤 칸 크기로 깔지 한 곳에서 계산한다.
//
// 실행 중에는 MapPlacementArea가, 에디터에서는 TrapPlacement가 이것을 부른다.
// 두 곳이 각자 계산하면 에디터에서 함정을 칸에 딱 맞춰 놓아도 실행하면 반 칸씩 어긋난다.
//
// 칸 크기를 정하는 방식이 핵심이다.
//   도로 폭 ÷ 도로에 넣을 칸 수(1 또는 3) = 칸 크기
// 그래서 도로 위에는 칸이 정확히 1개 또는 3개만 들어간다. 어디에 놓아야 하는지가 한눈에 보인다.
// 격자 자체는 맵 전체를 덮으므로 도로 밖(인도, 건물 옥상)에도 그대로 놓을 수 있다.
public static class PlacementGridLayout
{
    // 도로 폭에 칸을 몇 개 넣을지.
    public enum RoadCells
    {
        // 도로 폭 = 칸 하나. 도로 위에는 놓을 자리가 한 줄뿐이다.
        One = 1,

        // 도로 폭 = 칸 셋. 왼쪽 / 가운데 / 오른쪽 중에서 고른다.
        Three = 3,
    }

    public struct Settings
    {
        // 격자가 덮을 맵 전체 범위.
        public Bounds GroundBounds;

        // 격자 평면의 높이.
        public float SurfaceY;

        // 맵 가장자리에서 안쪽으로 줄일 여백.
        public float EdgePadding;

        // 도로를 찾지 못했을 때 쓸 칸 크기.
        public float FallbackCellSize;

        // 길이 Z축으로 뻗어 있는지. false면 X축으로 뻗어 있다.
        public bool AlongIsZ;

        // 도로 폭을 쟀는지.
        public bool HasRoad;

        // 진행 방향에 수직으로 잰 도로 폭.
        public float RoadWidth;

        // 도로 한가운데의 "가로지르는 축" 좌표. (길이 Z로 뻗었으면 x값)
        public float RoadCenterAcross;

        // 도로 폭에 넣을 칸 수.
        public RoadCells CellsAcrossRoad;

        // 플레이어를 찾았는지.
        public bool HasPlayer;

        // 플레이어의 "진행 축" 좌표. (길이 Z로 뻗었으면 z값)
        public float PlayerAlong;
    }

    public struct Result
    {
        public Vector3 Origin;
        public float CellSize;
        public int Columns;
        public int Rows;

        // 어떻게 계산했는지 로그로 남기기 위한 설명.
        public string Explanation;
    }

    public static Result Build(Settings settings)
    {
        float size = ResolveCellSize(settings, out string sizeReason);

        Bounds bounds = settings.GroundBounds;
        float padding = Mathf.Max(0f, settings.EdgePadding);

        float minX = bounds.min.x + padding;
        float minZ = bounds.min.z + padding;
        float maxX = bounds.max.x - padding;
        float maxZ = bounds.max.z - padding;

        float originX = minX;
        float originZ = minZ;

        // 도로 한가운데가 칸 한가운데에 오도록 가로지르는 축을 민다.
        // 칸이 1개든 3개든 홀수라서, 가운데 칸이 도로 한가운데에 딱 맞는다.
        if (settings.HasRoad)
        {
            if (settings.AlongIsZ)
            {
                originX = AlignToCellCenter(originX, settings.RoadCenterAcross, size);
            }
            else
            {
                originZ = AlignToCellCenter(originZ, settings.RoadCenterAcross, size);
            }
        }

        // 플레이어가 서 있는 자리도 칸 한가운데가 되게 진행 축을 민다.
        // 플레이어 정면에 놓는 함정이 칸 경계에 걸치지 않는다.
        if (settings.HasPlayer)
        {
            if (settings.AlongIsZ)
            {
                originZ = AlignToCellCenter(originZ, settings.PlayerAlong, size);
            }
            else
            {
                originX = AlignToCellCenter(originX, settings.PlayerAlong, size);
            }
        }

        // 밀면서 격자 시작점이 맵 안쪽으로 들어왔을 수 있다. 한 칸씩 뒤로 물려 맵을 다 덮게 한다.
        originX = PullBackTo(originX, minX, size);
        originZ = PullBackTo(originZ, minZ, size);

        int columns = Mathf.Max(1, Mathf.FloorToInt((maxX - originX) / size));
        int rows = Mathf.Max(1, Mathf.FloorToInt((maxZ - originZ) / size));

        return new Result
        {
            Origin = new Vector3(originX, settings.SurfaceY, originZ),
            CellSize = size,
            Columns = columns,
            Rows = rows,
            Explanation = $"칸 크기 {size:0.###}m ({sizeReason}), {columns}x{rows}칸, 원점 ({originX:0.##}, {settings.SurfaceY:0.##}, {originZ:0.##})",
        };
    }

    private static float ResolveCellSize(Settings settings, out string reason)
    {
        int cells = Mathf.Max(1, (int)settings.CellsAcrossRoad);

        if (settings.HasRoad && settings.RoadWidth > 0.01f)
        {
            reason = $"도로 폭 {settings.RoadWidth:0.##}m ÷ {cells}칸";
            return Mathf.Max(0.05f, settings.RoadWidth / cells);
        }

        reason = "도로를 못 찾아 기본 칸 크기 사용";
        return Mathf.Max(0.05f, settings.FallbackCellSize);
    }

    // target이 칸 한가운데(origin + (n + 0.5) * size)에 오도록 origin을 반 칸 이내로 민다.
    private static float AlignToCellCenter(float origin, float target, float size)
    {
        float steps = (target - origin) / size;
        float wanted = Mathf.Floor(steps) + 0.5f;
        return origin + (steps - wanted) * size;
    }

    // origin이 limit보다 뒤에서 시작하도록 칸 단위로 물린다. (칸 경계는 그대로 유지된다)
    private static float PullBackTo(float origin, float limit, float size)
    {
        if (origin <= limit)
        {
            return origin;
        }

        float steps = Mathf.Ceil((origin - limit) / size);
        return origin - steps * size;
    }

    // 오브젝트가 눈에 보이는 범위. 파티클은 미리보기 상태에 따라 경계가 들쭉날쭉해서 뺀다.
    public static bool TryMeasureRenderers(GameObject target, out Bounds bounds)
    {
        bounds = default;

        if (target == null)
        {
            return false;
        }

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

    // 길이 Z축으로 뻗어 있는지. 플레이어가 바라보는 쪽으로 판단한다.
    public static bool IsAlongZ(Vector3 pathDirection)
    {
        Vector3 flat = Vector3.ProjectOnPlane(pathDirection, Vector3.up);
        return Mathf.Abs(flat.z) >= Mathf.Abs(flat.x);
    }

    // 진행 방향에 수직으로 잰 폭과, 그 축에서의 한가운데 좌표.
    public static void MeasureAcross(Bounds bounds, bool alongIsZ, out float width, out float center)
    {
        width = alongIsZ ? bounds.size.x : bounds.size.z;
        center = alongIsZ ? bounds.center.x : bounds.center.z;
    }
}
