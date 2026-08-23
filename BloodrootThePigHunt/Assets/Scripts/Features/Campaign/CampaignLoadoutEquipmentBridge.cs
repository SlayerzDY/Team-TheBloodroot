using System;
using System.Collections;
using System.Collections.Generic;
using Bloodroot.Features.Hub;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bloodroot.Campaign
{
    /// <summary>
    /// Converts durable campaign inventory ownership into the transient
    /// equipment state owned by the safety player and HUD implementations.
    /// The inventory remains the ownership authority; this adapter never
    /// mutates safety scripts, prefabs, or source ScriptableObjects.
    /// Runtime equipment clones are deliberately session-local compatibility
    /// state and are rebuilt from durable inventory ownership when required.
    /// </summary>
    [DefaultExecutionOrder(-800)]
    [DisallowMultipleComponent]
    public sealed class CampaignLoadoutEquipmentBridge : MonoBehaviour
    {
        public const string AuthoringRootName =
            "__CAMPAIGN_LOADOUT_EQUIPMENT";
        public const string RifleItemName = "M1 Garand";
        public const string RifleAmmoItemName = "M1 Garand Ammo";
        public const string RadarItemName = "Radar";
        public const string PistolStableId = "pistol";
        public const string RifleStableId = "rifle";
        public const string ShotgunStableId = "shotgun";

        private static CampaignLoadoutEquipmentBridge instance;

        [Header("Campaign Authority")]
        [SerializeField] private CampaignStateService campaignState;
        [SerializeField] private CampaignInventoryCarryover inventoryCarryover;

        [Header("Inventory Ownership Tokens")]
        [SerializeField] private GameObject rifleInventoryPickup;
        [SerializeField] private GameObject rifleAmmoInventoryPickup;
        [SerializeField] private GameObject radarInventoryPickup;

        [Header("Safety Equipment Source")]
        [SerializeField] private gunStats pistolDefinition;
        [SerializeField] private gunStats rifleDefinition;
        [SerializeField] private gunStats shotgunDefinition;

        private CampaignStateService subscribedState;
        private CampaignInventoryCarryover subscribedCarryover;
        private HubLoadoutStation subscribedLoadoutStation;
        private Coroutine deferredReconcileRoutine;

        private playerController boundPlayerController;
        private List<gunStats> boundSafetyGunInventory;
        private bool rifleGrantAttempted;
        private bool rifleGrantSucceeded;
        private bool rifleAdoptedFromSafetySave;
        private bool radarActivationAttempted;
        private bool radarActivationSucceeded;
        private int rifleGrantAttemptCount;
        private int radarActivationAttemptCount;
        private gunStats runtimeRifleDefinition;
        private List<gunStats> activeStableGunGeneration =
            new List<gunStats>();
        private List<gunStats> pendingStableGunGeneration =
            new List<gunStats>();
        private bool safetyCheckpointSynchronized;

        public static CampaignLoadoutEquipmentBridge Instance => instance;

        public CampaignStateService CampaignState => campaignState;
        public CampaignInventoryCarryover InventoryCarryover =>
            inventoryCarryover;
        public GameObject RifleInventoryPickup => rifleInventoryPickup;
        public GameObject RifleAmmoInventoryPickup =>
            rifleAmmoInventoryPickup;
        public GameObject RadarInventoryPickup => radarInventoryPickup;
        public gunStats RifleDefinition => rifleDefinition;
        public gunStats PistolDefinition => pistolDefinition;
        public gunStats ShotgunDefinition => shotgunDefinition;
        public gunStats RuntimeRifleDefinition => runtimeRifleDefinition;
        public playerController BoundPlayerController => boundPlayerController;
        public bool RifleGrantAttempted => rifleGrantAttempted;
        public bool RifleGrantSucceeded => rifleGrantSucceeded;
        public bool RifleAdoptedFromSafetySave =>
            rifleAdoptedFromSafetySave;
        public bool RadarActivationAttempted => radarActivationAttempted;
        public bool RadarActivationSucceeded => radarActivationSucceeded;
        public int RifleGrantAttemptCount => rifleGrantAttemptCount;
        public int RadarActivationAttemptCount =>
            radarActivationAttemptCount;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            ResolveLiveCampaignAuthority();
        }

        private void OnEnable()
        {
            if (instance != this)
                return;

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            RebindCampaignSubscriptions();
            RebindLoadoutStation();

            if (Application.isPlaying)
                StartDeferredReconcile();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            StopDeferredReconcile();
            UnbindLoadoutStation();
            UnbindCampaignSubscriptions();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            StopDeferredReconcile();
            UnbindLoadoutStation();
            UnbindCampaignSubscriptions();

            if (instance == this)
            {
                instance = null;
                ResetRuntimeEquipmentState();
            }
        }

        /// <summary>
        /// Authoring contract: place this component by itself on a separate
        /// top-level root named __CAMPAIGN_LOADOUT_EQUIPMENT. The referenced
        /// state and carryover may be scene bootstrap instances; at runtime the
        /// bridge always rebinds to CampaignStateService.Instance.
        /// </summary>
        public void Configure(
            CampaignStateService state,
            CampaignInventoryCarryover carryover,
            GameObject riflePickup,
            GameObject rifleAmmoPickup,
            GameObject radarPickup,
            gunStats rifleStats)
        {
            Configure(
                state,
                carryover,
                riflePickup,
                rifleAmmoPickup,
                radarPickup,
                null,
                rifleStats,
                null);
        }

        public void Configure(
            CampaignStateService state,
            CampaignInventoryCarryover carryover,
            GameObject riflePickup,
            GameObject rifleAmmoPickup,
            GameObject radarPickup,
            gunStats pistolStats,
            gunStats rifleStats,
            gunStats shotgunStats)
        {
            campaignState = state;
            inventoryCarryover = carryover;
            rifleInventoryPickup = riflePickup;
            rifleAmmoInventoryPickup = rifleAmmoPickup;
            radarInventoryPickup = radarPickup;
            pistolDefinition = pistolStats;
            rifleDefinition = rifleStats;
            shotgunDefinition = shotgunStats;
            ResetRuntimeEquipmentState();

            if (Application.isPlaying && instance == this)
            {
                RebindCampaignSubscriptions();
                RebindLoadoutStation();
                StartDeferredReconcile();
            }
        }

        public bool ValidateConfiguration(out string error)
        {
            if (transform.parent != null ||
                !string.Equals(
                    gameObject.name,
                    AuthoringRootName,
                    StringComparison.Ordinal))
            {
                error =
                    $"Campaign loadout equipment must be a top-level root named '{AuthoringRootName}'.";
                return false;
            }

            Component[] rootComponents = GetComponents<Component>();
            if (rootComponents.Length != 2 ||
                GetComponent<Transform>() == null)
            {
                error =
                    "Campaign loadout equipment root must contain only Transform and CampaignLoadoutEquipmentBridge.";
                return false;
            }

            if (campaignState == null || inventoryCarryover == null ||
                inventoryCarryover.gameObject != campaignState.gameObject ||
                campaignState.GetComponent<CampaignInventoryCarryover>() !=
                inventoryCarryover)
            {
                error =
                    "Campaign loadout equipment requires the matching CampaignStateService and CampaignInventoryCarryover authority.";
                return false;
            }

            if (!ValidateItemPickup(
                    rifleInventoryPickup,
                    RifleItemName,
                    "rifle",
                    out error) ||
                !ValidateItemPickup(
                    rifleAmmoInventoryPickup,
                    RifleAmmoItemName,
                    "rifle ammunition",
                    out error) ||
                !ValidateItemPickup(
                    radarInventoryPickup,
                    RadarItemName,
                    "radar",
                    out error))
            {
                return false;
            }

            ItemStats rifleItem = CampaignInventoryTokenUtility.GetItemStats(
                rifleInventoryPickup);
            ItemStats ammoItem = CampaignInventoryTokenUtility.GetItemStats(
                rifleAmmoInventoryPickup);
            ItemStats radarItem = CampaignInventoryTokenUtility.GetItemStats(
                radarInventoryPickup);
            if (ReferenceEquals(rifleItem, ammoItem) ||
                ReferenceEquals(rifleItem, radarItem) ||
                ReferenceEquals(ammoItem, radarItem))
            {
                error =
                    "Campaign loadout equipment ownership tokens must use three distinct ItemStats values.";
                return false;
            }

            if (!ValidateGunDefinition(
                    pistolDefinition,
                    "pistol",
                    out error) ||
                !ValidateGunDefinition(
                    rifleDefinition,
                    "rifle",
                    out error) ||
                !ValidateGunDefinition(
                    shotgunDefinition,
                    "shotgun",
                    out error))
            {
                return false;
            }

            if (pistolDefinition.gunModel == rifleDefinition.gunModel ||
                pistolDefinition.gunModel == shotgunDefinition.gunModel ||
                rifleDefinition.gunModel == shotgunDefinition.gunModel)
            {
                error =
                    "Stable Safety gun definitions must expose distinct authored gun models.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Reconciles the fully initialized destination player immediately.
        /// Repeated calls for the same player are strictly idempotent.
        /// </summary>
        public bool TryReconcileNow()
        {
            if (instance != this)
                return false;

            ResolveLiveCampaignAuthority();
            if (campaignState == null ||
                CampaignStateService.Instance != campaignState ||
                inventoryCarryover == null ||
                inventoryCarryover !=
                    campaignState.GetComponent<CampaignInventoryCarryover>() ||
                inventoryCarryover.IsRestoreInProgress ||
                inventoryCarryover.HasPendingRestoreFailure)
            {
                return false;
            }

            if (!ValidateConfiguration(out string configurationError))
            {

                return false;
            }

            gameManager manager = gameManager.instance;
            GameObject player = manager != null ? manager.player : null;
            playerController controller = player != null
                ? player.GetComponent<playerController>()
                : null;
            Inventory inventory = player != null
                ? player.GetComponent<Inventory>()
                : null;

            if (manager == null || player == null || controller == null ||
                inventory == null || inventory.inventoryItems == null ||
                inventory.inventoryItems.Length == 0 ||
                manager.player != player ||
                manager.playerController != controller ||
                controller.gameObject != inventory.gameObject)
            {
                return false;
            }

            int rifleQuantity =
                GetInventoryQuantity(inventory, rifleInventoryPickup);
            bool newPlayerBinding =
                !ReferenceEquals(boundPlayerController, controller);
            SanitizeSafetyGunInventory(controller);
            gunStats loadedRifle = null;
            int loadedRifleIndex = -1;
            bool hasLoadedRifle = rifleQuantity > 0 &&
                                  TryFindLoadedRifle(
                                      controller,
                                      out loadedRifle,
                                      out loadedRifleIndex);
            bool rifleGrantPending = rifleQuantity > 0 &&
                                     !hasLoadedRifle &&
                                     (newPlayerBinding ||
                                      !rifleGrantAttempted);

            // Safety's animated Player resolves its child Animator in Start,
            // and getGunStats immediately drives that Animator while changing
            // presentation. Scene restore can announce completion before an
            // unusual Player lifecycle has reached Start. Treat that as not
            // ready instead of letting Safety partially add the gun and then
            // throw; the deferred reconcile can safely retry next frame.
            if ((rifleGrantPending || hasLoadedRifle) &&
                (controller.animator == null ||
                 controller.animator.runtimeAnimatorController == null))
            {
                return false;
            }

            BindPlayer(controller);

            bool succeeded = true;
            if (rifleQuantity > 0 && hasLoadedRifle &&
                !rifleGrantAttempted)
            {
                // Safety Save/Load now persists gunInv. Treat an already
                // loaded matching rifle as the equipment authority instead
                // of appending a second rifle for the same durable token.
                rifleGrantAttempted = true;
                var loadedGunsBefore =
                    new List<gunStats>(controller.gunInv);
                int loadedSelectionBefore = controller.gunInvPos;
                try
                {
                    if (!TryNormalizeLoadedRiflesPreservingSelection(
                            controller,
                            loadedRifle,
                            loadedRifleIndex,
                            out string adoptionError))
                    {
                        throw new InvalidOperationException(adoptionError);
                    }

                    rifleGrantSucceeded = true;
                    rifleAdoptedFromSafetySave = true;
                    safetyCheckpointSynchronized = false;
                }
                catch (Exception exception)
                {
                    controller.gunInv = loadedGunsBefore;
                    controller.gunInvPos = loadedSelectionBefore;
                    rifleGrantSucceeded = false;
                    succeeded = false;

                }
            }

            if (rifleQuantity > 0 && !rifleGrantAttempted)
            {
                // playerController adds to its private gun inventory before it
                // changes presentation. Mark the attempt first so a downstream
                // presentation exception cannot cause a duplicate on retry.
                rifleGrantAttempted = true;
                rifleGrantAttemptCount++;

                try
                {
                    List<gunStats> gunsBeforeGrant =
                        new List<gunStats>(controller.gunInv);
                    int selectionBeforeGrant = controller.gunInvPos;
                    bool preserveExistingSelection =
                        gunsBeforeGrant.Count > 0 &&
                        selectionBeforeGrant >= 0 &&
                        selectionBeforeGrant < gunsBeforeGrant.Count;
                    gunStats runtimeRifle = GetOrCreateRuntimeRifle();
                    IPickupGun pickupAuthority = controller;
                    pickupAuthority.getGunStats(runtimeRifle);
                    if (preserveExistingSelection)
                    {
                        controller.gunInvPos = selectionBeforeGrant;
                        if (!TryPresentCurrentGunSelection(
                                controller,
                                out string selectionError))
                        {
                            controller.gunInv = gunsBeforeGrant;
                            controller.gunInvPos = selectionBeforeGrant;
                            throw new InvalidOperationException(
                                selectionError);
                        }
                    }

                    rifleGrantSucceeded = true;
                    rifleAdoptedFromSafetySave = false;
                    safetyCheckpointSynchronized = false;
                }
                catch (Exception exception)
                {
                    rifleGrantSucceeded = false;
                    succeeded = false;

                }
            }

            if (GetInventoryQuantity(inventory, radarInventoryPickup) > 0 &&
                !radarActivationAttempted)
            {
                radarActivationAttempted = true;
                radarActivationAttemptCount++;

                try
                {
                    manager.ActivateRadar(true);
                    radarActivationSucceeded = true;
                    safetyCheckpointSynchronized = false;
                }
                catch (Exception exception)
                {
                    radarActivationSucceeded = false;
                    succeeded = false;

                }
            }

            if (succeeded && Application.isPlaying &&
                !safetyCheckpointSynchronized)
            {
                if (CampaignSafetySaveIntegration.TrySaveCurrentGame(
                        out string saveError))
                {
                    safetyCheckpointSynchronized = true;
                }
                else
                {
                    succeeded = false;
                    inventoryCarryover.MarkInventoryRecoveryPending(
                        "Campaign equipment was applied, but the paired " +
                        $"Safety checkpoint failed: {saveError}");
                }
            }

            if (succeeded)
                boundSafetyGunInventory = controller.gunInv;

            return succeeded;
        }

        private static bool ValidateItemPickup(
            GameObject pickup,
            string expectedName,
            string label,
            out string error)
        {
            ItemStats stats =
                CampaignInventoryTokenUtility.GetItemStats(pickup);
            if (pickup == null || stats == null ||
                !string.Equals(
                    stats.itemName?.Trim(),
                    expectedName,
                    StringComparison.Ordinal) ||
                stats.quantity <= 0 || stats.stackSize <= 0)
            {
                error =
                    $"Campaign loadout equipment requires the authored {label} Item pickup '{expectedName}'.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool ValidateGunDefinition(
            gunStats definition,
            string label,
            out string error)
        {
            if (definition == null || definition.gunModel == null ||
                definition.gunModel.GetComponent<MeshFilter>() == null ||
                definition.gunModel.GetComponent<MeshRenderer>() == null ||
                definition.bullet == null || definition.ammoMax <= 0)
            {
                error =
                    $"Campaign loadout equipment requires a usable {label} gunStats source with model, renderer, bullet, and positive magazine capacity.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        internal bool TryCaptureStableGunCheckpoint(
            playerController controller,
            out CampaignStableGunCheckpoint checkpoint,
            out string error)
        {
            checkpoint = null;
            if (!ValidateStableGunCatalog(out error) || controller == null)
            {
                if (controller == null)
                    error = "The active Safety playerController is missing.";
                return false;
            }

            // JsonUtility cannot preserve runtime ScriptableObject gun
            // references across Safety saves. A source player can therefore
            // briefly retain null/destroyed entries or an out-of-range
            // selection while its authoritative campaign checkpoint is being
            // captured. Those entries cannot represent owned equipment, so
            // remove only them before taking the stable snapshot. Any live
            // gun that is not one of the authored definitions remains a hard
            // failure below; this is deliberately not a permissive fallback
            // for unknown or corrupt gun data.
            SanitizeSafetyGunInventory(controller);
            List<gunStats> guns = controller.gunInv;
            string[] ids = new string[guns.Count];
            int[] ammo = new int[guns.Count];
            for (int index = 0; index < guns.Count; index++)
            {
                gunStats gun = guns[index];
                if (!TryGetStableGunId(gun, out string stableId,
                        out gunStats definition) ||
                    gun.ammoCurr < 0 || gun.ammoCurr > definition.ammoMax)
                {
                    error =
                        $"Safety gun slot {index} cannot be represented by the authored stable gun catalog.";
                    return false;
                }

                ids[index] = stableId;
                ammo[index] = gun.ammoCurr;
            }

            int selectedIndex = guns.Count > 0
                ? controller.gunInvPos
                : -1;
            if ((guns.Count == 0 && controller.gunInvPos != 0) ||
                (guns.Count > 0 &&
                 (selectedIndex < 0 || selectedIndex >= guns.Count)))
            {
                error = "Safety gun selection is outside its live gun list.";
                return false;
            }

            checkpoint = new CampaignStableGunCheckpoint
            {
                isAuthoritative = true,
                gunIds = ids,
                ammo = ammo,
                selectedIndex = selectedIndex
            };
            error = string.Empty;
            return true;
        }

        internal bool TryRestoreStableGunCheckpoint(
            playerController controller,
            string sceneName,
            out bool restored,
            out string error)
        {
            restored = false;
            if (!CampaignSafetySaveIntegration.TryReadStableGunCheckpoint(
                    sceneName,
                    out CampaignStableGunCheckpoint checkpoint,
                    out bool found,
                    out error))
            {
                return false;
            }

            if (!found)
            {
                SanitizeSafetyGunInventory(controller);
                error = string.Empty;
                return true;
            }

            if (DoesCurrentGunInventoryMatchCheckpoint(
                    controller,
                    checkpoint))
            {
                if (controller.animator != null &&
                    controller.animator.runtimeAnimatorController != null &&
                    !TryPresentCurrentGunSelection(controller, out error))
                {
                    return false;
                }

                restored = true;
                error = string.Empty;
                return true;
            }

            if (pendingStableGunGeneration.Count > 0)
            {
                error =
                    "A stable Safety gun restore is already pending transaction completion.";
                return false;
            }

            if (!TryBuildStableGunInventory(
                    checkpoint,
                    out List<gunStats> restoredGuns,
                    out int selectedIndex,
                    out error))
            {
                return false;
            }

            List<gunStats> previousGuns = controller.gunInv;
            int previousSelection = controller.gunInvPos;
            try
            {
                controller.gunInv = restoredGuns;
                controller.gunInvPos = selectedIndex >= 0
                    ? selectedIndex
                    : 0;
                if (controller.animator != null &&
                    controller.animator.runtimeAnimatorController != null &&
                    !TryPresentCurrentGunSelection(controller, out error))
                {
                    throw new InvalidOperationException(error);
                }

                pendingStableGunGeneration =
                    new List<gunStats>(restoredGuns);
                restored = true;
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                controller.gunInv = previousGuns ?? new List<gunStats>();
                controller.gunInvPos = previousSelection;
                DestroyStableGunGeneration(restoredGuns);
                error = exception.Message;
                return false;
            }
        }

        internal bool TryBuildStableGunInventory(
            CampaignStableGunCheckpoint checkpoint,
            out List<gunStats> guns,
            out int selectedIndex,
            out string error)
        {
            guns = new List<gunStats>();
            selectedIndex = -1;
            if (!ValidateStableGunCatalog(out error) ||
                !CampaignSafetySaveIntegration.ValidateStableGunShape(
                    checkpoint,
                    out error))
            {
                return false;
            }

            for (int index = 0; index < checkpoint.gunIds.Length; index++)
            {
                if (!TryGetStableGunDefinition(
                        checkpoint.gunIds[index],
                        out gunStats definition))
                {
                    DestroyStableGunGeneration(guns);
                    guns = new List<gunStats>();
                    error =
                        $"Stable Safety gun ID '{checkpoint.gunIds[index]}' is unsupported.";
                    return false;
                }

                int savedAmmo = checkpoint.ammo[index];
                if (savedAmmo < 0 || savedAmmo > definition.ammoMax)
                {
                    DestroyStableGunGeneration(guns);
                    guns = new List<gunStats>();
                    error =
                        $"Stable Safety gun slot {index} has invalid ammunition.";
                    return false;
                }

                gunStats runtimeGun = Instantiate(definition);
                runtimeGun.name =
                    $"{definition.name} (Stable Runtime {index})";
                runtimeGun.hideFlags = HideFlags.DontSave;
                runtimeGun.ammoCurr = savedAmmo;
                guns.Add(runtimeGun);
            }

            selectedIndex = checkpoint.selectedIndex;
            error = string.Empty;
            return true;
        }

        internal void CommitStableGunRestore()
        {
            if (pendingStableGunGeneration.Count == 0)
                return;

            DestroyStableGunGeneration(activeStableGunGeneration);
            activeStableGunGeneration = pendingStableGunGeneration;
            pendingStableGunGeneration = new List<gunStats>();
        }

        internal void RollbackStableGunRestore()
        {
            DestroyStableGunGeneration(pendingStableGunGeneration);
            pendingStableGunGeneration = new List<gunStats>();
        }

        private bool DoesCurrentGunInventoryMatchCheckpoint(
            playerController controller,
            CampaignStableGunCheckpoint checkpoint)
        {
            List<gunStats> guns = controller?.gunInv;
            if (guns == null || guns.Count != checkpoint.gunIds.Length ||
                (guns.Count > 0
                    ? controller.gunInvPos != checkpoint.selectedIndex
                    : checkpoint.selectedIndex != -1))
            {
                return false;
            }

            for (int index = 0; index < guns.Count; index++)
            {
                if (!TryGetStableGunId(
                        guns[index],
                        out string stableId,
                        out _) ||
                    !string.Equals(
                        stableId,
                        checkpoint.gunIds[index],
                        StringComparison.Ordinal) ||
                    guns[index].ammoCurr != checkpoint.ammo[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static void DestroyStableGunGeneration(
            IEnumerable<gunStats> generation)
        {
            foreach (gunStats gun in generation ?? Array.Empty<gunStats>())
            {
                if (gun == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(gun);
                else
                    DestroyImmediate(gun);
            }
        }

        internal bool TryPresentCurrentGunSelection(
            playerController controller,
            out string error)
        {
            if (controller == null)
            {
                error = "The Safety playerController is missing.";
                return false;
            }

            List<gunStats> ordered = controller.gunInv ?? new List<gunStats>();
            if (ordered.Count == 0)
            {
                controller.gunInv = ordered;
                controller.gunInvPos = 0;
                ClearGunPresentation(controller);
                controller.updatePlayerAmmo();
                error = string.Empty;
                return true;
            }

            int selected = controller.gunInvPos;
            if (selected < 0 || selected >= ordered.Count ||
                ordered[selected] == null ||
                ordered[selected].gunModel == null ||
                ordered[selected].gunModel.GetComponent<MeshFilter>() == null ||
                ordered[selected].gunModel.GetComponent<MeshRenderer>() == null ||
                controller.animator == null ||
                controller.animator.runtimeAnimatorController == null)
            {
                error =
                    "The selected Safety gun cannot be presented by the animated Player.";
                return false;
            }

            try
            {
                gunStats selectedGun = ordered[selected];
                var presentationList = new List<gunStats>(ordered);
                presentationList.RemoveAt(selected);
                controller.gunInv = presentationList;
                ((IPickupGun)controller).getGunStats(selectedGun);
                controller.gunInv = ordered;
                controller.gunInvPos = selected;
                controller.updatePlayerAmmo();
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                controller.gunInv = ordered;
                controller.gunInvPos = selected;
                error = exception.Message;
                return false;
            }
        }

        private bool ValidateStableGunCatalog(out string error)
        {
            if (!ValidateGunDefinition(pistolDefinition, "pistol", out error) ||
                !ValidateGunDefinition(rifleDefinition, "rifle", out error) ||
                !ValidateGunDefinition(shotgunDefinition, "shotgun", out error))
            {
                return false;
            }

            if (pistolDefinition.gunModel == rifleDefinition.gunModel ||
                pistolDefinition.gunModel == shotgunDefinition.gunModel ||
                rifleDefinition.gunModel == shotgunDefinition.gunModel)
            {
                error = "Stable Safety gun catalog models are not unique.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool TryGetStableGunId(
            gunStats gun,
            out string stableId,
            out gunStats definition)
        {
            if (MatchesGunDefinition(gun, pistolDefinition))
            {
                stableId = PistolStableId;
                definition = pistolDefinition;
                return true;
            }

            if (MatchesGunDefinition(gun, rifleDefinition))
            {
                stableId = RifleStableId;
                definition = rifleDefinition;
                return true;
            }

            if (MatchesGunDefinition(gun, shotgunDefinition))
            {
                stableId = ShotgunStableId;
                definition = shotgunDefinition;
                return true;
            }

            stableId = string.Empty;
            definition = null;
            return false;
        }

        private bool TryGetStableGunDefinition(
            string stableId,
            out gunStats definition)
        {
            switch (stableId?.Trim())
            {
                case PistolStableId:
                    definition = pistolDefinition;
                    return definition != null;
                case RifleStableId:
                    definition = rifleDefinition;
                    return definition != null;
                case ShotgunStableId:
                    definition = shotgunDefinition;
                    return definition != null;
                default:
                    definition = null;
                    return false;
            }
        }

        private static bool MatchesGunDefinition(
            gunStats candidate,
            gunStats definition)
        {
            if (candidate == null || definition == null)
                return false;

            // Pickups can hand the canonical authored ScriptableObject
            // directly to Safety. Treat that exact object as its own durable
            // identity even if a safe runtime system has altered a mutable
            // presentation field. Runtime clones still have to match the
            // stable visual/combat fingerprint below.
            if (ReferenceEquals(candidate, definition))
                return true;

            return candidate.gunModel == definition.gunModel &&
                   candidate.bullet == definition.bullet &&
                   candidate.ammoMax == definition.ammoMax &&
                   candidate.gunType == definition.gunType;
        }

        private static void ClearGunPresentation(playerController controller)
        {
            Transform model = controller.transform.Find(
                "First Person Gun Model");
            if (model != null)
            {
                MeshFilter filter = model.GetComponent<MeshFilter>();
                MeshRenderer renderer = model.GetComponent<MeshRenderer>();
                if (filter != null)
                    filter.sharedMesh = null;
                if (renderer != null)
                    renderer.sharedMaterial = null;
            }

            if (controller.animator != null)
                controller.animator.SetInteger("WeaponType", 0);
        }

        private static int GetInventoryQuantity(
            Inventory inventory,
            GameObject pickup)
        {
            ItemStats item =
                CampaignInventoryTokenUtility.GetItemStats(pickup);
            return inventory != null && item != null
                ? Mathf.Max(0, inventory.FindItem(item).Value)
                : 0;
        }

        private gunStats GetOrCreateRuntimeRifle()
        {
            if (runtimeRifleDefinition != null)
                return runtimeRifleDefinition;

            runtimeRifleDefinition = Instantiate(rifleDefinition);
            runtimeRifleDefinition.name =
                $"{rifleDefinition.name} (Campaign Runtime)";
            runtimeRifleDefinition.hideFlags = HideFlags.DontSave;
            runtimeRifleDefinition.ammoCurr =
                runtimeRifleDefinition.ammoMax;
            return runtimeRifleDefinition;
        }

        private static void SanitizeSafetyGunInventory(
            playerController controller)
        {
            if (controller.gunInv == null)
            {
                controller.gunInv = new List<gunStats>();
                controller.gunInvPos = 0;
                return;
            }

            controller.gunInv.RemoveAll(gun => gun == null);
            ClampSafetyGunSelection(controller);
        }

        private bool TryFindLoadedRifle(
            playerController controller,
            out gunStats loadedRifle,
            out int loadedRifleIndex)
        {
            loadedRifle = null;
            loadedRifleIndex = -1;
            if (controller == null || controller.gunInv == null)
                return false;

            int selectedIndex = controller.gunInv.Count > 0
                ? Mathf.Clamp(
                    controller.gunInvPos,
                    0,
                    controller.gunInv.Count - 1)
                : -1;
            if (selectedIndex >= 0 &&
                IsMatchingLoadedRifle(controller.gunInv[selectedIndex]))
            {
                loadedRifle = controller.gunInv[selectedIndex];
                loadedRifleIndex = selectedIndex;
                return true;
            }

            for (int index = 0; index < controller.gunInv.Count; index++)
            {
                gunStats gun = controller.gunInv[index];
                if (!IsMatchingLoadedRifle(gun))
                    continue;

                loadedRifle = gun;
                loadedRifleIndex = index;
                return true;
            }

            return false;
        }

        private bool TryNormalizeLoadedRiflesPreservingSelection(
            playerController controller,
            gunStats retainedRifle,
            int retainedRifleIndex,
            out string error)
        {
            List<gunStats> source = controller?.gunInv;
            if (source == null || retainedRifle == null ||
                retainedRifleIndex < 0 ||
                retainedRifleIndex >= source.Count ||
                !IsMatchingLoadedRifle(source[retainedRifleIndex]))
            {
                error = "The Safety-loaded rifle selection is invalid.";
                return false;
            }

            int oldSelected = source.Count > 0
                ? Mathf.Clamp(controller.gunInvPos, 0, source.Count - 1)
                : -1;
            var normalized = new List<gunStats>(source.Count);
            int newSelected = -1;
            for (int index = 0; index < source.Count; index++)
            {
                gunStats gun = source[index];
                bool matchingRifle = IsMatchingLoadedRifle(gun);
                if (matchingRifle && index != retainedRifleIndex)
                    continue;

                if (index == oldSelected)
                    newSelected = normalized.Count;
                normalized.Add(
                    index == retainedRifleIndex ? retainedRifle : gun);
            }

            if (normalized.Count == 0)
            {
                error = "Rifle normalization removed the entire gun list.";
                return false;
            }

            if (newSelected < 0)
            {
                // The selected slot was a duplicate rifle that was removed.
                // Select the retained equivalent without changing its ammo.
                newSelected = normalized.IndexOf(retainedRifle);
            }

            controller.gunInv = normalized;
            controller.gunInvPos = Mathf.Clamp(
                newSelected,
                0,
                normalized.Count - 1);
            return TryPresentCurrentGunSelection(controller, out error);
        }

        private bool IsMatchingLoadedRifle(gunStats gun)
        {
            return gun != null && gun.gunModel != null &&
                   gun.bullet != null &&
                   gun.gunModel == rifleDefinition.gunModel &&
                   gun.bullet == rifleDefinition.bullet &&
                   gun.gunModel.GetComponent<MeshFilter>() != null &&
                   gun.gunModel.GetComponent<MeshRenderer>() != null;
        }

        private static void ClampSafetyGunSelection(
            playerController controller)
        {
            int count = controller?.gunInv?.Count ?? 0;
            controller.gunInvPos = count > 0
                ? Mathf.Clamp(controller.gunInvPos, 0, count - 1)
                : 0;
        }

        private void BindPlayer(playerController controller)
        {
            if (ReferenceEquals(boundPlayerController, controller))
                return;

            boundPlayerController = controller;
            boundSafetyGunInventory = null;
            rifleGrantAttempted = false;
            rifleGrantSucceeded = false;
            rifleAdoptedFromSafetySave = false;
            radarActivationAttempted = false;
            radarActivationSucceeded = false;
            safetyCheckpointSynchronized = false;
        }

        private void ResolveLiveCampaignAuthority()
        {
            CampaignStateService liveState = CampaignStateService.Instance;
            if (liveState == null)
                return;

            campaignState = liveState;
            inventoryCarryover =
                liveState.GetComponent<CampaignInventoryCarryover>();
        }

        private void RebindCampaignSubscriptions()
        {
            ResolveLiveCampaignAuthority();

            CampaignStateService nextState =
                CampaignStateService.Instance == campaignState
                    ? campaignState
                    : null;
            CampaignInventoryCarryover nextCarryover = nextState != null
                ? nextState.GetComponent<CampaignInventoryCarryover>()
                : null;

            if (subscribedState != nextState)
            {
                if (subscribedState != null)
                    subscribedState.NewGameStarted -= HandleNewGameStarted;

                subscribedState = nextState;
                if (subscribedState != null)
                    subscribedState.NewGameStarted += HandleNewGameStarted;
            }

            if (subscribedCarryover != nextCarryover)
            {
                if (subscribedCarryover != null)
                {
                    subscribedCarryover.RestoreCompleted -=
                        HandleRestoreCompleted;
                }

                subscribedCarryover = nextCarryover;
                if (subscribedCarryover != null)
                {
                    subscribedCarryover.RestoreCompleted +=
                        HandleRestoreCompleted;
                }
            }
        }

        private void UnbindCampaignSubscriptions()
        {
            if (subscribedState != null)
                subscribedState.NewGameStarted -= HandleNewGameStarted;
            if (subscribedCarryover != null)
            {
                subscribedCarryover.RestoreCompleted -=
                    HandleRestoreCompleted;
            }

            subscribedState = null;
            subscribedCarryover = null;
        }

        private void RebindLoadoutStation()
        {
            HubLoadoutStation nextStation = null;
            Scene activeScene = SceneManager.GetActiveScene();
            HubLoadoutStation[] stations =
                FindObjectsByType<HubLoadoutStation>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            foreach (HubLoadoutStation station in stations)
            {
                if (station != null && station.gameObject.scene == activeScene)
                {
                    nextStation = station;
                    break;
                }
            }

            if (subscribedLoadoutStation == nextStation)
                return;

            UnbindLoadoutStation();
            subscribedLoadoutStation = nextStation;
            if (subscribedLoadoutStation != null)
            {
                subscribedLoadoutStation.LoadoutApplied +=
                    HandleLoadoutApplied;
            }
        }

        private void UnbindLoadoutStation()
        {
            if (subscribedLoadoutStation != null)
            {
                subscribedLoadoutStation.LoadoutApplied -=
                    HandleLoadoutApplied;
            }

            subscribedLoadoutStation = null;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (instance != this)
                return;

            RebindCampaignSubscriptions();
            RebindLoadoutStation();
            StartDeferredReconcile();
        }

        private void HandleRestoreCompleted(bool succeeded)
        {
            if (instance == this && succeeded)
            {
                playerController controller =
                    gameManager.instance != null
                        ? gameManager.instance.playerController
                        : null;
                if (controller != null &&
                    ReferenceEquals(controller, boundPlayerController) &&
                    !ReferenceEquals(
                        controller.gunInv,
                        boundSafetyGunInventory))
                {
                    rifleGrantAttempted = false;
                    rifleGrantSucceeded = false;
                    rifleAdoptedFromSafetySave = false;
                    radarActivationAttempted = false;
                    radarActivationSucceeded = false;
                    safetyCheckpointSynchronized = false;
                }

                TryReconcileNow();
            }
        }

        private void HandleLoadoutApplied(
            string loadoutId,
            bool succeeded,
            string message)
        {
            if (instance == this && succeeded)
                TryReconcileNow();
        }

        private void HandleNewGameStarted()
        {
            if (instance == this)
                ResetRuntimeEquipmentState();
        }

        private void StartDeferredReconcile()
        {
            if (!Application.isPlaying || !isActiveAndEnabled ||
                instance != this)
            {
                return;
            }

            StopDeferredReconcile();
            deferredReconcileRoutine =
                StartCoroutine(ReconcileAfterSceneStart());
        }

        private void StopDeferredReconcile()
        {
            if (deferredReconcileRoutine == null)
                return;

            StopCoroutine(deferredReconcileRoutine);
            deferredReconcileRoutine = null;
        }

        private IEnumerator ReconcileAfterSceneStart()
        {
            // CampaignInventoryCarryover starts its restore from sceneLoaded.
            // Waiting one frame ensures its pending state is visible regardless
            // of event subscription order.
            yield return null;

            ResolveLiveCampaignAuthority();
            while (inventoryCarryover != null &&
                   inventoryCarryover.IsRestoreInProgress)
            {
                yield return null;
            }

            if (inventoryCarryover != null &&
                !inventoryCarryover.HasPendingRestoreFailure)
            {
                TryReconcileNow();
            }

            deferredReconcileRoutine = null;
        }

        private void ResetRuntimeEquipmentState()
        {
            boundPlayerController = null;
            boundSafetyGunInventory = null;
            rifleGrantAttempted = false;
            rifleGrantSucceeded = false;
            rifleAdoptedFromSafetySave = false;
            radarActivationAttempted = false;
            radarActivationSucceeded = false;
            rifleGrantAttemptCount = 0;
            radarActivationAttemptCount = 0;
            safetyCheckpointSynchronized = false;

            DestroyStableGunGeneration(activeStableGunGeneration);
            DestroyStableGunGeneration(pendingStableGunGeneration);
            activeStableGunGeneration = new List<gunStats>();
            pendingStableGunGeneration = new List<gunStats>();

            if (runtimeRifleDefinition == null)
                return;

            gunStats runtime = runtimeRifleDefinition;
            runtimeRifleDefinition = null;
            if (Application.isPlaying)
                Destroy(runtime);
            else
                DestroyImmediate(runtime);
        }
    }
}
