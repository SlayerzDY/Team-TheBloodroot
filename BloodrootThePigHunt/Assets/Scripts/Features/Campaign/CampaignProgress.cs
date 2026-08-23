using System;
using System.Collections.Generic;
using UnityEngine;

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

    public static class CampaignSpawnDestinationIds
    {
        public const string FarmHub = "FarmHub";
        public const string BlackPinesArrival = "BlackPinesArrival";
    }

    /// <summary>
    /// Stable identity for the dedicated exposed-Heartroot inventory token.
    /// The stable ID is stored in campaign carryover/checkpoints; the
    /// serialized item ID and item name validate the authored pickup without
    /// changing Safety's Inventory or ItemStats schemas.
    /// </summary>
    public static class CampaignHeartrootInventoryIds
    {
        public const string StableId = "exposed_heartroot";
        public const string SerializedItemId =
            "BloodrootExposedHeartrootV1";
        public const string ItemName = "ExposedHeartroot";
        public const int RequiredCatalogCount = 6;

        private static readonly string[] OrderedRequiredStableIds =
        {
            "m1_garand",
            "m1_garand_ammo",
            "radar",
            "cursed_root_shard",
            "car_key",
            StableId
        };

        private static readonly IReadOnlyList<string> ReadOnlyRequiredIds =
            Array.AsReadOnly(OrderedRequiredStableIds);

        public static IReadOnlyList<string> RequiredStableIds =>
            ReadOnlyRequiredIds;

        public static bool IsRequiredStableId(string candidate)
        {
            string id = candidate?.Trim() ?? string.Empty;
            foreach (string required in OrderedRequiredStableIds)
            {
                if (string.Equals(required, id, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Stable campaign identifiers for the authored investigation evidence.
    /// Save data stores these strings rather than scene object names so scene
    /// hierarchy and presentation changes cannot invalidate progress.
    /// </summary>
    public static class CampaignEvidenceIds
    {
        public const string BlackPinesHealerSatchelTransferTag =
            "bp.healer_satchel_transfer_tag";
        public const string BlackPinesJuneFieldLog =
            "bp.june_field_log";
        public const string BlackPinesFireTowerRadioLog =
            "bp.fire_tower_radio_log";
        public const string StillwaterShipmentManifest =
            "stillwater.shipment_manifest";
        public const string StillwaterQualityLedger =
            "stillwater.quality_ledger";
        public const string StillwaterSampleTamperLog =
            "stillwater.sample_tamper_log";
        public const string HarrowPrizeHogRegistry =
            "harrow.prize_hog_registry";
        public const string HarrowContractorInvoices =
            "harrow.contractor_invoices";
        public const string HarrowTrustAssetRegister =
            "harrow.trust_asset_register";
        public const string HarrowPathologyConfiscationLedger =
            "harrow.pathology_confiscation_ledger";
        public const string HarrowGideonFinalDictation =
            "harrow.gideon_final_dictation";

        private static readonly string[] OrderedIds =
        {
            BlackPinesHealerSatchelTransferTag,
            BlackPinesJuneFieldLog,
            BlackPinesFireTowerRadioLog,
            StillwaterShipmentManifest,
            StillwaterQualityLedger,
            StillwaterSampleTamperLog,
            HarrowPrizeHogRegistry,
            HarrowContractorInvoices,
            HarrowTrustAssetRegister,
            HarrowPathologyConfiscationLedger,
            HarrowGideonFinalDictation
        };

        private static readonly IReadOnlyList<string> ReadOnlyIds =
            Array.AsReadOnly(OrderedIds);

        public static IReadOnlyList<string> All => ReadOnlyIds;

        public static bool IsCanonical(string evidenceId)
        {
            return TryGetArea(evidenceId, out _);
        }

        public static bool TryGetArea(
            string evidenceId,
            out CampaignAreaId area)
        {
            string id = evidenceId?.Trim() ?? string.Empty;
            switch (id)
            {
                case BlackPinesHealerSatchelTransferTag:
                case BlackPinesJuneFieldLog:
                case BlackPinesFireTowerRadioLog:
                    area = CampaignAreaId.BlackPines;
                    return true;
                case StillwaterShipmentManifest:
                case StillwaterQualityLedger:
                case StillwaterSampleTamperLog:
                    area = CampaignAreaId.StillwaterFeedMill;
                    return true;
                case HarrowPrizeHogRegistry:
                case HarrowContractorInvoices:
                case HarrowTrustAssetRegister:
                case HarrowPathologyConfiscationLedger:
                case HarrowGideonFinalDictation:
                    area = CampaignAreaId.HarrowEstate;
                    return true;
                default:
                    area = default;
                    return false;
            }
        }

        public static string RequiredNameStoneId(string evidenceId)
        {
            string id = evidenceId?.Trim() ?? string.Empty;
            return id switch
            {
                BlackPinesHealerSatchelTransferTag =>
                    CampaignNameStoneIds.Esther,
                StillwaterQualityLedger => CampaignNameStoneIds.Ruth,
                HarrowPathologyConfiscationLedger =>
                    CampaignNameStoneIds.Naomi,
                HarrowGideonFinalDictation => CampaignNameStoneIds.Nell,
                _ => string.Empty
            };
        }

        internal static string[] Normalize(IEnumerable<string> candidateIds)
        {
            var candidates = new HashSet<string>(StringComparer.Ordinal);
            if (candidateIds != null)
            {
                foreach (string candidate in candidateIds)
                {
                    string id = candidate?.Trim() ?? string.Empty;
                    if (id.Length > 0)
                    {
                        candidates.Add(id);
                    }
                }
            }

            var normalized = new List<string>(OrderedIds.Length);
            foreach (string id in OrderedIds)
            {
                if (candidates.Contains(id))
                {
                    normalized.Add(id);
                }
            }

            return normalized.ToArray();
        }
    }

    /// <summary>
    /// Stable campaign identifiers for the four true-name stones. Naomi's
    /// stone is intentionally assigned to Harrow Estate because Gideon moved
    /// her pathology evidence and confiscated stone to the liability vault.
    /// </summary>
    public static class CampaignNameStoneIds
    {
        public const string Esther = "name_stone_esther";
        public const string Ruth = "name_stone_ruth";
        public const string Naomi = "name_stone_naomi";
        public const string Nell = "name_stone_nell";

        private static readonly string[] OrderedIds =
        {
            Esther,
            Ruth,
            Naomi,
            Nell
        };

        private static readonly IReadOnlyList<string> ReadOnlyIds =
            Array.AsReadOnly(OrderedIds);

        public static IReadOnlyList<string> All => ReadOnlyIds;

        public static bool IsCanonical(string nameStoneId)
        {
            return TryGetArea(nameStoneId, out _);
        }

        public static bool TryGetArea(
            string nameStoneId,
            out CampaignAreaId area)
        {
            string id = nameStoneId?.Trim() ?? string.Empty;
            switch (id)
            {
                case Esther:
                    area = CampaignAreaId.BlackPines;
                    return true;
                case Ruth:
                    area = CampaignAreaId.StillwaterFeedMill;
                    return true;
                case Naomi:
                case Nell:
                    area = CampaignAreaId.HarrowEstate;
                    return true;
                default:
                    area = default;
                    return false;
            }
        }

        internal static string[] Normalize(IEnumerable<string> candidateIds)
        {
            var candidates = new HashSet<string>(StringComparer.Ordinal);
            if (candidateIds != null)
            {
                foreach (string candidate in candidateIds)
                {
                    string id = candidate?.Trim() ?? string.Empty;
                    if (id.Length > 0)
                    {
                        candidates.Add(id);
                    }
                }
            }

            var normalized = new List<string>(OrderedIds.Length);
            foreach (string id in OrderedIds)
            {
                if (candidates.Contains(id))
                {
                    normalized.Add(id);
                }
            }

            return normalized.ToArray();
        }
    }

    /// <summary>
    /// Stable identifiers for the five Root Tree offerings. The prologue
    /// object is distinct from the four later true-name stones even when all
    /// five currently share the same inventory presentation.
    /// </summary>
    public static class CampaignRootOfferingIds
    {
        public const string PrologueCursedObject =
            "prologue_cursed_object";

        private static readonly string[] OrderedIds =
        {
            PrologueCursedObject,
            CampaignNameStoneIds.Esther,
            CampaignNameStoneIds.Ruth,
            CampaignNameStoneIds.Naomi,
            CampaignNameStoneIds.Nell
        };

        private static readonly IReadOnlyList<string> ReadOnlyIds =
            Array.AsReadOnly(OrderedIds);

        public static IReadOnlyList<string> All => ReadOnlyIds;

        public static bool IsCanonical(string offeringId)
        {
            string id = offeringId?.Trim() ?? string.Empty;
            foreach (string canonicalId in OrderedIds)
            {
                if (string.Equals(
                        canonicalId,
                        id,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsNameStone(string offeringId)
        {
            return CampaignNameStoneIds.IsCanonical(offeringId);
        }

        internal static string[] Normalize(
            IEnumerable<string> candidateIds)
        {
            var candidates = new HashSet<string>(StringComparer.Ordinal);
            if (candidateIds != null)
            {
                foreach (string candidate in candidateIds)
                {
                    string id = candidate?.Trim() ?? string.Empty;
                    if (id.Length > 0)
                    {
                        candidates.Add(id);
                    }
                }
            }

            var normalized = new List<string>(OrderedIds.Length);
            foreach (string id in OrderedIds)
            {
                if (candidates.Contains(id))
                {
                    normalized.Add(id);
                }
            }

            return normalized.ToArray();
        }
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
            HubIntroductionCompleted = data.hubIntroductionCompleted;
            BlackPinesUnlocked = data.blackPinesUnlocked;
            BlackPinesCompleted = data.blackPinesCompleted;
            StillwaterUnlocked = data.stillwaterUnlocked;
            StillwaterCompleted = data.stillwaterCompleted;
            HarrowUnlocked = data.harrowUnlocked;
            HarrowCompleted = data.harrowCompleted;
            HollowUnlocked = data.hollowUnlocked;
            HollowTowerActivated = data.hollowTowerActivated;
            HollowCompleted = data.hollowCompleted;
            HollowVeilCrossed = data.hollowVeilCrossed;
            DefeatedWitchCount = Mathf.Clamp(
                data.defeatedWitchCount,
                0,
                3);
            HeartrootExposed = data.heartrootExposed;
            HeartrootCarried = data.heartrootCarried;
            HeartrootBurned = data.heartrootBurned;
            CampaignCompleted = data.campaignCompleted;
            PendingSceneName = data.pendingSceneName ?? string.Empty;
            PendingSpawnDestinationId =
                data.pendingSpawnDestinationId ?? string.Empty;
            collectedEvidenceIds =
                (string[])(data.collectedEvidenceIds?.Clone() ??
                           Array.Empty<string>());
            extractedNameStoneIds =
                (string[])(data.extractedNameStoneIds?.Clone() ??
                           Array.Empty<string>());
            offeredNameStoneIds =
                (string[])(data.offeredNameStoneIds?.Clone() ??
                           Array.Empty<string>());
            completedFarmEmergenceOfferingIds =
                (string[])(data.completedFarmEmergenceOfferingIds?.Clone() ??
                           Array.Empty<string>());
            PrologueCursedObjectRevealed =
                data.prologueCursedObjectRevealed;
            PrologueCursedObjectOffered =
                data.prologueCursedObjectOffered;
            PendingRootOfferingId =
                data.pendingRootOfferingId ?? string.Empty;
            ActiveFarmEmergenceOfferingId =
                data.activeFarmEmergenceOfferingId ?? string.Empty;
            NextPendingFarmEmergenceOfferingId =
                data.FindNextPendingFarmEmergenceOfferingId();
            PendingNameStoneOfferId =
                CampaignNameStoneIds.IsCanonical(PendingRootOfferingId)
                    ? PendingRootOfferingId
                    : string.Empty;
        }

        private readonly string[] collectedEvidenceIds;
        private readonly string[] extractedNameStoneIds;
        private readonly string[] offeredNameStoneIds;
        private readonly string[] completedFarmEmergenceOfferingIds;

        public int SaveVersion { get; }

        public bool PrologueCompleted { get; }

        public bool HubIntroductionCompleted { get; }

        public bool BlackPinesUnlocked { get; }

        public bool BlackPinesCompleted { get; }

        public bool StillwaterUnlocked { get; }

        public bool StillwaterCompleted { get; }

        public bool HarrowUnlocked { get; }

        public bool HarrowCompleted { get; }

        public bool HollowUnlocked { get; }

        public bool HollowTowerActivated { get; }

        public bool HollowCompleted { get; }

        public bool HollowVeilCrossed { get; }

        public int DefeatedWitchCount { get; }

        public bool HeartrootExposed { get; }

        /// <summary>
        /// Monotonic recovery fact. Current physical possession is derived as
        /// HeartrootCarried and not HeartrootBurned.
        /// </summary>
        public bool HeartrootCarried { get; }

        public bool HeartrootBurned { get; }

        public bool CampaignCompleted { get; }

        public string PendingSceneName { get; }

        public string PendingSpawnDestinationId { get; }

        public bool PrologueCursedObjectRevealed { get; }

        public bool PrologueCursedObjectOffered { get; }

        public string PendingRootOfferingId { get; }

        public string ActiveFarmEmergenceOfferingId { get; }

        public string NextPendingFarmEmergenceOfferingId { get; }

        public bool HasUnresolvedFarmEmergence =>
            ActiveFarmEmergenceOfferingId.Length > 0 ||
            NextPendingFarmEmergenceOfferingId.Length > 0;

        /// <summary>
        /// Compatibility view for existing Name Stone presenters. The
        /// prologue object is intentionally invisible through this property.
        /// </summary>
        public string PendingNameStoneOfferId { get; }

        public bool HasAllNameStonesOffered
        {
            get
            {
                foreach (string id in CampaignNameStoneIds.All)
                {
                    if (!Contains(offeredNameStoneIds, id))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public bool CanEnterHollow =>
            HarrowCompleted && HollowTowerActivated;

        public bool IsEvidenceCollected(string evidenceId)
        {
            return CampaignEvidenceIds.IsCanonical(evidenceId) &&
                   Contains(collectedEvidenceIds, evidenceId.Trim());
        }

        public bool IsNameStoneExtracted(string nameStoneId)
        {
            return CampaignNameStoneIds.IsCanonical(nameStoneId) &&
                   Contains(extractedNameStoneIds, nameStoneId.Trim());
        }

        public bool IsNameStoneOffered(string nameStoneId)
        {
            return CampaignNameStoneIds.IsCanonical(nameStoneId) &&
                   Contains(offeredNameStoneIds, nameStoneId.Trim());
        }

        public bool IsRootOfferingCommitted(string offeringId)
        {
            string id = offeringId?.Trim() ?? string.Empty;
            if (string.Equals(
                    id,
                    CampaignRootOfferingIds.PrologueCursedObject,
                    StringComparison.Ordinal))
            {
                return PrologueCursedObjectOffered;
            }

            return IsNameStoneOffered(id);
        }

        public bool IsFarmEmergenceCompleted(string offeringId)
        {
            string id = offeringId?.Trim() ?? string.Empty;
            return CampaignRootOfferingIds.IsCanonical(id) &&
                   Contains(completedFarmEmergenceOfferingIds, id);
        }

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

        private static bool Contains(string[] ids, string candidate)
        {
            if (ids == null || string.IsNullOrEmpty(candidate))
            {
                return false;
            }

            foreach (string id in ids)
            {
                if (string.Equals(id, candidate, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    [Serializable]
    internal sealed class CampaignSaveData
    {
        public const int CurrentVersion = 8;

        public int saveVersion = CurrentVersion;
        public bool prologueCompleted;
        public bool hubIntroductionCompleted;
        public bool blackPinesUnlocked;
        public bool blackPinesCompleted;
        public bool stillwaterUnlocked;
        public bool stillwaterCompleted;
        public bool harrowUnlocked;
        public bool harrowCompleted;
        public bool hollowUnlocked;
        public bool hollowTowerActivated;
        public bool hollowCompleted;
        public bool hollowVeilCrossed;
        public int defeatedWitchCount;
        public bool heartrootExposed;
        public bool heartrootCarried;
        public bool heartrootBurned;
        public bool campaignCompleted;
        public string pendingSceneName = string.Empty;
        public string pendingSpawnDestinationId = string.Empty;
        public string[] carriedInventoryItemIds = Array.Empty<string>();
        public int[] carriedInventoryQuantities = Array.Empty<int>();
        public string[] collectedEvidenceIds = Array.Empty<string>();
        public string[] extractedNameStoneIds = Array.Empty<string>();
        public string[] offeredNameStoneIds = Array.Empty<string>();
        public bool prologueCursedObjectRevealed;
        public bool prologueCursedObjectOffered;
        public string pendingRootOfferingId = string.Empty;
        public string activeFarmEmergenceOfferingId = string.Empty;
        public string[] completedFarmEmergenceOfferingIds =
            Array.Empty<string>();

        // Version 4 serialized this field. Version 5 migrates it into the
        // generic Root offering transaction and always clears the legacy copy.
        public string pendingNameStoneOfferId = string.Empty;

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
                hubIntroductionCompleted = hubIntroductionCompleted,
                blackPinesUnlocked = blackPinesUnlocked,
                blackPinesCompleted = blackPinesCompleted,
                stillwaterUnlocked = stillwaterUnlocked,
                stillwaterCompleted = stillwaterCompleted,
                harrowUnlocked = harrowUnlocked,
                harrowCompleted = harrowCompleted,
                hollowUnlocked = hollowUnlocked,
                hollowTowerActivated = hollowTowerActivated,
                hollowCompleted = hollowCompleted,
                hollowVeilCrossed = hollowVeilCrossed,
                defeatedWitchCount = defeatedWitchCount,
                heartrootExposed = heartrootExposed,
                heartrootCarried = heartrootCarried,
                heartrootBurned = heartrootBurned,
                campaignCompleted = campaignCompleted,
                pendingSceneName = pendingSceneName,
                pendingSpawnDestinationId = pendingSpawnDestinationId,
                carriedInventoryItemIds =
                    (string[])(carriedInventoryItemIds?.Clone() ??
                               Array.Empty<string>()),
                carriedInventoryQuantities =
                    (int[])(carriedInventoryQuantities?.Clone() ??
                            Array.Empty<int>()),
                collectedEvidenceIds =
                    (string[])(collectedEvidenceIds?.Clone() ??
                               Array.Empty<string>()),
                extractedNameStoneIds =
                    (string[])(extractedNameStoneIds?.Clone() ??
                               Array.Empty<string>()),
                offeredNameStoneIds =
                    (string[])(offeredNameStoneIds?.Clone() ??
                               Array.Empty<string>()),
                prologueCursedObjectRevealed =
                    prologueCursedObjectRevealed,
                prologueCursedObjectOffered = prologueCursedObjectOffered,
                pendingRootOfferingId = pendingRootOfferingId,
                activeFarmEmergenceOfferingId =
                    activeFarmEmergenceOfferingId,
                completedFarmEmergenceOfferingIds =
                    (string[])(completedFarmEmergenceOfferingIds?.Clone() ??
                               Array.Empty<string>()),
                pendingNameStoneOfferId = pendingNameStoneOfferId
            };
        }

        public void Normalize()
        {
            int loadedVersion = saveVersion;

            // Version 1 completed the prologue only after the player had
            // already reached the safe Hub. Preserve that semantic fact when
            // upgrading instead of replaying a new one-time arrival beat.
            if (loadedVersion < 2 && prologueCompleted)
            {
                hubIntroductionCompleted = true;
            }

            saveVersion = CurrentVersion;
            pendingSceneName ??= string.Empty;
            pendingSpawnDestinationId ??= string.Empty;
            carriedInventoryItemIds ??= Array.Empty<string>();
            carriedInventoryQuantities ??= Array.Empty<int>();
            collectedEvidenceIds = CampaignEvidenceIds.Normalize(
                collectedEvidenceIds);
            extractedNameStoneIds = CampaignNameStoneIds.Normalize(
                extractedNameStoneIds);
            offeredNameStoneIds = CampaignNameStoneIds.Normalize(
                offeredNameStoneIds);
            pendingRootOfferingId =
                pendingRootOfferingId?.Trim() ?? string.Empty;
            activeFarmEmergenceOfferingId =
                activeFarmEmergenceOfferingId?.Trim() ?? string.Empty;
            completedFarmEmergenceOfferingIds =
                CampaignRootOfferingIds.Normalize(
                    completedFarmEmergenceOfferingIds);
            pendingNameStoneOfferId =
                pendingNameStoneOfferId?.Trim() ?? string.Empty;

            NormalizePrologueEmergenceFacts();

            pendingSceneName = pendingSceneName.Trim();
            pendingSpawnDestinationId = pendingSpawnDestinationId.Trim();

            bool isOpenWorldArrival =
                string.Equals(
                    pendingSceneName,
                    CampaignSceneNames.OpenWorld,
                    StringComparison.Ordinal) &&
                string.Equals(
                    pendingSpawnDestinationId,
                    CampaignSpawnDestinationIds.BlackPinesArrival,
                    StringComparison.Ordinal);
            bool isFarmHubArrival =
                string.Equals(
                    pendingSceneName,
                    CampaignSceneNames.FarmPrologueHub,
                    StringComparison.Ordinal) &&
                string.Equals(
                    pendingSpawnDestinationId,
                    CampaignSpawnDestinationIds.FarmHub,
                    StringComparison.Ordinal);

            // Pending travel is a durable handoff, not arbitrary save data.
            // Keep only the two authored cross-scene arrivals, and never let
            // a pre-prologue/corrupt save bypass the Farm prerequisite.
            if (!prologueCompleted ||
                (!isOpenWorldArrival && !isFarmHubArrival))
            {
                pendingSceneName = string.Empty;
                pendingSpawnDestinationId = string.Empty;
            }

            if (carriedInventoryItemIds.Length !=
                carriedInventoryQuantities.Length)
            {
                carriedInventoryItemIds = Array.Empty<string>();
                carriedInventoryQuantities = Array.Empty<int>();
            }
            else
            {
                var migratedInventoryIds =
                    new List<string>(carriedInventoryItemIds.Length);
                var migratedInventoryQuantities =
                    new List<int>(carriedInventoryQuantities.Length);
                for (int index = 0;
                     index < carriedInventoryItemIds.Length;
                     index++)
                {
                    string itemId = carriedInventoryItemIds[index]?.Trim() ??
                                    string.Empty;
                    if (string.Equals(
                            itemId,
                            "name_stone",
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    migratedInventoryIds.Add(itemId);
                    migratedInventoryQuantities.Add(Mathf.Max(
                        0,
                        carriedInventoryQuantities[index]));
                }

                carriedInventoryItemIds = migratedInventoryIds.ToArray();
                carriedInventoryQuantities =
                    migratedInventoryQuantities.ToArray();
            }

            // Repair saves fail-closed at the first incomplete prerequisite.
            // A stray later flag must never skip an earlier campaign step.
            if (!prologueCompleted)
            {
                hubIntroductionCompleted = false;
                blackPinesUnlocked = false;
                blackPinesCompleted = false;
                stillwaterUnlocked = false;
                stillwaterCompleted = false;
                harrowUnlocked = false;
                harrowCompleted = false;
                hollowUnlocked = false;
                hollowTowerActivated = false;
                hollowCompleted = false;
            }
            else
            {
                blackPinesUnlocked = true;

                if (!blackPinesCompleted)
                {
                    stillwaterUnlocked = false;
                    stillwaterCompleted = false;
                    harrowUnlocked = false;
                    harrowCompleted = false;
                    hollowUnlocked = false;
                    hollowTowerActivated = false;
                    hollowCompleted = false;
                }
                else
                {
                    stillwaterUnlocked = true;

                    if (!stillwaterCompleted)
                    {
                        harrowUnlocked = false;
                        harrowCompleted = false;
                        hollowUnlocked = false;
                        hollowTowerActivated = false;
                        hollowCompleted = false;
                    }
                    else
                    {
                        harrowUnlocked = true;

                        if (!harrowCompleted)
                        {
                            hollowUnlocked = false;
                            hollowTowerActivated = false;
                            hollowCompleted = false;
                        }
                        else
                        {
                            // The final progression cylinder controls access
                            // through CanEnterHollow after Harrow.
                            hollowUnlocked = true;
                        }
                    }
                }
            }

            SeedLegacyQuestProgress(loadedVersion);
            NormalizeTowerCampaignProgress(loadedVersion);
            NormalizeQuestProgress();
            MigrateVersionFiveRootProgress(loadedVersion);
            NormalizeRootOfferingProgress();
            NormalizeHeartrootFinaleProgress(loadedVersion);
        }

        /// <summary>
        /// Retires legacy offering state while preserving completed campaign
        /// history. The final Hollow cylinder is now the only entry authority.
        /// </summary>
        private void NormalizeTowerCampaignProgress(int loadedVersion)
        {
            bool legacyHollowAccess = HasAllNameStonesOffered() ||
                                      hollowVeilCrossed ||
                                      hollowCompleted ||
                                      heartrootExposed ||
                                      heartrootCarried ||
                                      heartrootBurned ||
                                      campaignCompleted;
            if (harrowCompleted && legacyHollowAccess)
            {
                hollowTowerActivated = true;
            }

            // Keep these serialized fields solely so older saves load, then
            // discard their retired values before gameplay reads the state.
            extractedNameStoneIds = Array.Empty<string>();
            offeredNameStoneIds = Array.Empty<string>();
            pendingNameStoneOfferId = string.Empty;
            if (CampaignNameStoneIds.IsCanonical(pendingRootOfferingId))
            {
                pendingRootOfferingId = string.Empty;
            }

            if (CampaignNameStoneIds.IsCanonical(activeFarmEmergenceOfferingId))
            {
                activeFarmEmergenceOfferingId = string.Empty;
            }

            completedFarmEmergenceOfferingIds = ContainsId(
                    completedFarmEmergenceOfferingIds,
                    CampaignRootOfferingIds.PrologueCursedObject)
                ? new[] { CampaignRootOfferingIds.PrologueCursedObject }
                : Array.Empty<string>();
        }

        private void NormalizeHeartrootFinaleProgress(int loadedVersion)
        {
            defeatedWitchCount = Mathf.Clamp(defeatedWitchCount, 0, 3);

            // Before V6 the Hollow area could be completed only by the final
            // Recover Heartroot objective. Preserve that completed history as
            // recovered cargo while still requiring the new Farm burn ending.
            if (loadedVersion < 6 && hollowCompleted)
            {
                hollowVeilCrossed = true;
                defeatedWitchCount = 3;
                heartrootExposed = true;
                heartrootCarried = true;
            }

            bool canEnterHollow = harrowCompleted && hollowTowerActivated;
            if (!canEnterHollow)
            {
                hollowCompleted = false;
                hollowVeilCrossed = false;
                defeatedWitchCount = 0;
                heartrootExposed = false;
                heartrootCarried = false;
                heartrootBurned = false;
                campaignCompleted = false;
                NormalizeHeartrootInventoryToken();
                return;
            }

            // Repair monotonic prerequisite facts forward. A later durable
            // fact proves every earlier one. Current physical possession is
            // derived from carried and not burned; carried itself is history.
            if (campaignCompleted)
            {
                heartrootBurned = true;
            }

            if (heartrootBurned)
            {
                campaignCompleted = true;
                heartrootCarried = true;
                heartrootExposed = true;
                defeatedWitchCount = 3;
                hollowVeilCrossed = true;
                hollowCompleted = true;
            }
            else if (hollowCompleted || heartrootCarried)
            {
                heartrootCarried = true;
                heartrootExposed = true;
                defeatedWitchCount = 3;
                hollowVeilCrossed = true;
                hollowCompleted = true;
            }
            else if (heartrootExposed || defeatedWitchCount == 3)
            {
                heartrootExposed = true;
                defeatedWitchCount = 3;
                hollowVeilCrossed = true;
            }
            else if (defeatedWitchCount > 0)
            {
                hollowVeilCrossed = true;
            }

            if (!hollowVeilCrossed)
            {
                defeatedWitchCount = 0;
                heartrootExposed = false;
                heartrootCarried = false;
                heartrootBurned = false;
                campaignCompleted = false;
                hollowCompleted = false;
            }

            NormalizeHeartrootInventoryToken();
        }

        private void NormalizeHeartrootInventoryToken()
        {
            carriedInventoryItemIds ??= Array.Empty<string>();
            carriedInventoryQuantities ??= Array.Empty<int>();
            if (carriedInventoryItemIds.Length !=
                carriedInventoryQuantities.Length)
            {
                carriedInventoryItemIds = Array.Empty<string>();
                carriedInventoryQuantities = Array.Empty<int>();
            }

            int tokenIndex = -1;
            for (int index = 0;
                 index < carriedInventoryItemIds.Length;
                 index++)
            {
                if (string.Equals(
                        carriedInventoryItemIds[index]?.Trim(),
                        CampaignHeartrootInventoryIds.StableId,
                        StringComparison.Ordinal))
                {
                    tokenIndex = index;
                    break;
                }
            }

            int requiredQuantity = heartrootCarried && !heartrootBurned
                ? 1
                : 0;
            if (tokenIndex >= 0)
            {
                carriedInventoryQuantities[tokenIndex] = requiredQuantity;
                return;
            }

            if (requiredQuantity == 0)
                return;

            int oldLength = carriedInventoryItemIds.Length;
            Array.Resize(ref carriedInventoryItemIds, oldLength + 1);
            Array.Resize(ref carriedInventoryQuantities, oldLength + 1);
            carriedInventoryItemIds[oldLength] =
                CampaignHeartrootInventoryIds.StableId;
            carriedInventoryQuantities[oldLength] = 1;
        }

        public bool IsEvidenceCollected(string evidenceId)
        {
            return ContainsId(collectedEvidenceIds, evidenceId);
        }

        public bool IsNameStoneExtracted(string nameStoneId)
        {
            return ContainsId(extractedNameStoneIds, nameStoneId);
        }

        public bool IsNameStoneOffered(string nameStoneId)
        {
            return ContainsId(offeredNameStoneIds, nameStoneId);
        }

        public bool HasAllNameStonesOffered()
        {
            foreach (string id in CampaignNameStoneIds.All)
            {
                if (!IsNameStoneOffered(id))
                {
                    return false;
                }
            }

            return true;
        }

        public bool IsRootOfferingCommitted(string offeringId)
        {
            string id = offeringId?.Trim() ?? string.Empty;
            if (string.Equals(
                    id,
                    CampaignRootOfferingIds.PrologueCursedObject,
                    StringComparison.Ordinal))
            {
                return prologueCursedObjectOffered;
            }

            return CampaignNameStoneIds.IsCanonical(id) &&
                   IsNameStoneOffered(id);
        }

        public bool IsFarmEmergenceCompleted(string offeringId)
        {
            return ContainsId(
                completedFarmEmergenceOfferingIds,
                offeringId);
        }

        public string FindNextPendingFarmEmergenceOfferingId()
        {
            foreach (string id in CampaignRootOfferingIds.All)
            {
                if (IsRootOfferingCommitted(id) &&
                    !IsFarmEmergenceCompleted(id))
                {
                    return id;
                }
            }

            return string.Empty;
        }

        public bool HasUnresolvedFarmEmergence()
        {
            return !string.IsNullOrEmpty(activeFarmEmergenceOfferingId) ||
                   !string.IsNullOrEmpty(
                       FindNextPendingFarmEmergenceOfferingId());
        }

        public bool TryCommitRootOffering(string offeringId)
        {
            string id = offeringId?.Trim() ?? string.Empty;
            if (string.Equals(
                    id,
                    CampaignRootOfferingIds.PrologueCursedObject,
                    StringComparison.Ordinal))
            {
                if (!prologueCursedObjectRevealed ||
                    prologueCursedObjectOffered)
                {
                    return false;
                }

                prologueCursedObjectOffered = true;
                return true;
            }

            return CampaignNameStoneIds.IsCanonical(id) &&
                   TryAddOfferedNameStone(id);
        }

        public bool TryAddCompletedFarmEmergence(string offeringId)
        {
            string id = offeringId?.Trim() ?? string.Empty;
            if (!CampaignRootOfferingIds.IsCanonical(id) ||
                !IsRootOfferingCommitted(id) ||
                IsFarmEmergenceCompleted(id))
            {
                return false;
            }

            completedFarmEmergenceOfferingIds = Append(
                completedFarmEmergenceOfferingIds,
                id);
            completedFarmEmergenceOfferingIds =
                CampaignRootOfferingIds.Normalize(
                    completedFarmEmergenceOfferingIds);
            return true;
        }

        public bool TryAddEvidence(string evidenceId)
        {
            string id = evidenceId?.Trim() ?? string.Empty;
            if (!CampaignEvidenceIds.IsCanonical(id) ||
                IsEvidenceCollected(id))
            {
                return false;
            }

            collectedEvidenceIds = Append(collectedEvidenceIds, id);
            collectedEvidenceIds = CampaignEvidenceIds.Normalize(
                collectedEvidenceIds);
            return true;
        }

        public bool TryAddExtractedNameStone(string nameStoneId)
        {
            string id = nameStoneId?.Trim() ?? string.Empty;
            if (!CampaignNameStoneIds.IsCanonical(id) ||
                IsNameStoneExtracted(id))
            {
                return false;
            }

            extractedNameStoneIds = Append(extractedNameStoneIds, id);
            extractedNameStoneIds = CampaignNameStoneIds.Normalize(
                extractedNameStoneIds);
            return true;
        }

        public bool TryAddOfferedNameStone(string nameStoneId)
        {
            string id = nameStoneId?.Trim() ?? string.Empty;
            if (!IsNameStoneExtracted(id) || IsNameStoneOffered(id))
            {
                return false;
            }

            offeredNameStoneIds = Append(offeredNameStoneIds, id);
            offeredNameStoneIds = CampaignNameStoneIds.Normalize(
                offeredNameStoneIds);
            return true;
        }

        /// <summary>
        /// Regional cylinders now complete their normal area progression only.
        /// </summary>
        public bool ApplyTowerCompletionCredits(CampaignAreaId area)
        {
            return false;
        }

        /// <summary>
        /// Durably activates the final Hollow cylinder.
        /// </summary>
        public bool ApplyHollowTowerCredit()
        {
            if (!harrowCompleted || hollowTowerActivated)
            {
                return false;
            }

            hollowTowerActivated = true;
            return true;
        }

        private void NormalizeQuestProgress()
        {
            collectedEvidenceIds = FilterEvidenceForUnlockedAreas(
                collectedEvidenceIds);
            extractedNameStoneIds = FilterStonesForUnlockedAreas(
                extractedNameStoneIds);

            var extracted = new HashSet<string>(
                extractedNameStoneIds,
                StringComparer.Ordinal);
            var offered = new List<string>();
            foreach (string id in CampaignNameStoneIds.Normalize(
                         offeredNameStoneIds))
            {
                if (extracted.Contains(id))
                {
                    offered.Add(id);
                }
            }

            offeredNameStoneIds = offered.ToArray();
        }

        private void NormalizePrologueEmergenceFacts()
        {
            bool prologueEmergenceCompleted = ContainsId(
                completedFarmEmergenceOfferingIds,
                CampaignRootOfferingIds.PrologueCursedObject);

            if (prologueEmergenceCompleted)
            {
                prologueCompleted = true;
            }

            if (prologueCompleted)
            {
                prologueCursedObjectRevealed = true;
                prologueCursedObjectOffered = true;
                if (!prologueEmergenceCompleted)
                {
                    completedFarmEmergenceOfferingIds = Append(
                        completedFarmEmergenceOfferingIds,
                        CampaignRootOfferingIds.PrologueCursedObject);
                    completedFarmEmergenceOfferingIds =
                        CampaignRootOfferingIds.Normalize(
                            completedFarmEmergenceOfferingIds);
                }
            }

            if (prologueCursedObjectOffered)
            {
                prologueCursedObjectRevealed = true;
            }
        }

        private void MigrateVersionFiveRootProgress(int loadedVersion)
        {
            if (loadedVersion < 5)
            {
                // Every offering that predates emergence persistence already
                // belongs to completed campaign history. Mark its wave as
                // resolved so upgrading never creates surprise retroactive
                // attacks at the Farm hub.
                foreach (string nameStoneId in offeredNameStoneIds)
                {
                    if (!IsFarmEmergenceCompleted(nameStoneId))
                    {
                        completedFarmEmergenceOfferingIds = Append(
                            completedFarmEmergenceOfferingIds,
                            nameStoneId);
                    }
                }

                if (string.IsNullOrEmpty(pendingRootOfferingId) &&
                    CampaignNameStoneIds.IsCanonical(
                        pendingNameStoneOfferId))
                {
                    pendingRootOfferingId = pendingNameStoneOfferId;
                }
            }

            pendingNameStoneOfferId = string.Empty;
        }

        private void NormalizeRootOfferingProgress()
        {
            NormalizePrologueEmergenceFacts();

            string[] canonicalCompleted =
                CampaignRootOfferingIds.Normalize(
                    completedFarmEmergenceOfferingIds);
            var completed = new List<string>(canonicalCompleted.Length);
            foreach (string id in canonicalCompleted)
            {
                if (IsRootOfferingCommitted(id))
                {
                    completed.Add(id);
                }
            }

            // A valid runtime transaction can produce only one committed but
            // incomplete offering. If hand-edited/corrupt v5 data contains a
            // backlog, retain the earliest unresolved emergence and treat all
            // later committed offerings as already resolved. This repairs the
            // invariant without replaying old waves or revoking offerings.
            string unresolvedId = string.Empty;
            foreach (string id in CampaignRootOfferingIds.All)
            {
                if (!IsRootOfferingCommitted(id) || completed.Contains(id))
                {
                    continue;
                }

                if (unresolvedId.Length == 0)
                {
                    unresolvedId = id;
                }
                else
                {
                    completed.Add(id);
                }
            }

            completedFarmEmergenceOfferingIds =
                CampaignRootOfferingIds.Normalize(completed);

            if (!CampaignRootOfferingIds.IsCanonical(
                    activeFarmEmergenceOfferingId) ||
                !string.Equals(
                    activeFarmEmergenceOfferingId,
                    unresolvedId,
                    StringComparison.Ordinal) ||
                IsFarmEmergenceCompleted(activeFarmEmergenceOfferingId))
            {
                activeFarmEmergenceOfferingId = string.Empty;
            }

            bool pendingIsAvailable =
                CampaignRootOfferingIds.IsCanonical(
                    pendingRootOfferingId) &&
                !IsRootOfferingCommitted(pendingRootOfferingId) &&
                unresolvedId.Length == 0;

            if (pendingIsAvailable &&
                string.Equals(
                    pendingRootOfferingId,
                    CampaignRootOfferingIds.PrologueCursedObject,
                    StringComparison.Ordinal))
            {
                pendingIsAvailable = prologueCursedObjectRevealed &&
                                     !prologueCompleted;
            }
            else if (pendingIsAvailable)
            {
                pendingIsAvailable =
                    CampaignNameStoneIds.IsCanonical(
                        pendingRootOfferingId) &&
                    IsNameStoneExtracted(pendingRootOfferingId);
            }

            if (!pendingIsAvailable)
            {
                pendingRootOfferingId = string.Empty;
            }

            pendingNameStoneOfferId = string.Empty;
        }

        private void SeedLegacyQuestProgress(int loadedVersion)
        {
            if (loadedVersion >= 4)
            {
                return;
            }

            if (blackPinesCompleted)
            {
                TryAddEvidence(
                    CampaignEvidenceIds.BlackPinesHealerSatchelTransferTag);
                TryAddEvidence(CampaignEvidenceIds.BlackPinesJuneFieldLog);
                TryAddEvidence(
                    CampaignEvidenceIds.BlackPinesFireTowerRadioLog);
                TryAddExtractedNameStone(CampaignNameStoneIds.Esther);
            }

            if (stillwaterCompleted)
            {
                TryAddEvidence(
                    CampaignEvidenceIds.StillwaterShipmentManifest);
                TryAddEvidence(CampaignEvidenceIds.StillwaterQualityLedger);
                TryAddEvidence(
                    CampaignEvidenceIds.StillwaterSampleTamperLog);
                TryAddExtractedNameStone(CampaignNameStoneIds.Ruth);
            }

            if (harrowCompleted)
            {
                TryAddEvidence(CampaignEvidenceIds.HarrowPrizeHogRegistry);
                TryAddEvidence(CampaignEvidenceIds.HarrowContractorInvoices);
                TryAddEvidence(CampaignEvidenceIds.HarrowTrustAssetRegister);
                TryAddEvidence(
                    CampaignEvidenceIds.HarrowPathologyConfiscationLedger);
                TryAddEvidence(
                    CampaignEvidenceIds.HarrowGideonFinalDictation);
                TryAddExtractedNameStone(CampaignNameStoneIds.Naomi);
                TryAddExtractedNameStone(CampaignNameStoneIds.Nell);
            }
        }

        private string[] FilterEvidenceForUnlockedAreas(string[] ids)
        {
            var filtered = new List<string>();
            foreach (string id in CampaignEvidenceIds.Normalize(ids))
            {
                if (CampaignEvidenceIds.TryGetArea(id, out CampaignAreaId area) &&
                    IsAreaUnlocked(area))
                {
                    filtered.Add(id);
                }
            }

            return filtered.ToArray();
        }

        private string[] FilterStonesForUnlockedAreas(string[] ids)
        {
            var filtered = new List<string>();
            foreach (string id in CampaignNameStoneIds.Normalize(ids))
            {
                if (CampaignNameStoneIds.TryGetArea(id, out CampaignAreaId area) &&
                    IsAreaUnlocked(area))
                {
                    filtered.Add(id);
                }
            }

            return filtered.ToArray();
        }

        private static bool ContainsId(string[] ids, string candidate)
        {
            string id = candidate?.Trim() ?? string.Empty;
            if (ids == null || id.Length == 0)
            {
                return false;
            }

            foreach (string existing in ids)
            {
                if (string.Equals(existing, id, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string[] Append(string[] ids, string value)
        {
            int length = ids?.Length ?? 0;
            var result = new string[length + 1];
            if (length > 0)
            {
                Array.Copy(ids, result, length);
            }

            result[length] = value;
            return result;
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
