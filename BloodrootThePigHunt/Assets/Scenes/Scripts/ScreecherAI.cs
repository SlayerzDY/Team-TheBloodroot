using UnityEngine;
using System.Collections.Generic;

public class ScreecherAI  : MonoBehaviour 
{
    [SerializeField] float alertRadius;

    void AlertNearby()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, alertRadius);
        foreach (Collider collider in hits)
        {
            EnemyAI ai = collider.GetComponent<EnemyAI>();
            if (ai != null && ai != this)
            {
                ai.Alert(transform.position);
            }
        }
    }

    public void Scream()
    {
        AlertNearby();
    }
}
