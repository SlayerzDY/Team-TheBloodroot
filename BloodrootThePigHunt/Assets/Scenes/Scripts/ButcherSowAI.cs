using UnityEngine;

public class BoarBruteAI : MonoBehaviour
{
    [SerializeField] float chargeSpeed;
    [SerializeField] float chargeTime;
    bool charging = false;
    float timer;

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
            }
        }
    }
    public void StartCharge() { 
    
        charging = true;
    }
}
