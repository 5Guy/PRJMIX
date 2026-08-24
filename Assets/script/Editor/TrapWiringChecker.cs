using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// 지금 열려 있는 씬의 함정이 "원소로 파훼되는" 상태인지 하나씩 따져 보고 정리해서 알려 준다.
// Tools > Molra > 함정 파훼 점검 으로 실행한다.
//
// 파훼가 되려면 다음이 전부 맞아야 한다. 하나라도 어긋나면 플레이 중에는
// "왜 원소를 올려도 아무 일이 없지?" 로만 보여서 원인을 찾기 어렵다.
//
//   1. 파훼 원소가 지정되어 있다                     (counterElements)
//   2. 그 원소를 조합창에서 실제로 만들 수 있다       (기본 원소에서 조합으로 도달 가능한가)
//   3. 판정 콜라이더가 트리거다                      (아니면 PlacementSystem이 못 찾는다)
//   4. 판정이 배치 격자의 탐색 높이 안에 있다         (PlacementSystem.trapSearchHeight)
//   5. 판정이 플레이어 동선 위에 있다                (빗나가 있으면 죽지도 파훼되지도 않는다)
//   6. 함정 앞 접근 트리거가 출발 지점을 덮지 않는다   (덮으면 원소를 올리자마자 꺼진다)
public static class TrapWiringChecker
{
    private const string ElementFolder = "Assets/Data/Nomal";
    private const string CombinationFolder = "Assets/Data/Combination";

    // 동선에서 이만큼 벗어나면 플레이어가 닿지 않는다고 본다.
    private const float PathTolerance = 1.5f;

