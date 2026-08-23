using UnityEngine;

// 흑요석 원소를 도로에 놓으면 세워지는 검은 벽.
//
// 흙 벽은 차를 그냥 세우지만, 흑요석은 유리처럼 단단하고 날카로워서
// 부딪힌 차가 그 자리에서 터진다. 어느 쪽이든 차가 멈추므로 플레이어는 죽지 않는다.
public class ObsidianWall : CarBlocker
{
    public override void OnCarBlocked(ChargingCar car)
    {
        if (car != null)
        {
            car.Explode();
        }
    }
}
