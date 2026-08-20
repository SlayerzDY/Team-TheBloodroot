//==============================================================================================
// Using Unity Engine
//==============================================================================================
using UnityEngine;
using UnityEngine.Audio;
//==============================================================================================
// Declare Audio Manager
//==============================================================================================
public class audioManager : MonoBehaviour
{
    //==========================================================================================
    // Declare Variables
    //==========================================================================================
    public static audioManager instance;
    public AudioMixer mainMix;
    public AudioMixerGroup sfxGroup;
    public AudioMixerGroup musicGroup;
    public AudioSource audPlayer;
    public AudioSource musicSpace;
    //==========================================================================================
    // Declare Functions
    //==========================================================================================
    // Function, Awake
    //------------------------------------------------------------------------------------------
    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else { Destroy(gameObject);}
    }

    //==========================================================================================
    // Function, Start
    //------------------------------------------------------------------------------------------

    void Start(){ ApplySettings(); }

    //==========================================================================================
    // Function, Play Spatial sound / 3d sound effects
    //------------------------------------------------------------------------------------------

    public void PlaySpatialSounds(AudioClip clip, Vector3 pos)
    {

        if(clip == null) {return;}

        // tmep 
        GameObject temp3D = new GameObject("Temp3DSounds");
        temp3D.transform.position = pos;

        //source
        AudioSource source = temp3D.AddComponent<AudioSource>();
        source.clip = clip;
        source.outputAudioMixerGroup = sfxGroup;
        source.spatialBlend = 1.0f;
        // logaruthmic for realistic and linear for straight distance
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = 1f;
        source.maxDistance = 20f;
        source.Play();

        // I am become death desstroyer of worlds
        Destroy(temp3D, clip.length);

    }

    //==========================================================================================
    // Function, Switch Music track
    //------------------------------------------------------------------------------------------

    public void SwitchBackMuic(AudioClip newTrack)
    {

        if(musicSpace == null || musicSpace.clip == newTrack) {return;}
        musicSpace.Stop();
        musicSpace.clip = newTrack;
        musicSpace.loop = true;
        musicSpace.Play();

    }

    //==========================================================================================
    // Function, Apply Saved Settings
    //------------------------------------------------------------------------------------------

    public void ApplySettings()
    {

        if(mainMix != null)
        {

            //sfx
            float savedSFX = PlayerPrefs.GetFloat("SFXVolumeSave", 0.4f);
            float dbSFX = Mathf.Log10(Mathf.Max(0.0001f, savedSFX)) * 20;
            mainMix.SetFloat("SFXVol", dbSFX);

            // mussic
            float savedMussic = PlayerPrefs.GetFloat("MusicVolumeSave", 0.4f);
            float dbMusic = Mathf.Log10(Mathf.Max(0.0001f, savedMussic)) * 20;
            mainMix.SetFloat("MusicVol", dbSFX);

        }

    }

    //==========================================================================================
    // Function, When A new Scenee loads
    //------------------------------------------------------------------------------------------

    void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) {ApplySettings(); }

    //==========================================================================================
    // Function, Cleeeanup
    //------------------------------------------------------------------------------------------

    void OnDestroy() { UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded; }

}
//==============================================================================================
// End of Audio Manager CS
//==============================================================================================