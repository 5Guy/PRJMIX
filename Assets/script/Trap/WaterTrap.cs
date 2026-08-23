using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// WaterTrap 부모 아래의 "스크립트용 빈 오브젝트"에 붙이는 관리 스크립트.
// 실제 판정은 별도 모델 오브젝트의 CapsuleCollider를 연결해서 사용한다.
//
// 파훼(상쇄)는 불 함정(ElementTrapCube)과 똑같은 흐름으로 돈다.
//   1) 조합창에서 끌어온 원소를 물 위에 올려 둔다 (TryCounter → IsArmed)
//   2) 플레이어가 함정 앞 트리거를 지나가면 물이 걷히고 길이 열린다 (IsCountered)
// 물을 건너게 해 주는 원소만 받는다 — 용암(굳혀서 밟고 지나감), 나무다리(나무+도구), 시멘트(진흙+재).
public class WaterTrap : MonoBehaviour, IElementCounterTrap
{
    [Header("판정 콜라이더")]
    [Tooltip("WaterTrapModel처럼 실제 물 모델에 붙어 있는 CapsuleCollider")]
    [SerializeField] private CapsuleCollider trapCollider;

    [Header("사망 연출")]
    [SerializeField] private string deathAnimationTriggerName = "Die";
    [Tooltip("물 함정에 죽은 뒤 플레이어 콜라이더를 Trigger로 바꿔 바닥 아래로 빠지게 한다")]
    [SerializeField] private bool dropPlayerThroughGround = true;

    [Header("원소 면역")]
    [Tooltip("IElementImmune을 구현한 플레이어가 Water 면역이면 사망하지 않는다")]
    [SerializeField] private bool respectWaterImmunity = true;

    [Header("파훼")]
    [Tooltip("이 원소를 물 위에 놓으면 물이 걷힌다 (용암 / 나무다리 / 시멘트)")]
    [SerializeField] private List<ElementData> counterElements = new List<ElementData>();
    [Tooltip("두 개 이상 올려야 걷히는 원소. 여기에 적지 않은 원소는 하나만 올리면 걷힌다\n" +
             "(예: 진흙 = 2 → 물 위에 진흙을 두 번 부어야 메워진다)")]
    [SerializeField] private List<ElementCounterCharge> counterCharges = new List<ElementCounterCharge>();
    [Tooltip("파훼될 때 치울 물 연출의 뿌리. 비우면 판정 콜라이더가 붙은 모델 오브젝트")]
    [SerializeField] private Transform effectRoot;
    [Tooltip("연출 오브젝트를 통째로 끈다. 끄면 판정만 사라지고 물은 그대로 보인다")]
    [SerializeField] private bool deactivateEffect = true;
    [Tooltip("원소를 올려 두기만 하고, 시작 버튼을 눌러야 실제로 걷힌다")]
    [SerializeField] private bool waitForStageStart = true;

    [Header("파훼 발동 시점")]
    [Tooltip("켜면 시작 버튼이 아니라, 플레이어가 함정 앞 트리거를 지나갈 때 걷힌다. " +
             "버튼 한 번에 맵의 함정이 모두 동시에 사라지지 않는다")]
    [SerializeField] private bool counterOnPlayerApproach = true;
    [Tooltip("직접 만들어 둔 접근 트리거. 비워두면 함정 앞에 자동으로 만든다")]
    [SerializeField] private TrapApproachZone approachZone;
    [Tooltip("플레이어가 오는 쪽으로 함정에서 이만큼 떨어진 곳에 트리거를 둔다(미터)")]
    [SerializeField] private float approachDistance = 3.5f;
    [Tooltip("자동으로 만들 트리거 상자의 크기(미터). x=도로를 가로지르는 폭, y=높이, z=두께")]
    [SerializeField] private Vector3 approachZoneSize = new Vector3(6f, 3f, 2f);

    [Header("흑요석 웅덩이 (용암으로 파훼할 때)")]
    [Tooltip("용암으로 파훼하면 물이 걷힌 자리에 흑요석 웅덩이가 차올라 건너갈 길이 된다")]
    [SerializeField] private bool buildObsidianPool = true;
    [Tooltip("이 종류의 원소로 파훼했을 때만 웅덩이가 차오른다")]
    [SerializeField] private ElementType obsidianPoolElement = ElementType.Lava;
    [Tooltip("비워두면 물 모델과 똑같은 크기의 원기둥을 만든다")]
    [SerializeField] private GameObject obsidianPoolPrefab;
    [SerializeField] private Color obsidianPoolColor = new Color(0.09f, 0.07f, 0.13f, 1f);
    [Tooltip("흑요석은 유리질이라 매끈하게 빛난다")]
    [Range(0f, 1f)]
    [SerializeField] private float obsidianPoolSmoothness = 0.85f;
    [Tooltip("웅덩이가 다 차오르는 데 걸리는 시간(초)")]
    [SerializeField] private float obsidianPoolRiseDuration = 0.6f;
    [Tooltip("용암이 떨어지기 시작하는 높이(미터)")]
    [SerializeField] private float lavaDropHeight = 9f;
    [Tooltip("용암이 물에 닿기까지 걸리는 시간(초)")]
    [SerializeField] private float lavaDropDuration = 0.5f;
    [ColorUsage(true, true)]
    [SerializeField] private Color lavaDropColor = new Color(1f, 0.35f, 0.06f, 1f);
    [Tooltip("용암이 물에 떨어질 때 울릴 소리")]
    [SerializeField] private AudioClip lavaDropSound;
    [Range(0f, 1f)]
    [SerializeField] private float lavaDropVolume = 1f;
    [Tooltip("웅덩이가 차오를 때 울릴 소리")]
    [SerializeField] private AudioClip obsidianPoolRiseSound;
    [Range(0f, 1f)]
    [SerializeField] private float obsidianPoolRiseVolume = 1f;

    [Header("나무다리 (나무다리로 파훼할 때)")]
    [Tooltip("나무다리로 파훼하면 하늘에 떠 있던 다리가 뚝 떨어져 물 위에 걸쳐진다")]
    [SerializeField] private bool dropWoodBridge = true;
    [Tooltip("이 종류의 원소로 파훼했을 때만 다리가 떨어진다")]
    [SerializeField] private ElementType woodBridgeElement = ElementType.Woodbridge;
    [Tooltip("씬에 미리 놓아 둔 다리. 지금 서 있는 자리가 곧 떨어지기 시작하는 높이가 된다. " +
             "회전과 가로 위치(X, Z)는 씬에 놓은 그대로 두고 높이만 내려온다")]
    [SerializeField] private Transform woodBridge;
    [Tooltip("위 칸이 비어 있으면 이 이름으로 씬에서 다리를 찾는다")]
    [SerializeField] private string woodBridgeObjectName = "Bridge";
    [Tooltip("떨어져 내려오는 데 걸리는 시간(초). 플레이어가 함정에 닿기 전에 끝나야 한다")]
    [SerializeField] private float woodBridgeDropDuration = 0.6f;
    [Tooltip("물 표면을 기준으로 다리를 올리고 내린다(미터). 발판 높이가 길과 어긋나면 여기서 맞춘다")]
    [SerializeField] private float woodBridgeHeightOffset;
    [Tooltip("다리가 떨어져 내려앉을 때 울릴 소리")]
    [SerializeField] private AudioClip woodBridgeLandSound;
    [Range(0f, 1f)]
    [SerializeField] private float woodBridgeLandVolume = 1f;