    // 배치 모드용: 스테이지를 하나씩 열어 가며 점검한다.
    // (-executeMethod TrapWiringChecker.CheckAllStages)
    //
    // 배치 모드는 아무 씬도 열지 않은 채 시작한다. 그냥 Check()를 부르면 "함정 0개,
    // 플레이어 없음"만 나온다 — 문제가 없다는 뜻이 아니라 아무것도 보지 않았다는 뜻이다.
    public static void CheckAllStages()
    {
        foreach (string path in new[]
        {
            "Assets/Scenes/Stage_01.unity",
            "Assets/Scenes/Stage_02.unity",
            "Assets/Scenes/Stage_03.unity",
        })
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                path, UnityEditor.SceneManagement.OpenSceneMode.Single);
            Debug.Log("──────────────────────────────────────────");
            Debug.Log($"[함정 점검] 씬: {path}");
            Check();
        }
    }

    [MenuItem("Tools/Molra/함정 파훼 점검")]
    public static void Check()
    {
        StringBuilder report = new StringBuilder();
        int problems = 0;

        HashSet<ElementData> craftable = CollectCraftableElements();
        report.AppendLine($"조합으로 만들 수 있는 원소 {craftable.Count}종");

        bool hasPath = TrapPlacement.TryGetPathDirection(out Vector3 direction, out Transform player);
        if (!hasPath)
        {
            report.AppendLine("⚠ 플레이어를 찾지 못해 동선 검사는 건너뜁니다.");
            problems++;
        }

        float searchHeight = ResolveTrapSearchHeight(report, ref problems);

        MonoBehaviour[] all = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        int trapCount = 0;

        foreach (MonoBehaviour behaviour in all)
        {
            if (behaviour is not IElementCounterTrap trap)
            {
                continue;
            }

            // 물 함정의 중계기는 주인과 같은 함정이다. 두 번 세지 않는다.
            if (behaviour is WaterTrapTriggerRelay)
            {
                continue;
            }

            trapCount++;
            problems += CheckTrap(behaviour, trap, craftable, searchHeight, hasPath, direction, player, report);
        }

        // 자동차는 파훼 말고 흙 벽으로 막는 길도 있다. 그쪽도 열려 있는지 본다.
        if (Object.FindFirstObjectByType<ChargingCar>() != null)
        {
            report.AppendLine("\n[돌진 자동차] 파훼 외에 흙 원소로 벽을 세워 막을 수도 있습니다.");
            problems += CheckEarthWallAvailable(craftable, report);
        }

        report.Insert(0, $"함정 {trapCount}개 점검, 문제 {problems}건\n");

        if (problems == 0)
        {
            Debug.Log($"[함정 점검] 이상 없습니다.\n{report}");
        }
        else
        {
            Debug.LogWarning($"[함정 점검] 문제 {problems}건을 찾았습니다.\n{report}");
        }
    }

    private static int CheckTrap(
        MonoBehaviour behaviour,
        IElementCounterTrap trap,
        HashSet<ElementData> craftable,
        float searchHeight,
        bool hasPath,
        Vector3 direction,
        Transform player,
        StringBuilder report)
    {
        int problems = 0;
        GameObject root = behaviour.transform.root.gameObject;
        report.AppendLine($"\n[{root.name} / {behaviour.name}] {behaviour.GetType().Name}");

        // 1·2. 파훼 원소
        List<ElementData> counters = ReadCounterElements(behaviour);

        if (counters.Count == 0)
        {
            report.AppendLine("  ✗ 파훼 원소가 하나도 지정되지 않았습니다. 어떤 원소를 올려도 꺼지지 않습니다.");
            problems++;
        }
        else
        {
            List<string> names = new List<string>();
            foreach (ElementData data in counters)
            {
                if (data == null)
                {
                    report.AppendLine("  ✗ 파훼 원소 목록에 빈 칸이 있습니다.");
                    problems++;
                    continue;
                }

                names.Add(data.ElementName);

                if (!craftable.Contains(data))
                {
                    report.AppendLine($"  ✗ '{data.ElementName}'은 기본 원소에서 조합으로 만들 수 없습니다. 조합식이 빠졌습니다.");
                    problems++;
                }
            }

            report.AppendLine($"  · 파훼 원소: {string.Join(" / ", names)}");
        }

        // 3·4·5. 판정 위치
        // 자동차는 차체 콜라이더를 실행 중에 만들어서 에디터에서는 잡히지 않는다. 보이는 크기로 대신 잰다.
        Collider anchor = TrapPlacement.ResolveAnchorCollider(root);
        Bounds judgement;

        if (anchor != null)
        {
            judgement = anchor.bounds;

            if (!anchor.isTrigger && behaviour is not ChargingCar)
            {
                report.AppendLine($"  ✗ 판정 콜라이더 '{anchor.name}'가 트리거가 아닙니다.");
                problems++;
            }
        }
        else if (TrapPlacement.TryMeasureVisualBounds(root, out Bounds visual))
        {
            judgement = visual;
            report.AppendLine("  · 판정 콜라이더가 실행 중에 만들어지는 종류라 보이는 크기로 검사합니다.");
        }
        else
        {
            report.AppendLine("  ✗ 판정 위치를 알아낼 방법이 없습니다(콜라이더도 Renderer도 없음).");
            return problems + 1;
        }

        // 파훼 원소를 올리는 칸은 "함정 자신"이 아니라 상쇄 칸일 수 있다(모래바람).
        // 모래바람은 휩쓸림 판정이 바닥에 있고, 물을 붓는 상쇄 칸은 그 위에 따로 떠 있다.
        Collider counterCollider = behaviour.GetComponent<Collider>();
        Vector3 counterCenter = counterCollider != null ? counterCollider.bounds.center : judgement.center;

        if (TrapPlacement.TryResolveGrid(out Vector3 gridOrigin, out float _, out int _, out int _))
        {
            float above = counterCenter.y - gridOrigin.y;
            if (above > searchHeight)
            {
                report.AppendLine(
                    $"  ✗ 파훼 칸이 바닥에서 {above:0.0}m 위에 있어 배치 탐색 높이({searchHeight:0.0}m)를 넘습니다. " +
                    "PlacementSystem의 Trap Search Height를 올리거나 함정을 낮추세요.");
                problems++;
            }
            else
            {
                report.AppendLine($"  · 파훼 칸 높이 {above:0.0}m (탐색 {searchHeight:0.0}m)");
            }
        }

        if (hasPath)
        {
            Vector3 toTrap = judgement.center - player.position;
            float along = Vector3.Dot(toTrap, direction);
            float sideways = Mathf.Abs(Vector3.Dot(toTrap, Vector3.Cross(Vector3.up, direction).normalized));

            report.AppendLine($"  · 출발 지점에서 앞으로 {along:0.0}m, 옆으로 {sideways:0.0}m");

            if (along < 0f)
            {
                report.AppendLine("  ✗ 함정이 출발 지점 뒤에 있어 플레이어가 지나가지 않습니다.");
                problems++;
            }

            // 6. 접근 트리거가 출발 지점을 덮는가
            // 자동차에는 접근 트리거가 없다(출발 트리거를 밟는 순간 파훼된다). 그때는 이 검사를 건너뛴다.
            float approachDistance = ReadFloat(behaviour, "approachDistance", 3.5f);
            bool approachMode = ReadBool(behaviour, "counterOnPlayerApproach", false);

            if (approachMode && along >= 0f && along < approachDistance + 1f)
            {
                report.AppendLine(
                    $"  ⚠ 출발 지점과 {along:0.0}m밖에 안 떨어져 접근 트리거({approachDistance:0.0}m)가 출발 지점에 걸칩니다. " +
                    "트리거는 자동으로 당겨지지만, 함정을 더 앞쪽으로 옮기는 편이 좋습니다.");
                problems++;
            }

            if (sideways > PathTolerance)
            {
                report.AppendLine($"  ✗ 동선에서 {sideways:0.0}m 벗어나 있어 플레이어가 닿지 않습니다.");
                problems++;
            }
        }

        return problems;
    }

    private static int CheckEarthWallAvailable(HashSet<ElementData> craftable, StringBuilder report)
    {
        foreach (ElementData data in craftable)
        {
            if (data.ElementType == ElementType.Earth)
            {
                report.AppendLine($"  · 벽을 세울 흙 원소 '{data.ElementName}' 사용 가능");
                return 0;
            }
        }

        report.AppendLine("  ✗ ElementType이 Earth인 원소가 없어 자동차를 막을 벽을 세울 수 없습니다.");
        return 1;
    }

    // ───────────────────────────── 읽기 도우미 ─────────────────────────────

    // counterElements는 ElementTrapCube / WaterTrap 양쪽에 있는 private 직렬화 필드다.
    private static List<ElementData> ReadCounterElements(MonoBehaviour behaviour)
    {
        List<ElementData> list = new List<ElementData>();
        SerializedProperty property = new SerializedObject(behaviour).FindProperty("counterElements");

        if (property == null || !property.isArray)
        {
            return list;
        }

        for (int i = 0; i < property.arraySize; i++)
        {
            list.Add(property.GetArrayElementAtIndex(i).objectReferenceValue as ElementData);
        }

        return list;
    }

    private static float ReadFloat(MonoBehaviour behaviour, string field, float fallback)
    {
        SerializedProperty property = new SerializedObject(behaviour).FindProperty(field);
        return property != null ? property.floatValue : fallback;
    }

    private static bool ReadBool(MonoBehaviour behaviour, string field, bool fallback)
    {
        SerializedProperty property = new SerializedObject(behaviour).FindProperty(field);
        return property != null ? property.boolValue : fallback;
    }

    private static float ResolveTrapSearchHeight(StringBuilder report, ref int problems)
    {
        PlacementSystem placement = Object.FindFirstObjectByType<PlacementSystem>();
        if (placement == null)
        {
            report.AppendLine("⚠ 씬에 PlacementSystem이 없습니다. 원소를 맵에 놓을 수 없습니다.");
            problems++;
            return 4f;
        }

        SerializedProperty property = new SerializedObject(placement).FindProperty("trapSearchHeight");
        return property != null ? property.floatValue : 4f;
    }

    // 기본 원소에서 시작해 조합식을 더 이상 새 원소가 안 나올 때까지 돌린다.
    private static HashSet<ElementData> CollectCraftableElements()
    {
        HashSet<ElementData> reachable = new HashSet<ElementData>();

        foreach (string guid in AssetDatabase.FindAssets("t:ElementData", new[] { ElementFolder }))
        {
            ElementData data = AssetDatabase.LoadAssetAtPath<ElementData>(AssetDatabase.GUIDToAssetPath(guid));
            if (data != null && data.IsBaseElement)
            {
                reachable.Add(data);
            }
        }

        List<ElementCombinationData> combinations = new List<ElementCombinationData>();
        foreach (string guid in AssetDatabase.FindAssets("t:ElementCombinationData", new[] { CombinationFolder }))
        {
            ElementCombinationData data = AssetDatabase.LoadAssetAtPath<ElementCombinationData>(AssetDatabase.GUIDToAssetPath(guid));
            if (data != null)
            {
                combinations.Add(data);
            }
        }

        bool grew = true;
        while (grew)
        {
            grew = false;

            foreach (ElementCombinationData combination in combinations)
            {
                if (combination.ResultElement == null ||
                    combination.FirstElement == null ||
                    combination.SecondElement == null)
                {
                    continue;
                }

                if (reachable.Contains(combination.FirstElement) &&
                    reachable.Contains(combination.SecondElement) &&
                    reachable.Add(combination.ResultElement))
                {
                    grew = true;
                }
            }
        }

        return reachable;
    }
}
