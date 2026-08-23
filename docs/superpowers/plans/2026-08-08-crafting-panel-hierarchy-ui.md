# Crafting Panel 하이라키 UI 전환 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. **Task 4는 Unity 에디터 GUI를 직접 조작해야 하는 사용자 전용 작업입니다 — 코딩 에이전트에게 위임하지 마세요.**

**Goal:** `CraftingPanelUI`가 런타임 코드로 매번 새로 만들던 조합창 UI(패널/사이드바/물 영역/목록 항목/원소 토큰/물결 효과)를 `CraftingPanel` 프리팹 안의 실제 하이라키 GameObject로 옮겨서, 사용자가 에디터에서 이미지 교체·배치 조정·템플릿 편집을 직접 할 수 있게 한다.

**Architecture:** 정적인 틀은 에디터 전용 `GenerateHierarchy()`(ContextMenu)가 한 번 만들어서 `[SerializeField]` 참조로 저장하고, 런타임 `Awake()`는 그 참조를 그대로 쓴다. 목록 항목/원소 토큰/물결 효과는 비활성 상태의 템플릿 GameObject를 `Instantiate()`해서 쓴다. 드래그·조합·물 물리·슬라이드 로직은 전혀 손대지 않는다.

**Tech Stack:** Unity 6000.0.81f1, C# (LangVersion 9.0), UGUI + TextMeshPro.

**참고 스펙:** `docs/superpowers/specs/2026-08-08-crafting-panel-hierarchy-ui-design.md`

## Global Constraints

- 이 프로젝트에는 자동화된 테스트 프레임워크가 없다 (Unity Test Runner 미설치, 헤드리스 실행 환경 없음). 각 작업 뒤 검증은 `dotnet build Assembly-CSharp.csproj`로 컴파일 확인을 하는 것으로 대체한다 (실행 위치: 프로젝트 루트 `C:\Users\Gunho\Desktop\molra`).
- 버전 관리는 Plastic SCM(`cm` CLI, git 아님)이다. **어떤 작업 단계에서도 자동으로 체크인하지 않는다** — 사용자가 직접 체크인한다.
- `Assets/script/Element/ElementVisual.cs`, `FloatingElement.cs`, `PlacementSystem.cs`는 수정하지 않는다.
- 드래그, 조합 판정, 물 물리 시뮬레이션, 슬라이드 열고닫기의 **동작**은 바꾸지 않는다 — 시각적 요소가 어떻게 생성되는지만 바뀐다.
- `CraftingPanelUI`의 기존 필드명(`canvas`, `panelRect`, `sidebarRect`, `listContent`, `waterLayer`, `dragLayer`, `toastText`)은 그대로 유지한다 (다른 메서드들이 이 이름을 그대로 참조하므로 불필요한 연쇄 수정을 피한다).

---

### Task 1: CraftToken — 아이콘/라벨 뷰 참조 + ApplyVisual 추가

**Files:**
- Modify: `Assets/script/Element/CraftToken.cs`

**Interfaces:**
- Produces: `public void ApplyVisual(ElementData data, bool flipLabelForBrightIcon)` — Task 2에서 `CraftingPanelUI.AddListEntry`/`CreateToken`이 호출한다. `public void EditorAssignViews(Image iconRef, TMP_Text labelRef)` (에디터 전용) — Task 3에서 템플릿 생성 코드가 호출한다.
- Consumes: `ElementVisual.GetColor(ElementData)`, `ElementVisual.Circle` (기존, `Assets/script/Element/ElementVisual.cs`, 변경 없음).

- [ ] **Step 1: usings와 필드 추가**

`Assets/script/Element/CraftToken.cs` 최상단을 다음으로 교체:

```csharp
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif
```

`private CraftToken dragTarget;` 바로 아래에 다음 필드 추가:

```csharp
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text label;
```

- [ ] **Step 2: ApplyVisual / EditorAssignViews 메서드 추가**

`Init(...)` 메서드 바로 아래에 추가:

