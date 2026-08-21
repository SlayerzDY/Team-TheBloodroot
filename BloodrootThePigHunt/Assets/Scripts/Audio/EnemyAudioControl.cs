using UnityEngine;

public class EnemyAudioControl : MonoBehaviour
{

    public AudioClip[] movementSound;
    public AudioClip[] idleSound;
    public AudioClip[] actionSound;
    public AudioClip[] deathSound;

    //==========================================================================================
    // Declare Functions
    //==========================================================================================
    // Function, Play Movement Sounds
    //------------------------------------------------------------------------------------------

    public void PlayMovementSound()  { PlaySound(movementSound, 0.7f, 1.0f, 0.85f, 1.15f); }

    //==========================================================================================
    // Function, Play Action Sounds
    //------------------------------------------------------------------------------------------

    public void PlayActionSound() { PlaySound(actionSound, 0.8f, 1.0f, 0.95f, 1.05f); }

    //==========================================================================================
    // Function, Play Idle Sounds
    //------------------------------------------------------------------------------------------

    public void PlayIdleSound() { PlaySound(idleSound, 0.5f, 0.8f, 0.9f, 1.1f); }

    //==========================================================================================
    // Function, Play Death Sounds
    //------------------------------------------------------------------------------------------

    public void PlayDeathSound() { PlaySound(deathSound, 0.9f, 1.0f, 0.9f, 1.1f); }

    //==========================================================================================
    // Function, Play all sounds
    //------------------------------------------------------------------------------------------

    private void PlaySound(AudioClip[] sound, float minVol, float maxVol, float minPitch, float maxPitch)
    {

        if(sound == null || sound.Length == 0 || audioManager.instance == null) {  return; }
        AudioClip currendSound = sound[Random.Range(0, sound.Length)];
        float randomVol = Random.Range(minVol, maxVol);
        float randomPit = Random.Range(minPitch, maxPitch);
        audioManager.instance.PlaySpatialSounds(currendSound, transform.root.position, randomVol, randomPit);

    }
    
}
