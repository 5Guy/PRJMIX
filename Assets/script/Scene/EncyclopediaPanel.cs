using UnityEngine;

// 스테이지 씬(예: Stage_03_1) 왼쪽 위 버튼으로 여는 속성도감 / 장애물 도감 패널.
// 페이지는 미리 만들어 둔 이미지 오브젝트 두 장이고, 좌우 화살표로 그 사이를 넘긴다.
//
// 인스펙터 연결
//  - Panel Root: 열고 닫을 패널 전체 오브젝트 (비워두면 이 스크립트가 붙은 오브젝트)
//  - Element Codex Page: 속성도감 이미지 오브젝트
//  - Obstacle Codex Page: 장애물 도감 이미지 오브젝트
//  - Other UI To Hide: 도감이 열려 있는 동안 꺼둘 다른 UI 오브젝트들 (조합창 버튼, 점수판 등)
//  - 왼쪽 위 버튼 OnClick -> OpenPanel()
//  - 닫기(X) 버튼 OnClick -> ClosePanel()
//  - 오른쪽 화살표 버튼 OnClick -> NextPage()
//  - 왼쪽 화살표 버튼 OnClick -> PreviousPage()
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

    [Header("페이지")]
    [SerializeField] private GameObject elementCodexPage;
    [SerializeField] private GameObject obstacleCodexPage;

    [Header("도감이 열려 있는 동안 숨길 다른 UI")]
    [Tooltip("도감을 열면 여기 등록한 오브젝트들을 꺼두고, 도감을 닫으면 다시 켠다 (조합창 버튼, 점수판 등)")]
    [SerializeField] private GameObject[] otherUIToHide = new GameObject[0];

    private bool showingObstaclePage;

    private void Awake()
    {
        IsOpen = false;

        GameObject root = GetPanelRoot();
        if (root != null)
        {
            root.SetActive(false);
        }
    }

    // 왼쪽 위 버튼: 도감을 열고 항상 속성도감(첫 페이지)부터 보여준다.
    public void OpenPanel()
    {
        if (StageFailPanel.IsOpen)
        {
            return;
        }

        IsOpen = true;
        showingObstaclePage = false;
        ApplyPage();

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

    // 오른쪽 화살표: 속성도감 -> 장애물 도감
    public void NextPage()
    {
        showingObstaclePage = true;
        ApplyPage();
    }

    // 왼쪽 화살표: 장애물 도감 -> 속성도감
    public void PreviousPage()
    {
        showingObstaclePage = false;
        ApplyPage();
    }

    private void ApplyPage()
    {
        if (elementCodexPage != null)
        {
            elementCodexPage.SetActive(!showingObstaclePage);
        }

        if (obstacleCodexPage != null)
        {
            obstacleCodexPage.SetActive(showingObstaclePage);
        }
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
