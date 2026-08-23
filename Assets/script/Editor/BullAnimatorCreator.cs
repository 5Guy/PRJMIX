using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// 황소 애니메이션을 재생할 수 있게 만들어 주는 에디터 도구.
// Tools > Molra > 황소 애니메이터 만들기 로 실행한다.
//
// bullsOneShot.fbx 는 모델과 애니메이션만 들어 있고 Animator Controller가 없다.
// Unity는 Controller가 비어 있으면 클립이 있어도 아무것도 재생하지 않기 때문에,
// 씬에 그냥 끌어다 놓으면 황소가 뻣뻣하게 미끄러지기만 한다.
//
// 이 도구가 하는 일
//  1. FBX 안의 클립(Armature|ArmatureAction)에 Loop Time을 켠다. 안 켜면 한 번 뛰고 멈춘다.
//  2. Assets/Animator/Bull.controller 를 만든다.
//     상태는 Run 하나뿐이고 기본 상태라, 게임이 시작되는 순간부터 계속 반복 재생된다.
//     (황소는 서 있는 동작과 달리는 동작이 나뉘어 있지 않고 클립이 하나뿐이다)
//  3. 씬에서 고른 황소(또는 씬에 하나뿐인 ChargingBull)의 Animator에 물려 준다.
public static class BullAnimatorCreator
{
    private const string BullModelPath = "Assets/Prefab/bullsOneShot.fbx";
    private const string ControllerPath = "Assets/Animator/Bull.controller";
    private const string RunState = "Run";

    [MenuItem("Tools/Molra/황소 애니메이터 만들기")]
    public static void CreateAnimator()
    {
        AnimationClip clip = PrepareClip();
        if (clip == null)
        {
            return;
        }

        AnimatorController controller = BuildController(clip);
        AssignToBull(controller);

        Selection.activeObject = controller;
        EditorGUIUtility.PingObject(controller);
    }

    // FBX의 클립을 반복 재생되게 고쳐서 가져온다.
    //
    // 이 FBX는 클립이 잘려 있지 않아(clipAnimations가 비어 있다) 기본 테이크 하나만 들어 있다.
    // 기본 테이크는 Loop Time이 꺼진 채로 들어오므로, 그대로 쓰면 한 바퀴 뛰고 정지 자세로 굳는다.
    // defaultClipAnimations를 그대로 복사해 loopTime만 켜서 다시 심는다.
    private static AnimationClip PrepareClip()
    {
        ModelImporter importer = AssetImporter.GetAtPath(BullModelPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"{BullModelPath} 를 찾지 못했습니다.");
            return null;
        }

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
        {
            clips = importer.defaultClipAnimations;
        }

        if (clips == null || clips.Length == 0)
        {
            Debug.LogError(
                $"{BullModelPath} 안에 애니메이션 클립이 없습니다. " +
                "FBX Inspector의 Animation 탭에서 Import Animation이 켜져 있는지 확인해 주세요.");
            return null;
        }

        bool changed = false;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            if (!clip.loopTime)
            {
                clip.loopTime = true;
                changed = true;
            }
        }

        if (changed || importer.clipAnimations == null || importer.clipAnimations.Length == 0)
        {
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            Debug.Log($"{BullModelPath}: 클립 {clips.Length}개에 Loop Time을 켜고 다시 가져왔습니다.");
        }

        // 다시 가져온 뒤의 실제 클립 에셋을 집는다. FBX 안에 하위 에셋으로 들어 있다.
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(BullModelPath))
        {
            // 미리보기용 __preview__ 클립은 건너뛴다. 이건 에셋에 물릴 수 없다.
            if (asset is AnimationClip found && !found.name.StartsWith("__preview__"))
            {
                Debug.Log($"{BullModelPath}: 애니메이션 클립 '{found.name}' (길이 {found.length:0.00}초)을 찾았습니다.");
                return found;
            }
        }

        Debug.LogError($"{BullModelPath} 에서 AnimationClip 에셋을 꺼내지 못했습니다.");
        return null;
    }

    private static AnimatorController BuildController(AnimationClip clip)
    {
        EnsureFolder("Assets/Animator");

        // 이미 있으면 덮어쓰지 않고 상태만 다시 맞춘다(황소에 물려 둔 참조가 안 끊긴다).
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }

        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        // 지난번에 만들어 둔 Idle/Charge 구조가 남아 있으면 걷어낸다.
        // Idle이 기본 상태이면 황소가 첫 프레임에서 굳은 채로 서 있게 된다.
        RemoveState(machine, "Idle");
        RemoveState(machine, "Charge");

        AnimatorState run = FindState(machine, RunState) ?? machine.AddState(RunState, new Vector3(260f, 120f, 0f));
        run.motion = clip;
        run.speed = 1f;

        // 기본 상태 하나뿐이라 파라미터도 전환도 없다. 켜지는 순간부터 계속 반복 재생된다.
        machine.defaultState = run;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        Debug.Log(
            $"{ControllerPath} 를 만들었습니다.\n" +
            $"  상태는 '{RunState}' 하나뿐이며 기본 상태라 시작하자마자 계속 반복 재생됩니다.");

        return controller;
    }

    // 예전 구조를 지운다. 상태에 걸린 전환도 함께 사라진다.
    private static void RemoveState(AnimatorStateMachine machine, string name)
    {
        AnimatorState state = FindState(machine, name);
        if (state != null)
        {
            machine.RemoveState(state);
        }
    }

    private static AnimatorState FindState(AnimatorStateMachine machine, string name)
    {
        foreach (ChildAnimatorState child in machine.states)
        {
            if (child.state != null && child.state.name == name)
            {
                return child.state;
            }
        }

        return null;
    }

    // 씬에 있는 황소의 Animator에 컨트롤러를 물려 준다.
    private static void AssignToBull(AnimatorController controller)
    {
        ChargingBull bull = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponentInChildren<ChargingBull>(true)
            : null;

        if (bull == null)
        {
            ChargingBull[] found = Object.FindObjectsByType<ChargingBull>(FindObjectsSortMode.None);
            if (found.Length == 1)
            {
                bull = found[0];
            }
            else if (found.Length > 1)
            {
                Debug.LogWarning(
                    "씬에 황소가 여럿이라 어느 것에 물릴지 알 수 없습니다. " +
                    $"하이어라키에서 황소를 고른 뒤 다시 실행하거나, Animator의 Controller에 {ControllerPath} 를 직접 넣어 주세요.");
                return;
            }
        }

        if (bull == null)
        {
            Debug.LogWarning(
                "씬에서 황소(ChargingBull)를 찾지 못했습니다. " +
                $"컨트롤러는 만들어 두었으니 황소의 Animator에 {ControllerPath} 를 직접 넣어 주세요.");
            return;
        }

        Animator animator = bull.GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            Debug.LogWarning($"{bull.name}: Animator가 없어 컨트롤러를 물리지 못했습니다.", bull);
            return;
        }

        Undo.RecordObject(animator, "황소 애니메이터 물리기");
        animator.runtimeAnimatorController = controller;

        // 이동은 Rigidbody가 맡는다. 루트 모션을 켜 두면 모델만 따로 앞서 나간다.
        animator.applyRootMotion = false;

        // 화면 밖에서 시작하면 애니메이션이 멈춰 있을 수 있어 항상 돌게 둔다.
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        EditorUtility.SetDirty(animator);
        Debug.Log($"{bull.name}: Animator에 {controller.name} 를 물렸습니다. 씬을 저장해 주세요.", bull);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        int split = path.LastIndexOf('/');
        AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
    }
}
