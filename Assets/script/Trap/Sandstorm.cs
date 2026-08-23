using UnityEngine;

// 맵에 서 있는 모래바람(EzTornado 연출을 붙여 쓴다).
//
// - 불 함정처럼 처음부터 맵에 드러나 있다. 소용돌이 자체가 장애물이라 닿으면 안 된다.
// - 휩쓸림 판정(SandstormKillZone)에 플레이어가 들어오면 "시스템 다운" 연출과 함께 사망 처리한다.
// - 상쇄 칸(ElementTrapCube)에 물 계열 원소를 올려 두면 모래가 젖어 가라앉아 모래바람이 사라진다.
//   상쇄 처리(원소 올리기 / 시작 버튼에 맞춰 끄기 / 되돌리기)는 전부 ElementTrapCube가 맡는다.
// - 원소를 배치하는 탑뷰에서는 소용돌이가 도로를 가리므로, 연출을 숨기고 납작한 표식만 남긴다.
//
// moveSpeed를 올리면 자기 forward 방향으로 밀려오는 모래바람이 된다.
// 이때는 자동차(ChargingCar)와 달리 물리로 밀지 않고 트랜스폼만 옮긴다.
// 파티클 연출이라 무언가에 걸려 멈추면 오히려 어색하기 때문이다.
public class Sandstorm : MonoBehaviour
{
    [Header("이동")]
    [Tooltip("초당 이동 거리. 0이면 제자리에서 소용돌이만 친다")]
    [SerializeField] private float moveSpeed;
    [Tooltip("시작 위치에서 이만큼 나아가면 멈춘다. 0 이하면 계속 나아간다")]
    [SerializeField] private float travelDistance;
    [Tooltip("끝까지 나아가면 연출을 통째로 끈다")]
    [SerializeField] private bool disappearAtEnd = true;

    [Header("발동")]
    [Tooltip("시작 버튼을 누르는 순간 바로 몰아친다. 제자리 모래바람은 켜 두면 된다")]
    [SerializeField] private bool activateOnStageStart = true;

    [Header("연출 크기")]
    [Tooltip("EzTornado 프리팹은 사막 데모 기준이라(반경 16.6m, 입자 20~40m) 이 스테이지에서는 " +
             "플레이어가 소용돌이 안에 파묻혀 아무것도 안 보인다. 여기서 줄여 쓴다")]
    [SerializeField] private float visualScale = 0.03f;
    [Tooltip("크기를 적용할 연출 뿌리. 비워두면 파티클이 들어 있는 자식을 찾아 쓴다")]
    [SerializeField] private Transform visualRoot;

    [Header("탑뷰 표시")]
    [Tooltip("원소를 배치하는 탑뷰에서는 소용돌이를 숨기고 납작한 표식만 보여 준다")]
    [SerializeField] private bool hideVisualInTopView = true;
    [SerializeField] private Color topViewMarkerColor = new Color(0.85f, 0.72f, 0.42f, 0.9f);
    [Tooltip("탑뷰 표식의 지름(미터)")]
    [SerializeField] private float topViewMarkerDiameter = 1.2f;

    [Header("휩쓸림 판정")]
    [Tooltip("비워두면 연출 크기에 맞춘 트리거(SandstormKillZone)를 자동으로 만들어 붙인다")]
    [SerializeField] private SandstormKillZone killZone;
    [Tooltip("자동으로 만들 때 쓸 반지름")]
    [SerializeField] private float killRadius = 0.6f;
    [Tooltip("자동으로 만들 때 쓸 높이")]
    [SerializeField] private float killHeight = 2f;
    [SerializeField] private string deathAnimationTriggerName = "Die";

    [Header("상쇄")]
    [Tooltip("이 모래바람을 끄는 상쇄 칸. 비워두면 자식에서 찾는다")]
    [SerializeField] private ElementTrapCube counterTrap;
    [Tooltip("바람 원소로 끌 때 쓸 흰 폭풍 연출(WindCounterEffect)을 자동으로 달아 둔다. " +
             "씬에 직접 붙여 둔 것이 있으면 그것을 그대로 쓴다")]
    [SerializeField] private bool autoAddWindCounterEffect = true;