```csharp
    // 아이콘 스프라이트/색과 이름 글자를 채운다.
    // flipLabelForBrightIcon이 true면(물에 뜨는 토큰처럼 글자가 아이콘 위에 바로 얹히는 경우)
    // 아이콘이 밝은 단색일 때 글자색을 뒤집는다. 목록 항목처럼 글자가 아이콘과 안 겹치면 false로 둔다.
    public void ApplyVisual(ElementData data, bool flipLabelForBrightIcon)
    {
        Color iconColor = ElementVisual.GetColor(data);
        icon.sprite = data.Icon != null ? data.Icon : ElementVisual.Circle;
        icon.color = data.Icon != null ? Color.white : iconColor;

        label.text = data.ElementName;

        if (!flipLabelForBrightIcon)
        {
            return;
        }

        float luminance = iconColor.r * 0.299f + iconColor.g * 0.587f + iconColor.b * 0.114f;
        label.color = data.Icon == null && luminance > 0.65f
            ? new Color(0.06f, 0.09f, 0.16f)
            : Color.white;
    }

#if UNITY_EDITOR
    // 하이라키 생성기가 템플릿을 만들 때만 쓴다. 런타임에는 절대 호출하지 않는다.
    public void EditorAssignViews(Image iconRef, TMP_Text labelRef)
    {
        icon = iconRef;
        label = labelRef;
        EditorUtility.SetDirty(this);
    }
#endif
```

- [ ] **Step 3: 컴파일 확인**

Run: `dotnet build Assembly-CSharp.csproj -v:q`
Expected: `빌드했습니다.` / `오류 0개` (0 errors). `icon`/`label`/`ApplyVisual`가 아직 아무 데서도 안 쓰여도 컴파일 에러는 아니다 (Task 2에서 쓰기 시작함).

---

### Task 2: CraftingPanelUI — 하이라키 참조 필드 추가 + 런타임 재배선

이 작업이 끝나면 `BuildUI()`와 그 하위 메서드들은 더 이상 호출되지 않는 죽은 코드로 남는다 (Task 3에서 에디터 생성기로 옮기며 정리한다). 의도된 중간 상태이며, 이 작업만으로는 Play 모드에서 조합창이 아직 뜨지 않는다(하이라키 참조가 비어 있으므로) — 그것도 의도된 상태다.

**Files:**
- Modify: `Assets/script/Element/CraftingPanelUI.cs`

**Interfaces:**
- Consumes: `CraftToken.ApplyVisual(ElementData, bool)` (Task 1에서 추가).
- Produces: `entryTemplate`, `tokenTemplate` (타입 `CraftToken`), `splashTemplate` (타입 `RectTransform`) 필드 — Task 3의 에디터 생성기가 이 필드들에 값을 대입한다.

- [ ] **Step 1: 필드 선언부 교체**

`private Canvas canvas;` 부터 `private TMP_Text toastText;`까지의 7줄을 다음으로 교체:

```csharp
    [Header("하이라키 참조 (에디터에서 \"하이라키 UI 생성/재생성\"으로 자동 채움)")]
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
```

**주의:** 이 시점엔 새 `bubbleSize` 필드를 추가하지 않는다. 기존에 `[Header("레이아웃")]` 블록 안에 있는 `[SerializeField] private float bubbleSize = 96f;`가 아직 그대로 남아있으므로(Task 3에서 삭제됨), Step 2에서는 그 필드에 그냥 값을 대입해서 재사용한다. 지금 새 필드를 추가하면 같은 이름이 중복 선언되어 컴파일 에러가 난다.

- [ ] **Step 2: Awake()에서 BuildUI 호출을 검증 로직으로 교체**

```csharp
    private void Awake()
    {
#if UNITY_EDITOR
        AutoFillData();
#endif
        ResolveFont();
        EnsureEventSystem();

        if (!ValidateHierarchyReferences())
        {
            enabled = false;
            return;
        }

        bubbleSize = ((RectTransform)tokenTemplate.transform).sizeDelta.x;

        foreach (ElementData data in CollectStartingElements())
        {
            if (Discover(data))
            {
                AddListEntry(data);
            }
        }

        isOpen = startOpened;
        openAmount = isOpen ? 1f : 0f;
        ApplySlide();
    }

    // 하이라키 UI가 미리 만들어져 있는지 확인한다. 비어 있으면 인스펙터에서
    // CraftingPanelUI 컴포넌트를 우클릭해 "하이라키 UI 생성/재생성"을 실행해야 한다.
    private bool ValidateHierarchyReferences()
    {
        if (canvas != null && panelRect != null && sidebarRect != null && listContent != null &&
            waterLayer != null && dragLayer != null && toastText != null &&
            entryTemplate != null && tokenTemplate != null && splashTemplate != null)
        {
            return true;
        }

        Debug.LogError("[CraftingPanelUI] 하이라키 참조가 비어 있습니다. 인스펙터에서 CraftingPanelUI 컴포넌트를 우클릭해 '하이라키 UI 생성/재생성'을 실행해주세요.", this);
        return false;
    }
```

