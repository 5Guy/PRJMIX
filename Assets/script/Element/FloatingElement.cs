using UnityEngine;

// 위쪽 원소창(물 속)에 둥둥 떠 있는 원소 한 개의 상태값.
// 실제 물리 계산은 CraftingPanelUI가 한 번에 돌린다.
public class FloatingElement : MonoBehaviour
{
    public ElementData Data;
    public RectTransform Rect;
    public Vector2 Velocity;
    public Vector2 RestPosition;   // 물 위에서 머무르려는 기준 자리
    public bool HasCustomRest;     // 직접 끌어다 놓은 자리면 격자로 되돌리지 않는다
    public float Radius;
    public float Phase;            // 개체마다 다른 출렁임 타이밍
}
