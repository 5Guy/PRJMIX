using System.Collections;
using LitMotion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

// "시스템 다운" 화면 연출.
//
// SandstormKillZone은 기존 모랫빛 지직거림 연출을 사용하고,
// ElementalKillEffect는 LitMotion으로 검은 커튼이 닫혔다가 리스폰 후 열리는 연출을 사용한다.
// UI는 전부 런타임에 만들기 때문에 씬에 미리 넣어 둘 것이 없다.
public class SystemDownScreen : MonoBehaviour
{
    private const float FadeOutDuration = 0.35f;

    private static SystemDownScreen instance;

    private CanvasGroup group;
    private RectTransform canvasRect;
    private Image overlay;
    private Image scanline;
    private Image leftCurtain;
    private Image rightCurtain;
    private RectTransform leftCurtainRect;
    private RectTransform rightCurtainRect;
    private RectTransform titleRect;
    private TextMeshProUGUI title;
    private TextMeshProUGUI message;
    private Coroutine routine;
    private bool playingCurtain;

    // 정적 참조는 플레이 세션을 넘어 남으므로 매 실행마다 비운다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    // 이미 떠 있으면 새로 만들지 않고 시간만 다시 채운다(연달아 휩쓸려도 화면이 겹치지 않게).
    public static void Show(string messageText, float duration)
    {
        EnsureInstance();
        instance.Play(messageText, duration);
    }

    // ElementalKillEffect용: 검은 커튼 + Title/Message 텍스트만 쓴다.
    // SandstormKillZone용 SandOverlay/Scanline 패널 요소는 사용하지 않는다.
    public static void ShowCurtain(string messageText, float startDelay, float closeDuration, float closedHoldDuration, float openDuration)
    {
        EnsureInstance();
        instance.PlayCurtain(messageText, startDelay, closeDuration, closedHoldDuration, openDuration);
    }

    // 검은 커튼을 닫고 그대로 둔다(다시 열지 않는다).
    // 황소처럼 리스폰 없이 판이 끝나는 경우, 실패 패널 뒤에 깔 검은 배경으로 쓴다.
    // 거두는 것은 부르는 쪽이 Hide()로 하거나, 씬을 새로 불러올 때 저절로 사라진다.
    public static void ShowCurtainClosed(float startDelay, float closeDuration)
    {
        EnsureInstance();
        instance.PlayCurtainClosed(startDelay, closeDuration);
    }

    public static void Hide()
    {
        if (instance != null)
        {
            Destroy(instance.gameObject);
            instance = null;
        }
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        GameObject host = new GameObject("SystemDownScreen");
        instance = host.AddComponent<SystemDownScreen>();
        instance.Build();
    }

    private void OnEnable()
    {
        // 리스폰하면 연출은 제 역할을 다한 것이므로 곧바로 걷는다.
        PlayerAutoWalker.Respawned += HandleRespawned;
    }

    private void OnDisable()
    {
        PlayerAutoWalker.Respawned -= HandleRespawned;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void HandleRespawned()
    {
        if (!playingCurtain)
        {
            Hide();
        }
    }

    private void Play(string messageText, float duration)
    {
        if (message != null)
        {
            message.text = messageText;
        }

        StopCurrentRoutine();
        routine = StartCoroutine(PlayRoutine(Mathf.Max(0.1f, duration)));
    }

    private void PlayCurtain(string messageText, float startDelay, float closeDuration, float closedHoldDuration, float openDuration)
    {
        if (message != null)
        {
            message.text = messageText;
        }

        // 커튼 연출은 검은 커튼과 Title/Message만 사용한다.
        // SandstormKillZone의 Show()에서 쓰는 SandOverlay/Scanline은 시작 지연 시간 중에도 보이면 안 된다.
        overlay.gameObject.SetActive(false);
        scanline.gameObject.SetActive(false);
        SetSystemDownTextVisible(false);
        StopCurrentRoutine();
        routine = StartCoroutine(PlayCurtainRoutine(
            Mathf.Max(0f, startDelay),
            Mathf.Max(0.01f, closeDuration),
            Mathf.Max(0f, closedHoldDuration),
            Mathf.Max(0.01f, openDuration)));
    }

    private void PlayCurtainClosed(float startDelay, float closeDuration)
    {
        overlay.gameObject.SetActive(false);
        scanline.gameObject.SetActive(false);
        SetSystemDownTextVisible(false);
        StopCurrentRoutine();
        routine = StartCoroutine(PlayCurtainClosedRoutine(
            Mathf.Max(0f, startDelay),
            Mathf.Max(0.01f, closeDuration)));
    }

    private void StopCurrentRoutine()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        playingCurtain = false;
    }

