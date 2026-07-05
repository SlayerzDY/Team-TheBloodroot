using UnityEngine;
using UnityEngine.AI;

  public class BoarBruteAI : MonoBehaviour
    {
        [SerializeField] float chargeSpeed;
        [SerializeField] float chargeTime;
    public bool charging;
        float timer;

    NavMeshAgent agent;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void Update()
        {
            if (charging)
            {
                timer += Time.deltaTime;

                // Move forward fast
                transform.position += transform.forward * chargeSpeed * Time.deltaTime;

                if (timer >= chargeTime)
                {
                    charging = false;
                    timer = 0f;
                
                agent.enabled = true;


                agent.SetDestination(gameManager.instance.player.transform.position);
                }
            }
        }
        public void StartCharge()
        {

            charging = true;

           agent.enabled = false;
        }
    }



