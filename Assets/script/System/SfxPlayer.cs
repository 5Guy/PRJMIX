using UnityEngine;

// 인스펙터에 꽂아 둔 AudioClip을 한 번씩 울리는 공용 헬퍼.
// 클립을 비워 두면 아무 소리도 나지 않으므로, 사운드 에셋을 아직 안 넣었어도 그대로 동작한다.
public static class SfxPlayer
{
    // 위치가 있는 소리(자동차 충돌 등). 카메라와의 거리에 따라 들린다.
    public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip == null || volume <= 0f)
        {
            return;
        }

        AudioSource.PlayClipAtPoint(clip, position, Mathf.Clamp01(volume));
    }

    // 화면 어디서 나든 똑같이 들려야 하는 소리(UI 등)는 리스너 위치에서 울린다.
    public static void PlayUI(AudioClip clip, float volume = 1f)
    {
        if (clip == null || volume <= 0f)
        {
            return;
        }

        AudioListener listener = Object.FindFirstObjectByType<AudioListener>();
        Vector3 position = listener != null ? listener.transform.position : Vector3.zero;
        AudioSource.PlayClipAtPoint(clip, position, Mathf.Clamp01(volume));
    }
}
