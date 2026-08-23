using UnityEngine;

// 모래바람에 휩쓸리는 판정.
//
// 자동차의 CarKillZone과 같은 역할이다.
// 사망 후 화면 연출(검은 커튼 + 실패 패널)은 다른 트랩과 같이 ElementalKillEffect가 맡는다.
// 이 오브젝트나 부모/자식에 ElementalKillEffect가 붙어 있으면 사망 처리 직후 자동으로 재생된다.
//
// Sandstorm이 없으면 자동으로 만들어 붙이므로 씬에서 따로 만들 필요는 없다.
[RequireComponent(typeof(Collider))]
public class SandstormKillZone : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("비워두면 부모에서 찾는다")]
    [SerializeField] private Sandstorm sandstorm;

    [Header("설정")]
    [Tooltip("모래바람이 부는 중일 때만 휩쓸림 판정을 낸다. " +
             "제자리에 서 있는 모래바람은 꺼 두어야 닿는 순간 걸린다")]
    [SerializeField] private bool onlyWhileBlowing;
    [SerializeField] private string deathAnimationTriggerName = "Die";

    [Header("사운드")]
    [Tooltip("플레이어가 휩쓸렸을 때 울릴 소리")]
    [SerializeField] private AudioClip hitSound;
    [Range(0f, 1f)]
    [SerializeField] private float hitVolume = 1f;

    private Collider zoneCollider;

    public void Setup(Sandstorm owner, string deathTrigger)
    {
        sandstorm = owner;
        deathAnimationTriggerName = deathTrigger;
    }

    private void Awake()
    {
        zoneCollider = GetComponent<Collider>();
        if (!zoneCollider.isTrigger)
        {
            Debug.LogWarning($"{name}: SandstormKillZone의 Collider는 IsTrigger가 켜져 있어야 합니다. 자동으로 켭니다.", this);
            zoneCollider.isTrigger = true;
        }

        if (sandstorm == null)
        {
            sandstorm = GetComponentInParent<Sandstorm>();
        }

        if (sandstorm == null)
        {
            Debug.LogError($"{name}: SandstormKillZone이 모래바람(Sandstorm)을 찾지 못했습니다.", this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleContact(other);
    }

    // 바람이 플레이어를 따라잡는 도중 Enter를 놓치는 경우를 대비해 머무는 동안에도 확인한다.
    private void OnTriggerStay(Collider other)
    {
        HandleContact(other);
    }

    private void HandleContact(Collider other)
    {
        if (onlyWhileBlowing && (sandstorm == null || !sandstorm.IsBlowing))
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

        // 사망 후 화면 연출(검은 커튼 + 실패 패널)은 ElementalKillEffect가 맡는다.
        ElementalKillEffect.Play(this, player);
        SfxPlayer.PlayAt(hitSound, transform.position, hitVolume);
    }
}
