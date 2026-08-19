//==============================================================================================
// Using Unity Engine
//==============================================================================================
using UnityEngine;
using System.Collections;
using Unity.VisualScripting;
//==============================================================================================
// Instructions for Using the Dissolve Script
//==============================================================================================
/* 
 * Instructions for using the Dissolver Script:
 * 1.) Attach the Dissolver script to the GameObject you want to dissolve.
 * 2.) Assign the dissolveMaterial in the Inspector.
 * 3.) Assign the Renderer of the model you want to dissolve in the Inspector.
 * 4.) Assign the dissolveDuration in the Inspector (default is 10 seconds).
 * 5.) Assign the starting dissolve strength with dissolveStrength in the Inspector (default is 0).
 * 6.) Assign the flashDuration, remember the duration is doubled for the flash effect, so if you want a 1 second flash, set it to 0.5 seconds.
 * 7.) Assign the flash Start Strength the default is 0
 * 8.) Assign the flash End Strength the default is 0.3
 * 9.) To call the dissolve effect use the following code in another script:
 * To dissolve and delete the Game Object, Use the following code:
 * if (this.GetComponent<Dissolver>() != null) { this.GetComponent<Dissolver>().StartCoroutine(this/GetComponent<Dissolver>().dissolve()); }
 * To dissolve and keep the Game Object, Use the following code:
 * if (this.GetComponent<Dissolver>() != null) { this.GetComponent<Dissolver>().StartCoroutine(this.GetComponent<Dissolver>().dissolveFlash()); }
*/
//==============================================================================================
// Declare Dissolver
//==============================================================================================
public class Dissolver : MonoBehaviour
{
    //==========================================================================================
    // Declare Variables
    //==========================================================================================
    [SerializeField] Material dissolveMaterial;
    [SerializeField] Renderer model;
    [SerializeField] float dissolveDuration = 10f;
    [SerializeField] float dissolveStrength = 0;
    [SerializeField] float flashDuration = 0.5f;
    [SerializeField] float flashStartStrength = 0f;
    [SerializeField] float flashEndStrength = 0.3f;
    private Color colorOrig;
    private Material localDissolveMat;
    private int flashToken = 0;
    //==========================================================================================
    // Function, Start
    //==========================================================================================
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        colorOrig = model.material.color;
        if (dissolveMaterial != null)
        {
            localDissolveMat = new Material(dissolveMaterial);
            localDissolveMat.SetColor("_Color", colorOrig);
        }
    }
    void OnDestroy()
    {
        if (localDissolveMat != null)
        {
            Destroy(localDissolveMat);
        }
    }
    //==========================================================================================
    // Function, dissolve
    //==========================================================================================
    public IEnumerator dissolve(bool playerDeath = false) {
        flashToken++;
        Renderer[] allRenderers = GetComponentsInChildren<Renderer>();
        for (int i = 0; i < allRenderers.Length; i++) {
            allRenderers[i].sharedMaterial = localDissolveMat;
        }
        float elapsedTime = 0;
        localDissolveMat.SetColor("_Color", colorOrig);
        while (elapsedTime < dissolveDuration) {
            elapsedTime += Time.deltaTime;
            dissolveStrength = Mathf.Lerp(0f, 1f, elapsedTime / dissolveDuration);
            localDissolveMat.SetFloat("_DissolveStrength", dissolveStrength);
            yield return null;
        }
        Destroy(gameObject);
    }
    //==========================================================================================
    // Function, dissolve return
    //==========================================================================================
    public IEnumerator dissolveReturn(bool playerDeath = false) {
        Renderer[] allRenderers = GetComponentsInChildren<Renderer>();
        Material[][] originalMaterials = new Material[allRenderers.Length][];
        for (int i = 0; i < allRenderers.Length; i++) {
            originalMaterials[i] = allRenderers[i].sharedMaterials;
            Material[] dissolveSetup = new Material[originalMaterials[i].Length];
            for (int j = 0; j < dissolveSetup.Length; j++) {
                dissolveSetup[j] = localDissolveMat;
            }
            allRenderers[i].sharedMaterials = dissolveSetup;
        }
        if (playerDeath) {
            StartCoroutine(dissolveFlash(true));
            yield break;
        }
        for (int i = 0; i < allRenderers.Length; i++) {
            allRenderers[i].sharedMaterial = localDissolveMat;
        }
        float elapsedTime = 0;
        localDissolveMat.SetColor("_Color", colorOrig);
        while (elapsedTime < dissolveDuration) {
            elapsedTime += Time.deltaTime;
            dissolveStrength = Mathf.Lerp(1f, 0f, elapsedTime / dissolveDuration);
            localDissolveMat.SetFloat("_DissolveStrength", dissolveStrength);
            yield return null;
        }
        dissolveStrength = 0f;
        // Restore the original materials back
        for (int i = 0; i < allRenderers.Length; i++) {
            allRenderers[i].sharedMaterials = originalMaterials[i];
            localDissolveMat.SetFloat("_DissolveStrength", dissolveStrength);
        }
    }
    //==========================================================================================
    // Function, dissolveFlash
    //==========================================================================================
    public IEnumerator dissolveFlash(bool playerDeath = false) {
        if (flashToken > 0) { yield break; }
        flashToken++;
        float cacheflashEndStrength = gameManager.instance.player.GetComponent<Dissolver>().flashEndStrength;
        if (playerDeath) { gameManager.instance.player.GetComponent<Dissolver>().flashEndStrength = 1f; }
        Renderer[] allRenderers = GetComponentsInChildren<Renderer>();
        Material[][] originalMaterials = new Material[allRenderers.Length][];
        for (int i = 0; i < allRenderers.Length; i++) {
            originalMaterials[i] = allRenderers[i].sharedMaterials;
            Material[] dissolveSetup = new Material[originalMaterials[i].Length];
            for (int j = 0; j < dissolveSetup.Length; j++) {
                dissolveSetup[j] = localDissolveMat;
            }
            allRenderers[i].sharedMaterials = dissolveSetup;
        }
        localDissolveMat.SetColor("_Color", colorOrig);
        float elapsedTime = 0;
        // Flash In
        while (elapsedTime < flashDuration) {
            elapsedTime += Time.deltaTime;
            dissolveStrength = Mathf.Lerp(flashStartStrength, flashEndStrength, elapsedTime / flashDuration);
            localDissolveMat.SetFloat("_DissolveStrength", dissolveStrength);
            yield return null;
        }
        // Flash Out
        elapsedTime = 0;
        while (elapsedTime < flashDuration) {
            elapsedTime += Time.deltaTime;
            dissolveStrength = Mathf.Lerp(flashEndStrength, flashStartStrength, elapsedTime / flashDuration);
            localDissolveMat.SetFloat("_DissolveStrength", dissolveStrength);
            yield return null;
        }
        dissolveStrength = 0f;
        localDissolveMat.SetFloat("_DissolveStrength", dissolveStrength);
        // Restore the original materials back
        for (int i = 0; i < allRenderers.Length; i++) {
            allRenderers[i].sharedMaterials = originalMaterials[i];
        }
        gameManager.instance.player.GetComponent<Dissolver>().flashEndStrength = cacheflashEndStrength;
        flashToken = 0;
    }
    //==========================================================================================
}
//==============================================================================================
// End of Dissolver .cs
//==============================================================================================