using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// 이름과 안 맞는 원소 모델을 직접 만들어 끼워 준다.
//
// 왜 필요한가
//   원소마다 에셋 팩에서 아무 프리팹이나 골라 물려 둔 탓에, 맵에 놓으면 이름과 딴판인 것이
//   서 있었다. 물은 파란 네모 타일, 흙은 새까만 정육면체, 철은 보라색 결정이 박힌 돌이었다.
//   에셋 팩에는 대신 쓸 만한 것이 없어서(불꽃도, 파도도, 쇳덩이도 없다) 저폴리로 직접 짓는다.
//
// 무엇을 건드리지 않는가
//   이미 이름대로 잘 생긴 원소는 손대지 않는다 — 나무다리, 칼, 나무, 숲, 풀숲의 나무,
//   구름, 시멘트 자루, 광석, 대장간, 바람·폭풍(연출 프리팹), 불(모닥불 프리팹).
//   아래 Recipes 에 적힌 원소만 새로 만든다.
//
// 어떻게 쓰나
//   Tools > Molra > 원소 모델 새로 만들기
//   Unity.exe -batchmode -quit -projectPath . -executeMethod ElementModelBuilder.BuildAll
public static class ElementModelBuilder
{
    private const string ElementFolder = "Assets/Data/Nomal";
    private const string OutputFolder = "Assets/Prefab/ElementModels";
    private const string MeshFolder = OutputFolder + "/Meshes";
    private const string MaterialFolder = OutputFolder + "/Materials";

    // 나무·숲이 함께 쓰는 나무 한 그루. 통나무 대신 이걸 심는다.
    private const string TreePrefab =
        "Assets/Pandazole_Ultimate_Pack/Pandazole Nature Environment Pack/Prefabs/Tree_01_Spring.prefab";

    // ───────────────────────────── 색 ─────────────────────────────

    private static readonly Color SoilBrown = new Color(0.42f, 0.29f, 0.17f);
    private static readonly Color SoilDark = new Color(0.29f, 0.19f, 0.11f);
    private static readonly Color WaterBlue = new Color(0.16f, 0.52f, 0.88f);
    private static readonly Color WaterLight = new Color(0.42f, 0.74f, 0.96f);
    private static readonly Color PuddleBlue = new Color(0.62f, 0.82f, 0.92f);
    private static readonly Color PuddleRim = new Color(0.35f, 0.65f, 0.85f);
    private static readonly Color PuddleShine = new Color(0.93f, 0.98f, 1f);
    private static readonly Color MudBrown = new Color(0.35f, 0.24f, 0.13f);
    private static readonly Color MudLight = new Color(0.46f, 0.33f, 0.19f);
    private static readonly Color SwampGreen = new Color(0.22f, 0.34f, 0.19f);
    private static readonly Color SwampMoss = new Color(0.30f, 0.42f, 0.20f);
    private static readonly Color ReedGreen = new Color(0.36f, 0.52f, 0.24f);
    private static readonly Color RockDark = new Color(0.26f, 0.23f, 0.22f);
    private static readonly Color LavaGlow = new Color(1f, 0.34f, 0.05f);
    private static readonly Color IronGrey = new Color(0.55f, 0.56f, 0.60f);
    private static readonly Color SteelWhite = new Color(0.80f, 0.85f, 0.92f);
    private static readonly Color RustOrange = new Color(0.62f, 0.32f, 0.13f);
    private static readonly Color RustDark = new Color(0.40f, 0.20f, 0.09f);
    private static readonly Color ObsidianBlack = new Color(0.06f, 0.05f, 0.09f);
    private static readonly Color EmberRed = new Color(0.75f, 0.15f, 0.03f);
    private static readonly Color SteamWhite = new Color(0.90f, 0.94f, 0.98f);
    private static readonly Color AshGrey = new Color(0.60f, 0.59f, 0.57f);
    private static readonly Color CinderBlack = new Color(0.18f, 0.17f, 0.18f);
    private static readonly Color MountainGreen = new Color(0.24f, 0.42f, 0.23f);
    private static readonly Color MountainGreenLight = new Color(0.35f, 0.57f, 0.28f);
    private static readonly Color VolcanoBrown = new Color(0.45f, 0.28f, 0.17f);
    private static readonly Color VolcanoBrownDark = new Color(0.33f, 0.20f, 0.12f);
    private static readonly Color LavaDeep = new Color(0.78f, 0.16f, 0.02f);
    private static readonly Color OreRock = new Color(0.55f, 0.53f, 0.50f);
    private static readonly Color DiamondBlue = new Color(0.16f, 0.55f, 0.85f);
    private static readonly Color DiamondPale = new Color(0.36f, 0.76f, 0.96f);
    private static readonly Color GrassGreen = new Color(0.35f, 0.68f, 0.28f);
    private static readonly Color GrassDeep = new Color(0.25f, 0.53f, 0.21f);
    private static readonly Color WoodHandle = new Color(0.55f, 0.38f, 0.22f);

    private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
    private static readonly Dictionary<string, Mesh> SavedMeshes = new Dictionary<string, Mesh>();

