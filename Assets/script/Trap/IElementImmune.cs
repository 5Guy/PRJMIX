// 특정 아이템을 사용해 원소 함정에 면역이 된 상태를 표현하는 인터페이스.
// Player 쪽 아이템/상태 시스템이 이 인터페이스를 구현하면, ElementTrapCube가 사망 처리 전에 면역 여부를 확인한다.
public interface IElementImmune
{
    bool IsImmuneTo(ElementType elementType);
}
