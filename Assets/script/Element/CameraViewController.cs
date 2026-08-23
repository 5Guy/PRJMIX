using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// 기본 비스듬한 시점 ↔ 완전한 탑뷰를 부드럽게 오간다.
// 조합창을 열거나 원소를 배치하는 동안에는 탑뷰로 내려다본다.
//
// 게임 시작 전에는 플레이어의 출발 지점에 고정되어 있다가,
// 시작 버튼을 누르면 그때부터 플레이어를 따라다닌다.
[RequireComponent(typeof(Camera))]
public class CameraViewController : MonoBehaviour
{
    [Tooltip("비워두면 Player 태그가 붙은 오브젝트를 찾아 쓴다")]
    [SerializeField] private Transform target;

    // 기본은 첫 번째 사진처럼 옆에서 살짝 내려다보는 시점(도로가 화면 좌우로 흐른다).
    [Header("기본 시점 (측면)")]
    [SerializeField] private Vector3 defaultOffset = new Vector3(-18f, 5f, 0f);
    [SerializeField] private Vector3 defaultEuler = new Vector3(14f, 90f, 0f);
    [SerializeField] private float defaultOrthoSize = 8f;

    // 조합창을 열거나 배치할 때는 완전한 탑뷰. 방위를 유지하려고 yaw는 그대로 둔다.
    [Header("탑뷰")]
    [SerializeField] private Vector3 topOffset = new Vector3(0f, 28f, 0f);
    [SerializeField] private Vector3 topEuler = new Vector3(90f, 90f, 0f);
    [SerializeField] private float topOrthoSize = 13f;

    [SerializeField] private float transitionSpeed = 4f;

    [Header("플레이어 따라가기")]
    [Tooltip("켜면 시작 버튼을 누르기 전까지는 플레이어의 출발 지점에 고정된다")]
    [SerializeField] private bool followOnlyAfterStageStart = true;
    [Tooltip("위 각도 대신 오프셋 방향에서 각도를 계산해, 대상이 화면 한가운데 오게 한다")]
    [SerializeField] private bool aimAtTarget = true;

    [Header("맵 이동 (마우스 좌클릭 드래그)")]
    [SerializeField] private bool allowPan = true;
    [SerializeField] private float panSpeed = 1.4f;
    [Tooltip("켜면 어느 시점에서든 화면 좌우로만 훑어볼 수 있다")]
    [SerializeField] private bool horizontalPanOnly = true;

    [Header("탑뷰 고정")]
    [Tooltip("켜면 탑뷰로 들어간 순간의 지점에 카메라가 멈춰, 플레이어를 따라가지 않는다")]
    [SerializeField] private bool lockTopViewToAnchor = true;

    [Header("맵 확대 / 축소 (마우스 휠)")]
    [SerializeField] private bool allowZoom = true;
    [Tooltip("휠 한 칸에 바뀌는 비율")]
    [SerializeField] private float zoomStep = 0.12f;
    [Tooltip("1보다 작을수록 가까이 당겨 본다")]
    [SerializeField] private float minZoom = 0.35f;
    [SerializeField] private float maxZoom = 2.5f;

    // 탑뷰 상태가 실제로 바뀔 때만 알려준다 — 배치된 원소들이 이걸 듣고 2D/3D 모습을 바꾼다.
    public static event System.Action<bool> TopViewChanged;
    public static bool IsTopView { get; private set; }

