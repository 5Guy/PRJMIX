using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// 원소 함정의 감지 오브젝트(예: 큐브 위에 얹는 불기둥)에 붙이는 스크립트.
// Assets/Data의 ElementData(Fire, Water, Wood, Dart, Iron)를 참조해서 어떤 원소 함정인지 식별한다.
// Player 태그를 가진 오브젝트가 트리거에 닿으면 연출 애니메이션을 재생하고,
// 아이템 등으로 인한 면역(IElementImmune) 여부를 확인한 뒤 면역이 아니면 사망 이벤트를 발행한다.
// 실제 사망 처리(씬 재시작, UI 등)는 이 이벤트를 구독하는 별도 시스템(GameManager 등)에서 담당한다.
[RequireComponent(typeof(Collider))]
public class ElementTrapCube : MonoBehaviour, IElementCounterTrap
{
    [Header("원소 정보")]
    [SerializeField] private ElementData elementData;

    [Header("연출")]
    [SerializeField] private Animator animator;
    [SerializeField] private string animationTriggerName = "Activate";

    [Header("사망 연출")]
    [SerializeField] private string deathAnimationTriggerName = "Die";

    [Header("상쇄")]
    [Tooltip("이 원소를 함정 칸에 배치하면 함정이 꺼진다 (예: 불 함정 ↔ 물)")]
    [SerializeField] private List<ElementData> counterElements = new List<ElementData>();
    [Tooltip("두 개 이상 올려야 꺼지는 원소. 여기에 적지 않은 원소는 하나만 올리면 꺼진다\n" +
             "(예: 진흙 = 2 → 함정 칸에 진흙을 두 번 올려야 덮인다)")]
    [SerializeField] private List<ElementCounterCharge> counterCharges = new List<ElementCounterCharge>();
    [Tooltip("상쇄될 때 끌 연출의 뿌리. 비우면 자기 자신")]
    [SerializeField] private Transform effectRoot;
    [Tooltip("연출 오브젝트를 통째로 끈다. 끄면 파티클이 서서히 사그라들 뿐 불꽃이 한동안 남는다")]
    [SerializeField] private bool deactivateEffect = true;
    [Tooltip("원소를 올려 두기만 하고, 시작 버튼을 눌러야 실제로 꺼진다")]
    [SerializeField] private bool waitForStageStart = true;

    [Header("상쇄 발동 시점")]
    [Tooltip("켜면 시작 버튼이 아니라, 플레이어가 함정 앞 트리거를 지나갈 때 꺼진다. " +
             "버튼 한 번에 맵의 함정이 모두 동시에 사라지지 않는다")]
    [SerializeField] private bool counterOnPlayerApproach = true;
    [Tooltip("직접 만들어 둔 접근 트리거. 비워두면 함정 앞에 자동으로 만든다")]
    [SerializeField] private TrapApproachZone approachZone;
    [Tooltip("플레이어가 오는 쪽으로 함정에서 이만큼 떨어진 곳에 트리거를 둔다(미터)")]
    [SerializeField] private float approachDistance = 3.5f;
    [Tooltip("자동으로 만들 트리거 상자의 크기(미터). x=도로를 가로지르는 폭, y=높이, z=두께")]
    [SerializeField] private Vector3 approachZoneSize = new Vector3(6f, 3f, 2f);

    [Header("이벤트")]
    [SerializeField] private UnityEvent<GameObject> onPlayerKilled;
    [SerializeField] private UnityEvent onCountered;

    [Header("사운드")]
    [Tooltip("플레이어가 함정에 걸렸을 때 울릴 소리")]
    [SerializeField] private AudioClip killSound;
    [Range(0f, 1f)]
    [SerializeField] private float killVolume = 1f;
    [Tooltip("원소에 상쇄되어 함정이 꺼질 때 울릴 소리")]
    [SerializeField] private AudioClip counterSound;
    [Range(0f, 1f)]
    [SerializeField] private float counterVolume = 1f;

    private Collider trapCollider;
    private bool countered;
    private ElementData armedElement;
    private int armedCount;
    private bool effectWasActive = true;
    private bool createdApproachZone;

    public ElementData ElementData => elementData;
    public bool IsCountered => countered;

    // 이 함정의 "보이는 모습" 전체를 담은 뿌리.
    //
    // 불꽃·연기는 보통 이 감지 오브젝트의 형제로 붙어 있어서, 자기 하위만 봐서는 함정의 모습을 다 찾지 못한다.
    // 탑뷰에서 3D 모습을 감출 때(TrapTopViewIcon) 이 뿌리를 기준으로 삼는다.
    public Transform VisualRoot => effectRoot != null ? effectRoot : transform;