- [ ] **Step 3: ApplySlide() 단순화**

`private void ApplySlide()` 안의 첫 줄을 교체:

```csharp
    private void ApplySlide()
    {
        float height = panelRect.rect.height;
        float eased = openAmount * openAmount * (3f - 2f * openAmount);   // smoothstep
        panelRect.anchoredPosition = new Vector2(0f, Mathf.Lerp(-height, 0f, eased));
        canvas.gameObject.SetActive(openAmount > 0f || isOpen || keepCanvasAlive);
    }
```

(기존에는 `panelRect.rect.height > 0f ? panelRect.rect.height : referenceResolution.y * panelHeightRatio` 였는데, 이제 panelRect는 항상 하이라키에 실존하는 유효한 RectTransform이므로 fallback이 필요 없다.)

- [ ] **Step 4: AddListEntry 교체**

```csharp
    private void AddListEntry(ElementData data)
    {
        CraftToken entry = Instantiate(entryTemplate, listContent);
        entry.name = $"Entry_{data.ElementName}";
        entry.gameObject.SetActive(true);
        entry.Init(this, data, true);
        entry.ApplyVisual(data, false);
    }
```

- [ ] **Step 5: CreateToken 교체**

```csharp
    private CraftToken CreateToken(RectTransform parent, ElementData data)
    {
        CraftToken token = Instantiate(tokenTemplate, parent);
        token.name = $"Token_{data.ElementName}";
        token.gameObject.SetActive(true);
        token.Init(this, data, false);
        token.ApplyVisual(data, true);

        FloatingElement bubble = token.GetComponent<FloatingElement>();
        bubble.Data = data;
        bubble.Rect = token.Rect;
        bubble.Radius = bubbleSize * 0.5f;
        bubble.Phase = Random.Range(0f, 20f);

        return token;
    }
```

- [ ] **Step 6: SplashRoutine 교체**

```csharp
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
```

- [ ] **Step 7: 컴파일 확인**

Run: `dotnet build Assembly-CSharp.csproj -v:q`
Expected: `오류 0개`. (`BuildUI`, `BuildSidebar`, `BuildMainArea`, `BuildHint`, `CreateRect`, `AddImage`, `CreateText`, `CreateVerticalScrollbar`, `ScrollbarWidth`와 `referenceResolution`/`panelHeightRatio`/`sidebarWidth`/`panelColor`/`waterColor`/구버전 `bubbleSize` 필드는 이 시점엔 아직 파일에 남아있고 아무도 안 쓰지만, 컴파일 에러는 아니다 — Task 3에서 정리한다.)

---

### Task 3: CraftingPanelUI — 에디터 생성기 추가 + 레거시 코드 정리

**Files:**
- Modify: `Assets/script/Element/CraftingPanelUI.cs`

**Interfaces:**
- Consumes: `CraftToken.EditorAssignViews(Image, TMP_Text)` (Task 1).
- Produces: `[ContextMenu("하이라키 UI 생성/재생성")] GenerateHierarchy()` — 사용자가 Task 4에서 에디터 인스펙터로 직접 호출한다.

- [ ] **Step 1: `[Header("레이아웃")]` 필드 블록 삭제**

다음 6줄을 통째로 삭제한다 (지금까지 `BuildUI` 계열에서만 쓰였고, 이제 그 메서드들이 로컬 상수를 쓰도록 바꾸므로 필드 자체가 불필요해짐):

```csharp
    [Header("레이아웃")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [SerializeField] private float panelHeightRatio = 0.35f;
    [SerializeField] private float sidebarWidth = 400f;
    [SerializeField] private float bubbleSize = 96f;
    [SerializeField] private Color panelColor = new Color(0.03f, 0.08f, 0.33f, 0.94f);
    [SerializeField] private Color waterColor = new Color(0.08f, 0.22f, 0.62f, 0.55f);
```

`bubbleSize`는 Task 2에서 `Awake()`가 이미 `bubbleSize = ((RectTransform)tokenTemplate.transform).sizeDelta.x;`로 값을 대입하는 대상이므로, 위 블록을 삭제하면 필드 자체가 없어져 컴파일 에러가 난다. 삭제하는 김에, `private bool isOpen;` 바로 위에 계산용 필드로 다시 선언한다:

