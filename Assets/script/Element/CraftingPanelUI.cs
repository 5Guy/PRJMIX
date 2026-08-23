using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

// StageScene에서 마우스 휠 위 / Tab 으로 올라오는 조합창(Infinite Craft 스타일).
//
// 화면 구성
//  ┌───────────┬──────────┬──────────┬──────────┐
//  │           │          │          │          │  ← 가로 하얀선 위 : 세 갈래 보관 칸
//  │  원소 목록  ├──────── 가로 하얀선 ─────────────┤
//  │  (왼쪽)    │  물 : 원소가 둥둥 뜨고, 겹치면 조합    │
//  └───────────┴──────────────────────────────┘
//
// 하이라키는 Assets/Prefab/UI/CraftingPanel.prefab 안에 직접 만들어져 있고, 이 컴포넌트는
// 그 참조를 인스펙터로 받아서 물리/드래그/조합 로직만 담당한다.
public class CraftingPanelUI : MonoBehaviour
{
    // 조합 성공/실패마다 알림이 필요한 쪽(예: 로봇 표정 UI)이 구독한다.
    public static event System.Action CombineSucceeded;
    public static event System.Action CombineFailed;

    // Esc 처리(UIInputManager)가 "조합창이 열려 있으면 Esc는 Tab과 똑같이 닫기만 한다"를
    // 판단할 때 쓴다. 씬에 조합창은 하나뿐이므로 마지막으로 Awake된 인스턴스를 기억해 둔다.
    private static CraftingPanelUI activeInstance;
    public static bool IsOpen { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        CombineSucceeded = null;
        CombineFailed = null;
        activeInstance = null;
        IsOpen = false;
    }

    // Esc를 눌렀을 때 조합창이 열려 있으면 Tab을 누른 것과 똑같이 닫기만 한다.
    public static void CloseIfOpen()
    {
        if (activeInstance != null && activeInstance.isOpen)
        {
            activeInstance.SetOpen(false);
        }
    }

    [Header("원소 데이터")]
    [SerializeField] private ElementData elementData;                     // 시작 원소 하나를 직접 지정하고 싶을 때
    [SerializeField] private List<ElementData> startingElements = new List<ElementData>();
    [SerializeField] private List<ElementCombinationData> combinations = new List<ElementCombinationData>();
    [SerializeField] private TMP_FontAsset font;

    [Header("열고 닫기")]
    [SerializeField] private float slideDuration = 0.25f;
    [SerializeField] private bool startOpened = false;
    [Tooltip("시작 버튼을 누르면 Tab으로 조합창을 열 수 없게 한다. 죽어서 다시 배치할 때 풀린다")]
    [SerializeField] private bool lockAfterStageStart = true;

    [Header("물 느낌")]
    [SerializeField] private float buoyancySpring = 7f;                   // 제자리로 돌아오려는 부력
    [SerializeField] private float waterDrag = 2.4f;                      // 물의 저항
    [SerializeField] private float bobAmplitude = 12f;                    // 출렁이는 폭
    [SerializeField] private float bobSpeed = 1.2f;
    [SerializeField] private float swayStrength = 70f;                    // 좌우로 흔들리는 물살
    [SerializeField] private float splashSpeed = 2200f;                   // 새 원소가 물에 빠지는 속도