    [Header("사운드")]
    [Tooltip("불어오기 시작할 때 한 번 울릴 소리")]
    [SerializeField] private AudioClip launchSound;
    [Range(0f, 1f)]
    [SerializeField] private float launchVolume = 1f;
    [Tooltip("부는 동안 계속 울릴 바람 소리(선택). EzTornado/Audio/windloop 를 쓰면 된다")]
    [SerializeField] private AudioClip windLoop;
    [Range(0f, 1f)]
    [SerializeField] private float windVolume = 0.7f;

    private AudioSource windSource;
    private Renderer[] visualRenderers;
    private Transform topViewMarker;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private bool wasActiveAtStart = true;
    private bool isBlowing;

    public bool IsBlowing => isBlowing;

    // 물을 부어 꺼 둔 상태. 이때는 트리거를 밟아도 불어오지 않는다.
    public bool IsCountered => counterTrap != null && counterTrap.IsCountered;

    private void Awake()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
        wasActiveAtStart = gameObject.activeSelf;

        if (counterTrap == null)
        {
            counterTrap = GetComponentInChildren<ElementTrapCube>(true);
        }

        ApplyVisualScale();
        EnsureKillZone();
        EnsureWindSource();
        EnsureWindCounterEffect();
        BuildTopViewMarker();

