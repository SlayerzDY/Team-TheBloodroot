using UnityEngine;

public class AnimatorSoundBridge : MonoBehaviour
{
    private EnemyAudioControl mainAudio;

    void Start(){ mainAudio = GetComponentInParent<EnemyAudioControl>(); }

    public void PlayMovementSound() => mainAudio.PlayMovementSound();

    public void PlayIdleSound() => mainAudio.PlayIdleSound();

    public void PlayActionSound() => mainAudio.PlayActionSound();

    public void PlayHurtSound() => mainAudio.PlayHurtSound();

    public void PlayReloadSound() => mainAudio.PlayReloadSound();

    public void StopEnemySounds()
    {
        if (audioManager.instance == null) { return; }
        if (mainAudio == null) { return; }
        if (mainAudio != null && audioManager.instance != null) { audioManager.instance.StopAllSoundsOnObject(transform.root);}
    }
}
