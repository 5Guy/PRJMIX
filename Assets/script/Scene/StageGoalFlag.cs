using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

// 맵 끝에 세우는 도착 깃발.
// Player 태그를 가진 오브젝트가 트리거에 닿으면 걷기를 멈추고 잠시 뒤 다음 씬으로 넘어간다.
// 넘어갈 씬은 인스펙터의 '다음 씬'에 씬 파일을 끌어다 놓으면 된다(빌드 세팅에도 들어 있어야 한다).
[RequireComponent(typeof(Collider))]
public class StageGoalFlag : MonoBehaviour
{
#if UNITY_EDITOR
    [Header("다음 씬")]
    [Tooltip("여기에 씬 파일을 끌어다 놓으면 아래 씬 이름이 자동으로 채워진다")]
    [SerializeField] private UnityEditor.SceneAsset nextScene;
#endif

    [Tooltip("실제로 불러올 씬 이름. 위에 씬 파일을 넣으면 자동으로 채워진다")]
    [SerializeField] private string nextSceneName;

    [Header("도착 처리")]
    [Tooltip("도착하면 플레이어의 자동 걷기를 멈춘다")]
    [SerializeField] private bool stopPlayerOnReach = true;
    [Tooltip("씬을 넘기기 전까지 기다리는 시간(초). 연출 시간을 벌 때 쓴다")]
    [SerializeField] private float sceneChangeDelay = 1f;
    [Tooltip("도착 시 플레이어 Animator에 걸 트리거 이름. 비우면 재생하지 않는다")]
    [SerializeField] private string clearAnimationTriggerName = "";

    [Header("연출")]
    [Tooltip("도착 시 재생할 파티클/사운드 등의 뿌리. 비우면 아무것도 하지 않는다")]
    [SerializeField] private GameObject reachEffect;

    [Header("이벤트")]
    [SerializeField] private UnityEvent<GameObject> onPlayerReached;

    private Collider goalCollider;
    private bool reached;

    public bool Reached => reached;

    private void Awake()
    {
        goalCollider = GetComponent<Collider>();
        if (!goalCollider.isTrigger)
        {
            Debug.LogWarning($"{name}: StageGoalFlag의 Collider는 IsTrigger가 켜져 있어야 합니다. 자동으로 켭니다.", this);
            goalCollider.isTrigger = true;
        }

        if (reachEffect != null)
        {
            reachEffect.SetActive(false);
        }

        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.LogWarning($"{name}: 넘어갈 씬이 비어 있어 도착해도 씬이 바뀌지 않습니다.", this);
        }
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        // 씬 파일을 넣어 두면 이름을 손으로 적지 않아도 되게 동기화한다.
        if (nextScene != null)
        {
            nextSceneName = nextScene.name;
        }
#endif
        if (sceneChangeDelay < 0f)
        {
            sceneChangeDelay = 0f;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        HandlePlayerContact(other);
    }

    // 빠르게 지나가 OnTriggerEnter를 놓치는 경우를 대비해 머무는 동안에도 확인한다.
    private void OnTriggerStay(Collider other)
    {
        HandlePlayerContact(other);
    }

    private void HandlePlayerContact(Collider other)
    {
        if (reached)
        {
            return;
        }

        Transform player = PlayerLocator.FindPlayerRoot(other.transform);
        if (player == null || PlayerLocator.IsAlreadyDead(player))
        {
            return;
        }

        reached = true;

        if (stopPlayerOnReach)
        {
            PlayerAutoWalker walker = player.GetComponentInChildren<PlayerAutoWalker>();
            if (walker != null)
            {
                walker.StopWalking();
            }
        }

        PlayClearAnimation(player);

        if (reachEffect != null)
        {
            reachEffect.SetActive(true);
        }

        onPlayerReached?.Invoke(player.gameObject);

        StartCoroutine(LoadNextSceneRoutine());
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

    private IEnumerator LoadNextSceneRoutine()
    {
        // 시간 조작 시스템이 timeScale을 건드려도 대기 시간이 늘어나지 않도록 실시간으로 센다.
        yield return new WaitForSecondsRealtime(sceneChangeDelay);

        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.LogError($"{name}: 넘어갈 씬 이름이 비어 있어 씬을 전환하지 못했습니다.", this);
            yield break;
        }

        if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.LogError($"{name}: '{nextSceneName}' 씬이 빌드 세팅(File > Build Settings)에 없습니다. 먼저 추가해 주세요.", this);
            yield break;
        }

        SceneManager.LoadScene(nextSceneName);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.35f);
        Collider preview = goalCollider != null ? goalCollider : GetComponent<Collider>();
        if (preview != null)
        {
            Bounds bounds = preview.bounds;
            Gizmos.DrawCube(bounds.center, bounds.size);
            Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 1f);
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }
}
