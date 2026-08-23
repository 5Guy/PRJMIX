using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

// 화면 오른쪽 위에 항상 떠 있는 점수판.
//
//  - 100점에서 시작한다.
//  - 조합창에서 재료를 하나 꺼내면 -1점.
//  - 꺼낸 재료를 조합하지 않고 도로 넣으면(취소) +1점.
//  - 조합에 성공하면 결과 원소(ElementData)의 Score만큼 오른다.
//    점수 값은 엑셀 '조합식' 시트를 그대로 옮겨 Assets/Data/Nomal 의 ScriptableObject에 넣어 두었다.
//    (대부분 +1, 물+나무(늪)처럼 일부는 +2)
//
// 조합창(CraftingPanelUI)은 Tab을 누르면 캔버스째 꺼지기 때문에, 점수판은 조합창에 얹지 않고
// 자기만의 캔버스를 따로 만든다. 그래서 Tab을 누르든 말든 점수는 계속 보인다.
public class ScoreSystem : MonoBehaviour
{
    [Header("점수")]
    [SerializeField] private int startingScore = 100;
    [Tooltip("조합창에서 재료를 하나 꺼낼 때 깎이는 점수")]
    [SerializeField] private int takeOutCost = 1;

    [Header("표시")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [SerializeField] private Vector2 margin = new Vector2(40f, 32f);
    [SerializeField] private Vector2 boxSize = new Vector2(340f, 96f);
    [SerializeField] private Color boxColor = new Color(0.03f, 0.08f, 0.33f, 0.82f);

    public static ScoreSystem Instance { get; private set; }

    // 점수가 바뀔 때마다 (현재 점수, 변화량) 을 알린다.
    public static event Action<int, int> ScoreChanged;

    public int Score { get; private set; }

    private TMP_Text scoreText;
    private TMP_Text deltaText;
    private Coroutine deltaRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Score = startingScore;

        ResolveFont();
        BuildUI();
        Refresh();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ───────────────────────────── 점수 조작 ─────────────────────────────

    // 점수판이 없는 씬에서도 조합창이 그냥 돌아가도록, 없으면 조용히 넘어간다.
    public static void Add(int amount)
    {
        if (Instance != null)
        {
            Instance.AddScore(amount);
        }
    }

    // 재료를 하나 꺼냈다. (조합창 왼쪽 목록에서 물로 끌어낼 때)
    public static void TakeOutMaterial()
    {
        if (Instance != null)
        {
            Instance.AddScore(-Mathf.Abs(Instance.takeOutCost));
        }
    }

    // 꺼냈던 재료를 조합하지 않고 도로 넣었다. (물 밖으로 끌고 나가 취소할 때) TakeOutMaterial의 반대.
    public static void PutBackMaterial()
    {
        if (Instance != null)
        {
            Instance.AddScore(Mathf.Abs(Instance.takeOutCost));
        }
    }

    // 조합에 성공했다. 오르는 점수는 결과 원소가 들고 있다.
    public static void Combined(ElementData result)
    {
        if (Instance != null && result != null)
        {
            Instance.AddScore(result.Score);
        }
    }

    public void AddScore(int amount)
    {
        if (amount == 0)
        {
            return;
        }

        Score += amount;
        Refresh();
        ShowDelta(amount);
        ScoreChanged?.Invoke(Score, amount);
    }

    public void ResetScore()
    {
        Score = startingScore;
        Refresh();
        ScoreChanged?.Invoke(Score, 0);
    }

    private void Refresh()
    {
        if (scoreText != null)
        {
            scoreText.text = $"SCORE  {Score}";
        }
    }

    // 점수가 움직이면 숫자 아래에 +2 / -1 이 잠깐 떴다 사라진다.
    private void ShowDelta(int amount)
    {
        if (deltaText == null || !isActiveAndEnabled)
        {
            return;
        }

        if (deltaRoutine != null)
        {
            StopCoroutine(deltaRoutine);
        }

        deltaRoutine = StartCoroutine(DeltaRoutine(amount));
    }

    private IEnumerator DeltaRoutine(int amount)
    {
        Color color = amount > 0 ? new Color(0.6f, 1f, 0.7f) : new Color(1f, 0.6f, 0.55f);
        deltaText.text = amount > 0 ? $"+{amount}" : amount.ToString();

        for (float t = 0f; t < 0.9f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.9f;
            deltaText.color = new Color(color.r, color.g, color.b, 1f - k);
            deltaText.rectTransform.anchoredPosition = new Vector2(0f, -8f - k * 14f);
            yield return null;
        }

        deltaText.color = new Color(color.r, color.g, color.b, 0f);
        deltaRoutine = null;
    }

    // ───────────────────────────── UI 생성 ─────────────────────────────

    private void BuildUI()
    {
        GameObject canvasObject = new GameObject("ScoreCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;   // 조합창(100)보다 위에 그려서 절대 가려지지 않게 한다

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // 오른쪽 위 모서리에 붙인다.
        GameObject boxObject = new GameObject("ScoreBox", typeof(RectTransform), typeof(Image));
        RectTransform box = (RectTransform)boxObject.transform;
        box.SetParent(canvasObject.transform, false);
        box.anchorMin = new Vector2(1f, 1f);
        box.anchorMax = new Vector2(1f, 1f);
        box.pivot = new Vector2(1f, 1f);
        box.sizeDelta = boxSize;
        box.anchoredPosition = new Vector2(-margin.x, -margin.y);

        Image background = boxObject.GetComponent<Image>();
        background.color = boxColor;
        background.raycastTarget = false;   // 점수판이 클릭을 가로채면 안 된다

        scoreText = CreateText(box, string.Empty, 44f);
        scoreText.alignment = TextAlignmentOptions.Right;
        scoreText.rectTransform.offsetMin = new Vector2(16f, 0f);
        scoreText.rectTransform.offsetMax = new Vector2(-16f, 0f);

        // 변화량은 점수 상자 바로 아래에 뜬다.
        RectTransform deltaRect = new GameObject("Delta", typeof(RectTransform)).GetComponent<RectTransform>();
        deltaRect.SetParent(box, false);
        deltaRect.anchorMin = new Vector2(0f, 0f);
        deltaRect.anchorMax = new Vector2(1f, 0f);
        deltaRect.pivot = new Vector2(0.5f, 1f);
        deltaRect.sizeDelta = new Vector2(0f, 40f);
        deltaRect.anchoredPosition = new Vector2(0f, -8f);

        deltaText = CreateText(deltaRect, string.Empty, 32f);
        deltaText.alignment = TextAlignmentOptions.Right;
        deltaText.rectTransform.offsetMax = new Vector2(-16f, 0f);
        deltaText.color = new Color(1f, 1f, 1f, 0f);
    }

    private TMP_Text CreateText(RectTransform parent, string content, float size)
    {
        GameObject textObject = new GameObject("Text", typeof(RectTransform));
        RectTransform rect = (RectTransform)textObject.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();

        if (font != null)
        {
            text.font = font;
        }

        text.text = content;
        text.fontSize = size;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    // 조합창과 같은 폰트를 쓴다. 인스펙터가 비어 있으면 Assets/Font에서 찾는다.
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
}
