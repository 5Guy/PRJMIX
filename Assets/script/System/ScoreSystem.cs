using System;
using UnityEngine;

// 점수 데이터와 규칙만 담당한다. 화면에 어떻게 그릴지는 ScoreUI가 맡는다.
//
//  - 100점에서 시작한다.
//  - 조합창에서 재료를 하나 꺼내면 -1점.
//  - 꺼낸 재료를 조합하지 않고 도로 넣으면(취소) +1점.
//  - 조합에 성공하면 결과 원소(ElementData)의 Score만큼 오른다.
//    점수 값은 엑셀 '조합식' 시트를 그대로 옮겨 Assets/Data/Nomal 의 ScriptableObject에 넣어 두었다.
//    (대부분 +1, 물+나무(늪)처럼 일부는 +2)
public class ScoreSystem : MonoBehaviour
{
    [Header("점수")]
    [SerializeField] private int startingScore = 100;
    [Tooltip("조합창에서 재료를 하나 꺼낼 때 깎이는 점수")]
    [SerializeField] private int takeOutCost = 1;

    public static ScoreSystem Instance { get; private set; }

    // 점수가 바뀔 때마다 (현재 점수, 변화량) 을 알린다. ScoreUI는 이 이벤트만 구독한다.
    public static event Action<int, int> ScoreChanged;

    public int Score { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Score = startingScore;
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
        ScoreChanged?.Invoke(Score, amount);
    }

    public void ResetScore()
    {
        Score = startingScore;
        ScoreChanged?.Invoke(Score, 0);
    }
}
