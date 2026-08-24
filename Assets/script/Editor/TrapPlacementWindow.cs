using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Assets/Prefab/Trab 의 함정을 씬 뷰에서 정확히 찍어 놓는 창.
// Tools > Molra > 함정 배치 창 으로 연다.
//
// 그냥 프리팹을 끌어다 놓는 것과 다른 점
//   - 클릭한 지점의 바닥 높이를 재서, 함정의 "보이는 바닥"이 딱 지면에 닿게 내린다.
//   - PlacementGrid(파훼 원소를 놓는 격자)의 칸 한가운데에 맞춰 세운다.
//     함정이 칸 경계에 걸치면 어느 칸에 원소를 올려야 할지 애매해지기 때문이다.
//   - 플레이어 진행 방향을 기준으로 회전을 잡아 준다(정면 / 마주보기 / 가로지르기).
//
// 이미 놓아 둔 함정도 아래쪽 "선택한 오브젝트 정리" 버튼으로 같은 규칙에 맞춰 줄 수 있다.
public class TrapPlacementWindow : EditorWindow
{
    // 플레이어 진행 방향을 기준으로 함정을 어느 쪽으로 세울지.
    private enum Facing
    {
        None,        // 프리팹 그대로
        AlongPath,   // 플레이어와 같은 방향
        FacePlayer,  // 플레이어를 마주본다 (돌진하는 자동차)
        AcrossPath,  // 길을 가로지른다
    }

    private readonly List<GameObject> prefabs = new List<GameObject>();
    private int selected = -1;

    private bool placing;
    private bool snapToCell = true;
    private float fallbackStep = 1f;
    private float lift;
    private float sideOffset;
    private Facing facing = Facing.AlongPath;
    private float extraYaw;

    private Vector3 previewPoint;
    private bool previewValid;
    private Vector2 scroll;

    [MenuItem("Tools/Molra/함정 배치 창")]
    public static void Open()
    {
        GetWindow<TrapPlacementWindow>("함정 배치").minSize = new Vector2(300f, 420f);
    }

