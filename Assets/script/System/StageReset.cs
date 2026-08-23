using UnityEngine;

// 스테이지를 처음 상태로 되돌리는 신호를 한곳에서 뿌린다.
//
// 언제 울리나
//  - 시작 버튼을 누를 때 : 지난 판에서 꺼 놓은 함정을 되살린다.
//    (이번 판에 올려 둔 상쇄 원소는 아직 "예약"만 된 상태라 이 복구에 지워지지 않고,
//     복구가 끝난 뒤 StageStartButton이 StageStarted를 울려서 그때 함정이 꺼진다)
//  - 플레이어가 죽어 리스폰할 때 : 함정을 되살리고 배치해 둔 원소·돌 벽도 전부 회수한다.
public static class StageReset
{
    public enum Reason
    {
        StageStart,   // 시작 버튼
        PlayerDeath,  // 사망 후 리스폰
    }

    // 함정(ElementTrapCube), 배치 시스템(PlacementSystem) 등이 구독한다.
    public static event System.Action<Reason> Requested;

    // 정적 이벤트는 플레이 세션을 넘어 남으므로 매 실행마다 비운다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Requested = null;
    }

    public static void Request(Reason reason)
    {
        Requested?.Invoke(reason);
    }
}
