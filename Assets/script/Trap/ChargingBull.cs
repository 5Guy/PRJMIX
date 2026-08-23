using System.Collections.Generic;
using UnityEngine;

// 맵에 미리 배치해 두는 황소(bullsOneShot).
//
// 자동차(ChargingCar)와 같은 뼈대를 쓰되, 다른 점은 두 가지다.
//  - 앞을 가로막는 물체를 멈추지 않고 그대로 들이받아 날려 버린다(Rigidbody에 힘을 준다).
//  - 플레이어에게 도달하면 그 자리에 멈추고, BullKillZone이 플레이어를 날린다.
//    사망 후 화면 연출은 판정 오브젝트에 붙은 ElementalKillEffect가 맡는다.
//    돌진 중에는 Kinematic이라 몸통 콜라이더가 플레이어를 밀어내 버리므로,
//    받힘 판정 트리거에 들어오기를 기다리지 않고 벽과 같은 방식으로 앞을 훑어 플레이어를 찾는다.
//
// 발동은 자식으로 둔 빈 상자(BullTriggerZone)가 맡는다. 플레이어가 그 범위에 들어오면 Charge()가 불린다.
//
// 막는 방법: 지금은 돌 벽(CarBlocker)에 막힌다.
// 나중에 용암+물 = 흑요석이 생기면 blockedByStoneWall을 끄고 IsBlocker()에 흑요석 조건만 넣으면 된다.
[RequireComponent(typeof(Rigidbody))]
public class ChargingBull : MonoBehaviour
{
    // 돌진 방향을 무엇을 기준으로 읽을지.
    public enum DirectionSpace
    {
        Local,  // 황소 자신을 기준으로. 황소를 회전시키면 방향도 같이 돈다
        World,  // 씬 좌표 기준. 황소를 어느 쪽으로 돌려 놓든 정해진 쪽으로만 달린다
    }

    [Header("이동")]
    [SerializeField] private float speed = 10f;

    [Header("돌진 방향")]
    [Tooltip("Local = 황소를 회전시키면 방향도 따라 돈다 / World = 씬 좌표 기준으로 고정")]
    [SerializeField] private DirectionSpace directionSpace = DirectionSpace.Local;
    [Tooltip("달려 나갈 방향. Local 기준 (0,0,1)이 모델의 앞쪽이다.\n" +
             "모델의 앞이 Z+가 아니면 (0,0,-1)이나 (1,0,0) 처럼 바꿔 주면 된다.\n" +
             "부딪힌 물체와 플레이어도 이 방향으로 날아간다")]
    [SerializeField] private Vector3 chargeDirection = Vector3.forward;
    [Tooltip("위아래 성분을 버리고 수평으로만 달린다. 비탈에 비스듬히 놓아도 땅으로 파고들지 않는다")]
    [SerializeField] private bool keepHorizontal = true;
    [Tooltip("시작할 때의 회전을 계속 붙잡아 둔다. 물리 충돌로 황소가 돌아가 엉뚱한 쪽으로 달리는 것을 막는다")]
    [SerializeField] private bool holdRotation = true;

    [Header("모델 보정")]
    [Tooltip("모델이 항상 돌진 방향을 바라보게 매 프레임 강제한다.\n" +
             "애니메이션 클립이 모델의 회전을 직접 쥐고 있으면 씬에서 아무리 돌려 놔도 " +
             "실행하는 순간 한 방향으로 돌아가 버리는데, 이걸 켜면 그 위에 덮어쓴다")]
    [SerializeField] private bool faceChargeDirection = true;
    [Tooltip("모델의 코가 향한 쪽이 앞이 아닐 때 이만큼(도) 더 돌린다. 보통 0 / 90 / 180 / 270 중 하나면 맞는다.\n" +
             "판정과 이동에는 영향이 없고 보이는 것만 돌아간다")]
    [SerializeField] private float visualYawOffset;
    [Tooltip("돌려 줄 모델. 비워두면 Animator가 붙은 자식을 쓴다")]
    [SerializeField] private Transform modelRoot;

    [Header("사망 연출")]
    [SerializeField] private string deathAnimationTriggerName = "Die";

