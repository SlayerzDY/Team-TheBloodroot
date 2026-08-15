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
    // Serialized Variables
    [SerializeField] float chargeSpeed;
    [SerializeField] float chargeTime;

    [SerializeField] AudioClip[] chargeSound;
    [SerializeField, Range(0f, 1f)] float chargeSoundVolume;

    [SerializeField] AudioClip[] damageSounds;
    [SerializeField, Range(0f, 1f)] float damageSoundVolume;

    [SerializeField] AudioClip[] trotSounds;
    [SerializeField, Range(0f, 1f)] float trotSoundVolume;

    // Non-serialized Variables
    public bool charging;
    float timer;

    //==========================================================================================
    // Start
    //==========================================================================================
    protected override void Start()
    {
        base.Start();
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }
    }

    //==========================================================================================
    // Update
    //==========================================================================================
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

                if (agent != null)
                {
                    agent.enabled = true;
                    if (gameManager.instance != null && gameManager.instance.player != null)
                    {
                        agent.SetDestination(gameManager.instance.player.transform.position);
                    }
                }
            }
        }
    }

    //==========================================================================================
    // Start Charge
    //==========================================================================================
    public virtual void StartCharge()
    {
        charging = true;

        if (agent != null)
        {
            agent.enabled = false;
        }
    }
}
//==============================================================================================
// End of BoarBruteAI.cs
//==============================================================================================