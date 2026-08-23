using UnityEngine;

public class ButtonClickSound : MonoBehaviour
{
    [Header("Sound")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip clickClip;

    public void PlayClick()
    {
        if (audioSource == null || clickClip == null) return;
        audioSource.PlayOneShot(clickClip);
    }
}