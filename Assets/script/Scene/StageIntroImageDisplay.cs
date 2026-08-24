using System.Collections;
using UnityEngine;

// 스테이지 시작 시 좌상단에 안내 이미지를 띄운다.
// 처음 보여줄 때는 닫기(X) 버튼 없이 지정된 시간이 지나면 자동으로 사라지고, 그 자리에 다시 보기 버튼이 나타난다.
// 다시 보기 버튼으로 이미지를 다시 띄우면 닫기(X) 버튼이 나타나고, 자동으로 사라지지 않으며
// 플레이어가 직접 닫기 버튼을 눌러야 사라진다.
public class StageIntroImageDisplay : MonoBehaviour
{
    [SerializeField] private GameObject imageRoot;
    [SerializeField] private GameObject reopenButton;
    [SerializeField] private GameObject closeButton;
    [SerializeField] private float displaySeconds = 5f;

    private Coroutine hideRoutine;

    private void Start()
    {
        ShowGuide(showCloseButton: false, autoHide: true);
    }

    // 다시 보기 버튼에 연결된다. 이후로는 닫기(X) 버튼이 나타나고, 자동으로는 닫히지 않는다.
    public void Show()
    {
        ShowGuide(showCloseButton: true, autoHide: false);
    }

    public void Hide()
    {
        CancelHideTimer();
        imageRoot.SetActive(false);
        reopenButton.SetActive(true);
    }

    private void ShowGuide(bool showCloseButton, bool autoHide)
    {
        CancelHideTimer();
        imageRoot.SetActive(true);
        reopenButton.SetActive(false);
        closeButton.SetActive(showCloseButton);
        if (autoHide)
        {
            hideRoutine = StartCoroutine(HideAfterDelay());
        }
    }

    private void CancelHideTimer()
    {
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSecondsRealtime(displaySeconds);
        hideRoutine = null;
        Hide();
    }
}
