using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 상쇄 연출(TrapCounterEffect)을 대신 돌려 주는 씬 뿌리의 일꾼.
//
// 함정은 꺼질 때 연출 뿌리를 통째로 SetActive(false) 하기 때문에,
// 함정 오브젝트 위에서 코루틴을 돌리면 파도가 밀려가는 도중에 뚝 끊긴다.
// 그래서 연출은 함정과 상관없는 이 오브젝트 위에서 돌리고,
// 만들어 낸 파도·비구름·진흙 같은 임시 오브젝트도 여기에 등록해 둔다.
//
// 스테이지가 처음 상태로 돌아가면(시작 버튼 / 사망 리스폰) 돌던 연출과 임시 오브젝트를 전부 치운다.
public class CounterEffectRunner : MonoBehaviour
{
    private static CounterEffectRunner instance;

    private readonly List<GameObject> spawned = new List<GameObject>();

    // 연출 코루틴을 시작한다. 함정이 꺼져도 끊기지 않는다.
    public static Coroutine Run(IEnumerator routine)
    {
        return Instance.StartCoroutine(routine);
    }

    // 연출이 만들어 낸 임시 오브젝트. 스테이지가 되돌아갈 때 같이 치우기 위해 맡아 둔다.
    public static GameObject Track(GameObject temporary)
    {
        if (temporary != null)
        {
            Instance.spawned.Add(temporary);
        }

        return temporary;
    }

    private static CounterEffectRunner Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject host = new GameObject("CounterEffectRunner");
                instance = host.AddComponent<CounterEffectRunner>();
            }

            return instance;
        }
    }

    private void Awake()
    {
        StageReset.Requested += HandleStageReset;
    }

    private void OnDestroy()
    {
        StageReset.Requested -= HandleStageReset;

        if (instance == this)
        {
            instance = null;
        }
    }

    private void HandleStageReset(StageReset.Reason reason)
    {
        StopAllCoroutines();

        foreach (GameObject temporary in spawned)
        {
            if (temporary != null)
            {
                Destroy(temporary);
            }
        }

        spawned.Clear();
    }
}
