using System.Collections;
using UnityEngine;

// 맵에 미리 배치해 두는 자동차.
// 트리거 존(CarTriggerZone)이 발동하면 자기 forward 방향으로 직진하며 달려온다.
// - 앞쪽 치임 트리거(CarKillZone)에 플레이어가 들어오면 사망 처리를 맡긴다.
// - CarBlocker가 붙은 벽(흙 원소로 세운 벽 포함)에 부딪히면 멈춘다. 그러면 플레이어는 죽지 않는다.
//
// 파훼 방법에 따라 멈추는 모습이 다르다. 어느 쪽이든 달리기를 멈추므로(IsCharging = false)
// 치임 판정은 그 순간부터 나지 않는다.
//   흙 벽       : StopBlocked  — 그 자리에 선다
//   흑요석 벽   : Explode      — 그 자리에서 터진다
//   바람        : BlowAway     — 들려서 뒤로 날아간다
//   늪          : SinkInto     — 앞쪽부터 박히며 가라앉는다
[RequireComponent(typeof(Rigidbody))]
public class ChargingCar : MonoBehaviour
{
    [Header("이동")]
    [SerializeField] private float speed = 12f;

    [Header("사망 연출")]
    [SerializeField] private string deathAnimationTriggerName = "Die";

    [Header("리셋")]
    [Tooltip("플레이어가 리스폰하면 자동차도 시작 위치로 되돌린다")]
    [SerializeField] private bool resetOnPlayerRespawn = true;

    [Header("충돌체")]
    [Tooltip("차체에 콜라이더가 없으면 모델 크기에 맞춘 BoxCollider를 자동으로 붙인다")]
    [SerializeField] private bool autoCreateCollider = true;
    [Tooltip("돌 벽을 몇 미터 앞에서부터 알아채고 멈출지")]
    [SerializeField] private float blockerLookAhead = 0.6f;

    [Header("치임 판정")]
    [Tooltip("비워두면 차체 크기에 맞춘 트리거(CarKillZone)를 자동으로 만들어 붙인다")]
    [SerializeField] private CarKillZone killZone;
    [Tooltip("자동으로 만들 때, 차체 크기 대비 치임 판정의 크기 비율. 1보다 작으면 차체 안에 묻혀 판정이 나지 않으므로 1 미만은 1로 취급한다")]
    [SerializeField] private float killZoneScale = 1f;
    [Tooltip("자동으로 만들 때, 차체 표면 바깥으로 치임 판정을 얼마나 내밀지(m)")]
    [SerializeField] private float killZonePadding = 0.2f;

    [Header("사운드")]
    [Tooltip("출발할 때 한 번 울릴 소리")]
    [SerializeField] private AudioClip launchSound;
    [Range(0f, 1f)]
    [SerializeField] private float launchVolume = 1f;
    [Tooltip("벽에 막혀 멈출 때 울릴 소리")]
    [SerializeField] private AudioClip blockedSound;
    [Range(0f, 1f)]
    [SerializeField] private float blockedVolume = 1f;
    [Tooltip("달리는 동안 계속 울릴 엔진 소리(선택). 넣으면 반복 재생된다")]
    [SerializeField] private AudioClip engineLoop;
    [Range(0f, 1f)]
    [SerializeField] private float engineVolume = 0.6f;

    [Header("파훼 연출")]
    [Tooltip("터진 뒤 잔해가 남아 있는 시간(초)")]
    [SerializeField] private float wreckLifetime = 2.5f;
    [Tooltip("바람에 날아간 뒤 차가 사라지기까지의 시간(초). 도로로 되굴러와 길을 막지 않게 한다")]
    [SerializeField] private float blowAwayLifetime = 3f;
    [Tooltip("흑요석 벽에 부딪혀 터질 때 울릴 소리")]
    [SerializeField] private AudioClip explodeSound;
    [Range(0f, 1f)]
    [SerializeField] private float explodeVolume = 1f;

    private Rigidbody body;
    private Collider bodyCollider;
    private AudioSource engineSource;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private RigidbodyConstraints startConstraints;
    private bool isCharging;
    private bool isWrecked;

    public bool IsCharging => isCharging;

    // 터졌거나 날아갔거나 늪에 빠져서 더 이상 위협이 아닌 상태.
    public bool IsWrecked => isWrecked;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();

