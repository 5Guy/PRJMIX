using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer audioMixer;

    private const string MasterVolume = "MasterVolume";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // SystemManager 프리팹 안에서는 AudioManager가 자식 오브젝트라 DontDestroyOnLoad가
        // 조용히 무시된다("DontDestroyOnLoad only works for root GameObjects..." 경고).
        // 그러면 씬을 옮길 때마다 Instance가 파괴돼 옵션창이 AudioManager를 못 찾고,
        // 볼륨 슬라이더를 움직여도 리스너가 안 걸려 조절이 먹통이 된다.
        // 부모에서 떼어내 루트로 만들어야 실제로 살아남는다.
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        LoadVolume();
    }

    public void SetMasterVolume(float volume)
    {
        float db = Mathf.Log10(Mathf.Max(volume, 0.0001f)) * 20f;

        audioMixer.SetFloat(MasterVolume, db);

        PlayerPrefs.SetFloat(MasterVolume, volume);
        PlayerPrefs.Save();
    }

    public float GetMasterVolume()
    {
        return PlayerPrefs.GetFloat(MasterVolume, 1f);
    }

    private void LoadVolume()
    {
        float volume = PlayerPrefs.GetFloat(MasterVolume, 1f);

        float db = Mathf.Log10(Mathf.Max(volume, 0.0001f)) * 20f;

        audioMixer.SetFloat(MasterVolume, db);
    }
}