    [Header("시멘트 (시멘트로 파훼할 때)")]
    [Tooltip("시멘트로 파훼하면 소금 치듯 두 번 뿌린 뒤 물이 회색 시멘트로 매워진다")]
    [SerializeField] private bool fillCementPool = true;
    [Tooltip("이 종류의 원소로 파훼했을 때만 시멘트가 뿌려진다")]
    [SerializeField] private ElementType cementPoolElement = ElementType.Cement;
    [Tooltip("쓸 시멘트 모델. 프로젝트 창의 fbx나 프리팹을 그대로 물리면 된다. " +
             "물리면 시작할 때 하나 만들어 두고 계속 그것만 쓴다")]
    [SerializeField] private GameObject cementPrefab;
    [Tooltip("위 칸이 비어 있을 때 쓸, 씬에 미리 놓아 둔 시멘트 오브젝트")]
    [SerializeField] private Transform cementObject;
    [Tooltip("위 두 칸이 다 비어 있으면 이 이름으로 씬에서 시멘트를 찾는다")]
    [SerializeField] private string cementObjectName = "Cement";
    [Tooltip("모델로 만들어 쓸 때 맞출 지름(미터). 씬 오브젝트를 쓸 때는 크기를 건드리지 않는다")]
    [SerializeField] private float cementDiameter = 0.8f;
    [Tooltip("시멘트 원소를 올리기 전까지 감춰 둔다. 원소를 올리면 나타나고, 다 뿌린 뒤에 사라진다")]
    [SerializeField] private bool hideCementUntilArmed = true;
    [Tooltip("원소를 올렸을 때 시멘트가 물 표면에서 이만큼 위에 떠 있는다(미터)")]
    [SerializeField] private float cementHoverHeight = 0.9f;
    [Tooltip("소금 치듯 흔드는 횟수")]
    [Range(1, 5)]
    [SerializeField] private int cementSprinkleCount = 2;
    [Tooltip("한 번 기울였다 돌아오는 데 걸리는 시간(초)")]
    [SerializeField] private float cementSprinkleDuration = 0.32f;
    [Tooltip("뿌릴 때 기울이는 각도(도)")]
    [SerializeField] private float cementSprinkleAngle = 55f;
    [Tooltip("기울인 채로 손목 털듯 까딱이는 폭(도)")]
    [SerializeField] private float cementShakeAngle = 9f;
    [Tooltip("한 번 뿌리고 다음까지 쉬는 시간(초)")]
    [SerializeField] private float cementSprinkleGap = 0.12f;
    [Tooltip("한 번에 쏟아지는 알갱이 수")]
    [Range(4, 300)]
    [SerializeField] private int cementGrainCount = 45;
    [Tooltip("알갱이 하나의 크기(미터)")]
    [SerializeField] private float cementGrainSize = 0.07f;
    [SerializeField] private Color cementColor = new Color(0.62f, 0.62f, 0.6f, 1f);
    [Tooltip("다 뿌린 뒤 물이 시멘트로 매워지는 데 걸리는 시간(초)")]
    [SerializeField] private float cementFillDuration = 0.5f;
    [Tooltip("한 번 뿌릴 때마다 울릴 소리")]
    [SerializeField] private AudioClip cementSprinkleSound;
    [Range(0f, 1f)]
    [SerializeField] private float cementSprinkleVolume = 1f;

    [Header("이벤트")]
    [SerializeField] private UnityEvent<GameObject> onPlayerKilled;
    [SerializeField] private UnityEvent onCountered;

    [Header("사운드")]
    [Tooltip("플레이어가 물 함정에 걸렸을 때 울릴 소리")]
    [SerializeField] private AudioClip killSound;
    [Range(0f, 1f)]
    [SerializeField] private float killVolume = 1f;
    [Tooltip("물이 걷힐 때 울릴 소리")]
    [SerializeField] private AudioClip counterSound;
    [Range(0f, 1f)]
    [SerializeField] private float counterVolume = 1f;

    private readonly Dictionary<Transform, ColliderState[]> changedPlayerColliders = new Dictionary<Transform, ColliderState[]>();

    private bool countered;
    private ElementData armedElement;
    private int armedCount;
    private bool effectWasActive = true;
    private bool createdApproachZone;

    // 파훼할 때 만들어 낸 연출 오브젝트(웅덩이, 시멘트 알갱이). 되돌릴 때 통째로 치운다.
    private readonly List<GameObject> spawnedEffects = new List<GameObject>();
    private bool counterEffectBuilt;

    // 나무다리와 시멘트는 씬에 미리 놓여 있는 오브젝트라 지우지 않고 제자리로 되돌린다.
    private Vector3 woodBridgeHomePosition;
    private Vector3 woodBridgeHomeScale;
    private bool woodBridgeHomeKnown;
    private Coroutine woodBridgeRoutine;

    // 물 위에 올려 둔 용암 표시. 굳는 순간 치운다.
    private Transform lavaMark;

    private Vector3 cementHomePosition;
    private Quaternion cementHomeRotation;
    private bool cementHomeActive;
    private bool cementHomeKnown;
    private Coroutine cementRoutine;

    private struct ColliderState
    {
        public Collider Collider;
        public bool WasTrigger;
    }

    public bool IsCountered => countered;

    // 필요한 개수까지 다 올려 두어 발동을 기다리는 상태.
    // 진흙 2개짜리 함정에 하나만 올려 둔 동안은 아직 false다(플레이어가 지나가도 걷히지 않는다).
    public bool IsArmed => armedElement != null && armedCount >= RequiredFor(armedElement) && !countered;

    // 올려 두기만 하고 아직 개수가 안 찬 상태도 포함해서, 무언가 올라가 있는지.
    private bool HasCharge => armedElement != null && armedCount > 0 && !countered;

    // 이 원소로 물을 걷는 데 필요한 개수.
    private int RequiredFor(ElementData data)
    {
        return ElementCounterCharge.RequiredFor(counterCharges, data);
    }

