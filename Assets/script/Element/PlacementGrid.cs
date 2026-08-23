using UnityEngine;

// 도로를 X축 / Z축에 평행한 격자로 나눈다.
// 셀 좌표는 (x = X방향, y = Z방향), 원점은 이 오브젝트의 위치(격자의 한쪽 모서리)다.
public class PlacementGrid : MonoBehaviour
{
    [SerializeField] private float cellSize = 2f;
    [SerializeField] private int columns = 4;   // X 방향 칸 수
    [SerializeField] private int rows = 24;     // Z 방향 칸 수

    public float CellSize => cellSize;
    public int Columns => columns;
    public int Rows => rows;
    public float SurfaceY => transform.position.y;

    public void Configure(float size, int columnCount, int rowCount)
    {
        cellSize = size;
        columns = columnCount;
        rows = rowCount;
    }

    public Vector3 CellToWorld(Vector2Int cell)
    {
        Vector3 origin = transform.position;
        return new Vector3(
            origin.x + (cell.x + 0.5f) * cellSize,
            origin.y,
            origin.z + (cell.y + 0.5f) * cellSize);
    }

    public Vector2Int WorldToCell(Vector3 world)
    {
        Vector3 origin = transform.position;
        return new Vector2Int(
            Mathf.FloorToInt((world.x - origin.x) / cellSize),
            Mathf.FloorToInt((world.z - origin.z) / cellSize));
    }

    public bool Contains(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < columns && cell.y >= 0 && cell.y < rows;
    }

    // 마우스 위치를 도로 바닥으로 투영할 때 쓰는 평면.
    public Plane Surface => new Plane(Vector3.up, new Vector3(0f, SurfaceY, 0f));

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 1f, 0.4f);
        Vector3 origin = transform.position;

        for (int x = 0; x <= columns; x++)
        {
            Vector3 from = origin + new Vector3(x * cellSize, 0f, 0f);
            Gizmos.DrawLine(from, from + new Vector3(0f, 0f, rows * cellSize));
        }

        for (int z = 0; z <= rows; z++)
        {
            Vector3 from = origin + new Vector3(0f, 0f, z * cellSize);
            Gizmos.DrawLine(from, from + new Vector3(columns * cellSize, 0f, 0f));
        }
    }
}