    [Header("황소 애니메이션")]
    [Tooltip("황소 애니메이션은 처음부터 계속 재생된다(컨트롤러가 알아서 반복한다).\n" +
             "돌진할 때 동작을 따로 바꾸고 싶을 때만 Animator 파라미터 이름을 적는다. 비워 두면 아무것도 건드리지 않는다")]
    [SerializeField] private string chargeAnimationBoolName = "";

    [Header("리셋")]
    [Tooltip("플레이어가 리스폰하면 황소도 시작 위치로 되돌린다")]
    [SerializeField] private bool resetOnPlayerRespawn = true;

    [Header("충돌체")]
    [Tooltip("몸통에 콜라이더가 없으면 모델 크기에 맞춘 BoxCollider를 자동으로 붙인다")]
    [SerializeField] private bool autoCreateCollider = true;

    [Header("막힘")]
    [Tooltip("돌 벽(CarBlocker)에 막혀 멈춘다. 흑요석이 생기면 이걸 끄고 흑요석 조건으로 바꾼다")]
    [SerializeField] private bool blockedByStoneWall = true;
    [Tooltip("벽을 몇 미터 앞에서부터 알아채고 멈출지")]
    [SerializeField] private float blockerLookAhead = 0.6f;
    [Tooltip("돌진하는 동안 물리 충돌에 밀리지 않고 정해진 속도로 밀고 나간다.\n" +
             "인도 턱·가로수 같은 맵 구조물에 걸려 혼자 멈추는 일이 없다.\n" +
             "멈춰 세울 수 있는 것은 앞을 살펴 찾아내는 돌 벽뿐이다")]
    [SerializeField] private bool plowThroughObstacles = true;

    [Header("바닥")]
    [Tooltip("밀고 나가는 동안 발밑에서 찾을 바닥. 여기서 뺀 레이어는 바닥으로 치지 않는다")]
    [SerializeField] private LayerMask groundLayers = ~0;
    [Tooltip("발밑을 얼마나 아래까지 훑을지(미터). 계단이나 턱을 타고 넘을 만큼 넉넉히 준다")]
    [SerializeField] private float groundProbeDepth = 3f;
    [Tooltip("찾은 바닥에서 이만큼 띄운다. 발이 땅에 살짝 묻히면 올려 준다")]
    [SerializeField] private float groundOffset;

    [Header("들이받기")]
    [Tooltip("부딪힌 Rigidbody를 앞으로 밀어내는 세기(m/s)")]
    [SerializeField] private float knockbackSpeed = 14f;
    [Tooltip("같이 얹어 주는 위쪽 속도(m/s). 있어야 바닥을 긁지 않고 붕 뜬다")]
    [SerializeField] private float knockbackLift = 6f;
    [Tooltip("날아가는 동안 빙글 도는 세기")]
    [SerializeField] private float knockbackSpin = 6f;
    [Tooltip("이보다 무거운 것은 밀지 않는다. 0 이하면 무게를 따지지 않는다")]
    [SerializeField] private float maxKnockbackMass = 0f;

    [Header("치임 판정")]
    [Tooltip("비워두면 몸통 크기에 맞춘 트리거(BullKillZone)를 자동으로 만들어 붙인다")]
    [SerializeField] private BullKillZone killZone;
    [Tooltip("자동으로 만들 때, 몸통 크기 대비 치임 판정의 크기 비율")]
    [SerializeField] private float killZoneScale = 0.9f;
    [Tooltip("몸통이 닿기 몇 미터 앞에서부터 플레이어를 받은 것으로 칠지.\n" +
             "몸통 콜라이더가 플레이어를 밀어내 버리면 안쪽에 있는 트리거에는 영영 들어오지 않으므로, " +
             "트리거만 기다리지 않고 앞을 직접 훑어서 찾는다")]
    [SerializeField] private float playerHitLookAhead = 0.1f;

    [Header("사운드")]
    [Tooltip("돌진을 시작할 때 한 번 울릴 소리(울음소리)")]
    [SerializeField] private AudioClip chargeSound;
    [Range(0f, 1f)]
    [SerializeField] private float chargeVolume = 1f;
    [Tooltip("무언가를 들이받아 날릴 때 울릴 소리")]
    [SerializeField] private AudioClip impactSound;
    [Range(0f, 1f)]
    [SerializeField] private float impactVolume = 1f;
    [Tooltip("벽에 막혀 멈출 때 울릴 소리")]
    [SerializeField] private AudioClip blockedSound;
    [Range(0f, 1f)]
    [SerializeField] private float blockedVolume = 1f;
    [Tooltip("달리는 동안 계속 울릴 발굽 소리(선택). 넣으면 반복 재생된다")]
    [SerializeField] private AudioClip runLoop;
    [Range(0f, 1f)]
    [SerializeField] private float runVolume = 0.6f;

