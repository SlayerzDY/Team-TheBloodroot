using System;

namespace Bloodroot.Campaign
{
    public enum CampaignAreaId
    {
        BlackPines = 0,
        StillwaterFeedMill = 1,
        HarrowEstate = 2,
        BloodrootHollow = 3
    }

    public static class CampaignSceneNames
    {
        public const string FarmPrologueHub = "Farm_PrologueHub";
        public const string OpenWorld = "Bloodroot_OpenWorld";
    }

    /// <summary>
    /// Read-only view of the current campaign state. Callers receive a value
    /// copy so gameplay systems cannot bypass the service's monotonic updates.
    /// </summary>
    public readonly struct CampaignProgressSnapshot
    {
        internal CampaignProgressSnapshot(CampaignSaveData data)
        {
            SaveVersion = data.saveVersion;
            PrologueCompleted = data.prologueCompleted;
            BlackPinesUnlocked = data.blackPinesUnlocked;
            BlackPinesCompleted = data.blackPinesCompleted;
            StillwaterUnlocked = data.stillwaterUnlocked;
            StillwaterCompleted = data.stillwaterCompleted;
            HarrowUnlocked = data.harrowUnlocked;
            HarrowCompleted = data.harrowCompleted;
            HollowUnlocked = data.hollowUnlocked;
            HollowCompleted = data.hollowCompleted;
            PendingSceneName = data.pendingSceneName ?? string.Empty;
            PendingSpawnDestinationId =
                data.pendingSpawnDestinationId ?? string.Empty;
        }

        public int SaveVersion { get; }

        public bool PrologueCompleted { get; }

        public bool BlackPinesUnlocked { get; }

        public bool BlackPinesCompleted { get; }

        public bool StillwaterUnlocked { get; }

        public bool StillwaterCompleted { get; }

        public bool HarrowUnlocked { get; }

        public bool HarrowCompleted { get; }

        public bool HollowUnlocked { get; }

        public bool HollowCompleted { get; }

        public string PendingSceneName { get; }

        public string PendingSpawnDestinationId { get; }

        public bool IsAreaUnlocked(CampaignAreaId area)
        {
            return area switch
            {
                CampaignAreaId.BlackPines => BlackPinesUnlocked,
                CampaignAreaId.StillwaterFeedMill => StillwaterUnlocked,
                CampaignAreaId.HarrowEstate => HarrowUnlocked,
                CampaignAreaId.BloodrootHollow => HollowUnlocked,
                _ => false
            };
        }

        public bool IsAreaCompleted(CampaignAreaId area)
        {
            return area switch
            {
                CampaignAreaId.BlackPines => BlackPinesCompleted,
                CampaignAreaId.StillwaterFeedMill => StillwaterCompleted,
                CampaignAreaId.HarrowEstate => HarrowCompleted,
                CampaignAreaId.BloodrootHollow => HollowCompleted,
                _ => false
            };
        }
    }

    [Serializable]
    internal sealed class CampaignSaveData
    {
        public const int CurrentVersion = 1;

        public int saveVersion = CurrentVersion;
        public bool prologueCompleted;
        public bool blackPinesUnlocked;
        public bool blackPinesCompleted;
        public bool stillwaterUnlocked;
        public bool stillwaterCompleted;
        public bool harrowUnlocked;
        public bool harrowCompleted;
        public bool hollowUnlocked;
        public bool hollowCompleted;
        public string pendingSceneName = string.Empty;
        public string pendingSpawnDestinationId = string.Empty;

        public CampaignProgressSnapshot Snapshot =>
            new CampaignProgressSnapshot(this);

        public static CampaignSaveData CreateNewGame()
        {
            return new CampaignSaveData();
        }

        public CampaignSaveData Clone()
        {
            return new CampaignSaveData
            {
                saveVersion = saveVersion,
                prologueCompleted = prologueCompleted,
                blackPinesUnlocked = blackPinesUnlocked,
                blackPinesCompleted = blackPinesCompleted,
                stillwaterUnlocked = stillwaterUnlocked,
                stillwaterCompleted = stillwaterCompleted,
                harrowUnlocked = harrowUnlocked,
                harrowCompleted = harrowCompleted,
                hollowUnlocked = hollowUnlocked,
                hollowCompleted = hollowCompleted,
                pendingSceneName = pendingSceneName,
                pendingSpawnDestinationId = pendingSpawnDestinationId
            };
        }

