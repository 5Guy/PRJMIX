using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

// 화면 왼쪽 위에 떠 있는 "스테이지 다시하기" 버튼.
// StageStartButton의 시작하기를 누르기 전까지는 보이지 않다가,
// 시작하기를 누르는 순간(StageStarted) 나타나서 현재 스테이지를 재시작할 수 있게 된다.
//
// UI는 런타임에 만들기 때문에 씬에는 이 컴포넌트만 있으면 된다.
public class RestartStageButton : MonoBehaviour
{
    [Header("버튼")]
    [SerializeField] private string label = "다시하기 버튼";
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private float fontSize = 32f;
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [Tooltip("시작하기 버튼과 같은 자리에서 바뀌어 보이도록, 기본값이 StageStartButton과 동일하다")]
    [SerializeField] private Vector2 buttonSize = new Vector2(320f, 96f);
    [Tooltip("화면 위쪽 가장자리에서 떨어뜨릴 거리. 시작하기 버튼과 같은 값을 쓰면 같은 자리에 겹친다")]
    [SerializeField] private float topMargin = 48f;
    [Tooltip("버튼 배경에 쓸 스프라이트. 비워 두면 기본 패널 그림(settingPanel)을 쓴다")]
    [SerializeField] private Sprite buttonSprite;
    [Tooltip("스프라이트를 넣었을 때 곱해지는 색. 원래 그림 그대로 보려면 흰색으로 둔다")]
    [SerializeField] private Color buttonSpriteTint = Color.white;
    [Tooltip("스프라이트를 넣지 않았을 때 쓰는 단색")]
    [SerializeField] private Color buttonColor = new Color(0.5f, 0.16f, 0.16f, 0.95f);
    [Tooltip("스프라이트 원래 비율을 지킨다. 패널 그림은 버튼 크기에 맞춰 늘리는 게 보통이라 꺼 둔다")]
    [SerializeField] private bool preserveButtonAspect = false;

    private Canvas canvas;
    private Button button;
    private bool hiddenForPauseMenu;   // 일시정지 메뉴가 열려서 숨긴 것이면 true, 메뉴가 닫힐 때만 되살린다

    private void Awake()
    {
        ResolveFont();
        ButtonSkin.ResolveBackground(ref buttonSprite);
        EnsureEventSystem();
        BuildUI();

        canvas.gameObject.SetActive(false);

        StageStartButton.StageStarted += HandleStageStarted;
        StageReset.Requested += HandleStageReset;
        PauseMenuManager.MenuVisibilityChanged += HandlePauseMenuVisibilityChanged;
    }

    private void OnDestroy()
    {
        StageStartButton.StageStarted -= HandleStageStarted;
        StageReset.Requested -= HandleStageReset;
        PauseMenuManager.MenuVisibilityChanged -= HandlePauseMenuVisibilityChanged;
    }

    // 시작하기를 누른 순간부터 다시하기 버튼이 보인다.
    private void HandleStageStarted()
    {
        canvas.gameObject.SetActive(true);
    }

    // 죽어서 스테이지가 처음 상태로 돌아가면, 다시 시작하기를 눌러야 하므로 도로 숨긴다.
    private void HandleStageReset(StageReset.Reason reason)
    {
        if (reason != StageReset.Reason.PlayerDeath)
        {
            return;
        }

        canvas.gameObject.SetActive(false);
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

    private void RestartStage()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // ───────────────────────────── UI 생성 ─────────────────────────────

    private void BuildUI()
    {
        GameObject canvasObject = new GameObject("RestartButtonCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject buttonObject = new GameObject("RestartButton", typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform rect = (RectTransform)buttonObject.transform;
        rect.SetParent(canvasObject.transform, false);
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = buttonSize;
        rect.anchoredPosition = new Vector2(0f, -topMargin);

        Image background = buttonObject.GetComponent<Image>();
        if (buttonSprite != null)
        {
            background.sprite = buttonSprite;
            background.color = buttonSpriteTint;
            background.preserveAspect = preserveButtonAspect;

            // 9-슬라이스 여백이 있는 스프라이트면 늘려도 모서리가 뭉개지지 않는다.
            background.type = buttonSprite.border == Vector4.zero ? Image.Type.Simple : Image.Type.Sliced;
        }
        else
        {
            background.color = buttonColor;
        }

        button = buttonObject.GetComponent<Button>();
        button.targetGraphic = background;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        colors.selectedColor = Color.white;
        button.colors = colors;

        button.onClick.AddListener(RestartStage);

        GameObject textObject = new GameObject("Label", typeof(RectTransform));
        RectTransform textRect = (RectTransform)textObject.transform;
        textRect.SetParent(rect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        if (font != null)
        {
            text.font = font;
        }

        text.text = label;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = textColor;
        text.raycastTarget = false;
    }

    private void ResolveFont()
    {
        if (font != null)
        {
            return;
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
