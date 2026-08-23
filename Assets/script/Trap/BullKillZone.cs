using UnityEngine;

// 황소 앞쪽에 붙는 "받힘 판정" 트리거.
//
// 자동차의 CarKillZone과 같은 역할이지만 다음이 더 있다.
//  - 받은 그 자리에서 황소를 멈춰 세운다(플레이어를 뚫고 계속 달리지 않는다).
//  - 플레이어만 물리로 날린다(PlayerAutoWalker.Launch). 그동안 걷기 제어가 잠시 손을 뗀다.
//
// 사망 후 화면 연출(검은 커튼 + 실패 패널)은 다른 트랩과 같이 ElementalKillEffect가 맡는다.
// 이 오브젝트나 부모/자식에 ElementalKillEffect가 붙어 있으면 사망 처리 직후 자동으로 재생된다.
//
// ChargingBull이 없으면 자동으로 만들어 붙이므로 씬에서 따로 만들 필요는 없다.
[RequireComponent(typeof(Collider))]
public class BullKillZone : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("비워두면 부모에서 찾는다")]
    [SerializeField] private ChargingBull bull;

    [Header("설정")]
    [Tooltip("황소가 돌진하는 중일 때만 받힘 판정을 낸다")]
    [SerializeField] private bool onlyWhileCharging = true;
    [SerializeField] private string deathAnimationTriggerName = "Die";
    [Tooltip("플레이어를 받은 자리에서 황소를 멈춰 세운다. 끄면 플레이어를 날리고도 계속 달린다")]
    [SerializeField] private bool stopBullOnHit = true;

    [Header("날려버리기")]
    [Tooltip("플레이어를 앞으로 밀어내는 세기(m/s)")]
    [SerializeField] private float launchSpeed = 12f;
    [Tooltip("같이 얹어 주는 위쪽 속도(m/s)")]
    [SerializeField] private float launchLift = 7f;

    [Header("사운드")]
    [Tooltip("플레이어를 받았을 때 울릴 소리")]
    [SerializeField] private AudioClip hitSound;
    [Range(0f, 1f)]
    [SerializeField] private float hitVolume = 1f;

    private Collider zoneCollider;

    public void Setup(ChargingBull owner, string deathTrigger)
    {
        bull = owner;
        deathAnimationTriggerName = deathTrigger;
    }

    private void Awake()
    {
        zoneCollider = GetComponent<Collider>();
        if (!zoneCollider.isTrigger)
        {
            Debug.LogWarning($"{name}: BullKillZone의 Collider는 IsTrigger가 켜져 있어야 합니다. 자동으로 켭니다.", this);
            zoneCollider.isTrigger = true;
        }

        if (bull == null)
        {
            bull = GetComponentInParent<ChargingBull>();
        }

        if (bull == null)
        {
            Debug.LogError($"{name}: BullKillZone이 황소(ChargingBull)를 찾지 못했습니다.", this);
        }
    }

    // 황소(ChargingBull)가 앞을 훑다가 플레이어를 찾았을 때 부른다.
    //
    // 돌진하는 동안 황소는 Kinematic이라 몸통 콜라이더가 플레이어를 밀어내 버린다.
    // 그래서 판정 상자가 몸통 안쪽에 있으면 플레이어가 여기까지 들어오지 못하고 그냥 밀려 나간다.
    // 트리거를 기다리지 않고 황소 쪽에서 직접 알려 주는 길을 열어 둔다.
    public void ReportHit(Collider other)
    {
        HandleContact(other);
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleContact(other);
    }

    // 빠른 속도로 지나가며 Enter를 놓치는 경우를 대비해 머무는 동안에도 확인한다.
    private void OnTriggerStay(Collider other)
    {
        HandleContact(other);
    }

    private void HandleContact(Collider other)
    {
        if (onlyWhileCharging && (bull == null || !bull.IsCharging))
        {
            return;
        }

        Transform player = PlayerLocator.FindPlayerRoot(other.transform);
        if (player == null || PlayerLocator.IsAlreadyDead(player))
        {
            return;
        }

        // 사망 처리(Die 애니메이션 + 감속/리스폰 예약)가 먼저다.
        if (!PlayerLocator.Kill(player, deathAnimationTriggerName, this))
        {
            return;
        }

        // 받은 자리에서 황소는 멈춘다. 플레이어만 날아간다.
        if (stopBullOnHit && bull != null)
        {
            bull.Stop();
        }

        // 그다음 물리로 날린다. 날아가는 동안에는 PlayerAutoWalker가 속도를 덮어쓰지 않는다.
        LaunchPlayer(player);

        // 사망 후 화면 연출(검은 커튼 + 실패 패널)은 ElementalKillEffect가 맡는다.
        ElementalKillEffect.Play(this, player);
        SfxPlayer.PlayAt(hitSound, transform.position, hitVolume);
    }

    private void LaunchPlayer(Transform player)
    {
        PlayerAutoWalker walker = player.GetComponentInChildren<PlayerAutoWalker>();
        if (walker == null)
        {
            return;
        }

        // 밀어내는 방향은 황소의 돌진 방향(ChargingBull의 '돌진 방향' 항목).
        // 황소를 못 찾으면 판정 상자의 앞쪽을 쓴다.
        Vector3 direction = bull != null ? bull.ChargeDirection : transform.forward;
        walker.Launch(direction * launchSpeed + Vector3.up * launchLift);
    }

    // 씬에 놓을 때 판정 범위를 눈으로 확인할 수 있게 그려 준다.
    private void OnDrawGizmosSelected()
    {
        if (!(GetComponent<Collider>() is BoxCollider box))
        {
            return;
        }

        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.9f, 0.3f, 0.3f, 0.6f);
        Gizmos.DrawWireCube(box.center, box.size);
        Gizmos.matrix = previous;
    }
}
