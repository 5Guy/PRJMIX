using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 깃발 도착 지점에서 클리어 패널을 띄우고, 패널 안의 4개 버튼 동작을 처리한다.
//
// 배치 예시
//  - FlagRoot(빈 부모)
//      - GoalPanelObject(빈 오브젝트, 이 스크립트 부착)
//      - GoalTrigger(콜라이더 자식, Is Trigger 켜기)
//
// 인스펙터 연결
//  - Trigger Object: 실제 플레이어가 닿을 콜라이더가 붙은 자식 오브젝트
//  - Goal Panel: 띄울 UI 패널. 시작 시 자동으로 SetActive(false)
//  - Next Button: 다음 스테이지 씬 이동 버튼
//  - Restart Button: 현재 스테이지 씬 재시작 버튼
//  - Stage Select Button: 스테이지 선택 씬 이동 버튼
//  - Quit Button: 게임 종료 버튼
//  - Next Scene Name: 다음 스테이지 씬 이름
//  - Stage Select Scene Name: 스테이지 선택 씬 이름
public class GoalPanel : MonoBehaviour
{
    [Header("트리거")]
    [Tooltip("플레이어가 닿을 콜라이더가 붙은 자식 오브젝트를 연결한다")]
    [SerializeField] private Transform triggerObject;

    [Tooltip("Trigger Object 대신 콜라이더를 직접 연결해도 된다")]
    [SerializeField] private Collider triggerCollider;

    [Header("패널")]
    [Tooltip("도착하면 띄울 패널. 시작 시 자동으로 꺼진다")]
    [SerializeField] private GameObject goalPanel;
    [Tooltip("플레이어가 닿았을 때 패널을 띄운다")]
    [SerializeField] private bool showPanelOnReach = true;
    [Tooltip("패널을 띄울 때 게임 시간을 멈춘다")]
    [SerializeField] private bool pauseGameWhenPanelOpens = true;

    [Header("도착 처리 옵션")]
    [Tooltip("도착하면 플레이어의 자동 걷기를 멈춘다")]
    [SerializeField] private bool stopPlayerOnReach = true;
    [Tooltip("도착 시 플레이어 Animator에 트리거를 보낸다")]
    [SerializeField] private bool playClearAnimationOnReach = false;
    [Tooltip("도착 시 플레이어 Animator에 걸 트리거 이름")]
    [SerializeField] private string clearAnimationTriggerName = "";
    [Tooltip("도착 시 이펙트 오브젝트를 켠다")]
    [SerializeField] private bool playReachEffectOnReach = false;
    [Tooltip("도착 시 재생할 파티클/사운드 등의 뿌리")]
    [SerializeField] private GameObject reachEffect;
    [Tooltip("도착 시 UnityEvent를 실행한다")]
    [SerializeField] private bool invokeEventOnReach = true;

    [Header("도착 이벤트")]
    [SerializeField] private UnityEvent<GameObject> onPlayerReached;

    [Header("패널 버튼")]
    [SerializeField] private Button nextButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button stageSelectButton;
    [SerializeField] private Button quitButton;


    [Header("씬")]
    [Tooltip("1번 버튼이 이동할 다음 스테이지 씬 이름")]
    [SerializeField] private string nextSceneName;
    [Tooltip("3번 버튼이 이동할 스테이지 선택 씬 이름")]
    [SerializeField] private string stageSelectSceneName = "StageSelectScene";

    [Header("기존 StageGoalFlag 막기")]
    [Tooltip("기존 StageGoalFlag가 씬에 남아 있으면 자동 다음 씬 이동을 막기 위해 전부 꺼 둔다")]
    [SerializeField] private bool disableStageGoalFlagOnAwake = true;

    private bool opened;

