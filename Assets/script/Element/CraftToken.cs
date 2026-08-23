using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// 마우스로 끌 수 있는 원소 아이콘.
// - 왼쪽 목록 / 위쪽 원소창의 아이콘은 "복사본을 만들어" 끌고 나온다(원본은 그대로 남는다).
// - 아래 조합 영역에 놓인 아이콘은 자기 자신이 움직인다.
[RequireComponent(typeof(RectTransform))]
public class CraftToken : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public ElementData Data { get; private set; }
    public RectTransform Rect { get; private set; }

    private CraftingPanelUI owner;
    private bool spawnCopyOnDrag;
    private CraftToken dragTarget;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text label;

    public bool HasViews => icon != null && label != null;

    public void Init(CraftingPanelUI panel, ElementData data, bool spawnCopy)
    {
        owner = panel;
        Data = data;
        spawnCopyOnDrag = spawnCopy;
        Rect = (RectTransform)transform;
    }

    // 아이콘 스프라이트/색과 이름 글자를 채운다.
    public void ApplyVisual(ElementData data)
    {
        Color iconColor = ElementVisual.GetColor(data);
        icon.sprite = data.Icon != null ? data.Icon : ElementVisual.Circle;
        icon.color = data.Icon != null ? Color.white : iconColor;

        label.text = data.ElementName;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (owner == null || Data == null)
        {
            return;
        }

        dragTarget = spawnCopyOnDrag ? owner.SpawnCraftToken(Data, eventData.position) : this;
        owner.OnTokenDragBegin(dragTarget);
        owner.MoveTokenToPointer(dragTarget, eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragTarget == null)
        {
            return;
        }

        owner.MoveTokenToPointer(dragTarget, eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragTarget == null)
        {
            return;
        }

        owner.OnTokenDropped(dragTarget, eventData.position);
        dragTarget = null;
    }
}
