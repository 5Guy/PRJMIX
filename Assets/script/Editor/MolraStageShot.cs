using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// 실제로 플레이하면서 원소를 몇 개 놓아 보고, 탑뷰와 사선뷰를 각각 한 장씩 찍는다.
//
// 배치한 원소가 칸에 비해 얼마나 큰지, 함정 표시 위에 무엇이 그려지는지, 물이 물처럼 보이는지는
// 숫자로는 알 수 없다. 사람이 보는 것과 같은 화면을 그대로 뽑아서 눈으로 확인한다.
//
//   Unity.exe -batchmode -projectPath . -executeMethod MolraStageShot.ShootStage01
//   (-nographics 를 주면 안 된다)
public static class MolraStageShot
{
    private const string SceneKey = "Molra.StageShot.Scene";

    public static void ShootStage01() => Run("Assets/Scenes/Stage_01.unity");

    private static void Run(string scenePath)
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        {
            Debug.LogError("[스테이지 그림] 그래픽 장치가 없습니다. -nographics 없이 실행해 주세요.");
            EditorApplication.Exit(1);
            return;
        }

        SessionState.SetString(SceneKey, scenePath);
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        EditorApplication.update += PumpPlayerLoop;
        EditorApplication.EnterPlaymode();
    }

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

        GameObject host = new GameObject("MolraStageShotRunner");
        Object.DontDestroyOnLoad(host);
        host.AddComponent<MolraStageShotRunner>();
    }
}

