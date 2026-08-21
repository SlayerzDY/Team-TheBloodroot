using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public class ScreecherAI  : MonoBehaviour 
{
    [SerializeField] float alertRadius;
    [SerializeField] float screamCooldown = 5f;
    //[SerializeField] private AudioClip[] scream;
    [SerializeField] public NavMeshAgent agent;
    //[Range(0f, 1f)] [SerializeField] float screamVolume;
    //private Animator animator;
    float screamTimer;
    public GameObject screamVFX;

    private void Start() {
        //animator = GetComponentInChildren<Animator>();
    }

     void Update()
    {
        //animator.SetFloat("Speed", agent.velocity.magnitude / agent.speed, 0.1f, Time.deltaTime);
        if (screamTimer > 0)
     
            screamTimer -= Time.deltaTime;
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
        //GetComponent<EnemyAudioControl>().PlayActionSound();
        //if (scream.Length > 0) { audioManager.instance.audPlayer.PlayOneShot(scream[UnityEngine.Random.Range(0, scream.Length)], screamVolume); }
        AlertNearby();
        SpawnVFX();
    }
}
