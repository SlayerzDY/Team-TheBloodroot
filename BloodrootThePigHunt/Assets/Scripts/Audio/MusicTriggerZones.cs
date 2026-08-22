using UnityEngine;

public class MusicTriggerZones : MonoBehaviour
{
   
    public AudioClip zoneMusic;
    //just in case need ambiencee with music
   // public AudioClip zoneAmbience;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (audioManager.instance != null)
            {
                // just in case we neeed ambience
                audioManager.instance.SwitchBackMuic(zoneMusic);
                //audioManager.instance.SwitchAmbience(zoneAmbience);
            }
        }
    }
}
