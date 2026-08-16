using System;
using UnityEngine;
using UnityEngine.Events;

namespace Bloodroot.Features.AlphaEnemies
{
    [DisallowMultipleComponent]
    public sealed class WitchDefenseAltar : MonoBehaviour, global::IDamage
    {
        [Header("Vitals")]
        [SerializeField, Min(1)] private int maxHealth = 500;

        [Header("Authored Presentation")]
        [SerializeField] private Animator animator;
        [SerializeField] private string hitTrigger = "Hit";
        [SerializeField] private string destroyedTrigger = "Destroyed";
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip hitClip;
        [SerializeField] private AudioClip destroyedClip;

        [Header("Authored Events")]
        [SerializeField] private UnityEvent onDamaged = new UnityEvent();
        [SerializeField] private UnityEvent onDestroyed = new UnityEvent();

        private int currentHealth;
        private bool isDestroyed;

        public event Action<WitchDefenseAltar> Damaged;
        public event Action<WitchDefenseAltar> Destroyed;

        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;
        public bool IsDestroyed => isDestroyed;

        private void Awake()
        {
            ResetAltar();
        }

        public void ResetAltar()
        {
            isDestroyed = false;
            currentHealth = maxHealth;
        }

        public void TakeDamage(int amount)
        {
            if (isDestroyed || amount <= 0)
            {
                return;
            }

            currentHealth = Mathf.Max(0, currentHealth - amount);
            TriggerAnimator(hitTrigger);
            PlayClip(hitClip);
            AlphaEnemyEventUtility.Invoke(onDamaged, this, nameof(onDamaged));
            AlphaEnemyEventUtility.Invoke(Damaged, this, this, nameof(Damaged));
            if (currentHealth == 0)
            {
                DestroyAltar();
            }
        }

        public void onDeath(bool dead)
        {
            if (dead)
            {
                DestroyAltar();
            }
        }

        private void DestroyAltar()
        {
            if (isDestroyed)
            {
                return;
            }

            isDestroyed = true;
            currentHealth = 0;
            TriggerAnimator(destroyedTrigger);
            PlayClip(destroyedClip);
            AlphaEnemyEventUtility.Invoke(onDestroyed, this, nameof(onDestroyed));
            AlphaEnemyEventUtility.Invoke(Destroyed, this, this, nameof(Destroyed));
        }

        private void TriggerAnimator(string triggerName)
        {
            if (animator != null && !string.IsNullOrWhiteSpace(triggerName))
            {
                animator.SetTrigger(triggerName);
            }
        }

        private void PlayClip(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1, maxHealth);
        }
    }
}
