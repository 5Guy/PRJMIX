using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// 고친 원소 모델을 실제 스테이지의 격자에 나란히 놓고 한 장 찍는다.
//
// 왜 필요한가
//   ElementModelShot은 원소 하나를 빈 바닥에 세워 놓고 찍는다. 그것만으로는
//   "칸에 견주어 얼마나 큰지", "옆 칸 원소와 나란히 두었을 때 구별이 되는지",
//   "게임 조명 아래에서도 색이 살아 있는지"를 알 수 없다. 사람이 보는 화면 그대로 찍어야 한다.
//
//   Unity.exe -batchmode -projectPath . -executeMethod ElementGridShot.ShootChanged
//   (-nographics 를 주면 안 된다. 그리는 장치가 필요하다)
public static class ElementGridShot
{
    private const string SceneKey = "Molra.GridShot.Scene";
    private const string ModeKey = "Molra.GridShot.Mode";
    private const string ScenePath = "Assets/Scenes/Stage_01.unity";

    // 쓰나미는 놓아 봐야 그 자리에 아무것도 안 선다. 밀려오는 파도를 따로 찍어야 한다.
    public static bool IsTsunamiMode => SessionState.GetString(ModeKey, string.Empty) == "tsunami";

    // 이번에 손본 원소들. 격자에 놓이는 차례이기도 하다.
    //
    // 쓰나미와 흙은 뺐다. 둘 다 맵에 모델을 세우지 않는 원소다
    // (쓰나미는 PlacementSystem.invisibleElements 에 들어 있어 파도 연출만 남고,
    //  흙은 놓는 자리에 돌담을 세운다). 넣어 봐야 빈 칸만 찍힌다.
    public static readonly string[] Picks =
    {
        "Mountain", "Wood", "Forest",
        "Ore", "Volcano", "Rust",
    };

    // 밀려오는 쓰나미 파도를 몇 장 찍는다.
    public static void ShootTsunami()
    {
        SessionState.SetString(ModeKey, "tsunami");
        ShootChanged();
    }

    public static void ShootChanged()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        {
            Debug.LogError("[격자 그림] 그래픽 장치가 없습니다. -nographics 없이 실행해 주세요.");
            EditorApplication.Exit(1);
            return;
        }

        SessionState.SetString(SceneKey, ScenePath);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        EditorApplication.update += PumpPlayerLoop;
        EditorApplication.EnterPlaymode();
    }

    // 배치 모드에서는 에디터가 알아서 프레임을 돌리지 않는다. 직접 돌려 줘야 코루틴이 진행된다.
    private static void PumpPlayerLoop()
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.QueuePlayerLoopUpdate();
        }
    }

    private static void HandlePlayModeChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredEditMode)
        {
            return;
        }

        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        EditorApplication.update -= PumpPlayerLoop;
        SessionState.EraseString(SceneKey);
        SessionState.EraseString(ModeKey);

        if (Application.isBatchMode)
        {
            EditorApplication.Exit(0);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallShooter()
    {
        if (string.IsNullOrEmpty(SessionState.GetString(SceneKey, string.Empty)))
        {
            return;
        }

        GameObject host = new GameObject("ElementGridShotRunner");
        Object.DontDestroyOnLoad(host);
        host.AddComponent<ElementGridShotRunner>();
    }
}

public class ElementGridShotRunner : MonoBehaviour
{
    private const int Width = 1400;
    private const int Height = 900;

    private void Start()
    {
        StartCoroutine(Shoot());
    }

