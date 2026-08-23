using System;
using UnityEngine;

namespace Bloodroot.Features.AlphaEnemies
{
    /// <summary>
    /// Final three-stage witch. It begins behind an arena ward, adds summoned
    /// hog pressure when vulnerable, then gains a telegraphed Heartroot pulse
    /// and a broad projectile nova at low health.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WitchMatriarchAI : WitchController
    {
        [Header("Matriarch Phases")]
        [SerializeField, Range(0.1f, 0.95f)] private float conjureThreshold = 0.65f;
        [SerializeField, Range(0.05f, 0.9f)] private float furyThreshold = 0.35f;

        [Header("Heartroot Pulse")]
        [SerializeField] private GameObject pulsePrefab;
        [SerializeField, Min(0.1f)] private float pulseCooldown = 8f;
        [SerializeField, Min(0.1f)] private float pulseRadius = 4.75f;
        [SerializeField, Min(0f)] private float pulseDamageMultiplier = 0.75f;

        [Header("Authored Events")]
        [SerializeField] private WitchMatriarchPhaseEvent onPhaseChanged =
            new WitchMatriarchPhaseEvent();

        private WitchMatriarchPhase phase = WitchMatriarchPhase.Ward;
        private float nextPulseAt;
        private WitchAreaPulse activePulse;
        private bool pulseWarningIssued;

        public event Action<WitchMatriarchPhase> PhaseChanged;

        public WitchMatriarchPhase Phase => phase;

        protected override bool SupportsShield => true;
        protected override bool SupportsSummoning => true;

        protected override void TickCombat()
        {
            RefreshPhase();
            if (phase != WitchMatriarchPhase.Ward)
            {
                TrySummonMinion();
            }

            if (phase == WitchMatriarchPhase.HeartrootFury)
            {
                TryCastProjectileVolley(
                    projectileCount: 5,
                    spreadDegrees: 70f,
                    homeOnTarget: false,
                    damageMultiplier: 0.62f,
                    speedMultiplier: 1.15f);
                TryCreateHeartrootPulse();
                return;
            }

            if (phase == WitchMatriarchPhase.Conjure)
            {
                TryCastProjectileVolley(
                    projectileCount: 3,
                    spreadDegrees: 18f,
                    homeOnTarget: true,
                    damageMultiplier: 0.7f,
                    speedMultiplier: 1f);
                TryCreateHeartrootPulse();
                return;
            }

            TryCastMagic();
        }

        protected override void OnEncounterPrepared()
        {
            nextPulseAt = Time.time + pulseCooldown;
            SetPhase(IsShielded
                ? WitchMatriarchPhase.Ward
                : WitchMatriarchPhase.Conjure);
            RefreshPhase();
        }

        protected override void OnHealthChanged()
        {
            RefreshPhase();
        }

        protected override void OnDying()
        {
            SetPhase(WitchMatriarchPhase.Dead);
            if (activePulse != null)
            {
                Destroy(activePulse.gameObject);
                activePulse = null;
            }
        }

        private void RefreshPhase()
        {
            if (IsDead)
            {
                SetPhase(WitchMatriarchPhase.Dead);
                return;
            }

            if (IsShielded)
            {
                SetPhase(WitchMatriarchPhase.Ward);
                return;
            }

            SetPhase(HealthRatio <= furyThreshold
                ? WitchMatriarchPhase.HeartrootFury
                : WitchMatriarchPhase.Conjure);
        }

        private void TryCreateHeartrootPulse()
        {
            if (pulsePrefab == null || Time.time < nextPulseAt ||
                activePulse != null)
            {
                return;
            }

            Transform pulseTarget = SecondaryAttackTarget != null
                ? SecondaryAttackTarget
                : Target;
            if (pulseTarget == null)
            {
                return;
            }

            GameObject instance = Instantiate(
                pulsePrefab,
                pulseTarget.position,
                Quaternion.identity);
            activePulse = instance.GetComponent<WitchAreaPulse>();
            if (activePulse == null)
            {
                Destroy(instance);
                if (!pulseWarningIssued)
                {
                    pulseWarningIssued = true;
                    Debug.LogWarning(
                        $"{name}: Matriarch pulse prefab requires WitchAreaPulse.",
                        this);
                }

                nextPulseAt = Time.time + pulseCooldown;
                return;
            }

            int damage = Mathf.Max(0, Mathf.RoundToInt(
                MagicDamage * pulseDamageMultiplier));
            activePulse.Configure(
                gameObject,
                pulseTarget,
                damage,
                pulseRadius);
            RaiseCombatAudioCue(WitchCombatAudioCue.HeartrootPulse);
            nextPulseAt = Time.time + pulseCooldown;
        }

        private void SetPhase(WitchMatriarchPhase newPhase)
        {
            if (phase == newPhase)
            {
                return;
            }

            phase = newPhase;
            AlphaEnemyEventUtility.Invoke(
                onPhaseChanged,
                phase,
                this,
                nameof(onPhaseChanged));
            AlphaEnemyEventUtility.Invoke(
                PhaseChanged,
                phase,
                this,
                nameof(PhaseChanged));
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            conjureThreshold = Mathf.Clamp(conjureThreshold, 0.1f, 0.95f);
            furyThreshold = Mathf.Clamp(
                furyThreshold,
                0.05f,
                conjureThreshold - 0.05f);
            pulseCooldown = Mathf.Max(0.1f, pulseCooldown);
            pulseRadius = Mathf.Max(0.1f, pulseRadius);
            pulseDamageMultiplier = Mathf.Max(0f, pulseDamageMultiplier);
        }
    }
}
