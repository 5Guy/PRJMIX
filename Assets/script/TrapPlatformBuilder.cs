using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// TrapPlatformBuilder는 투명 위치 오브젝트(Trap Point)를 기준으로
/// Thermal 함정을 생성하는 공통 기능 클래스입니다.
///
/// 사용 구조 예시:
/// - 플랫폼 큐브들은 그대로 유지합니다.
/// - 큐브 위에 함정 위치 표시용 투명 빈 오브젝트를 배치합니다.
/// - Inspector에서 그 투명 오브젝트 Transform을 연결합니다.
/// - 이 빌더가 해당 위치에 Thermal 함정 프리팹을 생성합니다.
/// </summary>
public static class TrapPlatformBuilder
{
    // 이 빌더가 생성한 함정 오브젝트 이름 앞에 붙일 접두사입니다.
    private const string TrapPrefix = "ThermalTrap_";

    /// <summary>
    /// 지정된 투명 위치 오브젝트들 위에 Thermal 함정을 배치합니다.
    /// </summary>
    /// <param name="owner">로그와 Undo 처리를 위한 호출자 MonoBehaviour.</param>
    /// <param name="trapParent">생성된 함정들을 넣을 부모 Transform.</param>
    /// <param name="thermalTrapPrefab">배치할 Thermal 함정 프리팹.</param>
    /// <param name="trapPoints">함정을 설치할 투명 위치 오브젝트 Transform 목록.</param>
    public static void BuildTraps(
        MonoBehaviour owner,
        Transform trapParent,
        GameObject thermalTrapPrefab,
        IReadOnlyList<Transform> trapPoints)
    {
        // 부모가 비어 있으면 호출자 오브젝트 아래에 함정을 생성합니다.
        if (trapParent == null && owner != null)
        {
            trapParent = owner.transform;
        }

        // 그래도 부모가 없으면 함정을 정리해서 넣을 수 없으므로 중단합니다.
        if (trapParent == null)
        {
            Debug.LogWarning("[TrapPlatformBuilder] Trap Parent가 연결되지 않았습니다.", owner);
            return;
        }

        // Thermal 함정 프리팹이 없으면 함정을 생성할 수 없으므로 중단합니다.
        if (thermalTrapPrefab == null)
        {
            Debug.LogWarning("[TrapPlatformBuilder] Thermal Trap Prefab이 연결되지 않았습니다.", owner);
            return;
        }

        // 이전에 이 스크립트가 만든 함정을 지워 중복 생성을 막습니다.
        ClearGeneratedTraps(trapParent);

        // 지정된 투명 위치 오브젝트마다 함정을 생성합니다.
        for (int i = 0; i < trapPoints.Count; i++)
        {
            Transform trapPoint = trapPoints[i];

            // Inspector에서 비워둔 위치는 건너뜁니다.
            if (trapPoint == null)
            {
                Debug.LogWarning($"[TrapPlatformBuilder] Trap Point {i + 1}이 연결되지 않았습니다.", owner);
                continue;
            }

            CreateTrapAtPoint(trapParent, thermalTrapPrefab, trapPoint, i + 1);
        }
    }

    /// <summary>
    /// 이 빌더가 생성한 Thermal 함정 오브젝트를 제거합니다.
    /// </summary>
    /// <param name="trapParent">함정이 들어있는 부모 Transform.</param>
    public static void ClearGeneratedTraps(Transform trapParent)
    {
        if (trapParent == null)
        {
            return;
        }

        // 뒤에서 앞으로 순회해야 삭제 중 인덱스가 밀리지 않습니다.
        for (int i = trapParent.childCount - 1; i >= 0; i--)
        {
            Transform child = trapParent.GetChild(i);
            if (!child.name.StartsWith(TrapPrefix))
            {
                continue;
            }

            DestroyObject(child.gameObject);
        }
    }

    /// <summary>
    /// 투명 위치 오브젝트의 위치/회전/스케일을 기준으로 Thermal 함정을 생성합니다.
    /// </summary>
    /// <param name="trapParent">생성된 함정을 넣을 부모 Transform.</param>
    /// <param name="thermalTrapPrefab">생성할 Thermal 함정 프리팹.</param>
    /// <param name="trapPoint">함정 위치를 나타내는 투명 오브젝트 Transform.</param>
    /// <param name="index">함정 번호.</param>
    private static void CreateTrapAtPoint(Transform trapParent, GameObject thermalTrapPrefab, Transform trapPoint, int index)
    {
        // 투명 위치 오브젝트의 월드 위치와 회전을 그대로 사용합니다.
        GameObject trap = InstantiateObject(thermalTrapPrefab, trapPoint.position, trapPoint.rotation, trapParent);
        trap.name = TrapPrefix + index;

        // 투명 위치 오브젝트에서 크기를 조정할 수 있도록 스케일도 복사합니다.
        trap.transform.localScale = trapPoint.lossyScale;
    }

    /// <summary>
    /// Play Mode와 Edit Mode에서 모두 안전하게 오브젝트를 생성합니다.
    /// </summary>
    private static GameObject InstantiateObject(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
    {
#if UNITY_EDITOR
        // 에디터 상태에서는 Prefab 연결을 유지하며 생성합니다.
        if (!Application.isPlaying)
        {
            GameObject editorObject = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            editorObject.transform.position = position;
            editorObject.transform.rotation = rotation;
            Undo.RegisterCreatedObjectUndo(editorObject, "Create Thermal Trap");
            return editorObject;
        }
#endif

        // Play Mode에서는 일반 Instantiate를 사용합니다.
        return Object.Instantiate(prefab, position, rotation, parent);
    }

    /// <summary>
    /// Play Mode와 Edit Mode에서 모두 안전하게 오브젝트를 제거합니다.
    /// </summary>
    private static void DestroyObject(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(target);
        }
        else
        {
#if UNITY_EDITOR
            Undo.DestroyObjectImmediate(target);
#else
            Object.DestroyImmediate(target);
#endif
        }
    }
}