    private void OnEnable()
    {
        RefreshPrefabs();
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void RefreshPrefabs()
    {
        prefabs.Clear();

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { TrapPlacement.TrapFolder }))
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab != null)
            {
                prefabs.Add(prefab);
            }
        }

        prefabs.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        selected = Mathf.Clamp(selected, prefabs.Count > 0 ? 0 : -1, prefabs.Count - 1);
    }

    // ───────────────────────────── 창 ─────────────────────────────

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("함정 프리팹", EditorStyles.boldLabel);

        if (prefabs.Count == 0)
        {
            EditorGUILayout.HelpBox($"{TrapPlacement.TrapFolder} 에 프리팹이 없습니다.", MessageType.Warning);
        }

        for (int i = 0; i < prefabs.Count; i++)
        {
            bool isSelected = i == selected;
            if (GUILayout.Toggle(isSelected, prefabs[i].name, EditorStyles.miniButton) != isSelected)
            {
                selected = i;
            }
        }

        if (GUILayout.Button("목록 새로고침"))
        {
            RefreshPrefabs();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("배치 규칙", EditorStyles.boldLabel);

        snapToCell = EditorGUILayout.Toggle(
            new GUIContent("칸 한가운데에 맞추기", "파훼 원소를 놓는 PlacementGrid의 칸 중앙에 세운다"),
            snapToCell);

        if (!snapToCell)
        {
            fallbackStep = EditorGUILayout.FloatField(
                new GUIContent("격자 간격(m)", "격자를 못 찾았거나 끄고 쓸 때의 반올림 단위"),
                fallbackStep);
        }

        facing = (Facing)EditorGUILayout.EnumPopup(
            new GUIContent("바라보는 방향", "플레이어 진행 방향을 기준으로 잡는다"),
            facing);

        extraYaw = EditorGUILayout.FloatField(new GUIContent("추가 회전(도)"), extraYaw);

        lift = EditorGUILayout.FloatField(
            new GUIContent("높이 보정(m)", "바닥에 붙인 뒤 이만큼 더 띄운다. 음수면 파묻는다"),
            lift);

        sideOffset = EditorGUILayout.FloatField(
            new GUIContent("옆으로 밀기(m)", "진행 방향 기준 오른쪽이 +. 길 가장자리에 붙일 때 쓴다"),
            sideOffset);

        EditorGUILayout.Space();

        GUI.enabled = selected >= 0;
        string label = placing ? "배치 모드 끄기 (Esc)" : "배치 모드 켜기";
        if (GUILayout.Button(label, GUILayout.Height(30f)))
        {
            placing = !placing;
            SceneView.RepaintAll();
        }
        GUI.enabled = true;

        if (placing)
        {
            EditorGUILayout.HelpBox("씬 뷰에서 클릭하면 그 자리에 놓입니다. Esc로 끕니다.", MessageType.Info);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("선택한 오브젝트 정리", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("이미 놓아 둔 함정도 같은 규칙에 맞춰 줍니다.", EditorStyles.wordWrappedMiniLabel);

        GUI.enabled = Selection.transforms.Length > 0;

        if (GUILayout.Button("바닥에 붙이기"))
        {
            ApplyToSelection(alignHeight: true, alignCell: false, alignFacing: false);
        }

        if (GUILayout.Button("칸 한가운데로 맞추기"))
        {
            ApplyToSelection(alignHeight: false, alignCell: true, alignFacing: false);
        }

        if (GUILayout.Button("바라보는 방향 적용"))
        {
            ApplyToSelection(alignHeight: false, alignCell: false, alignFacing: true);
        }

        if (GUILayout.Button("전부 적용"))
        {
            ApplyToSelection(alignHeight: true, alignCell: true, alignFacing: true);
        }

        GUI.enabled = true;

        EditorGUILayout.EndScrollView();
    }

    // ───────────────────────────── 씬 뷰 ─────────────────────────────

    private void OnSceneGUI(SceneView view)
    {
        if (!placing || selected < 0)
        {
            return;
        }

        // 클릭이 오브젝트 선택으로 새어 나가지 않게 이 도구가 기본 조작을 가로챈다.
        int control = GUIUtility.GetControlID(FocusType.Passive);
        HandleUtility.AddDefaultControl(control);

        Event e = Event.current;

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            placing = false;
            Repaint();
            e.Use();
            return;
        }

        if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag || e.type == EventType.Repaint)
        {
            previewValid = TryResolvePoint(e.mousePosition, out previewPoint);
        }

        if (e.type == EventType.Repaint && previewValid)
        {
            DrawPreview();
        }

        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            if (TryResolvePoint(e.mousePosition, out Vector3 point))
            {
                Place(point);
            }

            e.Use();
        }

        if (e.type == EventType.MouseMove)
        {
            view.Repaint();
        }
    }

    // 마우스 아래의 지면. 콜라이더가 없는 맵에서도 잡히도록 HandleUtility를 쓴다.
    private bool TryResolvePoint(Vector2 mousePosition, out Vector3 point)
    {
        point = Vector3.zero;

        if (!HandleUtility.PlaceObject(mousePosition, out Vector3 hit, out Vector3 _))
        {
            // 맵 위가 아니면 y=0 평면으로 떨어뜨린다.
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            Plane plane = new Plane(Vector3.up, Vector3.zero);

            if (!plane.Raycast(ray, out float distance))
            {
                return false;
            }

            hit = ray.GetPoint(distance);
        }

        point = ApplyPlanarRules(hit);
        return true;
    }

    // 칸 맞춤 + 옆으로 밀기. 높이는 실제로 놓을 때 다시 잰다.
    private Vector3 ApplyPlanarRules(Vector3 point)
    {
        if (snapToCell && TrapPlacement.TryResolveGrid(out Vector3 origin, out float cellSize, out int _, out int _))
        {
            point = TrapPlacement.SnapToCellCenter(point, origin, cellSize);
        }
        else if (!snapToCell)
        {
            point = TrapPlacement.SnapToStep(point, fallbackStep);
        }

        if (!Mathf.Approximately(sideOffset, 0f) && TrapPlacement.TryGetPathDirection(out Vector3 direction, out Transform _))
        {
            point += Vector3.Cross(Vector3.up, direction).normalized * sideOffset;
        }

        return point;
    }

    private void DrawPreview()
    {
        float radius = 0.5f;
        if (snapToCell && TrapPlacement.TryResolveGrid(out Vector3 _, out float cellSize, out int _, out int _))
        {
            radius = cellSize * 0.5f;
        }

        Handles.color = new Color(0.35f, 1f, 0.6f, 0.9f);
        Handles.DrawWireDisc(previewPoint + Vector3.up * 0.02f, Vector3.up, radius);
        Handles.DrawLine(previewPoint, previewPoint + Vector3.up * 1.5f);

        Handles.color = new Color(0.35f, 1f, 0.6f, 0.35f);
        Handles.ArrowHandleCap(0, previewPoint, ResolveRotation(), 1.2f, EventType.Repaint);

        Handles.Label(previewPoint + Vector3.up * 1.7f, prefabs[selected].name);
    }

    private Quaternion ResolveRotation()
    {
        Quaternion rotation = selected >= 0 && selected < prefabs.Count
            ? prefabs[selected].transform.rotation
            : Quaternion.identity;

        if (facing != Facing.None && TrapPlacement.TryGetPathDirection(out Vector3 direction, out Transform _))
        {
            Vector3 forward = facing switch
            {
                Facing.AlongPath => direction,
                Facing.FacePlayer => -direction,
                Facing.AcrossPath => Vector3.Cross(Vector3.up, direction).normalized,
                _ => direction,
            };

            rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        return Quaternion.AngleAxis(extraYaw, Vector3.up) * rotation;
    }

    private void Place(Vector3 point)
    {
        GameObject prefab = prefabs[selected];
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

        Undo.RegisterCreatedObjectUndo(instance, $"{prefab.name} 배치");

        // 회전을 먼저 잡아야 판정 콜라이더의 위치(=뿌리와의 어긋남)가 제대로 계산된다.
        instance.transform.rotation = ResolveRotation();
        instance.transform.position = point;

        TrapPlacement.AlignHorizontally(instance.transform, point);
        TrapPlacement.SnapToGround(instance.transform, TrapPlacement.SampleGroundY(point, point.y), lift);

        // 함정끼리 겹치면 앞 함정의 접근 트리거가 뒤 함정 위에 얹힌다. 눈에 띄게 알려 준다.
        WarnIfTooCloseToOtherTrap(instance);

        Selection.activeGameObject = instance;
        EditorGUIUtility.PingObject(instance);

        Debug.Log($"[함정 배치] {prefab.name} → {instance.transform.position}", instance);
    }

    // 콜라이더가 없어 바닥을 못 쟀을 때 기준이 되는 높이. 플레이어가 서 있는 높이가 곧 지면이다.
    private static float GroundFallbackY =>
        TrapPlacement.TryGetPathDirection(out Vector3 _, out Transform player) && player != null
            ? player.position.y
            : 0f;

    // 함정 앞 접근 트리거(기본 3.5m)가 앞 함정과 겹치면 파훼 타이밍이 헷갈린다.
    private static void WarnIfTooCloseToOtherTrap(GameObject placed)
    {
        const float minimumGap = 4f;

        foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (behaviour is not IElementCounterTrap || behaviour.transform.IsChildOf(placed.transform))
            {
                continue;
            }

            float gap = Vector3.Distance(
                Vector3.ProjectOnPlane(behaviour.transform.position, Vector3.up),
                Vector3.ProjectOnPlane(placed.transform.position, Vector3.up));

            if (gap < minimumGap)
            {
                Debug.LogWarning(
                    $"[함정 배치] '{placed.name}'이 '{behaviour.name}'과 {gap:0.0}m밖에 떨어져 있지 않습니다. " +
                    $"함정 앞 접근 트리거가 겹칠 수 있으니 {minimumGap}m 이상 띄우는 것을 권합니다.",
                    placed);
                return;
            }
        }
    }

    private void ApplyToSelection(bool alignHeight, bool alignCell, bool alignFacing)
    {
        Transform[] targets = Selection.transforms;
        Undo.RecordObjects(targets, "함정 배치 정리");

        foreach (Transform target in targets)
        {
            if (alignCell)
            {
                // 뿌리가 아니라 함정이 실제로 서 있는 자리가 칸 한가운데에 오도록 맞춘다.
                Vector3 reference = TrapPlacement.ResolveAnchorPoint(target.gameObject);

                TrapPlacement.AlignHorizontally(target, ApplyPlanarRules(reference));
            }

            if (alignFacing)
            {
                // "회전 없음"이면 이미 잡아 둔 방향을 지우지 않고 추가 회전만 준다.
                target.rotation = facing == Facing.None
                    ? Quaternion.AngleAxis(extraYaw, Vector3.up) * target.rotation
                    : ResolveRotation();
            }

            if (alignHeight)
            {
                float groundY = TrapPlacement.SampleGroundY(target.position, GroundFallbackY);
                TrapPlacement.SnapToGround(target, groundY, lift);
            }

            EditorUtility.SetDirty(target);
        }

        Debug.Log($"[함정 배치] 선택한 {targets.Length}개를 정리했습니다.");
    }
}
