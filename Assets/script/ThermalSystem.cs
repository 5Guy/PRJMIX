using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 열 구역(Trigger Collider)에 들어온 대상이 일정 시간 이상 머무르면 사망 처리한다.
//
// 배치 구조
//   부모 빈 오브젝트
//    ├ ThermalSystem이 붙은 자식
//    └ Trigger Collider가 붙은 자식
//
// Trigger 이벤트는 Collider가 붙은 오브젝트에서만 발생하므로,
// 실행 시 그 오브젝트에 중계 컴포넌트(ThermalSystemTriggerRelay)를 붙여 이쪽으로 넘겨받는다.
public class ThermalSystem : MonoBehaviour
{
    [Header("Thermal 설정")]
    [Tooltip("열 구역으로 사용할 Trigger Collider입니다. 비워두면 부모 아래의 Collider를 자동으로 찾습니다.")]
    [SerializeField] private Collider thermalTriggerCollider;

    [Tooltip("이 시간 동안 열 구역 안에 머무르면 사망 처리됩니다.")]
    [SerializeField] private float deathDelay = 3f;

    [Tooltip("사망 처리 시 이동시킬 위치입니다.")]
    [SerializeField] private Vector3 deathMovePosition = Vector3.zero;

    // 구역 전체 면역이 끝나는 시각(Time.time 기준). 이 시간 전까지는 모든 대상의 경과 시간이 멈춘다.
    private float zoneImmunityEndTime;

    // 대상별 면역이 끝나는 시각(Time.time 기준).
    private readonly Dictionary<Transform, float> targetImmunityEndTimes = new Dictionary<Transform, float>();

    // 열 구역 안에서 사망 대기 중인 대상별 Coroutine.
    private readonly Dictionary<Transform, Coroutine> deathDelayCoroutines = new Dictionary<Transform, Coroutine>();

    private void Awake()
    {
        InitializeTriggerCollider();
    }

    private void OnDisable()
    {
        CancelAllDeathDelays();
    }

    private void InitializeTriggerCollider()
    {
        if (thermalTriggerCollider == null)
        {
            thermalTriggerCollider = FindSiblingOrChildCollider();
        }

        if (thermalTriggerCollider == null)
        {
            Debug.LogWarning($"[{nameof(ThermalSystem)}] Thermal Trigger Collider를 찾지 못했습니다. Inspector에서 직접 지정해 주세요.", this);
            return;
        }

        thermalTriggerCollider.isTrigger = true;

        ThermalSystemTriggerRelay relay = thermalTriggerCollider.GetComponent<ThermalSystemTriggerRelay>();
        if (relay == null)
        {
            relay = thermalTriggerCollider.gameObject.AddComponent<ThermalSystemTriggerRelay>();
        }

        relay.Initialize(this);
    }