    // 실제로 물이 걷힌 순간. 올려 둔 원소 표시를 함께 치우려고 PlacementSystem이 구독한다.
    public event System.Action Countered;

    // 파훼 판정에 쓰는 연출 뿌리. 비어 있으면 물 모델(판정 콜라이더가 붙은 오브젝트)을 쓴다.
    private Transform EffectRoot => effectRoot != null
        ? effectRoot
        : (trapCollider != null ? trapCollider.transform : transform);

    // 탑뷰에서 3D 모습을 감출 때(TrapTopViewIcon) 기준으로 삼는 뿌리. 물 함정은 연출 뿌리가 곧 물 모델이다.
    public Transform VisualRoot => EffectRoot;

    private void OnEnable()
    {
        PlayerAutoWalker.BeforeRespawn += HandleBeforeRespawn;
    }

    private void OnDisable()
    {
        PlayerAutoWalker.BeforeRespawn -= HandleBeforeRespawn;
        RestoreAllChangedColliders();
    }

    private void Awake()
    {
        // 파훼되면 물 모델을 통째로 끄기 때문에 OnEnable/OnDisable로는 복구 신호를 받을 수 없다.
        // 꺼져 있어도 신호가 오도록 수명 전체에 걸쳐 구독한다.
        StageReset.Requested += HandleStageReset;
        StageStartButton.StageStarted += HandleStageStarted;

        if (trapCollider == null)
        {
            trapCollider = GetComponentInParent<CapsuleCollider>();
        }

        if (trapCollider == null && transform.parent != null)
        {
            trapCollider = transform.parent.GetComponentInChildren<CapsuleCollider>();
        }

        if (trapCollider == null)
        {
            Debug.LogError($"{name}: WaterTrap이 사용할 CapsuleCollider를 찾지 못했습니다. WaterTrapModel의 콜라이더를 연결해 주세요.", this);
            enabled = false;
            return;
        }

        if (!trapCollider.isTrigger)
        {
            Debug.LogWarning($"{name}: WaterTrap의 CapsuleCollider는 IsTrigger가 켜져 있어야 합니다. 자동으로 켭니다.", trapCollider);
            trapCollider.isTrigger = true;
        }

        effectWasActive = EffectRoot.gameObject.activeSelf;

        WaterTrapTriggerRelay relay = trapCollider.GetComponent<WaterTrapTriggerRelay>();
        if (relay == null)
        {
            relay = trapCollider.gameObject.AddComponent<WaterTrapTriggerRelay>();
        }

        relay.Setup(this);
    }

    // 플레이어(월드는 StageWorldBuilder가 Awake에서 만든다)를 찾아야 하므로 Start에서 만든다.
    private void Start()
    {
        EnsureApproachZone();
        RememberWoodBridgeHome();
        RememberCementHome();
    }

    private void OnDestroy()
    {
        StageReset.Requested -= HandleStageReset;
        StageStartButton.StageStarted -= HandleStageStarted;

        // 파훼 연출은 이 오브젝트의 자식이라 같이 지워진다. 따로 치울 필요가 없다.

        // 자동으로 만든 트리거는 뿌리에 따로 서 있어서 같이 지워지지 않는다.
        if (createdApproachZone && approachZone != null)
        {
            Destroy(approachZone.gameObject);
        }
    }

    // ───────────────────────────── 파훼 ─────────────────────────────

    // 조합창에서 끌어온 원소가 이 물을 걷을 수 있는지.
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

    // 원소를 올리는 데 성공하면 true. PlacementSystem이 함정 칸에 원소를 놓을 때 호출한다.
    public bool TryCounter(ElementData data)
    {
        if (!CanBeCounteredBy(data))
        {
            return false;
        }

        // 같은 원소를 더 올리는 중이면 개수만 늘린다.
        armedCount = armedElement == data ? armedCount + 1 : 1;
        armedElement = data;

        // 시멘트를 올렸다면 씬에 감춰 둔 시멘트가 물 위로 올라온다. 이게 곧 "올려 둔 원소"의 모습이다.
        ShowCementForArming(data);

        // 아직 개수가 모자라면 여기서 끝. 플레이어가 지나가도 걷히지 않는다.
        int required = RequiredFor(data);
        if (armedCount < required)
        {
            Debug.Log($"{name}: '{data.ElementName}' {armedCount}/{required}개를 물 위에 올려 두었습니다. " +
                      $"{required - armedCount}개 더 부어야 물이 걷힙니다.", this);
            return true;
        }

        if (counterOnPlayerApproach)
        {
            Debug.Log($"{name}: '{data.ElementName}' {armedCount}/{required}개를 물 위에 올려 두었습니다. " +
                      "플레이어가 함정 앞을 지나가면 길이 열립니다.", this);
            return true;
        }

        if (!waitForStageStart || !StageStartButton.Exists || StageStartButton.HasStarted)
        {
            ApplyCounter();
        }
        else
        {
            Debug.Log($"{name}: '{data.ElementName}'을 물 위에 올려 두었습니다. 시작 버튼을 누르면 길이 열립니다.", this);
        }

        return true;
    }

    // 올려 둔 원소를 함정이 직접 보여 줄 때 쓸 모습. 시멘트만 자기 오브젝트를 내어 준다.
    // 나머지는 null이라, PlacementSystem이 늘 하던 대로 기본 구를 놓는다.
    public Transform GetCounterVisual(ElementData data)
    {
        if (IsCementElement(data))
        {
            RememberCementHome();
            return cementObject;
        }

        // 용암은 물 위에 고인 용암 웅덩이로 보여 준다. 회색 구가 물 위에 뜨면 어색하다.
        if (buildObsidianPool && data != null && data.ElementType == obsidianPoolElement)
        {
            return BuildLavaMark();
        }

        return null;
    }

    // 올려 둔 용암 표시. 물 위에 작게 고인 용암 웅덩이다.
    // 발동하면 이것과 별개로 하늘에서 용암이 떨어지므로, 여기서는 "올려 두었다"만 보여 주면 된다.
    private Transform BuildLavaMark()
    {
        Transform water = EffectRoot;

        Material material = WorldVisual.CreateLit(lavaDropColor);
        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", lavaDropColor);
        }

        float diameter = Mathf.Max(0.1f, water.lossyScale.x * 0.45f);

        GameObject mark = WorldVisual.CreateCylinder(
            "LavaMark",
            null,
            Vector3.zero,
            new Vector3(diameter, 0.05f, diameter),
            material);

        // 물 표면 바로 위에 얹는다.
        mark.transform.position = water.position + Vector3.up * (water.lossyScale.y + 0.05f);

        StripColliders(mark);
        TrackEffect(mark);