    [MenuItem("Tools/Molra/원소 모델 새로 만들기")]
    public static void BuildAll()
    {
        Materials.Clear();
        SavedMeshes.Clear();

        // 지난번에 만든 것을 통째로 지우고 새로 짓는다.
        // 남겨 두면 "Element_Water 1.asset" 처럼 번호가 붙은 찌꺼기가 쌓인다.
        if (AssetDatabase.IsValidFolder(MeshFolder))
        {
            AssetDatabase.DeleteAsset(MeshFolder);
        }

        Directory.CreateDirectory(MeshFolder);
        Directory.CreateDirectory(MaterialFolder);
        AssetDatabase.Refresh();

        int built = 0;

        foreach (ModelRecipe recipe in Recipes())
        {
            GameObject model = recipe.Build();
            model.name = recipe.Name;

            StripColliders(model);

            string prefabPath = $"{OutputFolder}/{recipe.Name}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(model, prefabPath);
            Object.DestroyImmediate(model);

            if (prefab == null)
            {
                Debug.LogError($"[원소 모델] '{recipe.Name}' 프리팹을 저장하지 못했습니다.");
                continue;
            }

            if (Assign(recipe.Name, prefab, recipe.Scale))
            {
                built++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[원소 모델] {built}개를 새로 만들어 원소에 물렸습니다. ({OutputFolder})");
    }

    // 새로 만들 원소와 그 모양, 그리고 놓일 때의 크기 배율.
    //
    // 배율을 따로 두는 까닭: 모델은 모두 칸 하나에 맞춰 줄어들기 때문에, 그대로 두면
    // 산도 녹 조각도 화면에서 크기가 똑같다. 산은 우뚝해야 하고 녹은 자잘해야 한다.
    private static IEnumerable<ModelRecipe> Recipes()
    {
        yield return Recipe("Water", BuildWater);
        yield return Recipe("Earth", BuildEarth);
        yield return Recipe("Mud", BuildMud);
        yield return Recipe("Swamp", BuildSwamp);
        yield return Recipe("Tsunami", BuildTsunami);
        yield return Recipe("Lava", BuildLava);
        yield return Recipe("Steam", BuildSteam);
        yield return Recipe("Ash", BuildAsh);
        yield return Recipe("Iron", BuildIron);
        yield return Recipe("Steel", BuildSteel);
        yield return Recipe("Rust", BuildRust, 0.80f);
        yield return Recipe("Obsidian", BuildObsidian);
        yield return Recipe("Mountain", BuildMountain, 1.55f);
        yield return Recipe("Volcano", BuildVolcano);
        yield return Recipe("Grass", BuildGrass);
        yield return Recipe("Tool", BuildTool);
        yield return Recipe("Ore", BuildOre);
        yield return Recipe("Wood", BuildWood);
        yield return Recipe("Forest", BuildForest, 1.45f);
    }

    private static ModelRecipe Recipe(string name, System.Func<GameObject> build, float scale = 1f)
    {
        return new ModelRecipe(name, build, scale);
    }

    private readonly struct ModelRecipe
    {
        public readonly string Name;
        public readonly System.Func<GameObject> Build;
        public readonly float Scale;

        public ModelRecipe(string name, System.Func<GameObject> build, float scale)
        {
            Name = name;
            Build = build;
            Scale = scale;
        }
    }

    // 만든 프리팹을 ElementData의 "월드 모델" 칸에 물린다.
    private static bool Assign(string elementName, GameObject prefab, float scale)
    {
        string path = $"{ElementFolder}/{elementName}.asset";
        ElementData data = AssetDatabase.LoadAssetAtPath<ElementData>(path);

        if (data == null)
        {
            Debug.LogWarning($"[원소 모델] '{path}' 를 찾지 못해 '{elementName}' 모델을 물리지 못했습니다.");
            return false;
        }

        SerializedObject serialized = new SerializedObject(data);
        serialized.FindProperty("worldModel").objectReferenceValue = prefab;
        serialized.FindProperty("worldScale").floatValue = scale;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
        return true;
    }

    // ───────────────────────────── 물 ─────────────────────────────

    // 땅에 고인 물웅덩이.
    //
    // 원반을 겹쳐 쌓았을 때는 테두리가 정확한 원이라 위에서 보면 맨홀 뚜껑이었다.
    // 웅덩이로 보이려면 둘레가 물이 흐른 대로 삐뚤빼뚤해야 하고(Puddle 메시), 수면에
    // 빛이 번지는 흰 줄이 있어야 한다. 물결 고리는 오히려 동심원이라 뚜껑처럼 보였다 — 뺀다.
    //
    // 모델은 제일 긴 축이 칸 지름(0.85m)에 맞춰 줄어든다. 여기서는 폭이 제일 길므로
    // 폭이 0.85m, 높이는 5cm쯤 되는 납작한 웅덩이가 된다.
    private static GameObject BuildWater()
    {
        GameObject root = new GameObject("Water");

        // 물 밑에 깔리는 진한 테두리. 물보다 조금 넓고 확실히 낮아서 둘레로만 비어져 나온다.
        //
        // 높이를 물의 절반으로 낮춰야 한다. 조금만 낮추면 가장자리에서는 테두리가 물보다
        // 높아져(수면이 먼저 떨어진다) 웅덩이가 아니라 물이 담긴 분화구로 보인다.
        Puddle(root, "Rim", 0.545f, 0.040f, 0.62f, 0f, Vector3.zero, PuddleRim, 0.55f);

        // 고인 물
        Puddle(root, "Pool", 0.51f, 0.082f, 0.62f, 0f, Vector3.zero, PuddleBlue, 0.75f);

        // 수면에 번지는 빛. 두 덩이를 어긋나게 붙여 구부러진 줄로 읽히게 한다.
        Shine(root, "Shine1", new Vector3(-0.15f, 0.080f, 0.04f), new Vector3(0.27f, 0.014f, 0.070f), -22f);
        Shine(root, "Shine2", new Vector3(0.01f, 0.080f, 0.10f), new Vector3(0.17f, 0.014f, 0.062f), 32f);
        Shine(root, "Shine3", new Vector3(0.19f, 0.076f, -0.08f), new Vector3(0.19f, 0.014f, 0.056f), -20f);
        Shine(root, "Shine4", new Vector3(0.31f, 0.072f, -0.03f), new Vector3(0.11f, 0.014f, 0.046f), 28f);

        return root;
    }

    // 둘레가 삐뚤빼뚤한 납작한 물덩이.
    private static void Puddle(
        GameObject root, string name, float radius, float height, float squash, float turn,
        Vector3 position, Color color, float smoothness)
    {
        Mesh mesh = ElementMeshFactory.Puddle($"Element_{root.name}_{name}", radius, height, squash, turn, 30, 5);
        GameObject piece = Part(root, name, mesh, position, Vector3.zero, Vector3.one, color, smoothness);

        // 납작한 물 위에 물 자신의 그림자가 드리우면, 빛이 낮게 들 때 수면에 검은 얼룩(줄무늬)이
        // 생긴다. 웅덩이는 그림자를 만들지 않는다 — 바닥에 깔린 물이라 어차피 그림자가 없다.
        piece.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // 수면에 뜬 흰 빛 한 줄.
    private static void Shine(GameObject root, string name, Vector3 position, Vector3 scale, float yaw)
    {
        GameObject piece = Primitive(root, name, PrimitiveType.Sphere, position, new Vector3(0f, yaw, 0f), scale, PuddleShine, 0.85f, null);

        // 빛이 그림자를 드리우면 수면 가운데에 검은 얼룩이 생긴다. 빛나는 자리는 그림자를 만들지 않는다.
        piece.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // ───────────────────────────── 흙 ─────────────────────────────

    private static GameObject BuildEarth()
    {
        GameObject root = new GameObject("Earth");

        MoundPart(root, "Pile", 0.48f, 0.42f, 10, 3, 0.12f, 1001, SoilBrown);

        // 흙덩이 몇 개를 얹어 "퍼 놓은 흙"으로 읽히게 한다.
        Sphere(root, "Clod1", new Vector3(0.25f, 0.26f, 0.10f), Vector3.one * 0.17f, SoilDark, 0.05f);
        Sphere(root, "Clod2", new Vector3(-0.19f, 0.21f, -0.20f), Vector3.one * 0.13f, SoilDark, 0.05f);
        Sphere(root, "Clod3", new Vector3(0.05f, 0.42f, -0.06f), Vector3.one * 0.15f, SoilBrown, 0.05f);
        Sphere(root, "Pebble", new Vector3(-0.34f, 0.04f, 0.24f), Vector3.one * 0.09f, SoilDark, 0.05f);

        return root;
    }

    // ───────────────────────────── 진흙 ─────────────────────────────

    private static GameObject BuildMud()
    {
        GameObject root = new GameObject("Mud");

        // 사방으로 퍼진 자국
        Disc(root, "Splat", 0.56f, 0.03f, Vector3.zero, MudLight, 0.15f);

        MoundPart(root, "Pool", 0.46f, 0.24f, 10, 3, 0.18f, 2002, MudBrown, 0.5f);

        // 진흙이 끓어오르는 방울
        Sphere(root, "Bubble1", new Vector3(0.13f, 0.25f, 0.06f), Vector3.one * 0.19f, MudLight, 0.35f);
        Sphere(root, "Bubble2", new Vector3(-0.16f, 0.21f, -0.10f), Vector3.one * 0.13f, MudLight, 0.35f);
        Sphere(root, "Bubble3", new Vector3(0.02f, 0.19f, -0.20f), Vector3.one * 0.09f, MudBrown, 0.35f);

        return root;
    }

    // ───────────────────────────── 늪 ─────────────────────────────

    private static GameObject BuildSwamp()
    {
        GameObject root = new GameObject("Swamp");

        Disc(root, "Water", 0.52f, 0.06f, Vector3.zero, SwampGreen, 0.5f);
        MoundPart(root, "Muck", 0.32f, 0.17f, 9, 2, 0.20f, 3003, SwampMoss);

        // 물가에 선 갈대
        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f + 14f;
            Vector3 spot = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0f, 0.36f);
            float height = 0.42f + (i % 3) * 0.12f;

            BladePart(root, $"Reed{i}", 0.08f, height, 0.12f,
                spot + Vector3.up * 0.03f, angle + 30f, ReedGreen);
        }

        Sphere(root, "Bubble", new Vector3(0.18f, 0.09f, 0.12f), Vector3.one * 0.11f, SwampMoss, 0.4f);

        return root;
    }

    // ───────────────────────────── 쓰나미 ─────────────────────────────

    private static GameObject BuildTsunami()
    {
        GameObject root = new GameObject("Tsunami");

        Disc(root, "Sea", 0.55f, 0.05f, new Vector3(0f, 0f, -0.10f), WaterBlue, 0.7f);

        // Wave 메시는 +z 쪽으로 말린다. 그런데 게임 카메라는 -z 쪽에서 내려다보므로
        // 그대로 두면 말려 넘어가는 앞면이 아니라 밋밋한 등짝만 보인다. 통째로 돌려 세운다.
        //
        // 프리팹 뿌리를 돌려 두면 소용없다 — ElementVisual.CreateWorldModel 이 놓을 때
        // 뿌리의 회전을 지운다. 한 겹 안쪽에 돌린 것을 두어야 살아남는다.
        GameObject facing = new GameObject("Facing");
        facing.transform.SetParent(root.transform, false);
        facing.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        // 얇게 서서 앞으로 말려 넘어가는 물마루.
        // taper 를 주어 양 끝이 스러지게 한다. 폭이 일정하면 파도가 아니라 판때기다.
        Mesh wave = ElementMeshFactory.Wave("Element_TsunamiWave", 0.82f, 0.66f, 0.44f, 14, 0.50f);
        Part(facing, "Crest", wave, new Vector3(0f, 0.02f, -0.26f), Vector3.zero, Vector3.one, WaterBlue, 0.8f);

        // 마루 앞으로 밀려 나온 물살.
        // 예전에는 마루 바로 뒤(z -0.34)에 두었는데, 마루가 더 크고 앞에 있어 통째로 가려
        // 화면에 한 번도 나온 적이 없었다. 앞으로 빼야 파도가 두 겹으로 밀려오는 것이 보인다.
        Mesh swell = ElementMeshFactory.Wave("Element_TsunamiSwell", 0.66f, 0.17f, 0.10f, 10, 0.70f);
        Part(facing, "Swell", swell, new Vector3(0f, 0.01f, 0.26f), Vector3.zero, Vector3.one, WaterLight, 0.8f);

        // 말려 넘어가는 입술에서 부서지는 거품.
        //
        // 납작한 흰 타원 하나를 얹어 두었더니 거품이 아니라 물감을 칠한 자국으로 보였다.
        // 크기가 제각각인 덩어리를 입술을 따라 물려 놓아야 부서지는 것으로 읽힌다.
        // 마루 끝(y 0.68, z 0.18)에 걸쳐 놓는다. 면 한가운데에 두면 벽에 붙은 물방울로 보인다.
        Foam(facing, "Foam1", new Vector3(0.00f, 0.67f, 0.19f), 0.17f);
        Foam(facing, "Foam2", new Vector3(0.16f, 0.65f, 0.17f), 0.13f);
        Foam(facing, "Foam3", new Vector3(-0.17f, 0.64f, 0.16f), 0.12f);
        Foam(facing, "Foam4", new Vector3(0.28f, 0.57f, 0.13f), 0.09f);
        Foam(facing, "Foam5", new Vector3(-0.29f, 0.55f, 0.12f), 0.08f);

        // 입술에서 떨어져 내리는 물보라
        Foam(facing, "Spray1", new Vector3(0.07f, 0.50f, 0.25f), 0.07f);
        Foam(facing, "Spray2", new Vector3(-0.11f, 0.41f, 0.24f), 0.05f);

        return root;
    }

    // 부서지는 물거품 덩어리 하나. 조금 눌러 놓아야 공처럼 보이지 않는다.
    private static void Foam(GameObject root, string name, Vector3 position, float size)
    {
        Sphere(root, name, position, new Vector3(size, size * 0.72f, size * 0.86f), SteamWhite, 0.6f);
    }

    // ───────────────────────────── 용암 ─────────────────────────────

    private static GameObject BuildLava()
    {
        GameObject root = new GameObject("Lava");

        // 굳은 겉껍질이 웅덩이를 두르고, 그 안에서 시뻘건 것이 끓는다.
        MoundPart(root, "Crust", 0.50f, 0.26f, 10, 3, 0.18f, 4004, RockDark);
        Disc(root, "Molten", 0.38f, 0.07f, new Vector3(0f, 0.19f, 0f), LavaGlow, 0.3f, LavaGlow);

        Sphere(root, "Blob1", new Vector3(0.11f, 0.26f, 0.04f), Vector3.one * 0.17f, LavaGlow, 0.3f, LavaGlow);
        Sphere(root, "Blob2", new Vector3(-0.14f, 0.23f, -0.09f), Vector3.one * 0.12f, LavaGlow, 0.3f, LavaGlow);

        // 겉으로 흘러넘친 자국
        Box(root, "Spill", new Vector3(0.32f, 0.06f, 0.24f), new Vector3(0.15f, 0.05f, 0.34f),
            new Vector3(0f, 34f, 0f), LavaGlow, 0.3f, LavaGlow);

        return root;
    }

    // ───────────────────────────── 불 ─────────────────────────────

    // ───────────────────────────── 수증기 ─────────────────────────────

    // 구름과 똑같은 모델을 쓰고 있어서 둘을 구별할 수 없었다.
    // 수증기는 "위로 피어오르는 것"이므로 세로로 길게, 위로 갈수록 옅고 작게 쌓는다.
    private static GameObject BuildSteam()
    {
        GameObject root = new GameObject("Steam");

        float[] heights = { 0.10f, 0.26f, 0.42f, 0.58f, 0.72f, 0.86f };
        float[] sizes = { 0.40f, 0.34f, 0.30f, 0.24f, 0.19f, 0.13f };
        float[] alphas = { 0.85f, 0.78f, 0.70f, 0.60f, 0.50f, 0.40f };

        for (int i = 0; i < heights.Length; i++)
        {
            float angle = i * 2.4f;
            Vector3 spot = new Vector3(Mathf.Cos(angle) * 0.09f, heights[i], Mathf.Sin(angle) * 0.09f);

            Color color = SteamWhite;
            color.a = alphas[i];

            Sphere(root, $"Puff{i}", spot, new Vector3(sizes[i], sizes[i] * 0.82f, sizes[i]), color, 0.2f);
        }

        // 김이 올라오는 자리
        Disc(root, "Source", 0.30f, 0.02f, Vector3.zero, new Color(SteamWhite.r, SteamWhite.g, SteamWhite.b, 0.5f), 0.2f);

        return root;
    }

    // ───────────────────────────── 재 ─────────────────────────────

    private static GameObject BuildAsh()
    {
        GameObject root = new GameObject("Ash");

        Disc(root, "Dust", 0.54f, 0.02f, Vector3.zero, AshGrey, 0.05f);
        MoundPart(root, "Pile", 0.48f, 0.30f, 10, 3, 0.16f, 5005, AshGrey);

        // 타다 남은 숯덩이
        Box(root, "Cinder1", new Vector3(0.18f, 0.22f, 0.11f), new Vector3(0.16f, 0.09f, 0.11f),
            new Vector3(12f, 28f, 8f), CinderBlack, 0.05f);
        Box(root, "Cinder2", new Vector3(-0.16f, 0.19f, -0.13f), new Vector3(0.13f, 0.08f, 0.10f),
            new Vector3(-8f, -40f, 14f), CinderBlack, 0.05f);

        // 아직 남은 불씨
        Sphere(root, "Ember1", new Vector3(0.02f, 0.28f, 0.05f), Vector3.one * 0.08f, EmberRed, 0.2f, EmberRed);
        Sphere(root, "Ember2", new Vector3(-0.13f, 0.23f, 0.10f), Vector3.one * 0.06f, EmberRed, 0.2f, EmberRed);

        return root;
    }

    // ───────────────────────────── 철 / 강철 ─────────────────────────────

    private static GameObject BuildIron()
    {
        GameObject root = new GameObject("Iron");

        // 아직 덜 다듬은 무쇠 덩이. 잘 벼려 낸 강철(반듯하게 쌓은 세 덩이)과 한눈에 갈린다.
        Mesh ingot = ElementMeshFactory.Ingot("Element_IronIngot", 0.70f, 0.36f, 0.26f, 0.70f);
        Part(root, "Ingot", ingot, Vector3.zero, new Vector3(0f, -8f, 0f), Vector3.one, IronGrey, 0.4f);

        Mesh small = ElementMeshFactory.Ingot("Element_IronIngotSmall", 0.46f, 0.26f, 0.20f, 0.70f);
        Part(root, "IngotTop", small, new Vector3(0.02f, 0.26f, 0.01f), new Vector3(0f, 24f, 0f), Vector3.one, IronGrey, 0.4f);

        // 떨어져 나온 쇳조각
        Box(root, "Scrap", new Vector3(-0.34f, 0.05f, 0.26f), new Vector3(0.18f, 0.10f, 0.13f),
            new Vector3(0f, 26f, 6f), IronGrey, 0.4f);

        return root;
    }

    private static GameObject BuildSteel()
    {
        GameObject root = new GameObject("Steel");

        Mesh ingot = ElementMeshFactory.Ingot("Element_SteelIngot", 0.62f, 0.30f, 0.17f, 0.76f);

        // 잘 벼려 낸 강철은 반듯하게 쌓아 둔다. 철(거친 덩이)과 한눈에 갈린다.
        Part(root, "Bottom1", ingot, new Vector3(0f, 0f, -0.17f), Vector3.zero, Vector3.one, SteelWhite, 0.82f);
        Part(root, "Bottom2", ingot, new Vector3(0f, 0f, 0.17f), Vector3.zero, Vector3.one, SteelWhite, 0.82f);
        Part(root, "Top", ingot, new Vector3(0f, 0.17f, 0f), Vector3.zero, Vector3.one, SteelWhite, 0.82f);

        return root;
    }

    private static GameObject BuildRust()
    {
        GameObject root = new GameObject("Rust");

        // 삭아서 부풀어 오른 쇳덩이
        MoundPart(root, "Corroded", 0.44f, 0.42f, 9, 3, 0.22f, 6006, RustOrange);

        // 껍질처럼 일어난 자리
        Box(root, "Flake1", new Vector3(0.23f, 0.24f, 0.11f), new Vector3(0.22f, 0.03f, 0.18f),
            new Vector3(24f, 20f, 10f), RustDark, 0.15f);
        Box(root, "Flake2", new Vector3(-0.20f, 0.19f, -0.15f), new Vector3(0.18f, 0.03f, 0.15f),
            new Vector3(-18f, -34f, -12f), RustDark, 0.15f);

        // 아직 삭지 않은 쇠가 살짝 드러나 있다
        Box(root, "Metal", new Vector3(0f, 0.42f, 0f), new Vector3(0.20f, 0.06f, 0.14f),
            new Vector3(6f, 14f, -4f), IronGrey, 0.5f);

        return root;
    }

    // ───────────────────────────── 흑요석 ─────────────────────────────

    private static GameObject BuildObsidian()
    {
        GameObject root = new GameObject("Obsidian");

        MoundPart(root, "Base", 0.36f, 0.10f, 8, 1, 0.16f, 7007, ObsidianBlack);

        Crystal(root, "Shard1", 0.17f, 0.12f, 0.52f, new Vector3(0f, 0.04f, 0f), new Vector3(4f, 12f, -6f), 0.9f);
        Crystal(root, "Shard2", 0.12f, 0.09f, 0.34f, new Vector3(0.22f, 0.03f, 0.09f), new Vector3(-8f, 40f, 18f), 0.9f);
        Crystal(root, "Shard3", 0.09f, 0.07f, 0.24f, new Vector3(-0.18f, 0.03f, -0.14f), new Vector3(10f, -30f, -16f), 0.9f);

        return root;
    }

    private static void Crystal(GameObject root, string name, float radius, float bottom, float top, Vector3 position, Vector3 euler, float smoothness)
    {
        Mesh mesh = ElementMeshFactory.Crystal($"Element_{root.name}_{name}", radius, bottom, top, 6);
        Part(root, name, mesh, position, euler, Vector3.one, ObsidianBlack, smoothness);
    }

    // ───────────────────────────── 산 / 화산 ─────────────────────────────

    private static GameObject BuildMountain()
    {
        GameObject root = new GameObject("Mountain");

        // 만년설이 덮인 회색 봉우리가 아니라 나무가 우거진 초록 산.
        // 위쪽을 조금 밝은 초록으로 덮어 두어야 덩어리 하나로 뭉개지지 않고 능선이 보인다.
        Cone(root, "Peak", 0.50f, 0.03f, 0.90f, 7, Vector3.zero, MountainGreen, 0.06f);
        Cone(root, "Ridge", 0.26f, 0.02f, 0.44f, 7, new Vector3(0f, 0.47f, 0f), MountainGreenLight, 0.06f);

        // 옆에 붙은 작은 봉우리 — 하나만 있으면 고깔모자처럼 보인다.
        Cone(root, "SidePeak", 0.26f, 0.02f, 0.48f, 6, new Vector3(0.34f, 0f, 0.16f), MountainGreen, 0.06f);
        Cone(root, "SideRidge", 0.14f, 0.01f, 0.24f, 6, new Vector3(0.34f, 0.26f, 0.16f), MountainGreenLight, 0.06f);

        return root;
    }

    private static GameObject BuildVolcano()
    {
        GameObject root = new GameObject("Volcano");

        // 꼭대기가 뚫린 원뿔 = 분화구. 새까맣던 것을 진한 갈색으로 바꾼다.
        Cone(root, "Cone", 0.50f, 0.20f, 0.62f, 9, Vector3.zero, VolcanoBrown, 0.08f, null, false);
        Cone(root, "Skirt", 0.56f, 0.50f, 0.09f, 9, Vector3.zero, VolcanoBrownDark, 0.08f);
        Disc(root, "Crater", 0.165f, 0.025f, new Vector3(0f, 0.545f, 0f), LavaGlow, 0.3f, LavaGlow);

        // 분화구 턱을 넘어 비탈을 타고 흘러내리는 용암.
        LavaFlow(root, "Flow1", 215f, 11);
        LavaFlow(root, "Flow2", 335f, 10);
        LavaFlow(root, "Flow3", 95f, 9);

        // 분화구 언저리에 튄 용암. 예전에는 공중에 띄워 두어 막대사탕처럼 보였다.
        Sphere(root, "Splash1", new Vector3(0.10f, 0.58f, 0.04f), Vector3.one * 0.075f, LavaGlow, 0.3f, LavaGlow);
        Sphere(root, "Splash2", new Vector3(-0.09f, 0.56f, 0.09f), Vector3.one * 0.055f, LavaGlow, 0.3f, LavaGlow);

        return root;
    }

    // 분화구에서 밑동까지 비탈을 타고 흘러내리는 용암 한 줄기.
    //
    // 예전에는 길쭉한 상자 하나를 비탈에 눕혀 두었다. 상자는 모서리가 곧아서
    // 산에 붙여 놓아도 흘러내리는 것이 아니라 꽂아 둔 막대기로 보였다.
    // 작은 덩어리를 비탈을 따라 겹치도록 촘촘히 놓으면 줄기가 이어져 흐르는 것으로 읽힌다.
    // 아래로 내려갈수록 넓게 퍼지고, 식어서 색이 어두워진다.
    private static void LavaFlow(GameObject root, string name, float yaw, int drops)
    {
        const float BottomRadius = 0.50f;
        const float TopRadius = 0.20f;
        const float Height = 0.62f;

        // 비탈면의 기울기와 길이. 위로 갈수록 안쪽으로 들어오므로 그만큼 눕혀야 면에 붙는다.
        float slope = Mathf.Atan2(BottomRadius - TopRadius, Height) * Mathf.Rad2Deg;
        float length = Mathf.Sqrt(Height * Height + (BottomRadius - TopRadius) * (BottomRadius - TopRadius));

        // 한 토막의 길이. 칸 수보다 넉넉히 잡아 앞뒤가 겹치게 한다. 겹치지 않으면 구슬을 꿴 것처럼 보인다.
        float segment = length / drops * 3f;

        for (int i = 0; i < drops; i++)
        {
            // 0이 분화구 턱, 1이 밑동.
            //
            // 토막이 간격보다 길기 때문에 맨 위와 맨 아래는 안쪽으로 물려 놓아야 한다.
            // 끝까지 채우면 위로는 분화구 위로 뿔처럼 삐져나오고 아래로는 땅을 뚫는다.
            float t = Mathf.Lerp(0.17f, 0.94f, i / (float)(drops - 1));

            float y = Mathf.Lerp(Height - 0.03f, 0.02f, t);
            float radius = Mathf.Lerp(TopRadius, BottomRadius, t);

            // 비탈을 따라 조금씩 휘어 내려간다. 곧게 떨어지면 다시 막대기가 된다.
            float turn = yaw + Mathf.Sin(t * 3.4f) * 3f;

            // radius 는 원뿔 꼭짓점까지의 거리라 면 한가운데보다 바깥이다.
            // 그대로 쓰면 줄기가 비탈에서 떠서 산 언저리에 구슬을 붙여 놓은 꼴이 된다.
            // 면까지 끌어당긴 뒤 반쯤 파묻어, 비탈에 팬 골을 타고 흐르는 것처럼 만든다.
            Vector3 spot = Quaternion.Euler(0f, turn, 0f) * new Vector3(0f, y, radius * 0.90f);

            // 아래로 갈수록 넓게 퍼지고, 식어서 어두워진다.
            float width = Mathf.Lerp(0.07f, 0.17f, t);
            Color glow = Color.Lerp(LavaGlow, LavaDeep, t);

            // 유니티의 오일러각은 Y를 마지막에 돌린다. (slope, turn, 0)이면 비탈로 눕힌 뒤 옆으로 돌리는 셈이다.
            // 늘이는 축은 로컬 Y라서, 눕히고 나면 그대로 비탈을 타고 내려가는 방향이 된다.
            Primitive(root, $"{name}_{i}", PrimitiveType.Sphere, spot,
                new Vector3(slope, turn, 0f), new Vector3(width, segment, 0.045f), glow, 0.3f, glow);
        }
    }

    // ───────────────────────────── 광석 ─────────────────────────────

    // 바위에 박힌 다이아몬드.
    //
    // 에셋 팩의 Jem_01 을 물려 두었더니 납작하게 누운 노란 판이라, 광석이 아니라
    // 버터 한 조각처럼 보였다. 돌덩이를 깔고 그 위에 보석을 세워 둔다.
    private static GameObject BuildOre()
    {
        GameObject root = new GameObject("Ore");

        MoundPart(root, "Rock", 0.40f, 0.24f, 8, 2, 0.20f, 7007, OreRock, 0.12f);
        Sphere(root, "Chip", new Vector3(-0.28f, 0.07f, 0.17f), Vector3.one * 0.14f, OreRock, 0.12f);
        Sphere(root, "Chip2", new Vector3(0.26f, 0.05f, -0.20f), Vector3.one * 0.11f, OreRock, 0.12f);

        Diamond(root, "Gem", 0.19f, new Vector3(0.02f, 0.33f, -0.01f));

        return root;
    }

    // 브릴리언트 컷 보석 하나.
    // 허리(girdle)를 기준으로 위는 상판까지 좁아지는 낮은 사다리꼴, 아래는 한 점으로 모이는 뿔이다.
    private static void Diamond(GameObject root, string name, float radius, Vector3 girdle)
    {
        // 아래쪽 뿔. 원뿔은 위로 뻗으므로 통째로 뒤집어 끝이 땅을 향하게 한다.
        Mesh pavilion = ElementMeshFactory.Cone($"Element_{root.name}_{name}Pavilion", radius, radius * 0.06f, radius * 1.2f, 8);
        Part(root, $"{name}Pavilion", pavilion, girdle, new Vector3(180f, 0f, 0f), Vector3.one, DiamondBlue, 0.9f);

        // 위쪽 크라운.
        Mesh crown = ElementMeshFactory.Cone($"Element_{root.name}_{name}Crown", radius, radius * 0.55f, radius * 0.42f, 8);
        Part(root, $"{name}Crown", crown, girdle, Vector3.zero, Vector3.one, DiamondPale, 0.95f);
    }

    // ───────────────────────────── 나무 / 숲 ─────────────────────────────

    // 나무 한 그루. 통나무 대신 숲에 선 것과 같은 나무를 세운다.
    private static GameObject BuildWood()
    {
        GameObject root = new GameObject("Wood");
        PlantTree(root, "Tree", Vector3.zero, 0f, 1f);
        return root;
    }

    // 같은 나무를 여러 그루 심어 빽빽한 숲으로 만든다.
    //
    // 간격은 나무 한 그루의 실제 폭을 재서 잡는다. 에셋마다 미터 단위가 제각각이라
    // 0.3 같은 숫자를 그대로 쓰면 한 점에 겹치거나 저 멀리 흩어진다.
    private static GameObject BuildForest()
    {
        GameObject root = new GameObject("Forest");

        GameObject first = PlantTree(root, "Tree1", Vector3.zero, 0f, 1f);

        if (first == null)
        {
            return root;
        }

        float step = ElementVisual.MeasureBounds(first).size.x;

        // 나무 폭의 배수로 적어 둔 자리. 1보다 작게 두어 가지가 서로 겹치게 한다.
        (Vector3 spot, float yaw, float scale)[] others =
        {
            (new Vector3(0.62f, 0f, 0.34f), 60f, 0.80f),
            (new Vector3(-0.58f, 0f, 0.30f), 145f, 0.86f),
            (new Vector3(0.30f, 0f, -0.58f), 220f, 0.76f),
            (new Vector3(-0.34f, 0f, -0.54f), 300f, 0.90f),
            (new Vector3(0.06f, 0f, 0.72f), 25f, 0.68f),
            (new Vector3(-0.74f, 0f, -0.10f), 190f, 0.72f),
        };

        for (int i = 0; i < others.Length; i++)
        {
            PlantTree(root, $"Tree{i + 2}", others[i].spot * step, others[i].yaw, others[i].scale);
        }

        return root;
    }

    // 에셋 팩의 나무를 프리팹 연결 없이 통째로 복제해 심는다.
    // 프리팹 인스턴스로 넣으면 콜라이더를 떼어 낸 것이 덮어쓰기로 남아 지저분해진다.
    private static GameObject PlantTree(GameObject root, string name, Vector3 position, float yaw, float scale)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(TreePrefab);

        if (source == null)
        {
            Debug.LogError($"[원소 모델] 나무 프리팹을 찾지 못했습니다: {TreePrefab}");
            return null;
        }

        GameObject tree = (GameObject)Object.Instantiate(source);
        tree.name = name;
        tree.transform.SetParent(root.transform, false);
        tree.transform.localPosition = position;
        tree.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        tree.transform.localScale = Vector3.one * scale;
        return tree;
    }

    // ───────────────────────────── 풀 ─────────────────────────────

    private static GameObject BuildGrass()
    {
        GameObject root = new GameObject("Grass");

        MoundPart(root, "Sod", 0.32f, 0.07f, 9, 1, 0.10f, 8008, SoilDark);

        // 사방으로 뻗은 풀잎 한 무더기
        for (int i = 0; i < 11; i++)
        {
            float angle = i * 137f;
            float reach = 0.06f + (i % 4) * 0.05f;
            Vector3 spot = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0.05f, reach);
            float height = 0.34f + (i % 5) * 0.09f;

            BladePart(root, $"Blade{i}", 0.09f, height, 0.16f, spot, angle, i % 2 == 0 ? GrassGreen : GrassDeep);
        }

        return root;
    }

