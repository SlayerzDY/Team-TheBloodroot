using UnityEngine;

namespace Bloodroot.Features.AlphaEnemies
{
    /// <summary>
    /// Root-ward specialist. Its arena-authored fragments provide the ward;
    /// its own attack is a readable, non-homing fan rather than the shared
    /// single seeking bolt used by the other witches.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WitchShieldBearerAI : WitchController
    {
        [Header("Shield Bearer Power")]
        [SerializeField, Range(3, 7)] private int fanProjectileCount = 3;
        [SerializeField, Range(5f, 90f)] private float fanSpreadDegrees = 26f;
        [SerializeField, Min(0f)] private float fanDamageMultiplier = 0.72f;
        [SerializeField, Min(0.01f)] private float fanSpeedMultiplier = 1.1f;

        protected override bool SupportsShield => true;

        protected override void TickCombat()
        {
            TryCastProjectileVolley(
                fanProjectileCount,
                fanSpreadDegrees,
                homeOnTarget: false,
                damageMultiplier: fanDamageMultiplier,
                speedMultiplier: fanSpeedMultiplier);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            fanProjectileCount = Mathf.Clamp(fanProjectileCount, 3, 7);
            fanSpreadDegrees = Mathf.Clamp(fanSpreadDegrees, 5f, 90f);
            fanDamageMultiplier = Mathf.Max(0f, fanDamageMultiplier);
            fanSpeedMultiplier = Mathf.Max(0.01f, fanSpeedMultiplier);
        }
    }
}
