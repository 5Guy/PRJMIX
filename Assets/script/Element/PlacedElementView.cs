using UnityEngine;

// 맵에 배치된 원소 하나의 모습을 담당한다.
// 탑뷰(2D)일 때는 조합창과 같은 원소 아이콘, 사선뷰(3D)일 때는 원래 모습(구 또는 벽 등)을 보여준다.
public class PlacedElementView : MonoBehaviour
{
    [SerializeField] private float diameterRatio = 0.6f;   // 칸 크기 대비 아이콘 지름

    // 원소 크기를 잴 때 기준으로 삼는 칸 크기(미터).
    //
    // 칸 크기는 스테이지마다 다르다 — 도로 폭을 칸 수로 나눠서 정하므로 2.08m ~ 2.32m다.
    // 그것을 그대로 곱하면 넓은 길에서는 원소 하나가 2m짜리 바위만 해져서 길을 다 가린다.
    // 원소는 "손에 든 것을 길에 내려놓는" 크기여야 하므로, 칸이 아무리 커져도 이 기준(1m)
    // 이상으로는 키우지 않는다. 칸이 이보다 작으면 칸 밖으로 삐져나가지 않게 칸을 따라간다.
    private const float ReferenceCellSize = 1f;

    private Transform round;
    private Transform flat;
    private float diameter;

    // 사선뷰에서 보이는 3D 모습. 놓은 뒤에 이것을 움직이는 연출(떨어지는 나무다리 등)이 쓴다.
    public Transform Round => round;

    // roundVisual을 넘기면(예: 돌 벽) 그 모습을 3D 모습으로 쓰고, 안 넘기면 기본 구를 만든다.
    // 벽처럼 콜라이더가 붙어있는 경우를 대비해, 숨길 때도 GameObject째로 끄지 않고 렌더러만 끈다.
    // diameterRatioOverride를 넘기면(PlacementSystem의 인스펙터 값) 기본 비율 대신 그 값을 쓴다.
    public void Init(ElementData data, float cellSize, Vector3 groundPosition, Transform roundVisual = null, float? diameterRatioOverride = null)
    {
        transform.position = groundPosition;

        if (diameterRatioOverride.HasValue)
        {
            diameterRatio = diameterRatioOverride.Value;
        }

        diameter = Mathf.Min(cellSize, ReferenceCellSize) * diameterRatio;

        round = roundVisual != null ? roundVisual : BuildRound(data);

        // 밖에서 받아 온 모습(함정이 내어 준 흰 폭풍·시멘트 등)은 콜라이더가 없을 수 있다.
        // 기본 구를 만든 경우에는 프리미티브 콜라이더가 딸려 오므로 그대로 둔다.
        if (roundVisual != null)
        {
            EnsurePickCollider(roundVisual);
        }

        flat = BuildFlat(data);

        Apply(CameraViewController.IsTopView);
    }

    private Transform BuildRound(ElementData data)
    {
        // 원소에 3D 모델을 물려 두었으면 그것을 놓는다. 없을 때만 색깔 구로 대신한다.
        Transform model = ElementVisual.CreateWorldModel(data, transform, transform.position, diameter);
        if (model != null)
        {
            return model;
        }

        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "Round";
        sphere.transform.SetParent(transform, false);
        sphere.transform.localPosition = Vector3.up * (diameter * 0.5f);
        sphere.transform.localScale = Vector3.one * diameter;
        sphere.GetComponent<Renderer>().sharedMaterial = WorldVisual.CreateLit(ElementVisual.GetColor(data));

        // 콜라이더는 그대로 둔다 — 오른쪽 클릭으로 회수할 때 이걸로 맞혀야 한다.
        return sphere.transform;
    }

    // 맵에 아무것도 세우지 않는다. 탑뷰의 납작한 아이콘만 남아 어느 칸에 놓았는지는 알 수 있다.
    //
    // 나무다리처럼 "놓아 두는 것 자체는 보이지 않고, 발동할 때 하늘에서 떨어지는" 원소에 쓴다.
    public void ClearRound()
    {
        if (round != null)
        {
            Destroy(round.gameObject);
            round = null;
        }

        // 3D 모습이 없어도 우클릭으로 회수할 수 있어야 한다.
        EnsurePickCollider(transform);
    }

    // 배치한 뒤에 3D 모습을 갈아 끼운다.
    //
    // 바람처럼 "놓는 순간에야 만들어지는 연출"이 곧 그 원소의 모습인 경우에 쓴다.
    // 미리 만들어 둔 기본 구는 치우고, 넘겨받은 것을 3D 모습으로 삼는다.
    public void ReplaceRound(Transform newRound)
    {
        if (newRound == null || newRound == round)
        {
            return;
        }

        if (round != null)
        {
            Destroy(round.gameObject);
        }

        round = newRound;

        // 기본 구에는 콜라이더가 딸려 있어 우클릭 회수가 됐다.
        // 연출로 갈아 끼우면 보통 콜라이더가 없으므로 맞을 곳을 하나 만들어 준다.
        EnsurePickCollider(newRound);

        Apply(CameraViewController.IsTopView);
    }

    // 우클릭 회수는 레이캐스트로 맞히는 것이라 콜라이더가 하나는 있어야 한다.
    //
    // 반드시 "새로 끼운 모습"만 보고 판단해야 한다.
    // 치우기로 한 기본 구는 Destroy를 불러도 이번 프레임 끝에야 사라져서,
    // 오브젝트 전체를 훑으면 곧 없어질 그 콜라이더를 세고 그냥 넘어가 버린다.
    // 그러면 프레임이 끝나는 순간 맞을 곳이 하나도 없는 배치물이 남는다.
    private void EnsurePickCollider(Transform visual)
    {
        if (visual == null || GetComponent<BoxCollider>() != null)
        {
            return;
        }

        // 연출에 콜라이더가 남아 있는지 세지 않고 무조건 하나 붙인다.
        //
        // 연출을 만들면서 프리미티브 콜라이더를 Destroy로 떼어 낸 경우가 많은데,
        // Destroy는 이번 프레임 끝에야 반영되므로 지금 세면 곧 없어질 것을 세게 된다.
        // 그러면 프레임이 끝나는 순간 맞을 곳이 하나도 없는 배치물이 남는다.
        //
        // 트리거로 두면 겹쳐도 아무것도 막지 않는다.
        // 회수 레이캐스트는 QueryTriggerInteraction.Collide로 쏘므로 그대로 맞는다.
        BoxCollider box = gameObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = Vector3.up * (diameter * 0.5f);
        box.size = Vector3.one * diameter;
    }

    // 조합창 아이콘과 같은 스프라이트를 바닥에 눕혀서 위에서 보면 아이콘처럼 보이게 한다.
    private Transform BuildFlat(ElementData data)
    {
        return ElementVisual.CreateFlatIcon(data, transform, transform.position + Vector3.up * 0.02f, diameter);
    }

    private void OnEnable()
    {
        CameraViewController.TopViewChanged += Apply;
    }

    private void OnDisable()
    {
        CameraViewController.TopViewChanged -= Apply;
    }

    private void Apply(bool topView)
    {
        SetRenderersEnabled(round, !topView);

        if (flat != null)
        {
            flat.gameObject.SetActive(topView);
        }
    }

    // GameObject를 통째로 끄면 벽에 붙은 콜라이더(자동차 충돌 감지)까지 꺼지므로, 렌더러만 끈다.
    private static void SetRenderersEnabled(Transform root, bool enabled)
    {
        if (root == null)
        {
            return;
        }

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = enabled;
        }
    }
}
