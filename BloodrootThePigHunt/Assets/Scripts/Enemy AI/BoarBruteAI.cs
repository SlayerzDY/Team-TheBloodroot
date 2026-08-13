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
        [SerializeField] float chargeSpeed;
        [SerializeField] float chargeTime;
    public bool charging;
        float timer;
    private Animator anim;
    NavMeshAgent agent;

    protected override void Awake() {
        base.Awake();
        anim = GetComponent<Animator>();
        if (anim == null)
        {
            Debug.LogWarning("Boar missing Animator component! Proceeding without animations.");
        }
    }

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