using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 배치 모드에서 실제로 플레이를 돌려 보는 검사 도구.
//
// 씬을 눈으로 볼 수 없는 자리(-batchmode)에서 "원소를 올리면 함정이 정말 꺼지는가"를
// 확인하려면 실제 플레이 세션이 필요하다. 에디터 도구만으로는 Awake/Start에서 만들어지는
// 접근 트리거나 판정 릴레이가 아예 존재하지 않아 아무것도 확인할 수 없다.
//
//   Unity.exe -batchmode -nographics -projectPath . -executeMethod MolraPlayProbe.RunStage01
//
// -quit 은 주지 않는다. 플레이가 끝난 뒤 이쪽에서 직접 에디터를 닫는다.
public static class MolraPlayProbe
{
    private const string SceneKey = "Molra.PlayProbe.Scene";
    private const string ElementKey = "Molra.PlayProbe.Element";

    public static void RunStage01() => Run("Assets/Scenes/Stage_01.unity");
    public static void RunStage02() => Run("Assets/Scenes/Stage_02.unity");
    public static void RunStage03() => Run("Assets/Scenes/Stage_03.unity");

    private static void Run(string scenePath)
    {
        // 무엇을 물 함정에 올려 볼지. 인자로 주면 그 원소를 쓴다.
        string element = ArgumentValue("-probeElement") ?? "Lava";

        SessionState.SetString(SceneKey, scenePath);
        SessionState.SetString(ElementKey, element);

        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        EditorApplication.playModeStateChanged += HandlePlayModeChanged;

        // 배치 모드(-batchmode -nographics)의 에디터는 할 일이 없으면 화면을 갱신하지 않는다.
        // 플레이 중에도 마찬가지라, 아무것도 하지 않으면 첫 몇 프레임만 돌고 그대로 멈춘다.
        // 에디터가 도는 동안 매번 플레이 루프를 한 번씩 돌려 달라고 부탁해 둔다.
        EditorApplication.update += PumpPlayerLoop;

        EditorApplication.EnterPlaymode();
    }

    private static void PumpPlayerLoop()
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.QueuePlayerLoopUpdate();
        }
    }

    private static string ArgumentValue(string name)
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static void HandlePlayModeChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredEditMode)
        {
            return;
        }

        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        EditorApplication.update -= PumpPlayerLoop;
        SessionState.EraseString(SceneKey);

        if (Application.isBatchMode)
        {
            EditorApplication.Exit(0);
        }
    }

    // 플레이가 시작되면(에디터 어셈블리는 플레이 중에도 살아 있다) 검사 진행자를 하나 세운다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallProbe()
    {
        if (string.IsNullOrEmpty(SessionState.GetString(SceneKey, string.Empty)))
        {
            return;
        }

        GameObject host = new GameObject("MolraPlayProbeRunner");
        Object.DontDestroyOnLoad(host);
        host.AddComponent<MolraPlayProbeRunner>().Setup(SessionState.GetString(ElementKey, "Lava"));
    }
}

// 실제 검사 절차. 사람이 하는 순서를 그대로 따라간다.
//   1) 물 함정을 찾아 파훼 원소를 올린다 (PlacementSystem이 하는 것과 같은 조회로 찾는다)
//   2) 시작 버튼을 누른다
//   3) 플레이어가 걸어가는 동안 지켜본다
//   4) 물이 걷혔는지, 플레이어가 죽지 않고 깃발까지 갔는지 적는다
public class MolraPlayProbeRunner : MonoBehaviour
{
    private string elementName = "Lava";
    private readonly StringBuilder log = new StringBuilder();

    public void Setup(string element)
    {
        elementName = element;
    }

    private void Start()
    {
        StartCoroutine(RunProbe());
    }

    private IEnumerator RunProbe()
    {
        // 배치 모드의 한 프레임은 눈 깜짝할 새(0.0005초)라, 그냥 두면 30초를 채우는 데
        // 6만 프레임이 든다. 프레임마다 게임 시간을 일정하게 흘려 보내 50fps로 녹화하듯 돌린다.
        Time.captureDeltaTime = 1f / 50f;

        // 씬의 Awake/Start가 다 돌 때까지 몇 프레임 기다린다.
        for (int i = 0; i < 5; i++)
        {
            yield return null;
        }

        Line("===== 플레이 검사 시작 =====");

        PlacementGrid grid = Object.FindFirstObjectByType<PlacementGrid>();
        Transform player = PlayerLocator.FindPlayer();
        StageGoalFlag goal = Object.FindFirstObjectByType<StageGoalFlag>();

        Line($"격자 {(grid != null ? $"칸 {grid.CellSize:0.000} 원점 {grid.transform.position}" : "없음")}");
        Line($"플레이어 {(player != null ? player.position.ToString() : "없음")}");
        Line($"깃발 {(goal != null ? goal.transform.position.ToString() : "없음")}");

        // 씬에 있는 파훼 함정 전부를 적는다.
        foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (behaviour is IElementCounterTrap && behaviour is not WaterTrapTriggerRelay)
            {
                Line($"함정 {behaviour.GetType().Name} '{behaviour.name}' (뿌리 '{behaviour.transform.root.name}') pos {behaviour.transform.position} active={behaviour.gameObject.activeInHierarchy}");

                // 어디에 닿으면 죽는지가 곧 함정의 실제 크기다.
                foreach (Collider hit in behaviour.transform.root.GetComponentsInChildren<Collider>(true))
                {
                    Line($"    콜라이더 '{hit.name}' trigger={hit.isTrigger} bounds c{hit.bounds.center} s{hit.bounds.size}");
                }
            }
        }

