using UnityEngine;

// 조합창에서 끌어온 원소를 올려 파훼(상쇄)할 수 있는 함정.
//
// 불 함정(ElementTrapCube)만 파훼되던 것을 물 함정(WaterTrap)까지 넓히면서 뽑아낸 규약이다.
// PlacementSystem / PlacedElement / TrapApproachZone 은 이제 구체 타입이 아니라 이 규약만 보고 움직이므로,
// 새 함정을 만들 때도 이것만 구현하면 배치·회수·리셋이 전부 그대로 따라온다.
//
// 파훼는 두 단계로 나뉜다.
//   1) TryCounter  : 원소를 함정 위에 "올려 둔다"(IsArmed). 아직 함정은 살아 있다.
//   2) 실제 발동    : 플레이어가 함정 앞 트리거를 지나가거나(NotifyPlayerApproached) 시작 버튼을 누를 때
//                    함정이 꺼진다(IsCountered). 이때 Countered 가 울린다.
// 올려 두기만 한 상태에서는 CancelCounter 로 원소를 회수할 수 있다.
public interface IElementCounterTrap
{
    // 원소를 올려 두었지만 아직 발동하지 않은 상태.
    bool IsArmed { get; }

    // 실제로 꺼진 상태.
    bool IsCountered { get; }

    // 이 원소로 함정을 끌 수 있는지. (이미 꺼졌거나 다른 원소가 올라가 있으면 false)
    bool CanBeCounteredBy(ElementData data);

    // 원소를 올리는 데 성공하면 true.
    bool TryCounter(ElementData data);

    // 올려 둔 원소를 함정이 직접 보여 줄 때 쓸 모습.
    //
    // 물 함정에 시멘트를 올리면 씬에 놓아 둔 시멘트 오브젝트가 물 위로 올라오는 식이다.
    // null이면 PlacementSystem이 늘 하던 대로 기본 구를 놓는다.
    // TryCounter 다음에 물어본다.
    Transform GetCounterVisual(ElementData data);

    // 아직 발동하기 전이라면 올려 둔 원소를 물린다. 회수에 성공하면 true.
    bool CancelCounter();

    // 함정 앞 트리거(TrapApproachZone)가 부른다. 올려 둔 원소가 없으면 아무 일도 없다.
    void NotifyPlayerApproached();

    // 실제로 꺼진 순간. 올려 둔 원소 표시를 함께 치우려고 PlacementSystem 이 구독한다.
    event System.Action Countered;
}
