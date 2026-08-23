# Crafting Panel 하이라키 UI 전환 설계

## 배경 / 문제

`Assets/script/Element/CraftingPanelUI.cs`의 `BuildUI()`가 조합창 UI(패널, 사이드바, 물 영역, 헤더, 힌트, 토스트, 스크롤바, 목록 항목, 원소 토큰, 물결 효과)를 전부 런타임에 코드로 새로 생성한다. 그 결과:

- 에디터의 하이라키에는 `CraftingPanelUI` 컴포넌트가 붙은 GameObject 하나만 보이고, 실제 비주얼은 Play 모드에서만 생겨났다가 Play를 멈추면 사라진다.
- 배경 이미지 교체, 위치/크기 조정, 아이콘·글자 배치 변경 등을 에디터에서 직접 할 수 없다.

## 목표

정적인 틀(패널/사이드바/물 영역/헤더/힌트/토스트/스크롤바)과, 반복 생성되는 요소(목록 항목, 원소 토큰, 물결 효과)의 템플릿을 실제 하이라키 GameObject로 `Assets/Prefab/UI/CraftingPanel.prefab` 안에 저장해서, 에디터에서 직접 이미지 교체·배치 조정·템플릿 편집이 가능하게 한다. 망가졌을 때 기본 모양으로 되돌리는 에디터 전용 리셋 기능도 제공한다.

## 범위

- 포함: `CraftingPanelUI.cs` 리팩터링(런타임 생성 → 에디터 생성 + 런타임 참조), `CraftToken.cs`에 아이콘/라벨 뷰 참조 추가, `CraftingPanel.prefab`에 하이라키 구조 추가(에디터에서 생성 버튼 실행 결과로 채워짐).
- 제외: 물리 시뮬레이션(부력/저항/출렁임), 드래그·조합·슬라이드 열고닫기 로직의 동작 변경. 이 기능들의 기존 동작은 그대로 유지한다. `ElementVisual.cs`, `FloatingElement.cs`, `PlacementSystem.cs`는 변경하지 않는다.

## 하이라키 구조

`CraftingPanel` 프리팹 아래, 에디터 생성 버튼이 만들어내는 구조:

```
CraftingPanel (CraftingPanelUI)
└─ CraftingCanvas (Canvas, CanvasScaler, GraphicRaycaster)
   └─ Panel (RectTransform, Image)
      ├─ Sidebar (RectTransform, Image)
      │  ├─ Header (RectTransform, TMP_Text: "원소 목록")
      │  └─ Viewport (RectTransform, Image, RectMask2D, ScrollRect)
      │     └─ Content (RectTransform, GridLayoutGroup, ContentSizeFitter)
      │        └─ EntryTemplate (비활성, RectTransform, Image, CraftToken)
      │           ├─ Icon (RectTransform, Image)
      │           └─ Label (RectTransform, TMP_Text)
      │  └─ Scrollbar (RectTransform, Image, Scrollbar)
      │     └─ Sliding Area
      │        └─ Handle (RectTransform, Image)
      ├─ Main (RectTransform)
      │  └─ Water (RectTransform, Image)
      │     └─ SplashTemplate (비활성, RectTransform, Image)
      ├─ Hint (RectTransform, TMP_Text)
      ├─ Toast (RectTransform, TMP_Text)
      └─ DragLayer (RectTransform)
         └─ TokenTemplate (비활성, RectTransform, Image, CraftToken, FloatingElement)
            ├─ Icon (RectTransform, Image)
            └─ Label (RectTransform, TMP_Text)
```

`EntryTemplate`, `TokenTemplate`, `SplashTemplate`은 `SetActive(false)` 상태로 프리팹에 저장되며, 런타임에는 절대 활성화되지 않고 `Instantiate()`의 원본으로만 쓰인다.

## CraftToken 변경

