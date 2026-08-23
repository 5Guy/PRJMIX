using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject firstScreenPanel;
    [SerializeField] private GameObject optionPanel;

    [Header("Scene")]
    [SerializeField] private string stageScene1Name = "StageScene 1";

    private void Start()
    {
        Time.timeScale = 1f;

        firstScreenPanel.SetActive(true);
        optionPanel.SetActive(false);
    }

    public void OpenOptionPanel()
    {
        firstScreenPanel.SetActive(false);
        optionPanel.SetActive(true);
    }

    public void CloseOptionPanel()
    {
        optionPanel.SetActive(false);
        firstScreenPanel.SetActive(true);
    }

    public void StartGame()
    {
        Time.timeScale = 1f;

        SceneManager.LoadScene(stageScene1Name);
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
