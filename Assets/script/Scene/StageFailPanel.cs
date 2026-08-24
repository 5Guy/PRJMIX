using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

// 스테이지 실패 시 띄우는 패널/캔버스에 직접 붙이는 스크립트.
//
// 권장 구조
//  - FailCanvas 또는 FailPanel 오브젝트에 이 스크립트를 직접 붙인다.
//  - 시작 기본값은 이 오브젝트를 Active 상태로 둬도 Awake에서 자동 SetActive(false) 된다.
//  - 버튼 OnClick에는 이 스크립트가 붙은 Canvas/Panel 오브젝트를 연결하고 아래 함수를 직접 선택한다.
//
// 버튼 OnClick 연결
//  1. 현재 스테이지 재시작 버튼 -> StageFailPanel.RestartStage()
//  2. 스테이지 선택 이동 버튼 -> StageFailPanel.GoStageSelect()
//  3. 게임 종료 버튼 -> StageFailPanel.QuitGame()
//
// ElementalKillEffect가 커튼 연출/리스폰 뒤 ShowAnyPanel()을 호출해서 이 패널을 띄운다.
//
// 씬에 패널을 넣어 두지 않았으면 Assets/Resources/FailStageCanvas 프리팹을 불러와 그 자리에서 만든다.
// 이 프리팹은 반드시 Resources 폴더에 있어야 한다. 다른 곳에 두면 에디터에서만 뜨고 빌드에서는 뜨지 않는다.
public class StageFailPanel : MonoBehaviour
{
    // 씬에 패널이 없을 때 불러올 프리팹.
    // 빌드에서는 Resources 폴더에 있는 것만 이름으로 불러올 수 있고,
    // 에디터에서는 아래 경로에서 바로 집어 온다.
    private const string FailCanvasResourceName = "FailStageCanvas";
    private const string FailCanvasAssetPath = "Assets/Resources/FailStageCanvas.prefab";

    // 조합창(100) · 검은 화면 연출(200)보다 위.
    private const int FrontSortingOrder = 250;

    public static bool IsOpen { get; private set; }

    // 패널이 열릴 때마다 알림이 필요한 쪽(예: 로봇 표정 UI)이 구독한다.
    // 씬에 미리 놓아둔 패널이든 프리팹에서 즉석에서 만든 패널이든 상관없이 울린다.
    public static event System.Action PanelOpened;

    [Header("패널")]
    [Tooltip("비워두면 이 스크립트가 붙은 Canvas/Panel 오브젝트를 켜고 끈다")]
    [SerializeField] private GameObject failPanel;
    [Tooltip("시작할 때 패널을 자동으로 꺼 둔다")]
    [SerializeField] private bool hideOnAwake = true;
    [Tooltip("패널을 띄울 때 게임 시간을 멈춘다")]
    [SerializeField] private bool pauseGameWhenPanelOpens = true;

