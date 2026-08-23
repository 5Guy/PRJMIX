using System.Collections;
using UnityEngine;

// 늪 원소를 도로에 놓으면 생기는 웅덩이.
//
// 달려오던 차가 이 칸에 들어오면 앞바퀴부터 푹 꺼지면서, 앞쪽이 박힌 채로 가라앉는다.
// 벽처럼 튕겨 내는 게 아니라 빨려 들어가는 것이라 판정은 트리거로 둔다.
// (자동차의 벽 감지 BoxCast는 트리거를 무시하므로 차는 멈추지 않고 그대로 들어온다)
//
// 늪은 차만 가리지 않는다. 걸어 들어온 플레이어도 그대로 빠져 죽으므로,
// 차가 지나갈 자리에만 놓아야 한다. 플레이어를 가라앉히는 방법은 물 함정(WaterTrap)과 같다.
// 사망 처리한 뒤 플레이어 콜라이더를 잠시 Trigger로 바꿔 바닥을 통과하게 만들고,
// 떨어지는 속도를 늪에 잠기는 속도로 눌러 준다.
[RequireComponent(typeof(Collider))]
public class SwampPit : MonoBehaviour
{
    [Header("차가 빠지는 정도")]
    [Tooltip("차가 가라앉는 깊이(미터)")]
    [SerializeField] private float depth = 1.4f;
    [Tooltip("앞쪽이 박히면서 기우는 각도(도). 클수록 앞으로 곤두박질친다")]
    [SerializeField] private float pitchAngle = 55f;
    [Tooltip("다 가라앉기까지 걸리는 시간(초)")]
    [SerializeField] private float sinkDuration = 1.6f;

    [Header("플레이어")]
    [Tooltip("걸어 들어온 플레이어도 빠져 죽는다")]
    [SerializeField] private bool swallowPlayer = true;
    [Tooltip("플레이어가 잠기는 속도(m/s). 낮을수록 천천히 빨려 들어간다")]
    [SerializeField] private float playerSinkSpeed = 0.7f;
    [Tooltip("이만큼 잠기면 더 내려가지 않고 멈춘다(미터)")]
    [SerializeField] private float playerSinkDepth = 2.5f;
    [SerializeField] private string deathAnimationTriggerName = "Die";

    [Header("연출")]
    [SerializeField] private Color splashColor = new Color(0.2f, 0.26f, 0.15f);
    [Tooltip("물결과 거품이 퍼지는 지름(미터). 0이면 판정 크기에서 가져온다")]
    [SerializeField] private float splashDiameter;

    [Header("사운드")]
    [Tooltip("차나 플레이어가 빠질 때 울릴 소리")]
    [SerializeField] private AudioClip sinkSound;
    [Range(0f, 1f)]
    [SerializeField] private float sinkVolume = 1f;

    private Collider zone;
    private bool carUsed;

    // 플레이어를 가라앉히는 동안 잠시 바꿔 둔 것들. 리스폰 직전에 그대로 되돌린다.
    private Coroutine playerSinkRoutine;
    private Collider[] changedColliders;
    private bool[] wasTrigger;

    private void Awake()
    {
        zone = GetComponent<Collider>();
        if (!zone.isTrigger)
        {
            zone.isTrigger = true;
        }

        if (splashDiameter <= 0f)
        {
            Bounds bounds = zone.bounds;
            splashDiameter = Mathf.Max(0.5f, Mathf.Max(bounds.size.x, bounds.size.z));
        }
    }

    private void OnEnable()
    {
        // 시작 버튼을 다시 누르면 한 번 더 쓸 수 있어야 한다.
        StageReset.Requested += HandleStageReset;

        // 리스폰 직전에 플레이어를 원래대로 돌려놓는다.
        // 이 웅덩이는 리스폰이 끝난 뒤에야 치워지므로 여기서 확실히 되돌릴 수 있다.
        PlayerAutoWalker.BeforeRespawn += HandleBeforeRespawn;
    }

    private void OnDisable()
    {
        StageReset.Requested -= HandleStageReset;
        PlayerAutoWalker.BeforeRespawn -= HandleBeforeRespawn;

        // 웅덩이를 회수하거나 스테이지가 끝나 사라질 때, 바꿔 둔 채로 두면 플레이어가 계속 바닥을 통과한다.
        RestorePlayer();
    }

