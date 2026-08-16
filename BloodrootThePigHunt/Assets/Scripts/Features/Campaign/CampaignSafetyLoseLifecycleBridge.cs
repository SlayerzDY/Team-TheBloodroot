using System;
using System.Reflection;
using UnityEngine;

namespace Bloodroot.Campaign
{
    /// <summary>
    /// Completes the online-safety lose/respawn lifecycle without replacing
    /// its authored Respawn button or respawn implementation. The protected
    /// gameManager latches its private lose guard on the first death; this
    /// scene-added bridge clears only that guard after the protected respawn
    /// callback has raised PlayerRespawned.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CampaignSafetyLoseLifecycleBridge : MonoBehaviour
    {
        private const string SafetyLoseGuardFieldName = "amDying";

        private static readonly FieldInfo SafetyLoseGuardField =
            typeof(global::gameManager).GetField(
                SafetyLoseGuardFieldName,
                BindingFlags.Instance |
                BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly);

        [SerializeField]
        private global::gameManager configuredManager;

        [NonSerialized]
        private global::gameManager boundManager;

        public global::gameManager ConfiguredManager => configuredManager;

        public static void RequireSafetyContract()
        {
            FieldInfo field = SafetyLoseGuardField;
            if (field == null ||
                field.Name != SafetyLoseGuardFieldName ||
                field.DeclaringType != typeof(global::gameManager) ||
                field.FieldType != typeof(bool) ||
                field.IsStatic ||
                field.IsInitOnly ||
                field.IsLiteral ||
                !field.IsPrivate)
            {
                throw new InvalidOperationException(
                    "Campaign lose lifecycle integration requires the exact " +
                    "private instance bool gameManager.amDying contract.");
            }
        }

        public void Configure(global::gameManager manager)
        {
            RequireSafetyContract();

            if (manager == null)
            {
                throw new ArgumentNullException(nameof(manager));
            }

            if (manager.gameObject != gameObject)
            {
                throw new InvalidOperationException(
                    "CampaignSafetyLoseLifecycleBridge must be configured with " +
                    "the gameManager on its own GameObject.");
            }

            global::gameManager[] managers =
                gameObject.GetComponents<global::gameManager>();
            if (managers.Length != 1 || managers[0] != manager)
            {
                throw new InvalidOperationException(
                    "CampaignSafetyLoseLifecycleBridge requires exactly one " +
                    "gameManager on its own GameObject.");
            }

            if (configuredManager != manager)
            {
                Unbind();
                configuredManager = manager;
            }

            if (isActiveAndEnabled)
            {
                Bind();
            }
        }

        private void OnEnable()
        {
            RequireSafetyContract();
            if (configuredManager != null)
            {
                Bind();
            }
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void Bind()
        {
            RequireSafetyContract();

            if (configuredManager == null ||
                configuredManager.gameObject != gameObject ||
                gameObject.GetComponents<global::gameManager>().Length != 1 ||
                gameObject.GetComponent<global::gameManager>() !=
                    configuredManager)
            {
                throw new InvalidOperationException(
                    "CampaignSafetyLoseLifecycleBridge has no exact same-object " +
                    "gameManager configuration.");
            }

            if (boundManager == configuredManager)
            {
                return;
            }

            Unbind();
            boundManager = configuredManager;
            boundManager.PlayerRespawned += HandlePlayerRespawned;
        }

        private void Unbind()
        {
            if (boundManager == null)
            {
                return;
            }

            boundManager.PlayerRespawned -= HandlePlayerRespawned;
            boundManager = null;
        }

        private void HandlePlayerRespawned()
        {
            RequireSafetyContract();

            if (boundManager == null ||
                boundManager != configuredManager ||
                boundManager.gameObject != gameObject)
            {
                throw new InvalidOperationException(
                    "CampaignSafetyLoseLifecycleBridge received a respawn from " +
                    "an unrecognized gameManager.");
            }

            if (!(bool)SafetyLoseGuardField.GetValue(boundManager))
            {
                return;
            }

            SafetyLoseGuardField.SetValue(boundManager, false);
            if ((bool)SafetyLoseGuardField.GetValue(boundManager))
            {
                throw new InvalidOperationException(
                    "CampaignSafetyLoseLifecycleBridge could not reset the " +
                    "online-safety lose guard after respawn.");
            }
        }
    }
}
