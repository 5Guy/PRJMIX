using UnityEngine;

// 런타임에 만드는 월드 오브젝트용 재질/도형 헬퍼.
public static class WorldVisual
{
    private static Shader litShader;
    private static Shader unlitShader;

    // URP 프로젝트라 Standard 대신 URP 셰이더를 찾는다.
    public static Material CreateLit(Color color)
    {
        if (litShader == null)
        {
            litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            if (litShader == null)
            {
                Debug.LogError(
                    "Lit 셰이더를 찾지 못했습니다. " +
                    "Project Settings > Graphics > Always Included Shaders에 " +
                    "'Universal Render Pipeline/Lit'을 넣어 주세요.");
            }
        }

        return CreateMaterial(litShader, color);
    }

    // 반투명한 재질(파도·비구름처럼 비쳐 보여야 하는 연출용).
    // URP Lit은 기본이 불투명이라 표면 종류를 Transparent로 직접 바꿔 줘야 한다.
    public static Material CreateTransparentLit(Color color)
    {
        Material material = CreateLit(color);

        material.SetFloat("_Surface", 1f);     // 0 = Opaque, 1 = Transparent
        material.SetFloat("_Blend", 0f);       // 0 = Alpha
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_AlphaClip", 0f);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        return material;
    }

    // 이미 만들어 둔 재질의 색만 바꾼다(진흙이 서서히 밝아지는 연출처럼 매 프레임 바꿀 때).
    public static void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        material.color = color;
    }

    public static Material CreateUnlit(Color color)
    {
        if (unlitShader == null)
        {
            unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        }

        // 빌드에서는 씬의 어떤 재질도 쓰지 않는 셰이더가 통째로 빠진다(셰이더 스트리핑).
        // 배치 칸 하이라이트가 통째로 안 보이는 것보다는 Lit으로라도 그리는 편이 낫다.
        if (unlitShader == null)
        {
            Debug.LogWarning(
                "Unlit 셰이더를 찾지 못했습니다. " +
                "Project Settings > Graphics > Always Included Shaders에 " +
                "'Universal Render Pipeline/Unlit'을 넣어 주세요. 지금은 Lit으로 대신 그립니다.");
            return color.a < 1f ? CreateTransparentLit(color) : CreateLit(color);
        }

        return CreateMaterial(unlitShader, color);
    }

    // 비쳐 보이는 Unlit 재질. 배치 칸 표시처럼 "지형 위에 옅게 덮는" 것에 쓴다.
    //
    // URP/Unlit도 기본은 불투명이라, 알파를 넣어도 그냥 진하게 칠해진다.
    // CreateTransparentLit과 같은 방식으로 표면 종류를 직접 Transparent로 바꿔 줘야 한다.
    public static Material CreateTransparentUnlit(Color color)
    {
        Material material = CreateUnlit(color);

        // CreateUnlit이 셰이더를 못 찾아 Lit으로 대신 만들었다면 이미 반투명 처리가 되어 있다.
        if (material.shader != unlitShader)
        {
            return material;
        }

        material.SetFloat("_Surface", 1f);     // 0 = Opaque, 1 = Transparent
        material.SetFloat("_Blend", 0f);       // 0 = Alpha
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_AlphaClip", 0f);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        return material;
    }

    private static Material CreateMaterial(Shader shader, Color color)
    {
        Material material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        material.color = color;
        return material;
    }

    public static GameObject CreateBox(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = position;
        box.transform.localScale = scale;
        box.GetComponent<Renderer>().sharedMaterial = material;
        return box;
    }

    // 물 웅덩이처럼 납작한 원반을 만들 때 쓴다. 원기둥 프리미티브는 세로 2칸짜리라
    // scale.y 1이면 높이 2가 된다(WaterTrapModel과 같은 규칙).
    public static GameObject CreateCylinder(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = name;
        cylinder.transform.SetParent(parent, false);
        cylinder.transform.localPosition = position;
        cylinder.transform.localScale = scale;
        cylinder.GetComponent<Renderer>().sharedMaterial = material;
        return cylinder;
    }

    public static void SetColor(Renderer renderer, Color color)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.sharedMaterial = CreateLit(color);
    }
}
