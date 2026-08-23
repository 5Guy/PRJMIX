using UnityEngine;

/// <summary>
/// PlayerAutoMoveToTarget은 플레이어를 지정한 목표 지점까지 자동으로 이동시키는 스크립트입니다.
/// Inspector에서 목표 지점과 이동 속도를 조절할 수 있습니다.
/// </summary>
public class PlayerAutoMoveToTarget : MonoBehaviour
{
    [Header("목표 설정")]
    [Tooltip("플레이어가 자동으로 이동할 목표 지점입니다.")]
    [SerializeField] private Transform targetPoint;

    [Header("이동 설정")]
    [Tooltip("플레이어 이동 속도입니다. Inspector에서 원하는 값으로 조절할 수 있습니다.")]
    [SerializeField] private float moveSpeed = 3f;

    [Tooltip("목표 지점에 이 거리 이하로 가까워지면 도착한 것으로 처리합니다.")]
    [SerializeField] private float stopDistance = 0.05f;

    [Tooltip("도착 후 자동 이동을 멈출지 여부입니다.")]
    [SerializeField] private bool stopWhenArrived = true;

    // 플레이어에 Rigidbody가 있다면 물리 이동을 위해 사용합니다.
    private Rigidbody playerRigidbody;

    // 목표 지점에 도착했는지 저장합니다.
    private bool isArrived;

    /// <summary>
    /// 시작 시 Rigidbody가 있는지 확인합니다.
    /// </summary>
    private void Awake()
    {
        playerRigidbody = GetComponent<Rigidbody>();
    }

    /// <summary>
    /// Rigidbody가 있는 경우 FixedUpdate에서 물리 기반으로 이동합니다.
    /// Rigidbody가 없다면 Update에서 이동하므로 여기서는 처리하지 않습니다.
    /// </summary>
    private void FixedUpdate()
    {
        if (playerRigidbody == null)
        {
            return;
        }

        MoveToTarget(Time.fixedDeltaTime, true);
    }

    /// <summary>
    /// Rigidbody가 없는 경우 Transform 기반으로 매 프레임 이동합니다.
    /// Rigidbody가 있다면 FixedUpdate에서 이동하므로 여기서는 처리하지 않습니다.
    /// </summary>
    private void Update()
    {
        if (playerRigidbody != null)
        {
            return;
        }

        MoveToTarget(Time.deltaTime, false);
    }

    /// <summary>
    /// 목표 지점까지 플레이어를 이동시킵니다.
    /// </summary>
    /// <param name="deltaTime">프레임 시간.</param>
    /// <param name="useRigidbody">Rigidbody 이동을 사용할지 여부.</param>
    private void MoveToTarget(float deltaTime, bool useRigidbody)
    {
        // 목표 지점이 없으면 이동할 수 없습니다.
        if (targetPoint == null)
        {
            return;
        }

        // 도착 후 멈추는 옵션이 켜져 있고 이미 도착했다면 더 이상 이동하지 않습니다.
        if (stopWhenArrived && isArrived)
        {
            return;
        }

        Vector3 currentPosition = transform.position;
        Vector3 targetPosition = targetPoint.position;
        float distance = Vector3.Distance(currentPosition, targetPosition);

        // 목표 지점에 충분히 가까우면 도착 처리합니다.
        if (distance <= stopDistance)
        {
            isArrived = true;
            return;
        }

        // 현재 위치에서 목표 위치까지 moveSpeed 속도로 이동할 다음 위치를 계산합니다.
        Vector3 nextPosition = Vector3.MoveTowards(currentPosition, targetPosition, moveSpeed * deltaTime);

        if (useRigidbody)
        {
            // Rigidbody가 있으면 MovePosition으로 물리 업데이트에 맞춰 이동합니다.
            playerRigidbody.MovePosition(nextPosition);
        }
        else
        {
            // Rigidbody가 없으면 Transform 위치를 직접 변경합니다.
            transform.position = nextPosition;
        }
    }

    /// <summary>
    /// 외부에서 새로운 목표 지점을 설정할 때 사용할 수 있는 함수입니다.
    /// </summary>
    /// <param name="newTargetPoint">새 목표 지점.</param>
    public void SetTargetPoint(Transform newTargetPoint)
    {
        targetPoint = newTargetPoint;
        isArrived = false;
    }

    /// <summary>
    /// 자동 이동을 다시 시작하고 싶을 때 도착 상태를 초기화합니다.
    /// </summary>
    public void RestartMove()
    {
        isArrived = false;
    }
}
