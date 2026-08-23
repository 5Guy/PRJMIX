using System.Collections;
using UnityEngine;

// 플레이어가 바라보는 방향(forward)으로 자동으로 계속 걸어가게 한다.
// Rigidbody가 이 오브젝트가 아니라 자식(예: Capsule)에 붙어 있어도 동작한다.
// 함정에 닿아 사망하면 ElementTrapCube가 IPlayerKillable.OnKilled()를 호출한다.
// 사망 시에는 즉시 멈추지 않고 속도를 서서히 줄인 뒤, 잠시 후 시작 지점으로 리스폰한다.
public class PlayerAutoWalker : MonoBehaviour, IPlayerKillable
{
    [Header("이동")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private bool walkOnStart = true;

    [Header("사망 / 리스폰")]
    [Tooltip("사망 후 속도가 0까지 줄어드는 데 걸리는 시간(초)")]
    [SerializeField] private float slowDownDuration = 0.4f;
    [Tooltip("사망 후 리스폰까지 기다리는 시간(초)")]
    [SerializeField] private float respawnDelay = 1.5f;
    [Tooltip("비워두면 게임 시작 위치로 리스폰한다")]
    [SerializeField] private Transform respawnPoint;

    [Header("날아감")]
    [Tooltip("황소에 받혀 날아가는 동안 몸이 굴러가도록 회전 고정을 잠시 푼다")]
    [SerializeField] private bool tumbleWhileLaunched = true;

    private Rigidbody body;
    private Animator animator;
    private Transform modelTransform;
    private Vector3 modelLocalPosition;
    private Quaternion modelLocalRotation;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private RigidbodyConstraints walkConstraints;
    private float currentSpeed;
    private bool isWalking;
    private bool isDead;
    private bool isLaunched;
    private bool isRiding;
    private float rideSpeed;
    private Vector3 rideDirection;
    private Coroutine respawnRoutine;

    public bool IsDead => isDead;

    // 리스폰 직전 물 함정처럼 임시로 바꾼 플레이어 상태를 되돌리기 위한 알림.
    public static event System.Action<Transform> BeforeRespawn;

    // 리스폰 시 자동차/트리거 등 스테이지 요소를 함께 되돌리기 위한 알림.
    public static event System.Action Respawned;

    private void Awake()
    {
        // Rigidbody는 보통 자식 Capsule에 있으므로 자기 자신 -> 자식 순으로 찾는다.
        body = GetComponent<Rigidbody>();
        if (body == null)
        {
            body = GetComponentInChildren<Rigidbody>();
        }

        if (body == null)
        {
            Debug.LogError($"{name}: PlayerAutoWalker가 Rigidbody를 찾지 못해 이동할 수 없습니다.", this);
            enabled = false;
            return;
        }

        // 이동은 Rigidbody 속도로만 처리한다.
        // 루트 모션을 켜두면 애니메이션이 모델 트랜스폼을 따로 움직여서
        // 모델이 콜라이더와 다른 속도로 혼자 앞서 나가므로 반드시 꺼야 한다.
        animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.applyRootMotion = false;
            modelTransform = animator.transform;
            modelLocalPosition = modelTransform.localPosition;
            modelLocalRotation = modelTransform.localRotation;
        }

        // 걷는 중 콜라이더가 넘어지지 않도록 회전을 고정한다.
        body.constraints |= RigidbodyConstraints.FreezeRotation;

        // 날아갈 때 잠시 풀었다가 리스폰할 때 그대로 되돌리기 위해 원래 값을 기억해 둔다.
        walkConstraints = body.constraints;

        // 리스폰 기준점은 Rigidbody의 시작 위치(= 실제로 움직이는 몸통 기준)로 잡는다.
        startPosition = body.position;
        startRotation = body.rotation;

        isWalking = walkOnStart;
    }

    private void FixedUpdate()
    {
        // 날아가는 동안에는 속도를 건드리지 않는다.
        // 여기서 매 프레임 속도를 덮어쓰면 받힌 힘이 그대로 지워져 제자리에서 죽어 버린다.
        if (isLaunched)
        {
            return;
        }

        if (isDead)
        {
            // 사망 후에는 목표 속도 0까지 서서히 감속한다.
            float deceleration = slowDownDuration > 0f ? moveSpeed / slowDownDuration : float.MaxValue;
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, deceleration * Time.fixedDeltaTime);
        }
        else if (isRiding)
        {
            // 파도에 실려 가는 동안에는 걷기 속도 대신 파도 속도로 밀려간다.
            currentSpeed = rideSpeed;
        }
        else
        {
            currentSpeed = isWalking ? moveSpeed : 0f;
        }

