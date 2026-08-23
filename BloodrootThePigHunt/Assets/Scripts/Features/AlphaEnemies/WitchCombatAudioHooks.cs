using UnityEngine;
using UnityEngine.Events;

namespace Bloodroot.Features.AlphaEnemies
{
    /// <summary>
    /// Inspector-facing, no-asset audio timing bridge for a witch root.
    ///
    /// This component deliberately stores no AudioClips and calls no audio
    /// APIs. During the audio pass, assign the UnityEvents below to an audio
    /// cue receiver (or subscribe to <see cref="WitchController.CombatAudioCue"/>
    /// in code). The callbacks are emitted only for completed gameplay
    /// actions, never for a failed cast, failed summon, or cancelled pulse.
    /// </summary>
    [AddComponentMenu("Bloodroot/Audio/Witch Combat Audio Hooks")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WitchController))]
    public sealed class WitchCombatAudioHooks : MonoBehaviour
    {
        [Header("Witch Source")]
        [Tooltip("The root combat controller. Left empty only for new prefabs; it resolves from this GameObject.")]
        [SerializeField] private WitchController witch;

        [Header("Audio Integration Cues (No Clips Assigned Here)")]
        [Tooltip("Once for every accepted damage hit, including the final hit, after health has been reduced.")]
        [SerializeField] private UnityEvent onDamageTaken = new UnityEvent();

        [Tooltip("Once after one or more magic projectiles have been spawned and configured.")]
        [SerializeField] private UnityEvent onProjectileAttack = new UnityEvent();

        [Tooltip("Once after a summoned Boar has been created, prepared, and alerted successfully.")]
        [SerializeField] private UnityEvent onMinionSummoned = new UnityEvent();

        [Tooltip("Once when the witch's active root ward breaks. Shielded witches only.")]
        [SerializeField] private UnityEvent onShieldBroken = new UnityEvent();

        [Tooltip("Once after the Matriarch has created and configured a Heartroot pulse.")]
        [SerializeField] private UnityEvent onHeartrootPulse = new UnityEvent();

        [Tooltip("Once when death becomes irreversible, after the authored death trigger/default clip but before loot and delayed destruction.")]
        [SerializeField] private UnityEvent onDeathStarted = new UnityEvent();

        [Tooltip("Once when the controller has started one of its configured ambient clips.")]
        [SerializeField] private UnityEvent onAmbientStarted = new UnityEvent();

        private bool subscribed;

        public WitchController Witch => witch;

        private void Awake()
        {
            ResolveWitch();
        }

        private void OnEnable()
        {
            ResolveWitch();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnValidate()
        {
            ResolveWitch();
        }

        private void ResolveWitch()
        {
            if (witch == null)
            {
                witch = GetComponent<WitchController>();
            }
        }

        private void Subscribe()
        {
            if (subscribed || witch == null)
            {
                return;
            }

            witch.CombatAudioCue += HandleCombatAudioCue;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || witch == null)
            {
                return;
            }

            witch.CombatAudioCue -= HandleCombatAudioCue;
            subscribed = false;
        }

        private void HandleCombatAudioCue(WitchCombatAudioCue cue)
        {
            switch (cue)
            {
                case WitchCombatAudioCue.DamageTaken:
                    AlphaEnemyEventUtility.Invoke(
                        onDamageTaken,
                        this,
                        nameof(onDamageTaken));
                    break;

                case WitchCombatAudioCue.ProjectileAttack:
                    AlphaEnemyEventUtility.Invoke(
                        onProjectileAttack,
                        this,
                        nameof(onProjectileAttack));
                    break;

                case WitchCombatAudioCue.MinionSummoned:
                    AlphaEnemyEventUtility.Invoke(
                        onMinionSummoned,
                        this,
                        nameof(onMinionSummoned));
                    break;

                case WitchCombatAudioCue.ShieldBroken:
                    AlphaEnemyEventUtility.Invoke(
                        onShieldBroken,
                        this,
                        nameof(onShieldBroken));
                    break;

                case WitchCombatAudioCue.HeartrootPulse:
                    AlphaEnemyEventUtility.Invoke(
                        onHeartrootPulse,
                        this,
                        nameof(onHeartrootPulse));
                    break;

                case WitchCombatAudioCue.DeathStarted:
                    AlphaEnemyEventUtility.Invoke(
                        onDeathStarted,
                        this,
                        nameof(onDeathStarted));
                    break;

                case WitchCombatAudioCue.AmbientStarted:
                    AlphaEnemyEventUtility.Invoke(
                        onAmbientStarted,
                        this,
                        nameof(onAmbientStarted));
                    break;
            }
        }
    }
}
