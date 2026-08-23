using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 투명 위치 오브젝트 1개를 기준으로 Thermal 함정 1개를 설치하는 버전입니다.
/// 큐브는 바꾸지 않고, Inspector에 연결한 Trap Point 위치에 함정만 생성합니다.
/// </summary>
[ExecuteAlways]
public class TrapPlatformOne : MonoBehaviour
{
    [Header("생성 설정")]
    [Tooltip("생성된 Thermal 함정이 들어갈 부모입니다. 비워두면 이 오브젝트 아래에 생성됩니다.")]
    [SerializeField] private Transform trapParent;

    [Tooltip("함정으로 배치할 Thermal 프리팹입니다.")]
    [SerializeField] private GameObject thermalTrapPrefab;

    [Header("함정 위치 오브젝트")]
    [Tooltip("함정 1개가 설치될 투명 위치 오브젝트입니다.")]
    [SerializeField] private Transform trapPoint;

    [Header("자동 갱신")]
    [Tooltip("Inspector 값이 바뀔 때 함정을 자동으로 다시 배치합니다.")]
    [SerializeField] private bool autoBuild = true;

    private void OnEnable()
    {
        Build();
    }

    private void OnValidate()
    {
        if (!autoBuild)
        {
            return;
        }

#if UNITY_EDITOR
        EditorApplication.delayCall += DelayedBuild;
#else
        Build();
#endif
    }

#if UNITY_EDITOR
    private void DelayedBuild()
    {
        EditorApplication.delayCall -= DelayedBuild;

        if (this == null || !autoBuild)
        {
            return;
        }

        Build();
    }
#endif

    /// <summary>
    /// Inspector에 연결한 투명 위치 오브젝트에 Thermal 함정 1개를 생성합니다.
    /// </summary>
    [ContextMenu("Build Thermal Traps")]
    public void Build()
    {
        TrapPlatformBuilder.BuildTraps(this, trapParent, thermalTrapPrefab, new[] { trapPoint });
    }

    /// <summary>
    /// 생성된 Thermal 함정을 제거합니다.
    /// </summary>
    [ContextMenu("Clear Thermal Traps")]
    public void Clear()
    {
        Transform parent = trapParent != null ? trapParent : transform;
        TrapPlatformBuilder.ClearGeneratedTraps(parent);
    }
}
