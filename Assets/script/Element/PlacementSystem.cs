using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// 조합창에서 끌고 나온 원소를 도로 격자 위에 배치한다.
//
// - 도로 격자 안이면 어느 칸에나 놓을 수 있다.
// - 회전 조작은 없다. 원하는 칸에 마우스를 놓기만 하면 된다.
// - 그 칸에 장애물이 있으면 장애물에 속성이 붙고, 비어 있으면 도로 위에 놓인다.
// - 이미 놓은 것은 다시 클릭하면 조합창으로 회수된다.
public class PlacementSystem : MonoBehaviour
{
    [SerializeField] private PlacementGrid grid;
    [SerializeField] private Transform player;
    [SerializeField] private CameraViewController cameraView;
    [SerializeField] private Camera worldCamera;

    [SerializeField] private Color validColor = new Color(0.35f, 1f, 0.5f, 1f);
    [SerializeField] private Color hoverColor = new Color(1f, 0.95f, 0.4f, 1f);
    [Tooltip("이 칸의 함정을 끌 수 있을 때 쓰는 색")]
    [SerializeField] private Color counterColor = new Color(0.3f, 0.85f, 1f, 1f);

    [Header("흙 벽")]
    [Tooltip("이 원소를 놓으면 자동차를 막는 벽이 세워진다. 비워두면 아래 원소 종류로 찾는다")]
    [SerializeField] private ElementData wallElement;
    [Tooltip("wallElement가 비어 있을 때 이 종류의 원소를 벽으로 쓴다 (흙 = Dark)")]
    [SerializeField] private ElementType wallElementType = ElementType.Earth;
    [Tooltip("비워두면 회색 박스로 자동 생성한다")]
    [SerializeField] private GameObject wallPrefab;
    [SerializeField] private float wallHeight = 2f;
    [SerializeField] private Color wallColor = new Color(0.55f, 0.55f, 0.58f, 1f);

    [Header("자동차 파훼 설치물")]
    [Tooltip("바람 원소로 만드는 돌풍에 쓸 연출 프리팹(EzTornado 등). " +
             "비워두면 소용돌이 파티클을 직접 만들어 쓴다")]
    [SerializeField] private GameObject windPrefab;

    [Header("회수")]
    [Tooltip("맵에 놓은 원소를 오른쪽 클릭하면 조합창으로 되돌린다")]
    [SerializeField] private bool allowRetrieve = true;
    [Tooltip("회수할 때 울릴 소리")]
    [SerializeField] private AudioClip retrieveSound;
    [Range(0f, 1f)]
    [SerializeField] private float retrieveVolume = 1f;

    [Header("함정 상쇄")]
    [Tooltip("칸 위 어느 높이까지 함정을 찾을지")]
    [SerializeField] private float trapSearchHeight = 4f;
    [Tooltip("함정이 꺼질 때 올려 둔 원소가 사라지는 데 걸리는 시간(초)")]
    [SerializeField] private float counterMarkLifetime = 0.6f;

    [Header("엉뚱한 곳에 놓은 바람")]
    [Tooltip("모래바람 상쇄 칸이 아닌 곳에 바람을 놓으면 폭풍이 플레이어를 쫓아온다")]
    [SerializeField] private bool spawnRogueWind = true;
    [Tooltip("이 종류의 원소가 그렇게 동작한다")]
    [SerializeField] private ElementType rogueWindElement = ElementType.Storm;
    [Tooltip("색을 베낄 모래바람. 비워두면 씬에서 찾는다")]
    [SerializeField] private Sandstorm rogueWindColorSource;

    [Header("모습을 감추는 원소")]
    [Tooltip("맵에 아무것도 세우지 않는 원소들. 놓는 것 자체는 안 보이고 연출만 남는다\n" +
             "(나무다리 = 하늘에서 떨어지는 다리, 쓰나미 = 몰려오는 파도)")]
    [SerializeField]
    private ElementType[] invisibleElements =
    {
        ElementType.Woodbridge,
        ElementType.Tsunami,
    };

    [Header("쓰나미")]
    [Tooltip("이 원소는 함정 칸이든 맨땅이든 놓기만 하면 파도가 몰려온다")]
    [SerializeField] private ElementType tsunamiElement = ElementType.Tsunami;
    [Tooltip("놓고 나서 파도가 밀려오기까지 기다리는 시간(초)")]
    [SerializeField] private float tsunamiDelay = 1f;
    [Tooltip("파도 연출. 비워 두면 씬에서 찾고, 없으면 기본값으로 만들어 쓴다")]
    [SerializeField] private TsunamiWave tsunamiWave;

    [Header("맨땅에 부은 용암")]
    [Tooltip("물 함정이 아닌 곳에 용암을 놓으면 사방으로 흘러 퍼지고, 밟으면 타 죽는다")]
    [SerializeField] private bool spillLava = true;
    [Tooltip("이 종류의 원소가 그렇게 동작한다")]
    [SerializeField] private ElementType lavaElement = ElementType.Lava;

    [Header("놓을 자리 표시")]
    [Tooltip("놓으려는 칸을 지금 든 원소의 색으로 물들인다. 끄면 아래 기본 색만 쓴다")]
    [SerializeField] private bool tintHighlightWithElement = true;
    [Tooltip("얼마나 옅게 할지. 0이면 색을 그대로 쓰고, 1에 가까울수록 흰색에 가까워진다")]
    [Range(0f, 1f)]
    [SerializeField] private float highlightPaleness = 0.15f;
    [Tooltip("칸 안쪽 색의 진하기. 바닥이 살짝 비칠 정도만 남긴다")]
    [Range(0.05f, 1f)]
    [SerializeField] private float highlightAlpha = 0.6f;
    [Tooltip("칸 테두리의 진하기. 안쪽보다 진해야 칸 경계가 또렷하게 보인다")]
    [Range(0.05f, 1f)]
    [SerializeField] private float highlightBorderAlpha = 1f;
    [Tooltip("칸 테두리의 두께. 칸 크기에 대한 비율이다")]
    [Range(0.02f, 0.3f)]
    [SerializeField] private float highlightBorderThickness = 0.1f;

