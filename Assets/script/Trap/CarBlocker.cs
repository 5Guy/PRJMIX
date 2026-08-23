using UnityEngine;

// 달려오는 자동차를 막는 장애물(돌 벽)에 붙이는 표식.
// PlacementSystem이 돌 벽을 만들 때 자동으로 붙여 주고,
// 맵에 미리 놓아 둔 벽에도 직접 붙여서 쓸 수 있다.
//
// 막혔을 때 무슨 일이 일어나는지는 벽마다 다르다. 흙 벽은 차를 그냥 세우고,
// 흑요석 벽(ObsidianWall)은 차를 터뜨린다. 자동차는 어느 벽에 부딪혔는지 따지지 않고
// 이 OnCarBlocked만 부르므로, 새 벽을 만들 때는 여기만 물려받으면 된다.
public class CarBlocker : MonoBehaviour
{
    public virtual void OnCarBlocked(ChargingCar car)
    {
        if (car != null)
        {
            car.StopBlocked();
        }
    }
}
