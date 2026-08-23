using UnityEngine;
using System.Collections;

public class PlayerAudio : MonoBehaviour
{
    [Header("Audio Source")]
    [Tooltip("비워두면 이 오브젝트/자식에서 AudioSource를 자동으로 찾는다")]
    [SerializeField] private AudioSource audioSource;

    [Header("Audio Clips")]
    [SerializeField] private AudioClip footstepClip;
    [SerializeField] private AudioClip interactionClip;
    [SerializeField] private AudioClip deathClip;

    [Header("SFX Stop Timing")]
    [Tooltip("조합 성공 사운드를 재생한 뒤 이 시간 후 AudioSource를 멈춘다. 0 이하면 자동으로 멈추지 않는다")]
    [SerializeField] private float interactionStopDelaySeconds = 0.5f;

    private IPlayerKillable killable;
    private Coroutine interactionStopRoutine;

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = GetComponentInChildren<AudioSource>();
        }

        FindKillable();
    }

    public void PlayFootstep()
    {
        // 발소리는 AudioSource의 Play On Awake / Loop 설정으로 처리한다.
        // 애니메이션 이벤트에 남아 있는 PlayFootstep 호출은 중복 재생을 막기 위해 무시한다.
    }

    public void PlayInteractionSound()
    {
        CancelInteractionStop();
        StopLoopingSourceIfNeeded();
        PlayOneShot(interactionClip, "조합 성공");

        if (interactionStopDelaySeconds > 0f)
        {
            interactionStopRoutine = StartCoroutine(StopInteractionAfterDelay());
        }
    }

    public void PlayDeathSound()
    {
        CancelInteractionStop();
        StopLoopingSourceIfNeeded();
        PlayOneShot(deathClip, "사망");
    }

    public void RestartFootstepLoop()
    {
        CancelInteractionStop();

        if (IsDead())
        {
            Debug.Log("[PlayerAudio] 플레이어가 사망 상태라 걷는 사운드를 다시 재생하지 않습니다.", this);
            return;
        }

        if (audioSource == null)
        {
            Debug.LogWarning("[PlayerAudio] AudioSource가 없어 걷는 사운드를 다시 재생할 수 없습니다.", this);
            return;
        }

        if (footstepClip != null)
        {
            audioSource.clip = footstepClip;
        }

        if (audioSource.clip == null)
        {
            Debug.LogWarning("[PlayerAudio] AudioSource에 걷는 AudioClip이 없어 걷는 사운드를 다시 재생할 수 없습니다.", this);
            return;
        }

        audioSource.loop = true;

        if (!audioSource.isPlaying)
        {
            audioSource.Play();
        }

        Debug.Log($"[PlayerAudio] 걷는 사운드 재시작: {audioSource.clip.name}", this);
    }

    private IEnumerator StopInteractionAfterDelay()
    {
        yield return new WaitForSeconds(interactionStopDelaySeconds);

        if (audioSource != null)
        {
            audioSource.Stop();
            Debug.Log($"[PlayerAudio] 조합 성공 사운드를 {interactionStopDelaySeconds:0.###}초 후 정지했습니다.", this);
        }

        interactionStopRoutine = null;
    }

    private void CancelInteractionStop()
    {
        if (interactionStopRoutine == null)
        {
            return;
        }

        StopCoroutine(interactionStopRoutine);
        interactionStopRoutine = null;
    }

    private void PlayOneShot(AudioClip clip, string label)
    {
        if (audioSource == null)
        {
            Debug.LogWarning($"[PlayerAudio] AudioSource가 없어 {label} 사운드를 재생할 수 없습니다.", this);
            return;
        }

        if (clip == null)
        {
            Debug.LogWarning($"[PlayerAudio] {label} AudioClip이 비어 있어 사운드를 재생할 수 없습니다.", this);
            return;
        }

        Debug.Log($"[PlayerAudio] {label} 사운드 재생: {clip.name}", this);
        audioSource.PlayOneShot(clip);
    }

    private void StopLoopingSourceIfNeeded()
    {
        if (audioSource != null && audioSource.loop)
        {
            audioSource.Stop();
        }
    }

    private bool IsDead()
    {
        if (killable == null)
        {
            FindKillable();
        }

        return killable != null && killable.IsDead;
    }

    private void FindKillable()
    {
        killable = GetComponentInParent<IPlayerKillable>();
        if (killable == null)
        {
            killable = GetComponentInChildren<IPlayerKillable>();
        }
    }
}
