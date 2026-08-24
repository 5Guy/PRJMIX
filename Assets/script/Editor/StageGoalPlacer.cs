using UnityEditor;
using UnityEngine;

// 도착 깃발(StageGoalFlag)을 배치 격자의 칸에 맞춰 세우는 도구.
//
// 깃발이 어디에 있느냐가 곧 동선 길이이고, 동선 길이가 함정 배치 간격을 정한다.
// 그래서 깃발도 함정과 같은 칸 위에 서 있어야 "몇 칸 걸어가서 무엇을 만난다"가 딱 떨어진다.
//
// 옮기는 것은 x/z와 높이뿐이다. 회전과 다음 씬 설정 등은 건드리지 않는다.
public static class StageGoalPlacer
{
    // 플레이어 앞 몇 칸에 깃발을 세울지의 기본값.
    //
    // Stage_01 기준으로 함정은 3, 5, 7, 9칸에 선다(최소 2칸 간격).
    // 깃발을 8칸에 두면 마지막 물 함정(7칸)은 깃발 앞, 달려오는 자동차(9칸)는 깃발 너머가 된다.
    public const int DefaultCellsAhead = 8;

    [MenuItem("Tools/Molra/깃발을 칸 한가운데로 맞추기 (자리는 그대로)")]
    public static void SnapGoalToCell()
    {
        Place(null);
    }

    // 배치 모드용: 플레이어 앞 DefaultCellsAhead 칸으로 옮긴다.
    public static void MoveGoalAheadDefault()
    {
        Place(DefaultCellsAhead);
    }

    /// <summary>
    /// 깃발을 칸 한가운데에 세운다.
    /// </summary>
    /// <param name="cellsAhead">
    /// 플레이어 앞 몇 칸에 세울지. null이면 자리는 그대로 두고 가장 가까운 칸 한가운데로만 맞춘다.
    /// </param>
    /// <param name="snapToGround">높이도 바닥에 맞출지. false면 x/z만 옮기고 높이는 그대로 둔다.</param>
    public static bool Place(int? cellsAhead, bool snapToGround = true)
    {
        StageGoalFlag goal = Object.FindFirstObjectByType<StageGoalFlag>();
        if (goal == null)
        {
            Debug.LogWarning("[깃발] 도착 깃발(StageGoalFlag)을 찾지 못했습니다.");
            return false;
        }

        if (!TrapPlacement.TryGetPathDirection(out Vector3 direction, out Transform player))
        {
            Debug.LogWarning("[깃발] 플레이어를 찾지 못해 진행 방향을 알 수 없습니다.");
            return false;
        }

        if (!TrapPlacement.TryResolveGrid(out Vector3 origin, out float cellSize, out int _, out int _))
        {
            Debug.LogWarning("[깃발] 배치 격자를 계산하지 못했습니다.");
            return false;
        }

        // 몇 칸 앞으로 옮길 때는 플레이어가 서 있는 칸에서 진행 방향으로 그만큼 나아간 지점을 쓴다.
        // 옆으로는 플레이어와 같은 줄(= 도로 한가운데 칸)에 세운다.
        Vector3 target = cellsAhead.HasValue
            ? player.position + direction * (cellsAhead.Value * cellSize)
            : goal.transform.position;

        if (cellsAhead.HasValue)
        {
            // 진행 방향에 수직인 쪽은 플레이어의 줄을 그대로 따른다.
            Vector3 across = Vector3.Cross(Vector3.up, direction).normalized;
            float drift = Vector3.Dot(target - player.position, across);
            target -= across * drift;
        }

        Vector3 cellCenter = TrapPlacement.SnapToCellCenter(target, origin, cellSize);

        Undo.RecordObject(goal.transform, "Place Goal Flag");

        // 깃발은 함정과 달리 뿌리와 판정 위치가 거의 같아서 뿌리를 그대로 칸 한가운데에 둔다.
        goal.transform.position = new Vector3(cellCenter.x, goal.transform.position.y, cellCenter.z);

        // 이미 세워 둔 스테이지의 깃발 높이까지 건드리면 연출이 어긋날 수 있어서, 높이는 선택으로 둔다.
        if (snapToGround)
        {
            TrapPlacement.SnapToGround(goal.transform, TrapPlacement.SampleGroundY(cellCenter, player.position.y));
        }

        EditorUtility.SetDirty(goal.transform);

        float distance = Vector3.Dot(goal.transform.position - player.position, direction);
        Debug.Log(
            $"[깃발] '{goal.name}'을 {goal.transform.position}에 세웠습니다. " +
            $"플레이어에서 {distance:0.00}m (= {distance / cellSize:0.#}칸) 앞입니다.",
            goal);

        return true;
    }
}
