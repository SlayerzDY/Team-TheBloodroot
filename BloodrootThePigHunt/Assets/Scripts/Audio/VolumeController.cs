using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class VolumeController : MonoBehaviour
{
    public AudioMixer mainMixer;
    public Slider sfxSlide;
    public Slider musicSlide;

    void Start()
    {
        if(sfxSlide != null) { sfxSlide.value = PlayerPrefs.GetFloat("SFXVolumeSave", 0.4f); }
        if(musicSlide != null) { musicSlide.value = PlayerPrefs.GetFloat("MusicVolumeSave", 0.4f); }

        sfxSlide.onValueChanged.AddListener(val => SetVolume("SFXVol", "SFXVolumeSave", val));
        musicSlide.onValueChanged.AddListener(val => SetVolume("MusicVol", "MusicVolumeSave", val));
    }

    void SetVolume(string parameterName, string prefKey, float sliderValue)
    {
        float dbValue = Mathf.Log10(Mathf.Max(0.0001f, sliderValue)) * 20;
        mainMixer.SetFloat(parameterName, dbValue);
        PlayerPrefs.SetFloat(prefKey, sliderValue);
        PlayerPrefs.Save();
    }
}
