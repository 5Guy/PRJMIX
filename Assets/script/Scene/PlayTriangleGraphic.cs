using UnityEngine;
using UnityEngine.UI;

// 오른쪽을 가리키는 재생(▶) 삼각형. 스프라이트 없이 메시로 직접 그리므로
// 어떤 해상도에서도 가장자리가 깨지지 않는다.
// StageStartButton이 런타임에 붙여 쓴다.
public class PlayTriangleGraphic : MaskableGraphic
{
    // 삼각형은 가로로 조금 좁아야 재생 버튼처럼 보인다(정삼각형에 가깝게).
    [SerializeField] private float widthRatio = 0.86f;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = GetPixelAdjustedRect();
        float halfHeight = rect.height * 0.5f;
        float halfWidth = Mathf.Min(rect.width, rect.height * widthRatio) * 0.5f;
        float centerX = rect.center.x;
        float centerY = rect.center.y;

        // 삼각형은 무게중심이 왼쪽(-halfWidth/3)에 있어서 그대로 두면 왼쪽으로 쏠려 보인다.
        // 무게중심이 한가운데 오도록 밀어 준다.
        centerX += halfWidth / 3f;

        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;

        vertex.position = new Vector3(centerX - halfWidth, centerY + halfHeight);   // 왼쪽 위
        vh.AddVert(vertex);
        vertex.position = new Vector3(centerX - halfWidth, centerY - halfHeight);   // 왼쪽 아래
        vh.AddVert(vertex);
        vertex.position = new Vector3(centerX + halfWidth, centerY);                // 오른쪽 꼭짓점
        vh.AddVert(vertex);

        vh.AddTriangle(0, 1, 2);
    }
}
