using UnityEngine;

// 플레이어가 들어오면 자동차를 출발시키는 트리거 박스.
// 자동차 자체와 분리해 두어서, 트리거 위치와 자동차 위치를 따로 배치할 수 있다.
[RequireComponent(typeof(Collider))]
public class CarTriggerZone : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] private ChargingCar car;

    [Header("설정")]
    [Tooltip("플레이어가 리스폰하면 다시 발동할 수 있게 되돌린다")]
    [SerializeField] private bool rearmOnPlayerRespawn = true;

    [Header("사운드")]
    [Tooltip("플레이어가 트리거를 밟아 자동차가 출발할 때 울릴 소리")]
    [SerializeField] private AudioClip triggerSound;
    [Range(0f, 1f)]
    [SerializeField] private float triggerVolume = 1f;

    private Collider zoneCollider;
    private bool triggered;

    private void Awake()
    {
        zoneCollider = GetComponent<Collider>();
        if (!zoneCollider.isTrigger)
        {
            Debug.LogWarning($"{name}: CarTriggerZone의 Collider는 IsTrigger가 켜져 있어야 합니다. 자동으로 켭니다.", this);
            zoneCollider.isTrigger = true;
        }

        if (car == null)
        {
            Debug.LogError($"{name}: CarTriggerZone에 자동차(ChargingCar)가 연결되지 않았습니다.", this);
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
        if (triggered || car == null)
        {
            return;
        }

        if (PlayerLocator.FindPlayerRoot(other.transform) == null)
        {
            return;
        }

        triggered = true;

        SfxPlayer.PlayAt(triggerSound, transform.position, triggerVolume);
        car.Launch();
    }

    private void HandlePlayerRespawned()
    {
        if (rearmOnPlayerRespawn)
        {
            triggered = false;
        }
    }
}