        // 달리는 도중 뒤집히지 않도록 회전을 고정한다.
        // 바람에 날아갈 때는 이 고정을 잠깐 풀어야 하므로, 원래 값을 기억해 두었다가 되돌린다.
        body.constraints |= RigidbodyConstraints.FreezeRotation;
        startConstraints = body.constraints;

        startPosition = body.position;
        startRotation = body.rotation;

        if (autoCreateCollider)
        {
            EnsureBodyCollider();
        }

        bodyCollider = FindBodyCollider();

        EnsureKillZone();
        EnsureEngineSource();
    }

    // 치임 판정은 차체와 따로 둔다. 출발 트리거(CarTriggerZone)에 들어간 콜라이더가
    // 자동차 Rigidbody로 전달되면서 사망 처리까지 같이 일어나던 문제를 막기 위해서다.
    private void EnsureKillZone()
    {
        if (killZone == null)
        {
            killZone = GetComponentInChildren<CarKillZone>();
        }

        if (killZone != null)
        {
            killZone.Setup(this, deathAnimationTriggerName);
            return;
        }

        if (bodyCollider == null)
        {
            Debug.LogWarning($"{name}: 차체 콜라이더가 없어 치임 판정(CarKillZone)을 만들지 못했습니다.", this);
            return;
        }

        GameObject zoneObject = new GameObject("CarKillZone");
        zoneObject.transform.SetParent(transform, false);

        // 차체를 감싸는 크기로 만든다. 자기 좌표계 기준으로 재야 회전이 있어도 맞는다.
        Bounds worldBounds = bodyCollider.bounds;
        Vector3 localCenter = transform.InverseTransformPoint(worldBounds.center);
        Vector3 localExtents = transform.InverseTransformVector(worldBounds.extents);

        BoxCollider box = zoneObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = localCenter;

        Vector3 localSize = new Vector3(
            Mathf.Abs(localExtents.x) * 2f,
            Mathf.Abs(localExtents.y) * 2f,
            Mathf.Abs(localExtents.z) * 2f);

        // 차체 콜라이더는 솔리드라 플레이어가 차체 안쪽까지 파고들지 못한다.
        // 치임 판정을 차체보다 작게 잡으면(예전 기본값 0.9) 트리거에 닿을 일이 없어 치여도 죽지 않는다.
        // 차체 표면을 살짝 넘어서도록 키워서, 부딪히는 순간 판정이 나게 한다.
        // 차체 스케일이 1이 아닐 수 있으므로 여유분은 월드 기준으로 재서 로컬로 환산한다.
        Vector3 localPadding = transform.InverseTransformVector(Vector3.one * Mathf.Max(0f, killZonePadding));

        box.size = localSize * Mathf.Max(1f, killZoneScale) + new Vector3(
            Mathf.Abs(localPadding.x),
            Mathf.Abs(localPadding.y),
            Mathf.Abs(localPadding.z));

        killZone = zoneObject.AddComponent<CarKillZone>();
        killZone.Setup(this, deathAnimationTriggerName);
    }

    private void EnsureEngineSource()
    {
        if (engineLoop == null)
        {
            return;
        }

        engineSource = gameObject.AddComponent<AudioSource>();
        engineSource.clip = engineLoop;
        engineSource.loop = true;
        engineSource.playOnAwake = false;
        engineSource.volume = engineVolume;
        engineSource.spatialBlend = 1f;
    }

    // 차체 콜라이더(= 트리거가 아닌 것). 출발 트리거 존은 제외한다.
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

    // 모델만 있고 콜라이더가 없으면 벽에도 바닥에도 부딪히지 못하고 그대로 통과한다.
    // 차체 Renderer들의 크기를 재서 딱 맞는 BoxCollider를 하나 붙여 준다.
    private void EnsureBodyCollider()
    {
        // 출발 트리거(CarTriggerZone)는 차체가 아니므로 세지 않는다.
        if (FindBodyCollider() != null)
        {
            return;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Debug.LogWarning($"{name}: Renderer가 없어 자동차 콜라이더를 만들지 못했습니다.", this);
            return;
        }

        // 회전을 뺀 자기 좌표계 기준으로 크기를 재야 박스가 차체에 맞는다.
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
        if (!isCharging)
        {
            return;
        }

        // 충돌 반응만 믿으면 빠른 속도에서 벽을 스치거나 뚫고 지나갈 수 있다.
        // 이번 프레임에 지나갈 만큼 앞을 미리 훑어서, 벽이 있으면 닿기 전에 반응한다.
        CarBlocker blocker = FindBlockerAhead();
        if (blocker != null)
        {
            blocker.OnCarBlocked(this);
            return;
        }

        // 중력에 의한 낙하(y)는 그대로 두고 수평 이동만 덮어쓴다.
        Vector3 forward = transform.forward * speed;
        body.linearVelocity = new Vector3(forward.x, body.linearVelocity.y, forward.z);
    }

    // 앞쪽에 있는 벽을 돌려준다. 없으면 null.
    // 어떤 벽이냐에 따라 반응이 달라져서(멈춤 / 폭발) 있는지만이 아니라 벽 자체를 돌려준다.
    private CarBlocker FindBlockerAhead()
    {
        if (bodyCollider == null)
        {
            return null;
        }

        // 이번 물리 프레임 이동 거리 + 여유. 차체가 벽에 파묻히기 전에 걸린다.
        float reach = speed * Time.fixedDeltaTime + blockerLookAhead;

        Bounds bounds = bodyCollider.bounds;
        Vector3 halfExtents = bounds.extents * 0.95f;

        RaycastHit[] hits = Physics.BoxCastAll(
            bounds.center,
            halfExtents,
            transform.forward,
            transform.rotation,
            reach,
            ~0,
            QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
            {
                continue;
            }

            CarBlocker blocker = hit.collider.GetComponentInParent<CarBlocker>();
            if (blocker != null)
            {
                return blocker;
            }
        }

        return null;
    }

    public void Launch()
    {
        if (isCharging)
        {
            return;
        }

        isCharging = true;

        SfxPlayer.PlayAt(launchSound, transform.position, launchVolume);

        if (engineSource != null)
        {
            engineSource.Play();
        }
    }

    public void Stop()
    {
        if (!isCharging)
        {
            return;
        }

        isCharging = false;
        body.linearVelocity = new Vector3(0f, body.linearVelocity.y, 0f);

        if (engineSource != null)
        {
            engineSource.Stop();
        }
    }

    // 벽에 막혀 멈추는 경우. 부딪히는 소리를 함께 울린다.
    // 흙 벽(CarBlocker)의 기본 반응이라 벽 쪽에서 부른다.
    public void StopBlocked()
    {
        if (!isCharging)
        {
            return;
        }

        Stop();
        SfxPlayer.PlayAt(blockedSound, transform.position, blockedVolume);
    }

    // 흑요석 벽에 부딪혔을 때. 그 자리에서 터지고 차체는 사라진다.
    public void Explode()
    {
        if (isWrecked || !isCharging)
        {
            return;
        }

        isWrecked = true;

        Bounds bounds = bodyCollider != null ? bodyCollider.bounds : new Bounds(transform.position, Vector3.one);

        Stop();
        DisableKillZone();

        // 잔해가 굴러가지 않도록 물리는 여기서 멈춰 둔다. 되돌릴 때 다시 켠다.
        body.isKinematic = true;

        SfxPlayer.PlayAt(explodeSound, bounds.center, explodeVolume);
        CarWreckEffects.Explosion(bounds.center, bounds.size.magnitude * 0.5f, Mathf.Max(0.2f, wreckLifetime));

        SetVisible(false);
    }

    // 바람에 휩쓸렸을 때. 들려 올라가 뒤로 날아가며 빙글빙글 돈다.
    public void BlowAway(Vector3 direction, float force, float lift, float spin)
    {
        if (isWrecked || !isCharging)
        {
            return;
        }

        isWrecked = true;

        // Stop()은 속도를 지워 버린다. 달리던 기세는 살린 채로 달리기만 멈춘다.
        isCharging = false;

        if (engineSource != null)
        {
            engineSource.Stop();
        }

        DisableKillZone();

        // 공중에서 뒤집혀야 날아가는 느낌이 나므로 회전 고정을 푼다.
        body.isKinematic = false;
        body.constraints = RigidbodyConstraints.None;

        Vector3 push = direction.normalized * force + Vector3.up * lift;
        body.AddForce(push, ForceMode.VelocityChange);
        body.AddTorque(Random.onUnitSphere * spin, ForceMode.VelocityChange);

        StartCoroutine(HideAfter(blowAwayLifetime));
    }

    // 늪에 빠졌을 때. 앞쪽이 박히며 기울어진 채로 가라앉는다.
    public void SinkInto(float depth, float pitchAngle, float duration)
    {
        if (isWrecked || !isCharging)
        {
            return;
        }

        isWrecked = true;

        Stop();
        DisableKillZone();

        // 늪은 밀어내는 게 아니라 빨아들이는 것이라, 물리에 맡기지 않고 직접 끌어내린다.
        // 그대로 두면 바닥 콜라이더에 걸려 가라앉지 않는다.
        body.isKinematic = true;

        StartCoroutine(SinkRoutine(depth, pitchAngle, duration));
    }

    private IEnumerator SinkRoutine(float depth, float pitchAngle, float duration)
    {
        Vector3 from = transform.position;
        Vector3 to = from + Vector3.down * Mathf.Max(0.1f, depth);
        Quaternion fromRotation = transform.rotation;

        // x축으로 양수만큼 돌리면 앞이 아래로 처진다.
        Quaternion toRotation = fromRotation * Quaternion.Euler(pitchAngle, 0f, 0f);

        float life = Mathf.Max(0.05f, duration);

        for (float t = 0f; t < life; t += Time.deltaTime)
        {
            float k = t / life;

            // 앞이 먼저 푹 꺼졌다가 천천히 잠기도록 앞부분을 빠르게 기울인다.
            transform.SetPositionAndRotation(
                Vector3.Lerp(from, to, k * k),
                Quaternion.Slerp(fromRotation, toRotation, Mathf.Sqrt(k)));

            yield return null;
        }

        transform.SetPositionAndRotation(to, toRotation);
    }

    private IEnumerator HideAfter(float delay)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, delay));
        SetVisible(false);
        body.isKinematic = true;
    }

    // 치임 판정을 꺼 둔다. 파훼된 차가 굴러가며 플레이어를 치는 일이 없게 한다.
    private void DisableKillZone()
    {
        if (killZone != null)
        {
            killZone.gameObject.SetActive(false);
        }
    }

    private void SetVisible(bool visible)
    {
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = visible;
        }
    }

    public void ResetToStart()
    {
        StopAllCoroutines();

        // 파훼되면서 꺼 두었던 것들을 전부 되돌린다.
        // 속도를 지우기 전에 물리부터 켜야 한다(키네마틱 상태에서는 속도를 넣을 수 없다).
        body.isKinematic = false;
        body.constraints = startConstraints;

        Stop();
        isCharging = false;
        isWrecked = false;

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.position = startPosition;
        body.rotation = startRotation;

        // 늪에 빠질 때는 트랜스폼을 직접 옮겼으므로 그쪽도 맞춰 준다.
        transform.SetPositionAndRotation(startPosition, startRotation);

        SetVisible(true);

        if (killZone != null)
        {
            killZone.gameObject.SetActive(true);
        }
    }

    private void HandlePlayerRespawned()
    {
        if (resetOnPlayerRespawn)
        {
            ResetToStart();
        }
    }

    // 자동차 콜라이더가 트리거인지 아닌지 모르므로 두 경로 모두 받는다.
    //
    // 여기서는 벽에 막히는 것만 본다. 플레이어 치임은 앞쪽 CarKillZone이 맡는다.
    // (출발 트리거 존은 자동차의 자식이라 그 트리거 메시지가 이 Rigidbody로도 전달된다.
    //  예전처럼 여기서 사망 처리까지 하면 트리거를 밟는 순간 죽어 버린다)
    private void OnCollisionEnter(Collision collision)
    {
        HandleContact(collision.collider);
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleContact(other);
    }

    private void HandleContact(Collider other)
    {
        if (!isCharging)
        {
            return;
        }

        // 흙 원소로 세운 벽 등 CarBlocker가 붙은 것에 막히면 벽이 정한 대로 반응한다.
        CarBlocker blocker = other.GetComponentInParent<CarBlocker>();
        if (blocker != null)
        {
            blocker.OnCarBlocked(this);
        }
    }
}