    private void Awake()
    {
        SetupTriggerCollider();
        RegisterButtons();
        DisableStageGoalFlagsIfNeeded();

        if (goalPanel != null)
        {
            goalPanel.SetActive(false);
        }

        if (reachEffect != null)
        {
            reachEffect.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        UnregisterButtons();
    }

    private void OnValidate()
    {
        triggerCollider = FindTriggerCollider();
    }

    private void OnTriggerEnter(Collider other)
    {
        TryOpenPanel(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryOpenPanel(other);
    }

    internal void RelayTriggerEnter(Collider other)
    {
        TryOpenPanel(other);
    }

    internal void RelayTriggerStay(Collider other)
    {
        TryOpenPanel(other);
    }

    private void SetupTriggerCollider()
    {
        triggerCollider = FindTriggerCollider();

        if (triggerCollider == null)
        {
            Debug.LogWarning($"{name}: Trigger Object에 Collider가 없습니다. 콜라이더가 붙은 자식 오브젝트를 연결해 주세요.", this);
            return;
        }

        if (!triggerCollider.isTrigger)
        {
            Debug.LogWarning($"{name}: Trigger Collider는 Is Trigger가 켜져 있어야 합니다. 자동으로 켭니다.", triggerCollider);
            triggerCollider.isTrigger = true;
        }

        if (triggerCollider.gameObject != gameObject)
        {
            GoalPanelTriggerRelay relay = triggerCollider.GetComponent<GoalPanelTriggerRelay>();
            if (relay == null)
            {
                relay = triggerCollider.gameObject.AddComponent<GoalPanelTriggerRelay>();
            }

            relay.SetOwner(this);
        }
    }

    private Collider FindTriggerCollider()
    {
        if (triggerCollider != null)
        {
            return triggerCollider;
        }

        if (triggerObject != null)
        {
            return triggerObject.GetComponent<Collider>();
        }

        return GetComponent<Collider>();
    }

    private void RegisterButtons()
    {
        if (nextButton != null)
        {
            nextButton.onClick.AddListener(GoNextStage);
        }

        if (restartButton != null)
        {
            restartButton.onClick.AddListener(RestartStage);
        }

        if (stageSelectButton != null)
        {
            stageSelectButton.onClick.AddListener(GoStageSelect);
        }

        if (quitButton != null)
        {
            quitButton.onClick.AddListener(QuitGame);
        }
    }

    private void UnregisterButtons()
    {
        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(GoNextStage);
        }

        if (restartButton != null)
        {
            restartButton.onClick.RemoveListener(RestartStage);
        }

        if (stageSelectButton != null)
        {
            stageSelectButton.onClick.RemoveListener(GoStageSelect);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(QuitGame);
        }
    }

    private void DisableStageGoalFlagsIfNeeded()
    {
        if (!disableStageGoalFlagOnAwake)
        {
            return;
        }

        // GoalPanel 오브젝트와 StageGoalFlag가 같은 부모 밑에 없을 수도 있으므로
        // 씬에 남아 있는 StageGoalFlag를 전부 꺼서 자동 다음 씬 이동 코루틴이 시작되지 않게 한다.
        StageGoalFlag[] flags = FindObjectsByType<StageGoalFlag>(FindObjectsSortMode.None);
        foreach (StageGoalFlag flag in flags)
        {
            if (flag != null && flag.enabled)
            {
                flag.StopAllCoroutines();
                flag.enabled = false;
            }
        }
    }

    private void TryOpenPanel(Collider other)
    {
        DisableStageGoalFlagsIfNeeded();

        if (opened || other == null)
        {
            return;
        }

        Transform player = PlayerLocator.FindPlayerRoot(other.transform);
        if (player == null || PlayerLocator.IsAlreadyDead(player))
        {
            return;
        }

        opened = true;

        if (stopPlayerOnReach)
        {
            PlayerAutoWalker walker = player.GetComponentInChildren<PlayerAutoWalker>();
            if (walker != null)
            {
                walker.StopWalking();
            }
        }

        if (playClearAnimationOnReach)
        {
            PlayClearAnimation(player);
        }

        if (playReachEffectOnReach && reachEffect != null)
        {
            reachEffect.SetActive(true);
        }

        if (invokeEventOnReach)
        {
            onPlayerReached?.Invoke(player.gameObject);
        }

        if (showPanelOnReach)
        {
            OpenGoalPanel();
        }
    }

    private void OpenGoalPanel()
    {
        if (goalPanel != null)
        {
            goalPanel.SetActive(true);
        }
        else
        {
            Debug.LogWarning($"{name}: Goal Panel이 연결되지 않았습니다.", this);
        }

        if (pauseGameWhenPanelOpens)
        {
            Time.timeScale = 0f;
        }
    }

    private void PlayClearAnimation(Transform player)
    {
        if (string.IsNullOrEmpty(clearAnimationTriggerName))
        {
            return;
        }

        Animator animator = player.GetComponentInChildren<Animator>();
        if (animator == null)
        {
            Debug.LogWarning($"{name}: '{player.name}' 하위에서 Animator를 찾지 못해 도착 애니메이션을 재생할 수 없습니다.", this);
            return;
        }

        animator.SetTrigger(clearAnimationTriggerName);
    }

    // 1번 버튼: 다음 스테이지 씬으로 이동.
    public void GoNextStage()
    {
        LoadScene(nextSceneName, "다음 스테이지");
    }

    // 2번 버튼: 현재 스테이지 씬 재시작.
    public void RestartStage()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // 3번 버튼: 스테이지 선택 씬으로 이동.
    public void GoStageSelect()
    {
        LoadScene(stageSelectSceneName, "스테이지 선택");
    }

    // 4번 버튼: 게임 종료.
    public void QuitGame()
    {
        Time.timeScale = 1f;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void LoadScene(string sceneName, string label)
    {
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

// GoalPanel과 콜라이더가 서로 다른 오브젝트에 있을 때 트리거 이벤트를 GoalPanel로 넘긴다.
[DisallowMultipleComponent]
internal class GoalPanelTriggerRelay : MonoBehaviour
{
    [SerializeField] private GoalPanel owner;

    public void SetOwner(GoalPanel newOwner)
    {
        owner = newOwner;
    }

    private void OnTriggerEnter(Collider other)
    {
        owner?.RelayTriggerEnter(other);
    }

    private void OnTriggerStay(Collider other)
    {
        owner?.RelayTriggerStay(other);
    }
}