    // ───────────────────────────── 도구 ─────────────────────────────

    // 나뭇가지 하나가 "도구"로 놓여 있었다. 한눈에 연장으로 읽히는 망치로 바꾼다.
    private static GameObject BuildTool()
    {
        GameObject root = new GameObject("Tool");

        GameObject hammer = new GameObject("Hammer");
        hammer.transform.SetParent(root.transform, false);
        hammer.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        hammer.transform.localRotation = Quaternion.Euler(0f, 24f, 16f);

        // 자루
        Cylinder(hammer, "Handle", new Vector3(0f, 0.30f, 0f), new Vector3(0.075f, 0.30f, 0.075f), WoodHandle, 0.15f);
        Sphere(hammer, "HandleEnd", new Vector3(0f, 0.02f, 0f), Vector3.one * 0.10f, WoodHandle, 0.15f);

        // 머리
        Box(hammer, "Head", new Vector3(0f, 0.62f, 0f), new Vector3(0.42f, 0.15f, 0.16f), Vector3.zero, IronGrey, 0.55f);
        Box(hammer, "Face", new Vector3(0.24f, 0.62f, 0f), new Vector3(0.10f, 0.19f, 0.19f), Vector3.zero, IronGrey, 0.55f);

        // 반대쪽 노루발
        Box(hammer, "Claw", new Vector3(-0.26f, 0.64f, 0f), new Vector3(0.16f, 0.07f, 0.13f),
            new Vector3(0f, 0f, -22f), IronGrey, 0.55f);

        // 바닥에 눕혀 둔 못 두 개 — 망치 혼자면 크기를 가늠하기 어렵다.
        Box(root, "Nail1", new Vector3(0.26f, 0.02f, 0.22f), new Vector3(0.22f, 0.03f, 0.03f),
            new Vector3(0f, 18f, 0f), IronGrey, 0.6f);
        Box(root, "Nail2", new Vector3(0.20f, 0.02f, 0.30f), new Vector3(0.18f, 0.025f, 0.025f),
            new Vector3(0f, -14f, 0f), IronGrey, 0.6f);

        return root;
    }

