using UnityEngine;

// 엉뚱한 곳에 놓은 바람(Wind).
//
// 모래바람 상쇄 칸이 아닌 자리에 바람을 놓으면, 갈 곳 없는 흰 폭풍이 그 자리에 선다.
// 출발하고 나면 플레이어를 향해 천천히 다가오고, 따라잡는 순간 플레이어를 날려 버린다.
// 그 뒤 화면 연출(검은 커튼 + 실패 패널)은 다른 함정과 같이 ElementalKillEffect가 맡는다.
//
// 배치한 원소(PlacedElement)에 얹혀 살기 때문에, 출발 전에 원소를 회수하면 폭풍도 함께 사라진다.
public class RogueWindStorm : MonoBehaviour
{
    [Header("쫓아오기")]
    [Tooltip("플레이어를 향해 다가오는 속도(m/s). 플레이어 걷는 속도보다 조금 느려야 쫓기는 맛이 산다")]
    [SerializeField] private float chaseSpeed = 2.6f;
    [Tooltip("이만큼 가까워지면 휩쓸린 것으로 친다(미터)")]
    [SerializeField] private float catchRadius = 1.1f;
    [Tooltip("출발 버튼을 누른 뒤 이만큼 기다렸다가 움직이기 시작한다(초)")]
    [SerializeField] private float startDelay = 0.6f;

    [Header("날려 버리기")]
    [Tooltip("휩쓸린 플레이어가 날아가는 속도(m/s)")]
    [SerializeField] private float launchSpeed = 12f;
    [Tooltip("위로 띄우는 정도(m/s)")]
    [SerializeField] private float launchLift = 7f;
    [SerializeField] private string deathAnimationTriggerName = "Die";

    [Header("연출")]
    [Tooltip("원본 모래바람 대비 크기")]
    [SerializeField] private float windScale = 1f;
    [ColorUsage(true, true)]
    [SerializeField] private Color windColor = Color.white;
    [Tooltip("휩쓸릴 때 울릴 소리")]
    [SerializeField] private AudioClip hitSound;
    [Range(0f, 1f)]
    [SerializeField] private float hitVolume = 1f;

    private Transform visual;
    private bool caught;
    private float waited;

    // 이 폭풍의 연출. 배치한 원소의 3D 모습으로도 그대로 쓴다(구 대신 바람이 보이도록).
    public Transform Visual => visual;

    // 배치한 원소 오브젝트에 폭풍을 얹는다. 원소를 회수하면 같이 사라진다.
    public static RogueWindStorm Attach(GameObject placed, Vector3 groundPosition, Sandstorm colorSource)
    {
        RogueWindStorm storm = placed.AddComponent<RogueWindStorm>();
        storm.Build(groundPosition, colorSource);
        return storm;
    }

    private void Build(Vector3 groundPosition, Sandstorm colorSource)
    {
        visual = WindStormVisual.CreateWhiteCopy(colorSource, groundPosition, windScale, windColor);

        if (visual == null)
        {
            // 베낄 모래바람이 없으면 쫓아올 폭풍도 없다. 그냥 놓인 원소로 남는다.
            Debug.LogWarning($"{name}: 흰 폭풍을 만들지 못해 쫓아오지 않습니다.", this);
            enabled = false;
            return;
        }

        // 배치물의 자식으로 매달아 두면 회수할 때 함께 지워진다.
        visual.SetParent(transform, true);
    }

    private void Update()
    {
        if (caught || visual == null)
        {
            return;
        }

        // 출발 버튼을 누르기 전에는 제자리에 서 있는다. 아직 회수할 수 있는 시간이다.
        if (StageStartButton.Exists && !StageStartButton.HasStarted)
        {
            waited = 0f;
            return;
        }

        if (waited < startDelay)
        {
            waited += Time.deltaTime;
            return;
        }

        Transform player = PlayerLocator.FindPlayer();
        if (player == null || PlayerLocator.IsAlreadyDead(player))
        {
            return;
        }

        // 높이는 건드리지 않는다. 폭풍은 바닥을 쓸며 다가온다.
        Vector3 toPlayer = Vector3.ProjectOnPlane(player.position - visual.position, Vector3.up);

        if (toPlayer.magnitude <= Mathf.Max(0.1f, catchRadius))
        {
            Sweep(player, toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : visual.forward);
            return;
        }

        visual.position += toPlayer.normalized * (chaseSpeed * Time.deltaTime);
    }

    // 따라잡았다. 플레이어를 날려 보내고 판을 끝낸다.
    private void Sweep(Transform player, Vector3 direction)
    {
        caught = true;

        if (!PlayerLocator.Kill(player, deathAnimationTriggerName, this))
        {
            return;
        }

        PlayerAutoWalker walker = player.GetComponentInChildren<PlayerAutoWalker>();
        if (walker != null)
        {
            walker.Launch(direction * launchSpeed + Vector3.up * launchLift);
        }

        SfxPlayer.PlayAt(hitSound, visual != null ? visual.position : transform.position, hitVolume);

        // 사망 후 화면 연출(검은 커튼 + 실패 패널)은 ElementalKillEffect가 맡는다.
        ElementalKillEffect.Play(this, player);
    }
}