- `[SerializeField] private Image icon;`, `[SerializeField] private TMP_Text label;` 필드 추가.
- `public void ApplyVisual(ElementData data)` 메서드 추가: 아이콘 스프라이트/색, 라벨 텍스트, (아이콘 없을 때) 밝기에 따른 라벨 색 반전 로직을 여기로 옮긴다. `CraftingPanelUI.AddListEntry`/`CreateToken`이 Instantiate 이후 이 메서드를 호출.
- 기존 `Init(panel, data, spawnCopy)`는 그대로 유지(드래그 동작용 데이터). `Rect`도 그대로.

## CraftingPanelUI 변경

### 제거되는 필드

레이아웃을 하이라키가 직접 표현하므로 아래 필드는 제거한다:

- `referenceResolution` (Canvas의 CanvasScaler가 직접 들고 있음)
- `panelHeightRatio` (Panel의 anchor로 표현됨; `ApplySlide()`는 `panelRect.rect.height`만 사용하도록 단순화)
- `sidebarWidth` (Sidebar RectTransform의 `sizeDelta`로 표현됨)
- `panelColor`, `waterColor` (Panel/Water의 Image.color로 표현됨)
- `bubbleSize` (Inspector 노출 필드가 아니라, `private float bubbleSize`로 남기고 `Awake()`에서 `((RectTransform)tokenTemplate.transform).sizeDelta.x`로 한 번 계산해 둔다. 이후 `bubbleSize * 0.5f`, `bubbleSize * 1.5f`, `bubbleSize * 0.9f` 등 기존 계산식은 전부 그대로 둔다 — 값의 출처만 바뀐다)

### 유지되는 필드

물 시뮬레이션 튜닝(`buoyancySpring`, `waterDrag`, `bobAmplitude`, `bobSpeed`, `swayStrength`, `splashSpeed`), 열고닫기(`slideDuration`, `startOpened`, `lockAfterStageStart`), 데이터(`elementData`, `startingElements`, `combinations`, `font`)는 그대로 유지.

### 새로 추가되는 하이라키 참조 필드

`[Header("하이라키 참조")]`로 묶어서 추가 (모두 `[SerializeField] private`, 에디터 생성 버튼이 자동으로 채움):

- `Canvas canvas` — 기존에는 코드로 찾았지만 이제 직접 참조
- `RectTransform panelRect`
- `RectTransform sidebarRect`
- `RectTransform listContent`
- `RectTransform waterLayer`
- `RectTransform dragLayer`
- `TMP_Text toastText`
- `CraftToken entryTemplate`
- `CraftToken tokenTemplate`
- `RectTransform splashTemplate` (Image는 `GetComponent<Image>()`로 즉시 꺼내 씀)

### Awake() 흐름 변경

1. `EnsureEventSystem()` 그대로 유지.
2. `BuildUI()` 호출 제거. 대신 `ValidateHierarchyReferences()` 추가: 위 참조 필드 중 하나라도 null이면 `Debug.LogError`로 "인스펙터에서 CraftingPanelUI 컴포넌트를 우클릭해 '하이라키 UI 생성/재생성'을 실행해주세요" 안내 후 `enabled = false`로 조합창 비활성화(에러 방지).
3. `bubbleSize = ((RectTransform)tokenTemplate.transform).sizeDelta.x;`로 계산해 `private float bubbleSize` 필드에 저장 (기존에 Inspector 필드였던 것을 대체하는 계산값).
4. 나머지(시작 원소 등록, `isOpen`/`openAmount` 초기화, `ApplySlide()`)는 그대로.

### AddListEntry(data) 변경

```
CraftToken entry = Instantiate(entryTemplate, listContent);
entry.gameObject.SetActive(true);
entry.Init(this, data, true);
entry.ApplyVisual(data);
```
(기존처럼 `entry.name`은 `$"Entry_{data.ElementName}"`로 바꿔줘서 디버깅 시 구분되게 한다.)

### CreateToken(parent, data) 변경

```
CraftToken token = Instantiate(tokenTemplate, parent);
token.gameObject.SetActive(true);
token.Init(this, data, false);
token.ApplyVisual(data);

FloatingElement bubble = token.GetComponent<FloatingElement>();
bubble.Data = data;
bubble.Rect = token.Rect;
bubble.Radius = bubbleSize * 0.5f;
bubble.Phase = Random.Range(0f, 20f);
```

