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
    // public vari
    public static audioManager instance;
    public AudioMixer mainMix;
    public AudioMixerGroup sfxGroup;
    public AudioMixerGroup musicGroup;
    public AudioSource audPlayer;
    public AudioSource musicSpace;
    public AudioClip mainMenuMusic;
    public AudioClip hubMusic;
    public AudioClip openWorldDefaultMusic;
    public int poolSize;
    //private variablees
    private List<AudioSource> poolList = new List<AudioSource>();
    private Coroutine musicFadeCoroutine;
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
    // Function, Stop Pool items a
    //------------------------------------------------------------------------------------------

    public void StopAllSoundsOnObject(Transform enemyTransform)
    {
        if (enemyTransform == null) { return; }

        for (int i = 0; i < poolList.Count; i++)
        {
            if (poolList[i].gameObject.activeInHierarchy)
            {
                if (poolList[i].transform.parent == enemyTransform || poolList[i].transform.IsChildOf(enemyTransform))
                {
                    poolList[i].Stop();
                    poolList[i].gameObject.SetActive(false);
                    poolList[i].transform.SetParent(this.transform);
                }
            }
        }
    }
    //==========================================================================================
    // Function, Play Spatial sound / 3d sound effects
    //------------------------------------------------------------------------------------------

    public void PlaySpatialSounds(AudioClip clip, Transform enemyPos, float vol = 1f, float pitch = 1f)
    {

        if (clip == null) { return; }

        if(Camera.main != null)
        {

            float disToPlay = Vector3.Distance(Camera.main.transform.position, enemyPos.position);
            if (disToPlay > 50f) { return; }

        }

        AudioSource availableSource = GetPooledAudioSource();
        if (availableSource != null)
        {
            availableSource.gameObject.transform.SetParent(enemyPos);
            availableSource.gameObject.transform.localPosition = Vector3.zero;
            availableSource.gameObject.SetActive(true);
            availableSource.clip = clip;
            availableSource.volume = vol;
            availableSource.pitch = pitch;
            availableSource.Play();
            StartCoroutine(DisableSourceAfterPlaying(availableSource, clip.length / pitch));

        }

    }

    //==========================================================================================
    // Function, Enable and disable
    //------------------------------------------------------------------------------------------

    void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    //==========================================================================================
    // Function, Fadee Music track
    //------------------------------------------------------------------------------------------

    private System.Collections.IEnumerator FadeMusicTransition(AudioClip newTrack, float fadeTime)
    {
        float targetMaxVolume = 0.5f;
        if (musicSpace.clip != null && musicSpace.isPlaying)
        {
            float startVolume = musicSpace.volume;
            while (musicSpace.volume > 0)
            {
                musicSpace.volume -= startVolume * (Time.deltaTime / fadeTime);
                yield return null;
            }
        }

        musicSpace.Stop();
        musicSpace.clip = newTrack;
        musicSpace.outputAudioMixerGroup = musicGroup;
        musicSpace.spatialBlend = 0.0f;
        musicSpace.loop = true;

        if (newTrack != null)
        {
            musicSpace.Play();
            musicSpace.volume = 0f;
            while (musicSpace.volume < targetMaxVolume)
            {
                musicSpace.volume += targetMaxVolume * (Time.deltaTime / fadeTime);
                yield return null;
            }
            musicSpace.volume = targetMaxVolume;
        }
    }

    //==========================================================================================
    // Function, Switch Music track
    //------------------------------------------------------------------------------------------

    public void SwitchBackMuic(AudioClip newTrack)
    {

        if (musicSpace == null) { return; }
        if (musicSpace.clip == newTrack && musicSpace.isPlaying) { return; }
        if (musicFadeCoroutine != null) { StopCoroutine(musicFadeCoroutine); }
        musicFadeCoroutine = StartCoroutine(FadeMusicTransition(newTrack, 1.5f));

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
            mainMix.SetFloat("MusicVol", dbMusic);

        }

    }

    //==========================================================================================
    // Function, When A new Scenee loads
    //------------------------------------------------------------------------------------------

    void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) {
       
        ApplySettings();
        if (scene.name == "MainMenu"){  SwitchBackMuic(mainMenuMusic); }
        else if (scene.name == "Farm_PrologueHub") {  SwitchBackMuic(hubMusic); }
        else if (scene.name == "Bloodroot_OpenWorld") {  SwitchBackMuic(openWorldDefaultMusic);}

    }

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
        yield return new WaitForSecondsRealtime(delay);
        if (source != null && source.gameObject != null)
        {
            source.Stop(); 
            source.gameObject.SetActive(false); 
            if (this != null && transform != null) {source.gameObject.transform.SetParent(this.transform); }
        }
    }

    //==========================================================================================
    // Function, Set the volume to pause
    //------------------------------------------------------------------------------------------

    public void SetPauseMute(bool isPaused)
    {
        if (mainMix == null) { return; }
        if (isPaused)
        {
            mainMix.SetFloat("MusicVol", -80f);
            mainMix.SetFloat("SFXVol", -80f);
        }
        else { ApplySettings();}
    }

}
//==============================================================================================
// End of Audio Manager CS
//==============================================================================================