    [Header("지형 높이")]
    [Tooltip("놓을 자리의 지형 높이를 잴 때, 격자 평면에서 이만큼 위에서 아래로 광선을 쏜다(m).\n" +
             "건물 옥상처럼 높은 곳에도 놓으려면 그 높이보다 커야 한다")]
    [SerializeField] private float surfaceProbeUp = 60f;
    [Tooltip("격자 평면보다 아래로 이만큼까지 지형을 찾는다(m)")]
    [SerializeField] private float surfaceProbeDown = 20f;

    // 함정 뭉치에 속한 것이 길바닥에서 이만큼 넘게 떠 있으면 지형으로 세지 않는다(m).
    // 하늘에 매달린 나무다리(20m)는 걸러 내고, 함정 자신(물·불)은 그대로 남는 높이다.
    private const float TrapSkyHeight = 3f;

    [Header("아이콘 크기")]
    [Tooltip("맵에 놓인 원소 아이콘의 지름을 칸 크기에 대한 비율로 정한다 (모든 원소 공통). 1에 가까울수록 칸을 꽉 채운다")]
    [Range(0.1f, 1f)]
    [SerializeField] private float placedIconDiameterRatio = 0.85f;

    private GameObject highlight;

    private ElementData current;
    private Vector2Int hoverCell;
    private bool hoverValid;

    // 놓을 자리 표시에 쓰는 재질. 색만 매 프레임 바꿔 쓴다.
    // 테두리와 안쪽을 따로 두어야 "어느 칸인지"가 또렷하게 보인다.
    private Material highlightFillMaterial;
    private Material highlightBorderMaterial;

    private CraftingPanelUI craftingPanel;

    public bool IsPlacing => current != null;

    public void Setup(PlacementGrid placementGrid, Transform playerTransform, CameraViewController view, Camera camera)
    {
        grid = placementGrid;
        player = playerTransform;
        cameraView = view;
        worldCamera = camera;
    }

    private void OnEnable()
    {
        StageReset.Requested += HandleStageReset;
    }

    private void OnDisable()
    {
        StageReset.Requested -= HandleStageReset;
    }

    // 죽어서 다시 시작할 때는 맵을 처음 상태로 되돌린다.
    // 놓아 둔 원소·흙 벽은 전부 치우고 조합창으로 돌려준다.
    private void HandleStageReset(StageReset.Reason reason)
    {
        if (reason != StageReset.Reason.PlayerDeath)
        {
            return;
        }

        if (IsPlacing)
        {
            Cancel();
        }

        foreach (PlacedElement placed in PlacedElement.All)
        {
            if (placed == null)
            {
                continue;
            }

            // 이미 효과가 일어난 것(함정을 실제로 끈 원소)은 함정 쪽에서 돌려주므로 여기서는 건너뛴다.
            // 그러지 않으면 같은 원소가 두 번 조합창에 생긴다.
            ElementData returned = placed.CanRetrieve() ? placed.Data : null;
            placed.Remove();
            ReturnToPanel(returned);
        }
    }

    private void Update()
    {
        if (!allowRetrieve || IsPlacing)
        {
            return;
        }

        if (!ReadRetrieveClick(out Vector2 screenPosition))
        {
            return;
        }

        // 조합창 등 UI 위를 클릭한 것은 회수가 아니다.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        TryRetrieveAt(screenPosition);
    }

    // 회수는 오른쪽 클릭. 왼쪽은 배치와 맵 이동(드래그)에 쓰인다.
    private static bool ReadRetrieveClick(out Vector2 screenPosition)
    {
        screenPosition = default;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current == null || !Mouse.current.rightButton.wasPressedThisFrame)
        {
            return false;
        }

        screenPosition = Mouse.current.position.ReadValue();
        return true;
#else
        if (!Input.GetMouseButtonDown(1))
        {
            return false;
        }

