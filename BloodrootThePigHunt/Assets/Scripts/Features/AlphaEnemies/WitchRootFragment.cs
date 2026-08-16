using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Bloodroot.Features.AlphaEnemies
{
    [DisallowMultipleComponent]
    public sealed class WitchRootFragment : MonoBehaviour, global::IDamage
    {
        [Header("Vitals")]
        [SerializeField, Min(1)] private int maxHealth = 40;
        [SerializeField, Min(0f)] private float deactivateDelay = 0.15f;

        [Header("Authored Presentation")]
        [SerializeField] private Animator animator;
        [SerializeField] private string breakTrigger = "Break";
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip hitClip;
        [SerializeField] private AudioClip breakClip;
        [SerializeField] private Collider[] damageColliders;

        [Header("Authored Events")]
        [SerializeField] private WitchRootFragmentEvent onBroken = new WitchRootFragmentEvent();
        [SerializeField] private UnityEvent onHit = new UnityEvent();

        private WitchController owner;
        private int currentHealth;
        private bool isBroken;
        private Coroutine deactivateRoutine;

        public event Action<WitchRootFragment> Broken;

        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;
        public bool IsBroken => isBroken;

        private void Awake()
        {
            CacheCollidersIfNeeded();
            ResetFragment();
        }

        public void SetOwner(WitchController fragmentOwner)
        {
            owner = fragmentOwner;
        }

        public void ResetFragment()
        {
            if (deactivateRoutine != null)
            {
                StopCoroutine(deactivateRoutine);
                deactivateRoutine = null;
            }

            isBroken = false;
            currentHealth = maxHealth;
            SetCollidersEnabled(true);
        }

        public void TakeDamage(int amount)
        {
            if (isBroken || amount <= 0)
            {
                return;
            }

            currentHealth = Mathf.Max(0, currentHealth - amount);
            PlayClip(hitClip);
            AlphaEnemyEventUtility.Invoke(onHit, this, nameof(onHit));
            if (currentHealth == 0)
            {
                Break();
            }
        }

        public void onDeath(bool dead)
        {
            if (dead)
            {
                Break();
            }
        }

        private void Break()
        {
            if (isBroken)
            {
                return;
            }

            isBroken = true;
            currentHealth = 0;
            SetCollidersEnabled(false);
            if (animator != null && !string.IsNullOrWhiteSpace(breakTrigger))
            {
                animator.SetTrigger(breakTrigger);
            }

            PlayClip(breakClip);
            owner?.NotifyRootFragmentDestroyed(this);
            AlphaEnemyEventUtility.Invoke(onBroken, this, this, nameof(onBroken));
            AlphaEnemyEventUtility.Invoke(Broken, this, this, nameof(Broken));

            if (deactivateDelay <= 0f)
            {
                gameObject.SetActive(false);
            }
            else
            {
                deactivateRoutine = StartCoroutine(DeactivateAfterDelay());
            }
        }

        private IEnumerator DeactivateAfterDelay()
        {
            yield return new WaitForSeconds(deactivateDelay);
            deactivateRoutine = null;
            gameObject.SetActive(false);
        }

        private void CacheCollidersIfNeeded()
        {
            if (damageColliders == null || damageColliders.Length == 0)
            {
                damageColliders = GetComponentsInChildren<Collider>(true);
            }
        }

        private void SetCollidersEnabled(bool enabled)
        {
            CacheCollidersIfNeeded();
            foreach (Collider damageCollider in damageColliders)
            {
                if (damageCollider != null)
                {
                    damageCollider.enabled = enabled;
                }
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
            deactivateDelay = Mathf.Max(0f, deactivateDelay);
        }
    }
}
