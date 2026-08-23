using UnityEngine;

// 바람 원소를 도로에 놓으면 생기는 돌풍.
//
// 벽처럼 차를 막는 게 아니라, 달려오는 차가 이 칸에 들어오는 순간 통째로 들어 올려
// 뒤로 날려 버린다. 차가 도로 밖으로 굴러가면 플레이어는 그냥 지나갈 수 있다.
//
// 판정은 트리거라서 자동차의 벽 감지(BoxCast, 트리거 무시)에는 걸리지 않는다.
// 즉 차는 멈추지 않고 그대로 달려 들어오다가 날아간다.
[RequireComponent(typeof(Collider))]
public class WindGust : MonoBehaviour
{
    [Header("바람 세기")]
    [Tooltip("달려오던 방향의 반대로 밀어내는 세기(m/s)")]
    [SerializeField] private float pushForce = 9f;
    [Tooltip("위로 들어 올리는 세기(m/s)")]
    [SerializeField] private float lift = 11f;
    [Tooltip("공중에서 빙글빙글 도는 정도")]
    [SerializeField] private float spin = 8f;

    [Header("사운드")]
    [Tooltip("차가 휩쓸릴 때 울릴 소리")]
    [SerializeField] private AudioClip gustSound;
    [Range(0f, 1f)]
    [SerializeField] private float gustVolume = 1f;

    private bool used;

    private void Awake()
    {
        Collider zone = GetComponent<Collider>();
        if (!zone.isTrigger)
        {
            zone.isTrigger = true;
        }
    }

    // 시작 버튼을 다시 누르면 한 번 더 쓸 수 있어야 한다.
    // (사망 리스폰 때는 배치해 둔 원소째로 치워지므로 이 오브젝트 자체가 사라진다)
    private void OnEnable()
    {
        StageReset.Requested += HandleStageReset;
    }

    private void OnDisable()
    {
        StageReset.Requested -= HandleStageReset;
    }

    private void HandleStageReset(StageReset.Reason reason)
    {
        used = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (used)
        {
            return;
        }

        // 자동차의 치임 판정(CarKillZone)도 같은 Rigidbody라 여기로 들어온다. 본체를 찾아 쓴다.
        ChargingCar car = other.GetComponentInParent<ChargingCar>();
        if (car == null || !car.IsCharging)
        {
            return;
        }

        used = true;

        // 달려오던 방향의 반대로 밀어 올린다. 돌풍이 어느 쪽을 보고 있든 결과가 같아서,
        // 칸에 놓기만 하면 되는 이 게임의 배치 방식과 맞는다.
        car.BlowAway(-car.transform.forward, pushForce, lift, spin);
        SfxPlayer.PlayAt(gustSound, transform.position, gustVolume);
    }
}
