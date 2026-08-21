//==============================================================================================
// Using Unity Engine
//==============================================================================================
using System.Collections.Generic;
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
    public int poolSize;
    private List<AudioSource> poolList = new List<AudioSource>();
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
            InitializePool();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else { Destroy(gameObject);}
    }

    //==========================================================================================
    // Function, Start
    //------------------------------------------------------------------------------------------

    void Start(){ ApplySettings(); }


    //==========================================================================================
    // Function, Get the pool we need
    //------------------------------------------------------------------------------------------

    void InitializePool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            //temp
            GameObject temp3D = new GameObject("Pooled3DSound_" + i);
            temp3D.transform.SetParent(this.transform);

            //source
            AudioSource source = temp3D.AddComponent<AudioSource>();
            source.outputAudioMixerGroup = sfxGroup;
            source.spatialBlend = 1.0f;
            // logaruthmic for realistic and linear for straight distance
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 1f;
            source.maxDistance = 50f;

            temp3D.SetActive(false);
            poolList.Add(source);
        }
    }

    //==========================================================================================
    // Function, Play Spatial sound / 3d sound effects
    //------------------------------------------------------------------------------------------

    public void PlaySpatialSounds(AudioClip clip, Vector3 pos, float vol = 1f, float pitch = 1f)
    {

        if (clip == null) { return; }

        if(Camera.main != null)
        {

            float disToPlay = Vector3.Distance(Camera.main.transform.position, pos);
            if (disToPlay > 50f) { return; }

        }

        AudioSource availableSource = GetPooledAudioSource();
        if (availableSource != null)
        {
            availableSource.gameObject.transform.SetParent(null);
            availableSource.gameObject.transform.position = pos;
            availableSource.gameObject.SetActive(true);
            availableSource.clip = clip;
            availableSource.volume = vol;
            availableSource.pitch = pitch;
            availableSource.Play();
            StartCoroutine(DisableSourceAfterPlaying(availableSource, clip.length / pitch));

        }

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

    //==========================================================================================
    // Function, Get the pools audo source
    //------------------------------------------------------------------------------------------

    private AudioSource GetPooledAudioSource()
    {
        for (int i = 0; i < poolList.Count; i++)
        {
            if (!poolList[i].gameObject.activeInHierarchy)
            {
                return poolList[i];
            }
        }
        return null;
    }

    //==========================================================================================
    // Function, Disable source after play
    //------------------------------------------------------------------------------------------

    private System.Collections.IEnumerator DisableSourceAfterPlaying(AudioSource source, float delay)
    {
        yield return new WaitForSeconds(delay);
        source.gameObject.SetActive(false);
        source.gameObject.transform.SetParent(this.transform);
    }

}
//==============================================================================================
// End of Audio Manager CS
//==============================================================================================