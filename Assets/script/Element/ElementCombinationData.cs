using UnityEngine;

[CreateAssetMenu(
    fileName = "NewElementCombination",
    menuName = "Game/Element/Element Combination"
)]
public class ElementCombinationData : ScriptableObject
{
    [Header("재료 원소")]
    [SerializeField] private ElementData firstElement;
    [SerializeField] private ElementData secondElement;

    [Header("결과 원소")]
    [SerializeField] private ElementData resultElement;

    public ElementData FirstElement => firstElement;
    public ElementData SecondElement => secondElement;
    public ElementData ResultElement => resultElement;

    public bool IsMatch(ElementData elementA, ElementData elementB)
    {
        return
            (firstElement == elementA && secondElement == elementB) ||
            (firstElement == elementB && secondElement == elementA);
    }
}
