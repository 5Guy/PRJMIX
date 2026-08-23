using UnityEngine;
using UnityEngine.SceneManagement;

// 씬에 놓인 함정마다 탑뷰 표시(TrapTopViewIcon)를 하나씩 달아 준다.
//
// 함정은 스테이지 씬과 프리팹 여기저기에 흩어져 있어서, 하나하나 손으로 컴포넌트를 붙이면
// 새 함정을 놓을 때마다 빠뜨리기 쉽다. 씬이 열릴 때 한 번 훑어서 자동으로 달아 둔다.
//
// 손으로 조절하고 싶으면
//   - 함정에 TrapTopViewIcon을 직접 붙이고 값을 맞춰 두면 여기서는 건너뛴다.
//   - 어떤 함정만 3D 그대로 두고 싶으면 TrapTopViewIcon을 붙인 뒤 체크를 꺼 두면 된다.
//     (컴포넌트가 이미 있으므로 여기서 새로 달지 않고, 꺼져 있으니 아무 일도 하지 않는다)
public static class TrapTopViewInstaller
{
    // 어떤 함정인지 한눈에 알아보라고 표시의 색을 나눈다.
    private static readonly Color DangerColor = new Color(1f, 0.3f, 0.22f, 0.5f);
    private static readonly Color WaterColor = new Color(0.28f, 0.66f, 1f, 0.5f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        // 같은 델리게이트가 두 번 걸리지 않게 먼저 떼고 건다.
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;

        Install();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Install();
    }

    private static void Install()
    {
        int added = 0;

        // 불·물 함정은 판정 오브젝트가 연출의 자식이라, 스크립트가 붙은 곳이 아니라
        // 함정이 스스로 알려 주는 연출 뿌리에 달아야 불꽃·물까지 통째로 감춰진다.
        foreach (ElementTrapCube trap in FindAll<ElementTrapCube>())
        {
            added += InstallOn(trap.VisualRoot, ElementColor(trap.ElementData)) ? 1 : 0;
        }

        foreach (WaterTrap trap in FindAll<WaterTrap>())
        {
            added += InstallOn(trap.VisualRoot, WaterColor) ? 1 : 0;
        }

        // 달려오는 것들은 오브젝트 자체가 곧 함정의 모습이다.
        foreach (ChargingCar car in FindAll<ChargingCar>())
        {
            added += InstallOn(car.transform, DangerColor) ? 1 : 0;
        }

        foreach (ChargingBull bull in FindAll<ChargingBull>())
        {
            added += InstallOn(bull.transform, DangerColor) ? 1 : 0;
        }

        // 모래바람은 색과 크기를 스스로 알고 있어서 Awake에서 직접 달아 둔다(Sandstorm 참고).
        // 그 안에 든 상쇄 칸(ElementTrapCube)은 아래 검사에서 걸러진다.

        // 표시가 안 보인다는 말이 나올 때 "달리기는 했는지"부터 확인할 수 있게 한 줄 남긴다.
        int total = FindAll<TrapTopViewIcon>().Length;
        Debug.Log($"[탑뷰 표시] 함정 {added}개에 표시를 새로 달았습니다. 이 씬의 표시는 모두 {total}개입니다.");
    }

    // 불 함정은 주황, 물 함정은 파랑처럼 그 함정의 원소 색을 옅게 깔아 준다.
    private static Color ElementColor(ElementData data)
    {
        if (data == null)
        {
            return DangerColor;
        }

        Color color = ElementVisual.GetColor(data);
        return new Color(color.r, color.g, color.b, DangerColor.a);
    }

    private static T[] FindAll<T>() where T : Object
    {
        // 시작할 때 꺼 둔 함정(스테이지 중간에 켜지는 것)도 미리 달아 둔다.
        return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    // 새로 달았으면 true.
    private static bool InstallOn(Transform root, Color footprintColor)
    {
        if (root == null)
        {
            return false;
        }

        // 위아래 어디든 이미 표시가 있으면 그것이 이 함정을 맡는다.
        // (모래바람 ↔ 그 안의 상쇄 칸, 물 함정 ↔ 판정 릴레이처럼 함정이 겹쳐 있는 경우)
        if (root.GetComponentInParent<TrapTopViewIcon>(true) != null ||
            root.GetComponentInChildren<TrapTopViewIcon>(true) != null)
        {
            return false;
        }

        // 지름은 넘기지 않는다(0). 함정이 도로에서 차지하는 자리를 재서 알아서 잡는다.
        root.gameObject.AddComponent<TrapTopViewIcon>().Configure(root, null, footprintColor, 0f);
        return true;
    }
}