        // 상쇄되면 ElementTrapCube가 이 오브젝트를 통째로 끄기 때문에 OnEnable/OnDisable로는
        // 복구 신호를 받을 수 없다. 꺼져 있어도 신호가 오도록 수명 전체에 걸쳐 구독한다.
        StageReset.Requested += HandleStageReset;
        StageStartButton.StageStarted += HandleStageStarted;
    }

    private void OnDestroy()
    {
        StageReset.Requested -= HandleStageReset;
        StageStartButton.StageStarted -= HandleStageStarted;
    }

    // EzTornado 프리팹을 이 스테이지 크기에 맞게 줄인다.
    //
    // 그냥 부모 오브젝트의 Scale을 줄여도 소용이 없다. 이 프리팹의 파티클은
    // Scaling Mode가 Local로 되어 있어서 부모 스케일을 아예 무시하기 때문이다.
    // (그래서 씬에 넣어도 반경 16m짜리 소용돌이가 그대로 나오고, 플레이어가 그 안에 파묻혀
    //  화면에는 아무것도 안 보이는 것처럼 느껴진다)
    // Hierarchy로 바꿔 준 다음 연출 뿌리의 Scale을 줄여야 입자 크기까지 같이 작아진다.
    private void ApplyVisualScale()
    {
        ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>(true);
        if (particles.Length == 0)
        {
            return;
        }

        foreach (ParticleSystem particle in particles)
        {
            ParticleSystem.MainModule main = particle.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }

        Transform root = visualRoot;
        if (root == null)
        {
            // 파티클이 든 가지를 타고 올라가 모래바람 바로 아래 자식(= 프리팹 뿌리)을 찾는다.
            // 연출이 이 오브젝트 자신에게 붙어 있으면 거기서 멈춘다 — 더 올라가면 남의 오브젝트다.
            root = particles[0].transform;
            while (root != transform && root.parent != null && root.parent != transform)
            {
                root = root.parent;
            }
        }

        // visualScale은 "프리팹 원본 대비 최종 크기"로 읽히는 게 예측하기 쉽다.
        // 모래바람 오브젝트 자체가 이미 줄여져 있어도(StormTrab처럼 0.4배) 결과가 같도록 부모 스케일을 되돌린다.
        float scale = Mathf.Max(0.01f, visualScale);
        root.localScale = root == transform
            ? Vector3.one * scale
            : Vector3.Scale(Vector3.one * scale, InverseLossyScale());
    }

    // 이 오브젝트에 걸린 스케일을 되돌리는 배율.
    // 판정 캡슐이나 표식처럼 "미터로 지정한 크기"가 부모 스케일에 휩쓸리지 않게 할 때 쓴다.
    private Vector3 InverseLossyScale()
    {
        Vector3 lossy = transform.lossyScale;
        return new Vector3(
            Mathf.Approximately(lossy.x, 0f) ? 1f : 1f / lossy.x,
            Mathf.Approximately(lossy.y, 0f) ? 1f : 1f / lossy.y,
            Mathf.Approximately(lossy.z, 0f) ? 1f : 1f / lossy.z);
    }

    // 연출(파티클)만 있고 판정이 없으면 그냥 지나쳐 버린다. 기둥 모양 트리거를 하나 만들어 준다.
    private void EnsureKillZone()
    {
        if (killZone == null)
        {
            killZone = GetComponentInChildren<SandstormKillZone>(true);
        }

        if (killZone != null)
        {
            killZone.Setup(this, deathAnimationTriggerName);
            return;
        }

        GameObject zoneObject = new GameObject("SandstormKillZone");
        zoneObject.transform.SetParent(transform, false);

        // killRadius / killHeight는 미터로 적어 둔 값이다. 부모가 줄여져 있어도 그대로 지킨다.
        zoneObject.transform.localScale = InverseLossyScale();

        CapsuleCollider capsule = zoneObject.AddComponent<CapsuleCollider>();
        capsule.isTrigger = true;
        capsule.direction = 1;   // Y축 = 세로로 선 기둥
        capsule.radius = Mathf.Max(0.1f, killRadius);
        capsule.height = Mathf.Max(capsule.radius * 2f, killHeight);
        capsule.center = new Vector3(0f, capsule.height * 0.5f, 0f);

        killZone = zoneObject.AddComponent<SandstormKillZone>();
        killZone.Setup(this, deathAnimationTriggerName);

        // 사망 후 화면 연출도 같이 붙여 준다. 프리팹에 미리 붙여 둔 것과 같은 구성이 된다.
        if (zoneObject.GetComponentInParent<ElementalKillEffect>() == null)
        {
            zoneObject.AddComponent<ElementalKillEffect>();
        }
    }

    // 바람으로 끄는 연출을 스스로 달아 둔다.
    //
    // ElementTrapCube는 상쇄되는 순간 "올려 둔 원소를 맡은 TrapCounterEffect"를 뿌리에서 찾는데,
    // 씬에 손으로 붙여 두지 않으면 못 찾고 연출 없이 그냥 꺼진다.
    // 바람이 어떻게 보여야 하는지는 모래바람 자신이 알고 있으니(색을 베낄 대상이 자기 자신이다)
    // 여기서 직접 달아 둔다.
    private void EnsureWindCounterEffect()
    {
        if (!autoAddWindCounterEffect || counterTrap == null)
        {
            return;
        }

        if (!HasCounterEffectFor(ElementType.Storm))
        {
            gameObject.AddComponent<WindCounterEffect>().Setup(this);
        }

        // 칼로 베는 연출, 늪으로 삼키는 연출도 같은 이유로 스스로 달아 둔다.
        if (!HasCounterEffectFor(ElementType.Sword))
        {
            gameObject.AddComponent<SwordCounterEffect>().Setup();
        }

        if (!HasCounterEffectFor(ElementType.Swamp))
        {
            gameObject.AddComponent<SwampCounterEffect>().Setup();
        }
    }

    // 씬에서 직접 붙이고 값을 맞춰 둔 것이 있으면 건드리지 않는다.
    private bool HasCounterEffectFor(ElementType element)
    {
        foreach (TrapCounterEffect existing in transform.root.GetComponentsInChildren<TrapCounterEffect>(true))
        {
            if (existing.Element == element)
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureWindSource()
    {
        if (windLoop == null)
        {
            return;
        }

        windSource = gameObject.AddComponent<AudioSource>();
        windSource.clip = windLoop;
        windSource.loop = true;
        windSource.playOnAwake = false;
        windSource.volume = windVolume;
        windSource.spatialBlend = 1f;
    }

    // 탑뷰에서 숨길 대상(= 소용돌이 연출)을 미리 모아 두고, 대신 보여 줄 납작한 표식을 만든다.
    // 표식 자신은 숨김 대상에서 빠져야 하므로 렌더러를 먼저 모은다.
    private void BuildTopViewMarker()
    {
        visualRenderers = GetComponentsInChildren<Renderer>(true);

        if (visualRenderers.Length == 0)
        {
            Debug.LogWarning($"{name}: 모래바람 연출(Renderer)이 하나도 없습니다. EzTornado 프리팹을 자식으로 넣어 주세요.", this);
        }

        if (!hideVisualInTopView)
        {
            return;
        }

        GameObject markerObject = new GameObject("TopViewMarker", typeof(SpriteRenderer));
        markerObject.transform.SetParent(transform, false);

        // 표식 지름도 미터로 적어 둔 값이라, 부모 스케일에 휩쓸리지 않게 위치와 크기를 되돌린다.
        Vector3 inverse = InverseLossyScale();
        markerObject.transform.localPosition = Vector3.up * (0.03f * inverse.y);

        // 바닥에 눕혀야 위에서 봤을 때 아이콘처럼 보인다.
        markerObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        SpriteRenderer renderer = markerObject.GetComponent<SpriteRenderer>();
        renderer.sprite = ElementVisual.Circle;
        renderer.color = topViewMarkerColor;

        float native = Mathf.Max(renderer.sprite.bounds.size.x, renderer.sprite.bounds.size.y);
        float fit = native > 0f ? topViewMarkerDiameter / native : 1f;
        markerObject.transform.localScale = Vector3.Scale(Vector3.one * fit, inverse);

        topViewMarker = markerObject.transform;
    }

    private void OnEnable()
    {
        CameraViewController.TopViewChanged += ApplyViewMode;
        ApplyViewMode(CameraViewController.IsTopView);
    }

    private void OnDisable()
    {
        CameraViewController.TopViewChanged -= ApplyViewMode;
    }

    // 탑뷰에서는 소용돌이가 도로 전체를 가려 어느 칸에 원소를 놓는지 보이지 않는다.
    // 연출은 렌더러만 끄고(파티클은 그대로 돌게 두고) 납작한 표식으로 대신한다.
    private void ApplyViewMode(bool topView)
    {
        if (!hideVisualInTopView)
        {
            return;
        }

        if (visualRenderers != null)
        {
            foreach (Renderer renderer in visualRenderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = !topView;
                }
            }
        }

        if (topViewMarker != null)
        {
            topViewMarker.gameObject.SetActive(topView);
        }
    }

    private void HandleStageStarted()
    {
        if (activateOnStageStart)
        {
            Blow();
        }
    }

    private void Update()
    {
        if (!isBlowing || moveSpeed <= 0f)
        {
            return;
        }

        transform.position += transform.forward * (moveSpeed * Time.deltaTime);

        if (travelDistance <= 0f)
        {
            return;
        }

        // 시작점에서 진행 방향으로 얼마나 왔는지만 잰다. 지형 높낮이에 영향받지 않는다.
        float travelled = Vector3.Dot(transform.position - startPosition, transform.forward);
        if (travelled < travelDistance)
        {
            return;
        }

        Settle();
    }

    // 트리거 존이 부른다. 물로 꺼 둔 상태면 아무 일도 일어나지 않는다.
    public void Blow()
    {
        if (isBlowing)
        {
            return;
        }

        if (IsCountered)
        {
            return;
        }

        isBlowing = true;

        SfxPlayer.PlayAt(launchSound, transform.position, launchVolume);

        if (windSource != null)
        {
            windSource.Play();
        }
    }

    public void Stop()
    {
        if (!isBlowing)
        {
            return;
        }

        isBlowing = false;

        if (windSource != null)
        {
            windSource.Stop();
        }
    }

    // 끝까지 지나가서 잦아드는 경우. 설정에 따라 연출까지 걷어낸다.
    private void Settle()
    {
        Stop();

        if (disappearAtEnd)
        {
            gameObject.SetActive(false);
        }
    }

    public void ResetToStart()
    {
        Stop();
        transform.SetPositionAndRotation(startPosition, startRotation);

        // 끝까지 지나가며 꺼 둔 연출을 되살린다.
        // 물로 상쇄해서 꺼진 경우는 ElementTrapCube가 알아서 되살리므로 건드리지 않는다.
        if (!gameObject.activeSelf && wasActiveAtStart && !IsCountered)
        {
            gameObject.SetActive(true);
        }
    }

    private void HandleStageReset(StageReset.Reason reason)
    {
        ResetToStart();
    }

    // 씬에 놓을 때 판정 범위를 눈으로 확인할 수 있게 그려 준다.
    private void OnDrawGizmosSelected()
    {
        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

        Gizmos.color = new Color(0.9f, 0.78f, 0.45f, 0.5f);
        Gizmos.DrawWireSphere(new Vector3(0f, killHeight * 0.5f, 0f), killRadius);

        if (moveSpeed > 0f && travelDistance > 0f)
        {
            Gizmos.color = new Color(0.9f, 0.6f, 0.2f, 0.8f);
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * travelDistance);
        }

        Gizmos.matrix = previous;
    }
}
