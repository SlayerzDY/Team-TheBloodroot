using System;
using UnityEngine;

namespace Bloodroot.Features.BloodMoon
{
    [Serializable]
    public sealed class BloodMoonModifier
    {
        [SerializeField] private string displayName = "Blood Moon";
        [SerializeField, TextArea] private string description;
        [SerializeField, Min(0f)] private float enemyCount = 1f;
        [SerializeField, Min(0f)] private float enemyHealth = 1f;
        [SerializeField, Min(0f)] private float enemyDamage = 1f;
        [SerializeField, Min(0f)] private float enemySpeed = 1f;

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
