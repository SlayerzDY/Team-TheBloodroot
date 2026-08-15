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
    [SerializeField] AudioClip[] chargeSound;
    [Range(0f, 1f)][SerializeField] float chargeSoundVolume;
    [SerializeField] AudioClip[] damageSounds;
    [Range(0f, 1f)][SerializeField] float damageSoundVolume;
    [SerializeField] AudioClip[] trotSounds;
    [Range(0f, 1f)][SerializeField] float trotSoundVolume;
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
    public virtual void StartCharge() {
        playSound(chargeSound);
        charging = true;
        agent.enabled = false;
        }
    private void playSound(AudioClip[] clipToPlay)
    {
        if (clipToPlay != null && clipToPlay.Length > 0 && audioManager.instance != null && audioManager.instance.audPlayer != null)
        {
            int randomIndex = Random.Range(0, clipToPlay.Length);
            AudioClip selectedClip = clipToPlay[randomIndex];

            if (selectedClip != null)
            {
                audioManager.instance.audPlayer.PlayOneShot(selectedClip, chargeSoundVolume);
            }
        }
    }
}
//==============================================================================================
// End of Enemy AI .cs
//==============================================================================================