    [Header("씬")]
    [Tooltip("스테이지 선택 버튼이 이동할 씬 이름")]
    [SerializeField] private string stageSelectSceneName = "StageSelectScene";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsOpen = false;
        PanelOpened = null;
    }

    private void Awake()
    {
        // 씬을 다시 불러와도 정적 값은 남는다. 남아 있으면 새 판에서 조합창(Tab)과 ESC가 잠긴 채로 시작한다.
        IsOpen = false;

        if (hideOnAwake)
        {
            ClosePanelOnly();
        }
    }

    // ElementalKillEffect / RogueWindStorm 등에서 호출한다.
    // 씬에 있는 첫 StageFailPanel을 찾아 열고, 없으면 프리팹으로 만들어 연다.
    public static bool ShowAnyPanel()
    {
        StageFailPanel panel = FindFirstObjectByType<StageFailPanel>(FindObjectsInactive.Include);

        if (panel == null)
        {
            panel = SpawnFromPrefab();
        }

        if (panel == null)
        {
            Debug.LogWarning(
                $"StageFailPanel이 씬에도 없고 '{FailCanvasAssetPath}' 프리팹도 불러오지 못했습니다. " +
                "실패 패널 Canvas를 씬에 넣거나 프리팹을 Resources 폴더에 두세요.");
            return false;
        }

        panel.OpenPanel();
        return true;
    }

    // 씬에 패널이 없을 때 프리팹에서 하나 만들어 온다.
    private static StageFailPanel SpawnFromPrefab()
    {
        GameObject prefab = Resources.Load<GameObject>(FailCanvasResourceName);

#if UNITY_EDITOR
        if (prefab == null)
        {
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(FailCanvasAssetPath);
        }
#endif

        if (prefab == null)
        {
            return null;
        }

        GameObject spawned = Instantiate(prefab);
        spawned.name = prefab.name;
        spawned.SetActive(true);

        StageFailPanel panel = spawned.GetComponentInChildren<StageFailPanel>(true);
        if (panel == null)
        {
            Debug.LogWarning($"'{prefab.name}' 프리팹에 StageFailPanel이 없습니다.", spawned);
            Destroy(spawned);
            return null;
        }

        EnsureEventSystem();
        return panel;
    }

    // 버튼을 누르려면 EventSystem이 있어야 한다. 씬에 없으면 만들어 준다.
    private static void EnsureEventSystem()
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

    public void OpenPanel()
    {
        IsOpen = true;

        BringCanvasToFront();

        GameObject target = GetPanelObject();
        if (target != null)
        {
            ActivateWithParents(target);
        }
        else
        {
            Debug.LogWarning($"{name}: 켤 실패 패널 오브젝트를 찾지 못했습니다.", this);
        }

        PanelOpened?.Invoke();

        if (pauseGameWhenPanelOpens)
        {
            Time.timeScale = 0f;
        }
    }

    public void ClosePanelOnly()
    {
        IsOpen = false;

        GameObject target = GetPanelObject();
        if (target != null)
        {
            target.SetActive(false);
        }
    }

    // 패널 오브젝트만 켜서는 부족하다.
    // failPanel이 자식으로 물려 있고 그 위 어딘가(Canvas, 그룹 오브젝트)가 꺼져 있으면
    // SetActive(true)를 해도 화면에는 아무것도 나오지 않는다.
    // 그러면 IsOpen만 true가 되고 timeScale은 0으로 내려가서, 패널 없이 게임만 멈춘 것처럼 보인다.
    // 그래서 대상부터 맨 위 조상까지 올라가며 꺼져 있는 것을 전부 켠다.
    private static void ActivateWithParents(GameObject target)
    {
        for (Transform current = target.transform; current != null; current = current.parent)
        {
            if (!current.gameObject.activeSelf)
            {
                current.gameObject.SetActive(true);
            }
        }
    }

    // 조합창(100)이나 검은 화면 연출(200)에 가리지 않게 맨 위로 올린다.
    // 실패 패널은 판이 끝났다는 창이므로 무엇보다 위에 있어야 한다.
    private void BringCanvasToFront()
    {
        Canvas canvas = GetComponentInParent<Canvas>(true);
        if (canvas == null)
        {
            canvas = GetComponentInChildren<Canvas>(true);
        }

        if (canvas == null)
        {
            return;
        }

        // 화면 순서를 정하는 것은 맨 위 캔버스다.
        canvas = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;

        // 캔버스 자체가 꺼져 있으면 아무리 자식을 켜도 화면에 나오지 않는다.
        canvas.enabled = true;

        if (canvas.sortingOrder < FrontSortingOrder)
        {
            canvas.sortingOrder = FrontSortingOrder;
        }
    }

    private GameObject GetPanelObject()
    {
        return failPanel != null ? failPanel : gameObject;
    }

    // 1번 버튼: 현재 스테이지 씬 재시작.
    public void RestartStage()
    {
        IsOpen = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // 2번 버튼: 스테이지 선택 씬으로 이동.
    public void GoStageSelect()
    {
        LoadScene(stageSelectSceneName, "스테이지 선택");
    }

    // 3번 버튼: 게임 종료.
    public void QuitGame()
    {
        IsOpen = false;
        Time.timeScale = 1f;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void LoadScene(string sceneName, string label)
    {
        IsOpen = false;
        Time.timeScale = 1f;

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError($"{name}: {label} 씬 이름이 비어 있습니다.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"{name}: '{sceneName}' 씬이 Build Settings에 없습니다.", this);
            return;
        }

        SceneManager.LoadScene(sceneName);
    }
}
