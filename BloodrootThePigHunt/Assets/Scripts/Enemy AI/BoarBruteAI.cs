//==============================================================================================
// Using Unity Engine
//==============================================================================================
using UnityEngine;
using UnityEngine.AI;
//==============================================================================================
// Declare Boar Brute AI
//==============================================================================================
public class BoarBruteAI : enemyAI {
    //==========================================================================================
    // Variable Declarations
    //==========================================================================================
    [SerializeField] float chargeSpeed;
    [SerializeField] float chargeTime;
    public bool charging;
    float timer;
    [SerializeField] Animator anim;
    NavMeshAgent agent1;
    //==========================================================================================
    // Function Declarations
    //==========================================================================================
    // Function, Awake Override
    //------------------------------------------------------------------------------------------
    protected override void Awake() {
        base.Awake();
        anim = GetComponent<Animator>();
        if (anim == null) {
            Debug.LogWarning("Boar missing Animator component! Proceeding without animations.");
        }

    }
    //==========================================================================================
    // Function, Start Override
    //------------------------------------------------------------------------------------------
    protected override void Start() {
        base.Start();
        agent1 = GetComponent<NavMeshAgent>();
    }
    //==========================================================================================
    // Function, Update Override
    //------------------------------------------------------------------------------------------
    protected override void Update() {
        base.Update();    
        if (charging) {
            timer += Time.deltaTime;
            // Move forward fast
            transform.position += transform.forward * chargeSpeed * Time.deltaTime;
            if (timer >= chargeTime) {
                charging = false;
                timer = 0f;
                agent1.enabled = true;
                agent1.SetDestination(gameManager.instance.player.transform.position);
            }
        }
    }
    //==========================================================================================
    // Function, Start Charge
    //------------------------------------------------------------------------------------------
    public void StartCharge() {
        charging = true;
        agent1.enabled = false;
        }
    }
//==============================================================================================
// End of Enemy AI .cs
//==============================================================================================