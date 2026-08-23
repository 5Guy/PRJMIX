using UnityEngine;

// 용암에 닿는 판정.
//
// Unity의 트리거 콜백은 콜라이더가 붙은 오브젝트에서 나므로,
// 용암이 퍼지는 만큼 함께 커지는 이 판정 오브젝트가 받아서 본체(LavaSpill)로 넘긴다.
//
// MonoBehaviour는 파일 이름과 클래스 이름이 같아야 AddComponent로 붙일 수 있어서 파일을 따로 둔다.
[RequireComponent(typeof(Collider))]
public class LavaSpillZone : MonoBehaviour
{
    private LavaSpill owner;

    public void Setup(LavaSpill spill)
    {
        owner = spill;
    }

    private void OnTriggerEnter(Collider other)
    {
        owner?.Burn(other);
    }

    // 퍼지는 중에 판정이 플레이어를 삼키면 Enter가 오지 않는다. 머무는 동안에도 확인한다.
    private void OnTriggerStay(Collider other)
    {
        owner?.Burn(other);
    }
}
