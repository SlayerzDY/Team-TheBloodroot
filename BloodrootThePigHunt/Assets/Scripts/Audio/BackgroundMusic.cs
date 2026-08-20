using UnityEngine;

public class BackgroundMusic : MonoBehaviour
{
    private static BackgroundMusic instance;
    private AudioSource audioSource;

    void Awake()
    {
        if (instance != null){ Destroy(gameObject);}
        else
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            audioSource = GetComponent<AudioSource>();
        }
    }

    public void ChangeVolume(float volumeValue){ if (audioSource != null) { audioSource.volume = volumeValue; } }
}
