using UnityEngine;

// 황소의 자식으로 두는 빈 상자(Empty Cube) 트리거.
// 플레이어가 이 범위에 들어오면 황소가 돌진을 시작한다.
//
// 황소의 자식이라 황소를 옮기면 감지 범위도 같이 따라온다.
// (자식 트리거에 들어간 콜라이더는 황소 Rigidbody로도 전달되지만,
//  ChargingBull은 벽 막힘과 들이받기만 보고 사망 처리는 BullKillZone이 따로 맡으므로 문제되지 않는다)
[RequireComponent(typeof(Collider))]
public class BullTriggerZone : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("비워두면 부모에서 찾는다")]
    [SerializeField] private ChargingBull bull;

    [Header("설정")]
    [Tooltip("플레이어가 리스폰하면 다시 발동할 수 있게 되돌린다")]
    [SerializeField] private bool rearmOnPlayerRespawn = true;

    [Header("사운드")]
    [Tooltip("플레이어가 범위에 들어와 황소가 돌진할 때 울릴 소리")]
    [SerializeField] private AudioClip triggerSound;
    [Range(0f, 1f)]
    [SerializeField] private float triggerVolume = 1f;

    private Collider zoneCollider;
    private bool triggered;

    public void Setup(ChargingBull owner)
    {
        bull = owner;
    }

    private void Awake()
    {
        zoneCollider = GetComponent<Collider>();
        if (!zoneCollider.isTrigger)
        {
            Debug.LogWarning($"{name}: BullTriggerZone의 Collider는 IsTrigger가 켜져 있어야 합니다. 자동으로 켭니다.", this);
            zoneCollider.isTrigger = true;
        }

        if (bull == null)
        {
            bull = GetComponentInParent<ChargingBull>();
        }

        if (bull == null)
        {
            Debug.LogError($"{name}: BullTriggerZone이 황소(ChargingBull)를 찾지 못했습니다.", this);
        }
    }

    private void OnEnable()
    {
        PlayerAutoWalker.Respawned += HandlePlayerRespawned;
    }

    private void OnDisable()
    {
        PlayerAutoWalker.Respawned -= HandlePlayerRespawned;
    }

    private void OnTriggerEnter(Collider other)
    {
        HandlePlayerContact(other);
    }

    // 리스폰으로 범위 안에 곧바로 나타나면 Enter가 오지 않는다. 머무는 동안에도 확인한다.
    private void OnTriggerStay(Collider other)
    {
        HandlePlayerContact(other);
    }

    private void HandlePlayerContact(Collider other)
    {
        if (triggered || bull == null)
        {
            return;
        }

        if (PlayerLocator.FindPlayerRoot(other.transform) == null)
        {
            return;
        }

        triggered = true;

        SfxPlayer.PlayAt(triggerSound, transform.position, triggerVolume);
        bull.Charge();
    }

    private void HandlePlayerRespawned()
    {
        if (rearmOnPlayerRespawn)
        {
            triggered = false;
        }
    }

    // 씬에서 감지 범위를 눈으로 확인할 수 있게 그려 준다.
    private void OnDrawGizmosSelected()
    {
        if (!(GetComponent<Collider>() is BoxCollider box))
        {
            return;
        }

        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.95f, 0.55f, 0.2f, 0.5f);
        Gizmos.DrawWireCube(box.center, box.size);
        Gizmos.matrix = previous;
    }
}
