using System;
using System.Collections.Generic;
using UnityEngine;



public class ScreecherAI  : MonoBehaviour 
{
    [SerializeField] float alertRadius;
    [SerializeField] float screamCooldown = 5f;
    float screamTimer;
    public GameObject screamVFX;


     void Update()
    {
       
        if (screamTimer > 0)
     
            screamTimer -= Time.deltaTime;
    }

    void AlertNearby()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, alertRadius);
        foreach (Collider collider in hits)
        {   // Only alert enemies that have enemyAI
            enemyAI ai = collider.GetComponent<enemyAI>();
            if (ai != null && ai.gameObject != this.gameObject)
            {
                ai.Alert(transform.position);
            }
        }
    }
    void SpawnVFX()
    {
        if (screamVFX != null)
            Instantiate(screamVFX, transform.position, Quaternion.identity);
    }
    public bool CanScream()
    {
        return screamTimer <= 0f;
    }

    // Call this when the screecher scream
    public void Scream()
    {
        if(screamTimer > 0)
        {
            return;
        }
        screamTimer = screamCooldown;

        AlertNearby();
        SpawnVFX();
    }
}