    private void HandleStageReset(StageReset.Reason reason)
    {
        carUsed = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        // 자동차의 치임 판정(CarKillZone)도 같은 Rigidbody라 여기로 들어온다. 본체를 찾아 쓴다.
        ChargingCar car = other.GetComponentInParent<ChargingCar>();
        if (car != null)
        {
            SwallowCar(car);
            return;
        }

        if (swallowPlayer)
        {
            SwallowPlayer(other);
        }
    }

    private void SwallowCar(ChargingCar car)
    {
        if (carUsed || !car.IsCharging)
        {
            return;
        }

        carUsed = true;

        car.SinkInto(depth, pitchAngle, sinkDuration);
        PlaySplash(sinkDuration);
    }

    private void SwallowPlayer(Collider other)
    {
        Transform player = PlayerLocator.FindPlayerRoot(other.transform);
        if (player == null || PlayerLocator.IsAlreadyDead(player))
        {
            return;
        }

        if (!PlayerLocator.Kill(player, deathAnimationTriggerName, this))
        {
            return;
        }

        // 콜라이더를 Trigger로 바꿔 바닥을 통과하게 만든다. 이게 없으면 도로 위에 그냥 쓰러진다.
        MakeCollidersTrigger(player);

        Rigidbody body = player.GetComponentInChildren<Rigidbody>();
        if (body != null)
        {
            playerSinkRoutine = StartCoroutine(SinkPlayerRoutine(body));
        }

        PlaySplash(playerSinkDepth / Mathf.Max(0.05f, playerSinkSpeed));

        // 사망 후 화면 연출(검은 커튼 + 실패 패널)은 ElementalKillEffect가 맡는다.
        // 연출은 임시 오브젝트에서 도니, 이 웅덩이가 리스폰과 함께 치워져도 끊기지 않는다.
        ElementalKillEffect.Play(this, player);
    }

    // 그냥 두면 중력에 끌려 순식간에 떨어진다. 늪에 잠기는 속도로 눌러 준다.
    //
    // 걷기(PlayerAutoWalker)는 수평 속도만 덮어쓰고 y는 그대로 두므로, 여기서 y를 잡아도 서로 부딪히지 않는다.
    private IEnumerator SinkPlayerRoutine(Rigidbody body)
    {
        float sunk = 0f;
        float speed = Mathf.Max(0.05f, playerSinkSpeed);

        // 리스폰 직전에 밖에서 멈춰 줄 때까지 계속 잡고 있는다.
        // 다 잠긴 뒤에 놓아 버리면 다시 중력에 끌려 한없이 떨어진다.
        while (body != null)
        {
            float fall = sunk < playerSinkDepth ? -speed : 0f;
            sunk += speed * Time.fixedDeltaTime;

            Vector3 velocity = body.linearVelocity;

            // 늪에 발이 잡혀 앞으로 나아가지 못한다.
            body.linearVelocity = new Vector3(velocity.x * 0.2f, fall, velocity.z * 0.2f);

            yield return new WaitForFixedUpdate();
        }
    }

    private void HandleBeforeRespawn(Transform player)
    {
        RestorePlayer();
    }

    private void RestorePlayer()
    {
        if (playerSinkRoutine != null)
        {
            StopCoroutine(playerSinkRoutine);
            playerSinkRoutine = null;
        }

        if (changedColliders != null)
        {
            for (int i = 0; i < changedColliders.Length; i++)
            {
                if (changedColliders[i] != null)
                {
                    changedColliders[i].isTrigger = wasTrigger[i];
                }
            }

            changedColliders = null;
            wasTrigger = null;
        }
    }

    private void MakeCollidersTrigger(Transform player)
    {
        if (changedColliders != null)
        {
            return;
        }

        changedColliders = player.GetComponentsInChildren<Collider>();
        wasTrigger = new bool[changedColliders.Length];

        for (int i = 0; i < changedColliders.Length; i++)
        {
            wasTrigger[i] = changedColliders[i].isTrigger;
            changedColliders[i].isTrigger = true;
        }
    }

    private void PlaySplash(float duration)
    {
        // 물결·거품은 늪 한가운데 수면에서 올라온다.
        Vector3 surface = new Vector3(transform.position.x, zone.bounds.max.y, transform.position.z);

        CarWreckEffects.SwampSplash(surface, splashColor, splashDiameter, duration);
        SfxPlayer.PlayAt(sinkSound, transform.position, sinkVolume);
    }
}