        lavaMark = mark.transform;
        return lavaMark;
    }

    // 아직 걷히기 전이라면 올려 둔 원소를 하나 물린다. 배치한 원소를 회수할 때 쓴다.
    //
    // 여러 개를 올려 두었으면 한 번에 하나씩만 물린다.
    // 맵에 놓인 표시도 올려 둔 개수만큼 있으므로, 표시 하나에 원소 하나가 맞아떨어진다.
    public bool CancelCounter()
    {
        if (!HasCharge)
        {
            return false;
        }

        Debug.Log($"{name}: 올려 두었던 '{armedElement.ElementName}'을 하나 회수했습니다.", this);
        armedCount--;

        if (armedCount > 0)
        {
            return true;
        }

        armedCount = 0;
        armedElement = null;

        // 마지막 하나까지 물렸으니 물이 다시 흐른다. 뿌릴 것이 없어졌으므로 시멘트도 도로 감춘다.
        ReturnCementHome();
        return true;
    }

    // 함정 앞 트리거(TrapApproachZone)가 부른다.
    public void NotifyPlayerApproached()
    {
        if (!IsArmed)
        {
            return;
        }

        Debug.Log($"{name}: 플레이어가 함정 앞을 지나가 '{armedElement.ElementName}'이 발동합니다.", this);
        ApplyCounter();
    }

    private void HandleStageStarted()
    {
        // 함정 앞 트리거를 쓰는 함정은 출발한다고 걷히지 않는다. 플레이어가 다가올 때까지 기다린다.
        if (IsArmed && !counterOnPlayerApproach)
        {
            ApplyCounter();
        }
    }

    private void ApplyCounter()
    {
        ElementData data = armedElement;
        countered = true;

        if (trapCollider != null)
        {
            trapCollider.enabled = false;
        }

        Transform root = EffectRoot;

        // 무엇으로 건너게 되었는지 눈에 보여 준다. (물을 치우기 전에 크기·위치를 그대로 베낀다)
        //
        // 나무다리처럼 물 위에 걸치는 연출은 물이 남아 있어야 "다리를 딛고 건넌다"는 그림이 된다.
        // 시멘트처럼 뜸을 들이는 연출은 다 뿌린 뒤에 스스로 물을 치운다.
        // 그래서 "지금 당장 물을 치울지"는 연출 쪽이 정한다.
        bool keepWater = BuildCounterEffect(data, root);

        Debug.Log($"{name}: '{data.ElementName}'으로 물을 건널 수 있게 되었습니다.", this);
        SfxPlayer.PlayAt(counterSound, root.position, counterVolume);
        onCountered?.Invoke();
        Countered?.Invoke();

        // 물 모델을 통째로 치워야 길이 열린 것처럼 보인다. (이 뒤로는 root가 꺼져 있을 수 있으므로 마지막에 한다)
        if (!keepWater)
        {
            HideWaterModel(root);
        }
    }

    // 물을 눈에서 치운다. 소리와 물보라부터 멈추고 마지막에 오브젝트를 끈다.
    private void HideWaterModel(Transform root)
    {
        foreach (ParticleSystem particles in root.GetComponentsInChildren<ParticleSystem>())
        {
            // 이미 나온 입자는 자연스럽게 사그라들게 두고 새로 뿜는 것만 멈춘다.
            particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        foreach (AudioSource audio in root.GetComponentsInChildren<AudioSource>())
        {
            audio.Stop();
        }

        if (deactivateEffect)
        {
            root.gameObject.SetActive(false);
        }
    }

    // ─────────────────────────── 파훼 연출 ───────────────────────────

    // 파훼에 쓴 원소에 맞는 "건너는 방법"을 세우고, 물 모델을 그대로 남겨 둘지 알려 준다.
    //
    // 흑요석 웅덩이는 여기서 새로 만들고(물 모델은 곧 꺼지므로 그 밑에 두면 안 된다),
    // 나무다리는 씬에 미리 놓아 둔 것을 떨어뜨린다.
    private bool BuildCounterEffect(ElementData data, Transform waterModel)
    {
        if (data == null || counterEffectBuilt)
        {
            return false;
        }

        counterEffectBuilt = true;

        // 하늘에서 용암이 떨어져 물에 닿는 순간 굳는다.
        // 떨어지는 동안에는 물이 남아 있어야 "물에 용암이 쏟아졌다"는 그림이 된다.
        if (buildObsidianPool && data.ElementType == obsidianPoolElement)
        {
            StartCoroutine(DropLavaThenFreeze(waterModel));
            return true;
        }

        // 다리는 물 위에 걸치는 것이므로 다리를 떨어뜨렸을 때만 물을 남긴다.
        if (dropWoodBridge && data.ElementType == woodBridgeElement)
        {
            return DropSceneWoodBridge(waterModel);
        }

        // 시멘트는 다 뿌린 뒤에 물을 치우므로, 뿌리는 동안은 물을 남겨 둔다.
        if (fillCementPool && data.ElementType == cementPoolElement)
        {
            return SprinkleCement(waterModel);
        }

        return false;
    }

    // 만든 연출을 관리 오브젝트 밑으로 옮기고 기억해 둔다. 월드 크기는 그대로 유지한다.
    private void TrackEffect(GameObject effect)
    {
        effect.transform.SetParent(transform, true);
        spawnedEffects.Add(effect);
    }

    private void ClearSpawnedEffects()
    {
        foreach (GameObject effect in spawnedEffects)
        {
            if (effect != null)
            {
                Destroy(effect);
            }
        }

        spawnedEffects.Clear();
    }

    // 플레이어가 걸어서 지나가야 하므로 연출이 길을 막으면 안 된다.
    // 배치한 원소를 되찾는 레이캐스트(PlacementSystem)도 가리지 않게 콜라이더를 전부 뗀다.
    private static void StripColliders(GameObject target)
    {
        foreach (Collider effectCollider in target.GetComponentsInChildren<Collider>(true))
        {
            Destroy(effectCollider);
        }
    }

    // ─────────────────────── 물을 메우는 웅덩이 ───────────────────────

    // 물이 있던 자리와 똑같은 모양으로 웅덩이를 세우고 아래에서부터 차오르게 한다.
    // 흑요석(용암)과 시멘트가 색만 달리해서 함께 쓴다.
    private void BuildFillPool(string poolName, Transform waterModel, GameObject prefab, Color color,
        float smoothness, float riseDuration, AudioClip riseSound, float riseVolume)
    {
        Vector3 scale = waterModel.lossyScale;
        Vector3 position = waterModel.position;
        Transform pool;

        if (prefab != null)
        {
            pool = Instantiate(prefab, position, waterModel.rotation).transform;
            pool.localScale = scale;
        }
        else
        {
            Material material = WorldVisual.CreateLit(color);
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            // 물 모델과 같은 원기둥이라 반지름·높이가 그대로 맞는다.
            GameObject poolObject = WorldVisual.CreateCylinder(poolName, null, Vector3.zero, Vector3.one, material);
            pool = poolObject.transform;
            pool.SetPositionAndRotation(position, waterModel.rotation);
            pool.localScale = scale;

            StripColliders(poolObject);
        }

        TrackEffect(pool.gameObject);

        SfxPlayer.PlayAt(riseSound, position, riseVolume);
        StartCoroutine(RiseFillPool(pool, pool.localScale, pool.localPosition, riseDuration));
    }

    // 하늘에서 용암 덩이가 떨어져 물에 닿고, 그 자리가 흑요석으로 굳는다.
    private IEnumerator DropLavaThenFreeze(Transform waterModel)
    {
        float surfaceY = waterModel.position.y + waterModel.lossyScale.y;
        Vector3 impact = new Vector3(waterModel.position.x, surfaceY, waterModel.position.z);

        Material material = WorldVisual.CreateLit(lavaDropColor);
        float size = Mathf.Max(0.1f, waterModel.lossyScale.x * 0.45f);

        GameObject blob = WorldVisual.CreateCylinder("LavaDrop", null, Vector3.zero, Vector3.one, material);
        blob.transform.localScale = new Vector3(size, size * 0.7f, size);
        StripColliders(blob);
        TrackEffect(blob);

        Vector3 start = impact + Vector3.up * Mathf.Max(0.1f, lavaDropHeight);
        float duration = Mathf.Max(0.01f, lavaDropDuration);

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            if (blob == null)
            {
                yield break;
            }

            // 떨어지는 것이라 아래로 갈수록 빨라진다.
            float k = elapsed / duration;
            blob.transform.position = Vector3.Lerp(start, impact, k * k);
            yield return null;
        }

        if (blob != null)
        {
            blob.transform.position = impact;
        }

        SfxPlayer.PlayAt(lavaDropSound, impact, lavaDropVolume);

        // 올려 두었던 용암 표시를 먼저 치운다.
        // 이것은 상쇄 표식의 자식이 아니라 함정이 들고 있는 것이라, 표식이 지워져도 혼자 남는다.
        // 그냥 두면 다 굳은 흑요석 위에 빨간 조각이 얹혀 있게 된다.
        if (lavaMark != null)
        {
            Destroy(lavaMark.gameObject);
            lavaMark = null;
        }

        // 닿는 순간 물이 걷히고 그 자리가 흑요석으로 굳는다.
        HideWaterModel(waterModel);
        BuildFillPool("ObsidianPool", waterModel, obsidianPoolPrefab, obsidianPoolColor,
            obsidianPoolSmoothness, obsidianPoolRiseDuration, obsidianPoolRiseSound, obsidianPoolRiseVolume);

        // 굳는 동안 용암 덩이는 흑요석 색으로 식으며 납작해진다.
        float cool = Mathf.Max(0.01f, obsidianPoolRiseDuration);
        Vector3 blobScale = blob != null ? blob.transform.localScale : Vector3.one;

        for (float elapsed = 0f; elapsed < cool; elapsed += Time.unscaledDeltaTime)
        {
            if (blob == null)
            {
                yield break;
            }

            float k = elapsed / cool;
            WorldVisual.SetMaterialColor(material, Color.Lerp(lavaDropColor, obsidianPoolColor, k));
            blob.transform.localScale = new Vector3(blobScale.x, blobScale.y * (1f - k), blobScale.z);
            yield return null;
        }

        if (blob != null)
        {
            Destroy(blob);
        }
    }

    // 바닥은 그대로 두고 높이만 0에서 원래대로 키운다. 물이 차오르듯 보인다.
    // 원기둥 프리미티브는 중심 기준 위아래로 scale.y만큼 뻗으므로, 높이를 키우는 만큼 중심도 같이 올려 준다.
    private static IEnumerator RiseFillPool(Transform pool, Vector3 targetScale, Vector3 targetPosition, float riseDuration)
    {
        float duration = Mathf.Max(0.01f, riseDuration);
        float bottomY = targetPosition.y - targetScale.y;

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            if (pool == null)
            {
                yield break;
            }

            // 처음엔 왈칵 차오르다 끝에서 잦아든다.
            float k = 1f - Mathf.Pow(1f - elapsed / duration, 3f);

            pool.localScale = new Vector3(targetScale.x, targetScale.y * k, targetScale.z);
            pool.localPosition = new Vector3(targetPosition.x, bottomY + targetScale.y * k, targetPosition.z);

            yield return null;
        }

        if (pool != null)
        {
            pool.localScale = targetScale;
            pool.localPosition = targetPosition;
        }
    }

    // ─────────────────────────── 시멘트 ───────────────────────────

    // 씬에 놓아 둔 시멘트를 찾아, 처음 각도를 기억해 둔다. 뿌린 뒤 이 각도로 돌아온다.
    private void RememberCementHome()
    {
        if (!fillCementPool || cementHomeKnown)
        {
            return;
        }

        // 따로 물려 둔 것이 없으면, 파훼 원소로 등록된 시멘트가 들고 있는 3D 모델을 그대로 쓴다.
        // 그래야 "시멘트는 이렇게 생겼다"를 ElementData 한 곳에만 적어 두면 된다.
        if (cementPrefab == null)
        {
            foreach (ElementData candidate in counterElements)
            {
                if (IsCementElement(candidate) && candidate.WorldModel != null)
                {
                    cementPrefab = candidate.WorldModel;
                    break;
                }
            }
        }

        // 모델 에셋을 물려 두었으면 그것으로 하나 만들어 쓴다.
        // 씬에 미리 놓아 둘 필요가 없고, 이 함정 밑에 붙어 있으니 함정과 함께 정리된다.
        if (cementObject == null && cementPrefab != null)
        {
            GameObject spawned = Instantiate(cementPrefab, transform);
            spawned.name = $"{cementPrefab.name} (WaterTrap)";

            // fbx는 단위에 따라 터무니없이 크거나 작게 들어온다. 눈에 보이는 크기로 맞춰 준다.
            Bounds bounds = ElementVisual.MeasureBounds(spawned);
            float widest = Mathf.Max(bounds.size.x, bounds.size.z);
            if (widest > 0.0001f)
            {
                spawned.transform.localScale *= cementDiameter / widest;
            }

            cementObject = spawned.transform;
        }

        if (cementObject == null)
        {
            cementObject = FindSceneObject(cementObjectName);
        }

        if (cementObject == null)
        {
            Debug.LogWarning(
                $"{name}: 뿌릴 시멘트를 찾지 못했습니다. " +
                $"cementPrefab 칸에 시멘트 모델을 물리거나, cementObject 칸에 씬의 시멘트를 물리거나, " +
                $"씬 오브젝트 이름을 '{cementObjectName}'으로 맞춰 주세요.",
                this);
            return;
        }

        cementHomePosition = cementObject.position;
        cementHomeRotation = cementObject.rotation;
        cementHomeActive = cementObject.gameObject.activeSelf;
        cementHomeKnown = true;

        // 어느 칸에서 온 것인지 남겨 둔다. 안 보일 때 무엇이 비었는지 콘솔만 보면 알 수 있어야 한다
        // 원소를 올리기 전까지는 감춰 둔다. 올리는 순간 "이걸 뿌릴 거다"라는 뜻으로 나타난다.
        ReturnCementHome();
    }

    // 아무것도 올려 두지 않았을 때의 자리·모습으로 되돌린다.
    private void ReturnCementHome()
    {
        if (!cementHomeKnown || cementObject == null)
        {
            return;
        }

        cementObject.SetPositionAndRotation(cementHomePosition, cementHomeRotation);
        cementObject.gameObject.SetActive(!hideCementUntilArmed && cementHomeActive);
    }

    // 시멘트 원소를 올려 두면 씬의 시멘트가 물 위로 올라와 뜬다. 이게 곧 "올려 둔 원소"의 모습이다.
    // 회수하면 원래 자리로 돌아가 다시 감춰진다.
    private void ShowCementForArming(ElementData data)
    {
        if (!IsCementElement(data))
        {
            return;
        }

        // Start에서 못 찾았어도 여기서 한 번 더 찾아본다.
        // (씬이 늦게 만들어지거나 나중에 칸을 채웠을 수 있다. 성공할 때까지 계속 다시 시도한다)
        RememberCementHome();

        if (cementObject == null)
        {
            return;
        }

        Transform water = EffectRoot;
        Vector3 hover = water.position + Vector3.up * (water.lossyScale.y + cementHoverHeight);

        cementObject.SetPositionAndRotation(hover, cementHomeRotation);
        cementObject.gameObject.SetActive(true);
    }

    private bool IsCementElement(ElementData data)
    {
        return fillCementPool && data != null && data.ElementType == cementPoolElement;
    }

    // 씬에서 이름으로 오브젝트를 찾는다.
    //
    // GameObject.Find는 꺼져 있는 오브젝트를 못 찾고 이름도 정확히 맞아야 해서,
    // 꺼진 것까지 훑고 대소문자와 "cement (1)" 같은 사본 꼬리표도 눈감아 준다.
    private static Transform FindSceneObject(string wanted)
    {
        if (string.IsNullOrEmpty(wanted))
        {
            return null;
        }

        Transform prefixMatch = null;

        foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (string.Equals(candidate.name, wanted, System.StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }

            if (prefixMatch == null && candidate.name.StartsWith(wanted, System.StringComparison.OrdinalIgnoreCase))
            {
                prefixMatch = candidate;
            }
        }

        return prefixMatch;
    }

    // 소금 치듯 시멘트를 뿌리고, 다 뿌린 뒤 물을 회색 시멘트로 매운다. 시작했으면 true.
    //
    // 뿌리는 동안에는 물이 남아 있어야 "물에 시멘트를 붓는" 그림이 되므로,
    // 물을 치우는 일은 연출이 끝나는 순간 코루틴이 직접 한다.
    private bool SprinkleCement(Transform waterModel)
    {
        RememberCementHome();

        if (cementObject == null)
        {
            // 시멘트 오브젝트가 없으면 뿌리는 연출만 건너뛰고 매우기는 그대로 한다.
            HideWaterModel(waterModel);
            BuildFillPool("CementPool", waterModel, null, cementColor, 0.15f,
                cementFillDuration, cementSprinkleSound, cementSprinkleVolume);
            return true;
        }

        if (cementRoutine != null)
        {
            StopCoroutine(cementRoutine);
        }

        cementRoutine = StartCoroutine(SprinkleThenFill(waterModel));
        return true;
    }

    private IEnumerator SprinkleThenFill(Transform waterModel)
    {
        // 물 표면을 겨냥해 알갱이를 뿌린다.
        float surfaceY = waterModel.position.y + waterModel.lossyScale.y;
        ParticleSystem grains = BuildGrainBurst(surfaceY);

        int shakes = Mathf.Max(1, cementSprinkleCount);
        for (int i = 0; i < shakes; i++)
        {
            yield return ShakeCement(waterModel, grains);

            if (i < shakes - 1)
            {
                yield return new WaitForSecondsRealtime(Mathf.Max(0f, cementSprinkleGap));
            }
        }

        // 다 뿌린 시멘트는 사라지고, 그와 동시에 물이 시멘트로 매워진다.
        if (cementObject != null)
        {
            cementObject.rotation = cementHomeRotation;
            cementObject.gameObject.SetActive(false);
        }

        HideWaterModel(waterModel);
        BuildFillPool("CementPool", waterModel, null, cementColor, 0.15f,
            cementFillDuration, null, 0f);

        cementRoutine = null;
    }

    // 소금통을 쥐고 한 번 터는 동작. 물 쪽으로 기울여 쏟고, 톡톡 털고, 제자리로 돌아온다.
    private IEnumerator ShakeCement(Transform waterModel, ParticleSystem grains)
    {
        // 물 쪽으로 기울어지도록, 시멘트에서 물로 가는 가로 방향의 직각축을 회전축으로 삼는다.
        Vector3 toWater = Vector3.ProjectOnPlane(waterModel.position - cementObject.position, Vector3.up);
        Vector3 axis = toWater.sqrMagnitude > 0.0001f
            ? Vector3.Cross(Vector3.up, toWater.normalized)
            : cementObject.right;

        Quaternion tilted = Quaternion.AngleAxis(cementSprinkleAngle, axis) * cementHomeRotation;

        // 기울이기 → 털기 → 되돌리기를 4 : 3 : 3으로 나눠 쓴다.
        float duration = Mathf.Max(0.03f, cementSprinkleDuration);
        float tiltIn = duration * 0.4f;
        float flick = duration * 0.3f;
        float tiltOut = duration * 0.3f;

        for (float elapsed = 0f; elapsed < tiltIn; elapsed += Time.unscaledDeltaTime)
        {
            if (cementObject == null)
            {
                yield break;
            }

            cementObject.rotation = Quaternion.Slerp(cementHomeRotation, tilted, elapsed / tiltIn);
            yield return null;
        }

        if (cementObject == null)
        {
            yield break;
        }

        cementObject.rotation = tilted;

        // 제일 많이 기울어진 순간에 한 움큼 쏟는다.
        if (grains != null)
        {
            grains.transform.position = GrainSpout();
            grains.Emit(Mathf.Max(1, cementGrainCount));
        }

        SfxPlayer.PlayAt(cementSprinkleSound, cementObject.position, cementSprinkleVolume);

        // 기울인 채로 손목을 털듯 두어 번 까딱인다. 이게 있어야 "붓는" 것이 아니라 "뿌리는" 것으로 보인다.
        for (float elapsed = 0f; elapsed < flick; elapsed += Time.unscaledDeltaTime)
        {
            if (cementObject == null)
            {
                yield break;
            }

            float wobble = Mathf.Sin(elapsed / flick * Mathf.PI * 4f) * cementShakeAngle;
            cementObject.rotation = Quaternion.AngleAxis(wobble, axis) * tilted;
            yield return null;
        }

        for (float elapsed = 0f; elapsed < tiltOut; elapsed += Time.unscaledDeltaTime)
        {
            if (cementObject == null)
            {
                yield break;
            }

            cementObject.rotation = Quaternion.Slerp(tilted, cementHomeRotation, elapsed / tiltOut);
            yield return null;
        }

        if (cementObject != null)
        {
            cementObject.rotation = cementHomeRotation;
        }
    }

    // 알갱이가 쏟아져 나오는 자리. 기울어진 지금 자세의 제일 낮은 곳이다.
    private Vector3 GrainSpout()
    {
        Bounds bounds = ElementVisual.MeasureBounds(cementObject.gameObject);
        return new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
    }

    // 물 위로 쏟아질 시멘트 알갱이. 뿌릴 때마다 Emit으로 한 움큼씩 뿜는다.
    private ParticleSystem BuildGrainBurst(float surfaceY)
    {
        GameObject grainObject = new GameObject("CementGrains");
        grainObject.transform.position = GrainSpout();

        ParticleSystem grains = grainObject.AddComponent<ParticleSystem>();
        grains.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        const float gravity = 2.5f;

        // 알갱이가 물에 닿을 즈음 사라지도록 낙하 시간을 계산해 수명으로 쓴다.
        // (그러지 않으면 물을 뚫고 바닥 아래까지 떨어진다)
        float fallDistance = Mathf.Max(0.2f, grainObject.transform.position.y - surfaceY);
        float lifetime = Mathf.Sqrt(2f * fallDistance / (9.81f * gravity));

        ParticleSystem.MainModule main = grains.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = lifetime;
        main.startSpeed = 0.5f;
        main.startSize = Mathf.Max(0.005f, cementGrainSize);
        main.startColor = cementColor;
        main.gravityModifier = gravity;
        main.maxParticles = Mathf.Max(1, cementGrainCount) * Mathf.Max(1, cementSprinkleCount);

        // 뿌리는 것은 Emit으로만 한다. 저절로 새어 나오지 않게 한다.
        ParticleSystem.EmissionModule emission = grains.emission;
        emission.enabled = false;

        // 아래로 쏟아지도록 원뿔을 눕힌다. (원뿔은 기본이 +Z 방향이다)
        ParticleSystem.ShapeModule shape = grains.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 14f;
        shape.radius = 0.12f;
        shape.rotation = new Vector3(90f, 0f, 0f);

        ParticleSystemRenderer grainRenderer = grainObject.GetComponent<ParticleSystemRenderer>();
        grainRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        grainRenderer.sharedMaterial = WorldVisual.CreateUnlit(cementColor);

        grains.Play();
        TrackEffect(grainObject);
        return grains;
    }

    // 시멘트를 처음 각도로 돌려놓고 다시 감춘다. 다시 뿌리는 것을 볼 수 있어야 하기 때문이다.
    private void RestoreCement()
    {
        if (!cementHomeKnown || cementObject == null)
        {
            return;
        }

        if (cementRoutine != null)
        {
            StopCoroutine(cementRoutine);
            cementRoutine = null;
        }

        ReturnCementHome();
    }

    // ─────────────────────────── 나무다리 ───────────────────────────

    // 씬에 놓아 둔 다리를 찾아, 처음 서 있던 자리를 기억해 둔다.
    // 그 자리가 곧 "하늘에서 떨어지기 시작하는 높이"이자, 되돌릴 때 돌아갈 자리다.
    private void RememberWoodBridgeHome()
    {
        if (!dropWoodBridge || woodBridgeHomeKnown)
        {
            return;
        }

        if (woodBridge == null)
        {
            woodBridge = FindSceneObject(woodBridgeObjectName);
        }

        if (woodBridge == null)
        {
            Debug.LogWarning(
                $"{name}: 떨어뜨릴 나무다리를 찾지 못했습니다. " +
                $"woodBridge 칸에 씬의 다리를 물리거나, 이름을 '{woodBridgeObjectName}'으로 맞춰 주세요.",
                this);
            return;
        }

        woodBridgeHomePosition = woodBridge.position;
        woodBridgeHomeScale = woodBridge.localScale;
        woodBridgeHomeKnown = true;
    }

    // 하늘에 떠 있던 다리를 물 위로 떨어뜨린다. 떨어뜨렸으면 true.
    //
    // 회전과 가로 위치(X, Z)는 씬에 놓은 그대로 두고 높이만 내린다.
    // 다리를 어디에 어떤 각도로 걸칠지는 씬에서 눈으로 맞추는 편이 정확하기 때문이다.
    private bool DropSceneWoodBridge(Transform waterModel)
    {
        RememberWoodBridgeHome();

        if (woodBridge == null)
        {
            return false;
        }

        // 물은 납작한 원기둥이라 lossyScale.y가 중심에서 표면까지 높이다.
        float surfaceY = waterModel.position.y + waterModel.lossyScale.y;

        // 피벗이 모델 어디에 박혀 있든 상관없도록, 다리의 제일 낮은 점이 물 표면에 닿게 맞춘다.
        Bounds bounds = ElementVisual.MeasureBounds(woodBridge.gameObject);
        float pivotToBottom = woodBridge.position.y - bounds.min.y;
        float landingY = surfaceY + pivotToBottom + woodBridgeHeightOffset;

        if (woodBridgeRoutine != null)
        {
            StopCoroutine(woodBridgeRoutine);
        }

        woodBridgeRoutine = StartCoroutine(DropWoodBridge(woodBridge, landingY));
        return true;
    }

    // 하늘에서 곧장 아래로 떨어져 물 위에 쿵 내려앉는다. X, Z와 회전은 건드리지 않는다.
    private IEnumerator DropWoodBridge(Transform bridge, float landingY)
    {
        float duration = Mathf.Max(0.01f, woodBridgeDropDuration);
        Vector3 start = bridge.position;

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            if (bridge == null)
            {
                yield break;
            }

            // 떨어지는 것이라 아래로 갈수록 빨라진다.
            float k = elapsed / duration;
            bridge.position = new Vector3(start.x, Mathf.Lerp(start.y, landingY, k * k), start.z);

            yield return null;
        }

        if (bridge == null)
        {
            yield break;
        }

        bridge.position = new Vector3(start.x, landingY, start.z);
        SfxPlayer.PlayAt(woodBridgeLandSound, bridge.position, woodBridgeLandVolume);

        yield return SettleWoodBridge(bridge);
        woodBridgeRoutine = null;
    }

    // 다리를 처음 있던 하늘 자리로 되돌린다. 다시 떨어지는 것을 볼 수 있어야 하기 때문이다.
    private void RestoreWoodBridge()
    {
        if (!woodBridgeHomeKnown || woodBridge == null)
        {
            return;
        }

        if (woodBridgeRoutine != null)
        {
            StopCoroutine(woodBridgeRoutine);
            woodBridgeRoutine = null;
        }

        woodBridge.position = woodBridgeHomePosition;
        woodBridge.localScale = woodBridgeHomeScale;
    }

    // 착지 순간 살짝 눌렸다가 펴진다. 무거운 것이 떨어진 느낌을 준다.
    private static IEnumerator SettleWoodBridge(Transform bridge)
    {
        const float settleDuration = 0.18f;
        Vector3 baseScale = bridge.localScale;

        for (float elapsed = 0f; elapsed < settleDuration; elapsed += Time.unscaledDeltaTime)
        {
            if (bridge == null)
            {
                yield break;
            }

            float squash = Mathf.Sin(elapsed / settleDuration * Mathf.PI) * 0.25f;
            bridge.localScale = new Vector3(
                baseScale.x * (1f + squash * 0.4f),
                baseScale.y * (1f - squash),
                baseScale.z);

            yield return null;
        }

        if (bridge != null)
        {
            bridge.localScale = baseScale;
        }
    }

    // 시작 버튼을 누를 때 / 죽어서 리스폰할 때 함정을 처음 상태로 되돌린다.
    private void HandleStageReset(StageReset.Reason reason)
    {
        ResetToOriginal(reason == StageReset.Reason.PlayerDeath);
    }

    // clearArmed가 true면 올려 둔 원소까지 물려서 완전히 처음 상태로 만든다.
    public void ResetToOriginal(bool clearArmed)
    {
        if (clearArmed)
        {
            // 이미 파훼에 써 버린 원소는 맵에서 표시까지 사라진 상태라 PlacementSystem이 회수해 줄 수 없다.
            // 함정이 직접 조합창으로 돌려준다.
            // 여러 개를 부어 걷었다면(진흙 2개처럼) 쓴 만큼 다 돌려준다.
            if (countered && armedElement != null)
            {
                int used = Mathf.Max(1, armedCount);
                Debug.Log($"{name}: 파훼에 썼던 '{armedElement.ElementName}' {used}개를 조합창으로 돌려줍니다.", this);

                for (int i = 0; i < used; i++)
                {
                    CraftingPanelUI.ReturnToPanel(armedElement);
                }
            }

            armedElement = null;
            armedCount = 0;

            // 올려 두기만 하고 아직 뿌리지 않은 채로 죽었을 수 있다. 그때도 시멘트는 도로 감춘다.
            // (아래 파훼 되돌리기는 실제로 파훼된 함정만 거치므로 여기서 따로 챙긴다)
            ReturnCementHome();
        }

        if (!countered)
        {
            return;
        }

        countered = false;

        // 물이 다시 흐르므로 만들어 낸 연출은 치우고, 씬에 있던 것들은 제자리로 돌려보낸다.
        counterEffectBuilt = false;
        ClearSpawnedEffects();
        RestoreWoodBridge();
        RestoreCement();

        Transform root = EffectRoot;

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

        Debug.Log($"{name}: 물 함정을 처음 상태로 되돌렸습니다.", this);
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

        // 물 모델 쪽을 기준으로 세워야 관리 오브젝트가 어디에 있든 물 앞에 선다.
        approachZone = TrapApproachZone.Build(this, EffectRoot, approachDistance, approachZoneSize);
        createdApproachZone = true;
    }

    // ───────────────────────────── 사망 판정 ─────────────────────────────

    public void HandleTrigger(Collider other)
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

        if (respectWaterImmunity && IsPlayerImmuneToWater(other))
        {
            Debug.Log($"{name}: 플레이어 '{player.name}'가 물 원소에 면역이라 사망하지 않습니다.", this);
            return;
        }

        if (!PlayerLocator.Kill(player, deathAnimationTriggerName, this))
        {
            return;
        }

        if (dropPlayerThroughGround)
        {
            MakePlayerCollidersTrigger(player);
        }

        ElementalKillEffect.Play(this, player);
        Debug.Log($"{name}: 플레이어 '{player.name}'가 물 함정에 닿았습니다. (충돌 콜라이더: {other.name})", this);
        SfxPlayer.PlayAt(killSound, trapCollider.transform.position, killVolume);
        onPlayerKilled?.Invoke(player.gameObject);
    }

    private void MakePlayerCollidersTrigger(Transform player)
    {
        if (changedPlayerColliders.ContainsKey(player))
        {
            return;
        }

        Collider[] colliders = player.GetComponentsInChildren<Collider>();
        ColliderState[] states = new ColliderState[colliders.Length];
        for (int i = 0; i < colliders.Length; i++)
        {
            states[i] = new ColliderState
            {
                Collider = colliders[i],
                WasTrigger = colliders[i].isTrigger
            };

            colliders[i].isTrigger = true;
        }

        changedPlayerColliders[player] = states;
    }

    private void HandleBeforeRespawn(Transform player)
    {
        RestorePlayerColliders(player);
    }

    private void RestorePlayerColliders(Transform player)
    {
        if (player == null || !changedPlayerColliders.TryGetValue(player, out ColliderState[] states))
        {
            return;
        }

        foreach (ColliderState state in states)
        {
            if (state.Collider != null)
            {
                state.Collider.isTrigger = state.WasTrigger;
            }
        }

        changedPlayerColliders.Remove(player);
    }

    private void RestoreAllChangedColliders()
    {
        foreach (KeyValuePair<Transform, ColliderState[]> pair in changedPlayerColliders)
        {
            foreach (ColliderState state in pair.Value)
            {
                if (state.Collider != null)
                {
                    state.Collider.isTrigger = state.WasTrigger;
                }
            }
        }

        changedPlayerColliders.Clear();
    }

    private static bool IsPlayerImmuneToWater(Collider playerCollider)
    {
        IElementImmune immunity = playerCollider.GetComponentInParent<IElementImmune>();
        return immunity != null && immunity.IsImmuneTo(ElementType.Water);
    }
}
