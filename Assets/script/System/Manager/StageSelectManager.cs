using UnityEngine;
using UnityEngine.SceneManagement;

// StageSelectScene 관리
public class StageSelectManager : MonoBehaviour
{
    [Header("Manual UI")]
    [SerializeField] private GameObject manualPanel;

    private void Start()
    {
        // 시작할 때 메뉴얼 패널 닫기
        if (manualPanel != null)
        {
            manualPanel.SetActive(false);
        }
    }

    // =========================
    // 스테이지 이동
    // =========================
    public void LoadStage(string sceneName)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    // =========================
    // 메뉴얼 열기
    // =========================
    public void OpenManual()
    {
        if (manualPanel != null)
        {
            manualPanel.SetActive(true);
        }
    }

    // =========================
    // 메뉴얼 닫기
    // =========================
    public void CloseManual()
    {
        if (manualPanel != null)
        {
            manualPanel.SetActive(false);
        }
    }
}