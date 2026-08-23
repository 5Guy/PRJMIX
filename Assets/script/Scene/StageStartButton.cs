using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

// 스테이지가 시작되자마자 플레이어가 걸어 나가지 않도록 세워 두고,
// 화면의 "시작" 버튼을 눌러야 출발하게 한다.
// 조합창을 먼저 열어 원소를 배치할 시간을 벌어 주는 용도다.
//
// UI는 런타임에 만들기 때문에 씬에는 이 컴포넌트만 있으면 된다.
public class StageStartButton : MonoBehaviour
{
    [Header("버튼")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [SerializeField] private Vector2 buttonSize = new Vector2(320f, 96f);
    [Tooltip("화면 위쪽 가장자리에서 떨어뜨릴 거리. 조합창은 아래쪽에 뜨므로 위에 둔다")]
    [SerializeField] private float topMargin = 48f;
    [Tooltip("버튼 배경에 쓸 스프라이트. 비워 두면 기본 패널 그림(settingPanel)을 쓴다")]
    [SerializeField] private Sprite buttonSprite;
    [Tooltip("스프라이트를 넣었을 때 곱해지는 색. 원래 그림 그대로 보려면 흰색으로 둔다")]
    [SerializeField] private Color buttonSpriteTint = Color.white;
    [Tooltip("스프라이트를 넣지 않았을 때 쓰는 단색")]
    [SerializeField] private Color buttonColor = new Color(0.09f, 0.5f, 0.32f, 0.95f);
    [Tooltip("스프라이트 원래 비율을 지킨다. 패널 그림은 버튼 크기에 맞춰 늘리는 게 보통이라 꺼 둔다")]
    [SerializeField] private bool preserveButtonAspect = false;

    // 버튼 위에 무엇을 올릴지 고른다.
    public enum ButtonContent
    {
        Triangle,   // 재생(▶) 삼각형을 직접 그린다
        Sprite,     // iconSprite에 넣은 그림을 쓴다
        Text,       // label에 적은 글자를 쓴다
    }

    [Header("버튼에 올릴 것")]
    [SerializeField] private ButtonContent content = ButtonContent.Text;
    [SerializeField] private Color contentColor = Color.white;
    [Tooltip("버튼 가운데에서 밀어낼 거리")]
    [SerializeField] private Vector2 contentOffset = Vector2.zero;

    [Header("Triangle / Sprite 일 때")]
    [Tooltip("Sprite 모드에서 쓸 그림")]
    [SerializeField] private Sprite iconSprite;
    [SerializeField] private Vector2 iconSize = new Vector2(48f, 52f);

    [Header("Text 일 때")]
    [SerializeField] private string label = "시작버튼";
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private float fontSize = 40f;

    [Header("동작")]
    [Tooltip("버튼을 누르기 전까지 플레이어를 멈춰 둔다")]
    [SerializeField] private bool holdPlayersUntilPressed = true;
    [Tooltip("한 번 누르면 버튼이 사라진다")]
    [SerializeField] private bool hideAfterPress = true;
    [Tooltip("플레이어가 죽어 스테이지가 초기화되면 버튼을 다시 띄운다")]
    [SerializeField] private bool rearmAfterDeath = true;

    [Header("사운드")]
    [Tooltip("시작 버튼을 눌렀을 때 울릴 소리")]
    [SerializeField] private AudioClip startSound;
    [Range(0f, 1f)]
    [SerializeField] private float startVolume = 1f;

    private Canvas canvas;
    private bool started;
    private bool hiddenForPauseMenu;   // 일시정지 메뉴가 열려서 숨긴 것이면 true, 메뉴가 닫힐 때만 되살린다

    public bool IsStarted => started;

    // 시작 버튼을 누르는 순간 반응해야 하는 것들(예: 물을 부어 둔 불 함정)이 구독한다.
    public static event System.Action StageStarted;

    // 씬에 시작 버튼이 아예 없으면(예: StageScene 1) 기다릴 것도 없다.
    public static bool Exists { get; private set; }
    public static bool HasStarted { get; private set; }

    // 정적 값은 플레이 세션을 넘어 남으므로 매 실행마다 되돌린다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Exists = false;
        HasStarted = false;
        StageStarted = null;
    }

    private void Awake()
    {
        Exists = true;

        // 씬을 다시 불러와도 정적 값은 남는다(ResetStatics는 플레이를 시작할 때 한 번만 돈다).
        // 실패 패널의 '재시작'처럼 씬을 새로 여는 길이 생겼으므로, 버튼이 살아날 때마다 되돌린다.
        // 이걸 빼먹으면 새 판이 이미 출발한 것으로 취급돼 조합창(Tab)이 잠기고 함정이 미리 켜진다.
        HasStarted = false;
        started = false;

        ResolveFont();
        ButtonSkin.ResolveBackground(ref buttonSprite);
        EnsureEventSystem();
        BuildUI();

        StageReset.Requested += HandleStageReset;
        PauseMenuManager.MenuVisibilityChanged += HandlePauseMenuVisibilityChanged;
    }

