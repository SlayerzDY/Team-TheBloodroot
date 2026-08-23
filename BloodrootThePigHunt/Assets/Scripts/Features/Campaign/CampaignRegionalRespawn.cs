using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bloodroot.Campaign
{
    [Serializable]
    public sealed class CampaignRegionalRespawnMapping
    {
        [SerializeField] private CampaignAreaId area;
        [SerializeField] private GameObject missionRoot;
        [SerializeField] private Transform respawnSocket;

        public CampaignRegionalRespawnMapping(
            CampaignAreaId mappedArea,
            GameObject mappedMissionRoot,
            Transform mappedRespawnSocket)
        {
            area = mappedArea;
            missionRoot = mappedMissionRoot;
            respawnSocket = mappedRespawnSocket;
        }

        public CampaignAreaId Area => area;
        public GameObject MissionRoot => missionRoot;
        public Transform RespawnSocket => respawnSocket;
    }

    /// <summary>
    /// Keeps the existing GameManager respawn reference synchronized with the
    /// active Open World mission. The authored fallback spawn is mirrored to
    /// the same socket so a later GameManager.updatePlayer call cannot cause a
    /// respawn at the scene default. No player, enemy, inventory, or UI object
    /// is created or owned by this component.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class CampaignRegionalRespawn : MonoBehaviour
    {
        private static readonly CampaignAreaId[] RequiredAreaOrder =
        {
            CampaignAreaId.BlackPines,
            CampaignAreaId.StillwaterFeedMill,
            CampaignAreaId.HarrowEstate,
            CampaignAreaId.BloodrootHollow
        };

        [SerializeField]
        private CampaignOpenWorldProgression progression;

        [SerializeField]
        private CampaignRegionalRespawnMapping[] regionMappings =
            Array.Empty<CampaignRegionalRespawnMapping>();

        [SerializeField]
        private bool placePlayerAtActiveRegionOnSceneStart = true;

        [SerializeField, Min(0)]
        private int initialPlacementDelayFrames = 2;

        [SerializeField, Min(0f)]
        private float playerLookupTimeout = 5f;

        private global::gameManager boundManager;
        private GameObject fallbackSpawnTarget;
        private Vector3 fallbackSpawnPosition;
        private Quaternion fallbackSpawnRotation;
        private bool fallbackSpawnCaptured;
        private CampaignRegionalRespawnMapping activeMapping;
        private bool configurationValidationAttempted;
        private bool configurationValid;
        private string configurationProblem = string.Empty;
        private bool configurationErrorLogged;

        public CampaignOpenWorldProgression Progression => progression;

        public IReadOnlyList<CampaignRegionalRespawnMapping> RegionMappings =>
            regionMappings ?? Array.Empty<CampaignRegionalRespawnMapping>();

        public bool PlacePlayerAtActiveRegionOnSceneStart =>
            placePlayerAtActiveRegionOnSceneStart;

        public int InitialPlacementDelayFrames => initialPlacementDelayFrames;

        public float PlayerLookupTimeout => playerLookupTimeout;

        public bool HasActiveRegion => activeMapping != null;

        public CampaignAreaId ActiveArea => activeMapping != null
            ? activeMapping.Area
            : CampaignAreaId.BlackPines;

        public Transform ActiveRespawnSocket =>
            activeMapping != null ? activeMapping.RespawnSocket : null;

        public event Action<CampaignAreaId, Transform> ActiveRegionChanged;
        public event Action ActiveRegionCleared;

        private IEnumerator Start()
        {
            // A named campaign arrival owns the initial placement for this
            // scene. Capture that durable handoff before CampaignSpawnPoint
            // acknowledges and clears it on the next frame; otherwise this
            // component's later regional placement would overwrite the
            // authored Black Pines arrival on first entry.
            bool preserveNamedSceneArrival =
                ShouldPreserveNamedSceneArrival(
                    CampaignStateService.Instance,
                    gameObject.scene.name);
            bool preserveSafetySameScenePosition =
                CampaignSafetySaveIntegration.ShouldApplyLoadedPosition(
                    gameObject.scene.name,
                    CampaignStateService.Instance);

            RefreshActiveRegion();

            for (int frame = 0; frame < initialPlacementDelayFrames; frame++)
            {
                yield return null;
            }

            if (!placePlayerAtActiveRegionOnSceneStart)
            {
                yield break;
            }

            if (preserveNamedSceneArrival ||
                preserveSafetySameScenePosition)
            {
                yield break;
            }

            float deadline = Time.unscaledTime + playerLookupTimeout;
            do
            {
                RefreshActiveRegion();

                if (activeMapping == null)
                {
                    yield break;
                }

                if (TryPlacePlayerAtActiveRegion())
                {
                    yield break;
                }

                yield return null;
            }
            while (Time.unscaledTime <= deadline);


        }

        private static bool ShouldPreserveNamedSceneArrival(
            CampaignStateService stateService,
            string sceneName)
        {
            return stateService != null &&
                   stateService.HasPendingSpawn(
                       sceneName,
                       CampaignSpawnDestinationIds.BlackPinesArrival);
        }

        private void LateUpdate()
        {
            // Four reference comparisons keep this robust when progression
            // changes a mission root or GameManager.updatePlayer restores its
            // tagged scene-default spawn reference. This path allocates no
            // per-frame collections.
            RefreshActiveRegion();
        }

        private void OnDisable()
        {
            ReleaseManagerAndRestoreFallback();
            ClearActiveMapping();
        }

        public void Configure(
            CampaignOpenWorldProgression worldProgression,
            CampaignRegionalRespawnMapping[] mappings,
            bool placePlayerOnSceneStart,
            int placementDelayFrames = 2,
            float lookupTimeout = 5f)
        {
            ReleaseManagerAndRestoreFallback();
            progression = worldProgression;
            regionMappings = mappings != null
                ? (CampaignRegionalRespawnMapping[])mappings.Clone()
                : Array.Empty<CampaignRegionalRespawnMapping>();
            placePlayerAtActiveRegionOnSceneStart = placePlayerOnSceneStart;
            initialPlacementDelayFrames = Mathf.Max(0, placementDelayFrames);
            playerLookupTimeout = Mathf.Max(0f, lookupTimeout);
            configurationErrorLogged = false;
            configurationValidationAttempted = false;
            configurationValid = false;
            configurationProblem = string.Empty;
            activeMapping = null;

            if (isActiveAndEnabled)
            {
                RefreshActiveRegion();
            }
        }

        public bool RefreshActiveRegion()
        {
            if (!TryResolveActiveMapping(
                    out CampaignRegionalRespawnMapping resolved,
                    out string problem))
            {
                ReleaseManagerAndRestoreFallback();
                ClearActiveMapping();

                if (!configurationErrorLogged)
                {
                    configurationErrorLogged = true;

                }

                return false;
            }

            configurationErrorLogged = false;
            BindCurrentManager();

            if (resolved == null)
            {
                RestoreFallbackSpawnReference();
                ClearActiveMapping();
                return true;
            }

            bool changed = activeMapping != resolved;
            activeMapping = resolved;
            ApplyActiveSpawnReference();

            if (changed)
            {
                CampaignEventUtility.Invoke(
                    ActiveRegionChanged,
                    resolved.Area,
                    resolved.RespawnSocket,
                    this);
            }

            return true;
        }

        public bool TryResolveActiveMapping(
            out CampaignRegionalRespawnMapping mapping,
            out string problem)
        {
            mapping = null;

            if (!EnsureConfigurationValidated(out problem))
            {
                return false;
            }

            int activeCount = 0;
            CampaignRegionalRespawnMapping[] authoredMappings =
                regionMappings ?? Array.Empty<CampaignRegionalRespawnMapping>();

            foreach (CampaignRegionalRespawnMapping candidate in
                     authoredMappings)
            {
                if (candidate == null ||
                    candidate.MissionRoot == null ||
                    candidate.RespawnSocket == null)
                {
                    configurationValidationAttempted = false;
                    problem =
                        "a regional mapping was destroyed after validation.";
                    return false;
                }

                if (!candidate.MissionRoot.activeSelf)
                {
                    continue;
                }

                activeCount++;
                mapping = candidate;
            }

            if (activeCount <= 1)
            {
                problem = string.Empty;
                return true;
            }

            mapping = null;
            problem =
                $"exactly zero or one mission root may be active; found " +
                $"{activeCount}.";

            return false;
        }

        public bool TryValidateAuthoredConfiguration(out string problem)
        {
            if (progression == null)
            {
                problem = "CampaignOpenWorldProgression is not assigned.";
                return false;
            }

            CampaignRegionalRespawnMapping[] authoredMappings =
                regionMappings ?? Array.Empty<CampaignRegionalRespawnMapping>();
            IReadOnlyList<GameObject> progressionRoots =
                progression.AreaMissionRoots;

            if (authoredMappings.Length != RequiredAreaOrder.Length ||
                progressionRoots.Count != RequiredAreaOrder.Length)
            {
                problem =
                    "exactly four regional mappings and four progression " +
                    "mission roots are required.";
                return false;
            }

            var uniqueRoots = new HashSet<GameObject>();
            var uniqueSockets = new HashSet<Transform>();

            for (int index = 0; index < RequiredAreaOrder.Length; index++)
            {
                CampaignRegionalRespawnMapping mapping =
                    authoredMappings[index];
                if (mapping == null ||
                    mapping.Area != RequiredAreaOrder[index])
                {
                    problem =
                        $"mapping {index} must represent " +
                        $"{RequiredAreaOrder[index]} in campaign order.";
                    return false;
                }

                GameObject root = mapping.MissionRoot;
                Transform socket = mapping.RespawnSocket;
                if (root == null || socket == null ||
                    progressionRoots[index] != root)
                {
                    problem =
                        $"mapping {mapping.Area} does not reference its exact " +
                        "progression mission root and respawn socket.";
                    return false;
                }

                if (!uniqueRoots.Add(root) || !uniqueSockets.Add(socket))
                {
                    problem = "mission roots and respawn sockets must be unique.";
                    return false;
                }

                if (socket == root.transform ||
                    !socket.IsChildOf(root.transform) ||
                    !socket.gameObject.activeSelf)
                {
                    problem =
                        $"the {mapping.Area} socket must be an active child of " +
                        "its mission root.";
                    return false;
                }

                Component[] socketComponents =
                    socket.gameObject.GetComponents<Component>();
                if (socketComponents.Length != 1 ||
                    socketComponents[0] is not Transform ||
                    socket.childCount != 0)
                {
                    problem =
                        $"the {mapping.Area} respawn socket must be a leaf " +
                        "Transform with no collider, renderer, UI, or runtime " +
                        "behavior.";
                    return false;
                }
            }

            problem = string.Empty;
            return true;
        }

        private bool EnsureConfigurationValidated(out string problem)
        {
            if (!configurationValidationAttempted)
            {
                configurationValid =
                    TryValidateAuthoredConfiguration(
                        out configurationProblem);
                configurationValidationAttempted = true;
            }

            problem = configurationProblem;
            return configurationValid;
        }

        public bool TryPlacePlayerAtActiveRegion()
        {
            if (activeMapping == null)
            {
                return false;
            }

            BindCurrentManager();
            GameObject player = boundManager != null
                ? boundManager.player
                : null;
            if (player == null)
            {
                return false;
            }

            CharacterController controller =
                player.GetComponent<CharacterController>();
            bool controllerWasEnabled =
                controller != null && controller.enabled;

            try
            {
                if (controllerWasEnabled)
                {
                    controller.enabled = false;
                }

                Transform socket = activeMapping.RespawnSocket;
                player.transform.SetPositionAndRotation(
                    socket.position,
                    socket.rotation);
                Physics.SyncTransforms();
            }
            catch (Exception exception)
            {

                return false;
            }
            finally
            {
                if (controllerWasEnabled && controller != null)
                {
                    controller.enabled = true;
                }
            }

            ApplyActiveSpawnReference();
            return true;
        }

        private void BindCurrentManager()
        {
            global::gameManager current = global::gameManager.instance;
            if (boundManager == current)
            {
                CaptureFallbackSpawnIfNeeded();
                return;
            }

            ReleaseManagerAndRestoreFallback();
            boundManager = current;
            if (boundManager != null)
            {
                boundManager.PlayerRespawned += HandlePlayerRespawned;
            }

            CaptureFallbackSpawnIfNeeded();
        }

        private void HandlePlayerRespawned()
        {
            // Safety's playerController respawn copies only the tagged
            // fallback position. Reapply the owned regional socket atomically
            // so both position and rotation remain authoritative.
            if (activeMapping != null)
            {
                TryPlacePlayerAtActiveRegion();
            }
        }

        private void CaptureFallbackSpawnIfNeeded()
        {
            if (fallbackSpawnCaptured || boundManager == null)
            {
                return;
            }

            GameObject candidate = boundManager.playerSpawnPos;
            if (candidate == null || IsAuthoredSocket(candidate.transform))
            {
                candidate = FindTaggedFallbackSpawn();
            }

            if (candidate == null || IsAuthoredSocket(candidate.transform))
            {
                return;
            }

            fallbackSpawnTarget = candidate;
            fallbackSpawnPosition = candidate.transform.position;
            fallbackSpawnRotation = candidate.transform.rotation;
            fallbackSpawnCaptured = true;
        }

        private GameObject FindTaggedFallbackSpawn()
        {
            try
            {
                return GameObject.FindWithTag("PlayerSpawnPos");
            }
            catch (UnityException exception)
            {

                return null;
            }
        }

        private bool IsAuthoredSocket(Transform candidate)
        {
            if (candidate == null)
            {
                return false;
            }

            CampaignRegionalRespawnMapping[] authoredMappings =
                regionMappings ?? Array.Empty<CampaignRegionalRespawnMapping>();
            foreach (CampaignRegionalRespawnMapping mapping in authoredMappings)
            {
                if (mapping != null && mapping.RespawnSocket == candidate)
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplyActiveSpawnReference()
        {
            if (activeMapping == null)
            {
                return;
            }

            CaptureFallbackSpawnIfNeeded();
            Transform socket = activeMapping.RespawnSocket;

            if (fallbackSpawnCaptured && fallbackSpawnTarget != null)
            {
                fallbackSpawnTarget.transform.SetPositionAndRotation(
                    socket.position,
                    socket.rotation);
            }

            if (boundManager != null)
            {
                boundManager.playerSpawnPos = socket.gameObject;
            }
        }

        private void RestoreFallbackSpawnReference()
        {
            if (fallbackSpawnCaptured && fallbackSpawnTarget != null)
            {
                fallbackSpawnTarget.transform.SetPositionAndRotation(
                    fallbackSpawnPosition,
                    fallbackSpawnRotation);
            }

            if (boundManager != null && fallbackSpawnTarget != null)
            {
                boundManager.playerSpawnPos = fallbackSpawnTarget;
            }
        }

        private void ReleaseManagerAndRestoreFallback()
        {
            RestoreFallbackSpawnReference();

            if (boundManager != null)
            {
                boundManager.PlayerRespawned -= HandlePlayerRespawned;
            }

            boundManager = null;
            fallbackSpawnTarget = null;
            fallbackSpawnCaptured = false;
        }

        private void ClearActiveMapping()
        {
            if (activeMapping == null)
            {
                return;
            }

            activeMapping = null;
            CampaignEventUtility.Invoke(ActiveRegionCleared, this);
        }

        private void OnValidate()
        {
            initialPlacementDelayFrames =
                Mathf.Max(0, initialPlacementDelayFrames);
            playerLookupTimeout = Mathf.Max(0f, playerLookupTimeout);
            configurationValidationAttempted = false;
            configurationValid = false;
            configurationProblem = string.Empty;
        }
    }
}