    [Header("하이라키 참조 (CraftingPanel 프리팹에서 직접 연결)")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private RectTransform sidebarRect;
    [SerializeField] private RectTransform listContent;
    [SerializeField] private RectTransform waterLayer;
    [SerializeField] private RectTransform dragLayer;
    [SerializeField] private TMP_Text toastText;
    [SerializeField] private CraftToken entryTemplate;
    [SerializeField] private CraftToken tokenTemplate;
    [SerializeField] private RectTransform splashTemplate;

    private readonly List<ElementData> discovered = new List<ElementData>();
    private readonly List<FloatingElement> bubbles = new List<FloatingElement>();

    private float bubbleSize;   // Awake에서 tokenTemplate 크기로 채움 (Inspector 필드 아님)
    private Vector2 openAnchoredPosition;

    private bool isOpen;
    private float openAmount;   // 0 = 완전히 내려감, 1 = 완전히 올라옴
    private Coroutine toastRoutine;

    private PlacementSystem placement;
    private CameraViewController cameraView;
    private bool keepCanvasAlive;             // 배치 중에는 창이 내려가도 드래그가 끊기면 안 된다

    private void Awake()
    {
        activeInstance = this;

#if UNITY_EDITOR
        AutoFillData();
#endif
        EnsureEventSystem();

        if (!ValidateHierarchyReferences())
        {
            enabled = false;
            return;
        }

        bubbleSize = ((RectTransform)tokenTemplate.transform).rect.width;
        openAnchoredPosition = panelRect.anchoredPosition;

        foreach (ElementData data in CollectStartingElements())
        {
            if (Discover(data))
            {
                AddListEntry(data);
            }
        }

        isOpen = startOpened;
        IsOpen = startOpened;
        openAmount = isOpen ? 1f : 0f;
        ApplySlide();

        // 화면 아래쪽의 'ㅅㅅ' 버튼. Tab 말고 마우스로도 조합창을 여닫을 수 있게 한다.
        CraftingToggleButton.Ensure();
    }

    // 조합창이 다 올라왔을 때 창의 윗변이 화면 바닥에서 얼마나 떨어져 있는지(화면 픽셀).
    // 'ㅅㅅ' 버튼이 창 바로 위에 올라앉을 자리를 잡는 데 쓴다.
    public float OpenTopScreenY => panelRect != null && canvas != null ? panelRect.rect.height * canvas.scaleFactor : 0f;

    // 'ㅅㅅ' 버튼처럼 런타임에 만들어지는 UI가 쓸 글꼴.
    // (에디터 밖에서는 AssetDatabase로 찾을 수 없으므로, 프리팹에 저장된 이 값을 빌려 간다)
    public TMP_FontAsset LabelFont => font != null ? font : (toastText != null ? toastText.font : null);

    // 하이라키 UI가 미리 만들어져 있는지 확인한다. 비어 있으면 CraftingPanel 프리팹의
    // 인스펙터 참조 필드가 끊어진 것이므로 프리팹을 열어 직접 연결해야 한다.
    private bool ValidateHierarchyReferences()
    {
        bool valid = canvas != null && panelRect != null && sidebarRect != null && listContent != null &&
            waterLayer != null && dragLayer != null && toastText != null &&
            entryTemplate != null && tokenTemplate != null && splashTemplate != null &&
            entryTemplate.HasViews && tokenTemplate.HasViews &&
            tokenTemplate.GetComponent<FloatingElement>() != null &&
            splashTemplate.GetComponent<Image>() != null;

        if (valid)
        {
            return true;
        }

        Debug.LogError("[CraftingPanelUI] 하이라키 참조가 비어 있습니다. CraftingPanel 프리팹을 열어 인스펙터 참조 필드를 직접 연결해주세요.", this);
        return false;
    }

    private void Start()
    {
        // 월드(도로·플레이어·카메라)는 StageWorldBuilder가 Awake에서 만들므로 Start에서 찾는다.
        placement = FindFirstObjectByType<PlacementSystem>();
        cameraView = FindFirstObjectByType<CameraViewController>();
    }

    private void OnEnable()
    {
        StageStartButton.StageStarted += HandleStageStarted;
    }

    private void OnDisable()
    {
        StageStartButton.StageStarted -= HandleStageStarted;
    }

    private void OnDestroy()
    {
        if (activeInstance == this)
        {
            activeInstance = null;
        }
    }

    // 출발하면 조합창은 내려가고 잠긴다.
    private void HandleStageStarted()
    {
        SetOpen(false);
        RestartPlayerFootstepLoop();
    }

    private void Update()
    {
        HandleInput();
        UpdateSlide();

        // 조합창을 열면 탑뷰, 닫으면 원래 시점. 배치 중에는 PlacementSystem이 맡는다.
        if (cameraView != null && (placement == null || !placement.IsPlacing))
        {
            cameraView.SetTopView(isOpen);
        }

        if (openAmount > 0.001f)
        {
            UpdateWater(Time.unscaledDeltaTime);
        }
    }

    // ───────────────────────────── 입력 ─────────────────────────────

    // 출발한 뒤에는 배치를 바꿀 수 없다. 조합창을 여는 Tab도 막힌다.
    // 죽어서 스테이지가 초기화되면 시작 버튼이 다시 뜨면서 저절로 풀린다.
    public bool IsLocked => lockAfterStageStart && StageStartButton.Exists && StageStartButton.HasStarted;

    // 조합창을 여닫는 건 Tab 하나뿐이다.
    // 마우스 휠은 목록 스크롤과 지도 확대(CameraViewController)에 쓰인다.
    private void HandleInput()
    {
        if (StageFailPanel.IsOpen || IsLocked)
        {
            return;
        }

        if (ReadTabPressed())
        {
            SetOpen(!isOpen);
        }
    }

    private bool ReadTabPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Tab);
#endif
    }

    public void SetOpen(bool open)
    {
        if (isOpen == open)
        {
            return;
        }

        isOpen = open;
        IsOpen = open;

        if (isOpen)
        {
            canvas.gameObject.SetActive(true);
            return;
        }

        RestartPlayerFootstepLoop();
    }

    private void BeginPlacement(CraftToken token)
    {
        if (placement == null || placement.IsPlacing || token.Data == null)
        {
            return;
        }

        keepCanvasAlive = true;

        // 끌고 있는 아이콘은 조합창 밖으로 나가도 마우스를 그대로 따라다닌다.
        // (예전에는 여기서 alpha를 0으로 만들어 감추고 월드의 고스트에 맡겼는데,
        //  그 고스트가 화면에 보이지 않아서 조합창을 벗어나는 순간 아무것도 없는 것처럼 보였다.
        //  어느 칸에 놓이는지는 PlacementSystem의 칸 강조 표시가 알려 준다.)
        placement.Begin(token.Data);
    }

    private void UpdateSlide()
    {
        float target = isOpen ? 1f : 0f;
        if (Mathf.Approximately(openAmount, target))
        {
            return;
        }

        float step = slideDuration > 0f ? Time.unscaledDeltaTime / slideDuration : 1f;
        openAmount = Mathf.MoveTowards(openAmount, target, step);
        ApplySlide();
    }

    private void ApplySlide()
    {
        float height = panelRect.rect.height;
        float eased = openAmount * openAmount * (3f - 2f * openAmount);   // smoothstep
        float closedY = openAnchoredPosition.y - height;
        panelRect.anchoredPosition = new Vector2(openAnchoredPosition.x, Mathf.Lerp(closedY, openAnchoredPosition.y, eased));
        canvas.gameObject.SetActive(openAmount > 0f || isOpen || keepCanvasAlive);
    }

    // ───────────────────────────── 물 시뮬레이션 (하얀선 아래) ─────────────────────────────

    private void UpdateWater(float dt)
    {
        if (bubbles.Count == 0 || waterLayer.rect.width <= 0f)
        {
            return;
        }

        dt = Mathf.Min(dt, 0.033f);
        LayoutRestPositions();

        float time = Time.unscaledTime;
        float damping = Mathf.Exp(-waterDrag * dt);

        for (int i = 0; i < bubbles.Count; i++)
        {
            FloatingElement bubble = bubbles[i];
            Vector2 position = bubble.Rect.anchoredPosition;

            // 기준 자리는 파도를 타듯 위아래로 출렁인다.
            Vector2 rest = bubble.RestPosition;
            rest.y += Mathf.Sin(time * bobSpeed + bubble.Phase) * bobAmplitude;
            rest.x += Mathf.Cos(time * bobSpeed * 0.7f + bubble.Phase) * bobAmplitude * 0.6f;

            // 부력: 기준 자리로 천천히 끌어당기고, 물살로 살짝 흔든다.
            bubble.Velocity += (rest - position) * (buoyancySpring * dt);
            bubble.Velocity += new Vector2(
                Mathf.PerlinNoise(bubble.Phase, time * 0.35f) - 0.5f,
                Mathf.PerlinNoise(time * 0.35f, bubble.Phase) - 0.5f) * (swayStrength * dt);

            bubble.Velocity *= damping;
            bubble.Rect.anchoredPosition = position + bubble.Velocity * dt;
        }

        ResolveCollisions();
        ClampToWater();
    }

    // 원소끼리 부딪히면 서로 밀어낸다.
    private void ResolveCollisions()
    {
        for (int i = 0; i < bubbles.Count; i++)
        {
            for (int j = i + 1; j < bubbles.Count; j++)
            {
                FloatingElement a = bubbles[i];
                FloatingElement b = bubbles[j];

                Vector2 delta = b.Rect.anchoredPosition - a.Rect.anchoredPosition;
                float minDistance = a.Radius + b.Radius;
                float distance = delta.magnitude;

                if (distance >= minDistance)
                {
                    continue;
                }

                Vector2 normal = distance > 0.0001f ? delta / distance : Random.insideUnitCircle.normalized;
                float overlap = (minDistance - distance) * 0.5f;

                a.Rect.anchoredPosition -= normal * overlap;
                b.Rect.anchoredPosition += normal * overlap;

                // 튕겨 나가는 느낌만 살짝 준다.
                float push = Vector2.Dot(b.Velocity - a.Velocity, normal);
                if (push < 0f)
                {
                    Vector2 impulse = normal * push * 0.5f;
                    a.Velocity += impulse;
                    b.Velocity -= impulse;
                }
            }
        }
    }

    private void ClampToWater()
    {
        Rect area = waterLayer.rect;
        float halfWidth = area.width * 0.5f;
        float halfHeight = area.height * 0.5f;

        foreach (FloatingElement bubble in bubbles)
        {
            Vector2 position = bubble.Rect.anchoredPosition;
            Vector2 velocity = bubble.Velocity;

            float limitX = halfWidth - bubble.Radius;
            float limitY = halfHeight - bubble.Radius;

            if (Mathf.Abs(position.x) > limitX)
            {
                position.x = Mathf.Sign(position.x) * limitX;
                velocity.x = -velocity.x * 0.4f;
            }

            // 수면(가로 하얀선)과 바닥을 절대 넘지 않는다.
            if (position.y < -limitY)
            {
                position.y = -limitY;
                velocity.y = -velocity.y * 0.4f;
            }
            else if (position.y > limitY)
            {
                position.y = limitY;
                velocity.y = -Mathf.Abs(velocity.y) * 0.4f;
            }

            bubble.Rect.anchoredPosition = position;
            bubble.Velocity = velocity;
        }
    }

    // 첫 번째 사진처럼 지그재그 격자로 기준 자리를 잡는다.
    private void LayoutRestPositions()
    {
        Rect area = waterLayer.rect;
        float cell = bubbleSize * 1.5f;
        int columns = Mathf.Max(1, Mathf.FloorToInt((area.width - cell * 0.5f) / cell));
        int slot = 0;

        for (int i = 0; i < bubbles.Count; i++)
        {
            // 직접 끌어다 놓은 원소는 그 자리를 지킨다.
            if (bubbles[i].HasCustomRest)
            {
                continue;
            }

            int row = slot / columns;
            int column = slot % columns;
            slot++;

            float x = -area.width * 0.5f + cell * 0.75f + column * cell + (row % 2 == 1 ? cell * 0.5f : 0f);
            float y = area.height * 0.5f - cell * 0.7f - row * cell * 0.85f;

            // 아래로 넘치면 바닥 근처에서 다시 떠 있게 한다.
            float floor = -area.height * 0.5f + cell * 0.5f;
            bubbles[i].RestPosition = new Vector2(Mathf.Min(x, area.width * 0.5f - cell * 0.5f), Mathf.Max(y, floor));
        }
    }

    // ───────────────────────────── 원소 획득 / 조합 ─────────────────────────────

    private List<ElementData> CollectStartingElements()
    {
        List<ElementData> list = new List<ElementData>();

        if (elementData != null)
        {
            list.Add(elementData);
        }

        foreach (ElementData data in startingElements)
        {
            if (data != null && !list.Contains(data))
            {
                list.Add(data);
            }
        }

        return list;
    }

    // 처음 보는 원소인지 기록만 한다. 목록(사이드바) 추가는 호출하는 쪽에서 따로 결정한다.
    private bool Discover(ElementData data)
    {
        if (data == null || discovered.Contains(data))
        {
            return false;
        }

        discovered.Add(data);
        return true;
    }

    // 씬 어디서든 원소를 조합창으로 돌려보낼 때 쓰는 지름길.
    // (함정에 써서 사라진 원소를 되돌릴 때처럼, 조합창 참조가 없는 곳에서 부른다)
    public static void ReturnToPanel(ElementData data)
    {
        if (data == null)
        {
            return;
        }

        CraftingPanelUI panel = FindFirstObjectByType<CraftingPanelUI>();
        if (panel == null)
        {
            Debug.LogWarning($"[CraftingPanelUI] 조합창을 찾지 못해 '{data.ElementName}'을 돌려주지 못했습니다.");
            return;
        }

        panel.ReturnElement(data);
    }

    // 맵에 놓았던 원소를 도로 조합창의 물로 돌려보낸다. (PlacementSystem이 회수할 때 호출)
    public void ReturnElement(ElementData data)
    {
        if (data == null)
        {
            return;
        }

        Discover(data);
        AddFloating(data, true);
        ShowToast($"{data.ElementName}을(를) 회수했어요");
    }

    // 종류로 원소를 찾는다. 흙 벽처럼 "이 종류면 이렇게 동작" 하는 규칙에서 쓴다.
    public ElementData FindElementOfType(ElementType type)
    {
        foreach (ElementData data in discovered)
        {
            if (data != null && data.ElementType == type)
            {
                return data;
            }
        }

        foreach (ElementData data in startingElements)
        {
            if (data != null && data.ElementType == type)
            {
                return data;
            }
        }

        return null;
    }

    private ElementData FindResult(ElementData a, ElementData b)
    {
        foreach (ElementCombinationData combination in combinations)
        {
            if (combination != null && combination.IsMatch(a, b) && combination.ResultElement != null)
            {
                return combination.ResultElement;
            }
        }

        return null;
    }

    // 조합에 성공하면 재료 두 개가 사라지므로 true를 돌려준다.
    private bool TryCombine(CraftToken token)
    {
        FloatingElement partner = FindOverlapping(token);
        if (partner == null)
        {
            return false;
        }

        ElementData result = FindResult(token.Data, partner.Data);
        if (result == null)
        {
            ShowToast($"{token.Data.ElementName} + {partner.Data.ElementName} … 조합할 수 없어요");
            CombineFailed?.Invoke();
            return false;
        }

        RemoveFloating(partner);
        RemoveFloating(token.GetComponent<FloatingElement>());

        bool isNew = Discover(result);
        if (isNew)
        {
            AddListEntry(result);   // 새로 발견한 원소는 왼쪽 목록에도 추가한다.
        }

        AddFloating(result, true);

        // 조합에 성공하면 결과 원소가 들고 있는 점수(엑셀 '조합식' 시트의 점수)만큼 오른다.
        ScoreSystem.Combined(result);

        ShowToast(isNew ? $"새로운 원소 발견! {result.ElementName}" : $"{result.ElementName} 조합 성공");
        CombineSucceeded?.Invoke();
        return true;
    }

    private void PlayPlayerInteractionSound(ElementData result)
    {
        Transform player = PlayerLocator.FindPlayer();
        if (player == null)
        {
            Debug.LogWarning($"[CraftingPanelUI] '{result.ElementName}' 조합 성공, 하지만 플레이어를 찾지 못해 상호작용 사운드를 재생하지 못했습니다.", this);
            return;
        }

        PlayerAudio audio = player != null ? player.GetComponentInChildren<PlayerAudio>() : null;
        if (audio == null)
        {
            Debug.LogWarning($"[CraftingPanelUI] '{result.ElementName}' 조합 성공, 하지만 '{player.name}' 하위에서 PlayerAudio를 찾지 못했습니다.", this);
            return;
        }

        Debug.Log($"[CraftingPanelUI] '{result.ElementName}' 조합 성공, '{player.name}'의 PlayerAudio.PlayInteractionSound() 호출", this);
        audio.PlayInteractionSound();
    }

    private void RestartPlayerFootstepLoop()
    {
        Transform player = PlayerLocator.FindPlayer();
        PlayerAudio audio = player != null ? player.GetComponentInChildren<PlayerAudio>() : null;
        audio?.RestartFootstepLoop();
    }

    private FloatingElement FindOverlapping(CraftToken token)
    {
        FloatingElement nearest = null;
        float nearestDistance = bubbleSize * 0.9f;

        foreach (FloatingElement other in bubbles)
        {
            if (other == null || other.Rect == token.Rect)
            {
                continue;
            }

            float distance = Vector2.Distance(other.Rect.anchoredPosition, token.Rect.anchoredPosition);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = other;
            }
        }

        return nearest;
    }

    private void RemoveFloating(FloatingElement bubble)
    {
        if (bubble == null)
        {
            return;
        }

        bubbles.Remove(bubble);
        Destroy(bubble.gameObject);
    }

    // ───────────────────────────── 드래그 (CraftToken이 호출) ─────────────────────────────

    public CraftToken SpawnCraftToken(ElementData data, Vector2 screenPosition)
    {
        // 왼쪽 목록에서 재료를 하나 꺼내는 순간 점수가 1점 깎인다.
        ScoreSystem.TakeOutMaterial();

        CraftToken token = CreateToken(dragLayer, data);
        MoveTokenToPointer(token, screenPosition);
        return token;
    }

    public void OnTokenDragBegin(CraftToken token)
    {
        // 끄는 동안에는 물 시뮬레이션에서 빼고 최상위 레이어로 올린다.
        FloatingElement bubble = token.GetComponent<FloatingElement>();
        if (bubble != null)
        {
            bubbles.Remove(bubble);
        }

        token.Rect.SetParent(dragLayer, false);
        token.transform.SetAsLastSibling();
        SetTokenRaycast(token, false);
    }

    public void MoveTokenToPointer(CraftToken token, Vector2 screenPosition)
    {
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(dragLayer, screenPosition, UICamera, out Vector2 local))
        {
            token.Rect.anchoredPosition = local;
        }

        // 조합창 밖으로 끌고 나가면 맵에 배치할 수 있는 실체가 되고, 다시 안으로 들어오면 취소된다.
        if (placement != null)
        {
            bool outside = !RectTransformUtility.RectangleContainsScreenPoint(panelRect, screenPosition, UICamera);

            if (outside && !placement.IsPlacing)
            {
                BeginPlacement(token);
            }
            else if (!outside && placement.IsPlacing)
            {
                CancelPlacement();
            }

            if (placement.IsPlacing)
            {
                placement.UpdatePreview(screenPosition);
            }
        }
    }

    private void CancelPlacement()
    {
        placement.Cancel();
        keepCanvasAlive = false;
    }

    public void OnTokenDropped(CraftToken token, Vector2 screenPosition)
    {
        // 맵에 배치하는 중이면 격자 칸 배치로 처리한다.
        if (placement != null && placement.IsPlacing)
        {
            bool placed = placement.Confirm(screenPosition);
            keepCanvasAlive = false;

            if (placed)
            {
                Destroy(token.gameObject);
            }
            else
            {
                // 배치에 실패하면 조합창의 물로 되돌린다.
                SetTokenRaycast(token, true);
                DropIntoWater(token, screenPosition);
            }

            return;
        }

        SetTokenRaycast(token, true);

        // 조합창 안(물)에 놓으면 다시 둥둥 뜨고, 겹친 원소가 있으면 조합한다.
        if (RectTransformUtility.RectangleContainsScreenPoint(waterLayer, screenPosition, UICamera))
        {
            DropIntoWater(token, screenPosition);
            return;
        }

        // 그 밖(목록 위 등)에 놓으면 취소한 것으로 보고 점수를 돌려준 뒤 사라진다.
        ScoreSystem.PutBackMaterial();
        Destroy(token.gameObject);
    }

    private void DropIntoWater(CraftToken token, Vector2 screenPosition)
    {
        token.Rect.SetParent(waterLayer, false);

        FloatingElement bubble = token.GetComponent<FloatingElement>();
        bubble.Velocity = Vector2.zero;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(waterLayer, screenPosition, UICamera, out Vector2 local))
        {
            Rect area = waterLayer.rect;
            local.x = Mathf.Clamp(local.x, -area.width * 0.5f + bubble.Radius, area.width * 0.5f - bubble.Radius);
            local.y = Mathf.Clamp(local.y, -area.height * 0.5f + bubble.Radius, area.height * 0.5f - bubble.Radius);

            // 놓은 자리를 기준 자리로 삼아, 격자 뒤쪽으로 밀려나지 않게 한다.
            token.Rect.anchoredPosition = local;
            bubble.RestPosition = local;
            bubble.HasCustomRest = true;
        }

        // 겹쳐 놓았으면 조합(재료는 사라짐), 아니면 그대로 물에 띄운다.
        if (TryCombine(token))
        {
            return;
        }

        bubbles.Add(bubble);
    }

    private void SetTokenRaycast(CraftToken token, bool enabled)
    {
        foreach (Graphic graphic in token.GetComponentsInChildren<Graphic>(true))
        {
            graphic.raycastTarget = enabled && graphic is Image;
        }
    }

    private Camera UICamera => canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

    // ───────────────────────────── UI 생성 ─────────────────────────────

    private void AddListEntry(ElementData data)
    {
        CraftToken entry = Instantiate(entryTemplate, listContent);
        entry.name = $"Entry_{data.ElementName}";
        entry.gameObject.SetActive(true);
        entry.Init(this, data, true);
        entry.ApplyVisual(data);
    }

    // 하얀선 아래 물에 원소를 띄운다. withSplash면 수면 위에서 풍덩 떨어진다.
    private void AddFloating(ElementData data, bool withSplash)
    {
        CraftToken token = CreateToken(waterLayer, data);
        FloatingElement bubble = token.GetComponent<FloatingElement>();
        bubbles.Add(bubble);

        LayoutRestPositions();

        if (withSplash)
        {
            // 수면(하얀선)을 넘지 않도록 수면 바로 아래에서 시작해 깊이 잠겼다가 떠오른다.
            float surface = waterLayer.rect.height * 0.5f - bubble.Radius;
            bubble.Rect.anchoredPosition = new Vector2(bubble.RestPosition.x, surface);
            bubble.Velocity = new Vector2(Random.Range(-60f, 60f), -splashSpeed);
            StartCoroutine(SplashRoutine(new Vector2(bubble.RestPosition.x, waterLayer.rect.height * 0.5f - 6f)));
        }
        else
        {
            bubble.Rect.anchoredPosition = bubble.RestPosition + Random.insideUnitCircle * 20f;
            bubble.Velocity = Random.insideUnitCircle * 40f;
        }
    }

    private IEnumerator SplashRoutine(Vector2 surfacePoint)
    {
        RectTransform ring = Instantiate(splashTemplate, waterLayer);
        ring.gameObject.SetActive(true);
        ring.anchoredPosition = surfacePoint;
        Image image = ring.GetComponent<Image>();

        const float duration = 0.55f;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float k = t / duration;
            ring.localScale = new Vector3(1f + k * 2.4f, 0.35f + k * 0.5f, 1f);
            image.color = new Color(1f, 1f, 1f, 0.8f * (1f - k));
            yield return null;
        }

        Destroy(ring.gameObject);
    }

    private CraftToken CreateToken(RectTransform parent, ElementData data)
    {
        CraftToken token = Instantiate(tokenTemplate, parent);
        token.name = $"Token_{data.ElementName}";
        token.gameObject.SetActive(true);
        token.Init(this, data, false);
        token.ApplyVisual(data);

        FloatingElement bubble = token.GetComponent<FloatingElement>();
        bubble.Data = data;
        bubble.Rect = token.Rect;
        bubble.Radius = bubbleSize * 0.5f;
        bubble.Phase = Random.Range(0f, 20f);

        return token;
    }

    private void ShowToast(string message)
    {
        if (toastRoutine != null)
        {
            StopCoroutine(toastRoutine);
        }

        toastRoutine = StartCoroutine(ToastRoutine(message));
    }

    private IEnumerator ToastRoutine(string message)
    {
        toastText.text = message;

        for (float t = 0f; t < 1.6f; t += Time.unscaledDeltaTime)
        {
            float alpha = t < 1.2f ? 1f : 1f - (t - 1.2f) / 0.4f;
            toastText.color = new Color(1f, 0.95f, 0.6f, alpha);
            yield return null;
        }

        toastText.color = new Color(1f, 0.95f, 0.6f, 0f);
        toastRoutine = null;
    }

    // ───────────────────────────── 공용 유틸 ─────────────────────────────

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        eventSystem.AddComponent<InputSystemUIInputModule>();
#else
        eventSystem.AddComponent<StandaloneInputModule>();