        // 접근 트리거가 어디에 섰는지.
        foreach (TrapApproachZone zone in Object.FindObjectsByType<TrapApproachZone>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            BoxCollider box = zone.GetComponent<Collider>() as BoxCollider;
            Line($"접근 트리거 '{zone.name}' pos {zone.transform.position} size {(box != null ? box.size.ToString() : "?")}");
        }

        // 파훼할 수 있는 함정마다 맞는 원소를 찾아 올려 둔다.
        //
        // 사람이 조합창에서 끌어다 놓는 것과 같은 결과가 되도록, 원소를 하나씩 대 보고
        // 함정이 받아 주는 것을 쓴다. 진흙 2개처럼 여러 번 올려야 하는 함정은 다 찰 때까지 붓는다.
        ElementData[] elements = LoadElements();
        Line($"원소 {elements.Length}개를 불러왔습니다.");

        foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (behaviour is not IElementCounterTrap trap || behaviour is WaterTrapTriggerRelay)
            {
                continue;
            }

            ElementData chosen = null;

            // -probeElement 로 지정한 원소가 이 함정에 먹히면 그것을 먼저 쓴다.
            // "물 함정이 용암으로도, 나무다리로도, 시멘트로도 걷히는가"를 하나씩 확인할 때 쓴다.
            foreach (ElementData candidate in elements)
            {
                if (candidate.name == elementName && trap.CanBeCounteredBy(candidate))
                {
                    chosen = candidate;
                    break;
                }
            }

            foreach (ElementData candidate in elements)
            {
                if (chosen != null)
                {
                    break;
                }

                if (trap.CanBeCounteredBy(candidate))
                {
                    chosen = candidate;
                }
            }

            if (chosen == null)
            {
                Line($"[!] '{behaviour.name}' 을 끌 수 있는 원소를 하나도 찾지 못했습니다.");
                continue;
            }

            int poured = 0;
            while (trap.TryCounter(chosen) && poured < 8)
            {
                poured++;

                if (trap.IsArmed)
                {
                    break;
                }
            }

            Line($"'{behaviour.name}' ← '{chosen.ElementName}' {poured}개, 올림 완료={trap.IsArmed}");
        }

        WaterTrap water = Object.FindFirstObjectByType<WaterTrap>();

        // 시작 버튼을 누른다.
        StageStartButton start = Object.FindFirstObjectByType<StageStartButton>(FindObjectsInactive.Include);
        if (start != null)
        {
            start.Begin();
            Line("시작 버튼을 눌렀습니다.");
        }
        else
        {
            Line("시작 버튼이 없습니다. 걷기만 시작합니다.");
            PlayerAutoWalker walker = Object.FindFirstObjectByType<PlayerAutoWalker>(FindObjectsInactive.Include);
            if (walker != null)
            {
                walker.StartWalking();
            }
        }

        // 죽어서 되살아나는 순간을 놓치지 않도록 지켜본다.
        PlayerAutoWalker.BeforeRespawn += who => Line($"[!] 플레이어가 죽어 되살아납니다 ({who.position})");

        // 걸어가는 동안 지켜본다.
        float watched = 0f;
        float nextTrace = 0f;
        int frames = 0;
        bool reportedCounter = false;

        // 프레임 수로도 끊는다. 배치 모드에서 프레임이 아주 느리게 돌면
        // 게임 시간 30초를 채우느라 몇 십 분씩 붙잡고 있게 된다.
        while (watched < 30f && frames < 4000)
        {
            watched += Time.deltaTime;
            frames++;

            if (frames % 500 == 0)
            {
                Debug.Log($"[검사] {frames}프레임, 게임시간 {watched:0.0}초");
            }

            if (water != null && water.IsCountered && !reportedCounter)
            {
                reportedCounter = true;
                Line($"[{watched:0.00}s] 물이 걷혔습니다. 플레이어 {(player != null ? player.position.ToString() : "?")}");
            }

            if (player == null)
            {
                Line($"[{watched:0.00}s] 플레이어가 사라졌습니다.");
                break;
            }

            if (watched >= nextTrace)
            {
                nextTrace = watched + 0.5f;
                Line($"[{watched:0.00}s] 플레이어 {player.position}");
            }

            yield return null;
        }

        Line($"검사 종료: 물 걷힘 = {(water != null ? water.IsCountered.ToString() : "-")}, 플레이어 {(player != null ? player.position.ToString() : "없음")}");

        if (player != null && goal != null)
        {
            float left = Vector3.Distance(
                new Vector3(player.position.x, 0f, player.position.z),
                new Vector3(goal.transform.position.x, 0f, goal.transform.position.z));

            Line($"깃발까지 남은 거리 {left:0.0}m");
        }
        Line("===== 플레이 검사 끝 =====");

        Debug.Log(log.ToString());

        EditorApplication.ExitPlaymode();
    }

    // 조합창에 뜨는 원소 전부. 검사에서 함정마다 맞는 것을 골라 쓴다.
    private static ElementData[] LoadElements()
    {
        List<ElementData> elements = new List<ElementData>();

        foreach (string guid in AssetDatabase.FindAssets("t:ElementData", new[] { "Assets/Data/Nomal" }))
        {
            ElementData data = AssetDatabase.LoadAssetAtPath<ElementData>(AssetDatabase.GUIDToAssetPath(guid));
            if (data != null)
            {
                elements.Add(data);
            }
        }

        return elements.ToArray();
    }

    private void Line(string text)
    {
        log.AppendLine(text);
    }
}
