using System.Collections.Generic;
using UnityEngine;

// 도로 위 장애물. 격자 셀 하나를 차지하며, 조합으로 만든 속성(원소)을 배치받는다.
// 속성이 붙은 뒤에는 다른 배치 원소들처럼 탑뷰(2D)에서는 아이콘, 사선뷰(3D)에서는
// 색이 바뀐 장애물 모습으로 보인다.
public class Obstacle : MonoBehaviour
{
    [SerializeField] private float iconDiameterRatio = 0.7f;   // 장애물 크기 대비 아이콘 지름

    private static readonly List<Obstacle> all = new List<Obstacle>();

    [SerializeField] private Vector2Int cell;
    [SerializeField] private Renderer body;

    public Vector2Int Cell => cell;
    public ElementData AppliedElement { get; private set; }

    private Material originalMaterial;
    private bool originalCaptured;
    private Transform icon;

    public void Setup(Vector2Int gridCell, Renderer bodyRenderer)
    {
        cell = gridCell;
        body = bodyRenderer;
    }

    // 속성 배치. 어떤 원소가 붙었는지 보여 준다.
    public void Apply(ElementData data)
    {
        AppliedElement = data;

        if (body == null)
        {
            return;
        }

        // 회수했을 때 원래 색으로 돌아갈 수 있도록 처음 재질을 기억해 둔다.
        if (!originalCaptured)
        {
            originalMaterial = body.sharedMaterial;
            originalCaptured = true;
        }

        WorldVisual.SetColor(body, ElementVisual.GetColor(data));

        float diameter = body.transform.localScale.x * iconDiameterRatio;
        Vector3 iconPosition = body.transform.position + Vector3.up * (body.transform.localScale.y * 0.5f + 0.02f);

        if (icon == null)
        {
            icon = ElementVisual.CreateFlatIcon(data, transform.parent, iconPosition, diameter);
        }
        else
        {
            icon.position = iconPosition;
            ElementVisual.ApplyIconVisual(icon.GetComponent<SpriteRenderer>(), data, diameter);
        }

        ApplyViewMode(CameraViewController.IsTopView);
    }

    // 붙였던 속성을 떼고 원래 모습으로 되돌린다.
    public void Clear()
    {
        AppliedElement = null;

        if (body != null)
        {
            if (originalCaptured)
            {
                body.sharedMaterial = originalMaterial;
            }

            body.enabled = true;
        }

        if (icon != null)
        {
            Destroy(icon.gameObject);
            icon = null;
        }
    }

    public static Obstacle FindAt(Vector2Int cell)
    {
        foreach (Obstacle obstacle in all)
        {
            if (obstacle != null && obstacle.cell == cell)
            {
                return obstacle;
            }
        }

        return null;
    }

    private void OnEnable()
    {
        all.Add(this);
        CameraViewController.TopViewChanged += ApplyViewMode;
    }

    private void OnDisable()
    {
        all.Remove(this);
        CameraViewController.TopViewChanged -= ApplyViewMode;
    }

    // 속성이 붙어 있을 때만 탑뷰/사선뷰에 따라 아이콘 ↔ 장애물 모습을 바꾼다.
    // 속성이 없는 맨 장애물은 뷰와 무관하게 항상 그대로 보인다.
    private void ApplyViewMode(bool topView)
    {
        if (AppliedElement == null || icon == null || body == null)
        {
            return;
        }

        body.enabled = !topView;
        icon.gameObject.SetActive(topView);
    }
}
