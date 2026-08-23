using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class TextHoverFade : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Target")]
    [Tooltip("페이드할 글자. 사용하지 않으면 비워두세요.")]
    [SerializeField] private TMP_Text targetText;

    [Tooltip("페이드할 버튼. 사용하지 않으면 비워두세요.")]
    [SerializeField] private Button targetButton;

    [Header("Alpha")]
    [Range(0f, 1f)]
    [SerializeField] private float normalAlpha = 1f;

    [Range(0f, 1f)]
    [SerializeField] private float hoverAlpha = 0.35f;

    [Header("Fade Speed")]
    [SerializeField] private float fadeSpeed = 10f;

    private float currentAlpha;
    private float targetAlpha;

    private void Awake()
    {
        // Text와 Button이 둘 다 지정되지 않았다면
        // 자식에서 Text를 자동으로 찾음
        if (targetText == null && targetButton == null)
        {
            targetText = GetComponentInChildren<TMP_Text>(true);
        }

        ResetFade();
    }

    private void OnEnable()
    {
        // 다른 화면에서 돌아왔을 때 초기화
        ResetFade();
    }

    private void Update()
    {
        if (Mathf.Approximately(currentAlpha, targetAlpha))
            return;

        currentAlpha = Mathf.MoveTowards(
            currentAlpha,
            targetAlpha,
            fadeSpeed * Time.unscaledDeltaTime
        );

        ApplyAlpha(currentAlpha);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        targetAlpha = hoverAlpha;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        targetAlpha = normalAlpha;
    }

    private void ResetFade()
    {
        currentAlpha = normalAlpha;
        targetAlpha = normalAlpha;

        ApplyAlpha(normalAlpha);
    }

    private void ApplyAlpha(float alpha)
    {
        // =========================
        // Text 페이드
        // =========================
        if (targetText != null)
        {
            Color textColor = targetText.color;
            textColor.a = alpha;
            targetText.color = textColor;
        }

        // =========================
        // Button 페이드
        // =========================
        if (targetButton != null)
        {
            Graphic buttonGraphic = targetButton.targetGraphic;

            if (buttonGraphic != null)
            {
                Color buttonColor = buttonGraphic.color;
                buttonColor.a = alpha;
                buttonGraphic.color = buttonColor;
            }
        }
    }
}