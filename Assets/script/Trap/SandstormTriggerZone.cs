using UnityEngine;

// 플레이어가 들어오면 모래바람을 일으키는 트리거 박스.
// 자동차의 CarTriggerZone과 같은 구조라, 트리거 위치와 모래바람 위치를 따로 배치할 수 있다.
[RequireComponent(typeof(Collider))]
public class SandstormTriggerZone : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] private Sandstorm sandstorm;

    [Header("설정")]
    [Tooltip("플레이어가 리스폰하면 다시 발동할 수 있게 되돌린다")]
    [SerializeField] private bool rearmOnPlayerRespawn = true;

    [Header("사운드")]
    [Tooltip("플레이어가 트리거를 밟아 모래바람이 일 때 울릴 소리")]
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
            Debug.LogWarning($"{name}: SandstormTriggerZone의 Collider는 IsTrigger가 켜져 있어야 합니다. 자동으로 켭니다.", this);
            zoneCollider.isTrigger = true;
        }

        if (sandstorm == null)
        {
            Debug.LogError($"{name}: SandstormTriggerZone에 모래바람(Sandstorm)이 연결되지 않았습니다.", this);
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
        if (triggered || sandstorm == null)
        {
            return;
        }

        if (PlayerLocator.FindPlayerRoot(other.transform) == null)
        {
            return;
        }

        triggered = true;

        SfxPlayer.PlayAt(triggerSound, transform.position, triggerVolume);
        sandstorm.Blow();
    }

    private void HandlePlayerRespawned()
    {
        if (rearmOnPlayerRespawn)
        {
            triggered = false;
        }
    }
}
