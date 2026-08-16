using UnityEngine;

namespace Bloodroot.Features.AlphaEnemies
{
    /// <summary>
    /// Hog-caller specialist. It keeps a bounded screen of ground-bound hogs
    /// between itself and the player while continuing its ranged pressure.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WitchSummonerAI : WitchController
    {
        [Header("Summoner Power")]
        [SerializeField, Min(0f)] private float screenedDamageMultiplier = 1.05f;
        [SerializeField, Min(0f)] private float exposedDamageMultiplier = 0.8f;

        protected override bool SupportsSummoning => true;

        protected override void TickCombat()
        {
            TrySummonMinion();
            TryCastProjectileVolley(
                projectileCount: 1,
                spreadDegrees: 0f,
                homeOnTarget: true,
                damageMultiplier: ActiveMinionCount > 0
                    ? screenedDamageMultiplier
                    : exposedDamageMultiplier,
                speedMultiplier: 1f);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            screenedDamageMultiplier = Mathf.Max(0f, screenedDamageMultiplier);
            exposedDamageMultiplier = Mathf.Max(0f, exposedDamageMultiplier);
        }
    }
}