`SpawnCraftToken`, `OnTokenDragBegin`, `MoveTokenToPointer`, `OnTokenDropped`, `DropIntoWater`, `TryCombine`, `FindOverlapping`, `RemoveFloating` 등 나머지 드래그/조합/물리 로직은 **변경하지 않는다** — 모두 `CraftToken`/`FloatingElement`의 `Rect`를 통해 동작하므로 토큰이 어떻게 생성됐는지와 무관하다.

### SplashRoutine 변경

```
RectTransform ring = Instantiate(splashTemplate, waterLayer);
ring.gameObject.SetActive(true);
Image image = ring.GetComponent<Image>();
```
이후 스케일/알파를 조절하는 애니메이션 루프는 그대로. 끝나면 `Destroy(ring.gameObject)`.

### 제거되는 헬퍼

런타임에서 더 이상 쓰이지 않는 `CreateRect`, `AddImage`, `CreateText`, `CreateVerticalScrollbar`, `BuildUI`, `BuildSidebar`, `BuildMainArea`, `BuildHint`는 **에디터 전용 생성기**로 이동한다 (아래 섹션).

## 에디터 생성기 (`#if UNITY_EDITOR`)

기존 `[ContextMenu("원소 / 조합 데이터 자동 채우기")] AutoFillData()`와 같은 방식으로 새 메서드 추가:

```
[ContextMenu("하이라키 UI 생성/재생성")]
private void GenerateHierarchy()
```

동작:

1. 기존 `CraftingCanvas` 자식이 있으면 `DestroyImmediate`로 제거.
2. 지금 `BuildUI()`/`BuildSidebar()`/`BuildMainArea()`/`BuildHint()`가 만들던 것과 동일한 모양으로 하이라키를 새로 생성 (현재 코드의 배치 수치를 그대로 에디터 생성 코드로 옮김 — 시각적 회귀 없음).
3. `EntryTemplate`(Content 하위, 비활성), `TokenTemplate`(DragLayer 하위, 비활성), `SplashTemplate`(Water 하위, 비활성)을 지금 `AddListEntry`/`CreateToken`/`SplashRoutine`이 만들던 모양 그대로 생성.
4. 위에서 나열한 모든 `[SerializeField]` 참조 필드에 생성된 객체를 대입.
5. `EditorUtility.SetDirty(this)` 호출 (프리팹 모드에서 실행 시 변경사항이 저장 대상으로 표시됨).
6. 실행 후 사용자가 Ctrl+S로 프리팹을 저장해야 영구 반영됨 — 이 사실을 `Debug.Log`로 안내.

이 메서드는 반복 실행 가능(멱등)해야 하며, 실행할 때마다 항상 1번 단계에서 이전 결과를 지우고 새로 만들어서 "기본값으로 리셋" 역할을 겸한다.

## 호환성 / 마이그레이션

기존 씬(`StageScene 1/3/5`, `StageScene 5_dst`)에 배치된 `CraftingPanel` 프리팹 인스턴스는 새 코드가 배포된 뒤 한 번은 반드시 에디터에서 "하이라키 UI 생성/재생성"을 실행해줘야 한다 (참조 필드가 비어있으면 `Awake()`에서 비활성화되고 에러 로그가 뜨므로 누락 시 바로 알 수 있음).

## 검증 계획

- 코드 리뷰로 기존 동작(드래그/조합/스크롤/슬라이드/물리)과의 동치성을 꼼꼼히 대조한다 (Unity 헤드리스 실행 환경이 없어 자동 테스트는 불가능).
- 사용자가 에디터에서 생성 버튼을 실행하고 Play 모드에서 다음을 직접 확인: 목록에서 원소 드래그, 물에서 조합, 배치를 위해 밖으로 드래그, 조합창 열고 닫기, 스크롤, Panel/Sidebar/Water에 새 이미지 끼워보기.

## 미해결 질문 없음

이 설계는 사용자와의 대화에서 확정된 3가지 결정(배경 이미지+배치 전체 편집, 템플릿도 하이라키 편집, 리셋 버튼 필요)을 모두 반영했다.