    // 필요한 개수까지 다 올려 두어 발동을 기다리는 상태.
    // 진흙 2개짜리 함정에 하나만 올려 둔 동안은 아직 false다(플레이어가 지나가도 꺼지지 않는다).
    public bool IsArmed => armedElement != null && armedCount >= RequiredFor(armedElement) && !countered;

    // 올려 두기만 하고 아직 개수가 안 찬 상태도 포함해서, 무언가 올라가 있는지.
    private bool HasCharge => armedElement != null && armedCount > 0 && !countered;

    // 이 원소로 함정을 끄는 데 필요한 개수.
    private int RequiredFor(ElementData data)
    {
        return ElementCounterCharge.RequiredFor(counterCharges, data);
    }

    // 실제로 꺼진 순간. 올려 둔 원소 표시를 함께 치우려고 PlacementSystem이 구독한다.
    public event System.Action Countered;

    // 조합창에서 끌어온 원소가 이 함정을 끌 수 있는지.
    public bool CanBeCounteredBy(ElementData data)
    {
        if (countered || data == null || !counterElements.Contains(data))
        {
            return false;
        }

        // 아무것도 안 올라가 있으면 당연히 올릴 수 있다.
        if (armedElement == null)
        {
            return true;
        }

        // 같은 원소라면 필요한 개수를 채울 때까지 더 올릴 수 있다(진흙 2개처럼).
        // 다른 원소는 먼저 올려 둔 것을 물려야 한다.
        return armedElement == data && armedCount < RequiredFor(data);
    }

    // 올려 둔 원소의 모습은 그 원소를 맡은 상쇄 연출이 정한다(바람 = 흰 폭풍).
    // 맡은 연출이 없거나 따로 보여 줄 것이 없으면 null이고, 그때는 기본 구가 놓인다.
    public Transform GetCounterVisual(ElementData data)
    {
        TrapCounterEffect effect = TrapCounterEffect.Find(this, data);
        return effect != null ? effect.CreatePlacedVisual(transform.position) : null;
    }

    // 원소를 올리는 데 성공하면 true. PlacementSystem이 함정 칸에 원소를 놓을 때 호출한다.
    // 실제로 꺼지는 시점은 플레이어가 함정 앞을 지나갈 때(기본) 또는 시작 버튼을 누를 때다.
    public bool TryCounter(ElementData data)
    {
        if (!CanBeCounteredBy(data))
        {
            return false;
        }

        // 같은 원소를 더 올리는 중이면 개수만 늘린다.
        armedCount = armedElement == data ? armedCount + 1 : 1;
        armedElement = data;

        // 아직 개수가 모자라면 여기서 끝. 플레이어가 지나가도 꺼지지 않는다.
        int required = RequiredFor(data);
        if (armedCount < required)
        {
            Debug.Log($"[상쇄 1/3] {name}: '{data.ElementName}' {armedCount}/{required}개를 올려 두었습니다. " +
                      $"{required - armedCount}개 더 올려야 함정이 꺼집니다.", this);
            return true;
        }

        // 함정 앞을 지나갈 때 꺼지는 방식이면, 여기서는 올려 두기만 한다.
        if (counterOnPlayerApproach)
        {
            Debug.Log($"[상쇄 1/3] {name}: '{data.ElementName}' {armedCount}/{required}개를 올려 두었습니다. " +
                      "출발한 뒤 플레이어가 함정 앞 트리거를 지나가면 연출이 시작됩니다.", this);
            return true;
        }

        // 시작 버튼이 없는 씬이거나 이미 출발했다면 기다릴 이유가 없다.
        if (!waitForStageStart || !StageStartButton.Exists || StageStartButton.HasStarted)
        {
            ApplyCounter();
        }

        return true;
    }

    // 아직 꺼지기 전이라면 올려 둔 원소를 하나 물린다. 배치한 원소를 회수할 때 쓴다.
    //
    // 여러 개를 올려 두었으면 한 번에 하나씩만 물린다.
    // 맵에 놓인 표시도 올려 둔 개수만큼 있으므로, 표시 하나에 원소 하나가 맞아떨어진다.
    public bool CancelCounter()
    {
        if (!HasCharge)
        {
            return false;
        }

        armedCount--;

        if (armedCount <= 0)
        {
            armedCount = 0;
            armedElement = null;
        }

        return true;
    }

    private void OnEnable()
    {
        StageStartButton.StageStarted += HandleStageStarted;
    }

    private void OnDisable()
    {
        StageStartButton.StageStarted -= HandleStageStarted;
    }

