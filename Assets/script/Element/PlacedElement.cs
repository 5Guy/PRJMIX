using System.Collections.Generic;
using UnityEngine;

// 맵에 배치된 원소 하나를 나타내는 표식.
// PlacementSystem이 만드는 모든 결과물(그냥 놓은 원소, 흙 벽, 함정 위에 올린 상쇄 원소,
// 장애물에 붙인 속성)에 붙는다.
//
// 이게 붙어 있으면
//  - 클릭해서 조합창으로 회수할 수 있고,
//  - 죽어서 스테이지가 초기화될 때 한꺼번에 치울 수 있다.
public class PlacedElement : MonoBehaviour
{
    public enum Kind
    {
        Element,            // 도로 위에 그냥 놓은 원소
        Wall,               // 흙 원소로 세운 벽
        CounterMark,        // 함정 위에 올려 둔 상쇄 원소
        ObstacleAttribute,  // 장애물에 붙인 속성
    }

    private static readonly List<PlacedElement> all = new List<PlacedElement>();

    private ElementData data;
    private Kind kind;
    private IElementCounterTrap trap;     // CounterMark일 때
    private MonoBehaviour trapObject;     // 위와 같은 것. 파괴 여부는 Unity의 == 로만 알 수 있다
    private Obstacle obstacle;            // ObstacleAttribute일 때
    private GameObject visual;            // 이 오브젝트와 별개로 같이 치워야 하는 표시
    private Vector2Int cell;
    private bool hasCell;                 // Element/Wall처럼 빈 칸에 놓인 것만 칸을 기억한다

    public ElementData Data => data;
    public Kind PlacedKind => kind;

    // 지금 맵에 놓여 있는 것들. 순회 중 파괴되므로 복사본을 준다.
    public static List<PlacedElement> All => new List<PlacedElement>(all);

    // 정적 목록은 플레이 세션을 넘어 남으므로 매 실행마다 비운다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        all.Clear();
    }

    public static PlacedElement Attach(GameObject target, ElementData elementData, Kind placedKind)
    {
        PlacedElement placed = target.AddComponent<PlacedElement>();
        placed.data = elementData;
        placed.kind = placedKind;
        return placed;
    }

    public PlacedElement WithTrap(IElementCounterTrap counteredTrap)
    {
        trap = counteredTrap;
        trapObject = counteredTrap as MonoBehaviour;
        return this;
    }

    // 인터페이스 참조로는 "이미 파괴된 오브젝트"를 걸러낼 수 없어서(C# null이 아니다)
    // 같은 것을 가리키는 MonoBehaviour 쪽으로 확인한다.
    private IElementCounterTrap Trap => trapObject != null ? trap : null;

    public PlacedElement WithObstacle(Obstacle target)
    {
        obstacle = target;
        return this;
    }

    // 빈 칸에 놓은 원소/벽이 차지한 칸. 한 칸에 하나만 있도록 겹치는지 확인할 때 쓴다.
    public PlacedElement WithCell(Vector2Int gridCell)
    {
        cell = gridCell;
        hasCell = true;
        return this;
    }

    // 빈 칸에 이미 놓여 있는 원소/벽을 찾는다. 장애물 속성은 Obstacle.FindAt으로 따로 찾는다.
    public static PlacedElement FindAt(Vector2Int gridCell)
    {
        foreach (PlacedElement placed in all)
        {
            if (placed != null && placed.hasCell && placed.cell == gridCell)
            {
                return placed;
            }
        }

        return null;
    }

    // 한 칸에 놓인 것을 전부 찾는다.
    //
    // 원래는 한 칸에 하나뿐이지만, 개수를 채워 넣는 함정(진흙 2개처럼)은
    // 올려 둔 개수만큼 표시가 겹쳐 놓인다. 그 칸을 비울 때는 전부 걷어야 한다.
    public static List<PlacedElement> FindAllAt(Vector2Int gridCell)
    {
        List<PlacedElement> found = new List<PlacedElement>();

        foreach (PlacedElement placed in all)
        {
            if (placed != null && placed.hasCell && placed.cell == gridCell)
            {
                found.Add(placed);
            }
        }

        return found;
    }

    // 함정에 같은 원소를 하나 더 올려 개수를 채우는 중인지.
    // 이 경우에는 이미 올려 둔 표시를 물리지 않고 그대로 두어야 개수가 쌓인다.
    public bool AcceptsStackOf(ElementData newData)
    {
        if (kind != Kind.CounterMark || newData == null || data != newData)
        {
            return false;
        }

        IElementCounterTrap counteredTrap = Trap;
        return counteredTrap != null && counteredTrap.CanBeCounteredBy(newData);
    }

    // 이 배치와 운명을 같이하는 별도의 표시(예: 장애물 위에 띄운 아이콘).
    public PlacedElement WithVisual(GameObject extra)
    {
        visual = extra;
        return this;
    }

    // 같은 자리에 다른 원소를 덮어 놓았을 때. 밀려난 원소를 돌려준다.
    public ElementData Replace(ElementData newData)
    {
        ElementData previous = data;
        data = newData;
        return previous;
    }

    // 회수할 수 있는 상태인지. 이미 효과가 발동한 뒤(함정이 실제로 꺼진 뒤)에는 되돌릴 수 없다.
    public bool CanRetrieve()
    {
        if (kind == Kind.CounterMark)
        {
            // 개수를 다 채우지 못해 아직 발동 대기(IsArmed)가 아닌 것도 회수할 수 있어야 한다.
            // 되돌릴 수 없는 것은 함정이 실제로 꺼진 뒤뿐이다.
            IElementCounterTrap counteredTrap = Trap;
            return counteredTrap != null && !counteredTrap.IsCountered;
        }

        return true;
    }

    // 배치를 되돌리고 원소를 돌려준다. 돌려줄 원소가 없으면 null.
    public ElementData Retrieve()
    {
        ElementData returned = data;

        // 이미 꺼진 함정은 되살리지 않는다. 원소도 돌려주지 않는다.
        IElementCounterTrap counteredTrap = Trap;
        if (kind == Kind.CounterMark && counteredTrap != null && !counteredTrap.CancelCounter())
        {
            return null;
        }

        Remove();
        return returned;
    }

    // 오브젝트만 치운다(원소는 돌려주지 않는다).
    public void Remove()
    {
        if (visual != null)
        {
            Destroy(visual);
        }

        if (kind == Kind.ObstacleAttribute)
        {
            // 장애물 자체는 맵의 일부라 지우면 안 되고, 붙였던 속성과 표식만 뗀다.
            if (obstacle != null)
            {
                obstacle.Clear();
            }

            all.Remove(this);
            Destroy(this);
            return;
        }

        Destroy(gameObject);
    }

    private void OnEnable()
    {
        if (!all.Contains(this))
        {
            all.Add(this);
        }
    }

    private void OnDisable()
    {
        all.Remove(this);
    }

    private void OnDestroy()
    {
        all.Remove(this);
    }
}
