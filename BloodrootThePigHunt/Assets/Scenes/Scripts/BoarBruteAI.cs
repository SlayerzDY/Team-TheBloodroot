using UnityEngine;

public class ButcherSowAI : MonoBehaviour
{
    [SerializeField] int heavyDamage;
    [SerializeField] float heavyAttackRate;
    float heavyTimer;

    void Update()
    {
        heavyTimer += Time.deltaTime;
    }
    public void HeavyAttack(IDamage target)
    {
        if (heavyTimer >= heavyAttackRate)
        {  
            target.TakeDamage(heavyDamage);
            heavyTimer = 0f;
        }
    }
}

