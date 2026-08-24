using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 씬 뷰에서 함정과 도착 깃발을 손으로 끌면 배치 칸 한가운데에 착 붙게 한다.
//
// 메뉴로 한 번에 맞추는 도구(TrapCellAligner)만으로는 레벨을 짤 수 없다.
// 눈으로 보며 옮기고 싶은데, 옮기는 족족 칸 경계에 걸치면 매번 메뉴를 다시 눌러야 한다.
// 그래서 끌고 있는 동안 바로 칸에 붙여 주고, 주변 칸도 그려서 어디에 놓이는지 보여 준다.
//
// 켜고 끄기 : Tools > Molra > 칸에 맞춰 끌기
//
// 옮겨 주는 것
//   - x/z : 판정 콜라이더 한가운데가 칸 한가운데에 오도록. (함정은 뿌리와 판정 위치가 크게 다르다)
//   - y   : 그 자리 바닥 높이에 맞춰. 단 옆으로 움직였을 때만 손대므로 높이는 따로 조절할 수 있다.
[InitializeOnLoad]
public static class CellSnapDragging
{
    private const string MenuPath = "Tools/Molra/칸에 맞춰 끌기";
    private const string EnabledKey = "Molra.CellSnapDragging.Enabled";

    // 이 정도 안쪽이면 이미 칸 한가운데에 있는 것으로 본다.
    private const float Tolerance = 0.005f;

    // 고른 것 주변 몇 칸까지 격자를 그릴지.
    private const int DrawRadius = 5;

    private static Vector3 gridOrigin;
    private static float gridCellSize;
    private static int gridColumns;
    private static int gridRows;
    private static bool gridReady;
    private static Scene gridScene;

    private static float groundFallbackY;

    static CellSnapDragging()
    {
        SceneView.duringSceneGui += OnSceneGui;
        Selection.selectionChanged += InvalidateGrid;
        EditorSceneManager.sceneOpened += (scene, mode) => InvalidateGrid();
        EditorApplication.delayCall += () => Menu.SetChecked(MenuPath, Enabled);
    }

    public static bool Enabled
    {
        get => EditorPrefs.GetBool(EnabledKey, true);
        set => EditorPrefs.SetBool(EnabledKey, value);
    }

    [MenuItem(MenuPath)]
    private static void Toggle()
    {
        Enabled = !Enabled;
        Menu.SetChecked(MenuPath, Enabled);

        Debug.Log($"[칸에 맞춰 끌기] {(Enabled ? "켰습니다" : "껐습니다")}. " +
                  "함정과 도착 깃발을 씬 뷰에서 끌면 배치 칸 한가운데에 붙습니다.");

        SceneView.RepaintAll();
    }

