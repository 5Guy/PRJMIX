using System.Collections;
using UnityEngine;

// 도로에 그냥 놓은 용암.
//
// 놓인 자리에서 사방으로 정해진 크기만큼 흘러 퍼지고, 그 위를 밟은 플레이어는 타 죽는다.
// 물 함정 위에 놓으면 물이 굳어 길이 되지만(WaterTrap), 아무 데나 놓으면 오히려 길을 막는 셈이다.
//
// 배치한 원소(PlacedElement)에 얹혀 살기 때문에, 출발 전에 원소를 회수하면 용암도 함께 사라진다.
public class LavaSpill : MonoBehaviour
{
    [Header("흘러내리기")]
    [Tooltip("놓자마자 고여 있는 웅덩이의 지름(미터). 출발하면 여기서부터 퍼진다")]
    [SerializeField] private float seedDiameter = 0.7f;
    [Tooltip("다 퍼졌을 때의 지름(미터)")]
    [SerializeField] private float spreadDiameter = 3f;
    [Tooltip("사방으로 퍼지는 데 걸리는 시간(초)")]
    [SerializeField] private float spreadDuration = 0.8f;
    [Tooltip("용암 웅덩이의 두께(미터)")]
    [SerializeField] private float thickness = 0.16f;
    [ColorUsage(true, true)]
    [SerializeField] private Color lavaColor = new Color(1f, 0.32f, 0.05f, 1f);

    [Header("사망 판정")]
    [Tooltip("다 퍼지기 전에도 닿으면 죽는다. 퍼지는 만큼 판정도 같이 넓어진다")]
    [SerializeField] private bool killWhileSpreading = true;
    [SerializeField] private string deathAnimationTriggerName = "Die";
    [Tooltip("밟았을 때 울릴 소리")]
    [SerializeField] private AudioClip burnSound;
    [Range(0f, 1f)]
    [SerializeField] private float burnVolume = 1f;

    private Transform pool;
    private SphereCollider zone;
    private Material material;
    private float spread;

    // 이 용암의 연출. 배치한 원소의 3D 모습으로도 그대로 쓴다(구 대신 용암이 보이도록).
    public Transform Visual => pool;

    // 배치한 원소 오브젝트에 용암을 얹는다. 원소를 회수하면 같이 사라진다.
    public static LavaSpill Attach(GameObject placed, Vector3 groundPosition)
    {
        LavaSpill spill = placed.AddComponent<LavaSpill>();
        spill.Build(groundPosition);
        return spill;
    }

    private void Build(Vector3 groundPosition)
    {
        material = WorldVisual.CreateLit(lavaColor);

        // 갓 흘러나온 용암은 스스로 빛나 보여야 한다.
        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", lavaColor);
        }

        // 물 함정의 웅덩이와 같은 납작한 원기둥. 원기둥은 세로 2칸짜리라 scale.y가 곧 반두께다.
        GameObject poolObject = WorldVisual.CreateCylinder(
            "LavaPool",
            null,
            Vector3.zero,
            new Vector3(0f, Mathf.Max(0.01f, thickness) * 0.5f, 0f),
            material);

        poolObject.transform.position = groundPosition;

        // 프리미티브 콜라이더는 길을 막는다. 사망 판정은 아래에서 트리거로 따로 만든다.
        Collider solid = poolObject.GetComponent<Collider>();
        if (solid != null)
        {
            Destroy(solid);
        }

        pool = poolObject.transform;
        pool.SetParent(transform, true);

        // 판정은 웅덩이의 자식으로 두면 크기 조절에 휩쓸린다. 배치물 밑에 따로 세운다.
        GameObject zoneObject = new GameObject("LavaKillZone");
        zoneObject.transform.SetParent(transform, false);
        zoneObject.transform.position = groundPosition;

        zone = zoneObject.AddComponent<SphereCollider>();
        zone.isTrigger = true;
        zone.radius = 0.01f;

        zoneObject.AddComponent<LavaSpillZone>().Setup(this);

        // 출발 전에도 놓아 둔 것이 보여야 한다. 작게 고인 채로 기다린다.
        spread = Mathf.Max(0.01f, seedDiameter);
        ApplySpread();

        // 놓자마자 퍼지면 회수할 틈이 없다. 출발 버튼을 누른 뒤에 흘러내린다.
        StartCoroutine(SpreadRoutine());
    }

    // 가운데서 사방으로 번진다. 퍼진 만큼 사망 판정도 같이 넓어진다.
    private IEnumerator SpreadRoutine()
    {
        // 출발 전에는 놓은 자리에 고여만 있는다. 이때는 원소를 회수할 수 있다.
        while (StageStartButton.Exists && !StageStartButton.HasStarted)
        {
            yield return null;
        }

        float duration = Mathf.Max(0.01f, spreadDuration);
        float target = Mathf.Max(0.1f, spreadDiameter);

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            // 처음엔 왈칵 쏟아지다 가장자리에서 느려진다. 걸쭉하게 퍼지는 느낌.
            spread = Mathf.Lerp(seedDiameter, target, 1f - Mathf.Pow(1f - elapsed / duration, 3f));
            ApplySpread();
            yield return null;
        }

        spread = target;
        ApplySpread();
    }

    private void ApplySpread()
    {
        if (pool != null)
        {
            pool.localScale = new Vector3(spread, Mathf.Max(0.01f, thickness) * 0.5f, spread);
        }

        if (zone != null)
        {
            zone.radius = Mathf.Max(0.01f, spread * 0.5f);
        }
    }

    // 판정 오브젝트가 부른다.
    public void Burn(Collider other)
    {
        if (!killWhileSpreading && spread < spreadDiameter)
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

        SfxPlayer.PlayAt(burnSound, transform.position, burnVolume);
        ElementalKillEffect.Play(this, player);
    }
}