    private Rigidbody body;
    private Collider bodyCollider;
    private Animator animator;
    private AudioSource runSource;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private bool isCharging;
    private bool animationWarned;
    private bool startedKinematic;

    // 한 번 날린 것을 매 프레임 다시 밀지 않도록 기억해 둔다(리셋할 때 비운다).
    private readonly HashSet<Rigidbody> knockedBack = new HashSet<Rigidbody>();

    public bool IsCharging => isCharging;

    // 실제로 달려 나가는 방향. 이동·벽 감지·날려버리기가 전부 이 하나를 본다.
    // (예전에는 transform.forward를 그대로 썼는데, 모델의 앞이 Z+가 아니면 엉뚱한 쪽으로 달렸다)
    public Vector3 ChargeDirection
    {
        get
        {
            Vector3 direction = directionSpace == DirectionSpace.Local
                ? transform.TransformDirection(chargeDirection)
                : chargeDirection;

            if (keepHorizontal)
            {
                direction.y = 0f;
            }

            // 방향을 (0,0,0)으로 비워 두면 제자리에서 부들거리므로 앞쪽으로 되돌린다.
            return direction.sqrMagnitude < 0.0001f ? transform.forward : direction.normalized;
        }
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();

        // 달리는 도중 뒤집히지 않도록 회전을 고정한다.
        body.constraints |= RigidbodyConstraints.FreezeRotation;

        startPosition = body.position;
        startRotation = body.rotation;
        startedKinematic = body.isKinematic;

        animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            // 이동은 Rigidbody 속도로만 처리한다. 루트 모션을 켜 두면 모델만 따로 앞서 나간다.
            animator.applyRootMotion = false;

            // 애니메이터가 황소 본체와 같은 오브젝트에 있으면, 클립이 트랜스폼을 건드릴 때
            // 돌진 방향과 판정 상자까지 같이 끌려다닌다. 모델은 자식으로 두는 편이 안전하다.
            if (animator.gameObject == gameObject)
            {
                Debug.LogWarning(
                    $"{name}: 황소 모델과 ChargingBull이 같은 오브젝트에 있습니다. " +
                    "애니메이션이 방향·판정을 흔들 수 있으니 GameObject > Molra > 선택한 오브젝트를 황소로 만들기 를 " +
                    "실행해 모델을 자식으로 내려 주세요.", this);
            }
        }

        ApplyVisualYaw();

        if (autoCreateCollider)
        {
            EnsureBodyCollider();
        }

        bodyCollider = FindBodyCollider();

