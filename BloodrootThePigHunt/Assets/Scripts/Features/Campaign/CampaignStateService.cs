using System;
using System.Collections.Generic;
using System.IO;
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

        private static CampaignStateService instance;

        private CampaignSaveData state = CampaignSaveData.CreateNewGame();
        private bool allowSaveWrites = true;

        public static CampaignStateService Instance => instance;

        public CampaignProgressSnapshot Current => state.Snapshot;

        public bool HasCompletedPrologue => state.prologueCompleted;

        public string SavePath =>
            Path.Combine(Application.persistentDataPath, SaveFileName);

        public event Action<CampaignProgressSnapshot> ProgressLoaded;
        public event Action<CampaignProgressSnapshot> ProgressChanged;
        public event Action PrologueCompleted;
        public event Action<CampaignAreaId> AreaUnlocked;
        public event Action<CampaignAreaId> AreaCompleted;
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
            LoadFromDisk();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
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

        public bool StartNewGame()
        {
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

            CampaignEventUtility.Invoke(NewGameStarted, this);
            CampaignEventUtility.Invoke(
                ProgressChanged,
                state.Snapshot,
                this);
            return true;
        }

        public bool MarkPrologueCompleted()
        {
            if (state.prologueCompleted)
            {
                return false;
            }

            CampaignSaveData previousState = state.Clone();
            state.prologueCompleted = true;
            bool blackPinesWasUnlocked =
                state.SetAreaUnlocked(CampaignAreaId.BlackPines);

            if (!SaveNow())
            {
                state = previousState;
                return false;
            }

            CampaignEventUtility.Invoke(PrologueCompleted, this);

            if (blackPinesWasUnlocked)
            {
                CampaignEventUtility.Invoke(
                    AreaUnlocked,
                    CampaignAreaId.BlackPines,
                    this);
            }

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
            state.SetAreaCompleted(area);
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