    private IEnumerator Shoot()
    {
        Time.captureDeltaTime = 1f / 50f;

        for (int i = 0; i < 10; i++)
        {
            yield return null;
        }

        PlacementSystem placement = Object.FindFirstObjectByType<PlacementSystem>();
        PlacementGrid grid = Object.FindFirstObjectByType<PlacementGrid>();
        CameraViewController view = Object.FindFirstObjectByType<CameraViewController>();
        Camera camera = Camera.main;

        if (placement == null || grid == null || camera == null)
        {
            Debug.LogError($"[격자 그림] 배치({placement != null}) / 격자({grid != null}) / 카메라({camera != null})를 찾지 못했습니다.");
            EditorApplication.ExitPlaymode();
            yield break;
        }

        // 조합창을 닫아야 3D 모습이 나온다. 열려 있으면 바닥에 눕는 2D 아이콘으로 바뀐다.
        CraftingPanelUI panel = Object.FindFirstObjectByType<CraftingPanelUI>(FindObjectsInactive.Include);

        if (panel != null)
        {
            panel.SetOpen(false);
        }

        if (view != null)
        {
            view.enabled = false;
        }

        if (ElementGridShot.IsTsunamiMode)
        {
            yield return ShootTsunami(placement, grid, camera);
            EditorApplication.ExitPlaymode();
            yield break;
        }

        // 함정 칸은 피한다. 함정 위에 놓으면 원소가 함정을 없애는 연출로 바뀌어
        // (쓰나미는 납작한 물자국이 되고 흙은 돌담이 된다) 모델을 볼 수 없다.
        HashSet<Vector2Int> blocked = new HashSet<Vector2Int>();

        foreach (ElementTrapCube cube in Object.FindObjectsByType<ElementTrapCube>(FindObjectsSortMode.None))
        {
            Mark(blocked, grid.WorldToCell(cube.VisualRoot.position));
        }

        foreach (WaterTrap trap in Object.FindObjectsByType<WaterTrap>(FindObjectsSortMode.None))
        {
            Mark(blocked, grid.WorldToCell(trap.VisualRoot.position));
        }

        // 플레이어 뒤쪽 빈 찻길. 앞쪽은 함정과 그 연출이 깔려 있어 원소가 가린다.
        Vector2Int origin = grid.WorldToCell(PlayerLocator.FindPlayer().position) + new Vector2Int(-1, -2);

        // 네 칸씩 두 줄, 한 칸씩 띄워서. 붙여 놓으면 산처럼 칸보다 큰 것이 옆 원소를 덮는다.
        List<Vector2Int> cells = new List<Vector2Int>();

        for (int i = 0; i < ElementGridShot.Picks.Length; i++)
        {
            // 찻길 폭이 네 칸뿐이라 가로는 한 칸씩, 세로는 두 칸씩 띄운다.
            Vector2Int cell = origin + new Vector2Int(i % 3, -(i / 3) * 2);

            // 함정에 걸리면 한 줄 위로 물러난다.
            // 이미 잡아 둔 자리와 겹치면 안 된다 — 겹치면 먼저 놓은 원소가 밀려나 사라진다.
            while (blocked.Contains(cell) || cells.Contains(cell))
            {
                cell += new Vector2Int(0, 1);
            }

            cells.Add(cell);
            blocked.Add(cell);
        }

        Vector3 focus = grid.CellToWorld(origin + new Vector2Int(1, -1));

        for (int i = 0; i < cells.Count; i++)
        {
            ElementData data = Load(ElementGridShot.Picks[i]);

            if (data == null)
            {
                continue;
            }

            // 놓을 칸이 화면 안에 있어야 클릭 좌표가 격자에 떨어진다.
            AimCamera(camera, focus);
            yield return null;

            placement.Begin(data);

            Vector3 target = grid.CellToWorld(cells[i]);
            Vector2 screen = camera.WorldToScreenPoint(target);

            placement.UpdatePreview(screen);
            bool placed = placement.Confirm(screen);

            Debug.Log($"[격자 그림] {data.name} → 칸 {cells[i]} {(placed ? "놓음" : "실패")}");
            yield return null;
        }

        for (int i = 0; i < 5; i++)
        {
            yield return null;
        }

        foreach (PlacedElementView placedView in Object.FindObjectsByType<PlacedElementView>(FindObjectsSortMode.None))
        {
            Bounds bounds = ElementVisual.MeasureBounds(placedView.gameObject);
            Debug.Log($"[격자 그림] '{placedView.name}' 자리 {placedView.transform.position} 크기 {bounds.size}");
        }

        AimCamera(camera, focus);
        Capture(camera, "grid_side");

        Debug.Log("[격자 그림] 끝");
        EditorApplication.ExitPlaymode();
    }