    // ───────────────────────── 조각을 붙이는 도구들 ─────────────────────────

    private static void MoundPart(
        GameObject root, string name, float radius, float height, int sides, int rings, float roughness, int seed,
        Color color, float smoothness = 0.08f)
    {
        Mesh mesh = ElementMeshFactory.Mound($"Element_{root.name}_{name}", radius, height, sides, rings, roughness, seed);
        Part(root, name, mesh, Vector3.zero, Vector3.zero, Vector3.one, color, smoothness);
    }

    private static void BladePart(
        GameObject root, string name, float width, float height, float bend, Vector3 position, float yaw, Color color)
    {
        Mesh mesh = ElementMeshFactory.Blade($"Element_{root.name}_{name}", width, height, bend, 4);
        Part(root, name, mesh, position, new Vector3(0f, yaw, 0f), Vector3.one, color, 0.15f);
    }

    // 밑동이 둥글고 위가 뾰족한 물방울(불꽃·물방울).
    private static void Teardrop(
        GameObject root, string name, float radius, float height, float sway,
        Vector3 position, Color color, float smoothness, Color? emission = null)
    {
        Mesh mesh = ElementMeshFactory.Teardrop($"Element_{root.name}_{name}", radius, height, 10, 6, sway);
        Part(root, name, mesh, position, Vector3.zero, Vector3.one, color, smoothness, emission);
    }

