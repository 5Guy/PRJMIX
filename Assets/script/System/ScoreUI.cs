using System.Collections;
using TMPro;
using UnityEngine;

// 화면 오른쪽 위에 항상 떠 있는 점수판을 그린다.
// 점수 규칙은 전혀 모르고, ScoreSystem.ScoreChanged 이벤트로 받은 값만 표시한다.
//
// ScoreCanvas/ScoreBox/ScoreText 등은 이 프리팹 안에 실제 오브젝트로 만들어 두었다.
// (EscUI, FaceUpCanvas, CraftingPanel처럼 에디터에서 눈으로 보고 편집할 수 있게)
// 조합창(CraftingPanelUI)은 Tab을 누르면 캔버스째 꺼지기 때문에, 점수판은 조합창에 얹지 않고
// 자기만의 캔버스를 따로 둔다. 그래서 Tab을 누르든 말든 점수는 계속 보인다.
public class ScoreUI : MonoBehaviour
{
    [Header("표시")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text deltaText;

    private Coroutine deltaRoutine;

    private void Start()
    {
        // ScoreSystem.Awake가 먼저 끝난 뒤이므로(모든 오브젝트의 Awake -> Start 순서) 여기서 읽어도 안전하다.
        Refresh(ScoreSystem.Instance != null ? ScoreSystem.Instance.Score : 0);
    }

    private void OnEnable()
    {
        ScoreSystem.ScoreChanged += HandleScoreChanged;
    }

    private void OnDisable()
    {
        ScoreSystem.ScoreChanged -= HandleScoreChanged;
    }

    private void HandleScoreChanged(int score, int delta)
    {
        Refresh(score);

        if (delta != 0)
        {
            ShowDelta(delta);
        }
    }

    private void Refresh(int score)
    {
        if (scoreText != null)
        {
            scoreText.text = $"SCORE  {score}";
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

        Vector2 basePosition = deltaText.rectTransform.anchoredPosition;

        for (float t = 0f; t < 0.9f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.9f;
            deltaText.color = new Color(color.r, color.g, color.b, 1f - k);
            deltaText.rectTransform.anchoredPosition = basePosition + new Vector2(0f, -k * 14f);
            yield return null;
        }

        deltaText.color = new Color(color.r, color.g, color.b, 0f);
        deltaText.rectTransform.anchoredPosition = basePosition;
        deltaRoutine = null;
    }
}