    // 상쇄되면 연출 오브젝트를 통째로 끄기 때문에 OnEnable/OnDisable로는 복구 신호를 받을 수 없다.
    // 꺼져 있어도 신호가 오도록 수명 전체에 걸쳐 구독한다.
    private void OnDestroy()
    {
        StageReset.Requested -= HandleStageReset;

        // 자동으로 만든 트리거는 뿌리에 따로 서 있어서 같이 지워지지 않는다.
        if (createdApproachZone && approachZone != null)
        {
            Destroy(approachZone.gameObject);
        }
    }

    private void HandleStageStarted()
    {
        // 함정 앞 트리거를 쓰는 함정은 출발한다고 꺼지지 않는다. 플레이어가 다가올 때까지 기다린다.
        if (IsArmed && !counterOnPlayerApproach)
        {
            ApplyCounter();
        }
    }

    // 함정 앞 트리거(TrapApproachZone)가 부른다.
    // 원소를 올려 두지 않았으면 아무 일도 일어나지 않는다 — 함정은 그대로 살아 있다.
    public void NotifyPlayerApproached()
    {
        if (!IsArmed)
        {
            return;
        }

        Debug.Log($"[상쇄 2/3] {name}: 플레이어가 함정 앞을 지나갔습니다. '{armedElement.ElementName}' 연출을 시작합니다.", this);
        ApplyCounter();
    }

    // 시작 버튼을 누를 때 / 죽어서 리스폰할 때 함정을 처음 상태로 되돌린다.
    // 시작 버튼 쪽은 StageStarted보다 먼저 오므로, 이번 판에 올려 둔 원소는 복구 뒤에 정상적으로 함정을 끈다.
    private void HandleStageReset(StageReset.Reason reason)
    {
        ResetToOriginal(reason == StageReset.Reason.PlayerDeath);
    }

    // clearArmed가 true면 올려 둔 원소까지 물려서 완전히 처음 상태로 만든다.
    public void ResetToOriginal(bool clearArmed)
    {
        if (clearArmed)
        {
            // 이미 상쇄에 써 버린 원소(예: 불 함정을 끈 물)는 맵에서 표시까지 사라진 상태라
            // PlacementSystem이 회수해 줄 수 없다. 함정이 직접 조합창으로 돌려준다.
            // 아직 올려 두기만 한 원소는 표시가 남아 있으므로 그쪽에서 회수한다.
            // 여러 개를 부어 껐다면(진흙 2개처럼) 쓴 만큼 다 돌려준다.
            if (countered && armedElement != null)
            {
                for (int i = 0; i < Mathf.Max(1, armedCount); i++)
                {
                    CraftingPanelUI.ReturnToPanel(armedElement);
                }
            }

            armedElement = null;
            armedCount = 0;
        }

        if (!countered)
        {
            return;
        }

        countered = false;

        Transform root = effectRoot != null ? effectRoot : transform;

        // ApplyCounter에서 통째로 껐다면 다시 켠다.
        if (deactivateEffect)
        {
            root.gameObject.SetActive(effectWasActive);
        }

        if (trapCollider != null)
        {
            trapCollider.enabled = true;
        }

        foreach (ParticleSystem particles in root.GetComponentsInChildren<ParticleSystem>())
        {
            particles.Clear(true);
            particles.Play(true);
        }

        foreach (AudioSource audio in root.GetComponentsInChildren<AudioSource>())
        {
            if (audio.clip != null && audio.playOnAwake)
            {
                audio.Play();
            }
        }
    }

    private void ApplyCounter()
    {
        ElementData data = armedElement;
        countered = true;

        // 판정은 연출을 기다리지 않고 바로 끈다.
        // 파도에 실려 함정 위로 지나가는 동안 죽어 버리면 안 된다.
        if (trapCollider != null)
        {
            trapCollider.enabled = false;
        }

        // 원소마다 다른 상쇄 연출이 붙어 있으면 먼저 재생하고, 연출이 알려 준 시간만큼 기다렸다가 끈다.
        // (연출은 함정이 꺼지면서 통째로 비활성화되어도 끊기지 않도록 CounterEffectRunner 위에서 돈다)
        TrapCounterEffect effect = TrapCounterEffect.Find(this, data);
        if (effect == null)
        {
            Debug.LogWarning(
                $"[상쇄 3/3] {name}: '{data?.ElementName}'({data?.ElementType}) 전용 연출을 찾지 못해 불만 끕니다. " +
                $"함정 프리팹({transform.root.name})에 이 원소를 맡은 TrapCounterEffect가 붙어 있는지, " +
                "그 컴포넌트의 '대상 원소'가 올려 둔 원소와 같은지 확인하세요.", this);
        }

        if (effect != null)
        {
            float delay = effect.Play(this);
            Debug.Log($"[상쇄 3/3] {name}: {effect.GetType().Name} 재생. {delay:0.00}초 뒤에 불이 꺼집니다.", this);

            if (delay > 0f)
            {
                CounterEffectRunner.Run(ExtinguishAfter(delay));
                return;
            }
        }

        Extinguish();
    }