    private static void Cone(
        GameObject root, string name, float bottomRadius, float topRadius, float height, int sides,
        Vector3 position, Color color, float smoothness, Color? emission = null, bool capTop = true)
    {
        Mesh mesh = ElementMeshFactory.Cone($"Element_{root.name}_{name}", bottomRadius, topRadius, height, sides, capTop);
        Part(root, name, mesh, position, Vector3.zero, Vector3.one, color, smoothness, emission);
    }

    // 납작한 원반. 웅덩이나 그을린 자국처럼 바닥에 깔리는 것에 쓴다.
    private static void Disc(
        GameObject root, string name, float radius, float height, Vector3 position,
        Color color, float smoothness, Color? emission = null)
    {
        Mesh mesh = ElementMeshFactory.Cone($"Element_{root.name}_{name}", radius, radius * 0.94f, height, 14);
        Part(root, name, mesh, position, Vector3.zero, Vector3.one, color, smoothness, emission);
    }

    private static void Sphere(
        GameObject root, string name, Vector3 position, Vector3 scale, Color color, float smoothness, Color? emission = null)
    {
        Primitive(root, name, PrimitiveType.Sphere, position, Vector3.zero, scale, color, smoothness, emission);
    }

    private static void Box(
        GameObject root, string name, Vector3 position, Vector3 scale, Vector3 euler, Color color, float smoothness, Color? emission = null)
    {
        Primitive(root, name, PrimitiveType.Cube, position, euler, scale, color, smoothness, emission);
    }

