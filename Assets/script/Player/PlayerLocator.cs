using UnityEngine;

// 플레이어 탐색 + 사망 처리 공통 로직.
// 불 함정(ElementTrapCube)과 자동차(ChargingCar)처럼 "닿으면 죽는" 요소들이 함께 쓴다.
public static class PlayerLocator
{
    public const string PlayerTag = "Player";

    // 씬의 "진짜 플레이어"를 찾는다.
    //
    // Player 태그는 여러 오브젝트에 붙어 있을 수 있어서(예: 옛날 테스트용 Capsule) 태그만으로는
    // 어느 것이 걸어다니는 플레이어인지 알 수 없다. 실제로 움직이는 PlayerAutoWalker를 먼저 찾고,
    // 없을 때만 태그로 넘어간다.
    public static Transform FindPlayer()
    {
        PlayerAutoWalker[] walkers = Object.FindObjectsByType<PlayerAutoWalker>(FindObjectsSortMode.None);

        // 걷는 놈이 여럿이면 Player 태그가 붙은 쪽을 고른다.
        foreach (PlayerAutoWalker walker in walkers)
        {
            Transform root = FindPlayerRoot(walker.transform);
            if (root != null)
            {
                return root;
            }
        }

        if (walkers.Length > 0)
        {
            return walkers[0].transform;
        }

        GameObject tagged = GameObject.FindGameObjectWithTag(PlayerTag);
        return tagged != null ? tagged.transform : null;
    }

    // 이 오브젝트(또는 그 하위)가 걸어다니는 플레이어인지.
    public static bool IsPlayer(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        return target.GetComponentInChildren<PlayerAutoWalker>() != null || FindPlayerRoot(target) != null;
    }

    // 콜라이더가 Player의 자식(예: Capsule)에 달려 있을 수 있으므로 상위까지 태그를 확인한다.
    public static Transform FindPlayerRoot(Transform target)
    {
        for (Transform current = target; current != null; current = current.parent)
        {
            if (current.CompareTag(PlayerTag))
            {
                return current;
            }
        }

        return null;
    }

    // 이미 사망 처리 중이면 true. 연출을 중복 재생하지 않기 위해 확인한다.
    public static bool IsAlreadyDead(Transform playerRoot)
    {
        IPlayerKillable killable = playerRoot != null ? playerRoot.GetComponentInChildren<IPlayerKillable>() : null;
        return killable != null && killable.IsDead;
    }

    // 사망 연출(Animator 트리거 + 사운드)을 재생하고, 감속/리스폰 반응을 플레이어에게 맡긴다.
    // 실제로 사망 처리가 일어났으면 true.
    public static bool Kill(Transform playerRoot, string deathTriggerName, Object context)
    {
        if (playerRoot == null)
        {
            return false;
        }

        IPlayerKillable killable = playerRoot.GetComponentInChildren<IPlayerKillable>();
        if (killable != null && killable.IsDead)
        {
            return false;
        }

        PlayDeathAnimation(playerRoot, deathTriggerName, context);
        PlayDeathSound(playerRoot);
        killable?.OnKilled();
        return true;
    }

    private static void PlayDeathAnimation(Transform playerRoot, string triggerName, Object context)
    {
        if (string.IsNullOrEmpty(triggerName))
        {
            return;
        }

        Animator animator = playerRoot.GetComponentInChildren<Animator>();
        if (animator == null)
        {
            Debug.LogWarning($"'{playerRoot.name}' 하위에서 Animator를 찾지 못해 사망 애니메이션을 재생할 수 없습니다.", context);
            return;
        }

        animator.SetTrigger(triggerName);
    }

    private static void PlayDeathSound(Transform playerRoot)
    {
        PlayerAudio audio = playerRoot.GetComponentInChildren<PlayerAudio>();
        audio?.PlayDeathSound();
    }
}