public class MolraStageShotRunner : MonoBehaviour
{
    private const int Size = 900;

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
            Debug.LogError($"[스테이지 그림] 배치 시스템({placement != null}) / 격자({grid != null}) / 카메라({camera != null})를 찾지 못했습니다.");
            EditorApplication.ExitPlaymode();
            yield break;
        }

        // 함정이 선 칸과 그 앞뒤 칸에 원소를 놓아 본다.
        // 함정 칸에 놓은 것은 "올려 둔 원소", 빈 칸에 놓은 것은 그냥 놓은 원소가 된다.
        List<Vector2Int> cells = new List<Vector2Int>();
        List<ElementData> picks = new List<ElementData>();

        WaterTrap water = Object.FindFirstObjectByType<WaterTrap>();
        if (water != null)
        {
            cells.Add(grid.WorldToCell(water.VisualRoot.position));
            picks.Add(Load("Lava"));
        }

        ElementTrapCube fire = null;
        foreach (ElementTrapCube cube in Object.FindObjectsByType<ElementTrapCube>(FindObjectsSortMode.None))
        {
            if (cube.ElementData != null && cube.ElementData.ElementType == ElementType.Fire)
            {
                fire = cube;
                break;
            }
        }

        Vector2Int center = fire != null
            ? grid.WorldToCell(fire.VisualRoot.position)
            : grid.WorldToCell(PlayerLocator.FindPlayer().position);

        // 함정 칸 주변의 빈 칸들 — 크기를 견주어 볼 수 있게 골고루 놓는다.
        cells.Add(center);
        picks.Add(Load("Water"));

        cells.Add(center + new Vector2Int(-1, 1));
        picks.Add(Load("Water"));

        cells.Add(center + new Vector2Int(0, 1));
        picks.Add(Load("Fire"));

        cells.Add(center + new Vector2Int(1, 1));
        picks.Add(Load("Mountain"));

        cells.Add(center + new Vector2Int(-1, 2));
        picks.Add(Load("Sword"));

        cells.Add(center + new Vector2Int(0, 2));
        picks.Add(Load("Steel"));

        cells.Add(center + new Vector2Int(1, 2));
        picks.Add(Load("Grass"));

        // 카메라를 함정 쪽으로 옮겨 놓아야 놓을 칸이 화면 안에 들어온다.
        Vector3 focus = grid.CellToWorld(center);

        if (view != null)
        {
            view.enabled = false;
        }

        for (int i = 0; i < cells.Count; i++)
        {
            if (picks[i] == null)
            {
                continue;
            }

            AimCamera(camera, focus, true);
            yield return null;

            placement.Begin(picks[i]);

            Vector3 target = grid.CellToWorld(cells[i]);
            Vector2 screen = camera.WorldToScreenPoint(target);

            placement.UpdatePreview(screen);
            bool placed = placement.Confirm(screen);

            Debug.Log($"[스테이지 그림] {picks[i].name} → 칸 {cells[i]} {(placed ? "놓음" : "실패")}");
            yield return null;
        }

        for (int i = 0; i < 5; i++)
        {
            yield return null;
        }

        // 탑뷰(조합창을 연 상태) — 함정 표시와 원소 아이콘이 어떻게 겹치는지 본다.
        SetTopView(view, true);

        for (int i = 0; i < 5; i++)
        {
            yield return null;
        }

        AimCamera(camera, focus, true);
        ReportPlaced("탑뷰");
        Capture(camera, "stage01_top");

        // 사선뷰(조합창을 닫은 상태) — 3D 모습으로 바뀐 원소를 본다.
        SetTopView(view, false);

        for (int i = 0; i < 5; i++)
        {
            yield return null;
        }

        AimCamera(camera, focus, false);
        ReportPlaced("사선뷰");
        Capture(camera, "stage01_side");

        Debug.Log("[스테이지 그림] 끝");
        EditorApplication.ExitPlaymode();
    }

    // 놓인 원소가 지금 시점에서 어떤 모습으로 서 있는지 적는다.
    // 탑뷰인데 3D 모습이 그대로 켜져 있으면 여기서 드러난다.
    private static void ReportPlaced(string label)
    {
        Debug.Log($"[스테이지 그림] {label} : IsTopView={CameraViewController.IsTopView}, 뷰 {(Object.FindFirstObjectByType<CameraViewController>(FindObjectsInactive.Include) != null ? "있음" : "없음")}");

        foreach (PlacedElementView placed in Object.FindObjectsByType<PlacedElementView>(FindObjectsSortMode.None))
        {
            Bounds bounds = ElementVisual.MeasureBounds(placed.gameObject);
            bool visible = false;

            if (placed.Round != null)
            {
                foreach (Renderer renderer in placed.Round.GetComponentsInChildren<Renderer>(true))
                {
                    visible |= renderer.enabled;
                }
            }

            Debug.Log($"[스테이지 그림] {label} '{placed.name}' 3D보임={visible} 위치 {placed.transform.position} 크기 {bounds.size}");
        }
    }

    // 카메라 추적은 꺼 두었지만 시점 전환(2D 아이콘 ↔ 3D 모습)은 그대로 알려야 한다.
    private static void SetTopView(CameraViewController view, bool topView)
    {
        // 시점은 조합창이 정한다.
        //
        // CraftingPanelUI.Update가 매 프레임 "열려 있으면 탑뷰, 닫혀 있으면 사선뷰"로 되돌려 놓기
        // 때문에, 카메라에 직접 SetTopView를 불러 봐야 다음 프레임에 그대로 뒤집힌다.
        // 사람이 하는 것과 똑같이 조합창을 여닫아서 시점을 바꾼다.
        CraftingPanelUI panel = Object.FindFirstObjectByType<CraftingPanelUI>(FindObjectsInactive.Include);

        if (panel != null)
        {
            panel.SetOpen(topView);
            return;
        }

        if (view == null)
        {
            view = Object.FindFirstObjectByType<CameraViewController>(FindObjectsInactive.Include);
        }

        if (view == null)
        {
            Debug.LogError("[스테이지 그림] 조합창도 카메라도 찾지 못해 시점을 바꾸지 못했습니다.");
            return;
        }

        view.SetTopView(topView);
    }

    // 탑뷰는 바로 위에서, 사선뷰는 비스듬히 내려다본다.
    private static void AimCamera(Camera camera, Vector3 focus, bool topView)
    {
        camera.orthographic = false;
        camera.fieldOfView = 45f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 300f;

        if (topView)
        {
            camera.transform.position = focus + Vector3.up * 11f;
            camera.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
        }
        else
        {
            Quaternion angle = Quaternion.Euler(26f, 18f, 0f);
            camera.transform.position = focus - angle * Vector3.forward * 9f + Vector3.up * 0.4f;
            camera.transform.rotation = angle;
        }
    }

    private static ElementData Load(string assetName)
    {
        ElementData data = AssetDatabase.LoadAssetAtPath<ElementData>($"Assets/Data/Nomal/{assetName}.asset");

        if (data == null)
        {
            Debug.LogWarning($"[스테이지 그림] 원소 '{assetName}' 을 찾지 못했습니다.");
        }

        return data;
    }

    private static void Capture(Camera camera, string label)
    {
        RenderTexture target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
        Texture2D shot = new Texture2D(Size, Size, TextureFormat.RGB24, false);

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
            shot.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            shot.Apply();
            RenderTexture.active = previous;

            string directory = Path.Combine(Path.GetTempPath(), "MolraStageShot");
            Directory.CreateDirectory(directory);

            string path = Path.Combine(directory, $"{label}.png");
            File.WriteAllBytes(path, shot.EncodeToPNG());

            Debug.Log($"[스테이지 그림] 저장했습니다: {path}");
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