    // 원기둥 프리미티브는 세로로 2칸짜리다. scale.y 에 "반높이"를 넣어야 한다.
    private static void Cylinder(
        GameObject root, string name, Vector3 position, Vector3 scale, Color color, float smoothness)
    {
        Primitive(root, name, PrimitiveType.Cylinder, position, Vector3.zero, scale, color, smoothness, null);
    }

    private static GameObject Primitive(
        GameObject root, string name, PrimitiveType type,
        Vector3 position, Vector3 euler, Vector3 scale, Color color, float smoothness, Color? emission)
    {
        GameObject piece = GameObject.CreatePrimitive(type);
        piece.name = name;
        piece.transform.SetParent(root.transform, false);
        piece.transform.localPosition = position;
        piece.transform.localRotation = Quaternion.Euler(euler);
        piece.transform.localScale = scale;
        piece.GetComponent<Renderer>().sharedMaterial = GetMaterial(color, smoothness, emission);
        return piece;
    }

    private static GameObject Part(
        GameObject root, string name, Mesh mesh,
        Vector3 position, Vector3 euler, Vector3 scale, Color color, float smoothness, Color? emission = null)
    {
        GameObject piece = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        piece.transform.SetParent(root.transform, false);
        piece.transform.localPosition = position;
        piece.transform.localRotation = Quaternion.Euler(euler);
        piece.transform.localScale = scale;

        piece.GetComponent<MeshFilter>().sharedMesh = SaveMesh(mesh);
        piece.GetComponent<MeshRenderer>().sharedMaterial = GetMaterial(color, smoothness, emission);
        return piece;
    }

