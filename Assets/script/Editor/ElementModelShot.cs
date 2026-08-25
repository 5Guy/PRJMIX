using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// 원소마다 물려 둔 3D 모델을 한 장씩 찍어 하나의 대조표 그림으로 저장한다.
//
// "맵에 놓았을 때 이상하게 생겼다"는 것은 숫자로는 알 수 없다. 실제로 놓이는 그대로
// (ElementVisual.CreateWorldModel과 같은 크기·자리 맞춤) 세워 놓고 눈높이에서 찍어 봐야
// 어느 원소가 이름과 안 맞는지 가려낼 수 있다.
//
//   Unity.exe -batchmode -projectPath . -quit -executeMethod ElementModelShot.ShootAll
//   (-nographics 를 주면 안 된다. 그리는 장치가 필요하다)
public static class ElementModelShot
{
    private const string ElementFolder = "Assets/Data/Nomal";

    private const int Tile = 256;
    private const int Columns = 6;
    private const int EachTile = 512;

    // 놓았을 때의 지름. PlacementSystem 기본값(칸 0.85배)과 같은 자리에서 본다.
    private const float Diameter = 1f;

    public static void ShootAll()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        {
            Debug.LogError("[원소 그림] 그래픽 장치가 없습니다. -nographics 없이 실행해 주세요.");
            return;
        }

        // 씬을 먼저 갈아 끼운다. 새 씬을 열면 쓰이지 않는 에셋이 정리되면서
        // 미리 불러 둔 ElementData가 통째로 사라진다.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        List<ElementData> elements = LoadElements();

        GameObject lightObject = new GameObject("Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.3f;
        lightObject.transform.rotation = Quaternion.Euler(38f, -140f, 0f);

        // 바닥이 있어야 "땅에 얹혀 있는지 파묻혀 있는지"가 보인다.
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.localScale = Vector3.one * 2f;
        floor.GetComponent<Renderer>().sharedMaterial = WorldVisual.CreateLit(new Color(0.5f, 0.52f, 0.55f));

        int rows = Mathf.CeilToInt(elements.Count / (float)Columns);
        Texture2D sheet = new Texture2D(Columns * Tile, rows * Tile, TextureFormat.RGB24, false);

        System.Text.StringBuilder order = new System.Text.StringBuilder();
        order.AppendLine($"[원소 그림] 대조표 {Columns}칸 x {rows}줄, 왼쪽 위부터 오른쪽으로:");

        for (int i = 0; i < elements.Count; i++)
        {
            ElementData data = elements[i];

            int column = i % Columns;
            int row = i / Columns;
            order.AppendLine($"  {row + 1}줄 {column + 1}칸 : {data.name} ({data.ElementName}) " +
                             $"모델 {(data.WorldModel != null ? AssetDatabase.GetAssetPath(data.WorldModel) : "없음")}");

            Texture2D shot = ShootOne(data, Tile);

            // Texture2D의 (0,0)은 왼쪽 아래다. 표는 왼쪽 위부터 채워 넣는다.
            int destinationY = (rows - 1 - row) * Tile;
            sheet.SetPixels(column * Tile, destinationY, Tile, Tile, shot.GetPixels());
            Object.DestroyImmediate(shot);
        }

        sheet.Apply();

        string directory = Path.Combine(Path.GetTempPath(), "MolraElementShot");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "elements.png");
        File.WriteAllBytes(path, sheet.EncodeToPNG());
        Object.DestroyImmediate(sheet);

        Debug.Log(order.ToString());
        Debug.Log($"[원소 그림] 저장했습니다: {path}");
    }

    // 대조표 말고 원소마다 한 장씩 따로 저장한다. 하나하나 크게 들여다볼 때 쓴다.
    //
    //   Unity.exe -batchmode -projectPath . -quit -executeMethod ElementModelShot.ShootEach
    public static void ShootEach()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        {
            Debug.LogError("[원소 그림] 그래픽 장치가 없습니다. -nographics 없이 실행해 주세요.");
            return;
        }

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        List<ElementData> elements = LoadElements();

        GameObject lightObject = new GameObject("Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.3f;
        lightObject.transform.rotation = Quaternion.Euler(38f, -140f, 0f);

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.localScale = Vector3.one * 2f;
        floor.GetComponent<Renderer>().sharedMaterial = WorldVisual.CreateLit(new Color(0.5f, 0.52f, 0.55f));

        string directory = Path.Combine(Path.GetTempPath(), "MolraElementShot", "each");
        Directory.CreateDirectory(directory);

        System.Text.StringBuilder order = new System.Text.StringBuilder();
        order.AppendLine("[원소 그림] 원소별 낱장:");

        for (int i = 0; i < elements.Count; i++)
        {
            ElementData data = elements[i];
            Texture2D shot = ShootOne(data, EachTile);
            string path = Path.Combine(directory, $"{i + 1:00}_{data.name}.png");
            File.WriteAllBytes(path, shot.EncodeToPNG());
            Object.DestroyImmediate(shot);

            order.AppendLine($"  {path} : {data.name} ({data.ElementName}) " +
                             $"모델 {(data.WorldModel != null ? AssetDatabase.GetAssetPath(data.WorldModel) : "없음")}");
        }

        Debug.Log(order.ToString());
        Debug.Log($"[원소 그림] 낱장을 저장했습니다: {directory}");
    }

    private static List<ElementData> LoadElements()
    {
        List<ElementData> elements = new List<ElementData>();

        foreach (string guid in AssetDatabase.FindAssets("t:ElementData", new[] { ElementFolder }))
        {
            ElementData data = AssetDatabase.LoadAssetAtPath<ElementData>(AssetDatabase.GUIDToAssetPath(guid));
            if (data != null)
            {
                elements.Add(data);
            }
        }

        elements.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return elements;
    }

    // 맵에 놓았을 때와 똑같이 세운 뒤 비스듬히 내려다보며 한 장 찍는다.
    private static Texture2D ShootOne(ElementData data, int size)
    {
        GameObject holder = new GameObject($"Shot_{data.name}");
        Transform model = ElementVisual.CreateWorldModel(data, holder.transform, Vector3.zero, Diameter);

        if (model == null)
        {
            // 모델이 없는 원소는 실제로도 색깔 구가 놓인다. 그 구를 그대로 세운다.
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.SetParent(holder.transform, false);
            sphere.transform.localPosition = Vector3.up * (Diameter * 0.5f);
            sphere.transform.localScale = Vector3.one * Diameter;
            sphere.GetComponent<Renderer>().sharedMaterial = WorldVisual.CreateLit(ElementVisual.GetColor(data));
        }

        GameObject cameraObject = new GameObject("__ElementShotCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = Diameter * 0.85f;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 50f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.14f, 0.15f, 0.18f);

        // 비스듬히 내려다본다. 정면에서만 보면 납작한 판이 판인지 알 수 없다.
        Vector3 focus = Vector3.up * (Diameter * 0.5f);
        Quaternion angle = Quaternion.Euler(24f, 35f, 0f);
        cameraObject.transform.position = focus - angle * Vector3.forward * 6f;
        cameraObject.transform.rotation = angle;

        RenderTexture target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
        Texture2D shot = new Texture2D(size, size, TextureFormat.RGB24, false);

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
            shot.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            shot.Apply();
            RenderTexture.active = previous;
        }
        finally
        {
            camera.targetTexture = null;
            Object.DestroyImmediate(cameraObject);
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(holder);
        }

        return shot;
    }
}
