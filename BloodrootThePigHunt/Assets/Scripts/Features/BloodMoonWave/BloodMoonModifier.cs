using System;
using UnityEngine;

namespace Bloodroot.Features.BloodMoon
{
    [Serializable]
    public sealed class BloodMoonModifier
    {
        [SerializeField] private string id = "new-modifier";
        [SerializeField] private string displayName = "New Modifier";
        [SerializeField, TextArea(2, 4)] private string description;

        [Header("Wave Multipliers")]
        [SerializeField, Min(0f)] private float enemyCount = 1f;
        [SerializeField, Min(0f)] private float enemyHealth = 1f;
        [SerializeField, Min(0f)] private float enemyDamage = 1f;
        [SerializeField, Min(0f)] private float enemySpeed = 1f;
        [SerializeField, Min(0f)] private float partReward = 1f;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public float EnemyCountMultiplier => enemyCount;
        public float EnemyHealthMultiplier => enemyHealth;
        public float EnemyDamageMultiplier => enemyDamage;
        public float EnemySpeedMultiplier => enemySpeed;
        public float PartRewardMultiplier => partReward;

        public BloodMoonModifier()
        {
        }

        public BloodMoonModifier(
            string id,
            string displayName,
            string description,
            float enemyCount,
            float enemyHealth,
            float enemyDamage,
            float enemySpeed,
            float partReward)
        {
            this.id = id;
            this.displayName = displayName;
            this.description = description;
            this.enemyCount = enemyCount;
            this.enemyHealth = enemyHealth;
            this.enemyDamage = enemyDamage;
            this.enemySpeed = enemySpeed;
            this.partReward = partReward;
        }

        public int ModifyEnemyCount(int baseCount)
        {
            return Mathf.Max(0, Mathf.CeilToInt(baseCount * enemyCount));
        }

        public float ModifyHealth(float baseHealth)
        {
            return Mathf.Max(0f, baseHealth * enemyHealth);
        }

        public float ModifyDamage(float baseDamage)
        {
            return Mathf.Max(0f, baseDamage * enemyDamage);
        }

        public float ModifySpeed(float baseSpeed)
        {
            return Mathf.Max(0f, baseSpeed * enemySpeed);
        }

        public int ModifyPartReward(int baseReward)
        {
            return Mathf.Max(0, Mathf.RoundToInt(baseReward * partReward));
        }

        internal void Validate()
        {
            id = string.IsNullOrWhiteSpace(id) ? "unnamed-modifier" : id.Trim();
            displayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName.Trim();
            enemyCount = Mathf.Max(0f, enemyCount);
            enemyHealth = Mathf.Max(0f, enemyHealth);
            enemyDamage = Mathf.Max(0f, enemyDamage);
            enemySpeed = Mathf.Max(0f, enemySpeed);
            partReward = Mathf.Max(0f, partReward);
        }
    }

    /// <summary>
    /// Implement this on a wave spawner or stat manager when it is more convenient
    /// to receive modifier changes than to query BloodMoonWaveDirector directly.
    /// </summary>
    public interface IBloodMoonModifierTarget
    {
        void ApplyBloodMoonModifier(BloodMoonModifier modifier);
        void ClearBloodMoonModifier();
    }
}