        EnsureKillZone();
        EnsureRunSource();
    }

    // 치임 판정은 몸통과 따로 둔다. 발동 트리거(BullTriggerZone)가 황소의 자식이라
    // 그 트리거에 들어간 콜라이더가 황소 Rigidbody로도 전달되는데,
    // 여기서 사망 처리까지 하면 "트리거를 밟는 순간 죽는" 일이 생기기 때문이다.
    private void EnsureKillZone()
    {
        if (killZone == null)
        {
            killZone = GetComponentInChildren<BullKillZone>(true);
        }

        if (killZone != null)
        {
            killZone.Setup(this, deathAnimationTriggerName);
            return;
        }

        if (bodyCollider == null)
        {
            Debug.LogWarning($"{name}: 몸통 콜라이더가 없어 치임 판정(BullKillZone)을 만들지 못했습니다.", this);
            return;
        }

        GameObject zoneObject = new GameObject("BullKillZone");
        zoneObject.transform.SetParent(transform, false);

        // 몸통을 감싸는 크기로 만든다. 자기 좌표계 기준으로 재야 회전이 있어도 맞는다.
        Bounds worldBounds = bodyCollider.bounds;
        Vector3 localCenter = transform.InverseTransformPoint(worldBounds.center);
        Vector3 localExtents = transform.InverseTransformVector(worldBounds.extents);

        BoxCollider box = zoneObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = localCenter;
        box.size = new Vector3(
            Mathf.Abs(localExtents.x) * 2f,
            Mathf.Abs(localExtents.y) * 2f,
            Mathf.Abs(localExtents.z) * 2f) * Mathf.Max(0.1f, killZoneScale);

        killZone = zoneObject.AddComponent<BullKillZone>();
        killZone.Setup(this, deathAnimationTriggerName);

        // 사망 후 화면 연출도 같이 붙여 준다. 프리팹에 미리 붙여 둔 것과 같은 구성이 된다.
        if (zoneObject.GetComponentInParent<ElementalKillEffect>() == null)
        {
            zoneObject.AddComponent<ElementalKillEffect>();
        }
    }

    // 돌려 줄 모델. 따로 지정하지 않았으면 Animator가 붙은 자식을 쓴다.
    private Transform ResolveModelRoot()
    {
        if (modelRoot != null)
        {
            return modelRoot;
        }

        Transform target = animator != null ? animator.transform : null;

        // 모델이 본체와 같은 오브젝트면 돌릴 수가 없다(돌리면 판정까지 같이 돈다).
        return target == transform ? null : target;
    }

    // 모델이 향한 쪽과 실제 달려가는 쪽이 다를 때 모델만 돌려서 맞춘다.
    // 판정 상자와 돌진 방향은 본체(이 오브젝트)에 붙어 있으므로 영향을 받지 않는다.
    private void ApplyVisualYaw()
    {
        // 매 프레임 맞추는 쪽을 켜 뒀으면 LateUpdate가 알아서 한다.
        if (faceChargeDirection || Mathf.Approximately(visualYawOffset, 0f))
        {
            return;
        }

        Transform target = ResolveModelRoot();
        if (target == null)
        {
            Debug.LogWarning($"{name}: 돌려 줄 모델을 찾지 못해 '모델 회전 보정'을 적용하지 못했습니다.", this);
            return;
        }

        target.localRotation = Quaternion.Euler(0f, visualYawOffset, 0f) * target.localRotation;
    }

    // 애니메이터가 포즈를 다 쓴 뒤(LateUpdate)에 모델의 방향만 덮어쓴다.
    //
    // 클립이 모델의 회전 커브를 들고 있으면, 씬에서 황소를 어느 쪽으로 돌려 놓아도
    // 실행하는 순간 클립이 정한 한 방향으로 돌아가 버린다. Awake에서 한 번 돌려 주는 것으로는
    // 매 프레임 다시 쓰이는 애니메이션을 이길 수 없어서 여기서 맨 마지막에 맞춘다.
    private void LateUpdate()
    {
        if (!faceChargeDirection)
        {
            return;
        }

        Transform target = ResolveModelRoot();
        if (target == null)
        {
            return;
        }

        target.rotation = Quaternion.LookRotation(ChargeDirection, Vector3.up)
                          * Quaternion.Euler(0f, visualYawOffset, 0f);
    }

    private void EnsureRunSource()
    {
        if (runLoop == null)
        {
            return;
        }

        runSource = gameObject.AddComponent<AudioSource>();
        runSource.clip = runLoop;
        runSource.loop = true;
        runSource.playOnAwake = false;
        runSource.volume = runVolume;
        runSource.spatialBlend = 1f;
    }

    // 몸통 콜라이더(= 트리거가 아닌 것). 발동 트리거와 치임 판정은 제외된다.
    private Collider FindBodyCollider()
    {
        foreach (Collider candidate in GetComponentsInChildren<Collider>())
        {
            if (!candidate.isTrigger)
            {
                return candidate;
            }
        }

        return null;
    }

    // 모델만 있고 콜라이더가 없으면 바닥도 벽도 뚫고 지나간다.
    // 몸통 Renderer들의 크기를 재서 딱 맞는 BoxCollider를 하나 붙여 준다.
    private void EnsureBodyCollider()
    {
        if (FindBodyCollider() != null)
        {
            return;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Debug.LogWarning($"{name}: Renderer가 없어 황소 콜라이더를 만들지 못했습니다.", this);
            return;
        }

        // 회전을 뺀 자기 좌표계 기준으로 크기를 재야 박스가 몸통에 맞는다.
        Bounds local = new Bounds(Vector3.zero, Vector3.zero);
        bool started = false;

        foreach (Renderer renderer in renderers)
        {
            Bounds world = renderer.bounds;
            Vector3 center = transform.InverseTransformPoint(world.center);
            Vector3 extents = transform.InverseTransformVector(world.extents);
            Bounds piece = new Bounds(center, new Vector3(
                Mathf.Abs(extents.x) * 2f,
                Mathf.Abs(extents.y) * 2f,
                Mathf.Abs(extents.z) * 2f));

            if (!started)
            {
                local = piece;
                started = true;
            }
            else
            {
                local.Encapsulate(piece);
            }
        }

        BoxCollider box = gameObject.AddComponent<BoxCollider>();
        box.center = local.center;
        box.size = local.size;
    }

    private void OnEnable()
    {
        PlayerAutoWalker.Respawned += HandlePlayerRespawned;
    }

    private void OnDisable()
    {
        PlayerAutoWalker.Respawned -= HandlePlayerRespawned;
    }

    private void FixedUpdate()
    {
        // 회전 고정만으로는 부족하다. 무언가에 비스듬히 부딪히면 물리가 몸을 돌려 놓고,
        // 그러면 Local 기준 돌진 방향까지 같이 돌아가 엉뚱한 쪽으로 달린다.
        // 시작할 때의 회전을 매 프레임 다시 붙잡아 둔다.
        if (holdRotation && body.rotation != startRotation)
        {
            body.rotation = startRotation;

            if (!body.isKinematic)
            {
                body.angularVelocity = Vector3.zero;
            }
        }

        if (!isCharging)
        {
            return;
        }

        // 충돌 반응만 믿으면 빠른 속도에서 벽을 스치거나 뚫고 지나갈 수 있다.
        // 이번 프레임에 지나갈 만큼 앞을 미리 훑어서, 벽이 있으면 닿기 전에 멈춘다.
        if (IsBlockerAhead())
        {
            StopBlocked();
            return;
        }

        // 벽과 같은 방식으로 플레이어도 앞을 훑어 찾는다.
        // 몸통 콜라이더(트리거가 아닌 것)가 플레이어를 밀어내기 때문에,
        // 몸통 안쪽에 있는 받힘 판정 트리거에 들어오기를 기다리면 그냥 밀고 지나가 버린다.
        HitPlayerAhead();

        // 받는 순간 BullKillZone이 황소를 멈춰 세우므로 여기서 한 번 더 확인한다.
        if (!isCharging)
        {
            return;
        }

        if (plowThroughObstacles)
        {
            MovePlowing();
        }
        else
        {
            // 중력에 의한 낙하(y)는 그대로 두고 수평 이동만 덮어쓴다.
            Vector3 run = ChargeDirection * speed;
            body.linearVelocity = new Vector3(run.x, body.linearVelocity.y, run.z);
        }
    }

    // 돌진하는 동안에는 Rigidbody를 Kinematic으로 두고 직접 옮긴다.
    //
    // 예전에는 충돌한 콜라이더를 Physics.IgnoreCollision으로 하나씩 꺼서 뚫고 갔는데,
    // 접촉점 하나만 보고 판단하는 데다 한 번 끄면 되돌리기 전까지 계속 꺼져 있어서
    // 바닥이 통째로 사라지고 땅으로 꺼지는 일이 있었다.
    //
    // Kinematic이면 지형에 밀리지도, 걸려 멈추지도 않는다. 대신 중력도 안 받으므로
    // 발밑을 훑어 바닥 높이에 붙여 준다. 날려버리기는 OverlapBox로 직접 찾아서 한다.
    private void MovePlowing()
    {
        Vector3 next = body.position + ChargeDirection * (speed * Time.fixedDeltaTime);
        next = SnapToGround(next);

        body.MovePosition(next);

        KnockbackOverlapping();
    }

    // 발밑에서 바닥을 찾아 그 높이에 세운다. 못 찾으면 높이를 그대로 둔다(허공에 뜬 채 지나간다).
    private Vector3 SnapToGround(Vector3 position)
    {
        // 턱을 타고 올라갈 수 있도록 조금 위에서 쏜다.
        float rise = Mathf.Max(0.5f, groundProbeDepth * 0.5f);
        Vector3 origin = position + Vector3.up * rise;

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            rise + groundProbeDepth,
            groundLayers,
            QueryTriggerInteraction.Ignore);

        float best = float.NegativeInfinity;
        foreach (RaycastHit hit in hits)
        {
            // 자기 몸을 바닥으로 착각하지 않게 거른다.
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            // 플레이어도 바닥이 아니다. 걸러내지 않으면 코앞의 플레이어를 밟고 올라서서
            // 받힘 판정이 머리 위를 지나가 버린다.
            if (PlayerLocator.FindPlayerRoot(hit.collider.transform) != null)
            {
                continue;
            }

            if (hit.point.y > best)
            {
                best = hit.point.y;
            }
        }

        if (float.IsNegativeInfinity(best))
        {
            return position;
        }

        position.y = best + groundOffset;
        return position;
    }

    // Kinematic으로 움직이는 동안에는 충돌 콜백을 기대할 수 없다.
    // 몸통이 겹친 것을 직접 찾아 날린다.
    private void KnockbackOverlapping()
    {
        if (!TryGetBodyBox(out Vector3 center, out Vector3 halfExtents, out Quaternion rotation))
        {
            return;
        }

        Collider[] overlapping = Physics.OverlapBox(
            center,
            halfExtents,
            rotation,
            ~0,
            QueryTriggerInteraction.Ignore);

        foreach (Collider other in overlapping)
        {
            if (IsBlocker(other))
            {
                StopBlocked();
                return;
            }

            // 이미 몸통에 겹쳐 버린 플레이어. 앞을 훑는 검사가 놓쳤을 때의 마지막 그물이다.
            if (ReportPlayerHit(other))
            {
                return;
            }

            Knockback(other);
        }
    }

    // 몸통이 닿기 직전의 플레이어를 찾아 받힘 판정에 넘긴다.
    private void HitPlayerAhead()
    {
        if (killZone == null)
        {
            return;
        }

        if (!TryGetBodyBox(out Vector3 center, out Vector3 halfExtents, out Quaternion rotation))
        {
            return;
        }

        float reach = speed * Time.fixedDeltaTime + Mathf.Max(0f, playerHitLookAhead);

        RaycastHit[] hits = Physics.BoxCastAll(
            center,
            halfExtents * 0.95f,
            ChargeDirection,
            rotation,
            reach,
            ~0,
            QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits)
        {
            if (ReportPlayerHit(hit.collider))
            {
                return;
            }
        }
    }

    // 플레이어면 받힘 판정(BullKillZone)에 넘기고 true.
    // 사망 연출·날려버리기·게임 오버 창은 전부 그쪽이 맡는다.
    private bool ReportPlayerHit(Collider other)
    {
        if (killZone == null || other == null)
        {
            return false;
        }

        if (PlayerLocator.FindPlayerRoot(other.transform) == null)
        {
            return false;
        }

        killZone.ReportHit(other);
        return true;
    }

    // 몸통 상자를 월드 기준으로 낸다. 겹침 검사와 벽 감지가 같은 상자를 쓴다.
    private bool TryGetBodyBox(out Vector3 center, out Vector3 halfExtents, out Quaternion rotation)
    {
        center = Vector3.zero;
        halfExtents = Vector3.zero;
        rotation = Quaternion.identity;

        if (bodyCollider == null)
        {
            return false;
        }

        if (bodyCollider is BoxCollider box)
        {
            Transform owner = box.transform;
            center = owner.TransformPoint(box.center);
            halfExtents = Vector3.Scale(box.size * 0.5f, AbsoluteScale(owner.lossyScale));
            rotation = owner.rotation;
            return true;
        }

        // 상자가 아니면 월드 AABB로 대신한다. 회전이 없는 셈 치는 것이라 조금 넉넉해진다.
        Bounds bounds = bodyCollider.bounds;
        center = bounds.center;
        halfExtents = bounds.extents;
        rotation = Quaternion.identity;
        return true;
    }

    private static Vector3 AbsoluteScale(Vector3 scale)
    {
        return new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
    }

    // 이 콜라이더가 황소를 멈춰 세우는 벽인지.
    // 흑요석이 생기면 여기만 고치면 된다(예: GetComponentInParent<ObsidianWall>()).
    private bool IsBlocker(Collider other)
    {
        if (other == null)
        {
            return false;
        }

        return blockedByStoneWall && other.GetComponentInParent<CarBlocker>() != null;
    }

    private bool IsBlockerAhead()
    {
        if (!TryGetBodyBox(out Vector3 center, out Vector3 halfExtents, out Quaternion rotation))
        {
            return false;
        }

        // 이번 물리 프레임 이동 거리 + 여유. 몸통이 벽에 파묻히기 전에 걸린다.
        float reach = speed * Time.fixedDeltaTime + blockerLookAhead;

        RaycastHit[] hits = Physics.BoxCastAll(
            center,
            halfExtents * 0.95f,
            ChargeDirection,
            rotation,
            reach,
            ~0,
            QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits)
        {
            if (!IsBlocker(hit.collider))
            {
                continue;
            }

            // BoxCast는 쏘는 순간 이미 겹쳐 있는 콜라이더를 거리 0으로 돌려준다.
            // 그것까지 벽으로 치면, 몸통이 스치기만 해도 출발하자마자 멈춰 버린다.
            // (흑요석처럼 바닥에 넓게 깔리는 설치물은 황소 발밑과 겹치기 쉽다)
            // 진짜로 앞을 막고 선 것만 벽으로 친다.
            if (hit.distance <= 0f)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    public void Charge()
    {
        if (isCharging)
        {
            return;
        }

        isCharging = true;

        // 지형에 밀리거나 걸리지 않도록 돌진하는 동안만 Kinematic으로 둔다.
        if (plowThroughObstacles)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }

        SetChargeAnimation(true);
        SfxPlayer.PlayAt(chargeSound, transform.position, chargeVolume);

        if (runSource != null)
        {
            runSource.Play();
        }
    }

    public void Stop()
    {
        if (!isCharging)
        {
            return;
        }

        isCharging = false;

        // 돌진하려고 켜 둔 Kinematic을 되돌린다. 다시 중력을 받고 땅에 내려앉는다.
        if (body.isKinematic != startedKinematic)
        {
            body.isKinematic = startedKinematic;
        }

        if (!body.isKinematic)
        {
            body.linearVelocity = new Vector3(0f, body.linearVelocity.y, 0f);
        }

        SetChargeAnimation(false);

        if (runSource != null)
        {
            runSource.Stop();
        }
    }

    // 벽에 막혀 멈추는 경우. 부딪히는 소리를 함께 울린다.
    private void StopBlocked()
    {
        if (!isCharging)
        {
            return;
        }

        Stop();
        SfxPlayer.PlayAt(blockedSound, transform.position, blockedVolume);
    }

    // 파라미터가 없는 컨트롤러에서도 터지지 않게 확인하고 건다.
    // 다만 조용히 넘어가면 "왜 안 움직이지?"를 알 수 없으므로 이유를 한 번 찍어 준다.
    private void SetChargeAnimation(bool charging)
    {
        if (string.IsNullOrEmpty(chargeAnimationBoolName))
        {
            return;
        }

        if (animator == null)
        {
            WarnAboutAnimationOnce("황소 모델에서 Animator를 찾지 못했습니다.");
            return;
        }

        if (animator.runtimeAnimatorController == null)
        {
            WarnAboutAnimationOnce(
                $"Animator에 Controller가 비어 있어 '{chargeAnimationBoolName}' 애니메이션을 재생할 수 없습니다. " +
                "Tools > Molra > 황소 애니메이터 만들기 를 실행하면 자동으로 만들어 붙여 줍니다.");
            return;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name != chargeAnimationBoolName)
            {
                continue;
            }

            if (parameter.type == AnimatorControllerParameterType.Bool)
            {
                animator.SetBool(chargeAnimationBoolName, charging);
            }
            else if (parameter.type == AnimatorControllerParameterType.Trigger && charging)
            {
                animator.SetTrigger(chargeAnimationBoolName);
            }

            return;
        }

        WarnAboutAnimationOnce(
            $"Animator Controller '{animator.runtimeAnimatorController.name}'에 " +
            $"'{chargeAnimationBoolName}' 파라미터가 없습니다. 이름을 맞추거나 인스펙터에서 바꿔 주세요.");
    }

    private void WarnAboutAnimationOnce(string reason)
    {
        if (animationWarned)
        {
            return;
        }

        animationWarned = true;
        Debug.LogWarning($"{name}: {reason}", this);
    }

    public void ResetToStart()
    {
        Stop();
        isCharging = false;
        knockedBack.Clear();

        body.isKinematic = startedKinematic;

        if (!body.isKinematic)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        body.position = startPosition;
        body.rotation = startRotation;
    }

    private void HandlePlayerRespawned()
    {
        if (resetOnPlayerRespawn)
        {
            ResetToStart();
        }
    }

    // 물리로 달릴 때(뚫고 지나가기를 끈 경우)의 경로.
    // Kinematic으로 달릴 때는 충돌 콜백이 오지 않으므로 KnockbackOverlapping이 대신한다.
    private void OnCollisionEnter(Collision collision)
    {
        HandleContact(collision.collider, true);
    }

    private void OnCollisionStay(Collision collision)
    {
        HandleContact(collision.collider, true);
    }

    // 자식으로 둔 감지 범위(BullTriggerZone)와 받힘 판정(BullKillZone)에 들어간 콜라이더는
    // 황소 Rigidbody로도 전달된다. 그것까지 날려 버리면 아직 닿지도 않은 물체가
    // 감지 범위에 들어서는 순간 튕겨 나가므로, 트리거 경로에서는 벽 막힘만 본다.
    private void OnTriggerEnter(Collider other)
    {
        HandleContact(other, false);
    }

    private void HandleContact(Collider other, bool touched)
    {
        if (!isCharging)
        {
            return;
        }

        // 돌 벽에 막히면 멈춘다. 이건 날리지 않는다.
        if (IsBlocker(other))
        {
            StopBlocked();
            return;
        }

        // 몸통이 플레이어에 닿았다면 받은 것이다.
        if (touched && ReportPlayerHit(other))
        {
            return;
        }

        if (touched)
        {
            Knockback(other);
        }
    }

    // 앞을 가로막은 물체를 날린다. 플레이어는 BullKillZone이 사망 처리와 함께 날리므로 여기서 뺀다.
    private void Knockback(Collider other)
    {
        Rigidbody target = other.attachedRigidbody;
        if (target == null || target == body || target.isKinematic)
        {
            return;
        }

        // 황소 자신의 일부(자식 콜라이더)는 밀지 않는다.
        if (target.transform.IsChildOf(transform))
        {
            return;
        }

        if (PlayerLocator.FindPlayerRoot(other.transform) != null)
        {
            return;
        }

        if (maxKnockbackMass > 0f && target.mass > maxKnockbackMass)
        {
            return;
        }

        if (!knockedBack.Add(target))
        {
            return;
        }

        // 무게와 상관없이 똑같이 시원하게 날아가도록 속도를 직접 바꾼다.
        Vector3 launch = ChargeDirection * knockbackSpeed + Vector3.up * knockbackLift;
        target.AddForce(launch, ForceMode.VelocityChange);

        if (knockbackSpin > 0f)
        {
            target.AddTorque(Random.onUnitSphere * knockbackSpin, ForceMode.VelocityChange);
        }

        SfxPlayer.PlayAt(impactSound, target.position, impactVolume);
    }

    // 씬에 놓을 때 돌진 방향을 눈으로 확인할 수 있게 그려 준다.
    // 인스펙터에서 방향을 바꾸면 이 화살표가 바로 따라 움직이므로 보면서 맞추면 된다.
    private void OnDrawGizmosSelected()
    {
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Vector3 direction = ChargeDirection;
        Vector3 tip = origin + direction * 5f;

        Gizmos.color = new Color(0.9f, 0.4f, 0.3f, 0.9f);
        Gizmos.DrawLine(origin, tip);

        // 화살촉. 어느 쪽이 앞인지 한눈에 보이게 한다.
        Vector3 side = Vector3.Cross(direction, Vector3.up).normalized * 0.4f;
        Gizmos.DrawLine(tip, tip - direction * 0.8f + side);
        Gizmos.DrawLine(tip, tip - direction * 0.8f - side);
    }
}
