//==============================================================================================
// Using Unity Engine
//==============================================================================================
using UnityEngine;
using UnityEngine.AI;
//==============================================================================================
// Declare Boar Brute AI
//==============================================================================================
public class BoarBruteAI : enemyAI
    {
        // Seralized Variables
        [SerializeField] float chargeSpeed;
        [SerializeField] float chargeTime;
        // none searlized variables
        public bool charging;
        float timer;

    protected override void Start()
    {
        base.Start();
        agent = GetComponent<NavMeshAgent>();
    }

    protected override void Update()
        {
        base.Update();    
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
//==============================================================================================
// End of Enemy AI .cs
//==============================================================================================