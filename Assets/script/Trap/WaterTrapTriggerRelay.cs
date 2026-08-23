using UnityEngine;

// Unity의 Trigger 콜백은 Collider가 붙은 오브젝트 쪽에서 발생하므로,
// 모델 오브젝트의 CapsuleCollider가 받은 접촉을 WaterTrap 관리 스크립트로 넘긴다.
//
// 파훼 규약(IElementCounterTrap)도 그대로 넘겨준다.
// WaterTrap 스크립트는 판정 콜라이더의 부모가 아니라 형제 오브젝트에 붙어 있어서,
// PlacementSystem이 콜라이더에서 GetComponentInParent로 함정을 거슬러 올라가면 찾지 못한다.
// 콜라이더에 붙어 있는 이 중계기가 "이 콜라이더는 저 함정 것"이라고 알려 주는 셈이다.
public class WaterTrapTriggerRelay : MonoBehaviour, IElementCounterTrap
{
    private WaterTrap owner;

    public void Setup(WaterTrap waterTrap)
    {
        owner = waterTrap;
    }

    private void OnTriggerEnter(Collider other)
    {
        owner?.HandleTrigger(other);
    }

    // 빠르게 지나가 OnTriggerEnter를 놓치는 경우를 대비해 머무는 동안에도 확인한다.
    private void OnTriggerStay(Collider other)
    {
        owner?.HandleTrigger(other);
    }

    // ───────── IElementCounterTrap : 전부 주인에게 넘긴다 ─────────

    public bool IsArmed => owner != null && owner.IsArmed;

    public bool IsCountered => owner != null && owner.IsCountered;

    public bool CanBeCounteredBy(ElementData data) => owner != null && owner.CanBeCounteredBy(data);

    public bool TryCounter(ElementData data) => owner != null && owner.TryCounter(data);

    public Transform GetCounterVisual(ElementData data) => owner != null ? owner.GetCounterVisual(data) : null;

    public bool CancelCounter() => owner != null && owner.CancelCounter();

    public void NotifyPlayerApproached() => owner?.NotifyPlayerApproached();

    public event System.Action Countered
    {
        add
        {
            if (owner != null)
            {
                owner.Countered += value;
            }
        }
        remove
        {
            if (owner != null)
            {
                owner.Countered -= value;
            }
        }
    }
}
