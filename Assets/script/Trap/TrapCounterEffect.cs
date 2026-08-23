using UnityEngine;

// 원소로 함정을 끌 때(상쇄) 보여 주는 연출의 공통 뼈대.
//
// 함정(ElementTrapCube)과 같은 오브젝트나 그 하위/부모에 붙여 두면,
// 상쇄가 일어나는 순간 함정이 "올려 둔 원소와 같은 원소"를 맡은 연출을 하나 골라 재생한다.
// 붙어 있는 연출이 없으면 예전처럼 불이 즉시 꺼진다.
//
// 원소마다 다르게 보여야 하므로 실제 연출은 파생 클래스가 맡는다.
//   - Tsunami : TsunamiCounterEffect  (파도가 밀려와 앞의 장애물을 쓸어 간다)
//   - Cloud   : RainCloudCounterEffect(비구름이 생겨 비로 불을 끈다)
//   - Mud     : MudCounterEffect      (진흙이 불을 덮고 색이 밝아진다)
public abstract class TrapCounterEffect : MonoBehaviour
{
    [Header("대상 원소")]
    [Tooltip("이 연출을 재생할 원소. 함정에 올려 둔 원소가 이것과 같을 때만 돈다")]
    // 파생 클래스가 Reset()에서 자기 원소로 기본값을 맞출 수 있도록 protected로 둔다.
    // (붙이자마자 Water로 되어 있어 연출이 안 걸리는 일이 잦다)
    [SerializeField] protected ElementType element = ElementType.Water;

    [Header("타이밍")]
    [Tooltip("연출이 시작되고 나서 불이 실제로 꺼지기까지 기다리는 시간(초). 0이면 즉시 꺼진다")]
    [SerializeField] protected float extinguishDelay = 0.6f;

    [Header("사운드")]
    [SerializeField] private AudioClip startSound;
    [Range(0f, 1f)]
    [SerializeField] private float startVolume = 1f;

    public ElementType Element => element;

    // 연출을 시작하고, 불이 실제로 꺼지기까지 함정이 기다려야 할 시간(초)을 돌려준다.
    // 쓰나미처럼 파도가 함정까지 오는 데 걸리는 시간이 거리에 따라 달라지는 연출도 있어서,
    // 고정값이 아니라 연출이 직접 계산해 알려 주는 방식으로 둔다.
    //
    // 주의: 함정은 꺼질 때 연출 뿌리를 통째로 비활성화하므로,
    // 시간이 걸리는 부분은 반드시 CounterEffectRunner 위에서 돌려야 중간에 끊기지 않는다.
    public float Play(ElementTrapCube trap)
    {
        SfxPlayer.PlayAt(startSound, transform.position, startVolume);
        return Mathf.Max(0f, OnPlay(trap));
    }

    // 연출을 시작하고 불이 꺼질 때까지의 시간(초)을 돌려준다.
    protected abstract float OnPlay(ElementTrapCube trap);

    // 이 원소를 함정에 올려 두었을 때(아직 발동 전) 보여 줄 모습.
    //
    // 바람처럼 원소 자체가 연출인 경우, 올려 둔 표시도 기본 구가 아니라 그 연출이어야 한다.
    // null을 돌려주면 PlacementSystem이 늘 하던 대로 색깔 구를 놓는다.
    public virtual Transform CreatePlacedVisual(Vector3 trapPosition)
    {
        return null;
    }

    // 함정이 서 있는 바닥 높이. 파도·진흙처럼 바닥에 깔리는 연출의 기준으로 쓴다.
    // 불기둥 오브젝트가 공중에 떠 있을 수 있어 아래로 레이를 쏴서 확인한다.
    protected static Vector3 GroundUnder(Vector3 position, float searchHeight = 20f)
    {
        Vector3 from = position + Vector3.up * searchHeight;

        if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, searchHeight * 2f, ~0, QueryTriggerInteraction.Ignore))
        {
            return hit.point;
        }

        return position;
    }

    // 함정에 붙어 있는 연출 중 이 원소를 맡은 것을 찾는다. 없으면 null.
    public static TrapCounterEffect Find(Component trap, ElementData data)
    {
        if (trap == null || data == null)
        {
            return null;
        }

        // 연출은 함정과 같은 오브젝트에 붙이는 것이 기본이지만,
        // 정리해 두려고 부모(연출 뿌리)나 하위 오브젝트에 몰아 놓아도 찾을 수 있게 한다.
        Transform root = trap.transform.root;

        foreach (TrapCounterEffect effect in root.GetComponentsInChildren<TrapCounterEffect>(true))
        {
            if (effect.element == data.ElementType)
            {
                return effect;
            }
        }

        return null;
    }
}
