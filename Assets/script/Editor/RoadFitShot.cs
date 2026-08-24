using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// 배치 칸 3개가 실제로 도로에 어떻게 얹히는지 위에서 내려다본 그림으로 저장한다.
//
// 숫자로만 재면 "도로 폭 9.48m, 칸 3개가 정확히 9.48m"라서 맞는 것처럼 보이는데,
// 메시에 안 쓰는 정점이 남아 있거나 가장자리 선이 삐져나와 있으면 눈에 보이는 도로는 더 좁다.
// 그래서 눈으로 확인할 수 있게 칸 경계를 그려서 한 장 뽑는다.
public static class RoadFitShot
{
    private const int Size = 1024;

    // 위아래로 이만큼의 두 배(18m)가 그림에 담긴다. 도로 폭 10m 안팎이 넉넉히 들어온다.
    private const float OrthoSize = 9f;

    // 배치 모드용. (-executeMethod RoadFitShot.ShootStage01)
    public static void ShootStage01()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Stage_01.unity", OpenSceneMode.Single);
        Shoot("Stage_01");
    }

    // 배치 모드용. (-executeMethod RoadFitShot.ShootAllStages)
    public static void ShootAllStages()
    {
        foreach (string path in StageGridDiagnostics.StageScenes)
        {
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Debug.Log("──────────────────────────────────────────");
            Debug.Log($"[그림] 씬: {path}");
            Shoot(System.IO.Path.GetFileNameWithoutExtension(path));
        }
    }

    [MenuItem("Tools/Molra/진단 : 도로에 칸이 맞는지 그림으로 저장")]
    public static void ShootOpenScene()
    {
        Shoot(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    private static void Shoot(string label)
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        {
            Debug.LogError("[그림] 그래픽 장치가 없어 렌더링할 수 없습니다. (-nographics 없이 실행해야 합니다)");
            return;
        }

        if (!TrapPlacement.TryGetPathDirection(out Vector3 direction, out Transform player))
        {
            Debug.LogError("[그림] 플레이어를 찾지 못했습니다.");
            return;
        }

        if (!TrapPlacement.TryResolveGrid(out Vector3 origin, out float cellSize, out int _, out int _))
        {
            Debug.LogError("[그림] 격자를 계산하지 못했습니다.");
            return;
        }

        bool alongIsZ = PlacementGridLayout.IsAlongZ(direction);
        Vector3 across = Vector3.Cross(Vector3.up, direction).normalized;

        // 플레이어가 서 있는 칸을 가운데로 삼는다.
        int playerAcrossCell = Mathf.FloorToInt(
            ((alongIsZ ? player.position.x : player.position.z) - (alongIsZ ? origin.x : origin.z)) / cellSize);

        // 1단계: 아무것도 안 그린 상태로 찍어서 아스팔트가 실제로 어디까지인지 픽셀로 잰다.
        Render(player, direction, label + "_clean", out Texture2D clean, out Camera shotCamera, out Vector3 cameraPosition, out Vector3 cameraRight);

        if (clean != null)
        {
            MeasureAsphalt(clean, cameraPosition, cameraRight, alongIsZ, player, cellSize);
            Object.DestroyImmediate(clean);
        }

        // 2단계: 지금 설정으로 칸 경계를 그려서 눈으로 확인할 그림을 찍는다.
        Transform markers = BuildCellMarkers(origin, cellSize, alongIsZ, playerAcrossCell, direction, across, player);

        try
        {
            Render(player, direction, label, out Texture2D shot, out Camera _, out Vector3 _, out Vector3 _);

            if (shot != null)
            {
                Object.DestroyImmediate(shot);
            }
        }
        finally
        {
            if (markers != null)
            {
                Object.DestroyImmediate(markers.gameObject);
            }
        }
    }

    // 찍은 그림에서 "어두운 아스팔트"가 가로로 어디부터 어디까지인지 잰다.
    //
    // 메시 크기(Renderer AABB)는 인도까지 품고 있어서 도로보다 훨씬 넓게 잡힌다.
    // 눈에 보이는 아스팔트가 진짜 도로이므로, 찍은 그림을 직접 읽는 것이 가장 확실하다.
    private static void MeasureAsphalt(
        Texture2D image, Vector3 cameraPosition, Vector3 cameraRight, bool alongIsZ, Transform player, float cellSize)
    {
        int center = Size / 2;

        System.Collections.Generic.List<int> lefts = new System.Collections.Generic.List<int>();
        System.Collections.Generic.List<int> rights = new System.Collections.Generic.List<int>();

        // 가로선을 여러 줄 훑는다.
        //
        // 길 색을 미리 정해 두지 않는다. 스테이지마다 아스팔트(어두운 회색)일 수도,
        // 흙길(밝은 모래색)일 수도 있기 때문이다. 대신 화면 한가운데(= 플레이어가 걷는 자리)의
        // 색을 그 줄의 "길 색"으로 삼고, 색이 비슷한 동안 좌우로 뻗어 나간다.
        // 차선 점선이 한가운데에 걸린 줄은 폭이 짧게 나와서 아래에서 걸러진다.
        for (int y = 0; y < Size; y += 4)
        {
            Color reference = image.GetPixel(center, y);

            int left = center;
            while (left > 0 && IsSimilar(image.GetPixel(left - 1, y), reference))
            {
                left--;
            }

            int right = center;
            while (right < Size - 1 && IsSimilar(image.GetPixel(right + 1, y), reference))
            {
                right++;
            }

            // 너무 좁은 줄(점선에 걸림)이나 화면 끝까지 닿은 줄(교차로)은 버린다.
            int span = right - left;
            if (span < Size / 8 || left == 0 || right == Size - 1)
            {
                continue;
            }

            lefts.Add(left);
            rights.Add(right);
        }

        if (lefts.Count == 0)
        {
            Debug.LogWarning("[그림] 아스팔트를 찾지 못했습니다. 카메라가 도로 위에 있는지 확인하세요.");
            return;
        }

        lefts.Sort();
        rights.Sort();

        int leftPixel = lefts[lefts.Count / 2];
        int rightPixel = rights[rights.Count / 2];

        float leftAcross = PixelToAcross(leftPixel, cameraPosition, cameraRight, alongIsZ);
        float rightAcross = PixelToAcross(rightPixel, cameraPosition, cameraRight, alongIsZ);

        float min = Mathf.Min(leftAcross, rightAcross);
        float max = Mathf.Max(leftAcross, rightAcross);
        float width = max - min;
        float middle = (min + max) * 0.5f;

        float playerAcross = alongIsZ ? player.position.x : player.position.z;

        Debug.Log(
            $"[그림] 눈에 보이는 길: 폭 {width:0.###}m, 한가운데 {middle:0.###}, 범위 {min:0.###} ~ {max:0.###} " +
            $"(줄 {lefts.Count}개 기준)");
        Debug.Log(
            $"[그림] 지금 칸 크기 {cellSize:0.###}m → 1칸 {cellSize:0.###}m / 3칸 {cellSize * 3f:0.###}m. " +
            $"길에 3칸을 넣으려면 roadWidthOverride={width:0.###} + 칸수 Three, " +
            $"1칸을 넣으려면 roadWidthOverride={width:0.###} + 칸수 One. " +
            $"roadCenterOffset={middle - playerAcross:0.###} (플레이어 가로 좌표 {playerAcross:0.###} 기준)");
    }

    // 같은 바닥으로 볼 만큼 색이 비슷한지.
    //
    // 명암만 조금 다른 같은 재질(그림자 진 아스팔트 등)은 같은 것으로 보고,
    // 인도·잔디처럼 확실히 다른 색은 갈라놓을 정도의 여유다.
    private static bool IsSimilar(Color pixel, Color reference)
    {
        return Mathf.Abs(pixel.r - reference.r) < 0.09f
            && Mathf.Abs(pixel.g - reference.g) < 0.09f
            && Mathf.Abs(pixel.b - reference.b) < 0.09f;
    }

    private static float PixelToAcross(int pixel, Vector3 cameraPosition, Vector3 cameraRight, bool alongIsZ)
    {
        // 카메라는 정사영이고 그림이 정사각형이라, 가로도 orthographicSize의 두 배가 담긴다.
        float offset = ((pixel + 0.5f) / Size - 0.5f) * (OrthoSize * 2f);
        Vector3 point = cameraPosition + cameraRight * offset;

        return alongIsZ ? point.x : point.z;
    }

    // 도로를 가로지르는 칸 경계 4줄을 진행 방향으로 길게 그어 둔다.
    // 가운데 3칸(플레이어 칸 ±1)의 바깥 경계가 도로 가장자리와 맞는지 보려는 것이다.
    private static Transform BuildCellMarkers(
        Vector3 origin, float cellSize, bool alongIsZ, int centerCell,
        Vector3 direction, Vector3 across, Transform player)
    {
        Transform root = new GameObject("__RoadFitMarkers").transform;

        Material edge = WorldVisual.CreateUnlit(new Color(1f, 0f, 1f, 1f));
        Material inner = WorldVisual.CreateUnlit(new Color(0f, 1f, 1f, 1f));

        float originAcross = alongIsZ ? origin.x : origin.z;
        float y = player.position.y + 0.08f;

        // 칸 경계는 4줄이다. 바깥 두 줄이 3칸의 끝이다.
        for (int i = -1; i <= 2; i++)
        {
            float acrossValue = originAcross + (centerCell + i) * cellSize;
            float offset = acrossValue - (alongIsZ ? player.position.x : player.position.z);

            Vector3 center = player.position + across * offset + Vector3.up * (y - player.position.y);

            GameObject line = WorldVisual.CreateBox(
                $"Edge_{i}",
                root,
                Vector3.zero,
                new Vector3(0.12f, 0.02f, 60f),
                (i == -1 || i == 2) ? edge : inner);

            line.transform.position = center;
            line.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            Object.DestroyImmediate(line.GetComponent<Collider>());
        }

        return root;
    }

    // 위에서 내려다본 그림을 한 장 찍어 저장하고, 잰 값을 계산할 수 있게 카메라 정보도 돌려준다.
    private static void Render(
        Transform player, Vector3 direction, string label,
        out Texture2D image, out Camera camera, out Vector3 cameraPosition, out Vector3 cameraRight)
    {
        image = null;
        camera = null;

        GameObject cameraObject = new GameObject("__RoadFitCamera");
        camera = cameraObject.AddComponent<Camera>();

        camera.orthographic = true;
        camera.orthographicSize = OrthoSize;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 200f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;

        // 바로 위에서 내려다보되, 화면 세로축을 진행 방향에 맞춘다.
        // 그러면 도로를 가로지르는 방향이 화면 가로축이 되어 폭을 재기 쉽다.
        cameraObject.transform.position = player.position + direction * 8f + Vector3.up * 40f;
        cameraObject.transform.rotation = Quaternion.LookRotation(Vector3.down, direction);

        cameraPosition = cameraObject.transform.position;
        cameraRight = cameraObject.transform.right;

        RenderTexture target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
        Texture2D shot = new Texture2D(Size, Size, TextureFormat.RGB24, false);

        try
        {
            camera.targetTexture = target;

            // URP에서는 Camera.Render()가 막혀 있어서 렌더 요청을 넣어야 한다.
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

            // 프로젝트의 Temp 폴더는 유니티가 종료할 때 지워 버리므로 밖에 저장한다.
            string directory = Path.Combine(Path.GetTempPath(), "MolraRoadFit");
            Directory.CreateDirectory(directory);

            string path = Path.Combine(directory, $"{label}_roadfit.png");
            File.WriteAllBytes(path, shot.EncodeToPNG());

            Debug.Log($"[그림] 저장했습니다: {path}");

            image = shot;
            shot = null;
        }
        finally
        {
            camera.targetTexture = null;
            Object.DestroyImmediate(cameraObject);
            camera = null;

            target.Release();
            Object.DestroyImmediate(target);

            if (shot != null)
            {
                Object.DestroyImmediate(shot);
            }
        }
    }
}
