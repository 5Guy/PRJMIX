using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 로봇의 표정을 화면 오른쪽 위에 계속 띄워 둔다.
// - 씬이 시작하면 neutral을 띄우고, 스테이지가 끝날 때까지 항상 떠 있는다(조합창이 열려 있어도 그대로 유지).
// - CraftingPanelUI에서 원소 조합에 성공/실패하면(정적 이벤트) 잠깐 successful/failed로 바뀌었다가 다시 neutral로 돌아온다.
// - StageGoalFlag의 onPlayerReached에 ShowSuccessful()을 연결하면 스테이지 클리어 표정이 뜬다.
// - StageFailPanel이 열리면(정적 이벤트) 자동으로 failed 표정이 뜬다.
public class FaceUpDisplay : MonoBehaviour
{
    [Header("표시 대상")]
    [SerializeField] private Image faceImage;
    [Tooltip("표정 이미지를 감싸는 테두리. 비워두면 테두리 없이 표정만 켜고 끈다")]
    [SerializeField] private Image frameImage;

    [Header("표정 이미지")]
    [SerializeField] private Sprite neutralSprite;
    [SerializeField] private Sprite successfulSprite;
    [SerializeField] private Sprite failedSprite;

    [Header("표시 시간")]
    [Tooltip("successful 표정을 보여준 뒤 neutral로 돌아오기까지 걸리는 시간(초)")]
    [SerializeField] private float successfulDuration = 2f;
    [Tooltip("failed 표정을 보여준 뒤 neutral로 돌아오기까지 걸리는 시간(초)")]
    [SerializeField] private float failedDuration = 2f;

    private Coroutine revertRoutine;

    private void OnEnable()
    {
        StageFailPanel.PanelOpened += ShowFailed;
        CraftingPanelUI.CombineSucceeded += ShowSuccessful;
        CraftingPanelUI.CombineFailed += ShowFailed;
    }

    private void OnDisable()
    {
        StageFailPanel.PanelOpened -= ShowFailed;
        CraftingPanelUI.CombineSucceeded -= ShowSuccessful;
        CraftingPanelUI.CombineFailed -= ShowFailed;
    }

    private void Start()
    {
        ShowNeutral();
    }

    // 스테이지가 유지되는 동안(조합창이 열려 있을 때 포함) 기본으로 떠 있는 표정. 사라지지 않는다.
    public void ShowNeutral()
    {
        CancelRevert();
        Show(neutralSprite);
    }

    public void ShowSuccessful()
    {
        Show(successfulSprite);
        ScheduleRevert(successfulDuration);
    }

    public void ShowFailed()
    {
        Show(failedSprite);
        ScheduleRevert(failedDuration);
    }

    private void Show(Sprite sprite)
    {
        if (faceImage == null || sprite == null)
        {
            return;
        }

        faceImage.sprite = sprite;
        faceImage.enabled = true;

        if (frameImage != null)
        {
            frameImage.enabled = true;
        }
    }

    private void ScheduleRevert(float delay)
    {
        CancelRevert();
        revertRoutine = StartCoroutine(RevertToNeutralAfterDelay(delay));
    }

    private void CancelRevert()
    {
        if (revertRoutine != null)
        {
            StopCoroutine(revertRoutine);
            revertRoutine = null;
        }
    }

    private IEnumerator RevertToNeutralAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        revertRoutine = null;
        Show(neutralSprite);
    }
}
