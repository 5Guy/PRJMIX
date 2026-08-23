using UnityEngine;

// 자동차(Pickup) 앞쪽에 붙는 "치임 판정" 트리거.
//
// 예전에는 자동차 본체(ChargingCar)가 직접 OnTriggerEnter로 사망 처리를 했는데,
// 출발 트리거(CarTriggerZone)가 자동차의 자식이라 그 트리거에 들어간 콜라이더가
// 자동차의 Rigidbody로도 전달되면서 "트리거를 밟자마자 죽는" 문제가 있었다.
// 치임 판정만 따로 떼어내서, 이 트리거에 실제로 들어와야 죽게 한다.
//
// ChargingCar가 없으면 자동으로 만들어 붙이므로 씬에서 따로 만들 필요는 없다.
[RequireComponent(typeof(Collider))]
public class CarKillZone : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("비워두면 부모에서 찾는다")]
    [SerializeField] private ChargingCar car;

    [Header("설정")]
    [Tooltip("자동차가 달리는 중일 때만 치임 판정을 낸다")]
    [SerializeField] private bool onlyWhileCharging = true;
    [SerializeField] private string deathAnimationTriggerName = "Die";

    [Header("사운드")]
    [Tooltip("플레이어를 쳤을 때 울릴 소리")]
    [SerializeField] private AudioClip hitSound;
    [Range(0f, 1f)]
    [SerializeField] private float hitVolume = 1f;

    private Collider zoneCollider;

    public void Setup(ChargingCar owner, string deathTrigger)
    {
        car = owner;
        deathAnimationTriggerName = deathTrigger;
    }

    private void Awake()
    {
        zoneCollider = GetComponent<Collider>();
        if (!zoneCollider.isTrigger)
        {
            Debug.LogWarning($"{name}: CarKillZone의 Collider는 IsTrigger가 켜져 있어야 합니다. 자동으로 켭니다.", this);
            zoneCollider.isTrigger = true;
        }

        if (car == null)
        {
            car = GetComponentInParent<ChargingCar>();
        }

        if (car == null)
        {
            Debug.LogError($"{name}: CarKillZone이 자동차(ChargingCar)를 찾지 못했습니다.", this);
        }
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
        if (onlyWhileCharging && (car == null || !car.IsCharging))
        {
            return;
        }

        Transform player = PlayerLocator.FindPlayerRoot(other.transform);
        if (player == null || PlayerLocator.IsAlreadyDead(player))
        {
            return;
        }

        if (!PlayerLocator.Kill(player, deathAnimationTriggerName, this))
        {
            return;
        }

        ElementalKillEffect.Play(this, player);
        SfxPlayer.PlayAt(hitSound, transform.position, hitVolume);
    }
}