        screenPosition = Input.mousePosition;
        return true;
#endif
    }

    // 클릭한 곳에 배치해 둔 원소가 있으면 조합창으로 되돌린다.
    private bool TryRetrieveAt(Vector2 screenPosition)
    {
        if (worldCamera == null)
        {
            return false;
        }

        Ray ray = worldCamera.ScreenPointToRay(screenPosition);

        // 함정처럼 트리거로 된 것이 앞을 가릴 수 있으므로 전부 받아서 가까운 것부터 본다.
        RaycastHit[] hits = Physics.RaycastAll(ray, 500f, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            PlacedElement placed = hit.collider.GetComponentInParent<PlacedElement>();
            if (placed == null)
            {
                continue;
            }

            if (!placed.CanRetrieve())
            {
                return false;
            }

            ElementData returned = placed.Retrieve();
            if (returned == null)
            {
                return false;
            }

            SfxPlayer.PlayAt(retrieveSound, hit.point, retrieveVolume);
            ReturnToPanel(returned);
            return true;
        }

        return false;
    }

    // 빈 칸에 이미 놓인 원소/벽이 있으면 조합창으로 돌려주고 자리를 비운다.
    //
    // 다만 같은 원소를 더 올려 개수를 채우는 중이면(진흙 2개짜리 함정처럼) 비우지 않는다.
    // 비워 버리면 아무리 부어도 늘 1개인 채로 남는다.
    private void ReplaceExistingAt(Vector2Int cell, ElementData data)
    {
        foreach (PlacedElement existing in PlacedElement.FindAllAt(cell))
        {
            if (existing == null)
            {
                continue;
            }

            if (existing.AcceptsStackOf(data))
            {
                return;
            }

            ReturnToPanel(existing.Retrieve());
        }
    }

    private void ReturnToPanel(ElementData data)
    {
        if (data == null)
        {
            return;
        }

        if (craftingPanel == null)
        {
            craftingPanel = FindFirstObjectByType<CraftingPanelUI>();
        }

        if (craftingPanel == null)
        {
            Debug.LogWarning($"{name}: 조합창을 찾지 못해 '{data.ElementName}'을 돌려주지 못했습니다.", this);
            return;
        }

        craftingPanel.ReturnElement(data);
    }

    // 흙 원소는 씬마다 따로 물려 주지 않아도 되도록 종류로도 찾는다.
    private ElementData WallElement
    {
        get
        {
            if (wallElement != null)
            {
                return wallElement;
            }

            if (craftingPanel == null)
            {
                craftingPanel = FindFirstObjectByType<CraftingPanelUI>();
            }

            wallElement = craftingPanel != null ? craftingPanel.FindElementOfType(wallElementType) : null;
            return wallElement;
        }
    }

    // 조합창이 원소를 끌어내면 호출한다.
    public void Begin(ElementData data)
    {
        if (data == null || grid == null)
        {
            return;
        }

        current = data;
        BuildHighlight();

        if (cameraView != null)
        {
            cameraView.SetTopView(true);
        }
    }

    public void UpdatePreview(Vector2 screenPosition)
    {
        if (!IsPlacing)
        {
            return;
        }

        hoverValid = TryGetCell(screenPosition, out hoverCell);

        if (highlight != null)
        {
            highlight.SetActive(hoverValid);

            if (hoverValid)
            {
                highlight.transform.position = CellSurface(hoverCell) + Vector3.up * 0.06f;

                // 여기에 놓으면 무슨 일이 일어나는지 색으로 미리 알려 준다.
                Color tint = ResolveHighlightColor(current, hoverCell);

                Color fill = tint;
                fill.a = highlightAlpha;
                WorldVisual.SetMaterialColor(HighlightFillMaterial, fill);

                // 테두리는 옅게 만들지 않고 원래 색 그대로 진하게 둘러 준다.
                Color border = tint;
                border.a = highlightBorderAlpha;
                WorldVisual.SetMaterialColor(HighlightBorderMaterial, border);
            }
        }
    }

    // 놓았을 때 호출. 배치에 성공하면 true.
    public bool Confirm(Vector2 screenPosition)
    {
        if (!IsPlacing)
        {
            return false;
        }

        UpdatePreview(screenPosition);

        ElementData data = current;
        bool placed = false;

        if (hoverValid)
        {
            // 한 칸에는 하나만 놓인다. 이미 있던 것은 먼저 조합창으로 돌려준다.
            //
            // 함정 칸도 마찬가지다. 함정에 올려 둔 원소를 물려야 함정이 다시 "상쇄할 수 있는" 상태가 되고,
            // 그래야 아래 FindCounterableTrapAt이 함정을 찾아 새 원소로 바꿔 올릴 수 있다.
            // (예전에는 여기를 그냥 지나쳐서, 함정 칸에만 원소가 계속 쌓였다)
            ReplaceExistingAt(hoverCell, data);

            // 쓰나미는 어느 칸에 놓든 무조건 밀려온다.
            //
            // 아래 갈래(함정 상쇄 / 장애물 속성 / 흙 벽 / 자동차 파훼 / 그냥 놓기) 중
            // 어디로 빠지든 상관없도록 갈래를 나누기 전에 부른다.
            // 갈래 안에 두면 그 칸이 무엇이냐에 따라 파도가 오기도 하고 안 오기도 한다.
            if (data.ElementType == tsunamiElement)
            {
                SurgeTsunamiAt(CellSurface(hoverCell), data);
            }

            IElementCounterTrap trap = FindCounterableTrapAt(hoverCell, data);
            Obstacle obstacle = Obstacle.FindAt(hoverCell);

            if (trap != null && trap.TryCounter(data))
            {
                // 함정 위에 원소를 올려 둔다. 실제로 꺼지는 순간 이 표시도 함께 사라진다.
                CreateCounterMark(data, hoverCell, trap);
            }
            else if (obstacle != null)
            {
                // 장애물이 있는 칸이면 그 장애물에 속성을 배치한다.
                // 이미 다른 속성이 붙어 있었다면 그 원소는 조합창으로 돌려준다.
                obstacle.Apply(data);

                PlacedElement marker = obstacle.GetComponent<PlacedElement>();
                if (marker != null)
                {
                    ReturnToPanel(marker.Replace(data));
                }
                else
                {
                    PlacedElement.Attach(obstacle.gameObject, data, PlacedElement.Kind.ObstacleAttribute)
                        .WithObstacle(obstacle);
                }
            }
            else if (WallElement != null && data == WallElement)
            {
                // 돌 원소는 그 칸에 자동차를 막는 벽을 세운다.
                CreateStoneWall(hoverCell);
            }
            else if (CarCounterBuilder.Handles(data))
            {
                // 흑요석·바람·늪은 그 칸에 자동차를 파훼하는 설치물을 세운다.
                CreateCarCounter(data, hoverCell);
            }
            else
            {
                GameObject placedObject = CreatePlacedElement(data, hoverCell);

                // 폭풍을 모래바람 상쇄 칸이 아닌 엉뚱한 곳에 놓았다.
                // 갈 곳 없는 흰 폭풍이 그 자리에 서서, 출발하면 플레이어를 쫓아온다.
                if (spawnRogueWind && data.ElementType == rogueWindElement)
                {
                    RogueWindStorm storm = RogueWindStorm.Attach(placedObject, CellSurface(hoverCell), rogueWindColorSource);

                    // 흰 폭풍 자체가 곧 그 원소의 모습이다. 기본 구 대신 이것을 쓴다.
                    if (storm.Visual != null)
                    {
                        placedObject.GetComponent<PlacedElementView>().ReplaceRound(storm.Visual);
                    }
                    else
                    {
                        // 그냥 구가 나오면 원인을 알 수 없으므로 분명히 남긴다.
                        Debug.LogWarning(
                            $"{name}: '{data.ElementName}'의 흰 폭풍을 만들지 못해 기본 구로 놓였습니다. " +
                            "베낄 Sandstorm이 씬에 있는지, Rogue Wind Color Source 칸을 확인하세요.", this);
                    }
                }

                HideIfInvisibleElement(placedObject, data);


                // 용암을 물 함정이 아닌 맨땅에 부었다. 사방으로 흘러 퍼지고 밟으면 타 죽는다.
                if (spillLava && data.ElementType == lavaElement)
                {
                    LavaSpill spill = LavaSpill.Attach(placedObject, CellSurface(hoverCell));

                    // 용암 웅덩이 자체가 곧 그 원소의 모습이다. 기본 구 대신 이것을 쓴다.
                    placedObject.GetComponent<PlacedElementView>().ReplaceRound(spill.Visual);
                }
            }

            placed = true;
        }

        End();
        return placed;
    }

    public void Cancel()
    {
        End();
    }

    private void End()
    {
        current = null;
        hoverValid = false;

        if (highlight != null)
        {
            Destroy(highlight);
            highlight = null;
        }

        if (cameraView != null)
        {
            cameraView.SetTopView(false);
        }
    }

    private Material HighlightFillMaterial
    {
        get
        {
            if (highlightFillMaterial == null)
            {
                highlightFillMaterial = CreateHighlightMaterial(highlightAlpha);
            }

            return highlightFillMaterial;
        }
    }

    private Material HighlightBorderMaterial
    {
        get
        {
            if (highlightBorderMaterial == null)
            {
                highlightBorderMaterial = CreateHighlightMaterial(highlightBorderAlpha);
            }

            return highlightBorderMaterial;
        }
    }

    // 바닥이 살짝 비쳐야 "저 칸 위에 놓인다"가 읽힌다. 완전히 불투명하면 바닥을 가려 버린다.
    //
    // 만들 때부터 알파를 넣어 둬야 한다. Unlit 셰이더가 빌드에서 빠졌을 때
    // WorldVisual이 Lit으로 대신 만들어 주는데, 그 갈림길이 알파를 보고 갈리기 때문이다.
    private Material CreateHighlightMaterial(float alpha)
    {
        Color start = validColor;
        start.a = alpha;

        return WorldVisual.CreateTransparentUnlit(start);
    }

    // 놓으려는 칸을 무슨 색으로 물들일지.
    //
    // 색이 두 가지를 한꺼번에 알려 준다.
    //   무엇을 놓는가 : 지금 든 원소의 색을 섞는다.
    //   놓으면 어떻게 되는가 : 함정을 끌 수 있는 칸 / 장애물에 속성이 붙는 칸 / 그냥 놓는 칸.
    // 마지막에 흰색 쪽으로 당겨 옅게 만든다. 바닥이 비쳐 보여야 어느 칸인지 알아볼 수 있다.
    private Color ResolveHighlightColor(ElementData data, Vector2Int cell)
    {
        Color state = validColor;

        if (FindCounterableTrapAt(cell, data) != null)
        {
            // 이 칸의 함정을 끌 수 있다.
            state = counterColor;
        }
        else if (Obstacle.FindAt(cell) != null)
        {
            // 장애물이 있는 칸이면 속성이 붙는다.
            state = hoverColor;
        }

        Color tint = tintHighlightWithElement && data != null
            ? Color.Lerp(state, ElementVisual.GetColor(data), 0.5f)
            : state;

        return Color.Lerp(tint, Color.white, highlightPaleness);
    }

    // 이 칸 위에 있고, 지금 들고 있는 원소로 끌 수 있는 함정을 찾는다.
    //
    // 불 함정(ElementTrapCube)뿐 아니라 물 함정(WaterTrap)도 찾아야 하므로 구체 타입이 아니라
    // 파훼 규약(IElementCounterTrap)으로 찾는다. 물 함정은 판정 콜라이더에 붙은
    // WaterTrapTriggerRelay가 이 규약을 대신 들고 있다.
    private IElementCounterTrap FindCounterableTrapAt(Vector2Int cell, ElementData data)
    {
        if (data == null || grid == null)
        {
            return null;
        }

        // 함정이 바닥에 살짝 파묻혀 있을 수 있으므로 바닥 아래쪽도 조금 훑는다.
        const float below = 1f;
        float size = grid.CellSize;
        float height = Mathf.Max(0.2f, trapSearchHeight) + below;
        float half = height * 0.5f;
        Vector3 center = CellSurface(cell) + Vector3.up * (half - below);

        // 함정 콜라이더는 트리거라서 트리거까지 포함해 훑어야 한다.
        Collider[] hits = Physics.OverlapBox(
            center,
            new Vector3(size * 0.5f, half, size * 0.5f),
            Quaternion.identity,
            ~0,
            QueryTriggerInteraction.Collide);

        IElementCounterTrap rejected = null;

        foreach (Collider hit in hits)
        {
            IElementCounterTrap trap = hit.GetComponentInParent<IElementCounterTrap>();
            if (trap == null)
            {
                continue;
            }

            if (trap.CanBeCounteredBy(data))
            {
                return trap;
            }

            rejected = trap;
        }

        // 함정 칸에 놓았는데 안 먹히는 경우가 잦아서, 왜 거절됐는지 남긴다.
        if (rejected != null)
        {
            // 규약만 들고 있어서는 어느 오브젝트인지 알 수 없다. 같은 것을 가리키는 쪽으로 확인한다.
            MonoBehaviour context = rejected as MonoBehaviour;

            Debug.LogWarning(
                $"[상쇄 0/3] {(context != null ? context.name : "함정")}: " +
                $"'{data.ElementName}'으로는 이 함정을 끌 수 없습니다. " +
                $"(이미 꺼짐={rejected.IsCountered}, 이미 원소 올림={rejected.IsArmed}) " +
                "함정의 counterElements 목록에 이 원소가 들어 있는지 확인하세요.",
                context);
        }

        return null;
    }

    // 상쇄에 쓸 원소를 함정 위에 올려 둔다.
    // 함정이 실제로 꺼지는 순간(보통 시작 버튼) 이 표시도 함께 사라진다.
    private void CreateCounterMark(ElementData data, Vector2Int cell, IElementCounterTrap trap)
    {
        // 함정에 올린 원소도 빈 칸에 놓은 원소와 똑같이 보여야 한다.
        // 탑뷰(2D)에서는 조합창과 같은 아이콘, 사선뷰(3D)에서는 원래 모습으로 전환하는 것은
        // 다른 배치물과 마찬가지로 PlacedElementView가 맡는다.
        GameObject mark = new GameObject($"Countered_{data.ElementName}", typeof(PlacedElementView));
        mark.transform.SetParent(transform, true);

        // 함정이 자기만의 모습을 내어 주면(물 함정의 시멘트처럼) 기본 구 대신 그것을 3D 모습으로 쓴다.
        // 함정이 제자리에 놓아 둔 것이므로 이 표식의 자식으로 삼지는 않는다. 치우는 것도 함정이 한다.
        Transform trapVisual = trap.GetCounterVisual(data);

        // 개수를 채워 넣는 함정(진흙 2개처럼)은 같은 칸에 표시가 겹친다.
        // 그대로 두면 하나만 놓인 것처럼 보이므로, 두 번째부터는 조금씩 어긋나게 놓는다.
        Vector3 spot = CellSurface(cell);
        int stacked = PlacedElement.FindAllAt(cell).Count;

        if (stacked > 0)
        {
            // 황금각으로 돌려 가며 놓는다. 몇 개가 쌓여도 한쪽으로 몰리지 않는다.
            float angle = stacked * 2.3999632f;
            spot += new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (grid.CellSize * 0.2f);
        }

        PlacedElementView view = mark.GetComponent<PlacedElementView>();
        view.Init(data, grid.CellSize, spot, trapVisual, placedIconDiameterRatio);

        HideIfInvisibleElement(mark, data);


        // 칸을 기억시켜 둬야 같은 칸에 다른 원소를 놓을 때 이 표시가 먼저 물러난다.
        PlacedElement.Attach(mark, data, PlacedElement.Kind.CounterMark).WithTrap(trap).WithCell(cell);

        if (trap.IsCountered)
        {
            StartCoroutine(ConsumeCounterMark(mark.transform));
            return;
        }

        // 아직 시작 전이면 올려 둔 채로 기다렸다가, 꺼질 때 같이 없앤다.
        trap.Countered += () =>
        {
            if (mark != null)
            {
                StartCoroutine(ConsumeCounterMark(mark.transform));
            }
        };
    }

    // 옆으로 번지면서 납작해지다가 없어진다. 물이 스며들어 마르는 느낌.
    private IEnumerator ConsumeCounterMark(Transform mark)
    {
        Vector3 startScale = mark.localScale;
        float lifetime = Mathf.Max(0.01f, counterMarkLifetime);

        for (float t = 0f; t < lifetime; t += Time.unscaledDeltaTime)
        {
            if (mark == null)
            {
                yield break;
            }

            float k = t / lifetime;
            mark.localScale = new Vector3(
                startScale.x * (1f + k * 0.4f),
                startScale.y * (1f - k),
                startScale.z * (1f + k * 0.4f));

            yield return null;
        }

        if (mark != null)
        {
            Destroy(mark.gameObject);
        }
    }

    private void BuildHighlight()
    {
        float size = grid.CellSize;

        // 테두리(바깥) 위에 안쪽 판을 살짝 띄워 얹는다.
        // 한 겹짜리 반투명 판만으로는 바닥과 섞여서 칸 경계가 잘 안 보인다.
        highlight = WorldVisual.CreateBox(
            "Highlight",
            transform,
            Vector3.zero,
            new Vector3(size, 0.06f, size),
            HighlightBorderMaterial);

        float inner = Mathf.Clamp01(1f - highlightBorderThickness * 2f);

        GameObject fill = WorldVisual.CreateBox(
            "HighlightFill",
            highlight.transform,
            new Vector3(0f, 0.4f, 0f),
            new Vector3(inner, 1f, inner),
            HighlightFillMaterial);

        Destroy(highlight.GetComponent<Collider>());
        Destroy(fill.GetComponent<Collider>());
        highlight.SetActive(false);
    }

    // 놓는 것 자체는 보이지 않아야 하는 원소면 3D 모습을 지운다.
    // 탑뷰의 납작한 아이콘은 남는다. 어느 칸에 놓았는지 보이지 않으면 회수할 수도 없다.
    private void HideIfInvisibleElement(GameObject placed, ElementData data)
    {
        if (invisibleElements == null || data == null)
        {
            return;
        }

        foreach (ElementType hidden in invisibleElements)
        {
            if (data.ElementType == hidden)
            {
                placed.GetComponent<PlacedElementView>().ClearRound();
                return;
            }
        }
    }

    // 파도를 부른다. 파도가 어떻게 생기고 어디서 오는지는 TsunamiWave가 다 알고 있으므로,
    // 함정에 붙어 있는 그것을 찾아 기준점만 넘겨 준다. 맨땅에 놓아도 함정과 똑같은 파도가 온다.
    private void SurgeTsunamiAt(Vector3 groundPosition, ElementData data)
    {
        TsunamiWave wave = tsunamiWave != null
            ? tsunamiWave
            : FindFirstObjectByType<TsunamiWave>(FindObjectsInactive.Include);

        // 씬에 없으면 직접 하나 만들어 쓴다.
        //
        // 이 연출은 원래 FireTrab 프리팹에 얹혀 있어서, 불 함정이 없는 스테이지에서는
        // 쓰나미를 놓아도 파도를 만들 방법이 아예 없었다.
        // 쓰나미는 어느 스테이지에서든 놓기만 하면 와야 하므로 씬 의존을 끊는다.
        if (wave == null)
        {
            // 이 배치 시스템 자신에게 붙인다.
            //
            // 따로 오브젝트를 만들어 CounterEffectRunner에 맡기면 안 된다.
            // 시작 버튼은 StageReset도 함께 울리는데, 그때 러너가 추적하던 연출을 정리하면서
            // 파도를 만들 이 컴포넌트까지 지워 버린다. 그러면 출발하는 순간 파도가 사라진다.
            // PlacementSystem은 판이 끝날 때까지 살아 있으므로 여기 붙여 두면 안전하다.
            wave = gameObject.AddComponent<TsunamiWave>();
            tsunamiWave = wave;

            // 파도 모양은 TsunamiWave가 직접 만들므로, 이대로도 파도는 정상적으로 온다.
            // 크기나 속도를 조절하려면 씬에 TsunamiWave를 두고 아래 칸에 물리면 된다.
            Debug.Log($"{name}: 씬에 TsunamiWave가 없어 기본값으로 하나 만들었습니다.", this);
        }

        // 어디까지 진행됐는지 남긴다. 파도가 안 보일 때 어느 단계에서 끊겼는지 알아야 한다.
        Debug.Log($"[쓰나미 1/2] '{data.ElementName}' 배치. {tsunamiDelay:0.0}초 뒤 {wave.name}이 파도를 부릅니다.", this);

        // 기다리는 일은 CounterEffectRunner에 맡긴다.
        // 여기서 돌리면 놓아 둔 원소를 회수하거나 화면을 넘길 때 코루틴이 끊겨 파도가 오지 않는다.
        // 한 번 놓았으면 무슨 일이 있어도 밀려와야 한다.
        // 이미 출발했으면 바로 세고, 아니면 출발 신호를 기다렸다가 센다.
        //
        // 출발을 코루틴 안에서 기다리면 안 된다.
        // 시작 버튼은 StageReset도 함께 울리는데, 그때 CounterEffectRunner가 돌던 연출을 정리하면서
        // 기다리던 코루틴까지 같이 끊어 버린다. 그래서 출발하는 순간 파도가 사라진다.
        // 신호를 직접 받아 그때 새로 시작하면 끊길 일이 없다.
        // 이미 출발한 뒤에 놓았을 때만 곧바로 센다.
        // 아직 출발 전이면 시작 버튼이 있든 없든 기다린다 — 배치 중에는 절대 파도가 오면 안 된다.
        if (StageStartButton.HasStarted)
        {
            CounterEffectRunner.Run(SurgeAfterDelay(wave, groundPosition));
            return;
        }

        if (!StageStartButton.Exists)
        {
            Debug.LogWarning(
                $"{name}: 씬에 StageStartButton이 없어 파도가 출발 신호를 기다립니다. " +
                "출발 신호가 없으면 파도는 오지 않습니다.", this);
        }

        void OnStarted()
        {
            StageStartButton.StageStarted -= OnStarted;
            CounterEffectRunner.Run(SurgeAfterDelay(wave, groundPosition));
        }

        StageStartButton.StageStarted += OnStarted;
    }

    private IEnumerator SurgeAfterDelay(TsunamiWave wave, Vector3 groundPosition)
    {
        if (tsunamiDelay > 0f)
        {
            yield return new WaitForSeconds(tsunamiDelay);
        }

        if (wave != null)
        {
            // 파도는 언제나 플레이어 등 뒤에서 온다. 놓은 칸은 "언제 오느냐"에만 쓰인다.
            wave.Surge();
        }
    }

    private GameObject CreatePlacedElement(ElementData data, Vector2Int cell)
    {
        GameObject placed = new GameObject($"Placed_{data.ElementName}", typeof(PlacedElementView));
        placed.transform.SetParent(transform, true);

        // 탑뷰(2D)/사선뷰(3D)에 따른 모습 전환은 PlacedElementView가 맡는다.
        placed.GetComponent<PlacedElementView>().Init(data, grid.CellSize, CellSurface(cell), diameterRatioOverride: placedIconDiameterRatio);

        PlacedElement.Attach(placed, data, PlacedElement.Kind.Element).WithCell(cell);
        return placed;
    }

    // 자동차(ChargingCar)를 막는 흙 벽을 한 칸에 세운다. 차가 여기 막히면 플레이어는 죽지 않는다.
    private void CreateStoneWall(Vector2Int cell)
    {
        float size = grid.CellSize;
        Vector3 groundPosition = CellSurface(cell);

        GameObject root = new GameObject($"StoneWall_{cell.x}_{cell.y}", typeof(PlacedElementView));
        root.transform.SetParent(transform, true);
        root.transform.position = groundPosition;

        GameObject wall;

        if (wallPrefab != null)
        {
            wall = Instantiate(wallPrefab, groundPosition, Quaternion.identity, root.transform);
        }
        else
        {
            // 칸 바닥에서 위로 세운다.
            wall = WorldVisual.CreateBox(
                "Wall",
                root.transform,
                Vector3.up * (wallHeight * 0.5f),
                new Vector3(size * 0.95f, wallHeight, size * 0.95f),
                WorldVisual.CreateLit(wallColor));
        }

        // 자동차가 이 벽을 인식하고 멈출 수 있도록 표식과 콜라이더를 보장한다.
        if (wall.GetComponentInChildren<Collider>() == null)
        {
            wall.AddComponent<BoxCollider>();
        }

        if (wall.GetComponentInChildren<CarBlocker>() == null)
        {
            wall.AddComponent<CarBlocker>();
        }

        // 탑뷰(2D)에서는 원소 아이콘, 사선뷰(3D)에서는 벽 모습을 보여준다.
        root.GetComponent<PlacedElementView>().Init(WallElement, size, groundPosition, wall.transform, placedIconDiameterRatio);

        PlacedElement.Attach(root, WallElement, PlacedElement.Kind.Wall).WithCell(cell);
    }

    // 흑요석 벽·돌풍·늪처럼 자동차를 파훼하는 설치물을 한 칸에 세운다.
    // 무엇이 어떻게 생기고 어떻게 동작하는지는 CarCounterBuilder가 정한다.
    //
    // 흙 벽과 마찬가지로 Kind.Wall로 등록해 두어, 오른쪽 클릭으로 회수할 수 있고
    // 죽어서 스테이지가 되돌아갈 때 함께 치워진다.
    private void CreateCarCounter(ElementData data, Vector2Int cell)
    {
        float size = grid.CellSize;
        Vector3 groundPosition = CellSurface(cell);

        GameObject root = new GameObject($"Placed_{data.ElementName}_{cell.x}_{cell.y}", typeof(PlacedElementView));
        root.transform.SetParent(transform, true);
        root.transform.position = groundPosition;

        Transform structure = CarCounterBuilder.Build(data, root.transform, groundPosition, size, windPrefab);

        // 탑뷰(2D)에서는 원소 아이콘, 사선뷰(3D)에서는 설치물 모습을 보여준다.
        root.GetComponent<PlacedElementView>().Init(data, size, groundPosition, structure, placedIconDiameterRatio);

        PlacedElement.Attach(root, data, PlacedElement.Kind.Wall).WithCell(cell);
    }

    private bool TryGetCell(Vector2 screenPosition, out Vector2Int cell)
    {
        cell = default;

        if (worldCamera == null)
        {
            return false;
        }

        Ray ray = worldCamera.ScreenPointToRay(screenPosition);

        // 먼저 실제 지형에 광선을 쏜다. 건물 옥상을 가리키고 있으면 옥상 지점이 나온다.
        // 지형에 맞지 않으면(하늘을 가리키는 등) 예전처럼 격자 평면으로 떨어뜨린다.
        Vector3 point;

        if (TryRaycastTerrain(ray, out RaycastHit hit))
        {
            point = hit.point;
        }
        else if (grid.Surface.Raycast(ray, out float distance))
        {
            point = ray.GetPoint(distance);
        }
        else
        {
            return false;
        }

        cell = grid.WorldToCell(point);
        return grid.Contains(cell);
    }

    // ───────────────────────────── 지형 높이 ─────────────────────────────

    // 칸 한가운데의 "땅 위" 지점.
    //
    // 예전에는 격자 평면(맵 오브젝트의 y) 높이를 그대로 썼다. 그래서 도로보다 높은 곳
    // (인도 턱, 건물 옥상, 언덕)에 놓으면 놓은 것이 지형에 파묻혀 보이지 않았다.
    // 이제는 그 칸의 지형 제일 윗면을 찾아서 그 위에 놓는다.
    private Vector3 CellSurface(Vector2Int cell)
    {
        Vector3 center = grid.CellToWorld(cell);
        center.y = SampleSurfaceY(center);
        return center;
    }

    // 그 지점 지형의 제일 윗면 높이. 찾지 못하면 격자 평면 높이를 쓴다.
    private float SampleSurfaceY(Vector3 point)
    {
        float top = grid.SurfaceY + surfaceProbeUp;
        Vector3 origin = new Vector3(point.x, top, point.z);
        float distance = surfaceProbeUp + surfaceProbeDown;

        // 함정은 트리거라서 QueryTriggerInteraction.Ignore로 자동으로 빠진다.
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore);

        float best = 0f;
        bool found = false;

        foreach (RaycastHit hit in hits)
        {
            if (!IsTerrain(hit.collider))
            {
                continue;
            }

            if (!found || hit.point.y > best)
            {
                best = hit.point.y;
                found = true;
            }
        }

        float ground = found ? best : grid.SurfaceY;

        // 물 함정 위에 놓은 것은 물에 얹혀야 한다.
        //
        // 물의 판정 콜라이더는 트리거라 위 광선에 잡히지 않는다. 그래서 물 위에 올린 원소가
        // 웅덩이 바닥 높이에 놓여 물에 잠겨 버렸다 — "물 위에는 안 올라가고 옆 칸에만 올라간다"로
        // 보이던 것이 이것이다. 물의 윗면을 찾아 그보다 낮으면 끌어올린다.
        float surface = SampleCounterTrapTopY(point);
        return surface > ground ? surface : ground;
    }

    // 그 지점을 덮고 있는 파훼 함정(물웅덩이 등)의 제일 윗면. 없으면 float.MinValue.
    //
    // 함정 판정 콜라이더는 트리거라서 광선으로는 잡히지 않는다. 그 자리에 겹치는 것을
    // 직접 훑어서 윗면을 잰다.
    private float SampleCounterTrapTopY(Vector3 point)
    {
        float half = (surfaceProbeUp + surfaceProbeDown) * 0.5f;
        Vector3 center = new Vector3(point.x, grid.SurfaceY + surfaceProbeUp - half, point.z);

        Collider[] hits = Physics.OverlapBox(
            center,
            new Vector3(0.01f, half, 0.01f),
            Quaternion.identity,
            ~0,
            QueryTriggerInteraction.Collide);

        float best = float.MinValue;

        foreach (Collider hit in hits)
        {
            if (hit == null || hit.GetComponentInParent<IElementCounterTrap>() == null)
            {
                continue;
            }

            // 함정 앞을 지나는지 보는 트리거는 함정의 몸이 아니다.
            // 길게 누워 있어서 그 윗면을 바닥으로 삼으면 원소가 허공에 뜬다.
            if (hit.GetComponentInParent<TrapApproachZone>() != null)
            {
                continue;
            }

            float top = SurfaceTopOf(hit);

            if (top > best)
            {
                best = top;
            }
        }

        return best;
    }

    // 함정 판정 콜라이더가 대신하는 "눈에 보이는 윗면". 잴 수 없으면 float.MinValue.
    //
    // 콜라이더 자체를 쓰면 안 된다. Stage_01 물 함정의 판정 콜라이더는 물에 빠지는 것을 잡으려고
    // 세로로 세운 캡슐이라 윗면이 수면(0.04m)보다 1.15m나 높다. 거기에 얹으면 원소가 허공에 뜬다.
    // 자식도 훑으면 안 된다 — 물 위에 떠 있는 화살표 표시(1.14m)가 잡힌다.
    // 판정 콜라이더가 붙어 있는 바로 그 오브젝트의 모습만 본다. 그것이 물 그 자체다.
    private static float SurfaceTopOf(Collider hit)
    {
        Renderer renderer = hit.GetComponent<Renderer>();

        if (renderer == null || renderer is ParticleSystemRenderer)
        {
            return float.MinValue;
        }

        return renderer.bounds.max.y;
    }

    // 마우스가 가리키는 지형. 놓아 둔 원소와 함정 트리거는 건너뛴다.
    private bool TryRaycastTerrain(Ray ray, out RaycastHit result)
    {
        result = default;

        RaycastHit[] hits = Physics.RaycastAll(ray, 1000f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (!IsTerrain(hit.collider))
            {
                continue;
            }

            result = hit;
            return true;
        }

        return false;
    }

    // 지형으로 볼 수 있는 콜라이더인지.
    //
    // 걸어다니는 플레이어를 지형으로 세면 플레이어가 서 있는 칸에 놓을 때
    // 원소가 플레이어 머리 위(1.2m)에 얹힌다. 놓아 둔 원소도 마찬가지로 위에 쌓인다.
    private bool IsTerrain(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        if (PlayerLocator.FindPlayerRoot(collider.transform) != null)
        {
            return false;
        }

        if (collider.GetComponentInParent<PlacedElement>() != null)
        {
            return false;
        }

        // 함정 뭉치에 속하면서 길바닥에서 한참 떠 있는 것은 밟고 설 땅이 아니다.
        //
        // 물 함정 위 20m 하늘에는 나무다리가 떨어질 차례를 기다리며 매달려 있다.
        // 그것을 땅으로 세면 두 가지가 한꺼번에 어긋난다.
        //   - 물 칸에 놓은 원소가 도로가 아니라 하늘의 다리 위에 얹힌다.
        //   - 함정을 찾는 상자도 그 높이에서 훑어서, 정작 발밑의 물을 놓친다.
        //     그래서 "물 위에는 원소가 안 올라가고 옆 칸에 놓아야 올라가는" 것으로 보인다.
        // 건물 옥상은 함정 뭉치가 아니므로 그대로 놓을 수 있다.
        if (grid != null
            && collider.bounds.min.y > grid.SurfaceY + TrapSkyHeight
            && BelongsToCounterTrap(collider))
        {
            return false;
        }

        return true;
    }

    // 파훼 함정 뭉치(함정 오브젝트와 그 형제들)에 속한 콜라이더인지.
    //
    // 함정 스크립트가 늘 조상에 있지는 않다. Stage_01 물 함정은 관리 스크립트와 물 모델,
    // 나무다리가 같은 뿌리 아래 형제로 놓여 있다. 그래서 뿌리까지 올라가 확인한다.
    private static bool BelongsToCounterTrap(Collider collider)
    {
        if (collider.GetComponentInParent<IElementCounterTrap>() != null)
        {
            return true;
        }

        Transform root = collider.transform.root;
        return root != collider.transform && root.GetComponentInChildren<IElementCounterTrap>(true) != null;
    }
}