    private void OnDestroy()
    {
        StageReset.Requested -= HandleStageReset;
        PauseMenuManager.MenuVisibilityChanged -= HandlePauseMenuVisibilityChanged;

        if (started)
        {
            return;
        }

        // 시작하지 못한 채 사라지면 기다리던 쪽이 영영 멈춰 있게 되므로 표시를 거둔다.
        Exists = false;
    }

    // PlayerAutoWalker가 Awake에서 walkOnStart를 읽으므로, 멈추는 건 그 뒤인 Start에서 한다.
    private void Start()
    {
        if (holdPlayersUntilPressed)
        {
            foreach (PlayerAutoWalker walker in FindWalkers())
            {
                walker.StopWalking();
            }
        }
    }

    public void Begin()
    {
        if (started)
        {
            return;
        }

        started = true;
        HasStarted = true;

        SfxPlayer.PlayUI(startSound, startVolume);

        // 지난 판에서 꺼 둔 함정을 먼저 되살린다.
        // 이번 판에 올려 둔 상쇄 원소는 아직 "예약"만 된 상태라 여기서 지워지지 않고,
        // 바로 다음 줄의 StageStarted에서 정상적으로 함정을 끈다.
        StageReset.Request(StageReset.Reason.StageStart);

        // 걷기 시작하기 전에, 미리 배치해 둔 원소들이 먼저 효과를 낸다.
        StageStarted?.Invoke();

        PlayerAutoWalker[] walkers = FindWalkers();
        foreach (PlayerAutoWalker walker in walkers)
        {
            walker.StartWalking();
        }

        if (hideAfterPress && canvas != null)
        {
            canvas.gameObject.SetActive(false);
        }
    }

    // 죽어서 스테이지가 처음 상태로 돌아가면, 다시 배치하고 출발할 수 있도록 버튼을 되살린다.
    private void HandleStageReset(StageReset.Reason reason)
    {
        if (reason != StageReset.Reason.PlayerDeath || !rearmAfterDeath)
        {
            return;
        }

        started = false;
        HasStarted = false;

        if (canvas != null)
        {
            canvas.gameObject.SetActive(true);
        }

        if (holdPlayersUntilPressed)
        {
            foreach (PlayerAutoWalker walker in FindWalkers())
            {
                walker.StopWalking();
            }
        }
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

    private static PlayerAutoWalker[] FindWalkers()
    {
        return FindObjectsByType<PlayerAutoWalker>(FindObjectsSortMode.None);
    }

    // ───────────────────────────── UI 생성 ─────────────────────────────

    private void BuildUI()
    {
        GameObject canvasObject = new GameObject("StartButtonCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // 조합창(100)보다 아래에 둬서, 창이 올라오면 가려지도록 한다.
        canvas.sortingOrder = 90;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject buttonObject = new GameObject("StartButton", typeof(RectTransform), typeof(Image), typeof(Button));
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

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = background;

        // ColorBlock은 배경색에 곱해지는 틴트다. 눌렀을 때만 살짝 어둡게 한다.
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        colors.selectedColor = Color.white;
        button.colors = colors;

        button.onClick.AddListener(Begin);

        BuildContent(rect);
    }

    // 버튼 위에 올릴 것(삼각형 / 그림 / 글자)을 만든다.
    private void BuildContent(RectTransform parent)
    {
        GameObject contentObject = new GameObject("Content", typeof(RectTransform));
        RectTransform contentRect = (RectTransform)contentObject.transform;
        contentRect.SetParent(parent, false);

        if (content == ButtonContent.Text)
        {
            // 글자는 버튼 전체에 깔고 가운데 정렬한다. 그래야 버튼 크기를 바꿔도 따라온다.
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = new Vector2(contentOffset.x, contentOffset.y);
            contentRect.offsetMax = new Vector2(contentOffset.x, contentOffset.y);

            TextMeshProUGUI text = contentObject.AddComponent<TextMeshProUGUI>();
            if (font != null)
            {
                text.font = font;
            }

            text.text = label;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = contentColor;
            text.raycastTarget = false;
            return;
        }

        contentRect.anchorMin = new Vector2(0.5f, 0.5f);
        contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.pivot = new Vector2(0.5f, 0.5f);
        contentRect.sizeDelta = iconSize;
        contentRect.anchoredPosition = contentOffset;

        if (content == ButtonContent.Sprite && iconSprite != null)
        {
            Image icon = contentObject.AddComponent<Image>();
            icon.sprite = iconSprite;
            icon.color = contentColor;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            return;
        }

        // Sprite를 골라 놓고 그림을 안 넣었으면 빈 버튼이 되므로, 삼각형으로 돌려 놓는다.
        PlayTriangleGraphic triangle = contentObject.AddComponent<PlayTriangleGraphic>();
        triangle.color = contentColor;
        triangle.raycastTarget = false;
    }

    private void ResolveFont()
    {
        if (font != null || content != ButtonContent.Text)
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