```csharp
    private float bubbleSize;   // Awake에서 tokenTemplate 크기로 채움 (Inspector 필드 아님)
```

- [ ] **Step 2: `BuildUI`/`BuildSidebar`/`BuildMainArea`/`BuildHint`/`CreateVerticalScrollbar`/`CreateRect`/`AddImage`/`CreateText`/`ScrollbarWidth`를 파일 맨 아래 `#if UNITY_EDITOR` 블록으로 이동**

파일 맨 아래 `#if UNITY_EDITOR` ~ `AutoFillData()` ~ `#endif` 블록(현재 파일 끝부분)의 `AutoFillData()` 메서드 바로 위에, 기존 "UI 생성" 섹션에 있던 다음 9개 메서드 + 상수를 **그대로 잘라서 옮긴다**: `BuildUI`, `BuildSidebar`, `ScrollbarWidth` 상수, `CreateVerticalScrollbar`, `BuildMainArea`, `BuildHint`, `CreateRect`, `AddImage`, `CreateText`. 원래 있던 자리(런타임 섹션)에서는 삭제한다.

옮기면서 아래 3곳을 로컬 상수 참조로 바꾼다 (필드를 삭제했으므로):

`BuildUI()` 시작 부분에 로컬 변수 추가하고 그 변수를 쓰도록 수정:

```csharp
    private void BuildUI()
    {
        Vector2 referenceResolution = new Vector2(1920f, 1080f);
        float panelHeightRatio = 0.35f;
        Color panelColor = new Color(0.03f, 0.08f, 0.33f, 0.94f);

        GameObject canvasObject = new GameObject("CraftingCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        float ratio = Mathf.Clamp(panelHeightRatio, 0.2f, 1f);
        panelRect = CreateRect("Panel", canvasObject.transform, Vector2.zero, new Vector2(1f, ratio));
        AddImage(panelRect, panelColor, true);

        BuildSidebar();
        BuildMainArea();

        dragLayer = CreateRect("DragLayer", panelRect, Vector2.zero, Vector2.one);
        dragLayer.SetAsLastSibling();

        BuildHint();
    }
```

`BuildSidebar()` 시작 부분에 로컬 변수 추가:

```csharp
    private void BuildSidebar()
    {
        float sidebarWidth = 400f;

        sidebarRect = CreateRect("Sidebar", panelRect, new Vector2(0f, 0f), new Vector2(0f, 1f));
        // ... (이하 기존 BuildSidebar 본문 그대로, sidebarWidth 참조 부분만 위에서 선언한 로컬 변수를 그대로 씀)
```

(본문 나머지는 원본 그대로 유지 — `sidebarRect.sizeDelta = new Vector2(sidebarWidth, 0f);`, `float cellWidth = (sidebarWidth - 32f - ScrollbarWidth - 4f - 24f - 10f) * 0.5f;` 등 기존 줄들이 이제 이 로컬 변수를 그대로 참조한다.)

`BuildMainArea()` 시작 부분에 로컬 변수 추가:

```csharp
    private void BuildMainArea()
    {
        float sidebarWidth = 400f;
        Color waterColor = new Color(0.08f, 0.22f, 0.62f, 0.55f);

        RectTransform main = CreateRect("Main", panelRect, Vector2.zero, Vector2.one);
        main.offsetMin = new Vector2(sidebarWidth, 0f);
        main.offsetMax = Vector2.zero;

        waterLayer = CreateRect("Water", main, Vector2.zero, Vector2.one);
        AddImage(waterLayer, waterColor, true);
    }
```

- [ ] **Step 3: 템플릿 생성 메서드 3개 추가**

같은 `#if UNITY_EDITOR` 블록 안, `BuildHint()` 바로 아래에 추가:

```csharp
    private const float GeneratedBubbleDiameter = 96f;

    private void BuildTemplates()
    {
        BuildEntryTemplate();
        BuildTokenTemplate();
        BuildSplashTemplate();
    }

    private void BuildEntryTemplate()
    {
        GameObject entry = new GameObject("EntryTemplate", typeof(RectTransform), typeof(Image), typeof(CraftToken));
        entry.transform.SetParent(listContent, false);

        Image background = entry.GetComponent<Image>();
        background.color = new Color(1f, 1f, 1f, 0.1f);

        RectTransform iconRect = CreateRect("Icon", (RectTransform)entry.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.sizeDelta = new Vector2(40f, 40f);
        iconRect.anchoredPosition = new Vector2(10f, 0f);
        Image icon = AddImage(iconRect, Color.white, false);
        icon.sprite = ElementVisual.Circle;

        RectTransform labelRect = CreateRect("Label", (RectTransform)entry.transform, Vector2.zero, Vector2.one);
        labelRect.offsetMin = new Vector2(58f, 0f);
        labelRect.offsetMax = new Vector2(-8f, 0f);
        TMP_Text label = CreateText(labelRect, "원소 이름", 24f, TextAlignmentOptions.Left);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;

        CraftToken token = entry.GetComponent<CraftToken>();
        token.EditorAssignViews(icon, label);
        entry.SetActive(false);

        entryTemplate = token;
    }

    private void BuildTokenTemplate()
    {
        GameObject tokenObject = new GameObject("TokenTemplate", typeof(RectTransform), typeof(CraftToken), typeof(FloatingElement));
        RectTransform rect = (RectTransform)tokenObject.transform;
        rect.SetParent(dragLayer, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(GeneratedBubbleDiameter, GeneratedBubbleDiameter);

        RectTransform iconRect = CreateRect("Icon", rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        iconRect.sizeDelta = new Vector2(GeneratedBubbleDiameter * 0.92f, GeneratedBubbleDiameter * 0.92f);
        iconRect.anchoredPosition = Vector2.zero;
        Image icon = AddImage(iconRect, Color.white, true);
        icon.sprite = ElementVisual.Circle;

        RectTransform labelRect = CreateRect("Label", rect, Vector2.zero, Vector2.one);
        TMP_Text label = CreateText(labelRect, "원소 이름", 22f, TextAlignmentOptions.Center);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;

        CraftToken token = tokenObject.GetComponent<CraftToken>();
        token.EditorAssignViews(icon, label);
        tokenObject.SetActive(false);

        tokenTemplate = token;
    }

    private void BuildSplashTemplate()
    {
        RectTransform ring = CreateRect("SplashTemplate", waterLayer, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        ring.sizeDelta = new Vector2(GeneratedBubbleDiameter, GeneratedBubbleDiameter);
        Image image = AddImage(ring, new Color(1f, 1f, 1f, 0.8f), false);
        image.sprite = ElementVisual.Ring;
        ring.gameObject.SetActive(false);

        splashTemplate = ring;
    }
```

- [ ] **Step 4: `GenerateHierarchy()` 진입점 추가**

`AutoFillData()` 메서드 바로 위에 추가:

```csharp
    [ContextMenu("하이라키 UI 생성/재생성")]
    private void GenerateHierarchy()
    {
        Transform existing = transform.Find("CraftingCanvas");
        if (existing != null)
        {
            DestroyImmediate(existing.gameObject);
        }

        ResolveFont();
        BuildUI();
        BuildTemplates();

        EditorUtility.SetDirty(this);
        Debug.Log("[CraftingPanelUI] 하이라키 UI를 새로 만들었습니다. Ctrl+S로 프리팹/씬을 저장해주세요.", this);
    }

```

- [ ] **Step 5: 컴파일 확인**

Run: `dotnet build Assembly-CSharp.csproj -v:q`
Expected: `오류 0개`. 특히 `BuildSidebar`/`BuildMainArea` 안에서 `sidebarWidth`(로컬 변수)를 참조하는 기존 줄들이 새로 선언한 로컬 변수와 이름이 같아서 자연스럽게 이어지는지 확인 — 컴파일이 통과하면 확인된 것이다.

- [ ] **Step 6: 파일 전체를 한 번 더 눈으로 검토**

`Assets/script/Element/CraftingPanelUI.cs`를 Read로 열어서, 런타임 섹션(Awake 위~SplashRoutine 근처)에 `BuildUI`/`BuildSidebar`/`BuildMainArea`/`BuildHint`/`CreateRect`/`AddImage`/`CreateText`/`CreateVerticalScrollbar`/`ScrollbarWidth`가 **중복으로 남아있지 않은지**(Step 2에서 자르고 옮겼는지) 확인한다. 중복 정의가 있으면 이 시점에 컴파일 에러(같은 멤버 두 번 선언)로 이미 드러났을 것이므로, Step 5가 통과했다면 사실상 자동으로 검증된다.

---

### Task 4: 하이라키 생성 + Play 모드 동작 확인 (사용자 직접 수행)

