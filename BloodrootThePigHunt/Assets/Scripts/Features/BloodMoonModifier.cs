using UnityEngine;
using System;

namespace Bloodroot.Features.BloodMoon
{
    
    public sealed class BloodMoonModifier : MonoBehaviour
    {
        [SerializeField] private string displayName = "Blood Moon";
        [SerializeField, TextArea] string description;
        [SerializeField, Min(0f)] float enemyCount;
        [SerializeField, Min(0f)] float enemyHealth;
        [SerializeField, Min(0f)] float enemyDamage;
        [SerializeField, Min(0f)] float enemySpeed;

        public string DisplayName => displayName;
        public string Description => description;

        public BloodMoonModifier()
        {

        }

        public BloodMoonModifier(
            string displayName,
            string description,
            float enemyCount,
            float enemyHealth,
            float enemyDamage,
            float enemySpeed)
        {
            this.displayName = displayName;
            this.description = description;
            this.enemyCount = enemyCount;
            this.enemyHealth = enemyHealth;
            this.enemyDamage = enemyDamage;
            this.enemySpeed = enemySpeed;
        }

        public int ModifyEnemyCount(int baseValue)
        {
            return Mathf.Max(0, Mathf.CeilToInt(baseValue * enemyCount));
        }

        public float ModifyHealth(float baseValue)
        {
            return Mathf.Max(0f, baseValue * enemyHealth);
        }

        public float ModifyDamage(float baseValue)
        {
            return Mathf.Max(0f, baseValue * enemyDamage);
        }

        public float ModifySpeed(float baseValue)
        {
            return Mathf.Max(0f, baseValue * enemySpeed);
        }

    }

}
