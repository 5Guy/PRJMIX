using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenuManager : MonoBehaviour
{
    public static bool IsMenuOpen { get; private set; }

    // 일시정지 메뉴가 열리고 닫힐 때마다 알림이 필요한 쪽(예: 시작하기/다시하기 버튼)이 구독한다.
    // true = 메뉴가 열림(가려야 함), false = 메뉴가 완전히 닫힘(원래 상태로 되돌려도 됨)
    public static event System.Action<bool> MenuVisibilityChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        MenuVisibilityChanged = null;
    }

    [Header("UI")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject optionPanel;

    [Header("Scene")]
    [SerializeField] private string mainMenuSceneName = "MainScene";

    [Header("Input")]
    [SerializeField] private UIInputManager inputManager;

    private bool isPauseOpen = false;
    private bool isOptionOpen = false;

    private void Start()
    {
        IsMenuOpen = false;
        Time.timeScale = 1f;

        pausePanel.SetActive(false);
        optionPanel.SetActive(false);
    }

    private void OnEnable()
    {
        if (inputManager != null)
        {
            inputManager.OnPausePressed += TogglePause;
        }
    }

    private void OnDisable()
    {
        if (inputManager != null)
        {
            inputManager.OnPausePressed -= TogglePause;
        }
    }

    private void TogglePause()
    {
        // 실패 패널이 떠 있는 동안에는 일시정지로 화면을 되살리지 못하게 막는다.
        if (StageFailPanel.IsOpen)
        {
            return;
        }

        if (isOptionOpen)
        {
            CloseOptionPanel();
        }
        else if (isPauseOpen)
        {
            ClosePausePanel();
        }
        else
        {
            OpenPausePanel();
        }
    }

    private void OpenPausePanel()
    {
        isPauseOpen = true;
        isOptionOpen = false;
        IsMenuOpen = true;
        Time.timeScale = 0f;

        pausePanel.SetActive(true);
        optionPanel.SetActive(false);

        MenuVisibilityChanged?.Invoke(true);
    }

    public void ClosePausePanel()
    {
        isPauseOpen = false;
        isOptionOpen = false;
        IsMenuOpen = false;
        Time.timeScale = 1f;

        pausePanel.SetActive(false);
        optionPanel.SetActive(false);

        MenuVisibilityChanged?.Invoke(false);
    }

    public void OpenOptionPanel()
    {
        isPauseOpen = false;
        isOptionOpen = true;

        pausePanel.SetActive(false);
        optionPanel.SetActive(true);
    }

    public void CloseOptionPanel()
    {
        isOptionOpen = false;
        isPauseOpen = true;

        optionPanel.SetActive(false);
        pausePanel.SetActive(true);
    }

    public void GoToMainScene()
    {
        IsMenuOpen = false;
        Time.timeScale = 1f;

        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void QuitGame()
    {
        IsMenuOpen = false;
        Time.timeScale = 1f;

        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