        public void Normalize()
        {
            saveVersion = CurrentVersion;
            pendingSceneName ??= string.Empty;
            pendingSpawnDestinationId ??= string.Empty;

            pendingSceneName = pendingSceneName.Trim();
            pendingSpawnDestinationId = pendingSpawnDestinationId.Trim();

            if (pendingSceneName.Length == 0 ||
                pendingSpawnDestinationId.Length == 0)
            {
                pendingSceneName = string.Empty;
                pendingSpawnDestinationId = string.Empty;
            }

            // Repair saves fail-closed at the first incomplete prerequisite.
            // A stray later flag must never skip an earlier campaign step.
            if (!prologueCompleted)
            {
                blackPinesUnlocked = false;
                blackPinesCompleted = false;
                stillwaterUnlocked = false;
                stillwaterCompleted = false;
                harrowUnlocked = false;
                harrowCompleted = false;
                hollowUnlocked = false;
                hollowCompleted = false;
                return;
            }

            blackPinesUnlocked = true;

            if (!blackPinesCompleted)
            {
                stillwaterUnlocked = false;
                stillwaterCompleted = false;
                harrowUnlocked = false;
                harrowCompleted = false;
                hollowUnlocked = false;
                hollowCompleted = false;
                return;
            }

            stillwaterUnlocked = true;

            if (!stillwaterCompleted)
            {
                harrowUnlocked = false;
                harrowCompleted = false;
                hollowUnlocked = false;
                hollowCompleted = false;
                return;
            }

            harrowUnlocked = true;

            if (!harrowCompleted)
            {
                hollowUnlocked = false;
                hollowCompleted = false;
                return;
            }

            hollowUnlocked = true;
        }

        public bool IsAreaUnlocked(CampaignAreaId area)
        {
            return area switch
            {
                CampaignAreaId.BlackPines => blackPinesUnlocked,
                CampaignAreaId.StillwaterFeedMill => stillwaterUnlocked,
                CampaignAreaId.HarrowEstate => harrowUnlocked,
                CampaignAreaId.BloodrootHollow => hollowUnlocked,
                _ => false
            };
        }

        public bool IsAreaCompleted(CampaignAreaId area)
        {
            return area switch
            {
                CampaignAreaId.BlackPines => blackPinesCompleted,
                CampaignAreaId.StillwaterFeedMill => stillwaterCompleted,
                CampaignAreaId.HarrowEstate => harrowCompleted,
                CampaignAreaId.BloodrootHollow => hollowCompleted,
                _ => false
            };
        }

        public bool SetAreaUnlocked(CampaignAreaId area)
        {
            if (IsAreaUnlocked(area))
            {
                return false;
            }

            switch (area)
            {
                case CampaignAreaId.BlackPines:
                    blackPinesUnlocked = true;
                    return true;
                case CampaignAreaId.StillwaterFeedMill:
                    stillwaterUnlocked = true;
                    return true;
                case CampaignAreaId.HarrowEstate:
                    harrowUnlocked = true;
                    return true;
                case CampaignAreaId.BloodrootHollow:
                    hollowUnlocked = true;
                    return true;
                default:
                    return false;
            }
        }

        public bool SetAreaCompleted(CampaignAreaId area)
        {
            if (IsAreaCompleted(area))
            {
                return false;
            }

            switch (area)
            {
                case CampaignAreaId.BlackPines:
                    blackPinesCompleted = true;
                    return true;
                case CampaignAreaId.StillwaterFeedMill:
                    stillwaterCompleted = true;
                    return true;
                case CampaignAreaId.HarrowEstate:
                    harrowCompleted = true;
                    return true;
                case CampaignAreaId.BloodrootHollow:
                    hollowCompleted = true;
                    return true;
                default:
                    return false;
            }
        }
    }
}