#endif
    }

#if UNITY_EDITOR
    // CraftingDataBuildPreprocessor가 빌드 직전에도 호출하므로 public이어야 한다.
    // 주의: 이 메뉴는 Project 창의 CraftingPanel 프리팹 에셋에서만 실행하세요.
    // 씬 안의 인스턴스에서 실행하면 그 씬만의 프리팹 오버라이드로 값이 고정돼서,
    // 나중에 프리팹을 갱신해도 그 씬에는 반영되지 않습니다(빌드가 이를 감지하면 막습니다).
    [ContextMenu("원소 / 조합 데이터 자동 채우기")]
    public void AutoFillData()
    {
        // Nomal / Combination 폴더가 항상 정답이므로, 매번 새로 스캔해서 덮어쓴다.
        // (전에는 리스트가 비어있을 때만 채웠는데, 그러면 나중에 폴더에 새 원소·조합을
        //  추가해도 이미 채워진 리스트에는 반영이 안 되고, 지워진 원소를 가리키는
        //  깨진 참조도 그대로 남아있는 문제가 있었다.)
        startingElements.Clear();

        // Nomal 폴더에는 기본 원소와 조합으로 만든 원소가 함께 들어있으므로,
        // isBaseElement 체크가 된 것만 시작 목록으로 가져온다.
        foreach (string guid in AssetDatabase.FindAssets("t:ElementData", new[] { "Assets/Data/Nomal" }))
        {
            ElementData data = AssetDatabase.LoadAssetAtPath<ElementData>(AssetDatabase.GUIDToAssetPath(guid));
            if (data != null && data.IsBaseElement)
            {
                startingElements.Add(data);
            }
        }

        combinations.Clear();

        foreach (string guid in AssetDatabase.FindAssets("t:ElementCombinationData", new[] { "Assets/Data/Combination" }))
        {
            ElementCombinationData data = AssetDatabase.LoadAssetAtPath<ElementCombinationData>(AssetDatabase.GUIDToAssetPath(guid));
            if (data != null)
            {
                combinations.Add(data);
            }
        }

        if (font == null)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset NeoDunggeunmo"))
            {
                font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (font != null)
                {
                    break;
                }
            }
        }

        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(this);
        }
    }
#endif
}
