using UnityEngine;

// 원소 아이콘용 임시 비주얼을 만들어 주는 헬퍼.
// 아직 Sprite가 없는 원소는 타입별 색깔 원으로 대신 표시한다.
public static class ElementVisual
{
    // 탑뷰에서 바닥에 겹쳐 눕는 납작한 표시들의 그리는 순서.
    //
    // 함정 표시와 그 위에 올려 둔 원소는 둘 다 바닥에 붙어 있어서 높이로는 앞뒤를 가릴 수 없다.
    // 정렬 순서를 확실히 갈라 두어야 함정 위에 원소를 놓았을 때 언제나 원소가 위로 올라온다.
    public const int TrapIconSortingOrder = 0;
    public const int PlacedIconSortingOrder = 100;

    private static Sprite circleSprite;
    private static Sprite ringSprite;

    public static Sprite Circle
    {
        get
        {
            if (circleSprite == null)
            {
                circleSprite = CreateCircle(128, 0f);
            }

            return circleSprite;
        }
    }

    // 물에 빠질 때 퍼지는 물결(도넛 모양)
    public static Sprite Ring
    {
        get
        {
            if (ringSprite == null)
            {
                ringSprite = CreateCircle(128, 0.86f);
            }

            return ringSprite;
        }
    }

    public static Color GetColor(ElementData data)
    {
        if (data == null)
        {
            return new Color(0.6f, 0.6f, 0.6f);
        }

        switch (data.ElementType)
        {
            case ElementType.Fire:
                return new Color(1f, 0.45f, 0.22f);
            case ElementType.Water:
                return new Color(0.28f, 0.66f, 1f);
            case ElementType.Wood:
                return new Color(0.42f, 0.85f, 0.38f);
            case ElementType.Earth:
                return new Color(0.58f, 0.42f, 0.88f);
            case ElementType.Iron:
                return new Color(0.78f, 0.81f, 0.88f);
            case ElementType.Lava:
                return new Color(0.80f, 0.12f, 0.02f);
            case ElementType.Tsunami:
                return new Color(0.12f, 0.45f, 0.82f);
            case ElementType.Cloud:
                return new Color(0.45f, 0.52f, 0.62f);
            case ElementType.Mud:
                return new Color(0.42f, 0.30f, 0.18f);
            case ElementType.Obsidian:
                return new Color(0.06f, 0.05f, 0.08f);
            case ElementType.Swamp:
                return new Color(0.24f, 0.31f, 0.18f);
            case ElementType.Wind:
                return new Color(0.72f, 0.86f, 0.92f);
        }

        return new Color(0.6f, 0.6f, 0.6f);
    }

