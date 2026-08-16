using UnityEngine;

namespace Bloodroot.Features.AlphaEnemies
{
    /// <summary>
    /// A bounded, telegraphed area strike. It tracks no targets after spawn:
    /// the assigned target must remain inside the displayed radius when the
    /// pulse resolves.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WitchAreaPulse : MonoBehaviour
    {
        [Header("Authored Presentation")]
        [SerializeField] private Transform visualRoot;
        [SerializeField, Min(0.05f)] private float telegraphDuration = 0.9f;
        [SerializeField, Min(0f)] private float lingerAfterDetonation = 0.12f;
        [SerializeField, Range(0.01f, 1f)] private float initialVisualFraction = 0.08f;

        private GameObject owner;
        private Transform assignedTarget;
        private int damage;
        private float radius;
        private float configuredAt;
        private float detonateAt;
        private float destroyAt;
        private Vector3 authoredVisualScale = Vector3.one;
        private bool configured;
        private bool detonated;

        public bool IsConfigured => configured;
        public bool HasDetonated => detonated;
        public float Radius => radius;

        private void Awake()
        {
            if (visualRoot != null)
            {
                authoredVisualScale = visualRoot.localScale;
            }
        }

        public void Configure(
            GameObject pulseOwner,
            Transform target,
            int configuredDamage,
            float configuredRadius)
        {
            owner = pulseOwner;
            assignedTarget = target;
            damage = Mathf.Max(0, configuredDamage);
            radius = Mathf.Max(0.1f, configuredRadius);
            configuredAt = Time.time;
            detonateAt = configuredAt + telegraphDuration;
            destroyAt = detonateAt + lingerAfterDetonation;
            configured = true;
            detonated = false;
            RefreshVisual(0f);
        }

        private void Update()
        {
            if (!configured)
            {
                return;
            }

            if (!detonated)
            {
                float progress = telegraphDuration <= 0f
                    ? 1f
                    : Mathf.Clamp01(
                        (Time.time - configuredAt) / telegraphDuration);
                RefreshVisual(progress);
                if (Time.time >= detonateAt)
                {
                    Detonate();
                }
            }

            if (detonated && Time.time >= destroyAt)
            {
                Destroy(gameObject);
            }
        }

        private void Detonate()
        {
            if (detonated)
            {
                return;
            }

            detonated = true;
            RefreshVisual(1f);
            if (assignedTarget == null || owner != null &&
                AlphaEnemyEventUtility.IsSameHierarchy(
                    assignedTarget,
                    owner.transform))
            {
                return;
            }

            Vector3 offset = assignedTarget.position - transform.position;
            if (offset.sqrMagnitude > radius * radius)
            {
                return;
            }

            global::IDamage receiver =
                AlphaEnemyEventUtility.FindDamageReceiver(assignedTarget);
            if (receiver != null && damage > 0)
            {
                receiver.TakeDamage(damage);
            }
        }

        private void RefreshVisual(float progress)
        {
            if (visualRoot == null)
            {
                return;
            }

            float diameter = radius * 2f;
            float fraction = Mathf.Lerp(
                initialVisualFraction,
                1f,
                Mathf.Clamp01(progress));
            visualRoot.localScale = new Vector3(
                authoredVisualScale.x * diameter * fraction,
                authoredVisualScale.y,
                authoredVisualScale.z * diameter * fraction);
        }

        private void OnValidate()
        {
            telegraphDuration = Mathf.Max(0.05f, telegraphDuration);
            lingerAfterDetonation = Mathf.Max(0f, lingerAfterDetonation);
            initialVisualFraction = Mathf.Clamp(initialVisualFraction, 0.01f, 1f);
        }
    }
}
