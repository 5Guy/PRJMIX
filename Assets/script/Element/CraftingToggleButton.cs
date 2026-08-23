using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

// 조합창(CraftingPanelUI)을 눌러서 올리고, 다시 눌러서 내리는 버튼.
//
// 자리 : 시작 버튼과 같은 가로 가운데(x = 0), 높이는 화면 아래쪽(스테이지 바닥 근처).
// 모양 : 닫혀 있을 때는 위에 'ㅅ', 아래에 "조합창 열기".
//        열리면 위아래가 바뀌어 "조합창 닫기"가 위, 뒤집힌 'ㅅ'이 아래로 간다.
//        이때 창 윗변 바로 위로 올라앉고, 납작해지면서 살짝 투명해진다.
//
// UI는 런타임에 만들기 때문에 씬에는 아무것도 없어도 된다.
// CraftingPanelUI가 Awake에서 직접 하나 만들어 붙인다.
public class CraftingToggleButton : MonoBehaviour
{
    [Header("자리")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [Tooltip("눌리는 영역의 크기. 도형과 글씨보다 넉넉하게 잡는다")]
    [SerializeField] private Vector2 buttonSize = new Vector2(240f, 110f);
    [Tooltip("화면 아래쪽 가장자리에서 띄울 거리. 시작 버튼과 같은 x축(가운데)에 놓인다")]
    [SerializeField] private float bottomMargin = 40f;

    [Header("'ㅅ' 도형")]
    [SerializeField] private float chevronWidth = 60f;      // 'ㅅ'의 가로 폭
    [SerializeField] private float chevronHeight = 26f;     // 'ㅅ'의 높이
    [SerializeField] private float strokeThickness = 10f;   // 획의 두께
    [SerializeField] private Color iconColor = new Color(1f, 1f, 1f, 0.95f);

    [Header("글씨")]
    [SerializeField] private string openLabel = "조합창 열기";
    [SerializeField] private string closeLabel = "조합창 닫기";
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private float labelFontSize = 30f;
    [SerializeField] private float labelHeight = 34f;
    [Tooltip("'ㅅ'과 글씨 사이 간격")]
    [SerializeField] private float rowGap = 10f;

    [Header("열렸을 때")]
    [Tooltip("조합창이 올라오면 이 알파까지 흐려진다")]
    [Range(0f, 1f)]
    [SerializeField] private float openedAlpha = 0.45f;
    [Tooltip("조합창 윗변에서 얼마나 띄워 올라앉을지")]
    [SerializeField] private float openedGap = 12f;
    [Tooltip("올라앉았을 때 세로로 얼마나 납작해질지. 1이면 그대로, 작을수록 납작하다")]
    [Range(0.2f, 1f)]
    [SerializeField] private float openedFlatten = 0.5f;
    [Tooltip("뒤집히고 흐려지는 데 걸리는 시간")]
    [SerializeField] private float flipDuration = 0.25f;

    private CraftingPanelUI panel;
    private Canvas canvas;
    private RectTransform buttonRect;
    private RectTransform chevronRect;
    private RectTransform labelRect;
    private TextMeshProUGUI labelText;
    private CanvasGroup group;

    private float flip;                // 0 = 위를 가리킴(닫힘), 1 = 아래를 가리킴(열림)
    private bool hiddenForPauseMenu;   // 일시정지 메뉴 때문에 숨긴 것이면 true

    // CraftingPanelUI가 부른다. 씬에 이미 버튼이 있으면 만들지 않는다.
    public static CraftingToggleButton Ensure()
    {
        CraftingToggleButton existing = FindFirstObjectByType<CraftingToggleButton>();
        if (existing != null)
        {
            return existing;
        }

        // AddComponent는 그 자리에서 Awake를 부르므로, panel은 Awake 안에서 스스로 찾게 둔다.
        return new GameObject("CraftingToggleButton").AddComponent<CraftingToggleButton>();
    }

    private void Awake()
    {
        // 조합창은 씬에 하나뿐이다. Ensure로 만들어질 때는 이미 살아 있으므로 바로 찾힌다.
        panel = FindFirstObjectByType<CraftingPanelUI>();

        ResolveFont();
        EnsureEventSystem();
        BuildUI();

        flip = CraftingPanelUI.IsOpen ? 1f : 0f;
        ApplyFlip();

        PauseMenuManager.MenuVisibilityChanged += HandlePauseMenuVisibilityChanged;
    }

    private void OnDestroy()
    {
        PauseMenuManager.MenuVisibilityChanged -= HandlePauseMenuVisibilityChanged;
    }

    private void Update()
    {
        if (panel == null)
        {
            panel = FindFirstObjectByType<CraftingPanelUI>();
        }

        UpdateVisibility();
        UpdateFlip();
    }

    // 조합창이 잠겼거나(출발한 뒤) 실패 패널이 떠 있으면 누를 수 없으니 아예 감춘다.
    private void UpdateVisibility()
    {
        if (canvas == null || hiddenForPauseMenu)
        {
            return;
        }

        bool usable = panel != null && !panel.IsLocked && !StageFailPanel.IsOpen;
        if (canvas.gameObject.activeSelf != usable)
        {
            canvas.gameObject.SetActive(usable);
        }
    }

    private void UpdateFlip()
    {
        float target = CraftingPanelUI.IsOpen ? 1f : 0f;

        // 창이 올라오는 중에는 창 높이도 같이 자라므로, 다 뒤집힌 뒤에도 자리는 계속 맞춰 준다.
        if (!Mathf.Approximately(flip, target))
        {
            float step = flipDuration > 0f ? Time.unscaledDeltaTime / flipDuration : 1f;
            flip = Mathf.MoveTowards(flip, target, step);
        }

        ApplyFlip();
    }

    private void ApplyFlip()
    {
        float eased = flip * flip * (3f - 2f * flip);   // smoothstep
        float row = (chevronHeight + labelHeight) * 0.5f + rowGap;

        // 닫힘 : 'ㅅ'이 위, 글씨가 아래. 열림 : 글씨가 위, 뒤집힌 'ㅅ'이 아래.
        chevronRect.anchoredPosition = new Vector2(0f, Mathf.Lerp(row * 0.5f, -row * 0.5f, eased));
        labelRect.anchoredPosition = new Vector2(0f, Mathf.Lerp(-row * 0.5f, row * 0.5f, eased));

        // y축 반전: 1 → -1 로 지나가며 뒤집힌다. 열렸을 때는 납작하게 눌린다.
        // 글씨는 뒤집으면 읽을 수 없으니 도형만 뒤집는다.
        chevronRect.localScale = new Vector3(1f, Mathf.Lerp(1f, -openedFlatten, eased), 1f);
        labelText.text = flip >= 0.5f ? closeLabel : openLabel;
        group.alpha = Mathf.Lerp(1f, openedAlpha, eased);

        // 닫혔을 때는 화면 바닥, 열렸을 때는 조합창 윗변 바로 위.
        // 창 위에서는 두 줄이 겨우 들어갈 만큼만 남겨 납작하게 만든다.
        float openedHeight = chevronHeight * openedFlatten + labelHeight + rowGap * 2f;
        buttonRect.sizeDelta = new Vector2(buttonSize.x, Mathf.Lerp(buttonSize.y, openedHeight, eased));
        buttonRect.anchoredPosition = new Vector2(0f, Mathf.Lerp(bottomMargin, OpenedMargin, eased));
    }

    // 조합창 윗변 높이(화면 픽셀)를 이 캔버스 좌표로 바꾼 값. 창을 못 찾으면 원래 자리에 둔다.
    private float OpenedMargin
    {
        get
        {
            if (panel == null || canvas == null || canvas.scaleFactor <= 0f)
            {
                return bottomMargin;
            }

            return panel.OpenTopScreenY / canvas.scaleFactor + openedGap;
        }
    }

    private void Toggle()
    {
        if (panel == null || panel.IsLocked || StageFailPanel.IsOpen)
        {
            return;
        }

        panel.SetOpen(!CraftingPanelUI.IsOpen);
    }

    // Esc로 일시정지 메뉴가 열리면 버튼을 숨기고, 완전히 닫히면(숨긴 게 이 메뉴 때문이었을 때만) 되살린다.
    private void HandlePauseMenuVisibilityChanged(bool isMenuOpen)
    {
        if (canvas == null)
        {
            return;
        }

        if (isMenuOpen)
        {
            hiddenForPauseMenu = canvas.gameObject.activeSelf;
            canvas.gameObject.SetActive(false);
        }
        else if (hiddenForPauseMenu)
        {
            hiddenForPauseMenu = false;
            canvas.gameObject.SetActive(true);
        }
    }

    // ───────────────────────────── UI 생성 ─────────────────────────────

    private void BuildUI()
    {
        GameObject canvasObject = new GameObject("CraftingToggleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // 조합창(100)보다 위에 둔다. 창이 올라와도 버튼은 그 위에 남아 다시 누를 수 있어야 한다.
        canvas.sortingOrder = 110;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject buttonObject = new GameObject("CraftingToggle", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CanvasGroup));
        RectTransform rect = (RectTransform)buttonObject.transform;
        buttonRect = rect;
        rect.SetParent(canvasObject.transform, false);

        // 시작 버튼과 같은 가운데(x = 0), 높이만 화면 아래쪽.
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = buttonSize;
        rect.anchoredPosition = new Vector2(0f, bottomMargin);

        // 눌리는 판은 보이지 않는다. 알파가 0이어도 레이캐스트는 그대로 받는다.
        Image hitArea = buttonObject.GetComponent<Image>();
        hitArea.color = new Color(0f, 0f, 0f, 0f);

        group = buttonObject.GetComponent<CanvasGroup>();

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = hitArea;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(Toggle);

        BuildChevron(rect);
        BuildLabel(rect);
    }

    // 'ㅅ' = 꼭짓점이 위에 있고 다리가 아래로 벌어지는 획 두 개.
    // 이 컨테이너만 통째로 y축 반전시킨다.
    private void BuildChevron(RectTransform parent)
    {
        GameObject chevronObject = new GameObject("Chevron", typeof(RectTransform));
        RectTransform chevron = (RectTransform)chevronObject.transform;
        chevronRect = chevron;
        chevron.SetParent(parent, false);
        chevron.anchorMin = new Vector2(0.5f, 0.5f);
        chevron.anchorMax = new Vector2(0.5f, 0.5f);
        chevron.pivot = new Vector2(0.5f, 0.5f);
        chevron.sizeDelta = new Vector2(chevronWidth, chevronHeight);

        float half = chevronWidth * 0.5f;
        float angle = Mathf.Atan2(chevronHeight, half) * Mathf.Rad2Deg;

        // 꼭짓점에서 획 두 개가 만나 벌어지도록, 길이에 두께 절반을 더해 이음매를 메운다.
        float length = Mathf.Sqrt(half * half + chevronHeight * chevronHeight) + strokeThickness * 0.5f;

        BuildStroke(chevron, "Left", new Vector2(-half * 0.5f, 0f), angle, length);
        BuildStroke(chevron, "Right", new Vector2(half * 0.5f, 0f), -angle, length);
    }

    private void BuildLabel(RectTransform parent)
    {
        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        labelRect = (RectTransform)labelObject.transform;
        labelRect.SetParent(parent, false);
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.sizeDelta = new Vector2(buttonSize.x, labelHeight);

        labelText = labelObject.AddComponent<TextMeshProUGUI>();
        if (font != null)
        {
            labelText.font = font;
        }

        labelText.text = openLabel;
        labelText.fontSize = labelFontSize;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.color = iconColor;
        labelText.raycastTarget = false;   // 누르는 건 뒤쪽 판이 맡는다
    }

    private void BuildStroke(RectTransform parent, string strokeName, Vector2 center, float angle, float length)
    {
        GameObject strokeObject = new GameObject(strokeName, typeof(RectTransform), typeof(Image));
        RectTransform stroke = (RectTransform)strokeObject.transform;
        stroke.SetParent(parent, false);
        stroke.anchorMin = new Vector2(0.5f, 0.5f);
        stroke.anchorMax = new Vector2(0.5f, 0.5f);
        stroke.pivot = new Vector2(0.5f, 0.5f);
        stroke.sizeDelta = new Vector2(length, strokeThickness);
        stroke.anchoredPosition = center;
        stroke.localEulerAngles = new Vector3(0f, 0f, angle);

        Image image = strokeObject.GetComponent<Image>();
        image.color = iconColor;
        image.raycastTarget = false;   // 누르는 건 뒤쪽 판이 맡는다
    }

    private void ResolveFont()
    {
        if (font != null)
        {
            return;
        }

        // 빌드에서는 조합창 프리팹에 저장된 글꼴을 빌려 쓴다. 안 그러면 한글이 깨진다.
        if (panel != null)
        {
            font = panel.LabelFont;
            if (font != null)
            {
                return;
            }
        }

#if UNITY_EDITOR
        foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets/Font" }))
        {
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (font != null)
            {
                return;
            }
        }
#endif
    }

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        eventSystem.AddComponent<InputSystemUIInputModule>();
#else
        eventSystem.AddComponent<StandaloneInputModule>();
#endif
    }
}
