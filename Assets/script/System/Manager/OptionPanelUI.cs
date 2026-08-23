using UnityEngine;
using UnityEngine.UI;

public class OptionPanelUI : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private Slider volumeSlider;

    private void Start()
    {
        if (AudioManager.Instance == null)
        {
            Debug.LogWarning("AudioManager가 존재하지 않습니다.");
            return;
        }

        volumeSlider.value = AudioManager.Instance.GetMasterVolume();

        volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
    }

    private void OnVolumeChanged(float value)
    {
        AudioManager.Instance.SetMasterVolume(value);
    }

    private void OnDestroy()
    {
        volumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
    }
}