    // 직접 만든 메시는 프리팹 안에 저장되지 않는다. 에셋으로 따로 남겨야 다음에 열어도 남아 있다.
    private static Mesh SaveMesh(Mesh mesh)
    {
        // 같은 메시를 여러 조각이 나눠 쓰는 경우(강철 잉곳 세 개)가 있다.
        // 이미 저장한 것이면 그대로 다시 쓴다. 다시 저장하려 들면 실패한다.
        if (SavedMeshes.TryGetValue(mesh.name, out Mesh saved) && saved != null)
        {
            Object.DestroyImmediate(mesh);
            return saved;
        }

        string path = $"{MeshFolder}/{mesh.name}.asset";
        AssetDatabase.CreateAsset(mesh, path);

        Mesh asset = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        SavedMeshes[mesh.name] = asset;
        return asset;
    }

    // 같은 색은 재질 하나를 나눠 쓴다. 원소마다 새로 만들면 재질만 백 개가 넘는다.
    private static Material GetMaterial(Color color, float smoothness, Color? emission)
    {
        string key = $"{ColorUtility.ToHtmlStringRGBA(color)}_{smoothness:0.00}_{(emission.HasValue ? ColorUtility.ToHtmlStringRGB(emission.Value) : "none")}";

        if (Materials.TryGetValue(key, out Material cached) && cached != null)
        {
            return cached;
        }

        string path = $"{MaterialFolder}/Element_{key}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("_BaseColor", color);
        material.color = color;
        material.SetFloat("_Smoothness", smoothness);

        // 알파가 있는 색은 비쳐 보여야 한다(물·수증기). URP Lit은 기본이 불투명이라 직접 바꾼다.
        if (color.a < 0.999f)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_ZWrite", 0f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        if (emission.HasValue)
        {
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            material.SetColor("_EmissionColor", emission.Value * 0.7f);
        }

        EditorUtility.SetDirty(material);
        Materials[key] = material;
        return material;
    }

    // 프리미티브에 딸려 오는 콜라이더는 치운다.
    // 회수용 콜라이더는 놓을 때 ElementVisual이 딱 하나만 붙여 준다.
    private static void StripColliders(GameObject root)
    {
        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
        {
            Object.DestroyImmediate(collider);
        }
    }
}
