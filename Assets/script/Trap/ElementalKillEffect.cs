using System.Collections;
using UnityEngine;

// 함정에 죽었을 때의 화면 연출을 한 곳에서 맡는 컴포넌트.
//
// 모든 함정은 사망 처리 직후 ElementalKillEffect.Play(this, player)를 부른다.
// 함정(또는 그 부모/자식)에 이 컴포넌트가 붙어 있으면 그 설정대로,
// 붙어 있지 않으면 기본 설정대로 같은 연출이 나간다.
// 스테이지마다 연출을 다르게 하고 싶을 때만 프리팹에 붙여서 값을 조절하면 된다.
//
// 연출 자체는 함정에서 떼어 낸 임시 오브젝트에서 돌린다.
// 늪 웅덩이나 파도처럼 사망과 함께 치워지는 함정에서도 연출이 중간에 끊기지 않는다.
public class ElementalKillEffect : MonoBehaviour
{
    [Header("시스템 다운 커튼 연출")]
    [Tooltip("켜져 있으면 검은 커튼이 화면을 덮었다가, 잠시 대기 후 걷히는 연출을 보여준다")]
    [SerializeField] private bool showSystemDown = true;
    [Tooltip("사망 애니메이션 후 커튼이 시작되기 전까지 기다리는 시간(초)")]
    [SerializeField] private float delayAfterDeath = 0.2f;
    [Tooltip("검은색 커튼이 양쪽에서 펼쳐져 화면을 완전히 덮는 시간(초)")]
    [SerializeField] private float curtainCloseDuration = 0.35f;
    [Tooltip("화면이 검은색으로 완전히 채워진 상태로 대기하는 시간(초)")]
    [SerializeField] private float closedHoldDuration = 1.5f;
    [Tooltip("검은색 커튼이 걷어져 화면이 다시 보이기까지 걸리는 시간(초)")]
    [SerializeField] private float curtainOpenDuration = 0.2f;
    [SerializeField] private string systemDownMessage = "SYSTEM DOWN";

    [Header("실패 패널")]
    [Tooltip("커튼 연출과 리스폰이 끝난 뒤 StageFailPanel을 띄운다")]
    [SerializeField] private bool showFailPanelAfterRespawn = true;

    // 리스폰이 어떤 이유로든 오지 않을 때 실패 패널을 영영 잃지 않도록 두는 한도(초).
    private const float MaxRespawnWait = 10f;

    // 임시 오브젝트에서 돌고 있는 사본인지. 연출이 끝나면 스스로 치운다.
    private bool isRunner;

    // 함정이 부르는 진입점. 사망 처리(PlayerLocator.Kill)가 성공한 직후에 부른다.
    public static void Play(Component trap, Transform player)
    {
        ElementalKillEffect settings = Find(trap);

        GameObject host = new GameObject("ElementalKillEffect (Runner)");
        ElementalKillEffect runner = host.AddComponent<ElementalKillEffect>();
        runner.isRunner = true;

        if (settings != null)
        {
            runner.CopySettingsFrom(settings);
        }

        runner.Play(player);
    }

    // 함정 자신 -> 부모 -> 자식 순으로 설정을 찾는다. 없으면 기본값을 쓴다.
    private static ElementalKillEffect Find(Component trap)
    {
        if (trap == null)
        {
            return null;
        }

        ElementalKillEffect effect = trap.GetComponent<ElementalKillEffect>();
        if (effect == null)
        {
            effect = trap.GetComponentInParent<ElementalKillEffect>();
        }

        if (effect == null)
        {
            effect = trap.GetComponentInChildren<ElementalKillEffect>();
        }

        return effect;
    }

    private void CopySettingsFrom(ElementalKillEffect other)
    {
        showSystemDown = other.showSystemDown;
        delayAfterDeath = other.delayAfterDeath;
        curtainCloseDuration = other.curtainCloseDuration;
        closedHoldDuration = other.closedHoldDuration;
        curtainOpenDuration = other.curtainOpenDuration;
        systemDownMessage = other.systemDownMessage;
        showFailPanelAfterRespawn = other.showFailPanelAfterRespawn;
    }

    public void Play(Transform player)
    {
        if (showSystemDown)
        {
            SystemDownScreen.ShowCurtain(
                systemDownMessage,
                delayAfterDeath,
                curtainCloseDuration,
                closedHoldDuration,
                curtainOpenDuration);
        }

        if (!showFailPanelAfterRespawn)
        {
            DestroyRunner();
            return;
        }

        StopAllCoroutines();
        StartCoroutine(ShowFailPanelAfterRespawnRoutine(player));
    }

    private IEnumerator ShowFailPanelAfterRespawnRoutine(Transform player)
    {
        float curtainTotalDuration = showSystemDown
            ? Mathf.Max(0f, delayAfterDeath)
              + Mathf.Max(0.01f, curtainCloseDuration)
              + Mathf.Max(0f, closedHoldDuration)
              + Mathf.Max(0.01f, curtainOpenDuration)
            : 0f;

        if (curtainTotalDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(curtainTotalDuration);
        }

        // 리스폰이 끝나 플레이어가 다시 살아난 뒤에 패널을 띄운다.
        // 패널이 뜨면 timeScale이 0으로 내려가므로 대기는 실시간으로 센다.
        IPlayerKillable killable = player != null ? player.GetComponentInChildren<IPlayerKillable>() : null;
        float waited = 0f;
        while (killable != null && killable.IsDead && waited < MaxRespawnWait)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        StageFailPanel.ShowAnyPanel();
        DestroyRunner();
    }

    private void DestroyRunner()
    {
        if (isRunner)
        {
            Destroy(gameObject);
        }
    }
}