    // 쓰나미를 놓고, 파도가 플레이어 쪽으로 밀려오는 동안 여러 장 찍는다.
    //
    // 파도는 플레이어 앞 26m 에서 솟아 18m/s 로 다가온다. 한 장만 찍으면 아직 멀리 있거나
    // 이미 지나가 버린 순간이 걸리기 십상이라, 다가오는 사이를 나눠 여러 장 남긴다.
    private IEnumerator ShootTsunami(PlacementSystem placement, PlacementGrid grid, Camera camera)
    {
        Transform player = PlayerLocator.FindPlayer();
        Vector2Int cell = grid.WorldToCell(player.position) + new Vector2Int(0, -1);

        // 플레이어 뒤에서 그가 보는 쪽을 함께 바라본다. 파도가 정면에서 다가온다.
        Vector3 advance = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
        Vector3 eye = player.position - advance * 9f + Vector3.up * 5.5f;

        camera.orthographic = false;
        camera.fieldOfView = 50f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 300f;
        camera.transform.position = eye;
        camera.transform.rotation = Quaternion.LookRotation(advance + Vector3.down * 0.32f, Vector3.up);

        yield return null;

        ElementData tsunami = Load("Tsunami");

        if (tsunami == null)
        {
            yield break;
        }

        placement.Begin(tsunami);

        Vector3 target = grid.CellToWorld(cell);
        Vector2 screen = camera.WorldToScreenPoint(target);

        placement.UpdatePreview(screen);
        Debug.Log($"[격자 그림] 쓰나미 → 칸 {cell} {(placement.Confirm(screen) ? "놓음" : "실패")}");

        // 출발 신호를 눌러야 파도가 온다.
        // PlacementSystem 은 배치가 끝나기 전에는 파도를 예약만 해 두고 붙잡고 있다
        // (배치하는 도중에 파도가 덮치면 안 되니까). 사람이 하듯 시작 버튼을 누른다.
        if (!StageStartButton.HasStarted)
        {
            StageStartButton button = Object.FindFirstObjectByType<StageStartButton>(FindObjectsInactive.Include);

            if (button == null)
            {
                Debug.LogError("[격자 그림] 시작 버튼을 찾지 못해 파도를 부를 수 없습니다.");
                yield break;
            }

            button.Begin();
            Debug.Log("[격자 그림] 출발 신호를 눌렀습니다.");
        }

        // captureDeltaTime 이 1/50 이므로 한 프레임이 0.02초다.
        // 놓고 1초 뒤에 파도가 솟고, 26m 를 18m/s 로 오니 닿기까지 다시 1.4초쯤 걸린다.
        int[] frames = { 60, 75, 88, 100 };
        int elapsed = 0;

        for (int shot = 0; shot < frames.Length; shot++)
        {
            while (elapsed < frames[shot])
            {
                elapsed++;
                yield return null;
            }

            camera.transform.position = eye;
            camera.transform.rotation = Quaternion.LookRotation(advance + Vector3.down * 0.32f, Vector3.up);

            TsunamiWaveZone zone = Object.FindFirstObjectByType<TsunamiWaveZone>();

            if (zone == null)
            {
                Debug.Log($"[격자 그림] {shot + 1}번째: 파도가 아직/이미 없습니다.");
            }
            else
            {
                System.Text.StringBuilder parts = new System.Text.StringBuilder();

                foreach (Renderer piece in zone.GetComponentsInChildren<Renderer>(true))
                {
                    parts.Append($" [{piece.name} {piece.bounds.center:F1} {piece.bounds.size:F1}]");
                }

                Debug.Log($"[격자 그림] {shot + 1}번째: 파도 {zone.transform.position}, 플레이어 {player.position}, 조각{parts}");
            }

            Capture(camera, $"tsunami_{shot + 1}");
        }
    }

    // 함정이 선 칸과 그 둘레를 표시해 둔다. 함정 자체가 한 칸보다 넓게 퍼져 있는 경우가 있다.
    private static void Mark(HashSet<Vector2Int> blocked, Vector2Int cell)
    {
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                blocked.Add(cell + new Vector2Int(x, y));
            }
        }
    }

    // 놓은 줄 전체가 들어오도록 비스듬히 내려다본다.
    private static void AimCamera(Camera camera, Vector3 focus)
    {
        camera.orthographic = false;
        camera.fieldOfView = 40f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 300f;

        Quaternion angle = Quaternion.Euler(30f, 10f, 0f);
        camera.transform.position = focus - angle * Vector3.forward * 9f + Vector3.up * 0.3f;
        camera.transform.rotation = angle;
    }

    private static ElementData Load(string assetName)
    {
        ElementData data = AssetDatabase.LoadAssetAtPath<ElementData>($"Assets/Data/Nomal/{assetName}.asset");

        if (data == null)
        {
            Debug.LogWarning($"[격자 그림] 원소 '{assetName}' 을 찾지 못했습니다.");
        }

        return data;
    }

    private static void Capture(Camera camera, string label)
    {
        RenderTexture target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        Texture2D shot = new Texture2D(Width, Height, TextureFormat.RGB24, false);

        try
        {
            camera.targetTexture = target;

            RenderPipeline.StandardRequest request = new RenderPipeline.StandardRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(camera, request))
            {
                RenderPipeline.SubmitRenderRequest(camera, request);
            }
            else
            {
                camera.Render();
            }

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            shot.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            shot.Apply();
            RenderTexture.active = previous;

            string directory = Path.Combine(Path.GetTempPath(), "MolraGridShot");
            Directory.CreateDirectory(directory);

            string path = Path.Combine(directory, $"{label}.png");
            File.WriteAllBytes(path, shot.EncodeToPNG());

            Debug.Log($"[격자 그림] 저장했습니다: {path}");
        }
        finally
        {
            camera.targetTexture = null;
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(shot);
        }
    }
}