        // 파도에 실려 갈 때는 파도가 흐르는 방향으로 밀려간다.
        // 모델의 forward가 뒤집혀 있어도 엉뚱한 쪽으로 날아가지 않도록 방향을 따로 받는다.
        Vector3 heading = isRiding && rideDirection.sqrMagnitude > 0.0001f
            ? rideDirection
            : body.transform.forward;

        // 중력에 의한 낙하(y)는 그대로 두고 수평 이동만 덮어쓴다.
        Vector3 move = heading * currentSpeed;
        body.linearVelocity = new Vector3(move.x, body.linearVelocity.y, move.z);
    }

    public void StartWalking()
    {
        if (isDead)
        {
            return;
        }

        isWalking = true;
    }

    public void StopWalking()
    {
        isWalking = false;
    }

    // 쓰나미(TsunamiCounterEffect)처럼 플레이어를 태워 밀고 가는 연출이 부른다.
    // Launch와 달리 사망 처리가 아니라서, 파도가 지나가면 EndRide로 걷기 상태로 그대로 돌아온다.
    // direction을 비워 두면(Vector3.zero) 평소처럼 몸이 바라보는 쪽으로 밀려간다.
    public void BeginRide(float speed, Vector3 direction)
    {
        if (isDead)
        {
            return;
        }

        isRiding = true;
        rideSpeed = speed;
        rideDirection = Vector3.ProjectOnPlane(direction, Vector3.up);

        if (rideDirection.sqrMagnitude > 0.0001f)
        {
            rideDirection.Normalize();
        }

        isWalking = true;

        Debug.Log($"{name}: 파도에 실렸습니다. 속도 {speed:0.0}m/s, 방향 {rideDirection}", this);
    }

    public void EndRide()
    {
        isRiding = false;
        rideSpeed = 0f;
        rideDirection = Vector3.zero;
    }

    // 황소(BullKillZone)처럼 밀어내는 함정이 부른다.
    // 사망 처리와 함께 쓰면 감속하는 대신 물리에 몸을 맡긴 채 날아가고,
    // 리스폰할 때 회전 고정과 걷기 상태가 원래대로 돌아온다.
    public void Launch(Vector3 velocity)
    {
        isLaunched = true;
        isWalking = false;
        currentSpeed = 0f;

        if (tumbleWhileLaunched)
        {
            body.constraints = walkConstraints & ~RigidbodyConstraints.FreezeRotation;
        }

        body.AddForce(velocity, ForceMode.VelocityChange);
    }

    public void OnKilled()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        isWalking = false;
        respawnRoutine = StartCoroutine(DeathAndRespawnRoutine());
    }

    // 게임 오버처럼 판이 그대로 끝나는 경우, 예약해 둔 리스폰을 거둔다.
    // (황소에게 받히면 BullKillZone이 부른다)
    public void CancelRespawn()
    {
        if (respawnRoutine == null)
        {
            return;
        }

        StopCoroutine(respawnRoutine);
        respawnRoutine = null;
    }

    private IEnumerator DeathAndRespawnRoutine()
    {
        yield return new WaitForSeconds(slowDownDuration + respawnDelay);
        respawnRoutine = null;
        BeforeRespawn?.Invoke(transform);
        Respawn();
    }

    public void Respawn()
    {
        // 밖에서 곧바로 불렀을 때 예약해 둔 리스폰이 뒤늦게 또 도는 일이 없도록 거둔다.
        CancelRespawn();

        Vector3 target = respawnPoint != null ? respawnPoint.position : startPosition;
        Quaternion targetRotation = respawnPoint != null ? respawnPoint.rotation : startRotation;

        // 날아가느라 풀어 둔 회전 고정을 먼저 되돌려야 리스폰한 뒤 넘어진 채로 서 있지 않는다.
        isLaunched = false;
        isRiding = false;
        rideSpeed = 0f;
        rideDirection = Vector3.zero;
        body.constraints = walkConstraints;

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.position = target;
        body.rotation = targetRotation;

        // Rebind로 Dying 상태와 Die 트리거를 함께 초기화해서 기본(걷기) 상태로 되돌린다.
        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0f);
        }

        // 애니메이션이 모델을 밀어냈을 경우를 대비해 콜라이더 기준 위치로 다시 붙인다.
        if (modelTransform != null)
        {
            modelTransform.localPosition = modelLocalPosition;
            modelTransform.localRotation = modelLocalRotation;
        }

        currentSpeed = 0f;
        isDead = false;
        isWalking = true;

        Respawned?.Invoke();

        // 자동차·트리거뿐 아니라 함정과 배치해 둔 원소까지 처음 상태로 되돌린다.
        // (시작 버튼이 다시 나타나 StopWalking을 걸어 주므로 순서상 이 뒤에 와야 한다)
        StageReset.Request(StageReset.Reason.PlayerDeath);
    }
}
