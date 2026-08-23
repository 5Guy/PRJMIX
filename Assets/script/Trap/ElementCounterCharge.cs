using System.Collections.Generic;
using UnityEngine;

// "이 원소는 몇 개를 올려야 함정이 꺼지는가"를 인스펙터에서 적어 두는 한 줄.
// 목록에 적지 않은 원소는 하나만 올리면 꺼진다(기본 1개).
// 예: 진흙 = 2 → 함정 칸에 진흙을 두 번 올려야 덮인다.
[System.Serializable]
public class ElementCounterCharge
{
    [Tooltip("개수를 따로 정할 원소")]
    [SerializeField] private ElementData element;

    [Tooltip("이 원소로 함정을 끄는 데 필요한 개수")]
    [Min(1)]
    [SerializeField] private int required = 2;

    public ElementData Element => element;

    // 1보다 작게 적어 두면 영영 안 꺼지므로 최소 1로 올린다.
    public int Required => Mathf.Max(1, required);

    // 목록에서 이 원소에 정해 둔 개수를 찾는다. 적혀 있지 않으면 1개.
    public static int RequiredFor(List<ElementCounterCharge> charges, ElementData data)
    {
        if (charges == null || data == null)
        {
            return 1;
        }

        foreach (ElementCounterCharge charge in charges)
        {
            if (charge != null && charge.element == data)
            {
                return charge.Required;
            }
        }

        return 1;
    }
}
