using UnityEngine;

[CreateAssetMenu(
    fileName = "NewElement",
    menuName = "Game/Element/Element Data"
)]
public class ElementData : ScriptableObject
{
    [Header("기본 정보")]
    [SerializeField] private string elementName;
    [SerializeField] private ElementType elementType;
    [SerializeField] private bool isBaseElement;   // 물/불/나무/흙/철처럼 스테이지 시작부터 목록에 뜨는 기본 원소인지

    [Header("Score")]
    [Tooltip("이 원소를 조합으로 만들었을 때 오르는 점수. 엑셀 '조합식' 시트의 점수 칸과 같다")]
    [SerializeField] private int score;
    [Header("UI")]
    [SerializeField] private Sprite icon;

    [TextArea]
    [SerializeField] private string description;

    [Header("월드")]
    [Tooltip("맵에 놓았을 때 보여 줄 3D 모델(fbx나 프리팹). " +
             "비워두면 원소 색 구가 대신 놓인다. 크기는 칸에 맞춰 자동으로 줄인다")]
    [SerializeField] private GameObject worldModel;

    [Tooltip("칸 크기에 맞춘 다음 한 번 더 곱하는 배율. 1이면 칸에 꽉 차게 놓인다. " +
             "산처럼 우뚝해야 하는 것은 크게, 녹처럼 작아야 하는 것은 작게 잡는다")]
    [SerializeField] private float worldScale = 1f;

    public string ElementName => elementName;
    public ElementType ElementType => elementType;
    public bool IsBaseElement => isBaseElement;
    public int Score => score;
    public Sprite Icon => icon;
    public string Description => description;
    public GameObject WorldModel => worldModel;

    // 이 값이 생기기 전에 만든 에셋에는 칸이 비어 0으로 읽힌다. 그때는 예전처럼 칸 크기 그대로 놓는다.
    public float WorldScale => worldScale > 0.01f ? worldScale : 1f;
}