    // 사망 연출 중에는 Time.timeScale이 어떻게 되어 있든 화면은 흘러가야 하므로
    // 전부 unscaledDeltaTime으로 돈다.
    private IEnumerator PlayRoutine(float duration)
    {
        SetCurtainVisible(false);
        SetCurtainProgress(0f);
        SetSystemDownTextVisible(true);
        overlay.gameObject.SetActive(true);
        scanline.gameObject.SetActive(true);
        group.alpha = 1f;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            // 남은 시간이 얼마 없으면 서서히 사라진다.
            float remaining = duration - elapsed;
            group.alpha = remaining < FadeOutDuration ? Mathf.Clamp01(remaining / FadeOutDuration) : 1f;

            // 지직거림: 모랫빛 농도와 스캔라인 위치를 매 프레임 흔든다.
            overlay.color = new Color(0.76f, 0.62f, 0.34f, Random.Range(0.55f, 0.78f));
            // 위치는 CanvasScaler 기준 해상도(1080) 안에서 잡는다. 실제 화면 크기와는 무관하다.
            scanline.rectTransform.anchoredPosition = new Vector2(0f, Random.Range(-540f, 540f));
            scanline.color = new Color(1f, 0.95f, 0.8f, Random.Range(0.03f, 0.12f));

            // 글자는 가끔 크게 튀고, 평소에는 조금씩만 떤다.
            float jolt = Random.value < 0.12f ? 18f : 4f;
            titleRect.anchoredPosition = new Vector2(Random.Range(-jolt, jolt), Random.Range(-jolt * 0.4f, jolt * 0.4f));
            title.color = Random.value < 0.08f ? new Color(1f, 0.35f, 0.3f) : Color.white;

            yield return null;
        }