    private IEnumerator ExtinguishAfter(float delay)
    {
        yield return new WaitForSeconds(delay);

        // 기다리는 사이에 죽어서 함정이 되살아났다면 그냥 둔다.
        if (countered)
        {
            Extinguish();
        }
    }

    // 실제로 불을 끄는 부분. 연출이 있으면 연출이 끝나는 시점에 불린다.
    private void Extinguish()
    {
        // 불꽃·연기 파티클은 보통 이 감지 오브젝트의 형제로 붙어 있어서,
        // 자기 하위만 끄면 판정만 사라지고 불은 계속 타 보인다. 연출 뿌리 전체를 끈다.
        Transform root = effectRoot != null ? effectRoot : transform;

        foreach (ParticleSystem particles in root.GetComponentsInChildren<ParticleSystem>())
        {
            // 이미 나온 입자는 자연스럽게 사그라들게 두고 새로 뿜는 것만 멈춘다.
            particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        foreach (AudioSource audio in root.GetComponentsInChildren<AudioSource>())
        {
            audio.Stop();
        }

        SfxPlayer.PlayAt(counterSound, transform.position, counterVolume);
        onCountered?.Invoke();
        Countered?.Invoke();

        // Stop()은 새로 뿜는 것만 막기 때문에 이미 떠 있는 불꽃이 수명만큼 계속 보인다.
        // 확실히 꺼진 것처럼 보이도록 연출 오브젝트를 통째로 끈다.
        // (이 오브젝트 자신이 뿌리일 수도 있으므로 반드시 마지막에 한다)
        if (deactivateEffect)
        {
            root.gameObject.SetActive(false);
        }
    }

    private void Awake()
    {
        StageReset.Requested += HandleStageReset;

        Transform root = effectRoot != null ? effectRoot : transform;
        effectWasActive = root.gameObject.activeSelf;

        trapCollider = GetComponent<Collider>();
        if (!trapCollider.isTrigger)
        {
            Debug.LogWarning($"{name}: ElementTrapCube의 Collider는 IsTrigger가 켜져 있어야 합니다. 자동으로 켭니다.", this);
            trapCollider.isTrigger = true;
        }
    }

    // 플레이어(월드는 StageWorldBuilder가 Awake에서 만든다)를 찾아야 하므로 Start에서 만든다.
    private void Start()
    {
        EnsureApproachZone();
    }

    // 함정 앞, 플레이어가 오는 쪽에 트리거 상자를 하나 세운다.
    private void EnsureApproachZone()
    {
        if (!counterOnPlayerApproach)
        {
            return;
        }

        if (approachZone == null)
        {
            approachZone = GetComponentInChildren<TrapApproachZone>(true);
        }

        // 씬에 직접 만들어 둔 트리거가 있으면 그대로 쓴다.
        if (approachZone != null)
        {
            approachZone.Setup(this);
            return;
        }

        approachZone = TrapApproachZone.Build(this, transform, approachDistance, approachZoneSize);
        createdApproachZone = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        HandlePlayerContact(other);
    }

    // 빠르게 지나가 OnTriggerEnter를 놓치는 경우를 대비해 머무는 동안에도 확인한다.
    // 이미 사망한 플레이어는 IPlayerKillable 쪽에서 걸러지므로 중복 처리되지 않는다.
    private void OnTriggerStay(Collider other)
    {
        HandlePlayerContact(other);
    }

    private void HandlePlayerContact(Collider other)
    {
        if (countered)
        {
            return;
        }

        Transform player = PlayerLocator.FindPlayerRoot(other.transform);
        if (player == null || PlayerLocator.IsAlreadyDead(player))
        {
            return;
        }

        PlayTrapAnimation();

        if (IsPlayerImmune(other))
        {
            return;
        }

        // 사망 연출 + 감속/리스폰 반응은 공통 로직에 맡긴다.
        if (!PlayerLocator.Kill(player, deathAnimationTriggerName, this))
        {
            return;
        }

        ElementalKillEffect.Play(this, player);
        SfxPlayer.PlayAt(killSound, transform.position, killVolume);
        onPlayerKilled?.Invoke(player.gameObject);
    }

    private bool IsPlayerImmune(Collider player)
    {
        if (elementData == null)
        {
            return false;
        }

        IElementImmune immunity = player.GetComponentInParent<IElementImmune>();
        return immunity != null && immunity.IsImmuneTo(elementData.ElementType);
    }

    private void PlayTrapAnimation()
    {
        if (animator == null || string.IsNullOrEmpty(animationTriggerName))
        {
            return;
        }

        animator.SetTrigger(animationTriggerName);
    }
}
