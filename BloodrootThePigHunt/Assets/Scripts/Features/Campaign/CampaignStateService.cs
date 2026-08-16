using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Bloodroot.Campaign
{
    /// <summary>
    /// Persistent authority for campaign progression and scene-arrival state.
    /// Add one authored instance to the campaign's first scene. It survives
    /// scene changes; duplicate authored instances safely destroy themselves.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class CampaignStateService : MonoBehaviour
    {
        public const string SaveFileName = "bloodroot-campaign.json";

        private const string LegacyCompanyName = "DefaultCompany";
        private const string LegacyProductName = "My project";

        private static CampaignStateService instance;

        private CampaignSaveData state = CampaignSaveData.CreateNewGame();
        private bool allowSaveWrites = true;
        private bool newerLegacySaveBlocksDowngrade;
        private string newerLegacySaveWarning = string.Empty;
        private string[] stagedAreaCompletionInventoryIds =
            Array.Empty<string>();
        private int[] stagedAreaCompletionInventoryQuantities =
            Array.Empty<int>();
        private bool hasStagedAreaCompletionInventory;

#if UNITY_EDITOR
        private Func<bool> editorSaveOverride;
#endif

        // Online safety owns the legacy gameManager/menu implementation. The
        // campaign scenes intentionally adapt to that implementation here so
        // safety-owned scripts and prefabs remain untouched. These cached
        // fields are limited to the three serialized menu references needed to
        // guard incomplete prototype panels in the campaign build.
        private static readonly BindingFlags SafetyMenuFieldFlags =
            BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo SafetyMenuActiveField =
            typeof(gameManager).GetField("menuActive", SafetyMenuFieldFlags);
        private static readonly FieldInfo SafetyMenuPauseField =
            typeof(gameManager).GetField("menuPause", SafetyMenuFieldFlags);
        private static readonly FieldInfo SafetyMenuOptionsField =
            typeof(gameManager).GetField("menuOptions", SafetyMenuFieldFlags);
        private static readonly FieldInfo SafetyMenuInventoryField =
            typeof(gameManager).GetField("menuInventory", SafetyMenuFieldFlags);

        private gameManager safetyMenuManager;
        private gameManager suppressedSafetyMenuManager;
        private bool restoreSuppressedSafetyMenuManager;
        private bool warnedAboutSafetyMenuContract;

        public static CampaignStateService Instance => instance;

        public CampaignProgressSnapshot Current => state.Snapshot;

        public bool HasCompletedPrologue => state.prologueCompleted;

        public bool HasAllNameStonesOffered =>
            state.HasAllNameStonesOffered();

        public bool CanEnterHollow =>
            state.harrowCompleted && state.HasAllNameStonesOffered();

        public bool IsHeartrootCarried =>
            state.heartrootCarried && !state.heartrootBurned;

        public bool IsCampaignCompleted => state.campaignCompleted;

        public bool PrologueCursedObjectRevealed =>
            state.prologueCursedObjectRevealed;

        public bool PrologueCursedObjectOffered =>
            state.prologueCursedObjectOffered;

        public string PendingRootOfferingId =>
            state.pendingRootOfferingId ?? string.Empty;

        public string ActiveFarmEmergenceOfferingId =>
            state.activeFarmEmergenceOfferingId ?? string.Empty;

        public string NextPendingFarmEmergenceOfferingId =>
            state.FindNextPendingFarmEmergenceOfferingId();

        public bool HasUnresolvedFarmEmergence =>
            state.HasUnresolvedFarmEmergence();

        /// <summary>
        /// Compatibility view retained for existing Name Stone presentation.
        /// The prologue offering is available only through the generic Root
        /// transaction API.
        /// </summary>
        public string PendingNameStoneOfferId =>
            CampaignNameStoneIds.IsCanonical(PendingRootOfferingId)
                ? PendingRootOfferingId
                : string.Empty;

        public string SavePath =>
            Path.Combine(Application.persistentDataPath, SaveFileName);

        public event Action<CampaignProgressSnapshot> ProgressLoaded;
        public event Action<CampaignProgressSnapshot> ProgressChanged;
        public event Action PrologueCompleted;
        public event Action HubIntroductionCompleted;
        public event Action<CampaignAreaId> AreaUnlocked;
        public event Action<CampaignAreaId> AreaCompleted;
        public event Action<string> EvidenceCollected;
        public event Action<string> NameStoneExtracted;
        public event Action<string> NameStoneOfferStarted;
        public event Action<string> NameStoneOfferCanceled;
        public event Action<string> NameStoneOffered;
        public event Action PrologueCursedObjectRevealCommitted;
        public event Action PrologueCursedObjectOfferCommitted;
        public event Action<string> RootOfferingStarted;
        public event Action<string> RootOfferingCanceled;
        public event Action<string> RootOfferingCommitted;
        public event Action<string> FarmEmergenceStarted;
        public event Action<string> FarmEmergenceCompleted;
        public event Action HollowEntryAvailable;
        public event Action HollowTowerActivated;
        public event Action HollowVeilCrossed;
        public event Action<int> HollowWitchDefeated;
        public event Action HeartrootExposed;
        public event Action HeartrootRecovered;
        public event Action HeartrootBurned;
        public event Action CampaignCompleted;
        public event Action NewGameStarted;
        public event Action<string> SaveFailed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
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
            TryMigrateLegacySaveLocation();
            if (newerLegacySaveBlocksDowngrade)
            {
                ApplyNewerLegacyWriteBlock(newerLegacySaveWarning);
            }
            else
            {
                LoadFromDisk();
            }
        }

        private void OnDestroy()
        {
            RestoreSafetyMenuManagerUpdate();

            if (instance == this)
            {
                instance = null;
            }
        }

        private void Update()
        {
            if (instance != this)
                return;

            gameManager manager = gameManager.instance;
            if (manager == null || !manager.enabled)
                return;

            if (safetyMenuManager != manager)
            {
                safetyMenuManager = manager;
                WireAuthoredSafetyOptionsPanel(manager);
            }

            if (!HasSafetyMenuContract())
            {
                if (!warnedAboutSafetyMenuContract)
                {
                    warnedAboutSafetyMenuContract = true;
                    Debug.LogWarning(
                        "Campaign safety-menu compatibility is inactive because " +
                        "the online safety gameManager menu contract changed.",
                        this);
                }

                return;
            }

            if (Input.GetButtonDown("Cancel") &&
                TryHandleSafetyMenuBack(manager))
            {
                return;
            }

            if (Input.GetButtonDown("Inventory"))
            {
                TryHandleSafetyInventoryToggle(manager);
            }
        }

        private void LateUpdate()
        {
            RestoreSafetyMenuManagerUpdate();
        }

        private bool TryHandleSafetyMenuBack(gameManager manager)
        {
            GameObject activeMenu = ReadSafetyMenu(
                SafetyMenuActiveField,
                manager);
            if (activeMenu == null)
                return false;

            GameObject pauseMenu = ReadSafetyMenu(
                SafetyMenuPauseField,
                manager);

            // The safety implementation already handles the authored pause
            // menu correctly when its tracker is present.
            if (activeMenu == pauseMenu && MenuTracker.Instance != null)
                return false;

            SuppressSafetyMenuManagerUpdate(manager);
            activeMenu.SetActive(false);

            GameObject previousMenu = MenuTracker.Instance != null
                ? MenuTracker.Instance.PreviousMenu()
                : null;

            if (previousMenu != null)
            {
                SafetyMenuActiveField.SetValue(manager, previousMenu);
                previousMenu.SetActive(true);
            }
            else
            {
                manager.stateUnpause();
            }

            return true;
        }

        private bool TryHandleSafetyInventoryToggle(gameManager manager)
        {
            GameObject activeMenu = ReadSafetyMenu(
                SafetyMenuActiveField,
                manager);
            GameObject inventoryMenu = ReadSafetyMenu(
                SafetyMenuInventoryField,
                manager);

            // Safety's inventory prototype is not authored into the campaign
            // UI yet. Suppress only that one input frame so its null panel is
            // never dereferenced; all other manager behavior remains active.
            if (activeMenu == null && inventoryMenu == null)
            {
                SuppressSafetyMenuManagerUpdate(manager);
                return true;
            }

            // Safety's close branch clears menuActive before using it. Close
            // the authored panel through the existing public unpause API.
            if (inventoryMenu != null && activeMenu == inventoryMenu)
            {
                SuppressSafetyMenuManagerUpdate(manager);
                manager.stateUnpause();
                return true;
            }

            return false;
        }

        private void WireAuthoredSafetyOptionsPanel(gameManager manager)
        {
            if (manager == null || SafetyMenuOptionsField == null ||
                ReadSafetyMenu(SafetyMenuOptionsField, manager) != null)
            {
                return;
            }

            Transform uiRoot = manager.transform.parent;
            Transform optionsPanel = uiRoot != null
                ? uiRoot.Find("Options Sub-Menu")
                : null;

            if (optionsPanel != null)
            {
                SafetyMenuOptionsField.SetValue(
                    manager,
                    optionsPanel.gameObject);
            }
        }

        private static bool HasSafetyMenuContract()
        {
            return SafetyMenuActiveField != null &&
                   SafetyMenuPauseField != null &&
                   SafetyMenuOptionsField != null &&
                   SafetyMenuInventoryField != null;
        }

        private static GameObject ReadSafetyMenu(
            FieldInfo field,
            gameManager manager)
        {
            return field?.GetValue(manager) as GameObject;
        }

        private void SuppressSafetyMenuManagerUpdate(gameManager manager)
        {
            if (manager == null || !manager.enabled)
                return;

            manager.enabled = false;
            suppressedSafetyMenuManager = manager;
            restoreSuppressedSafetyMenuManager = true;
        }

        private void RestoreSafetyMenuManagerUpdate()
        {
            if (!restoreSuppressedSafetyMenuManager)
                return;

            if (suppressedSafetyMenuManager != null)
            {
                suppressedSafetyMenuManager.enabled = true;
            }

            suppressedSafetyMenuManager = null;
            restoreSuppressedSafetyMenuManager = false;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                SaveNow();
            }
        }

        private void OnApplicationQuit()
        {
            SaveNow();
        }

        public bool LoadFromDisk()
        {
            ClearStagedAreaCompletionInventory();
            CampaignSaveLoadResult loadResult =
                CampaignSaveStore.Load(SavePath);

            state = loadResult.Data ?? CampaignSaveData.CreateNewGame();
            state.Normalize();
            allowSaveWrites = !loadResult.HasNewerUnsupportedVersion;

            if (!string.IsNullOrEmpty(loadResult.Warning))
            {
                Debug.LogWarning(loadResult.Warning, this);
            }

            if (allowSaveWrites &&
                (loadResult.NeedsPrimaryRepair || !loadResult.FoundSave))
            {
                SaveAllRecoveryCopies("initialize or repair");
            }

            CampaignProgressSnapshot snapshot = state.Snapshot;
            CampaignEventUtility.Invoke(ProgressLoaded, snapshot, this);
            CampaignEventUtility.Invoke(ProgressChanged, snapshot, this);
            return loadResult.LoadedSuccessfully;
        }

        internal bool TryExportCheckpoint(
            out string checkpointJson,
            out string error)
        {
            if (!allowSaveWrites)
            {
                checkpointJson = string.Empty;
                error =
                    "A newer unsupported campaign save blocks checkpoint export.";
                return false;
            }

            try
            {
                CampaignSaveData checkpoint = state.Clone();
                checkpoint.Normalize();
                if (!ValidateCheckpointInventory(
                        checkpoint,
                        out error))
                {
                    checkpointJson = string.Empty;
                    return false;
                }

                checkpointJson = JsonUtility.ToJson(checkpoint, true);
                if (string.IsNullOrWhiteSpace(checkpointJson))
                {
                    error = "Campaign checkpoint serialization was empty.";
                    return false;
                }

                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                checkpointJson = string.Empty;
                error = exception.Message;
                return false;
            }
        }

        internal bool TryRestoreCheckpoint(
            string checkpointJson,
            out string error)
        {
            if (!allowSaveWrites)
            {
                error =
                    "A newer unsupported campaign save blocks checkpoint restore.";
                return false;
            }

            CampaignSaveData checkpoint;
            try
            {
                if (string.IsNullOrWhiteSpace(checkpointJson))
                {
                    error = "Campaign checkpoint JSON is empty.";
                    return false;
                }

                checkpoint =
                    JsonUtility.FromJson<CampaignSaveData>(checkpointJson);
                if (checkpoint == null || checkpoint.saveVersion < 1 ||
                    checkpoint.saveVersion > CampaignSaveData.CurrentVersion)
                {
                    error =
                        "Campaign checkpoint version is invalid or newer than this build.";
                    return false;
                }

                if (!ValidateCheckpointInventory(checkpoint, out error))
                    return false;

                checkpoint.Normalize();
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }

            CampaignSaveData previousState = state.Clone();
            bool previousWritePermission = allowSaveWrites;
            string[] previousStagedIds =
                (string[])stagedAreaCompletionInventoryIds.Clone();
            int[] previousStagedQuantities =
                (int[])stagedAreaCompletionInventoryQuantities.Clone();
            bool previousHasStaged = hasStagedAreaCompletionInventory;

            state = checkpoint;
            ClearStagedAreaCompletionInventory();
            if (!SaveAllRecoveryCopies("restore the paired F9 checkpoint"))
            {
                state = previousState;
                allowSaveWrites = previousWritePermission;
                stagedAreaCompletionInventoryIds = previousStagedIds;
                stagedAreaCompletionInventoryQuantities =
                    previousStagedQuantities;
                hasStagedAreaCompletionInventory = previousHasStaged;
                error = "Campaign checkpoint recovery copies could not be written.";
                return false;
            }

            CampaignProgressSnapshot snapshot = state.Snapshot;
            CampaignEventUtility.Invoke(ProgressLoaded, snapshot, this);
            CampaignEventUtility.Invoke(ProgressChanged, snapshot, this);
            error = string.Empty;
            return true;
        }

        internal bool TryValidateCheckpoint(
            string checkpointJson,
            out string error)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(checkpointJson))
                {
                    error = "Campaign checkpoint JSON is empty.";
                    return false;
                }

                CampaignSaveData checkpoint =
                    JsonUtility.FromJson<CampaignSaveData>(checkpointJson);
                if (checkpoint == null || checkpoint.saveVersion < 1 ||
                    checkpoint.saveVersion > CampaignSaveData.CurrentVersion)
                {
                    error = "Campaign checkpoint version is invalid.";
                    return false;
                }

                if (!ValidateCheckpointInventory(checkpoint, out error))
                    return false;

                checkpoint.Normalize();
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        internal bool TryReadCheckpointInventory(
            string checkpointJson,
            out string[] itemIds,
            out int[] quantities,
            out string error)
        {
            itemIds = Array.Empty<string>();
            quantities = Array.Empty<int>();
            error = string.Empty;
            try
            {
                CampaignSaveData checkpoint =
                    JsonUtility.FromJson<CampaignSaveData>(checkpointJson);
                if (checkpoint == null || checkpoint.saveVersion < 1 ||
                    checkpoint.saveVersion > CampaignSaveData.CurrentVersion ||
                    !ValidateCheckpointInventory(checkpoint, out error))
                {
                    error = string.IsNullOrWhiteSpace(error)
                        ? "Campaign checkpoint is invalid."
                        : error;
                    return false;
                }

                itemIds =
                    (string[])checkpoint.carriedInventoryItemIds.Clone();
                quantities =
                    (int[])checkpoint.carriedInventoryQuantities.Clone();
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static bool ValidateCheckpointInventory(
            CampaignSaveData checkpoint,
            out string error)
        {
            string[] ids = checkpoint?.carriedInventoryItemIds;
            int[] quantities = checkpoint?.carriedInventoryQuantities;
            if (ids == null || quantities == null || ids.Length == 0 ||
                ids.Length != quantities.Length)
            {
                error =
                    "Campaign checkpoint inventory arrays are empty or mismatched.";
                return false;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < ids.Length; index++)
            {
                string id = ids[index]?.Trim() ?? string.Empty;
                if (id.Length == 0 || !seen.Add(id) ||
                    quantities[index] < 0)
                {
                    error =
                        "Campaign checkpoint inventory IDs or quantities are invalid.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private void TryMigrateLegacySaveLocation()
        {
            string currentPath = SavePath;
            // The current save store treats its primary, temporary, and
            // backup files as one recovery set. Do not let an older legacy
            // primary mask a valid current .tmp/.bak merely because the
            // current primary itself is absent or corrupt.
            if (HasRecoverableCurrentSave(currentPath))
            {
                return;
            }

            try
            {
                DirectoryInfo currentDirectory =
                    Directory.GetParent(Application.persistentDataPath);
                DirectoryInfo localLowDirectory = currentDirectory?.Parent;
                if (localLowDirectory == null)
                {
                    return;
                }

                string legacyPath = Path.Combine(
                    localLowDirectory.FullName,
                    LegacyCompanyName,
                    LegacyProductName,
                    SaveFileName);
                CampaignSaveLoadResult legacy =
                    CampaignSaveStore.Load(legacyPath);
                if (legacy.HasNewerUnsupportedVersion)
                {
                    newerLegacySaveBlocksDowngrade = true;
                    newerLegacySaveWarning = legacy.Warning;
                    return;
                }

                if (!legacy.LoadedSuccessfully || legacy.Data == null)
                {
                    return;
                }

                legacy.Data.Normalize();
                if (!CampaignSaveStore.TryWriteAllCopies(
                        currentPath,
                        legacy.Data,
                        out string migrationError))
                {
                    Debug.LogWarning(
                        "Could not migrate the legacy campaign save to the " +
                        $"current product folder: {migrationError}",
                        this);
                    return;
                }

                Debug.Log(
                    "Migrated the campaign save from the legacy product " +
                    "folder without deleting the recovery source.",
                    this);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "Legacy campaign save migration was skipped: " +
                    exception.Message,
                    this);
            }
        }

        private static bool HasRecoverableCurrentSave(string currentPath)
        {
            CampaignSaveLoadResult current =
                CampaignSaveStore.Load(currentPath);
            return current.LoadedSuccessfully ||
                   current.HasNewerUnsupportedVersion;
        }

        private void ApplyNewerLegacyWriteBlock(string warning)
        {
            ClearStagedAreaCompletionInventory();
            state = CampaignSaveData.CreateNewGame();
            allowSaveWrites = false;

            string message = string.IsNullOrWhiteSpace(warning)
                ? "A legacy campaign save was created by a newer game " +
                  "version. This version will not overwrite it."
                : warning.Trim();
            Debug.LogWarning(message, this);

            CampaignProgressSnapshot snapshot = state.Snapshot;
            CampaignEventUtility.Invoke(ProgressLoaded, snapshot, this);
            CampaignEventUtility.Invoke(ProgressChanged, snapshot, this);
        }

        public bool SaveNow()
        {
            if (!allowSaveWrites)
            {
                const string message =
                    "Campaign save was created by a newer game version. " +
                    "This version will not overwrite it.";
                CampaignEventUtility.Invoke(SaveFailed, message, this);
                return false;
            }

            state.Normalize();

#if UNITY_EDITOR
            if (editorSaveOverride != null)
            {
                bool saved;
                try
                {
                    saved = editorSaveOverride();
                }
                catch (Exception exception)
                {
                    saved = false;
                    Debug.LogException(exception, this);
                }

                if (!saved)
                {
                    const string editorMessage =
                        "The editor campaign-save test seam rejected the save.";
                    CampaignEventUtility.Invoke(
                        SaveFailed,
                        editorMessage,
                        this);
                }

                return saved;
            }
#endif

            if (CampaignSaveStore.TrySave(SavePath, state, out string error))
            {
                return true;
            }

            string messageWithPath =
                $"Could not save campaign progress to '{SavePath}': {error}";
            Debug.LogError(messageWithPath, this);
            CampaignEventUtility.Invoke(SaveFailed, messageWithPath, this);
            return false;
        }

#if UNITY_EDITOR
        public void ConfigureEditorSaveOverride(Func<bool> saveOverride)
        {
            editorSaveOverride = saveOverride;
        }
#endif

        /// <summary>
        /// Durably accepts the Player's authored thorn-veil crossing before
        /// encounter content or a witch can be activated. Re-entering after
        /// a load is an accepted no-op and emits no duplicate event.
        /// </summary>
        public bool TryMarkHollowVeilCrossed()
        {
            if (state.hollowVeilCrossed)
                return true;

            if (!CanEnterHollow || state.hollowCompleted ||
                state.heartrootCarried || state.heartrootBurned ||
                state.campaignCompleted)
            {
                return false;
            }

            CampaignSaveData previousState = state.Clone();
            state.hollowVeilCrossed = true;
            if (!SaveNow())
            {
                state = previousState;
                return false;
            }

            CampaignEventUtility.Invoke(HollowVeilCrossed, this);
            CampaignEventUtility.Invoke(
                ProgressChanged,
                state.Snapshot,
                this);
            return true;
        }

        /// <summary>
        /// Saves one expected witch death. Wave indexes are zero based and
        /// must arrive in exact order. The matching already-saved death is an
        /// idempotent no-op used by scene-reload recovery; drift is rejected.
        /// </summary>
        public bool TryRecordHollowWitchDefeated(int expectedWaveIndex)
        {
            if (expectedWaveIndex < 0 || expectedWaveIndex >= 3 ||
                !state.hollowVeilCrossed || state.heartrootCarried ||
                state.heartrootBurned || state.campaignCompleted)
            {
                return false;
            }

            int expectedDefeatedBefore = expectedWaveIndex;
            int expectedDefeatedAfter = expectedWaveIndex + 1;
            if (state.defeatedWitchCount == expectedDefeatedAfter)
                return true;

            if (state.defeatedWitchCount != expectedDefeatedBefore)
                return false;

            CampaignSaveData previousState = state.Clone();
            state.defeatedWitchCount = expectedDefeatedAfter;
            bool exposedNow = expectedDefeatedAfter == 3;
            if (exposedNow)
                state.heartrootExposed = true;

            if (!SaveNow())
            {
                state = previousState;
                return false;
            }

            CampaignEventUtility.Invoke(
                HollowWitchDefeated,
                expectedDefeatedAfter,
                this);
            if (exposedNow)
                CampaignEventUtility.Invoke(HeartrootExposed, this);

            CampaignEventUtility.Invoke(
                ProgressChanged,
                state.Snapshot,
                this);
            return true;
        }

        /// <summary>
        /// Commits the campaign victory immediately after the third witch.
        /// The legacy carried/burned terminal flags are retained only so old
        /// saves and inventory normalization stay compatible; no recovery or
        /// burn gameplay event is emitted by this path.
        /// </summary>
        public bool TryCompleteCampaignFromFinalWitch()
        {
            if (state.campaignCompleted)
            {
                return state.hollowVeilCrossed &&
                       state.defeatedWitchCount == 3 &&
                       state.heartrootExposed;
            }

            if (!state.hollowVeilCrossed ||
                state.defeatedWitchCount != 3 ||
                !state.heartrootExposed)
            {
                return false;
            }

            CampaignSaveData previousState = state.Clone();
            state.hollowCompleted = true;
            state.heartrootCarried = true;
            state.heartrootBurned = true;
            state.campaignCompleted = true;
            if (!SaveNow())
            {
                state = previousState;
                return false;
            }

            // Do not publish ProgressChanged here. The Hollow mission root
            // remains visible behind the Win menu so the final witch's
            // Heartroot drop can be seen. Reload normalization still sees the
            // durable terminal facts.
            CampaignEventUtility.Invoke(CampaignCompleted, this);
            return true;
        }

        /// <summary>
        /// Atomically makes the exposed Heartroot current cargo and stores the
        /// exact seven-entry inventory snapshot in the same campaign save.
        /// Safety's paired save/checkpoint is coordinated by the owned finale
        /// bridge immediately after this durable source-of-truth commit.
        /// </summary>
        public bool TryRecoverExposedHeartroot(
            string[] itemIds,
            int[] quantities)
        {
            if (!state.hollowVeilCrossed ||
                state.defeatedWitchCount != 3 ||
                !state.heartrootExposed || state.heartrootCarried ||
                state.heartrootBurned || state.campaignCompleted ||
                !TryValidateHeartrootInventorySnapshot(
                    itemIds,
                    quantities,
                    1,
                    out _))
            {
                return false;
            }

            if (!TryNormalizeInventorySnapshot(
                    itemIds,
                    quantities,
                    out string[] normalizedIds,
                    out int[] normalizedQuantities))
            {
                return false;
            }

            CampaignSaveData previousState = state.Clone();
            state.heartrootCarried = true;
            state.hollowCompleted = true;
            state.carriedInventoryItemIds = normalizedIds;
            state.carriedInventoryQuantities = normalizedQuantities;
            if (!SaveNow())
            {
                state = previousState;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Atomically removes current Heartroot cargo and commits both the
        /// burn and campaign completion facts. Win presentation must occur
        /// only after this method and the paired Safety checkpoint succeed.
        /// </summary>
        public bool TryBurnExposedHeartroot(
            string[] itemIds,
            int[] quantities)
        {
            if (!state.hollowVeilCrossed ||
                state.defeatedWitchCount != 3 ||
                !state.heartrootExposed || !state.heartrootCarried ||
                state.heartrootBurned || state.campaignCompleted ||
                !TryValidateHeartrootInventorySnapshot(
                    itemIds,
                    quantities,
                    0,
                    out _))
            {
                return false;
            }

            if (!TryNormalizeInventorySnapshot(
                    itemIds,
                    quantities,
                    out string[] normalizedIds,
                    out int[] normalizedQuantities))
            {
                return false;
            }

            CampaignSaveData previousState = state.Clone();
            state.heartrootCarried = true;
            state.heartrootBurned = true;
            state.campaignCompleted = true;
            state.carriedInventoryItemIds = normalizedIds;
            state.carriedInventoryQuantities = normalizedQuantities;
            if (!SaveNow())
            {
                state = previousState;
                return false;
            }

            return true;
        }

        internal void PublishHeartrootRecoveryCommitted()
        {
            if (!state.hollowCompleted || !state.heartrootCarried ||
                state.heartrootBurned || state.campaignCompleted)
            {
                return;
            }

            CampaignEventUtility.Invoke(
                AreaCompleted,
                CampaignAreaId.BloodrootHollow,
                this);
            CampaignEventUtility.Invoke(HeartrootRecovered, this);
            CampaignEventUtility.Invoke(
                ProgressChanged,
                state.Snapshot,
                this);
        }

        internal void PublishHeartrootBurnCommitted()
        {
            if (!state.heartrootCarried || !state.heartrootBurned ||
                !state.campaignCompleted)
            {
                return;
            }

            CampaignEventUtility.Invoke(HeartrootBurned, this);
            CampaignEventUtility.Invoke(CampaignCompleted, this);
            CampaignEventUtility.Invoke(
                ProgressChanged,
                state.Snapshot,
                this);
        }

        public static bool TryValidateHeartrootInventorySnapshot(
            string[] itemIds,
            int[] quantities,
            int expectedHeartrootQuantity,
            out string error)
        {
            if (expectedHeartrootQuantity < 0 ||
                expectedHeartrootQuantity > 1 || itemIds == null ||
                quantities == null ||
                itemIds.Length !=
                CampaignHeartrootInventoryIds.RequiredCatalogCount ||
                itemIds.Length != quantities.Length)
            {
                error =
                    "Heartroot persistence requires the exact seven-entry campaign inventory catalog.";
                return false;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            int heartrootQuantity = -1;
            for (int index = 0; index < itemIds.Length; index++)
            {
                string id = itemIds[index]?.Trim() ?? string.Empty;
                if (id.Length == 0 || !seen.Add(id) ||
                    !CampaignHeartrootInventoryIds.IsRequiredStableId(id) ||
                    quantities[index] < 0)
                {
                    error =
                        "Heartroot inventory persistence found blank, duplicate, or negative catalog data.";
                    return false;
                }

                if (string.Equals(
                        id,
                        CampaignHeartrootInventoryIds.StableId,
                        StringComparison.Ordinal))
                {
                    heartrootQuantity = quantities[index];
                }
            }

            foreach (string requiredId in
                     CampaignHeartrootInventoryIds.RequiredStableIds)
            {
                if (!seen.Contains(requiredId))
                {
                    error =
                        $"Heartroot inventory persistence is missing required stable ID '{requiredId}'.";
                    return false;
                }
            }

            if (heartrootQuantity != expectedHeartrootQuantity)
            {
                error =
                    $"Stable Heartroot token quantity must be exactly {expectedHeartrootQuantity}.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool StartNewGame()
        {
            ClearStagedAreaCompletionInventory();
            CampaignSaveData previousState = state.Clone();
            bool previousWritePermission = allowSaveWrites;
            state = CampaignSaveData.CreateNewGame();
            allowSaveWrites = true;

            if (!SaveAllRecoveryCopies("start a new game"))
            {
                state = previousState;
                allowSaveWrites = previousWritePermission;
                return false;
            }

            GetComponent<CampaignInventoryCarryover>()?.ClearSnapshot();

            CampaignEventUtility.Invoke(NewGameStarted, this);
            CampaignEventUtility.Invoke(
                ProgressChanged,
                state.Snapshot,
                this);
            return true;
        }

        public bool TryGetInventoryCarryover(
            out string[] itemIds,
            out int[] quantities)
        {
            itemIds = (string[])(state.carriedInventoryItemIds?.Clone() ??
                                 Array.Empty<string>());
            quantities = (int[])(state.carriedInventoryQuantities?.Clone() ??
                                  Array.Empty<int>());
            return itemIds.Length > 0 && itemIds.Length == quantities.Length;
        }

        public bool UpdateInventoryCarryover(
            string[] itemIds,
            int[] quantities)
        {
            if (!TryNormalizeInventorySnapshot(
                    itemIds,
                    quantities,
                    out string[] normalizedIds,
                    out int[] normalizedQuantities))
            {
                return false;
            }

            CampaignSaveData previousState = state.Clone();
            state.carriedInventoryItemIds = normalizedIds;
            state.carriedInventoryQuantities = normalizedQuantities;
            if (SaveNow())
            {
                return true;
            }

            state = previousState;
            return false;
        }

        /// <summary>
        /// Proves a live inventory snapshot is already consistent with the
        /// normalized campaign facts without mutating or saving the campaign.
        /// This prevents travel/F5 from accepting a stale Heartroot token
        /// quantity that normalization would silently repair in only one half
        /// of the paired Safety checkpoint.
        /// </summary>
        internal bool TryValidateInventoryCarryoverSnapshot(
            string[] itemIds,
            int[] quantities,
            out string error)
        {
            error = string.Empty;
            if (!TryNormalizeInventorySnapshot(
                    itemIds,
                    quantities,
                    out string[] normalizedIds,
                    out int[] normalizedQuantities))
            {
                error = "Campaign inventory snapshot is malformed.";
                return false;
            }

            CampaignSaveData candidate = state.Clone();
            candidate.carriedInventoryItemIds = normalizedIds;
            candidate.carriedInventoryQuantities = normalizedQuantities;
            candidate.Normalize();

            if (!InventorySnapshotsMatch(
                    normalizedIds,
                    normalizedQuantities,
                    candidate.carriedInventoryItemIds,
                    candidate.carriedInventoryQuantities))
            {
                error =
                    "Live inventory does not match the normalized durable campaign facts.";
                return false;
            }

            return true;
        }

        private static bool InventorySnapshotsMatch(
            string[] leftIds,
            int[] leftQuantities,
            string[] rightIds,
            int[] rightQuantities)
        {
            if (leftIds == null || leftQuantities == null ||
                rightIds == null || rightQuantities == null ||
                leftIds.Length != leftQuantities.Length ||
                rightIds.Length != rightQuantities.Length ||
                leftIds.Length != rightIds.Length)
            {
                return false;
            }

            for (int index = 0; index < leftIds.Length; index++)
            {
                if (!string.Equals(
                        leftIds[index],
                        rightIds[index],
                        StringComparison.Ordinal) ||
                    leftQuantities[index] != rightQuantities[index])
                {
                    return false;
                }
            }

            return true;
        }

        public bool MarkPrologueCompleted()
        {
            const string prologueOfferingId =
                CampaignRootOfferingIds.PrologueCursedObject;
            if (state.prologueCompleted ||
                !state.IsRootOfferingCommitted(prologueOfferingId) ||
                state.IsFarmEmergenceCompleted(prologueOfferingId) ||
                !string.Equals(
                    state.activeFarmEmergenceOfferingId,
                    prologueOfferingId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            // The legacy completion entry point is retained for the existing
            // Farm director, but it can no longer bypass the offered-object
            // emergence. Its completion is committed in the same save as the
            // prologue and Black Pines unlock.
            return CompleteFarmEmergenceTransaction(
                prologueOfferingId,
                true);
        }

        /// <summary>
        /// Records the one-time safe-hub arrival sequence. The update is
        /// durable before listeners are notified, matching the rest of the
        /// campaign progression contract.
        /// </summary>
        public bool MarkHubIntroductionCompleted()
        {
            if (!state.prologueCompleted ||
                state.hubIntroductionCompleted)
            {
                return false;
            }

            CampaignSaveData previousState = state.Clone();
            state.hubIntroductionCompleted = true;

            if (!SaveNow())
            {
                state = previousState;
                return false;
            }

            CampaignEventUtility.Invoke(HubIntroductionCompleted, this);
            CampaignEventUtility.Invoke(
                ProgressChanged,
                state.Snapshot,
                this);
            return true;
        }

        public bool UnlockArea(CampaignAreaId area)
        {
            if (!Enum.IsDefined(typeof(CampaignAreaId), area) ||
                !CanUnlockArea(area) ||
                state.IsAreaUnlocked(area))
            {
                return false;
            }

            CampaignSaveData previousState = state.Clone();
            state.SetAreaUnlocked(area);

            if (!SaveNow())
            {
                state = previousState;
                return false;
            }

            CampaignEventUtility.Invoke(AreaUnlocked, area, this);
            CampaignEventUtility.Invoke(
                ProgressChanged,
                state.Snapshot,
                this);
            return true;
        }

        public bool MarkAreaCompleted(CampaignAreaId area)
        {
            if (!Enum.IsDefined(typeof(CampaignAreaId), area) ||
                !state.IsAreaUnlocked(area) ||
                state.IsAreaCompleted(area))
            {
                return false;
            }

            CampaignSaveData previousState = state.Clone();
            if (hasStagedAreaCompletionInventory)
            {
                state.carriedInventoryItemIds =
                    (string[])stagedAreaCompletionInventoryIds.Clone();
                state.carriedInventoryQuantities =
                    (int[])stagedAreaCompletionInventoryQuantities.Clone();
            }

            state.SetAreaCompleted(area);
            state.ApplyTowerCompletionCredits(area);
            CampaignAreaId? nextArea = GetNextArea(area);
            bool unlockedNextArea =
                nextArea.HasValue && state.SetAreaUnlocked(nextArea.Value);

            if (!SaveNow())
            {
                state = previousState;
                return false;
            }

            CampaignEventUtility.Invoke(AreaCompleted, area, this);

            if (unlockedNextArea)
            {
                CampaignEventUtility.Invoke(
                    AreaUnlocked,
                    nextArea.Value,
                    this);
            }

            CampaignEventUtility.Invoke(
                ProgressChanged,
                state.Snapshot,
                this);
            return true;
        }

        /// <summary>
        /// Durably activates the final progression cylinder. The Nell stone
        /// facts are hidden compatibility state for Safety's established
        /// thorn-veil contract; no retired offering or emergence events are
        /// emitted by this simplified campaign action.
        /// </summary>
        public bool TryActivateHollowTower()
        {
            bool alreadyActivated =
                state.IsNameStoneOffered(CampaignNameStoneIds.Nell) &&
                state.IsFarmEmergenceCompleted(CampaignNameStoneIds.Nell);
            if (alreadyActivated)
                return true;

            if (!state.harrowCompleted || !state.hollowUnlocked ||
                state.hollowCompleted || state.heartrootCarried ||
                state.heartrootBurned || state.campaignCompleted ||
                !state.IsNameStoneOffered(CampaignNameStoneIds.Esther) ||
                !state.IsNameStoneOffered(CampaignNameStoneIds.Ruth) ||
                !state.IsNameStoneOffered(CampaignNameStoneIds.Naomi))
            {
                return false;
            }

            CampaignSaveData previousState = state.Clone();
            if (!state.ApplyHollowTowerCredit())
                return false;

            if (!SaveNow())
            {
                state = previousState;
                return false;
            }

            CampaignEventUtility.Invoke(HollowTowerActivated, this);
            CampaignEventUtility.Invoke(
                ProgressChanged,
                state.Snapshot,
                this);
            return true;
        }

        public bool IsAreaUnlocked(CampaignAreaId area)
        {
            return Enum.IsDefined(typeof(CampaignAreaId), area) &&
                   state.IsAreaUnlocked(area);
        }

        public bool IsAreaCompleted(CampaignAreaId area)
        {
            return Enum.IsDefined(typeof(CampaignAreaId), area) &&
                   state.IsAreaCompleted(area);
        }

        public bool IsEvidenceCollected(string evidenceId)
        {
            return CampaignEvidenceIds.IsCanonical(evidenceId) &&
                   state.IsEvidenceCollected(evidenceId.Trim());
        }

        public bool IsNameStoneExtracted(string nameStoneId)
        {
            return CampaignNameStoneIds.IsCanonical(nameStoneId) &&
                   state.IsNameStoneExtracted(nameStoneId.Trim());
        }

        public bool IsNameStoneOffered(string nameStoneId)
        {
            return CampaignNameStoneIds.IsCanonical(nameStoneId) &&
                   state.IsNameStoneOffered(nameStoneId.Trim());
        }

        /// <summary>
        /// Atomically records one canonical clue and, for the four authored
        /// stone-bearing clues, its matching extracted Name Stone.
        /// </summary>
        public bool TryRecordEvidence(
            string evidenceId,
            CampaignAreaId area,
            string optionalNameStoneId = null)
        {
            string normalizedEvidenceId = evidenceId?.Trim() ?? string.Empty;
            string normalizedStoneId =
                optionalNameStoneId?.Trim() ?? string.Empty;
            if (!Enum.IsDefined(typeof(CampaignAreaId), area) ||
                !CampaignEvidenceIds.TryGetArea(
                    normalizedEvidenceId,
                    out CampaignAreaId evidenceArea) ||
                evidenceArea != area ||
                !state.IsAreaUnlocked(area))
            {
                return false;
            }

            string requiredStoneId =
                CampaignEvidenceIds.RequiredNameStoneId(
                    normalizedEvidenceId);
            if (!string.Equals(
                    normalizedStoneId,
                    requiredStoneId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            bool evidenceChanged =
                !state.IsEvidenceCollected(normalizedEvidenceId);
            bool stoneChanged = normalizedStoneId.Length > 0 &&
                                !state.IsNameStoneExtracted(
                                    normalizedStoneId);
            if (!evidenceChanged && !stoneChanged)
            {
                return false;
            }

            CampaignSaveData previousState = state.Clone();
            if (evidenceChanged)
            {
                state.TryAddEvidence(normalizedEvidenceId);
            }

            if (stoneChanged)
            {
                state.TryAddExtractedNameStone(normalizedStoneId);
            }

            if (!SaveNow())
            {
                state = previousState;
                return false;
            }

            if (evidenceChanged)
            {
                CampaignEventUtility.Invoke(
                    EvidenceCollected,
                    normalizedEvidenceId,
                    this);
            }

            if (stoneChanged)
            {
                CampaignEventUtility.Invoke(
                    NameStoneExtracted,
                    normalizedStoneId,
                    this);
            }

            CampaignEventUtility.Invoke(
                ProgressChanged,
                state.Snapshot,
                this);
            return true;
        }

        public bool IsRootOfferingCommitted(string offeringId)
        {
            return CampaignRootOfferingIds.IsCanonical(offeringId) &&
                   state.IsRootOfferingCommitted(offeringId.Trim());
        }

        public bool IsFarmEmergenceCompleted(string offeringId)
        {
            return CampaignRootOfferingIds.IsCanonical(offeringId) &&
                   state.IsFarmEmergenceCompleted(offeringId.Trim());
        }

        public bool TryRevealPrologueCursedObject()
        {
            if (state.prologueCursedObjectRevealed ||
                state.prologueCompleted)
            {
                return false;
            }

            CampaignSaveData previousState = state.Clone();
            state.prologueCursedObjectRevealed = true;
            if (!SaveNow())
            {
                state = previousState;
                return false;
            }

            CampaignEventUtility.Invoke(
                PrologueCursedObjectRevealCommitted,
                this);
            CampaignEventUtility.Invoke(
                ProgressChanged,
                state.Snapshot,
                this);
            return true;
        }

        public bool TryBeginRootOffering(string offeringId)
        {
            string id = offeringId?.Trim() ?? string.Empty;
            if (!CampaignRootOfferingIds.IsCanonical(id) ||
                CampaignNameStoneIds.IsCanonical(id) ||
                !string.IsNullOrEmpty(state.pendingRootOfferingId) ||
                state.HasUnresolvedFarmEmergence() ||
                state.IsRootOfferingCommitted(id))
            {
                return false;
            }

            bool isPrologueOffering = string.Equals(
                id,
                CampaignRootOfferingIds.PrologueCursedObject,
                StringComparison.Ordinal);
            if (isPrologueOffering)
            {
                if (!state.prologueCursedObjectRevealed ||
                    state.prologueCompleted)
                {
                    return false;
                }
            }
            else if (!state.IsNameStoneExtracted(id))
            {
                return false;
            }

            CampaignSaveData previousState = state.Clone();
            state.pendingRootOfferingId = id;
            if (!SaveNow())
            {
                state = previousState;
                return false;
            }

            CampaignEventUtility.Invoke(RootOfferingStarted, id, this);
            if (CampaignNameStoneIds.IsCanonical(id))
            {
                CampaignEventUtility.Invoke(
                    NameStoneOfferStarted,
                    id,
                    this);
            }

            CampaignEventUtility.Invoke(
                ProgressChanged,
                state.Snapshot,
                this);
            return true;
        }

        public bool TryCommitPendingRootOffering()
        {
            string id = state.pendingRootOfferingId?.Trim() ?? string.Empty;
            if (!CampaignRootOfferingIds.IsCanonical(id) ||
                state.HasUnresolvedFarmEmergence() ||
                state.IsRootOfferingCommitted(id))
            {
                return false;
            }

            bool couldEnterBefore = CanEnterHollow;
            CampaignSaveData previousState = state.Clone();
            if (!state.TryCommitRootOffering(id))
            {
                return false;
            }

            state.pendingRootOfferingId = string.Empty;
            if (!SaveNow())
            {
                state = previousState;
                return false;
            }

            CampaignEventUtility.Invoke(RootOfferingCommitted, id, this);
            if (string.Equals(
                    id,
                    CampaignRootOfferingIds.PrologueCursedObject,
                    StringComparison.Ordinal))
            {
                CampaignEventUtility.Invoke(
                    PrologueCursedObjectOfferCommitted,
                    this);
            }
            else
            {
                CampaignEventUtility.Invoke(NameStoneOffered, id, this);
                if (!couldEnterBefore && CanEnterHollow)
                {
                    CampaignEventUtility.Invoke(HollowEntryAvailable, this);
                }
            }

            CampaignEventUtility.Invoke(
                ProgressChanged,
                state.Snapshot,
                this);
            return true;
        }

        public bool TryCommitPendingRootOffering(string expectedOfferingId)
        {
            string expected = expectedOfferingId?.Trim() ?? string.Empty;
            return CampaignRootOfferingIds.IsCanonical(expected) &&
                   string.Equals(
                       PendingRootOfferingId,
                       expected,
                       StringComparison.Ordinal) &&
                   TryCommitPendingRootOffering();
        }

        public bool TryCancelPendingRootOffering()
        {
            string id = state.pendingRootOfferingId?.Trim() ?? string.Empty;
            if (!CampaignRootOfferingIds.IsCanonical(id))
            {
                return false;
            }

            CampaignSaveData previousState = state.Clone();
            state.pendingRootOfferingId = string.Empty;
            if (!SaveNow())
            {
                state = previousState;
                return false;
            }

            CampaignEventUtility.Invoke(RootOfferingCanceled, id, this);
            if (CampaignNameStoneIds.IsCanonical(id))
            {
                CampaignEventUtility.Invoke(
                    NameStoneOfferCanceled,
                    id,
                    this);
            }

            CampaignEventUtility.Invoke(
                ProgressChanged,
                state.Snapshot,
                this);
            return true;
        }

        public bool TryCancelPendingRootOffering(string expectedOfferingId)
        {
            string expected = expectedOfferingId?.Trim() ?? string.Empty;
            return CampaignRootOfferingIds.IsCanonical(expected) &&
                   string.Equals(
                       PendingRootOfferingId,
                       expected,
                       StringComparison.Ordinal) &&
                   TryCancelPendingRootOffering();
        }

        public bool TryBeginNextFarmEmergence(out string offeringId)
        {
            offeringId = state.activeFarmEmergenceOfferingId?.Trim() ??
                         string.Empty;
            if (CampaignRootOfferingIds.IsCanonical(offeringId) &&
                state.IsRootOfferingCommitted(offeringId) &&
                !state.IsFarmEmergenceCompleted(offeringId))
            {
                // Reload/resume is intentionally idempotent: the existing
                // active emergence is returned without another write/event.
                return true;
            }

            offeringId = state.FindNextPendingFarmEmergenceOfferingId();
            if (offeringId.Length == 0)
            {
                return false;
            }

            CampaignSaveData previousState = state.Clone();
            state.activeFarmEmergenceOfferingId = offeringId;
            if (!SaveNow())
            {
                state = previousState;
                offeringId = string.Empty;
                return false;
            }

            CampaignEventUtility.Invoke(
                FarmEmergenceStarted,
                offeringId,
                this);
            CampaignEventUtility.Invoke(
                ProgressChanged,
                state.Snapshot,
                this);
            return true;
        }

        public bool TryCompleteFarmEmergence(string offeringId)
        {
            string id = offeringId?.Trim() ?? string.Empty;
            if (!CampaignRootOfferingIds.IsCanonical(id) ||
                !string.Equals(
                    state.activeFarmEmergenceOfferingId,
                    id,
                    StringComparison.Ordinal))
            {
                return false;
            }

            return CompleteFarmEmergenceTransaction(id, true);
        }

        private bool CompleteFarmEmergenceTransaction(
            string offeringId,
            bool requireActive)
        {
            string id = offeringId?.Trim() ?? string.Empty;
            if (!CampaignRootOfferingIds.IsCanonical(id) ||
                !state.IsRootOfferingCommitted(id) ||
                state.IsFarmEmergenceCompleted(id) ||
                (requireActive &&
                 !string.Equals(
                     state.activeFarmEmergenceOfferingId,
                     id,
                     StringComparison.Ordinal)))
            {
                return false;
            }

            CampaignSaveData previousState = state.Clone();
            if (!state.TryAddCompletedFarmEmergence(id))
            {
                return false;
            }

            if (string.Equals(
                    state.activeFarmEmergenceOfferingId,
                    id,
                    StringComparison.Ordinal))
            {
                state.activeFarmEmergenceOfferingId = string.Empty;
            }

            bool completedPrologue = string.Equals(
                id,
                CampaignRootOfferingIds.PrologueCursedObject,
                StringComparison.Ordinal);
            bool blackPinesWasUnlocked = false;
            if (completedPrologue)
            {
                state.prologueCompleted = true;
                blackPinesWasUnlocked =
                    state.SetAreaUnlocked(CampaignAreaId.BlackPines);
            }

            if (!SaveNow())
            {
                state = previousState;
                return false;
            }

            CampaignEventUtility.Invoke(FarmEmergenceCompleted, id, this);
            if (completedPrologue)
            {
                CampaignEventUtility.Invoke(PrologueCompleted, this);
                if (blackPinesWasUnlocked)
                {
                    CampaignEventUtility.Invoke(
                        AreaUnlocked,
                        CampaignAreaId.BlackPines,
                        this);
                }
            }

            CampaignEventUtility.Invoke(
                ProgressChanged,
                state.Snapshot,
                this);
            return true;
        }

        // Compatibility wrappers for the pre-v5 Name Stone surface.
        public bool TryBeginNameStoneOffer(string nameStoneId)
        {
            string id = nameStoneId?.Trim() ?? string.Empty;
            return CampaignNameStoneIds.IsCanonical(id) &&
                   TryBeginRootOffering(id);
        }

        public bool TryCommitPendingNameStoneOffer()
        {
            return CampaignNameStoneIds.IsCanonical(
                       PendingRootOfferingId) &&
                   TryCommitPendingRootOffering();
        }

        public bool TryCommitPendingNameStoneOffer(string expectedNameStoneId)
        {
            string expected = expectedNameStoneId?.Trim() ?? string.Empty;
            return CampaignNameStoneIds.IsCanonical(expected) &&
                   string.Equals(
                       PendingNameStoneOfferId,
                       expected,
                       StringComparison.Ordinal) &&
                   TryCommitPendingNameStoneOffer();
        }

        public bool TryCancelPendingNameStoneOffer()
        {
            return CampaignNameStoneIds.IsCanonical(
                       PendingRootOfferingId) &&
                   TryCancelPendingRootOffering();
        }

        public bool TryCancelPendingNameStoneOffer(string expectedNameStoneId)
        {
            string expected = expectedNameStoneId?.Trim() ?? string.Empty;
            return CampaignNameStoneIds.IsCanonical(expected) &&
                   string.Equals(
                       PendingNameStoneOfferId,
                       expected,
                       StringComparison.Ordinal) &&
                   TryCancelPendingNameStoneOffer();
        }

        internal bool TryStageAreaCompletionInventory(
            string[] itemIds,
            int[] quantities)
        {
            if (!TryNormalizeInventorySnapshot(
                    itemIds,
                    quantities,
                    out stagedAreaCompletionInventoryIds,
                    out stagedAreaCompletionInventoryQuantities))
            {
                ClearStagedAreaCompletionInventory();
                return false;
            }

            hasStagedAreaCompletionInventory = true;
            return true;
        }

        internal void ClearStagedAreaCompletionInventory()
        {
            stagedAreaCompletionInventoryIds = Array.Empty<string>();
            stagedAreaCompletionInventoryQuantities = Array.Empty<int>();
            hasStagedAreaCompletionInventory = false;
        }

        public bool PrepareSceneTravel(
            string sceneName,
            string spawnDestinationId)
        {
            if (string.IsNullOrWhiteSpace(sceneName) ||
                string.IsNullOrWhiteSpace(spawnDestinationId))
            {
                return false;
            }

            CampaignSaveData previousState = state.Clone();
            state.pendingSceneName = sceneName.Trim();
            state.pendingSpawnDestinationId = spawnDestinationId.Trim();
            state.Normalize();

            if (!string.Equals(
                    state.pendingSceneName,
                    sceneName.Trim(),
                    StringComparison.Ordinal) ||
                !string.Equals(
                    state.pendingSpawnDestinationId,
                    spawnDestinationId.Trim(),
                    StringComparison.Ordinal))
            {
                state = previousState;
                return false;
            }

            if (SaveNow())
            {
                return true;
            }

            state = previousState;
            return false;
        }

        public bool HasPendingSpawn(
            string sceneName,
            string spawnDestinationId)
        {
            return !string.IsNullOrWhiteSpace(sceneName) &&
                   !string.IsNullOrWhiteSpace(spawnDestinationId) &&
                   string.Equals(
                       state.pendingSceneName,
                       sceneName.Trim(),
                       StringComparison.Ordinal) &&
                   string.Equals(
                       state.pendingSpawnDestinationId,
                       spawnDestinationId.Trim(),
                       StringComparison.Ordinal);
        }

        public bool CompletePendingSpawn(
            string sceneName,
            string spawnDestinationId)
        {
            if (!HasPendingSpawn(sceneName, spawnDestinationId))
            {
                return false;
            }

            CampaignSaveData previousState = state.Clone();
            state.pendingSceneName = string.Empty;
            state.pendingSpawnDestinationId = string.Empty;

            if (!SaveNow())
            {
                state = previousState;
                return false;
            }

            return true;
        }

        public bool CancelPendingTravel(
            string sceneName,
            string spawnDestinationId)
        {
            return CompletePendingSpawn(sceneName, spawnDestinationId);
        }

        private static CampaignAreaId? GetNextArea(CampaignAreaId area)
        {
            return area switch
            {
                CampaignAreaId.BlackPines =>
                    CampaignAreaId.StillwaterFeedMill,
                CampaignAreaId.StillwaterFeedMill =>
                    CampaignAreaId.HarrowEstate,
                CampaignAreaId.HarrowEstate =>
                    CampaignAreaId.BloodrootHollow,
                _ => null
            };
        }

        private bool CanUnlockArea(CampaignAreaId area)
        {
            return area switch
            {
                CampaignAreaId.BlackPines => state.prologueCompleted,
                CampaignAreaId.StillwaterFeedMill =>
                    state.blackPinesCompleted,
                CampaignAreaId.HarrowEstate =>
                    state.stillwaterCompleted,
                CampaignAreaId.BloodrootHollow =>
                    state.harrowCompleted,
                _ => false
            };
        }

        private static bool TryNormalizeInventorySnapshot(
            string[] itemIds,
            int[] quantities,
            out string[] normalizedIds,
            out int[] normalizedQuantities)
        {
            normalizedIds = Array.Empty<string>();
            normalizedQuantities = Array.Empty<int>();
            if (itemIds == null || quantities == null ||
                itemIds.Length == 0 || itemIds.Length != quantities.Length)
            {
                return false;
            }

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            normalizedIds = new string[itemIds.Length];
            normalizedQuantities = new int[quantities.Length];
            for (int index = 0; index < itemIds.Length; index++)
            {
                string id = itemIds[index]?.Trim() ?? string.Empty;
                if (id.Length == 0 || !seenIds.Add(id))
                {
                    normalizedIds = Array.Empty<string>();
                    normalizedQuantities = Array.Empty<int>();
                    return false;
                }

                normalizedIds[index] = id;
                normalizedQuantities[index] = Mathf.Max(0, quantities[index]);
            }

            return true;
        }

        private bool SaveAllRecoveryCopies(string operation)
        {
            state.Normalize();

            if (CampaignSaveStore.TryWriteAllCopies(
                    SavePath,
                    state,
                    out string error))
            {
                return true;
            }

            string messageWithPath =
                $"Could not {operation} at '{SavePath}': {error}";
            Debug.LogError(messageWithPath, this);
            CampaignEventUtility.Invoke(SaveFailed, messageWithPath, this);
            return false;
        }
    }

    internal sealed class CampaignSaveLoadResult
    {
        public CampaignSaveData Data;
        public bool FoundSave;
        public bool LoadedSuccessfully;
        public bool NeedsPrimaryRepair;
        public bool HasNewerUnsupportedVersion;
        public string Warning = string.Empty;
    }

    internal static class CampaignSaveStore
    {
        private const string TemporarySuffix = ".tmp";
        private const string BackupSuffix = ".bak";

        public static CampaignSaveLoadResult Load(string primaryPath)
        {
            var result = new CampaignSaveLoadResult();
            string temporaryPath = primaryPath + TemporarySuffix;
            string backupPath = primaryPath + BackupSuffix;
            string[] candidates = { primaryPath, temporaryPath, backupPath };
            var failures = new List<string>();

            foreach (string candidate in candidates)
            {
                if (!File.Exists(candidate))
                {
                    continue;
                }

                result.FoundSave = true;

                if (TryRead(candidate, out CampaignSaveData data,
                        out bool newerVersion, out string error))
                {
                    result.Data = data;
                    result.LoadedSuccessfully = true;
                    result.NeedsPrimaryRepair =
                        !string.Equals(candidate, primaryPath,
                            StringComparison.Ordinal);

                    if (result.NeedsPrimaryRepair)
                    {
                        result.Warning =
                            $"Recovered campaign progress from '{candidate}'.";
                    }

                    return result;
                }

                if (newerVersion)
                {
                    result.Data = CampaignSaveData.CreateNewGame();
                    result.HasNewerUnsupportedVersion = true;
                    result.Warning = error;
                    return result;
                }

                failures.Add($"{candidate}: {error}");
            }

            result.Data = CampaignSaveData.CreateNewGame();

            if (result.FoundSave)
            {
                result.Warning =
                    "Campaign save data was unreadable. Starting from safe " +
                    "new-game defaults. " + string.Join(" | ", failures);
                result.NeedsPrimaryRepair = true;
            }

            return result;
        }

        public static bool TrySave(
            string primaryPath,
            CampaignSaveData data,
            out string error)
        {
            string temporaryPath = primaryPath + TemporarySuffix;
            string backupPath = primaryPath + BackupSuffix;

            try
            {
                string directory = Path.GetDirectoryName(primaryPath);

                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonUtility.ToJson(data, true);

                using (var stream = new FileStream(
                           temporaryPath,
                           FileMode.Create,
                           FileAccess.Write,
                           FileShare.None,
                           4096,
                           FileOptions.WriteThrough))
                using (var writer = new StreamWriter(
                           stream,
                           new UTF8Encoding(false),
                           4096,
                           true))
                {
                    writer.Write(json);
                    writer.Flush();
                    stream.Flush(true);
                }

                if (File.Exists(primaryPath))
                {
                    ReplaceFile(temporaryPath, primaryPath, backupPath);
                }
                else
                {
                    File.Move(temporaryPath, primaryPath);
                }

                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                TryDelete(temporaryPath);
                error = $"{exception.GetType().Name}: {exception.Message}";
                return false;
            }
        }

        public static bool TryWriteAllCopies(
            string primaryPath,
            CampaignSaveData data,
            out string error)
        {
            string primaryTemporaryPath = primaryPath + TemporarySuffix;
            string backupPath = primaryPath + BackupSuffix;
            string backupTemporaryPath = backupPath + TemporarySuffix;
            bool primaryExisted = File.Exists(primaryPath);

            try
            {
                string directory = Path.GetDirectoryName(primaryPath);

                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonUtility.ToJson(data, true);

                WriteTemporaryFile(backupTemporaryPath, json);
                ReplaceWithoutBackup(backupTemporaryPath, backupPath);

                try
                {
                    WriteTemporaryFile(primaryTemporaryPath, json);
                    ReplaceWithoutBackup(primaryTemporaryPath, primaryPath);
                }
                catch
                {
                    // The primary still represents the previous campaign if
                    // its replacement failed. Restore that same state to the
                    // recovery copy before reporting reset failure.
                    if (primaryExisted && File.Exists(primaryPath))
                    {
                        File.Copy(primaryPath, backupPath, true);
                    }
                    else
                    {
                        TryDelete(backupPath);
                    }

                    throw;
                }

                TryDelete(primaryTemporaryPath);
                TryDelete(backupTemporaryPath);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                TryDelete(primaryTemporaryPath);
                TryDelete(backupTemporaryPath);
                error = $"{exception.GetType().Name}: {exception.Message}";
                return false;
            }
        }

        private static bool TryRead(
            string path,
            out CampaignSaveData data,
            out bool newerVersion,
            out string error)
        {
            data = null;
            newerVersion = false;

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);

                if (string.IsNullOrWhiteSpace(json))
                {
                    error = "The file is empty.";
                    return false;
                }

                data = JsonUtility.FromJson<CampaignSaveData>(json);

                if (data == null || data.saveVersion < 1)
                {
                    data = null;
                    error = "The file has no valid save version.";
                    return false;
                }

                if (data.saveVersion > CampaignSaveData.CurrentVersion)
                {
                    int fileVersion = data.saveVersion;
                    data = null;
                    newerVersion = true;
                    error =
                        $"Campaign save version {fileVersion} is newer than " +
                        $"supported version {CampaignSaveData.CurrentVersion}.";
                    return false;
                }

                data.Normalize();
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                data = null;
                error = $"{exception.GetType().Name}: {exception.Message}";
                return false;
            }
        }

        private static void ReplaceFile(
            string temporaryPath,
            string primaryPath,
            string backupPath)
        {
            try
            {
                File.Replace(
                    temporaryPath,
                    primaryPath,
                    backupPath,
                    true);
            }
            catch (PlatformNotSupportedException)
            {
                // File.Replace is atomic on supported desktop filesystems.
                // This fallback keeps the same-directory temp strategy for
                // platforms whose managed file API does not expose replace.
                File.Copy(primaryPath, backupPath, true);
                File.Copy(temporaryPath, primaryPath, true);
                File.Delete(temporaryPath);
            }
        }

        private static void ReplaceWithoutBackup(
            string temporaryPath,
            string destinationPath)
        {
            if (!File.Exists(destinationPath))
            {
                File.Move(temporaryPath, destinationPath);
                return;
            }

            try
            {
                File.Replace(temporaryPath, destinationPath, null, true);
            }
            catch (PlatformNotSupportedException)
            {
                File.Copy(temporaryPath, destinationPath, true);
                File.Delete(temporaryPath);
            }
        }

        private static void WriteTemporaryFile(string path, string json)
        {
            using var stream = new FileStream(
                path,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.WriteThrough);
            using var writer = new StreamWriter(
                stream,
                new UTF8Encoding(false),
                4096,
                true);
            writer.Write(json);
            writer.Flush();
            stream.Flush(true);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception)
            {
                // The original save exception is more useful to the caller.
            }
        }
    }
}
