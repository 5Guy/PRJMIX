using UnityEngine;

// 함정 바로 앞(플레이어가 오는 쪽)에 놓이는 트리거 상자.
//
// 파훼 원소(IElementCounterTrap)를 올려 두어도 바로 꺼지지 않고,
// 플레이어가 이 트리거를 지나가는 순간에야 꺼진다.
// 시작 버튼 한 번에 맵의 모든 함정이 동시에 사라지면 무슨 일이 일어났는지 알아보기 어렵기 때문이다.
//
// 씬에 직접 만들어 붙여도 되고(그 경우 함정의 approachZone 칸에 물려 준다),
// 비워 두면 함정이 Build()로 앞에 알맞은 크기로 하나 세운다.
[RequireComponent(typeof(Collider))]
public class TrapApproachZone : MonoBehaviour
{
    [Tooltip("이 트리거가 발동시킬 함정. 비워두면 부모에서 찾는다")]
    [SerializeField] private MonoBehaviour trapBehaviour;

    [Header("사운드")]
    [Tooltip("플레이어가 트리거를 밟아 함정이 꺼질 때 울릴 소리")]
    [SerializeField] private AudioClip triggerSound;
    [Range(0f, 1f)]
    [SerializeField] private float triggerVolume = 1f;

    private IElementCounterTrap trap;

    public void Setup(IElementCounterTrap owner)
    {
        trap = owner;
        trapBehaviour = owner as MonoBehaviour;
    }

    private void Awake()
    {
        // 인스펙터에 물려 둔 것을 먼저 쓰고, 없으면 부모에서 찾는다.
        if (trap == null)
        {
            trap = trapBehaviour as IElementCounterTrap;
        }

        if (trap == null)
        {
            trap = GetComponentInParent<IElementCounterTrap>();
        }

        Collider zoneCollider = GetComponent<Collider>();
        if (!zoneCollider.isTrigger)
        {
            Debug.LogWarning($"{name}: TrapApproachZone의 Collider는 IsTrigger가 켜져 있어야 합니다. 자동으로 켭니다.", this);
            zoneCollider.isTrigger = true;
        }
    }

    // 함정 앞, 플레이어가 오는 쪽에 트리거 상자를 하나 세운다.
    //
    // 함정에 딸려 꺼지지 않도록 씬 뿌리에 따로 세운다.
    // (파훼되면 함정이나 연출 오브젝트는 통째로 꺼질 수 있다)
    // 함정 오브젝트가 공중에 떠 있는 경우(모래바람 상쇄 칸처럼)가 있어 높이는 플레이어 발밑에 맞춘다.
    public static TrapApproachZone Build(IElementCounterTrap owner, Transform anchor, float distance, Vector3 boxSize)
    {
        Transform player = PlayerLocator.FindPlayer();

        // 플레이어가 오는 쪽(= 출발 지점 방향)을 함정 "앞"으로 삼는다.
        Vector3 toPlayer = player != null
            ? Vector3.ProjectOnPlane(player.position - anchor.position, Vector3.up)
            : Vector3.zero;

        if (toPlayer.sqrMagnitude < 0.0001f)
        {
            // 플레이어를 못 찾았거나 함정 바로 위에 서 있다. 함정이 바라보는 반대쪽을 앞으로 친다.
            toPlayer = Vector3.ProjectOnPlane(-anchor.forward, Vector3.up);

            if (toPlayer.sqrMagnitude < 0.0001f)
            {
                toPlayer = Vector3.back;
            }

            Debug.LogWarning(
                $"{anchor.name}: 플레이어를 찾지 못해 함정 앞 트리거 방향을 추측했습니다. " +
                "필요하면 approachZone에 직접 만든 트리거를 물려 주세요.",
                anchor);
        }

        Vector3 direction = toPlayer.normalized;
        float reach = Mathf.Max(0.1f, distance);

        // 함정이 출발 지점과 가까우면 트리거가 플레이어 뒤에 서 버린다.
        // 그러면 플레이어가 처음부터 트리거 안에 서 있는 셈이라, 원소를 올리는 순간
        // (또는 출발하자마자) 함정이 꺼져 버린다. 반드시 플레이어와 함정 사이에 오게 당긴다.
        if (player != null)
        {
            float toPlayerDistance = toPlayer.magnitude;
            float limit = toPlayerDistance - Mathf.Max(0.1f, boxSize.z) * 0.5f - 0.5f;

            if (reach > limit)
            {
                Debug.LogWarning(
                    $"{anchor.name}: 출발 지점과 {toPlayerDistance:0.0}m밖에 안 떨어져 있어 " +
                    $"접근 트리거를 {reach:0.0}m → {Mathf.Max(0.1f, limit):0.0}m로 당겼습니다. " +
                    "함정을 좀 더 앞쪽에 놓는 것이 좋습니다.",
                    anchor);

                reach = Mathf.Max(0.1f, limit);
            }
        }

        Vector3 center = anchor.position + direction * reach;
        center.y = player != null ? player.position.y : anchor.position.y;

        GameObject zoneObject = new GameObject($"{anchor.name} ApproachZone");
        zoneObject.transform.SetPositionAndRotation(center, Quaternion.LookRotation(-direction, Vector3.up));

        Vector3 size = new Vector3(
            Mathf.Max(0.1f, boxSize.x),
            Mathf.Max(0.1f, boxSize.y),
            Mathf.Max(0.1f, boxSize.z));

        BoxCollider box = zoneObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = size;
        box.center = new Vector3(0f, size.y * 0.5f, 0f);   // 발밑에서 위로 세운다

        TrapApproachZone zone = zoneObject.AddComponent<TrapApproachZone>();
        zone.Setup(owner);

        return zone;
    }

    private void OnTriggerEnter(Collider other)
    {
        HandlePlayerContact(other);
    }

    // 리스폰으로 트리거 안에 곧바로 나타나면 Enter가 오지 않는다. 머무는 동안에도 확인한다.
    // 올려 둔 원소가 없으면 함정 쪽에서 그냥 무시하므로 중복으로 꺼질 걱정은 없다.
    private void OnTriggerStay(Collider other)
    {
        HandlePlayerContact(other);
    }

    private void HandlePlayerContact(Collider other)
    {
        if (trap == null || !trap.IsArmed)
        {
            return;
        }

        // 출발 전에는 발동하지 않는다.
        //
        // 플레이어는 시작 버튼을 누를 때까지 출발 지점에 서 있는데, 함정이 출발 지점과 가까우면
        // 그 자리가 트리거 안일 수 있다. 그러면 OnTriggerStay가 매 프레임 돌면서
        // 원소를 올려 두는 순간 함정이 꺼져 버린다("올리자마자 사라짐").
        // 파훼는 "걸어가다 함정 앞을 지날 때" 일어나야 하므로 출발 전에는 무시한다.
        if (StageStartButton.Exists && !StageStartButton.HasStarted)
        {
            return;
        }

        if (PlayerLocator.FindPlayerRoot(other.transform) == null)
        {
            return;
        }

        SfxPlayer.PlayAt(triggerSound, transform.position, triggerVolume);
        trap.NotifyPlayerApproached();
    }

    // 씬에서 트리거 위치를 눈으로 확인할 수 있게 그려 준다.
    private void OnDrawGizmosSelected()
    {
        BoxCollider box = GetComponent<Collider>() as BoxCollider;
        if (box == null)
        {
            return;
        }

        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
        Gizmos.color = new Color(0.4f, 0.85f, 0.6f, 0.5f);
        Gizmos.DrawWireCube(box.center, box.size);
        Gizmos.matrix = previous;
    }
}