        routine = null;
        Hide();
    }

    private IEnumerator PlayCurtainRoutine(float startDelay, float closeDuration, float closedHoldDuration, float openDuration)
    {
        playingCurtain = true;

        if (startDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(startDelay);
        }

        overlay.gameObject.SetActive(false);
        scanline.gameObject.SetActive(false);
        SetCurtainVisible(true);
        SetSystemDownTextVisible(false);
        SetCurtainProgress(0f);
        group.alpha = 1f;

        yield return PlayCurtainMotion(0f, 1f, closeDuration, Ease.InExpo);

        SetSystemDownTextVisible(true);
        titleRect.anchoredPosition = Vector2.zero;
        title.color = Color.white;

        if (closedHoldDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(closedHoldDuration);
        }

        SetSystemDownTextVisible(false);
        yield return PlayCurtainMotion(1f, 0f, openDuration, Ease.Linear);

        playingCurtain = false;
        routine = null;
        Hide();
    }

    // 닫히기만 하는 커튼. 글자 없이 화면만 검게 덮는다.
    private IEnumerator PlayCurtainClosedRoutine(float startDelay, float closeDuration)
    {
        // 리스폰 알림에 스스로 사라지지 않도록 커튼 연출 중임을 표시해 둔다.
        playingCurtain = true;

        if (startDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(startDelay);
        }

        overlay.gameObject.SetActive(false);
        scanline.gameObject.SetActive(false);
        SetCurtainVisible(true);
        SetSystemDownTextVisible(false);
        SetCurtainProgress(0f);
        group.alpha = 1f;

        yield return PlayCurtainMotion(0f, 1f, closeDuration, Ease.InExpo);

        // 닫힌 채로 둔다.
        routine = null;
    }

    private IEnumerator PlayCurtainMotion(float from, float to, float duration, Ease ease)
    {
        bool complete = false;
        LMotion.Create(from, to, duration)
            .WithEase(ease)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .WithOnComplete(() => complete = true)
            .Bind(SetCurtainProgress);

        while (!complete)
        {
            yield return null;
        }
    }

    private void SetCurtainProgress(float progress)
    {
        // 커튼 한 장의 폭은 화면 절반이다.
        //
        // 참조 해상도(1920)의 절반인 960을 그대로 박아 두면 화면이 16:9일 때만 맞는다.
        // CanvasScaler가 ScaleWithScreenSize + match 0.5라서, 비율이 다른 화면에서는
        // 캔버스 좌표계의 폭이 1920이 아니게 되고(예: 905x377 창에서는 약 2232),
        // 커튼이 960까지만 자라 가운데에 틈이 남은 채로 멈춘다.
        // 그래서 그때그때 캔버스의 실제 폭을 재서 그 절반을 쓴다.
        float halfWidth = canvasRect != null ? canvasRect.rect.width * 0.5f : 960f;

        // 반올림 때문에 다 닫힌 뒤에도 가운데에 1px 실선이 남는 일이 없도록 올림한다.
        // 두 장 다 검은색이라 조금 겹쳐도 보이는 결과는 같다.
        float width = Mathf.Ceil(halfWidth * Mathf.Clamp01(progress));
        leftCurtainRect.sizeDelta = new Vector2(width, 0f);
        rightCurtainRect.sizeDelta = new Vector2(width, 0f);
    }

    private void SetCurtainVisible(bool visible)
    {
        leftCurtain.gameObject.SetActive(visible);
        rightCurtain.gameObject.SetActive(visible);
    }

    private void SetSystemDownTextVisible(bool visible)
    {
        title.gameObject.SetActive(visible);
        message.gameObject.SetActive(visible);
    }

    private void Build()
    {
        GameObject canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // 조합창(100)보다 위에 덮어야 "시스템이 멎었다"는 느낌이 산다.
        canvas.sortingOrder = 200;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasRect = canvasObject.GetComponent<RectTransform>();

        group = canvasObject.GetComponent<CanvasGroup>();
        group.alpha = 1f;

        // 연출일 뿐이므로 클릭을 가로채지 않는다.
        group.blocksRaycasts = false;
        group.interactable = false;

        overlay = CreateStretchedImage("SandOverlay", canvasObject.transform, new Color(0.76f, 0.62f, 0.34f, 0.7f));
        overlay.gameObject.SetActive(false);

        scanline = CreateStretchedImage("Scanline", canvasObject.transform, new Color(1f, 0.95f, 0.8f, 0.08f));
        scanline.gameObject.SetActive(false);
        RectTransform scanRect = scanline.rectTransform;
        scanRect.anchorMin = new Vector2(0f, 0.5f);
        scanRect.anchorMax = new Vector2(1f, 0.5f);
        scanRect.offsetMin = new Vector2(0f, -40f);
        scanRect.offsetMax = new Vector2(0f, 40f);

        leftCurtain = CreateCurtainImage("LeftCurtain", canvasObject.transform, true);
        rightCurtain = CreateCurtainImage("RightCurtain", canvasObject.transform, false);
        leftCurtainRect = leftCurtain.rectTransform;
        rightCurtainRect = rightCurtain.rectTransform;
        SetCurtainVisible(false);

        TMP_FontAsset font = ResolveFont();

        title = CreateText("Title", canvasObject.transform, font, 140f, FontStyles.Bold);
        titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.sizeDelta = new Vector2(1400f, 220f);
        title.text = "SYSTEM DOWN";

        message = CreateText("Message", canvasObject.transform, font, 48f, FontStyles.Normal);
        RectTransform messageRect = message.rectTransform;
        messageRect.anchorMin = new Vector2(0.5f, 0.5f);
        messageRect.anchorMax = new Vector2(0.5f, 0.5f);
        messageRect.pivot = new Vector2(0.5f, 1f);
        messageRect.sizeDelta = new Vector2(1400f, 90f);
        messageRect.anchoredPosition = new Vector2(0f, -120f);

        SetSystemDownTextVisible(false);
    }

    private static Image CreateStretchedImage(string name, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)imageObject.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Image CreateCurtainImage(string name, Transform parent, bool left)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)imageObject.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = left ? Vector2.zero : new Vector2(1f, 0f);
        rect.anchorMax = left ? new Vector2(0f, 1f) : Vector2.one;
        rect.pivot = left ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, 0f);

        Image image = imageObject.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset font, float size, FontStyles style)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        if (font != null)
        {
            text.font = font;
        }

        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    // 시작 버튼(StageStartButton)과 같은 방식으로 프로젝트 폰트를 집어 온다.
    // 빌드에서는 여기서 null이 나오지만, TMP 기본 폰트(TMP Settings)를 프로젝트 한글 폰트로
    // 지정해 두었으므로 그대로 두어도 한글이 깨지지 않는다.
    private static TMP_FontAsset ResolveFont()
    {
#if UNITY_EDITOR
        foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets/Font" }))
        {
            TMP_FontAsset found = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (found != null)
            {
                return found;
            }
        }
#endif
        return null;
    }
}