    // 자신과 자식에서 먼저 찾고, 없으면 형제 오브젝트(부모 아래)까지 훑는다.
    private Collider FindSiblingOrChildCollider()
    {
        Collider foundCollider = GetComponentInChildren<Collider>();
        if (foundCollider != null)
        {
            return foundCollider;
        }

        if (transform.parent != null)
        {
            foreach (Collider candidate in transform.parent.GetComponentsInChildren<Collider>())
            {
                if (candidate.transform != transform)
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    public void HandleTriggerEnter(Collider other)
    {
        Transform target = ResolveTargetTransform(other);

        if (target == null || deathDelayCoroutines.ContainsKey(target))
        {
            return;
        }

        deathDelayCoroutines.Add(target, StartCoroutine(DeathDelayRoutine(target)));
    }

    public void HandleTriggerExit(Collider other)
    {
        Transform target = ResolveTargetTransform(other);

        if (target == null)
        {
            return;
        }

        CancelDeathDelay(target);
    }

    // 캐릭터는 Collider가 자식이고 Rigidbody가 루트에 있는 구조가 많으므로,
    // Rigidbody가 있으면 그쪽을 대상으로 삼아 루트 기준으로 처리한다.
    private Transform ResolveTargetTransform(Collider other)
    {
        if (other == null)
        {
            return null;
        }

        return other.attachedRigidbody != null ? other.attachedRigidbody.transform : other.transform;
    }

    // 구역 전체 면역. 면역 중에는 구역 안의 어떤 대상도 경과 시간이 늘지 않는다.
    public void ApplyZoneImmunity(float duration)
    {
        if (duration <= 0f)
        {
            return;
        }

        // 이미 남아 있는 면역이 더 길면 그쪽을 유지한다.
        zoneImmunityEndTime = Mathf.Max(zoneImmunityEndTime, Time.time + duration);
    }

    // 특정 대상만 면역시킨다.
    public void ApplyTargetImmunity(Transform target, float duration)
    {
        if (target == null || duration <= 0f)
        {
            return;
        }

        float newEndTime = Time.time + duration;

        if (targetImmunityEndTimes.TryGetValue(target, out float currentEndTime))
        {
            targetImmunityEndTimes[target] = Mathf.Max(currentEndTime, newEndTime);
        }
        else
        {
            targetImmunityEndTimes.Add(target, newEndTime);
        }
    }

    private bool IsImmune(Transform target)
    {
        if (Time.time < zoneImmunityEndTime)
        {
            return true;
        }

        if (target == null)
        {
            return false;
        }

        List<Transform> expiredTargets = null;
        bool immune = false;

        foreach (KeyValuePair<Transform, float> immunity in targetImmunityEndTimes)
        {
            Transform immuneTarget = immunity.Key;

            if (immuneTarget == null || Time.time >= immunity.Value)
            {
                (expiredTargets ??= new List<Transform>()).Add(immuneTarget);
                continue;
            }

            // Collider가 플레이어 루트의 자식인 경우도 같은 대상으로 본다.
            if (target == immuneTarget || target.IsChildOf(immuneTarget))
            {
                immune = true;
                break;
            }
        }

        // 반복 중에는 Dictionary를 고칠 수 없으므로 끝난 뒤에 정리한다.
        if (expiredTargets != null)
        {
            foreach (Transform expiredTarget in expiredTargets)
            {
                targetImmunityEndTimes.Remove(expiredTarget);
            }
        }

        return immune;
    }

    private IEnumerator DeathDelayRoutine(Transform target)
    {
        float elapsedTime = 0f;

        while (elapsedTime < deathDelay)
        {
            // 대기 중 대상이 사라졌으면 그대로 끝낸다.
            if (target == null)
            {
                deathDelayCoroutines.Remove(target);
                yield break;
            }

            // 면역 중에는 구역 안에 있어도 시간이 멈춘다.
            if (!IsImmune(target))
            {
                elapsedTime += Time.deltaTime;
            }

            yield return null;
        }

        if (target != null)
        {
            ProcessDeath(target);
        }

        deathDelayCoroutines.Remove(target);
    }

    private void ProcessDeath(Transform target)
    {
        target.position = deathMovePosition;
    }

    private void CancelDeathDelay(Transform target)
    {
        if (target == null)
        {
            return;
        }

        if (deathDelayCoroutines.TryGetValue(target, out Coroutine coroutine))
        {
            StopCoroutine(coroutine);
            deathDelayCoroutines.Remove(target);
        }
    }

    private void CancelAllDeathDelays()
    {
        foreach (Coroutine coroutine in deathDelayCoroutines.Values)
        {
            if (coroutine != null)
            {
                StopCoroutine(coroutine);
            }
        }

        deathDelayCoroutines.Clear();
        targetImmunityEndTimes.Clear();
        zoneImmunityEndTime = 0f;
    }
}

// Collider가 붙은 오브젝트에서 Trigger 이벤트를 받아 ThermalSystem으로 넘겨준다.
// 파일을 따로 만들지 않아도 되도록 ThermalSystem.cs 안에 함께 둔다.
public class ThermalSystemTriggerRelay : MonoBehaviour
{
    private ThermalSystem thermalSystem;

    public void Initialize(ThermalSystem owner)
    {
        thermalSystem = owner;
    }

    private void OnTriggerEnter(Collider other)
    {
        thermalSystem?.HandleTriggerEnter(other);
    }

    private void OnTriggerExit(Collider other)
    {
        thermalSystem?.HandleTriggerExit(other);
    }
}
