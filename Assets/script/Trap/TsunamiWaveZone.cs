using UnityEngine;

// 파도에 휩쓸리는 판정.
//
// 파도는 밀려가는 덩어리라 물리로 밀어내면 몸이 끼거나 튕겨 나가 어색하다.
// 트리거로 닿은 것만 잡아내고, 파도가 가는 방향으로 플레이어를 직접 날려 보낸다.
//
// MonoBehaviour는 파일 이름과 클래스 이름이 같아야 AddComponent로 붙일 수 있어서 파일을 따로 둔다.
[RequireComponent(typeof(Collider))]
public class TsunamiWaveZone : MonoBehaviour
{
    private Vector3 flow = Vector3.forward;
    private float blowSpeed = 20f;
    private float blowLift = 8f;
    private bool killPlayer = true;
    private string deathAnimationTriggerName = "Die";

    // 한 번 휩쓸린 뒤에는 파도가 몸을 지나는 동안 계속 다시 맞지 않게 잠근다.
    private bool swept;

    public void Setup(Vector3 waveFlow, float speed, float lift, bool kill, string deathTrigger)
    {
        flow = waveFlow;
        blowSpeed = speed;
        blowLift = lift;
        killPlayer = kill;
        deathAnimationTriggerName = deathTrigger;
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleContact(other);
    }

    // 파도가 빠르게 지나가면 Enter를 놓칠 수 있다. 머무는 동안에도 확인한다.
    private void OnTriggerStay(Collider other)
    {
        HandleContact(other);
    }

    private void HandleContact(Collider other)
    {
        if (swept)
        {
            return;
        }

        Transform player = PlayerLocator.FindPlayerRoot(other.transform);
        if (player == null || PlayerLocator.IsAlreadyDead(player))
        {
            return;
        }

        if (killPlayer && !PlayerLocator.Kill(player, deathAnimationTriggerName, this))
        {
            return;
        }

        swept = true;

        // 파도가 가는 쪽으로 밀리면서 떠오른다. 물살에 떠밀려 나가는 그림.
        PlayerAutoWalker walker = player.GetComponentInChildren<PlayerAutoWalker>();
        if (walker != null)
        {
            walker.Launch(flow * blowSpeed + Vector3.up * blowLift);
        }

        // 사망 후 화면 연출(검은 커튼 + 실패 패널)은 ElementalKillEffect가 맡는다.
        if (killPlayer)
        {
            ElementalKillEffect.Play(this, player);
        }
    }
}
