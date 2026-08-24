using UnityEngine;
using UnityEngine.UI;

// 스테이지 씬의 도감 버튼으로 여는 속성도감 / 장애물 도감 패널.
// 왼쪽 책갈피(원소/장애물)로 분류를 고르고, 좌우 화살표로 그 분류 안의 페이지를 넘긴다.
// 분류별 페이지는 배열이라 나중에 페이지를 추가할 때는 Inspector에서 스프라이트만 더 넣으면 된다.
//
// 인스펙터 연결
//  - Panel Root: 열고 닫을 패널 전체 오브젝트 (비워두면 이 스크립트가 붙은 오브젝트)
//  - Page Image: 현재 페이지 이미지를 보여줄 Image
//  - Element Pages / Obstacle Pages: 각 책갈피에서 순서대로 넘길 스프라이트 목록
//  - Other UI To Hide: 도감이 열려 있는 동안 꺼둘 다른 UI 오브젝트들 (도감 버튼, 조합창 버튼, 점수판 등)
//  - 도감 버튼 OnClick -> OpenPanel()
//  - 닫기(X) 버튼 OnClick -> ClosePanel()
//  - 원소 책갈피 OnClick -> ShowElementCategory() / 장애물 책갈피 OnClick -> ShowObstacleCategory()
//  - 오른쪽 화살표 버튼 OnClick -> NextPage() / 왼쪽 화살표 버튼 OnClick -> PreviousPage()
public class EncyclopediaPanel : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    // 도감이 열리고 닫힐 때마다 알림이 필요한 쪽(예: 시작하기 버튼, 점수판)이 구독한다.
    // true = 도감이 열림(가려야 함), false = 도감이 닫힘(원래 상태로 되돌려도 됨)
    public static event System.Action<bool> VisibilityChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsOpen = false;
        VisibilityChanged = null;
    }

    [Header("패널")]
    [Tooltip("비워두면 이 스크립트가 붙은 오브젝트를 켜고 끈다")]
    [SerializeField] private GameObject panelRoot;
    [Tooltip("패널을 띄울 때 게임 시간을 멈춘다")]
    [SerializeField] private bool pauseGameWhenPanelOpens = true;

    [Header("페이지 표시")]
    [SerializeField] private Image pageImage;

    [Header("책갈피별 페이지 (앞에서부터 순서대로 넘어간다)")]
    [SerializeField] private Sprite[] elementPages = new Sprite[0];
    [SerializeField] private Sprite[] obstaclePages = new Sprite[0];

    [Header("도감이 열려 있는 동안 숨길 다른 UI")]
    [Tooltip("도감을 열면 여기 등록한 오브젝트들을 꺼두고, 도감을 닫으면 다시 켠다 (도감 버튼, 조합창 버튼, 점수판 등)")]
    [SerializeField] private GameObject[] otherUIToHide = new GameObject[0];

    private Sprite[] currentPages;
    private int currentPageIndex;

    private void Awake()
    {
        IsOpen = false;

        GameObject root = GetPanelRoot();
        if (root != null)
        {
            root.SetActive(false);
        }
    }

    // 도감 버튼: 도감을 열고 항상 속성도감(원소 책갈피)부터 보여준다.
    public void OpenPanel()
    {
        if (StageFailPanel.IsOpen)
        {
            return;
        }

        IsOpen = true;
        ShowElementCategory();

        GameObject root = GetPanelRoot();
        if (root != null)
        {
            root.SetActive(true);
        }

        SetOtherUIActive(false);
        VisibilityChanged?.Invoke(true);

        if (pauseGameWhenPanelOpens)
        {
            Time.timeScale = 0f;
        }
    }

    // 닫기(X) 버튼과 ESC(UIInputManager) 둘 다 여기로 들어온다.
    public void ClosePanel()
    {
        IsOpen = false;

        GameObject root = GetPanelRoot();
        if (root != null)
        {
            root.SetActive(false);
        }

        SetOtherUIActive(true);
        VisibilityChanged?.Invoke(false);

        if (pauseGameWhenPanelOpens)
        {
            Time.timeScale = 1f;
        }
    }

    // UIInputManager가 ESC 우선순위를 판단할 때 부른다.
    public static void CloseIfOpen()
    {
        if (!IsOpen)
        {
            return;
        }

        EncyclopediaPanel panel = FindFirstObjectByType<EncyclopediaPanel>(FindObjectsInactive.Include);
        panel?.ClosePanel();
    }

    // 원소 책갈피
    public void ShowElementCategory()
    {
        currentPages = elementPages;
        currentPageIndex = 0;
        ApplyPage();
    }

    // 장애물 책갈피
    public void ShowObstacleCategory()
    {
        currentPages = obstaclePages;
        currentPageIndex = 0;
        ApplyPage();
    }

    // 오른쪽 화살표: 현재 책갈피 안에서 다음 페이지로 (마지막 페이지면 그대로 있는다. 나중에 페이지가 추가되면 자동으로 넘어갈 수 있게 된다)
    public void NextPage()
    {
        if (currentPages == null || currentPages.Length == 0)
        {
            return;
        }

        currentPageIndex = Mathf.Min(currentPageIndex + 1, currentPages.Length - 1);
        ApplyPage();
    }

    // 왼쪽 화살표: 현재 책갈피 안에서 이전 페이지로
    public void PreviousPage()
    {
        if (currentPages == null || currentPages.Length == 0)
        {
            return;
        }

        currentPageIndex = Mathf.Max(currentPageIndex - 1, 0);
        ApplyPage();
    }

    private void ApplyPage()
    {
        if (pageImage == null || currentPages == null || currentPages.Length == 0)
        {
            return;
        }

        pageImage.sprite = currentPages[currentPageIndex];
    }

    private GameObject GetPanelRoot()
    {
        return panelRoot != null ? panelRoot : gameObject;
    }

    private void SetOtherUIActive(bool active)
    {
        foreach (GameObject ui in otherUIToHide)
        {
            if (ui != null)
            {
                ui.SetActive(active);
            }
        }
    }
}
