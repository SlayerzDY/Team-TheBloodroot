using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public class ScreecherAI  : MonoBehaviour 
{
    [SerializeField] float alertRadius;
    [SerializeField] float screamCooldown = 5f;
    [SerializeField] public NavMeshAgent agent;
    private Animator animator;
    float screamTimer;
    public GameObject screamVFX;

    private void Start() {
        agent ??= GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>(true);
    }

     void Update()
    {
        if (screamTimer > 0)
        {
            screamTimer -= Time.deltaTime;
        }

        if (!CanScream() || gameManager.instance == null ||
            gameManager.instance.player == null)
        {
            return;
        }

        Vector3 toPlayer =
            gameManager.instance.player.transform.position - transform.position;
        if (toPlayer.sqrMagnitude <= alertRadius * alertRadius)
        {
            Scream();
        }
    }

    void AlertNearby()
    {
        //if (animator != null) { animator.SetTrigger("Roar"); }
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
        if (screamVFX != null) {
            Instantiate(screamVFX, transform.position, Quaternion.identity);
        }    
    }
    public bool CanScream() {
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
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }
        if (animator != null)
        {
            animator.SetTrigger("Roar");
        }
        AlertNearby();
        SpawnVFX();
    }
}