    // 정적 값은 플레이 세션을 넘어 남는다. 지난 판을 탑뷰인 채로 끝냈다면
    // 다음 판이 시작하자마자 2D 모습으로 켜져서 3D 오브젝트가 통째로 안 보인다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsTopView = false;
        TopViewChanged = null;
    }

    private bool topView;
    private Camera view;
    private Vector3 panOffset;      // 기본 시점에서 훑어본 양
    private Vector3 topPanOffset;   // 탑뷰에서 훑어본 양(시점끼리 섞이지 않게 따로 둔다)
    private Vector3 topAnchor;      // 탑뷰로 들어간 순간 바라보던 지점
    private float zoom = 1f;
    private bool following;
    private Vector3 anchor;   // 따라가지 않는 동안 바라볼 고정 지점(= 플레이어 출발 지점)
    private Transform inspectorTarget;
    private bool panBlocked;
    private PlacementSystem placement;

    public bool IsFollowing => following;

    private void Awake()
    {
        view = GetComponent<Camera>();

        // 인스펙터에 직접 넣어 둔 값을 기억해 둔다.
        // 다른 스크립트(MapPlacementArea 등)가 Awake에서 SetTarget을 부를 수 있으므로,
        // 무엇이 "원래 지정된 대상"이었는지는 여기서만 알 수 있다.
        inspectorTarget = target;
    }

    // 카메라는 항상 걸어다니는 플레이어를 기준으로 시작한다.
    // 인스펙터에 엉뚱한 오브젝트(골 지점 등)가 물려 있어도 여기서 바로잡는다.
    private void Start()
    {
        target = ResolveTarget();
        anchor = target != null ? target.position : transform.position;

        // 시작 버튼이 없는 씬이라면 기다릴 것이 없으므로 바로 따라간다.
        following = !followOnlyAfterStageStart || !StageStartButton.Exists || StageStartButton.HasStarted;

        SnapToCurrentView();
    }

    private void OnEnable()
    {
        StageStartButton.StageStarted += HandleStageStarted;
        StageReset.Requested += HandleStageReset;
    }

    private void OnDisable()
    {
        StageStartButton.StageStarted -= HandleStageStarted;
        StageReset.Requested -= HandleStageReset;
    }

    // 시작 버튼을 누르면 확대·이동해 둔 것을 모두 초기값으로 되돌리고
    // 그때부터 플레이어를 따라다닌다.
    private void HandleStageStarted()
    {
        following = true;

        // 여기서 topView 필드만 바꾸면 IsTopView와 어긋난다.
        // 그러면 조합창이 닫히며 부르는 SetTopView(false)가 "이미 false"라며 그냥 돌아가 버려서
        // TopViewChanged가 끝내 발행되지 않고, 배치한 원소와 모래바람이 2D 아이콘인 채로 남는다.
        SetTopView(false);

        ResetPan();
        ResetZoom();

        // 부드럽게 미끄러지면 출발 순간 화면이 크게 흔들린다. 바로 제자리에 맞춘다.
        SnapToCurrentView();
    }

    // 죽어서 처음 상태로 돌아가면 다시 출발 지점 고정으로 되돌린다.
    private void HandleStageReset(StageReset.Reason reason)
    {
        if (reason != StageReset.Reason.PlayerDeath)
        {
            return;
        }

        following = false;
        anchor = target != null ? target.position : anchor;
        ResetPan();
        ResetZoom();
        SnapToCurrentView();
    }

    private Transform ResolveTarget()
    {
        // 인스펙터에 플레이어를 직접 물려 놨으면 그대로 쓴다.
        if (PlayerLocator.IsPlayer(inspectorTarget))
        {
            return inspectorTarget;
        }

        Transform player = PlayerLocator.FindPlayer();
        if (player != null)
        {
            if (inspectorTarget != null && inspectorTarget != player)
            {
                Debug.LogWarning(
                    $"{name}: 카메라 대상이 플레이어가 아닌 '{inspectorTarget.name}'로 지정돼 있어 '{player.name}'로 바꿉니다.",
                    this);
            }

            return player;
        }

        Debug.LogWarning($"{name}: 플레이어를 찾지 못했습니다. 카메라 대상을 인스펙터에 직접 넣어 주세요.", this);
        return inspectorTarget;
    }

    public void SetTarget(Transform value)
    {
        target = value;

        if (value != null && !following)
        {
            anchor = value.position;
        }
    }

    public void SetTopView(bool value)
    {
        // 어떤 이유로든 정적 값(IsTopView)이 어긋났다면 같은 값이라도 다시 맞춰 알린다.
        // 2D 아이콘인 채로 굳어 버리는 것보다 이벤트를 한 번 더 보내는 편이 안전하다.
        if (topView == value && IsTopView == value)
        {
            return;
        }

        // 탑뷰로 들어가는 순간의 지점을 붙잡아 둔다. 조합창을 여는 동안에는
        // 플레이어가 달리고 있어도 카메라가 여기서 움직이지 않는다.
        if (value)
        {
            topAnchor = following && target != null ? target.position : anchor;
            topPanOffset = Vector3.zero;
        }

        topView = value;
        IsTopView = value;
        TopViewChanged?.Invoke(value);
    }

    // 전환 없이 즉시 맞춘다(시작할 때 한 번).
    public void SnapToCurrentView()
    {
        transform.SetPositionAndRotation(DesiredPosition, Quaternion.Euler(DesiredEuler));

        if (view == null)
        {
            view = GetComponent<Camera>();
        }

        if (view != null && view.orthographic)
        {
            view.orthographicSize = DesiredOrthoSize;
        }
    }

    // 따라가는 중이면 플레이어의 지금 위치, 아니면 출발 지점에 고정된다.
    // 탑뷰를 고정해 뒀다면 들어간 순간의 지점에서 꿈쩍하지 않는다.
    private Vector3 Pivot
    {
        get
        {
            if (topView && lockTopViewToAnchor)
            {
                return topAnchor;
            }

            return following && target != null ? target.position : anchor;
        }
    }

    // 지금 시점에서 훑어본 양.
    private Vector3 CurrentPan => topView ? topPanOffset : panOffset;

    private Vector3 DesiredPosition
    {
        get
        {
            // 확대는 시점 오프셋을 줄여 카메라를 바닥 쪽으로 당기는 것으로 표현한다.
            return Pivot + (topView ? topOffset : defaultOffset) * zoom + CurrentPan;
        }
    }

    // 카메라가 바라볼 각도.
    //
    // 인스펙터의 각도(defaultEuler)와 위치(defaultOffset)는 서로 따로 놀 수 있다.
    // 예를 들어 StageScene 3은 오프셋 방향이 약 -49°인데 각도는 -23.6°로 적혀 있어서,
    // 카메라가 플레이어를 정면으로 보지 않고 한쪽으로 치우쳐 담는다.
    // aimAtTarget을 켜면 오프셋에서 각도를 직접 계산해 플레이어가 화면 한가운데 오게 한다.
    private Vector3 DesiredEuler
    {
        get
        {
            Vector3 fallback = topView ? topEuler : defaultEuler;

            if (!aimAtTarget)
            {
                return fallback;
            }

            Vector3 toPivot = -(topView ? topOffset : defaultOffset);

            // 바로 위에서 내려다보는 탑뷰는 방향이 위쪽과 겹쳐 각도를 정할 수 없다. 적어 둔 값을 쓴다.
            if (toPivot.sqrMagnitude < 0.0001f ||
                Mathf.Abs(Vector3.Dot(toPivot.normalized, Vector3.up)) > 0.999f)
            {
                return fallback;
            }

            return Quaternion.LookRotation(toPivot, Vector3.up).eulerAngles;
        }
    }

    private float DesiredOrthoSize => (topView ? topOrthoSize : defaultOrthoSize) * zoom;

    public void ResetZoom()
    {
        zoom = 1f;
    }

    // 마우스 휠로 지도를 확대/축소한다. 조합창 위에서 굴리는 휠은 목록 스크롤이므로 건너뛴다.
    private void UpdateZoom()
    {
        if (!allowZoom)
        {
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        float scroll = ReadScroll();
        if (Mathf.Abs(scroll) < 0.01f)
        {
            return;
        }

        // 휠 한 칸의 크기는 환경마다 다르므로 방향만 쓴다.
        zoom = Mathf.Clamp(zoom * (1f - Mathf.Sign(scroll) * zoomStep), minZoom, maxZoom);
    }

    private static float ReadScroll()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
#else
        return Input.GetAxis("Mouse ScrollWheel");
#endif
    }

    // 마우스 왼쪽 버튼을 누른 채 끌면 맵을 훑어볼 수 있다.
    public void ResetPan()
    {
        panOffset = Vector3.zero;
        topPanOffset = Vector3.zero;
    }

    private void UpdatePan()
    {
        if (!allowPan)
        {
            return;
        }

        UpdatePanBlock();

        if (panBlocked)
        {
            return;
        }

        Vector2 delta = ReadDragDelta();
        if (delta.sqrMagnitude < 0.0001f)
        {
            return;
        }

        // 화면에서 끈 방향을 바닥 평면 위의 이동으로 바꾼다.
        Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);

        if (forward.sqrMagnitude < 0.0001f)
        {
            // 탑뷰라 카메라가 바닥을 수직으로 보고 있으면 위쪽 방향을 기준으로 삼는다.
            forward = Vector3.ProjectOnPlane(transform.up, Vector3.up);
        }

        forward = forward.normalized;

        // 기본 시점이든 탑뷰든 화면 좌우로만 훑는다. 위아래로 끈 만큼은 버린다.
        Vector3 move = horizontalPanOnly
            ? right * delta.x
            : right * delta.x + forward * delta.y;

        if (topView)
        {
            topPanOffset -= move * PanScale();
        }
        else
        {
            panOffset -= move * PanScale();
        }
    }

    // 화면에서 1픽셀 끈 것이 월드에서 몇 미터인지. 카메라 종류에 따라 계산이 다르다.
    private float PanScale()
    {
        if (view == null || Screen.height <= 0)
        {
            return panSpeed * 0.01f;
        }

        if (view.orthographic)
        {
            return panSpeed * (view.orthographicSize * 2f / Screen.height);
        }

        // 원근 카메라는 바닥까지의 거리에 따라 같은 픽셀이라도 이동량이 달라진다.
        float groundY = Pivot.y;
        float distance = Mathf.Max(1f, Mathf.Abs(transform.position.y - groundY));
        float worldHeight = 2f * distance * Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
        return panSpeed * (worldHeight / Screen.height);
    }

    // 왼쪽 버튼은 조합창 조작과 원소 배치에도 쓰인다.
    // 버튼을 누른 순간 UI 위였거나 배치 중이면, 손을 뗄 때까지 그 드래그로는 맵을 움직이지 않는다.
    private void UpdatePanBlock()
    {
        if (!ReadDragButtonHeld())
        {
            panBlocked = false;
            return;
        }

        if (!ReadDragButtonPressedThisFrame())
        {
            return;
        }

        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        panBlocked = overUI || IsPlacing();
    }

    private bool IsPlacing()
    {
        if (placement == null)
        {
            placement = FindFirstObjectByType<PlacementSystem>();
        }

        return placement != null && placement.IsPlacing;
    }

    private static bool ReadDragButtonHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
        return Input.GetMouseButton(0);
#endif
    }

    private static bool ReadDragButtonPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    private static Vector2 ReadDragDelta()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current == null || !Mouse.current.leftButton.isPressed)
        {
            return Vector2.zero;
        }

        return Mouse.current.delta.ReadValue();
#else
        if (!Input.GetMouseButton(0))
        {
            return Vector2.zero;
        }

        return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 10f;
#endif
    }

    private void LateUpdate()
    {
        UpdatePan();
        UpdateZoom();

        float t = 1f - Mathf.Exp(-transitionSpeed * Time.unscaledDeltaTime);
        transform.position = Vector3.Lerp(transform.position, DesiredPosition, t);
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(DesiredEuler), t);

        if (view != null && view.orthographic)
        {
            view.orthographicSize = Mathf.Lerp(view.orthographicSize, DesiredOrthoSize, t);
        }
    }
}
