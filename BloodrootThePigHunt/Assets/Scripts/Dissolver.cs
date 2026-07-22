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
 * if (GetComponent<Dissolver>() != null) { GetComponent<Dissolver>().StartCoroutine(GetComponent<Dissolver>().dissolve()); }
 * To dissolve and keep the Game Object, Use the following code:
 * if (GetComponent<Dissolver>() != null) { GetComponent<Dissolver>().StartCoroutine(GetComponent<Dissolver>().dissolveFlash()); }
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
    //==========================================================================================
    // Function, Start
    //==========================================================================================
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        colorOrig = model.material.color;
    }
    //==========================================================================================
    // Function, dissolve
    //==========================================================================================
    public IEnumerator dissolve(bool playerDeath = false) {
        if (playerDeath) {
            StartCoroutine(dissolveFlash(true));
            yield break;
        }
        Renderer[] allRenderers = GetComponentsInChildren<Renderer>();
        for (int i = 0; i < allRenderers.Length; i++) {
            allRenderers[i].sharedMaterial = dissolveMaterial;
        }
        float elapsedTime = 0;
        dissolveMaterial.SetColor("_Color", colorOrig);
        while (elapsedTime < dissolveDuration) {
            elapsedTime += Time.deltaTime;
            dissolveStrength = Mathf.Lerp(0f, 1f, elapsedTime / dissolveDuration);
            dissolveMaterial.SetFloat("_DissolveStrength", dissolveStrength);
            yield return null;
        }
        Destroy(gameObject);
    }
    //==========================================================================================
    // Function, dissolveFlash
    //==========================================================================================
    public IEnumerator dissolveFlash(bool playerDeath = false) {
        float cacheflashEndStrength = gameManager.instance.player.GetComponent<Dissolver>().flashEndStrength;
        if (playerDeath) { gameManager.instance.player.GetComponent<Dissolver>().flashEndStrength = 1f; }
        Renderer[] allRenderers = GetComponentsInChildren<Renderer>();
        Material[][] originalMaterials = new Material[allRenderers.Length][];
        for (int i = 0; i < allRenderers.Length; i++) {
            originalMaterials[i] = allRenderers[i].sharedMaterials;
            Material[] dissolveSetup = new Material[originalMaterials[i].Length];
            for (int j = 0; j < dissolveSetup.Length; j++) {
                dissolveSetup[j] = dissolveMaterial;
            }
            allRenderers[i].sharedMaterials = dissolveSetup;
        }
        dissolveMaterial.SetColor("_Color", colorOrig);
        float elapsedTime = 0;
        // Flash In
        while (elapsedTime < flashDuration) {
            elapsedTime += Time.deltaTime;
            dissolveStrength = Mathf.Lerp(flashStartStrength, flashEndStrength, elapsedTime / flashDuration);
            dissolveMaterial.SetFloat("_DissolveStrength", dissolveStrength);
            yield return null;
        }
        // Flash Out
        elapsedTime = 0;
        while (elapsedTime < flashDuration) {
            elapsedTime += Time.deltaTime;
            dissolveStrength = Mathf.Lerp(flashEndStrength, flashStartStrength, elapsedTime / flashDuration);
            dissolveMaterial.SetFloat("_DissolveStrength", dissolveStrength);
            yield return null;
        }
        dissolveStrength = 0f;
        dissolveMaterial.SetFloat("_DissolveStrength", dissolveStrength);
        // Restore the original materials back
        for (int i = 0; i < allRenderers.Length; i++) {
            allRenderers[i].sharedMaterials = originalMaterials[i];
        }
        gameManager.instance.player.GetComponent<Dissolver>().flashEndStrength = cacheflashEndStrength;
    }
    //==========================================================================================
}
//==============================================================================================
// End of Dissolver .cs
//==============================================================================================