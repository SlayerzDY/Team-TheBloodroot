using UnityEngine;

public class AnimatorSoundBridge : MonoBehaviour
{
    private EnemyAudioControl mainAudio;

    void Start(){ mainAudio = GetComponentInParent<EnemyAudioControl>(); }

    public void PlayMovementSound() => mainAudio.PlayMovementSound();

    public void PlayIdleSound() => mainAudio.PlayIdleSound();

    public void PlayActionSound() => mainAudio.PlayActionSound();
}
