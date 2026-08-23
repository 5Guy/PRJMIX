// 함정 등에 의해 사망 처리되는 대상을 표현하는 인터페이스.
// ElementTrapCube가 사망 연출 직후 이 인터페이스를 호출해서,
// 이동 정지 같은 대상 쪽 반응은 대상 스스로 처리하게 한다.
public interface IPlayerKillable
{
    // 이미 사망 처리 중인지. 함정이 사망 연출을 중복 재생하지 않도록 확인한다.
    bool IsDead { get; }

    void OnKilled();
}
