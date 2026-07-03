//==============================================================================================
// Using Unity Engine
//==============================================================================================
using UnityEngine;
using System.Collections;
using UnityEditor;
//==============================================================================================
// Declare Enemy AI
//==============================================================================================
public class EnemyAI : MonoBehaviour, IDamage {
    //==========================================================================================
    // Declare Variables
    //==========================================================================================
    [SerializeField] int HP;
    [SerializeField] Renderer model;
    [SerializeField] MobSpawner spawner;
    Color colorOrig;
    //==========================================================================================
    // Function, Start
    //==========================================================================================
    void Start() {
        colorOrig = model.material.color;

        spawner = FindAnyObjectByType<MobSpawner>();
    }
    //==========================================================================================
    // Function, Update
    //==========================================================================================
    void Update() {
        
    }
    //==========================================================================================
    // Function, TakeDamage
    //==========================================================================================
    public void TakeDamage(int amount) {
        HP -= amount;
        if (HP <= 0) {
            if (GetComponent<Dissolver>() != null) { GetComponent<Dissolver>().StartCoroutine(GetComponent<Dissolver>().dissolve()); }
        } else {
            StartCoroutine(flashRed());
        }
    }
    //==========================================================================================
    // Function, TakeDamage
    //==========================================================================================
    IEnumerator flashRed()
    {
        //model.material.color = Color.red;
        //yield return new WaitForSeconds(0.1f);
        //model.material.color = colorOrig;
        if (GetComponent<Dissolver>() != null) { GetComponent<Dissolver>().StartCoroutine(GetComponent<Dissolver>().dissolveFlash()); }
        yield return null;
    }

    public void onDeath(bool dead)
    {

        if (dead == true)
        {

            
        }
    }
    //==========================================================================================
}
//==============================================================================================
// End of Enemy AI .cs
//==============================================================================================