    // 자식까지 포함한 렌더러 전체를 감싸는 상자(월드 기준).
    // 모델마다 크기도 피벗 위치도 제각각이라, 크기를 맞추거나 제자리에 놓을 때 기준으로 쓴다.
    public static Bounds MeasureBounds(GameObject target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length == 0)
        {
            return new Bounds(target.transform.position, Vector3.zero);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    // 원소에 물려 둔 3D 모델을 맵에 놓는다. 모델이 없으면 null(그때는 색깔 구를 쓴다).
    //
    // fbx는 제작할 때 쓴 단위에 따라 터무니없이 크거나 작게 들어오고 피벗도 제각각이라,
    // 그대로 놓으면 화면 밖에 있거나 점만 하게 보인다. 경계 상자를 재서 칸 크기에 맞추고
    // 바닥이 땅에 닿도록 내려놓는다.
    public static Transform CreateWorldModel(ElementData data, Transform parent, Vector3 groundPosition, float diameter)
    {
        if (data == null || data.WorldModel == null)
        {
            return null;
        }

        GameObject model = Object.Instantiate(data.WorldModel);
        model.name = $"Model_{data.ElementName}";
        model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        model.transform.localScale = Vector3.one;

        // 원점에 회전 없이 놓아 두었으므로 지금 잰 상자가 곧 모델 자체의 크기다.
        // 가로 폭만 보고 맞추면 칼처럼 가늘고 긴 모델이 터무니없이 커진다.
        // (세워 둔 칼은 x·z가 몇 cm뿐이라, 그 얇은 축을 칸 지름에 맞추면 몇 배로 부푼다)
        // 세 축 중 제일 긴 것을 기준으로 삼아 어떤 모델이든 칸 안에 들어오게 한다.
        Bounds bounds = MeasureBounds(model);
        float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        float fit = longest > 0.0001f ? diameter / longest : 1f;

        // 여기까지는 모든 모델이 칸 하나에 똑같이 맞춰진다. 그래서 산도 자갈도 크기가 같아 보인다.
        // 원소마다 정해 둔 배율을 한 번 더 먹여야 산은 우뚝하고 녹은 자잘하게 놓인다.
        fit *= data.WorldScale;

        model.transform.localScale = Vector3.one * fit;

        // 피벗이 어디에 박혀 있든 칸 한가운데 바닥에 바로 서도록 경계 기준으로 놓는다.
        Vector3 target = new Vector3(
            groundPosition.x,
            groundPosition.y + bounds.extents.y * fit,
            groundPosition.z);

        model.transform.position = target - bounds.center * fit;
        model.transform.SetParent(parent, true);

        EnsurePickCollider(model, bounds);
        return model.transform;
    }

    // 오른쪽 클릭으로 회수할 때 레이캐스트가 맞을 곳이 필요하다.
    // 기본 구에는 프리미티브 콜라이더가 딸려 오지만, fbx는 보통 콜라이더 없이 들어온다.
    private static void EnsurePickCollider(GameObject model, Bounds localBounds)
    {
        if (model.GetComponentInChildren<Collider>(true) != null)
        {
            return;
        }

        BoxCollider box = model.AddComponent<BoxCollider>();
        box.center = localBounds.center;
        box.size = localBounds.size;
    }

    // 바닥에 눕혀 놓는 원소 아이콘(탑뷰용)을 만든다. 커스텀 아이콘이 있으면 그걸, 없으면 원 스프라이트를 쓴다.
    public static Transform CreateFlatIcon(ElementData data, Transform parent, Vector3 worldPosition, float diameter)
    {
        GameObject iconObject = new GameObject("Icon", typeof(SpriteRenderer));
        iconObject.transform.SetParent(parent, true);
        iconObject.transform.position = worldPosition;
        iconObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        ApplyIconVisual(iconObject.GetComponent<SpriteRenderer>(), data, diameter);
        return iconObject.transform;
    }

    // 바닥에 눕혀 놓는 그림 하나를 만든다(함정의 탑뷰 표시처럼 원소와 상관없는 표시용).
    //
    // size는 미터로 재는 가로·세로다. 가로세로를 다르게 주면 길게 누운 함정 자리도 그대로 표현할 수 있다.
    // sortingOrder로 겹치는 순서를 정한다 — 배치한 원소가 늘 위에 오도록 함정 쪽을 낮게 준다.
    public static SpriteRenderer CreateFlatSprite(
        string name, Transform parent, Sprite sprite, Color color, Vector2 size, int sortingOrder)
    {
        GameObject flat = new GameObject(name, typeof(SpriteRenderer));
        flat.transform.SetParent(parent, false);

        SpriteRenderer renderer = flat.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;

        Material material = IconMaterial;
        if (material != null)
        {
            renderer.sharedMaterial = material;
        }

        Vector2 native = sprite.bounds.size;
        flat.transform.localScale = new Vector3(
            native.x > 0f ? size.x / native.x : 1f,
            native.y > 0f ? size.y / native.y : 1f,
            1f);

        return renderer;
    }

    // 이미 만들어 둔 아이콘의 원소/크기를 다시 지정한다(장애물처럼 같은 아이콘을 재사용할 때).
    public static void ApplyIconVisual(SpriteRenderer renderer, ElementData data, float diameter)
    {
        // 유니티가 자동으로 붙여주는 기본 재질은 렌더 파이프라인에 따라 투명한 부분 없이
        // 네모난 배경째로 나올 수 있으므로, 렌더 파이프라인에 맞는 재질을 직접 지정한다.
        // 재질을 못 만든 경우에는 손대지 않는다. 기본 스프라이트 재질로라도 보이는 편이 낫다.
        Material material = IconMaterial;
        if (material != null)
        {
            renderer.sharedMaterial = material;
        }

        Sprite sprite = data.Icon != null ? data.Icon : Circle;
        renderer.sprite = sprite;
        renderer.color = data.Icon != null ? Color.white : GetColor(data);

        // 함정 표시(TrapIconSortingOrder)보다 뒤에 그려야 함정 위에 올려 둔 원소가 가려지지 않는다.
        // 둘 다 바닥에 납작하게 붙어 있어서 높이 차이로는 앞뒤가 정해지지 않는다.
        renderer.sortingOrder = PlacedIconSortingOrder;

        float nativeSize = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        float scale = nativeSize > 0f ? diameter / nativeSize : 1f;
        renderer.transform.localScale = Vector3.one * scale;
    }

    private static Material iconMaterial;

    private static Material IconMaterial
    {
        get
        {
            if (iconMaterial == null)
            {
                Shader shader =
                    Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ??
                    Shader.Find("Sprites/Default");

                // 빌드에서는 씬의 어떤 재질도 쓰지 않는 셰이더가 통째로 빠진다(셰이더 스트리핑).
                // 그러면 Shader.Find가 null을 돌려주고, 그대로 재질을 만들면 아무것도 그려지지 않는다.
                if (shader == null)
                {
                    Debug.LogWarning(
                        "원소 아이콘 셰이더를 찾지 못했습니다. " +
                        "Project Settings > Graphics > Always Included Shaders에 " +
                        "'Universal Render Pipeline/2D/Sprite-Unlit-Default'를 넣어 주세요. " +
                        "지금은 기본 스프라이트 재질로 그립니다.");
                    return null;
                }

                iconMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }

            return iconMaterial;
        }
    }

    // innerRatio가 0이면 꽉 찬 원, 0보다 크면 가운데가 뚫린 링.
    private static Sprite CreateCircle(int size, float innerRatio)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            name = innerRatio > 0f ? "ElementRing" : "ElementCircle",
            hideFlags = HideFlags.HideAndDontSave
        };

        float outer = size * 0.5f - 1f;
        float inner = outer * innerRatio;
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);

                // 경계에서 1픽셀만 부드럽게 깎아 계단 현상을 줄인다.
                float alpha = Mathf.Clamp01(outer - distance);
                if (innerRatio > 0f)
                {
                    alpha = Mathf.Min(alpha, Mathf.Clamp01(distance - inner));
                }

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