**이 작업은 Unity 에디터 GUI 조작이 필요해서 코딩 에이전트가 대신할 수 없다. 사용자가 직접 진행한다.**

**Files:** 없음 (Unity 에디터 작업 + `Assets/Prefab/UI/CraftingPanel.prefab` 저장)

- [ ] **Step 1: 프리팹 열기**

Unity 에디터에서 `Assets/Prefab/UI/CraftingPanel.prefab`을 더블클릭해 Prefab 모드로 연다.

- [ ] **Step 2: 하이라키 생성**

하이라키에서 `CraftingPanel` 오브젝트를 선택하고, 인스펙터의 `CraftingPanelUI` 컴포넌트 제목을 우클릭(또는 톱니바퀴 아이콘 클릭) → **"하이라키 UI 생성/재생성"** 실행. Console에 `하이라키 UI를 새로 만들었습니다` 로그가 뜨고, 하이라키에 `CraftingCanvas` 밑으로 `Panel > Sidebar/Main/Hint/Toast/DragLayer` 구조가 생기는지 확인.

- [ ] **Step 3: 콘솔 에러 확인**

Console 창에 빨간 에러가 없는지 확인 (특히 `ValidateHierarchyReferences` 관련 에러가 없어야 함).

- [ ] **Step 4: 저장**

Ctrl+S로 프리팹 저장. Prefab 모드에서 나온다.

- [ ] **Step 5: 씬에 배치된 인스턴스 갱신 확인**

`Assets/Scenes/StageScene 1.unity` 등 `CraftingPanel` 프리팹을 쓰는 씬을 열어서, 하이라키의 `CraftingPanel` 인스턴스가 프리팹의 새 구조를 정상적으로 반영하는지 확인 (프리팹 연결이 끊어져 있다면 "Overrides" 없이 그대로 반영되어야 정상).

- [ ] **Step 6: Play 모드 동작 확인**

Play 버튼을 누르고 다음을 확인:
- Tab 키로 조합창이 열리고 닫히는지
- 왼쪽 목록에서 원소를 드래그하면 물 위에 복사본이 떨어지는지
- 물 위에서 원소 두 개를 겹치면 조합되는지 (또는 "조합할 수 없어요" 토스트가 뜨는지)
- 원소를 조합창 밖으로 드래그하면 맵 배치 모드로 들어가는지
- 목록 스크롤이 잘 되는지

- [ ] **Step 7: 이미지 교체로 커스터마이징 확인**

하이라키에서 `Panel`, `Sidebar`, `Main/Water` 오브젝트를 각각 선택해 Image 컴포넌트의 Source Image에 원하는 스프라이트를 끼워보고 바로 반영되는지 확인. `EntryTemplate`/`TokenTemplate`의 `Icon`/`Label` 크기나 위치를 조절해보고 다음 Play 모드에서 반영되는지 확인.

- [ ] **Step 8: 문제가 있으면 재생성으로 리셋**

배치가 심하게 망가졌다면 Step 2의 "하이라키 UI 생성/재생성"을 다시 실행해 기본 모양으로 되돌릴 수 있다 (기존 `CraftingCanvas`를 지우고 새로 만듦).

---

## Self-Review 결과

- **스펙 커버리지:** 설계 문서의 "하이라키 구조", "CraftToken 변경", "CraftingPanelUI 변경"(제거 필드/유지 필드/새 참조 필드/Awake/AddListEntry/CreateToken/SplashRoutine/제거되는 헬퍼), "에디터 생성기", "검증 계획" 항목 모두 Task 1~4에 대응됨. "호환성/마이그레이션" 항목은 Task 4 Step 2~5(재생성 + 씬 확인)로 커버됨.
- **플레이스홀더 스캔:** 모든 코드 블록이 실제 코드이며 "TODO"/"적절히 처리" 같은 표현 없음.
- **타입/이름 일관성:** `entryTemplate`/`tokenTemplate`(타입 `CraftToken`), `splashTemplate`(타입 `RectTransform`), `bubbleSize`(private float, Inspector 미노출) 이름과 타입이 Task 2·3에서 동일하게 쓰임. `ApplyVisual(data, flipLabelForBrightIcon)` 시그니처가 Task 1 선언과 Task 2 호출부에서 일치. `EditorAssignViews(iconRef, labelRef)`가 Task 1 선언과 Task 3 호출부에서 일치.