    [MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, Enabled);
        return true;
    }

    [MenuItem("Tools/Molra/칸에 맞춰 끌기 : 격자 다시 계산")]
    private static void RefreshGrid()
    {
        InvalidateGrid();

        if (TryResolveGrid())
        {
            Debug.Log($"[칸에 맞춰 끌기] 격자를 다시 계산했습니다. 칸 크기 {gridCellSize:0.###}m, {gridColumns}x{gridRows}칸");
        }

        SceneView.RepaintAll();
    }

    private static void InvalidateGrid()
    {
        gridReady = false;
    }

    // 격자 계산은 맵의 Renderer를 전부 훑어서 무겁다. 매 프레임 다시 하면 씬 뷰가 버벅인다.
    // 고른 것이 바뀌거나 씬을 열 때만 다시 계산하고, 끄는 동안에는 계산해 둔 것을 쓴다.
    private static bool TryResolveGrid()
    {
        Scene active = SceneManager.GetActiveScene();

        if (gridReady && gridScene == active)
        {
            return true;
        }

        gridReady = TrapPlacement.TryResolveGrid(out gridOrigin, out gridCellSize, out gridColumns, out gridRows);
        gridScene = active;

        Transform player = PlayerLocator.FindPlayer();
        groundFallbackY = player != null ? player.position.y : gridOrigin.y;

        return gridReady;
    }

    private static void OnSceneGui(SceneView view)
    {
        if (!Enabled || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (Selection.transforms.Length == 0 || !TryResolveGrid())
        {
            return;
        }

        foreach (Transform selected in Selection.transforms)
        {
            if (!TryResolveTarget(selected, out Transform target, out bool isFlag))
            {
                continue;
            }

            SnapToCell(target, isFlag);
            DrawCellsAround(target, isFlag);
        }
    }

    /// <summary>
    /// 오브젝트 하나를 배치 칸 한가운데로 맞춘다.
    /// 씬 뷰에서 끌 때 쓰는 것과 완전히 같은 계산이라, 메뉴로 불러도 결과가 같다.
    /// </summary>
    /// <returns>함정이나 도착 깃발이라 실제로 맞출 수 있었으면 true.</returns>
    public static bool SnapObject(Transform selected)
    {
        if (!TryResolveGrid() || !TryResolveTarget(selected, out Transform target, out bool isFlag))
        {
            return false;
        }

        SnapToCell(target, isFlag);
        return true;
    }

    [MenuItem("Tools/Molra/고른 것을 칸 한가운데로 맞추기")]
    private static void SnapSelection()
    {
        InvalidateGrid();

        int snapped = 0;

        foreach (Transform selected in Selection.transforms)
        {
            if (SnapObject(selected))
            {
                snapped++;
            }
        }

        Debug.Log(snapped > 0
            ? $"[칸 맞춤] 고른 것 {snapped}개를 칸 한가운데로 맞췄습니다."
            : "[칸 맞춤] 고른 것 중에 함정이나 도착 깃발이 없습니다.");
    }

    // 고른 것이 함정이나 도착 깃발이면 통째로 옮길 뿌리를 알려 준다.
    private static bool TryResolveTarget(Transform selected, out Transform target, out bool isFlag)
    {
        target = null;
        isFlag = false;

        StageGoalFlag flag = selected.GetComponentInParent<StageGoalFlag>();
        if (flag != null)
        {
            target = flag.transform;
            isFlag = true;
            return true;
        }

        target = TrapCellAligner.ResolveTrapRoot(selected);
        return target != null;
    }

    private static void SnapToCell(Transform target, bool isFlag)
    {
        Vector3 anchor = AnchorPoint(target, isFlag);
        Vector3 center = TrapPlacement.SnapToCellCenter(anchor, gridOrigin, gridCellSize);

        // 이미 칸 한가운데면 손대지 않는다.
        // 이 검사가 있어야 위아래로만 올렸다 내렸다 할 때 바닥으로 도로 끌려가지 않는다.
        if (Mathf.Abs(anchor.x - center.x) < Tolerance && Mathf.Abs(anchor.z - center.z) < Tolerance)
        {
            return;
        }

        Undo.RecordObject(target, "칸에 맞춰 끌기");

        if (isFlag)
        {
            // 깃발은 뿌리와 판정 위치가 거의 같아서 뿌리를 그대로 칸 한가운데에 둔다.
            target.position = new Vector3(center.x, target.position.y, center.z);
        }
        else
        {
            TrapPlacement.AlignHorizontally(target, center);
        }

        TrapPlacement.SnapToGround(target, TrapPlacement.SampleGroundY(center, groundFallbackY));
        EditorUtility.SetDirty(target);
    }

    // 이 오브젝트에서 "칸에 맞춰야 할 지점".
    // 함정은 실제로 죽는 자리(판정 콜라이더)이지 뿌리가 아니다.
    private static Vector3 AnchorPoint(Transform target, bool isFlag)
    {
        if (isFlag)
        {
            return target.position;
        }

        Physics.SyncTransforms();

        Collider anchor = TrapPlacement.ResolveAnchorCollider(target.gameObject);
        return anchor != null ? anchor.bounds.center : target.position;
    }

    // 지금 어느 칸에 들어가 있는지 보이도록 주변 격자를 그린다.
    private static void DrawCellsAround(Transform target, bool isFlag)
    {
        Vector3 anchor = AnchorPoint(target, isFlag);

        int cellX = Mathf.FloorToInt((anchor.x - gridOrigin.x) / gridCellSize);
        int cellZ = Mathf.FloorToInt((anchor.z - gridOrigin.z) / gridCellSize);

        float y = TrapPlacement.SampleGroundY(anchor, groundFallbackY) + 0.05f;

        int minX = Mathf.Max(0, cellX - DrawRadius);
        int maxX = Mathf.Min(gridColumns, cellX + DrawRadius + 1);
        int minZ = Mathf.Max(0, cellZ - DrawRadius);
        int maxZ = Mathf.Min(gridRows, cellZ + DrawRadius + 1);

        Handles.color = new Color(1f, 1f, 1f, 0.25f);

        for (int x = minX; x <= maxX; x++)
        {
            float worldX = gridOrigin.x + x * gridCellSize;
            Handles.DrawLine(
                new Vector3(worldX, y, gridOrigin.z + minZ * gridCellSize),
                new Vector3(worldX, y, gridOrigin.z + maxZ * gridCellSize));
        }

        for (int z = minZ; z <= maxZ; z++)
        {
            float worldZ = gridOrigin.z + z * gridCellSize;
            Handles.DrawLine(
                new Vector3(gridOrigin.x + minX * gridCellSize, y, worldZ),
                new Vector3(gridOrigin.x + maxX * gridCellSize, y, worldZ));
        }

        // 지금 들어가 있는 칸은 채워서 확실히 보여 준다.
        Vector3 center = new Vector3(
            gridOrigin.x + (cellX + 0.5f) * gridCellSize,
            y,
            gridOrigin.z + (cellZ + 0.5f) * gridCellSize);

        float half = gridCellSize * 0.5f;
        Vector3[] quad =
        {
            center + new Vector3(-half, 0f, -half),
            center + new Vector3(-half, 0f, half),
            center + new Vector3(half, 0f, half),
            center + new Vector3(half, 0f, -half),
        };

        Handles.DrawSolidRectangleWithOutline(quad, new Color(0.35f, 0.9f, 1f, 0.2f), new Color(0.35f, 0.9f, 1f, 0.9f));
        Handles.Label(center, $"칸 ({cellX}, {cellZ})");
    }
}
