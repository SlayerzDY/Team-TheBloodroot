using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Bloodroot.Campaign;
using Bloodroot.Features.AlphaEnemies;
using Bloodroot.Features.FarmPrologue;
using Bloodroot.Features.WorldMissions;
using Bloodroot.OpenWorld;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Transactional, editor-authored wiring for the Farm prologue/hub and the
/// open-world campaign foundation. Runtime systems never create UI.
/// </summary>
public static class BloodrootCampaignFoundationSetup
{
    private enum RootServiceSchema
    {
        CurrentOnly,
        LegacyOnly,
        LegacyOrCurrent
    }

    private enum SafetyExtractionMenuOption
    {
        BlackPines,
        FarmHub
    }

    private const string FarmScenePath =
        "Assets/Scenes/Campaign/Farm_PrologueHub.unity";
    private const string OpenWorldScenePath =
        "Assets/Scenes/OpenWorld/Bloodroot_OpenWorld.unity";
    private const string NewFarmVisualMigrationSha256 =
        "EAA4B03E2D9D8A6345A4ED72E8FAC1867AF794E62BAB941B8DE3BCEB87EBA58E";
    private const string SafetyOpenWorldMigrationSha256 =
        "D21D398202621DC50C0D513722BF7479B5351184DAEBD7BB0C98128F8BD64FBC";
    private const string MainMenuScenePath =
        "Assets/Scenes/Alpha/MainMenu.unity";
    private const string FarmNavMeshDataPath =
        "Assets/Scenes/Campaign/NavMesh-Farm_PrologueHub.asset";
    private const string LegacyMissingFarmNavMeshGuid =
        "6f136a7fefe4db54b93fff26e891419d";
    private const string SafetyPlayerPrefabPath =
        "Assets/PreFabs/Player/Player.prefab";
    private const string SafetyPlayerPrefabGuid =
        "97b58db9d62441945a90056c1b1b933b";
    private const string LegacyPlayerForLevelPrefabPath =
        "Assets/PreFabs/Player/Player ForLevel.prefab";
    private const string SafetyBaseSpawnPrefabPath =
        "Assets/PreFabs/Player/PlayerSpawnPos.prefab";
    private const string SafetyBaseSpawnPrefabGuid =
        "c1dc0fd2d63e48c4cb65ad0eee06c979";
    private const string SafetyBlackPinesSpawnPrefabPath =
        "Assets/PreFabs/Player/PlayerSpawnPosBlackPines.prefab";
    private const string SafetyBlackPinesSpawnPrefabGuid =
        "1d8a7b80e68f16345bc308a8565456fe";
    private const string SafetyStillwaterSpawnPrefabPath =
        "Assets/PreFabs/Player/PlayerSpawnPosStillwater.prefab";
    private const string SafetyStillwaterSpawnPrefabGuid =
        "0608122c73a37134e997b728b9d7b43d";
    private const string SafetyHarrowSpawnPrefabPath =
        "Assets/PreFabs/Player/PlayerSpawnPosHarrowEstate.prefab";
    private const string SafetyHarrowSpawnPrefabGuid =
        "a9bf3d15b31f1c345ace9e758cad9f24";
    private const string SafetyHollowSpawnPrefabPath =
        "Assets/PreFabs/Player/PlayerSpawnPosBloodRootHollow.prefab";
    private const string SafetyHollowSpawnPrefabGuid =
        "dbb17ee233b133e43bd3e33c902d7173";
    private const string SafetyHubSpawnPrefabPath =
        "Assets/PreFabs/Player/PlayerSpawnPosHub.prefab";
    private const string SafetyHubSpawnPrefabGuid =
        "2b2782b5b1adec04cac6dfd41674d494";
    private const string SafetyTruckEscapePrefabPath =
        "Assets/PreFabs/Features/TruckEscapeEnding/TruckEscapeEnding.prefab";
    private const string SafetyTruckEscapePrefabGuid =
        "06798bd16365e8c4488c5e111b4ebe62";
    private const long SafetyLockedExtractionSourceFileId =
        6964299487611263512L;
    private const long SafetyTruckRootColliderSourceFileId =
        8400000000000003L;
    private const string SafetyTruckKeyPrefabPath =
        "Assets/PreFabs/Items/ItemPickups/Item Variants/Item_Key.prefab";
    private const string SafetyTruckKeyPrefabGuid =
        "3007f4358733b934d9c6ff4468631649";
    private const long SafetyTruckKeyItemSourceFileId =
        7248613269753264216L;
    private const string SafetyCursedPresentationPrefabPath =
        "Assets/PreFabs/Items/ItemPickups/Item Variants/" +
        "Item_CursedItem.prefab";
    private const string SafetyCursedPresentationPrefabGuid =
        "fba98a1dbc550e34fbc149aaa5ffdfb9";
    private const string SafetyCursedPreviewPresentationPrefabPath =
        "Assets/PreFabs/Items/ItemPickups/Item Variants/" +
        "Item_CursedItem_Preview Variant.prefab";
    private const string SafetyCursedPreviewPresentationPrefabGuid =
        "fe10ccb70f42ec8488781f3de931ea06";
    private const string OfferedVisualSlotName =
        "OFFERED_OBJECT_00_PROLOGUE";
    private const string OfferedVisualsRootName =
        "Offered Name Stone Visuals";
    private const string RootTreeOfferingName =
        "Campaign Root Tree Offering";
    private const string PrologueCursedPickupName =
        "Prologue Cursed Object Pickup";
    private const string PrologueCursedPresentationName =
        "Protected Cursed Object Presentation";
    private const string ImportedCursedPresentationName = "ASSET_Visual";
    private const string SafetyCursedPreviewInstanceName =
        "Item_CursedItem_Preview";
    private const string SafetyTreeInteractionUiName = "TreeInteractionUI";
    private const string SafetyItemPreviewLayerName = "ItemPreview";
    private const int SafetyItemPreviewLayer = 16;
    private const string LegacyTruckKeyPrefabPath =
        "Assets/PreFabs/Features/TruckEscapeEnding/TruckEscapeKey.prefab";
    private const string LegacyTruckKeyPrefabGuid =
        "3ede01ee28f24c56a12cdad47e75f34d";
    private const string RemovedTruckEscapeEndingScriptGuid =
        "5f77b220d8b24b7eb70b5023f4ad141c";
    private const string SafetyUiPrefabPath = "Assets/PreFabs/UI/UI.prefab";
    private const string SafetyUiPrefabGuid =
        "0e4f77b2cb9385a4f970b1e3413e896f";
    private const long SafetyExtractionMenuSourceFileId =
        9027542227490275248L;
    private const long SafetyExtractionOpenLevel1ButtonSourceFileId =
        8237981792648885035L;
    private const long SafetyExtractionOpenLevel2ButtonSourceFileId =
        3043962025385560768L;
    private const long SafetyExtractionOpenLevel3ButtonSourceFileId =
        1410335463376463448L;
    private const long SafetyExtractionOpenLevel4ButtonSourceFileId =
        5862654443021749835L;
    private const long SafetyExtractionHubButtonSourceFileId =
        8016916700229987355L;
    private const long SafetyExtractionOpenWorldButtonSourceFileId =
        6983105011337631069L;
    private const long SafetyExtractionFarmHubButtonSourceFileId =
        243032723362659174L;
    private const long SafetyExtractionCloseButtonSourceFileId =
        4938900468556415186L;
    private const long SafetyItemDatabaseSourceFileId =
        2963261659027704776L;
    private const string SafetyItemDatabaseScriptGuid =
        "258343aa205f11644ab58c1a6b6c16b2";
    private const long SafetyGameManagerSourceFileId =
        9126216655841172495L;
    private const string SafetyGameManagerScriptPath =
        "Assets/Scripts/LectureCode/gameManager.cs";
    private const string SafetyGameManagerScriptGuid =
        "26252ad9e2916294f86f4bdb9429d5eb";
    private const long SafetyRespawnButtonSourceFileId =
        2300431029082004298L;
    private const long SafetyRespawnFeedbackSourceFileId =
        4304174889121066551L;
    private const long SafetyButtonFunctionsSourceFileId =
        6970637296006004547L;
    private const string SafetyButtonFunctionsScriptPath =
        "Assets/Scripts/LectureCode/buttonFunctions.cs";
    private const string SafetyButtonFunctionsScriptGuid =
        "9db772dc605d1ab4a8e436e57a590930";
    private const int SafetyExtractionRequiredDefensesBeat = 1;
    private const long SafetyPlayerMainBodyTransformFileId =
        3956975264040935814L;
    private const string SafetyPlayerNestedCharacterPrefabGuid =
        "c8a1631c918379e4d836fa9efc15b08c";
    private const long SafetyPlayerNestedMainBodyTransformFileId =
        5703809270293624016L;
    private const string SafetyPlayerMainBodyModelGuid =
        "a53208845315ff94cb0c6341bce9348d";
    private const long SafetyPlayerMainBodyModelTransformFileId =
        -1406702563382136000L;
    private const long SafetyPlayerNestedCharacterRootTransformFileId =
        6708914341681198381L;
    private static readonly Vector3 SafetyPlayerNestedCharacterRootPosition =
        new(0f, -1.05f, 0f);
    private static readonly Vector3 SafetyPlayerNestedMainBodyPosition =
        new(25.52f, 0f, -66.78f);
    private const string FarmBackupPath =
        "Assets/Scenes/Campaign/Backups/Farm_PrologueHub_PreCampaignFoundation.unity";
    private const string OpenWorldBackupPath =
        "Assets/Scenes/OpenWorld/Backups/Bloodroot_OpenWorld_PreCampaignFoundation.unity";
    private const string EditorBuildSettingsPath =
        "ProjectSettings/EditorBuildSettings.asset";
    private const string RiflePickupPath =
        "Assets/PreFabs/AlphaPlaceholders/CampaignInventoryTokens/" +
        "M1_Garand_CampaignToken.prefab";
    private const string AmmoPickupPath =
        "Assets/PreFabs/AlphaPlaceholders/CampaignInventoryTokens/" +
        "M1_Garand_Ammo_CampaignToken.prefab";
    private const string RadarPickupPath =
        "Assets/PreFabs/AlphaPlaceholders/CampaignInventoryTokens/" +
        "Radar_CampaignToken.prefab";
    private const string PistolStatsPath =
        "Assets/PreFabs/Guns/Gun - Pistol.asset";
    private const string RifleStatsPath =
        "Assets/PreFabs/Guns/Gun - Rifle.asset";
    private const string ShotgunStatsPath =
        "Assets/PreFabs/Guns/Gun - Shotgun.asset";
    private const string RiflePickupGuid =
        "ab8bd3f9f7ec9b84da08e44c2cac39bd";
    private const string AmmoPickupGuid =
        "256d03bb9e7eda54eb26a8f311f4b1a0";
    private const string RadarPickupGuid =
        "af0d3c401c450df48936222e1a530401";
    private const string PistolStatsGuid =
        "b6d29358f0bf3d845805f3bcb22716dc";
    private const string RifleStatsGuid =
        "fae2818dcaf026f40adf374797892c06";
    private const string ShotgunStatsGuid =
        "28a05e06a8403e647ab62a3025b18e2b";
    private const string CursedShardPickupPath =
        "Assets/PreFabs/AlphaPlaceholders/CampaignInventoryTokens/" +
        "Cursed_Root_Shard_CampaignToken.prefab";
    private const string PrologueCursedObjectPickupPath =
        "Assets/PreFabs/AlphaPlaceholders/CampaignInventoryTokens/" +
        "Cursed_Item_CampaignToken.prefab";
    private const string ExposedHeartrootPickupPath =
        "Assets/PreFabs/AlphaPlaceholders/CampaignInventoryTokens/" +
        "Exposed_Heartroot_CampaignToken.prefab";
    private const string ItemPickupMasterPath =
        "Assets/PreFabs/Items/ItemPickups/Item_Pickup_Master.prefab";
    private const string ItemPickupMasterGuid =
        "10f052bb3168cca4e9b79c51d831fca3";
    private const long ItemPickupMasterItemFileId = 4822712193961387962L;
    private const long ItemPickupMasterColliderFileId = 895034978919146484L;
    private const string SafetyLeatherPickupPath =
        "Assets/PreFabs/Items/ItemPickups/Item Variants/Item_Leather.prefab";
    private const string SafetyLeatherPickupGuid =
        "8b3610a9ff3a5ba4abacdd05a4c8309b";
    private const string SafetyBoxPickupPath =
        "Assets/PreFabs/Items/ItemPickups/Item Variants/Item_Box.prefab";
    private const string SafetyStickPickupPath =
        "Assets/PreFabs/Items/ItemPickups/Item Variants/Item_Branch.prefab";
    private const string SafetyIronOrePickupPath =
        "Assets/PreFabs/Items/ItemPickups/Item Variants/Item_IronOre.prefab";
    private const string SafetyScrapMetalPickupPath =
        "Assets/PreFabs/Items/ItemPickups/Item Variants/Item_ScrapMetal.prefab";
    private const string SafetyStonePickupPath =
        "Assets/PreFabs/Items/ItemPickups/Item Variants/Item_Stone.prefab";
    private const string LegacyMissingFarmItemGuid =
        "993b416eddc25e949b7381ccee6c2f10";
    private const string LegacyBrokenFarmPickupName = "Item (6)";
    private const string SafetyBoarPrefabPath =
        "Assets/PreFabs/Enemies/Boar.prefab";
    private const string SafetyBoarPrefabGuid =
        "c8ad7f826e5d0ab4492f0a5a73e86d54";
    private const string SafetyBoarRootPrefabPath =
        "Assets/PreFabs/Enemies/BoarRoot.prefab";
    private const string SafetyBoarRootPrefabGuid =
        "e81ccd077fe525944bd95804a17261ee";
    private const string SafetyBoarAnimatorControllerPath =
        "Assets/UnityStoreDownloads/RedCambala/Animals/WildBoar/" +
        "Prefabs/BoarAnimator.controller";
    private const string SafetyBoarAnimatorControllerGuid =
        "4057d31ce3d918044bc13b2bbcfce93b";
    private const string LegacySafetyScreecherPrefabPath =
        "Assets/PreFabs/Enemies/Screecher.prefab";
    private const string LegacySafetyJuggernautPrefabPath =
        "Assets/PreFabs/Enemies/Juggernaut.prefab";
    private const string SafetyInfestationSpawnerPrefabPath =
        "Assets/PreFabs/Traps/Infestation Spawner.prefab";
    private const string SafetyInfestationSpawnerPrefabGuid =
        "e1ce03fef491c0348b78803a7cc428bb";
    private const string SafetyTerrainPrefabPath =
        "Assets/PreFabs/Level Stuff/SpawnNewLevel/Terrain.prefab";
    private const string SafetyTerrainPrefabGuid =
        "1f79be94585e4044fa81cd653aa395fd";
    private const long SafetyInfestationSpawnVolumeGameObjectFileId =
        6975027932341373004L;
    private const long SafetyTerrainSpawnVolumeColliderFileId =
        8660104687031834336L;
    private const string LegacyInfestationSpawnVolumeName = "SpawnInfest 2";
    private const string LegacyCampaignScreecherVariantPath =
        "Assets/PreFabs/AlphaPlaceholders/Screecher_CampaignCompatible.prefab";
    private const string LegacyCampaignJuggernautVariantPath =
        "Assets/PreFabs/AlphaPlaceholders/Juggernaut_CampaignCompatible.prefab";
    private const string FarmBackupSha256Lf =
        "2F994F37AEF043060DF1E88D78C9E9751CBC73A30373E65568400059E8CD19E0";
    private const string FarmBackupSha256CrLf =
        "E2398B82F8A7493484E28ED7CDB7D701872DDCED45B3B2CAA7AAD916127EA92F";
    private const string OpenWorldBackupSha256Lf =
        "4FFF4EFC332599F7B8256019B4A3B28463D9C9379EC7E8382275505FB6909DD7";
    private const string OpenWorldBackupSha256CrLf =
        "F41240B48D3B75005DCCAAE394B9BBEC95C50C1F7277F6DC6314E5F296DB97E6";
    private const int MobSpawnerWalkableAreaMask = 1;
    private const int FarmTerrainLayerIndex = 14;
    private const int CanonicalFarmNavMeshLayerMask =
        MobSpawnerWalkableAreaMask | (1 << FarmTerrainLayerIndex);
    private const float NavMeshDistanceTolerance = 0.01f;
    // The authored combat anchor sits 6.23m from its nearest baked point.
    // Keep this independent of enemy dimensions and below the runtime's
    // much broader MobSpawner sampling radius.
    private const float CombatAnchorMaxNavMeshDisplacement = 8f;
    private const float CombatAnchorMaxPlanarNavMeshDisplacement = 2f;
    private const float ChoreApproachMaxNavMeshDisplacement = 0.75f;
    private const float MinimumChoreStepSpacing = 2.5f;
    private const float MinimumChoreGroupCentroidSpacing = 8f;
    private const string ServiceRootName = "__CAMPAIGN_STATE";
    private const string SignatureName =
        "__CAMPAIGN_FOUNDATION_SIGNATURE_V5";
    private const string PreviousSignatureName =
        "__CAMPAIGN_FOUNDATION_SIGNATURE_V4";
    private const string PreviousV3SignatureName =
        "__CAMPAIGN_FOUNDATION_SIGNATURE_V3";
    private const string PreviousV2SignatureName =
        "__CAMPAIGN_FOUNDATION_SIGNATURE_V2";
    private const string LegacySignatureName =
        "__CAMPAIGN_FOUNDATION_SIGNATURE_V1";
    private const string SignaturePrefix =
        "__CAMPAIGN_FOUNDATION_SIGNATURE_";
    private const string InteractTag = "Interact";
    private const string InteractableLayerName = "Interactable";
    private const string SpawnVolumeLayerName = "Border";
    private const string PlayerLayerName = "Player";

    private static readonly string[] CampaignCompatibilityAssetPaths =
    {
        FarmNavMeshDataPath
    };

    private static readonly Vector3 LegacyBrokenFarmPickupPosition =
        new(66.084f, 1.87f, -68.398f);
    private static readonly Bounds LegacyInfestationSpawnVolumeWorldBounds =
        new(
            new Vector3(57.4f, 0.57f, -31.3f),
            new Vector3(75f, 1f, 125f));

    private const string FarmDirectorPath =
        "__CAMPAIGN_STRUCTURE/_CORE/FarmHubStateController (Component Pending)";
    private const string FarmCorePath =
        "__CAMPAIGN_STRUCTURE/_CORE";
    private const string FarmProloguePath =
        "__CAMPAIGN_STRUCTURE/_PROLOGUE_STATE";
    private const string FarmHubPath =
        "__CAMPAIGN_STRUCTURE/_HUB_STATE";
    private const string FarmObjectivesPath =
        "__CAMPAIGN_STRUCTURE/_PROLOGUE_STATE/Prologue Objectives";
    private const string FarmEnemiesPath =
        "__CAMPAIGN_STRUCTURE/_PROLOGUE_STATE/Prologue Enemies";
    private const string FarmDialoguePath =
        "__CAMPAIGN_STRUCTURE/_PROLOGUE_STATE/Prologue Dialogue";
    private const string FarmPrologueSpawnPath =
        "__CAMPAIGN_STRUCTURE/_PROLOGUE_STATE/Prologue Spawn";
    private const string FarmHubSpawnPath =
        "__CAMPAIGN_STRUCTURE/_HUB_STATE/Hub Spawn";
    private static readonly Vector3 FarmPrologueSpawnWorldPosition =
        new(71.22f, 2f, -65.02f);
    private static readonly Quaternion FarmPrologueSpawnWorldRotation =
        new(0f, -0.6795506f, 0f, 0.73362863f);
    private static readonly Vector3 FarmCurrentSafetyPlayerWorldPosition =
        new(69.15f, 2.14f, -63.31f);
    private static readonly Vector3 FarmInitialSafetyPlayerWorldPosition =
        new(61.779945f, 1.0699999f, -59.29054f);
    private static readonly Quaternion FarmSafetyPlayerWorldRotation =
        new(0f, -0.6795501f, 0f, 0.7336291f);
    private static readonly Vector3 FarmSafetyPlayerSourceCameraLocalPosition =
        new(0f, 0.809f, 0.013f);
    private static readonly Vector3 FarmSafetyPlayerSceneCameraLocalPosition =
        new(0f, 0.809f, 0.013f);
    private static readonly Vector3 FarmDuplicateSpawnWorldPosition =
        new(72.29361f, 1.0999982f, -64.818184f);
    private static readonly Quaternion FarmDuplicateSpawnWorldRotation =
        new(0f, -0.21848162f, 0f, 0.9758411f);
    private static readonly Vector3 FarmUnusedHubSpawnWorldPosition =
        new(73.28271f, 1.0999987f, -64.27895f);
    private static readonly Vector3 FarmSafetyWakePlayerWorldPosition =
        new(71.15f, 2f, -65.38f);
    private static readonly Vector3 FarmSafetyWakeViewWorldPosition =
        new(71.22f, 3.65f, -65.02f);
    private static readonly Vector3 FarmSafetyWakeLookWorldPosition =
        new(68.228775f, 3.65f, -64.79073f);
    private const string FarmTruckTravelPath =
        "__CAMPAIGN_STRUCTURE/_HUB_STATE/Truck Travel Point";
    private const string FarmLegacyCompletionTriggerPath =
        "__CAMPAIGN_STRUCTURE/_PROLOGUE_STATE/Complete Prologue Trigger";

    private const string FarmAuthoringSocketsName =
        "Farm Authoring Sockets";
    private const string FarmPropSocketsName =
        "Future Prop Sockets";
    private const string FarmPigSocketsName =
        "Future Domestic Pig Sockets";
    private const string WakePlayerAnchorName =
        "WAKE_Player_Anchor";
    private const string WakeViewAnchorName =
        "WAKE_View_Anchor";
    private const string WakeLookTargetName =
        "WAKE_Look_Target";
    private const string RumbleGroundOriginName =
        "RUMBLE_Ground_Origin";
    private const string RumbleAudioAnchorName =
        "RUMBLE_Audio_Anchor";
    private const string RumbleCameraAnchorName =
        "RUMBLE_Camera_Anchor";
    private const string EnemyEmergenceRootName =
        "Enemy Emergence";
    private const string EmergencePresentationName =
        "Emergence Presentation";
    private const string CombatBoundsName =
        "COMBAT_Area_Bounds";
    private const string CombatPlayerAnchorName =
        "COMBAT_Player_Anchor";
    private const string PropSocketName =
        "PROP_SOCKET";
    private const string PlayerApproachName =
        "PLAYER_APPROACH";
    private const string HubTruckSocketName =
        "PROP_SOCKET_Truck";
    private const string HubTruckApproachName =
        "PLAYER_APPROACH_Truck";
    private const string OutdatedSceneFileName =
        "OutDated Level.unity";

    private const string OpenWorldRootName = "Bloodroot_OpenWorld";
    private const string OpenWorldCorePath =
        "Bloodroot_OpenWorld/_CORE";
    private const string OpenWorldProgressionPath =
        "Bloodroot_OpenWorld/_CORE/OpenWorldProgressionManager (Component Pending)";
    private const string OpenWorldArrivalPath =
        "Bloodroot_OpenWorld/AREA_00_BLACK_PINES_FOREST/World Arrival Spawn";
    private const string LegacyOpenWorldReturnTruckName =
        "Return Truck (Travel Wiring Pending)";
    private const string OpenWorldTravelBackendName =
        "Open World Safety Extraction Backend";
    private const string OpenWorldTravelBackendPath =
        OpenWorldCorePath + "/" + OpenWorldTravelBackendName;
    private const string AlphaWorldCompletionTravelRootPrefix =
        "__ALPHA_WORLD_COMPLETION_TRAVEL_V";
    private const string AlphaWorldEvidenceVisualRootName =
        "__AREA_EVIDENCE_VISUALS_V1";
    private const string AlphaWorldMissionOwnedRootName =
        "__ALPHA_WORLD_MISSIONS_V3";
    private static readonly Vector3 OpenWorldSafetyTruckWorldPosition =
        new(-362f, 4.1279283f, -142f);
    private static readonly Quaternion OpenWorldSafetyTruckWorldRotation =
        Quaternion.identity;

    private static readonly string[] OpenWorldMissionSystemNames =
    {
        "Black Pines Mission Systems",
        "Stillwater Mission Systems",
        "Harrow Estate Mission Systems",
        "Bloodroot Hollow Boss Systems"
    };

    private static readonly CampaignAreaId[] OpenWorldMissionAreaIds =
    {
        CampaignAreaId.BlackPines,
        CampaignAreaId.StillwaterFeedMill,
        CampaignAreaId.HarrowEstate,
        CampaignAreaId.BloodrootHollow
    };

    private static readonly string[] OpenWorldRegionalSpawnPrefabPaths =
    {
        SafetyBlackPinesSpawnPrefabPath,
        SafetyStillwaterSpawnPrefabPath,
        SafetyHarrowSpawnPrefabPath,
        SafetyHollowSpawnPrefabPath
    };

    private static readonly string[] OpenWorldRegionalSpawnPrefabGuids =
    {
        SafetyBlackPinesSpawnPrefabGuid,
        SafetyStillwaterSpawnPrefabGuid,
        SafetyHarrowSpawnPrefabGuid,
        SafetyHollowSpawnPrefabGuid
    };

    private static readonly string[] SafetyItemDatabasePrefabGuids =
    {
        "225889327b7da874f8e79c1349a58c05",
        "cdf063b6c42ba3d439d49b44121b7832",
        "fba98a1dbc550e34fbc149aaa5ffdfb9",
        "fe10ccb70f42ec8488781f3de931ea06",
        "2bc31b8eae8adf9418d8a3af10c91310",
        "3007f4358733b934d9c6ff4468631649",
        "8b3610a9ff3a5ba4abacdd05a4c8309b",
        "6af61e0e80d39fe44a5e2d4296702f64",
        "400ebf9b7a3d1ba4a942981fc7f7565b",
        "bd9eb11ea6c815b4c8f22a4fc4f33e4f",
        "ce01f257e99964e4ca039fc23981034b",
        "df2ec1ccc28639c4bb65e529d18d6f1e",
        "c046b1c544b14df4081af4fcfa7f89be",
        "10f052bb3168cca4e9b79c51d831fca3"
    };

    private static readonly long[] SafetyItemDatabaseItemFileIds =
    {
        3198416994513444431L,
        7177601013948742704L,
        5040439880305407109L,
        7844390654222002915L,
        3609841578841610030L,
        7248613269753264216L,
        3501958250119339175L,
        7252916769366795250L,
        1250336663616833586L,
        6593283876892700520L,
        6185135373245803667L,
        5262651151442942561L,
        2097986040270169525L,
        4822712193961387962L
    };

    private static readonly string[] SafetyItemDatabaseItemIds =
    {
        "Box084378289",
        "Stick824358783",
        "Cursed Item321580235087",
        "Cursed Item1213580235",
        "Iron Ore24658092365",
        "Car Key2358932",
        "Leather4634576",
        "M1 Garand57457457",
        "M1 Garand Ammo5736854368",
        "Radar5474357346",
        "Scrap Metal3546547457",
        "Stone4574573547",
        "SuperHeavy13242135234",
        string.Empty
    };

    private static readonly string[] OpenWorldRegionalSpawnSourceNames =
    {
        "PlayerSpawnPosBlackPines",
        "PlayerSpawnPosStillwater",
        "PlayerSpawnPosHarrowEstate",
        "PlayerSpawnPosBloodRootHollow"
    };

    private static readonly string[] OpenWorldRegionalArrivalNames =
    {
        "RESPAWN_SOCKET_Black_Pines",
        "RESPAWN_SOCKET_Stillwater",
        "RESPAWN_SOCKET_Harrow_Estate",
        "RESPAWN_SOCKET_Bloodroot_Hollow"
    };

    private static readonly Vector2[] OpenWorldRegionalArrivalPoints =
    {
        new(-385f, -164f),
        new(397f, -548f),
        new(40f, 285f),
        new(50f, 530f)
    };

    private static readonly Vector2[] OpenWorldRegionalArrivalLookTargets =
    {
        new(-355f, -140f),
        new(430f, -525f),
        new(21f, 325f),
        new(53f, 546f)
    };

    private static readonly Vector3 MixedSafetyPlayerSourcePosition =
        new(-13.7761f, 8.07057f, -167.19273f);
    private static readonly Quaternion MixedSafetyPlayerSourceRotation =
        new(0f, -0.6795506f, 0f, 0.73362863f);
    private static readonly Vector3 MixedRegionalSpawnSourcePosition =
        new(-12.302977f, 8.022442f, -169.29488f);

    private static readonly string[] ChoreGroupNames =
    {
        "CHORE_Feed_Pigs",
        "CHORE_Muck_Stalls",
        "CHORE_Check_Water"
    };

    private static readonly string[] LegacyChoreIds =
    {
        "feed_pigs",
        "muck_stalls",
        "check_water"
    };

    private static readonly string[] LegacyChoreObjectives =
    {
        "Feed the pigs.",
        "Muck the pig stalls.",
        "Check the animals' water."
    };

    private const float TerrainGroundTolerance = 0.02f;

    private static readonly Vector2[] LegacyV4ChoreWorldXZ =
    {
        new(50.5f, 8.5f),
        new(45.5f, 8.5f),
        new(48f, 13f)
    };

    private readonly struct ChoreStepDefinition
    {
        public ChoreStepDefinition(
            string groupName,
            string name,
            string id,
            string objective,
            Vector2 worldXZ,
            Vector2 approachWorldXZ,
            Quaternion rotation,
            Vector3 scale,
            Vector3 colliderCenter,
            Vector3 colliderSize)
        {
            GroupName = groupName;
            Name = name;
            Id = id;
            Objective = objective;
            WorldXZ = worldXZ;
            ApproachWorldXZ = approachWorldXZ;
            Rotation = rotation;
            Scale = scale;
            ColliderCenter = colliderCenter;
            ColliderSize = colliderSize;
        }

        public string GroupName { get; }
        public string Name { get; }
        public string Id { get; }
        public string Objective { get; }
        public Vector2 WorldXZ { get; }
        public Vector2 ApproachWorldXZ { get; }
        public Quaternion Rotation { get; }
        public Vector3 Scale { get; }
        public Vector3 ColliderCenter { get; }
        public Vector3 ColliderSize { get; }
    }

    private static readonly Vector3 WednesdayMuckGroupLocalPosition =
        new(12.25f, 0f, -17.25f);

    private static readonly ChoreStepDefinition[] ChoreSteps =
    {
        new(
            ChoreGroupNames[0],
            "STEP_01_Collect_Feed_Scoop",
            "feed_collect_scoop",
            "Feed the pigs (1/3): Take a feed scoop from the bin.",
            new Vector2(67f, 10.25f),
            new Vector2(67f, 8.25f),
            Quaternion.identity,
            new Vector3(1f, 0.88541f, 1f),
            new Vector3(0f, 0.8f, 0f),
            new Vector3(2f, 1.6f, 2f)),
        new(
            ChoreGroupNames[0],
            "STEP_02_Fill_South_Trough",
            "feed_fill_south_trough",
            "Feed the pigs (2/3): Head southeast to the first feed trough and fill it.",
            new Vector2(69.735275f, 7.808823f),
            new Vector2(69.735275f, 5.808823f),
            new Quaternion(0f, 0.17638414f, 0f, 0.9843214f),
            Vector3.one,
            new Vector3(0f, 0.6f, 0f),
            new Vector3(2.8f, 1.2f, 1.6f)),
        new(
            ChoreGroupNames[0],
            "STEP_03_Fill_North_Trough",
            "feed_fill_north_trough",
            "Feed the pigs (3/3): Head east to the second feed trough and fill it.",
            new Vector2(79.02911f, 4.2839146f),
            new Vector2(81.02911f, 4.2839146f),
            new Quaternion(0f, 0.18671878f, 0f, 0.9824134f),
            Vector3.one,
            new Vector3(0f, 0.6f, 0f),
            new Vector3(2.8f, 1.2f, 1.6f)),
        new(
            ChoreGroupNames[1],
            "STEP_04_Clear_East_Stall",
            "muck_clear_east_stall",
            "Muck the stalls (1/3): Head north to the eastern pig stall and clear it.",
            new Vector2(80.100006f, 6.936747f),
            new Vector2(82.100006f, 6.936747f),
            new Quaternion(0f, 0.17783736f, 0f, 0.9840599f),
            new Vector3(1.4968368f, 1.7673154f, 1.8228465f),
            new Vector3(0f, 0.6f, 0f),
            new Vector3(2.4f, 1.2f, 2.4f)),
        new(
            ChoreGroupNames[1],
            "STEP_05_Clear_West_Stall",
            "muck_clear_west_stall",
            "Muck the stalls (2/3): Head west to the western pig stall and clear it.",
            new Vector2(70.182236f, 10.965786f),
            new Vector2(72.182236f, 10.965786f),
            new Quaternion(0f, 0.18167894f, 0f, 0.9833579f),
            new Vector3(1.4674368f, 1.3766f, 1.7861193f),
            new Vector3(0f, 0.6f, 0f),
            new Vector3(2.4f, 1.2f, 2.4f)),
        new(
            ChoreGroupNames[1],
            "STEP_06_Dump_Muck_Wheelbarrow",
            "muck_dump_waste",
            "Muck the stalls (3/3): Empty the wheelbarrow at the muck heap.",
            new Vector2(90.75f, 0f),
            new Vector2(92.75f, 0f),
            Quaternion.identity,
            Vector3.one,
            new Vector3(0f, 0.7f, 0f),
            new Vector3(2.4f, 1.4f, 2.4f)),
        new(
            ChoreGroupNames[2],
            "STEP_07_Prime_Livestock_Pump",
            "water_prime_pump",
            "Check the water (1/2): Prime the livestock pump.",
            new Vector2(63.044678f, 21.572998f),
            new Vector2(63.044678f, 19.572998f),
            new Quaternion(0f, 0.21740729f, 0f, 0.97608095f),
            new Vector3(1.2945f, 1.998f, 4.0776114f),
            new Vector3(0f, 0.9f, 0f),
            new Vector3(1.6f, 1.8f, 1.6f)),
        new(
            ChoreGroupNames[2],
            "STEP_08_Open_Trough_Valve",
            "water_open_trough_valve",
            "Check the water (2/2): Open the trough valve.",
            new Vector2(68.24361f, 16.849363f),
            new Vector2(70.24361f, 16.849363f),
            new Quaternion(0f, -0.97024566f, 0f, 0.2421227f),
            Vector3.one,
            new Vector3(0f, 0.6f, 0f),
            new Vector3(2.8f, 1.2f, 1.6f))
    };

    private static readonly Vector2[] ChoreStepWorldXZ =
        ChoreSteps.Select(step => step.WorldXZ).ToArray();

    private static readonly Vector2[] ChoreApproachWorldXZ =
        ChoreSteps.Select(step => step.ApproachWorldXZ).ToArray();

    private static readonly Vector2[] FarmPropSocketWorldXZ =
    {
        ChoreSteps[1].WorldXZ,
        ChoreSteps[3].WorldXZ,
        ChoreSteps[6].WorldXZ
    };

    // V3 authored these exact fixed heights. They remain accepted only during
    // transactional V3 preflight so the current builder can repair the scene.
    private static readonly Vector3[] LegacyV3ChoreWorldPositions =
    {
        new(50.5f, 6.25f, 8.5f),
        new(45.5f, 6.25f, 8.5f),
        new(48f, 6.25f, 13f)
    };

    private static readonly string[] FarmPropSocketNames =
    {
        "PROP_SOCKET_Feed_Trough",
        "PROP_SOCKET_Mucking_Area",
        "PROP_SOCKET_Water_Station"
    };

    private static readonly string[] FarmPigSocketNames =
    {
        "PIG_SOCKET_01",
        "PIG_SOCKET_02",
        "PIG_SOCKET_03",
        "PIG_SOCKET_04"
    };

    private static readonly Vector2[] FarmPigSocketWorldXZ =
    {
        new(54.5f, 4.5f),
        new(58f, 4f),
        new(55f, 10.5f),
        new(59f, 11.5f)
    };

    private static readonly Vector2[] LegacyV4PigSocketWorldXZ =
    {
        new(48.5f, 6f),
        new(50.5f, 5.5f),
        new(52.5f, 6.25f),
        new(50f, 14.5f)
    };

    private static readonly Vector3[] LegacyV3PigSocketWorldPositions =
    {
        new(48.5f, 6.25f, 6f),
        new(50.5f, 6.25f, 5.5f),
        new(52.5f, 6.25f, 6.25f),
        new(50f, 6.25f, 14.5f)
    };

    private static readonly string[] EmergenceZoneNames =
    {
        "EMERGENCE_ZONE_01",
        "EMERGENCE_ZONE_02",
        "EMERGENCE_ZONE_03"
    };

    private const string AlphaEmergenceVisualRootName =
        "Generated Alpha Infected Ground";

    [MenuItem("Tools/Bloodroot/Campaign Foundation/Build or Rebuild")]
    public static void BuildOrRebuild()
    {
        RunBuild(showDialog: true);
    }

    /// <summary>
    /// Non-interactive entry point for CI and command-line validation. Unity
    /// exits with a failure when the transactional build or its validators do
    /// not complete successfully.
    /// </summary>
    public static void BuildOrRebuildBatch()
    {
        if (!RunBuild(showDialog: false))
        {
            throw new InvalidOperationException(
                "Campaign Foundation batch build failed. See the Editor log " +
                "for the original exception and rollback result.");
        }
    }

    [MenuItem("Tools/Bloodroot/Campaign Foundation/Validate")]
    public static void ValidateMenu()
    {
        try
        {
            RequireNoDirtyLoadedScenes();
            ValidateProject(openScenesWhenNeeded: true);
            EditorUtility.DisplayDialog(
                "Campaign Foundation",
                "Farm prologue/hub and open-world campaign wiring passed validation.",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Campaign Foundation Validation Failed",
                exception.Message,
                "OK");
        }
    }

    /// <summary>
    /// Non-interactive standalone validator for CI and command-line checks.
    /// </summary>
    public static void ValidateBatch()
    {
        RequireNoDirtyLoadedScenes();
        ValidateProject(openScenesWhenNeeded: true);
        Debug.Log("Campaign Foundation standalone batch validation passed.");
    }

    private readonly struct AssetFileSnapshot
    {
        public AssetFileSnapshot(string assetPath)
        {
            AssetPath = assetPath;
            AssetBytes = ReadFileBytesIfPresent(ToAbsolutePath(assetPath));
            MetaBytes = ReadFileBytesIfPresent(
                ToAbsolutePath(assetPath + ".meta"));
        }

        public string AssetPath { get; }
        public byte[] AssetBytes { get; }
        public byte[] MetaBytes { get; }
    }

    private readonly struct SafetyTruckExtractionBinding
    {
        public SafetyTruckExtractionBinding(
            GameObject visualRoot,
            lockedExtraction interaction,
            BoxCollider solidCollider)
        {
            VisualRoot = visualRoot;
            Interaction = interaction;
            SolidCollider = solidCollider;
        }

        public GameObject VisualRoot { get; }
        public lockedExtraction Interaction { get; }
        public BoxCollider SolidCollider { get; }
    }

    private static bool RunBuild(bool showDialog)
    {
        byte[] farmBytes = null;
        byte[] openWorldBytes = null;
        byte[] buildSettingsBytes = null;
        AssetFileSnapshot[] compatibilityAssetSnapshots = null;
        SceneSetup[] originalSetup = null;
        EditorBuildSettingsScene[] originalBuildSettings = null;
        bool transactionStarted = false;

        try
        {
            RequireNoDirtyLoadedScenes();
            RequireAsset(MainMenuScenePath);
            RequireAsset(FarmScenePath);
            RequireAsset(OpenWorldScenePath);
            EnsureBackupPair();

            farmBytes = File.ReadAllBytes(ToAbsolutePath(FarmScenePath));
            bool isExactNewFarmVisualMigration = string.Equals(
                ComputeSha256(farmBytes),
                NewFarmVisualMigrationSha256,
                StringComparison.Ordinal);
            openWorldBytes =
                File.ReadAllBytes(ToAbsolutePath(OpenWorldScenePath));
            bool isExactSafetyOpenWorldMigration = string.Equals(
                ComputeSha256(openWorldBytes),
                SafetyOpenWorldMigrationSha256,
                StringComparison.Ordinal);
            buildSettingsBytes =
                File.ReadAllBytes(ToAbsolutePath(EditorBuildSettingsPath));
            compatibilityAssetSnapshots = CampaignCompatibilityAssetPaths
                .Select(path => new AssetFileSnapshot(path))
                .ToArray();
            originalSetup = EditorSceneManager.GetSceneManagerSetup();
            originalBuildSettings = EditorBuildSettings.scenes
                .Select(scene => new EditorBuildSettingsScene(
                    scene.path,
                    scene.enabled))
                .ToArray();
            transactionStarted = true;

            EnsureReleaseBuildSettings();
            ValidateReleaseBuildSettings();

            // Open the Farm in isolation so a one-time NavMesh repair cannot
            // collect collider geometry from another loaded scene. The exact
            // original editor setup is restored at commit or rollback.
            Scene farmScene = EditorSceneManager.OpenScene(
                FarmScenePath,
                OpenSceneMode.Single);
            ValidateFarmSafetyPlayerAndSpawnMigrationState(farmScene);
            NormalizeFarmSafetySpawnAuthority(farmScene);
            if (isExactNewFarmVisualMigration)
            {
                NormalizeExactUnusedFarmHubSpawnMarker(farmScene);
            }
            RefreshFarmSafetyPlayerBindingsForPreflight(farmScene);
            RepairMissingFarmNavMeshDataIfRecognized(farmScene, farmBytes);
            Scene openWorldScene = OpenTargetScene(OpenWorldScenePath);

            ValidateOpenWorldPlayerAndSpawnMigrationState(openWorldScene);
            DisableNestedPlayerServiceBehaviours(
                RequireTaggedObject(farmScene, "Player").transform);
            DisableRecognizedFarmWorktableInputAuthority(farmScene);
            foreach (GameObject playerRoot in FindOpenWorldPlayerPrefabRoots(
                         openWorldScene))
            {
                DisableNestedPlayerServiceBehaviours(playerRoot.transform);
            }

            // Safety's Player can inherit legacy controller/input services from
            // its nested visual prefab. Normalize only those nested copies
            // before strict campaign validation so the protected root services
            // remain the sole gameplay authorities.
            if (isExactNewFarmVisualMigration)
            {
                ValidateExactNewFarmVisualMigrationShell(farmScene);
            }
            else
            {
                PreflightRecognizedOrEmpty(farmScene);
            }
            if (isExactSafetyOpenWorldMigration)
            {
                ValidateExactSafetyOpenWorldMigrationShell(openWorldScene);
            }
            else
            {
                PreflightRecognizedOrEmpty(openWorldScene);
            }

            EnsureSafetyPlayerInstance(
                farmScene,
                RequirePath(farmScene, FarmPrologueSpawnPath).transform);
            GameObject openWorldPlayer = EnsureSafetyPlayerInstance(
                openWorldScene,
                RequirePath(openWorldScene, OpenWorldArrivalPath).transform);
            NormalizeOpenWorldSafetySpawnMarkers(openWorldScene);
            ValidateNormalizedOpenWorldPlayerBeforeWiring(
                openWorldScene,
                openWorldPlayer);

            WireFarm(farmScene);
            WireOpenWorld(openWorldScene, openWorldPlayer);

            ValidateFarm(farmScene);
            ValidateOpenWorld(openWorldScene);

            if (!EditorSceneManager.SaveScene(farmScene, FarmScenePath) ||
                !EditorSceneManager.SaveScene(
                    openWorldScene,
                    OpenWorldScenePath))
            {
                throw new InvalidOperationException(
                    "Unity could not save both campaign scenes.");
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            RestoreSceneSetup(originalSetup);
            ValidateProject(openScenesWhenNeeded: true);

            Debug.Log(
                "Campaign foundation built: Farm prologue/hub, authored " +
                "chores/fade, persistent travel, and open-world barriers passed validation.");

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Campaign Foundation",
                    "Build/Rebuild completed and passed validation.",
                    "OK");
            }

            return true;
        }
        catch (Exception exception)
        {
            bool rollbackSucceeded = !transactionStarted ||
                                     RestoreSceneBytes(
                                         farmBytes,
                                         openWorldBytes,
                                         originalSetup,
                                         originalBuildSettings,
                                         buildSettingsBytes,
                                         compatibilityAssetSnapshots);

            Debug.LogException(exception);

            string rollbackText = !transactionStarted
                ? "The transaction did not begin; target scene files and " +
                  "Build Settings were not changed."
                : rollbackSucceeded
                    ? "Both target scene files, Build Settings, and campaign " +
                      "compatibility assets were restored."
                    : "Automatic rollback was incomplete; use the immutable " +
                      "pre-campaign backups.";

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Campaign Foundation Build Failed",
                    exception.Message + "\n\n" + rollbackText,
                    "OK");
            }

            return false;
        }
    }

    private static void WireFarm(Scene scene)
    {
        ValidateSafetyItemDatabaseSource(scene);
        RemoveExactLegacyFarmItemOverride(scene);
        NormalizeExactOwnedFarmCursedPresentations(scene);

        CampaignStateService state = EnsureRootService(scene);
        EnsureLoadoutEquipmentBridge(
            scene,
            state,
            state.GetComponent<CampaignInventoryCarryover>());
        GameObject directorObject = RequirePath(scene, FarmDirectorPath);
        FarmPrologueDirector director =
            EnsureComponent<FarmPrologueDirector>(directorObject);

        GameObject prologueRoot = RequirePath(scene, FarmProloguePath);
        GameObject hubRoot = RequirePath(scene, FarmHubPath);
        GameObject coreRoot = RequirePath(scene, FarmCorePath);
        GameObject objectivesRoot = RequirePath(scene, FarmObjectivesPath);
        GameObject enemiesRoot = RequirePath(scene, FarmEnemiesPath);
        GameObject dialogueRoot = RequirePath(scene, FarmDialoguePath);
        Transform prologueSpawn =
            RequirePath(scene, FarmPrologueSpawnPath).transform;
        Transform hubSpawn = RequirePath(scene, FarmHubSpawnPath).transform;
        Transform player = RequireTaggedObject(scene, "Player").transform;
        DisableNestedPlayerServiceBehaviours(player);

        waveManager encounter = RequireSingleComponent<waveManager>(scene);
        MobSpawner spawner = RequireSingleComponent<MobSpawner>(scene);
        GameObject[] campaignEnemyRoster = RequireFarmBoarRoster();
        ConfigureFarmEnemyRoster(spawner, campaignEnemyRoster);
        ConfigureFarmHogHuntIntro(encounter, enabled: false);
        Inventory inventory =
            RequireTaggedPlayerRootComponent<Inventory>(scene);
        playerController movement =
            RequireTaggedPlayerRootComponent<playerController>(scene);
        Interact interaction = RequireSingleEnabledPlayerInteract(scene);
        cameraController cameraLook =
            RequireSingleComponent<cameraController>(scene);
        RefreshCurrentSafetyTruckInstanceIfStale(scene);
        SafetyTruckExtractionBinding safetyTruck =
            RequireSafetyTruckExtractionBinding(scene);
        EnsureSafetyTruckKey(scene, hubRoot.transform);
        Transform legacySpawn =
            RequireTaggedObject(scene, "PlayerSpawnPos").transform;
        Terrain terrain = RequireSingleComponent<Terrain>(scene);
        ValidateFarmTerrain(terrain);
        Vector3[] choreWorldPositions =
            GetTerrainGroundPositions(terrain, ChoreStepWorldXZ);
        Vector3[] choreApproachWorldPositions =
            GetTerrainGroundPositions(terrain, ChoreApproachWorldXZ);
        Vector3[] propSocketWorldPositions =
            GetTerrainGroundPositions(terrain, FarmPropSocketWorldXZ);
        Vector3[] pigSocketWorldPositions =
            GetTerrainGroundPositions(terrain, FarmPigSocketWorldXZ);

        GameObject wakeRoot = EnsureDirectChild(dialogueRoot, "Wake Up Sequence");
        GameObject rumbleRoot =
            EnsureDirectChild(dialogueRoot, "Ground Rumble Sequence");

        // Prologue and Hub are intentionally distinct authored arrivals. The
        // scene-local tagged fallback follows the current prologue phase so
        // Safety's default-order player Start converges on the same pose.
        if (hubSpawn.GetComponent<CampaignSpawnPoint>() == null)
        {
            // One-time migration from the untouched Farm: its tagged legacy
            // fallback is the authored Hub arrival. Once the component exists,
            // the Hub marker itself is authoritative on every no-op rebuild.
            SetWorldPoseIfDifferent(hubSpawn, legacySpawn);
        }

        SetWorldPoseIfDifferent(legacySpawn, prologueSpawn);

        prologueRoot.SetActive(true);
        hubRoot.SetActive(false);
        enemiesRoot.SetActive(true);
        objectivesRoot.SetActive(true);
        dialogueRoot.SetActive(true);

        EnsureWakeAuthoring(scene, wakeRoot);
        EnsureFutureFarmContentSockets(
            coreRoot,
            propSocketWorldPositions,
            pigSocketWorldPositions);

        FarmChoreInteractable[] chores = EnsureChores(
            objectivesRoot,
            director,
            terrain,
            choreWorldPositions,
            choreApproachWorldPositions);

        EnsureCombatAuthoring(
            scene,
            enemiesRoot,
            rumbleRoot,
            spawner,
            encounter,
            prologueSpawn,
            choreApproachWorldPositions[
                choreApproachWorldPositions.Length - 1]);
        EnsureRumblePresentation(
            scene,
            rumbleRoot,
            director,
            cameraLook);

        CanvasGroup screenFader;
        FarmObjectivePresenter objectivePresenter;
        EnsureAuthoredFarmPresentation(
            scene,
            director,
            out screenFader,
            out objectivePresenter);

        director.ConfigureCampaign(
            state,
            player,
            legacySpawn,
            prologueSpawn,
            hubSpawn);
        director.ConfigureStateRoots(
            prologueRoot,
            hubRoot,
            wakeRoot,
            objectivesRoot,
            rumbleRoot);
        director.ConfigureEncounter(
            encounter,
            encounter.gameObject,
            spawner.gameObject);
        director.ConfigureChores(inventory, chores);
        director.ConfigureChoreOrder(true);
        director.ConfigurePlayerControl(
            new Behaviour[] { movement, interaction, cameraLook });
        director.ConfigureScreenFader(screenFader);
        director.ConfigureAutomaticTiming(true, 2f, false, 4f);
        EditorUtility.SetDirty(director);

        gameManager manager = RequireSingleComponent<gameManager>(scene);
        manager.player = player.gameObject;
        manager.playerController = movement;
        manager.playerSpawnPos = legacySpawn.gameObject;
        EditorUtility.SetDirty(manager);

        objectivePresenter.Configure(
            director,
            RequireNamedText(scene, "Game Goal Label"),
            RequireNamedText(scene, "Game Goal Data"));
        EditorUtility.SetDirty(objectivePresenter);

        foreach (InfestationSpawner infestation in
                 FindSceneComponents<InfestationSpawner>(scene))
        {
            infestation.enabled = false;
            EditorUtility.SetDirty(infestation);
        }

        DisableLegacyCompletionTrigger(scene);
        encounter.gameObject.SetActive(false);
        spawner.gameObject.SetActive(false);
        prologueRoot.SetActive(true);
        hubRoot.SetActive(false);
        // ConfigureStateRoots applies the director's non-serialized Inactive
        // phase while authoring. Restore the persistent chore environment only
        // after every director configuration call so Foundation and Alpha Hub
        // serialize the same fresh-prologue state on every rerun.
        EnsurePersistentFarmChoreEnvironmentAuthoredActive(objectivesRoot);

        CampaignSpawnPoint hubArrival =
            EnsureComponent<CampaignSpawnPoint>(hubSpawn.gameObject);
        hubArrival.Configure("FarmHub", hubSpawn, true);
        EditorUtility.SetDirty(hubArrival);

        GameObject travelPoint = RequirePath(scene, FarmTruckTravelPath);
        travelPoint.transform.SetPositionAndRotation(
            safetyTruck.VisualRoot.transform.position,
            safetyTruck.VisualRoot.transform.rotation);
        CampaignSceneTravel farmTravel = ConfigureTravelBackend(
            travelPoint,
            CampaignSceneNames.OpenWorld,
            "BlackPinesArrival",
            true,
            CampaignAreaId.BlackPines);
        EnsureHubTravelSockets(travelPoint);
        ConfigureSafetyExtractionMenu(
            scene,
            farmTravel,
            "Travel to Open World",
            SafetyExtractionMenuOption.BlackPines);

        EnsureSignature(scene);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static bool EnsurePersistentFarmChoreEnvironmentAuthoredActive(
        GameObject objectivesRoot)
    {
        if (objectivesRoot == null)
        {
            throw new InvalidOperationException(
                "Farm Prologue Objectives root is missing.");
        }

        if (objectivesRoot.activeSelf)
            return false;

        objectivesRoot.SetActive(true);
        EditorUtility.SetDirty(objectivesRoot);
        return true;
    }

    internal static bool
        EnsurePersistentFarmChoreEnvironmentAuthoredActiveForTests(
            GameObject objectivesRoot)
    {
        return EnsurePersistentFarmChoreEnvironmentAuthoredActive(
            objectivesRoot);
    }

    private static GameObject RequireExactSafetyTruckPrefab()
    {
        if (!string.Equals(
                AssetDatabase.AssetPathToGUID(SafetyTruckEscapePrefabPath),
                SafetyTruckEscapePrefabGuid,
                StringComparison.Ordinal) ||
            !string.Equals(
                AssetDatabase.AssetPathToGUID(SafetyTruckKeyPrefabPath),
                SafetyTruckKeyPrefabGuid,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Online-safety truck or truck-key prefab identity changed; " +
                "the campaign cannot safely bind the extraction interaction.");
        }

        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(
            SafetyTruckEscapePrefabPath);
        lockedExtraction[] interactions = source != null
            ? source.GetComponents<lockedExtraction>()
            : Array.Empty<lockedExtraction>();
        BoxCollider[] colliders = source != null
            ? source.GetComponents<BoxCollider>()
            : Array.Empty<BoxCollider>();
        lockedExtraction interaction = interactions.Length == 1
            ? interactions[0]
            : null;
        BoxCollider collider = colliders.Length == 1
            ? colliders[0]
            : null;
        GlobalObjectId interactionId = interaction != null
            ? GlobalObjectId.GetGlobalObjectIdSlow(interaction)
            : default;
        GlobalObjectId colliderId = collider != null
            ? GlobalObjectId.GetGlobalObjectIdSlow(collider)
            : default;
        SerializedProperty keyProperty = interaction != null
            ? new SerializedObject(interaction).FindProperty("key")
            : null;
        UnityEngine.Object keyObject = keyProperty?.objectReferenceValue;
        GlobalObjectId keyId = keyObject != null
            ? GlobalObjectId.GetGlobalObjectIdSlow(keyObject)
            : default;

        if (source == null ||
            PrefabUtility.GetPrefabAssetType(source) ==
                PrefabAssetType.NotAPrefab ||
            source.name != "TruckEscapeEnding" ||
            !source.activeSelf ||
            source.layer != RequireInteractableLayer() ||
            !source.CompareTag(InteractTag) ||
            interaction == null ||
            !interaction.enabled ||
            interactionId.targetObjectId !=
                SafetyLockedExtractionSourceFileId ||
            source.GetComponentsInChildren<MonoBehaviour>(true)
                .Count(component => component is IInteract) != 1 ||
            source.GetComponentsInChildren<MonoBehaviour>(true)
                .Single(component => component is IInteract) != interaction ||
            collider == null ||
            !collider.enabled ||
            collider.isTrigger ||
            !Approximately(collider.center, new Vector3(0f, 1.5f, 0f)) ||
            !Approximately(collider.size, new Vector3(4f, 1.5f, 5f)) ||
            colliderId.targetObjectId != SafetyTruckRootColliderSourceFileId ||
            keyObject == null ||
            !string.Equals(
                AssetDatabase.GetAssetPath(keyObject),
                SafetyTruckKeyPrefabPath,
                StringComparison.Ordinal) ||
            keyId.targetObjectId != SafetyTruckKeyItemSourceFileId)
        {
            throw new InvalidOperationException(
                "The exact online-safety truck prefab no longer exposes its " +
                "enabled lockedExtraction, solid collider, Interact " +
                "identity, or exact Car Key contract.");
        }

        return source;
    }

    private static SafetyTruckExtractionBinding
        RequireSafetyTruckExtractionBinding(Scene scene)
    {
        RequireExactSafetyTruckPrefab();

        if (FindSceneComponents<TruckEscapeEnding>(scene).Length != 0)
        {
            throw new InvalidOperationException(
                "The current online-safety truck lineage must not retain the " +
                "removed TruckEscapeEnding component.");
        }

        lockedExtraction[] interactions =
            FindSceneComponents<lockedExtraction>(scene);
        if (interactions.Length != 1 || interactions[0] == null)
        {
            throw new InvalidOperationException(
                $"Scene '{scene.name}' requires exactly one online-safety " +
                "lockedExtraction " +
                "component on the retained truck visual.");
        }

        lockedExtraction interaction = interactions[0];
        GameObject visualRoot =
            PrefabUtility.GetOutermostPrefabInstanceRoot(
                interaction.gameObject);
        string prefabPath = visualRoot != null
            ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(visualRoot)
            : string.Empty;
        lockedExtraction sourceInteraction =
            PrefabUtility.GetCorrespondingObjectFromSource(interaction);
        GlobalObjectId sourceInteractionId = sourceInteraction != null
            ? GlobalObjectId.GetGlobalObjectIdSlow(sourceInteraction)
            : default;

        if (visualRoot == null ||
            visualRoot != interaction.gameObject ||
            visualRoot.name != "TruckEscapeEnding" ||
            !visualRoot.activeInHierarchy ||
            !string.Equals(
                prefabPath,
                SafetyTruckEscapePrefabPath,
                StringComparison.Ordinal) ||
            !string.Equals(
                AssetDatabase.AssetPathToGUID(prefabPath),
                SafetyTruckEscapePrefabGuid,
                StringComparison.Ordinal) ||
            sourceInteraction == null ||
            !string.Equals(
                AssetDatabase.GetAssetPath(sourceInteraction),
                SafetyTruckEscapePrefabPath,
                StringComparison.Ordinal) ||
            sourceInteractionId.targetObjectId !=
            SafetyLockedExtractionSourceFileId)
        {
            throw new InvalidOperationException(
                $"Scene '{scene.name}' extraction interaction is not the " +
                "exact component on " +
                "the current online-safety truck prefab root.");
        }

        BoxCollider[] colliders = visualRoot.GetComponents<BoxCollider>();
        if (colliders.Length != 1)
        {
            throw new InvalidOperationException(
                "Online-safety truck root must retain exactly one physical " +
                "BoxCollider for extraction interaction and vehicle collision.");
        }

        BoxCollider collider = colliders[0];
        BoxCollider sourceCollider =
            PrefabUtility.GetCorrespondingObjectFromSource(collider);
        GlobalObjectId sourceColliderId = sourceCollider != null
            ? GlobalObjectId.GetGlobalObjectIdSlow(sourceCollider)
            : default;

        if (!collider.enabled ||
            collider.isTrigger ||
            !Approximately(collider.center, new Vector3(0f, 1.5f, 0f)) ||
            !Approximately(collider.size, new Vector3(4f, 1.5f, 5f)) ||
            sourceCollider == null ||
            !string.Equals(
                AssetDatabase.GetAssetPath(sourceCollider),
                SafetyTruckEscapePrefabPath,
                StringComparison.Ordinal) ||
            sourceColliderId.targetObjectId !=
            SafetyTruckRootColliderSourceFileId)
        {
            throw new InvalidOperationException(
                "Online-safety truck physical collider no longer matches the " +
                "exact retained solid-collision contract.");
        }

        SerializedProperty keyProperty =
            new SerializedObject(interaction).FindProperty("key");
        UnityEngine.Object keyObject = keyProperty?.objectReferenceValue;
        GlobalObjectId keyId = keyObject != null
            ? GlobalObjectId.GetGlobalObjectIdSlow(keyObject)
            : default;

        if (keyObject == null ||
            !string.Equals(
                AssetDatabase.GetAssetPath(keyObject),
                SafetyTruckKeyPrefabPath,
                StringComparison.Ordinal) ||
            keyId.targetObjectId != SafetyTruckKeyItemSourceFileId)
        {
            throw new InvalidOperationException(
                "Online-safety lockedExtraction is not wired to the exact " +
                "authored truck-key Item component.");
        }

        if (!interaction.enabled ||
            visualRoot.layer != RequireInteractableLayer() ||
            !visualRoot.CompareTag(InteractTag) ||
            visualRoot.GetComponentsInChildren<MonoBehaviour>(true)
                .Count(component => component is IInteract) != 1 ||
            visualRoot.GetComponentsInChildren<MonoBehaviour>(true)
                .Single(component => component is IInteract) != interaction)
        {
            throw new InvalidOperationException(
                $"Scene '{scene.name}' truck must retain the exact enabled " +
                "online-safety " +
                "lockedExtraction authority on the Interactable layer with " +
                "the Interact tag.");
        }

        return new SafetyTruckExtractionBinding(
            visualRoot,
            interaction,
            collider);
    }

    private static void RefreshCurrentSafetyTruckInstanceIfStale(Scene scene)
    {
        string sceneText = File.ReadAllText(ToAbsolutePath(scene.path));
        bool containsRemovedSourceReference =
            sceneText.Contains(
                RemovedTruckEscapeEndingScriptGuid,
                StringComparison.Ordinal) ||
            sceneText.Contains(
                "fileID: 8400000000000005, guid: " +
                SafetyTruckEscapePrefabGuid,
                StringComparison.Ordinal);

        if (!containsRemovedSourceReference)
            return;

        GameObject[] existing = FindPrefabInstanceRoots(
            scene,
            SafetyTruckEscapePrefabPath);
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(
            SafetyTruckEscapePrefabPath);
        if (existing.Length != 1 || source == null)
        {
            throw new InvalidOperationException(
                $"The recognized stale {scene.name} truck reference can be " +
                "repaired " +
                "only when exactly one current online-safety truck instance " +
                "and its exact source prefab are present.");
        }

        GameObject oldRoot = existing[0];
        Transform oldTransform = oldRoot.transform;
        Transform oldParent = oldTransform.parent;
        int oldSiblingIndex = oldTransform.GetSiblingIndex();
        Vector3 oldPosition = oldTransform.position;
        Quaternion oldRotation = oldTransform.rotation;
        Vector3 oldLocalScale = oldTransform.localScale;
        string oldName = oldRoot.name;
        bool oldActive = oldRoot.activeSelf;

        GameObject replacement = PrefabUtility.InstantiatePrefab(
            source,
            scene) as GameObject;
        if (replacement == null)
        {
            throw new InvalidOperationException(
                "Unity could not instantiate the exact current online-safety " +
                "truck while removing its obsolete component reference.");
        }

        if (oldParent != null)
            replacement.transform.SetParent(oldParent, false);

        replacement.transform.SetSiblingIndex(oldSiblingIndex);
        replacement.transform.SetPositionAndRotation(oldPosition, oldRotation);
        replacement.transform.localScale = oldLocalScale;
        replacement.name = oldName;
        replacement.SetActive(oldActive);

        Dictionary<UnityEngine.Object, UnityEngine.Object> replacementMap =
            BuildHierarchyReplacementMap(oldRoot, replacement);
        RewireExternalSceneReferences(
            scene,
            oldTransform,
            replacement.transform,
            replacementMap,
            "online-safety truck");

        UnityEngine.Object.DestroyImmediate(oldRoot);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static SafetyTruckExtractionBinding EnsureOpenWorldSafetyTruck(
        Scene scene)
    {
        GameObject source = RequireExactSafetyTruckPrefab();
        GameObject core = RequirePath(scene, OpenWorldCorePath);
        GameObject[] legacy = FindSceneNamedObjects(
            scene,
            LegacyOpenWorldReturnTruckName);
        GameObject[] current = FindPrefabInstanceRoots(
            scene,
            SafetyTruckEscapePrefabPath);

        if (legacy.Length == 1 && current.Length == 0)
        {
            GameObject oldRoot = legacy[0];
            ValidateLegacyOpenWorldReturnTruck(scene, oldRoot);
            int siblingIndex = oldRoot.transform.GetSiblingIndex();

            GameObject replacement = PrefabUtility.InstantiatePrefab(
                source,
                scene) as GameObject;
            if (replacement == null)
            {
                throw new InvalidOperationException(
                    "Unity could not instantiate the exact online-safety " +
                    "truck while replacing the Open World travel bypass.");
            }

            replacement.transform.SetParent(core.transform, false);
            replacement.transform.SetSiblingIndex(siblingIndex);
            replacement.transform.SetPositionAndRotation(
                OpenWorldSafetyTruckWorldPosition,
                OpenWorldSafetyTruckWorldRotation);
            replacement.transform.localScale = Vector3.one;
            replacement.name = "TruckEscapeEnding";
            replacement.SetActive(true);
            UnityEngine.Object.DestroyImmediate(oldRoot);
            EditorSceneManager.MarkSceneDirty(scene);
        }
        else if (legacy.Length == 0 && current.Length == 1)
        {
            RefreshCurrentSafetyTruckInstanceIfStale(scene);
        }
        else
        {
            throw new InvalidOperationException(
                "Open World extraction must contain either the one exact " +
                "legacy Return Truck migration source or the one connected " +
                "online-safety truck, never mixed, missing, or duplicated.");
        }

        current = FindPrefabInstanceRoots(
            scene,
            SafetyTruckEscapePrefabPath);
        if (FindSceneNamedObjects(
                scene,
                LegacyOpenWorldReturnTruckName).Length != 0 ||
            current.Length != 1)
        {
            throw new InvalidOperationException(
                "Open World Safety truck migration did not close to exactly " +
                "one connected current prefab and zero legacy truck roots.");
        }

        SafetyTruckExtractionBinding binding =
            RequireSafetyTruckExtractionBinding(scene);
        ValidateOpenWorldSafetyTruckPlacement(binding, core);
        return binding;
    }

    private static void ValidateLegacyOpenWorldReturnTruck(
        Scene scene,
        GameObject legacyTruck)
    {
        GameObject core = RequirePath(scene, OpenWorldCorePath);
        CampaignTravelInteractable[] interactables =
            FindSceneComponents<CampaignTravelInteractable>(scene);

        if (legacyTruck.transform.parent != core.transform ||
            !legacyTruck.activeSelf ||
            !Approximately(
                legacyTruck.transform.position,
                OpenWorldSafetyTruckWorldPosition) ||
            Quaternion.Angle(
                legacyTruck.transform.rotation,
                OpenWorldSafetyTruckWorldRotation) > 0.01f ||
            !Approximately(legacyTruck.transform.localScale, Vector3.one) ||
            legacyTruck.transform.childCount != 1 ||
            legacyTruck.transform.GetChild(0).name != "Truck Placeholder" ||
            legacyTruck.GetComponents<Component>().Length != 5 ||
            legacyTruck.GetComponents<BoxCollider>().Length != 1 ||
            legacyTruck.GetComponents<NavMeshModifier>().Length != 1 ||
            legacyTruck.GetComponents<CampaignSceneTravel>().Length != 1 ||
            legacyTruck.GetComponents<CampaignTravelInteractable>().Length != 1 ||
            interactables.Length != 1 ||
            interactables[0].gameObject != legacyTruck)
        {
            throw new InvalidOperationException(
                "Open World legacy Return Truck does not match the exact " +
                "recognized migration source topology and authored pose.");
        }

        ValidateTravel(
            legacyTruck,
            CampaignSceneNames.FarmPrologueHub,
            "FarmHub");
        ValidateNavMeshExcluded(legacyTruck);
    }

    private static void ValidateOpenWorldSafetyTruckPlacement(
        SafetyTruckExtractionBinding binding,
        GameObject core)
    {
        GameObject truck = binding.VisualRoot;
        GameObject source = RequireExactSafetyTruckPrefab();
        Component[] instanceComponents = truck.GetComponents<Component>();
        Component[] sourceComponents = source.GetComponents<Component>();

        if (truck.transform.parent != core.transform ||
            PrefabUtility.GetPrefabInstanceStatus(truck) !=
                PrefabInstanceStatus.Connected ||
            !Approximately(
                truck.transform.position,
                OpenWorldSafetyTruckWorldPosition) ||
            Quaternion.Angle(
                truck.transform.rotation,
                OpenWorldSafetyTruckWorldRotation) > 0.01f ||
            !Approximately(truck.transform.localScale, Vector3.one) ||
            instanceComponents.Length != sourceComponents.Length ||
            instanceComponents.Skip(1).Any(component =>
                PrefabUtility.GetCorrespondingObjectFromSource(component) ==
                null))
        {
            throw new InvalidOperationException(
                "Open World online-safety truck must remain one connected, " +
                "unextended prefab instance directly under _CORE at the " +
                "exact reachable legacy truck pose.");
        }
    }

    private static GameObject EnsureOpenWorldTravelBackend(Scene scene)
    {
        GameObject core = RequirePath(scene, OpenWorldCorePath);
        GameObject[] matches = FindSceneNamedObjects(
            scene,
            OpenWorldTravelBackendName);
        if (matches.Length > 1)
        {
            throw new InvalidOperationException(
                "Open World contains duplicate Safety extraction backends.");
        }

        GameObject backend = matches.Length == 1
            ? matches[0]
            : new GameObject(OpenWorldTravelBackendName);
        if (matches.Length == 0)
        {
            SceneManager.MoveGameObjectToScene(backend, scene);
            backend.transform.SetParent(core.transform, false);
        }

        if (backend.transform.parent != core.transform)
        {
            throw new InvalidOperationException(
                "Open World Safety extraction backend must be a direct child " +
                "of Bloodroot_OpenWorld/_CORE.");
        }

        backend.transform.localPosition = Vector3.zero;
        backend.transform.localRotation = Quaternion.identity;
        backend.transform.localScale = Vector3.one;
        EditorUtility.SetDirty(backend.transform);
        return backend;
    }

    private static void RemoveRecognizedOpenWorldExtractionBypasses(
        Scene scene,
        CampaignSceneTravel retainedBackend)
    {
        bool removed = false;
        WorldMissionCompletionTravel[] completionBypasses =
            FindSceneComponents<WorldMissionCompletionTravel>(scene);
        foreach (WorldMissionCompletionTravel completion in
                 completionBypasses)
        {
            GameObject host = completion.gameObject;
            CampaignSceneTravel[] pairedTravel =
                host.GetComponents<CampaignSceneTravel>();
            if (host.transform.parent == null ||
                !host.transform.parent.name.StartsWith(
                    AlphaWorldCompletionTravelRootPrefix,
                    StringComparison.Ordinal) ||
                host.transform.childCount != 0 ||
                host.GetComponents<Component>().Length != 3 ||
                pairedTravel.Length != 1 ||
                completion.SceneTravel != pairedTravel[0] ||
                pairedTravel[0].DestinationSceneName !=
                    CampaignSceneNames.FarmPrologueHub ||
                pairedTravel[0].SpawnDestinationId != "FarmHub")
            {
                throw new InvalidOperationException(
                    "Open World contains an unrecognized mission-completion " +
                    "travel bypass; automatic deletion is not safe.");
            }

            UnityEngine.Object.DestroyImmediate(host);
            removed = true;
        }

        CampaignTravelInteractable[] interactables =
            FindSceneComponents<CampaignTravelInteractable>(scene);
        CampaignSceneTravel[] travel =
            FindSceneComponents<CampaignSceneTravel>(scene);
        if (interactables.Length != 0 ||
            FindSceneComponents<WorldMissionCompletionTravel>(scene).Length !=
                0 ||
            travel.Length != 1 ||
            travel[0] != retainedBackend)
        {
            throw new InvalidOperationException(
                "Open World extraction must close with zero custom travel " +
                "IInteract/mission-completion bypasses and one retained " +
                "CampaignSceneTravel backend.");
        }

        if (removed)
            EditorSceneManager.MarkSceneDirty(scene);
    }

    private static GameObject EnsureSafetyTruckKey(
        Scene scene,
        Transform hubRoot)
    {
        GameObject source = RequireExactSafetyTruckKeyPrefab();
        GameObject[] legacy = FindPrefabInstanceRoots(
            scene,
            LegacyTruckKeyPrefabPath);
        GameObject[] current = FindPrefabInstanceRoots(
            scene,
            SafetyTruckKeyPrefabPath);

        if (legacy.Length == 1 && current.Length == 0)
        {
            GameObject oldRoot = legacy[0];
            Vector3 worldPosition = oldRoot.transform.position;
            Quaternion worldRotation = oldRoot.transform.rotation;
            Vector3 localScale = oldRoot.transform.localScale;

            GameObject replacement = PrefabUtility.InstantiatePrefab(
                source,
                scene) as GameObject;
            if (replacement == null)
            {
                throw new InvalidOperationException(
                    "Unity could not instantiate the exact online-safety Car " +
                    "Key while replacing the legacy extraction-key pickup.");
            }

            replacement.transform.SetParent(hubRoot, false);
            replacement.transform.SetPositionAndRotation(
                worldPosition,
                worldRotation);
            replacement.transform.localScale = localScale;
            replacement.name = "Item_Key";
            replacement.SetActive(true);
            UnityEngine.Object.DestroyImmediate(oldRoot);
            EditorSceneManager.MarkSceneDirty(scene);
            current = new[] { replacement };
            legacy = Array.Empty<GameObject>();
        }

        if (legacy.Length != 0 || current.Length != 1)
        {
            throw new InvalidOperationException(
                "Farm requires exactly one online-safety Car Key and zero " +
                "legacy TruckEscapeKey prefab instances after migration.");
        }

        ValidateSafetyTruckKey(current[0], hubRoot);
        return current[0];
    }

    private static GameObject RequireExactSafetyTruckKeyPrefab()
    {
        if (!string.Equals(
                AssetDatabase.AssetPathToGUID(SafetyTruckKeyPrefabPath),
                SafetyTruckKeyPrefabGuid,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The online-safety Car Key prefab GUID changed.");
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            SafetyTruckKeyPrefabPath);
        Item item = prefab != null ? prefab.GetComponent<Item>() : null;
        GlobalObjectId itemId = item != null
            ? GlobalObjectId.GetGlobalObjectIdSlow(item)
            : default;
        if (prefab == null ||
            prefab.name != "Item_Key" ||
            item == null ||
            item.item == null ||
            item.item.itemName != "Car Key" ||
            itemId.targetObjectId != SafetyTruckKeyItemSourceFileId)
        {
            throw new InvalidOperationException(
                "The exact online-safety Item_Key prefab no longer exposes " +
                "the authored Car Key Item contract.");
        }

        return prefab;
    }

    private static void ValidateSafetyTruckKey(
        GameObject keyRoot,
        Transform hubRoot)
    {
        GameObject[] current = FindPrefabInstanceRoots(
            keyRoot.scene,
            SafetyTruckKeyPrefabPath);
        Item[] items = keyRoot.GetComponents<Item>();
        Collider[] colliders = keyRoot.GetComponents<Collider>();
        Item sourceItem = items.Length == 1
            ? PrefabUtility.GetCorrespondingObjectFromSource(items[0])
            : null;
        GlobalObjectId sourceItemId = sourceItem != null
            ? GlobalObjectId.GetGlobalObjectIdSlow(sourceItem)
            : default;

        if (current.Length != 1 ||
            current[0] != keyRoot ||
            PrefabUtility.GetOutermostPrefabInstanceRoot(keyRoot) != keyRoot ||
            PrefabUtility.GetPrefabInstanceStatus(keyRoot) !=
                PrefabInstanceStatus.Connected ||
            keyRoot.transform.parent != hubRoot ||
            keyRoot.name != "Item_Key" ||
            !keyRoot.activeSelf ||
            keyRoot.layer != RequireInteractableLayer() ||
            !keyRoot.CompareTag(InteractTag) ||
            items.Length != 1 ||
            !items[0].enabled ||
            items[0].item == null ||
            items[0].item.itemName != "Car Key" ||
            sourceItem == null ||
            !string.Equals(
                AssetDatabase.GetAssetPath(sourceItem),
                SafetyTruckKeyPrefabPath,
                StringComparison.Ordinal) ||
            sourceItemId.targetObjectId != SafetyTruckKeyItemSourceFileId ||
            colliders.Length != 1 ||
            !colliders[0].enabled ||
            colliders[0].isTrigger)
        {
            throw new InvalidOperationException(
                "Farm online-safety Car Key must remain one connected, active " +
                "Hub child with the exact Car Key Item and pickup interaction.");
        }
    }

    private static void ValidateSafetyTruckKeyMigrationState(
        Scene scene,
        Transform hubRoot,
        bool allowLegacyKey)
    {
        RequireExactSafetyTruckKeyPrefab();
        GameObject[] legacy = FindPrefabInstanceRoots(
            scene,
            LegacyTruckKeyPrefabPath);
        GameObject[] current = FindPrefabInstanceRoots(
            scene,
            SafetyTruckKeyPrefabPath);

        if (current.Length == 1 && legacy.Length == 0)
        {
            ValidateSafetyTruckKey(current[0], hubRoot);
            if (FindSceneComponents<TruckEscapeKeyPickup>(scene).Length != 0)
            {
                throw new InvalidOperationException(
                    "Farm retains a legacy TruckEscapeKeyPickup after the " +
                    "online-safety Car Key migration.");
            }

            return;
        }

        if (!allowLegacyKey || legacy.Length != 1 || current.Length != 0)
        {
            throw new InvalidOperationException(
                "Farm extraction-key state must be either the one exact " +
                "recognized legacy pickup during migration or the one exact " +
                "online-safety Car Key, never mixed, missing, or duplicated.");
        }

        GameObject oldRoot = legacy[0];
        TruckEscapeKeyPickup[] oldPickups =
            oldRoot.GetComponents<TruckEscapeKeyPickup>();
        if (oldRoot.transform.parent != null ||
            oldRoot.name != "TruckEscapeKey" ||
            oldRoot.activeSelf ||
            oldPickups.Length != 1)
        {
            throw new InvalidOperationException(
                "Farm legacy extraction key does not match the exact inactive " +
                "top-level migration source.");
        }
    }

    private readonly struct LegacyFarmItemOverrideMatch
    {
        public LegacyFarmItemOverrideMatch(
            GameObject instanceRoot,
            PropertyModification[] modifications,
            int modificationIndex)
        {
            InstanceRoot = instanceRoot;
            Modifications = modifications;
            ModificationIndex = modificationIndex;
        }

        public GameObject InstanceRoot { get; }
        public PropertyModification[] Modifications { get; }
        public int ModificationIndex { get; }
        public PropertyModification Modification =>
            Modifications[ModificationIndex];
    }

    private static void RemoveExactLegacyFarmItemOverride(Scene scene)
    {
        LegacyFarmItemOverrideMatch[] matches =
            FindLegacyFarmItemOverrideMatches(scene);
        ReadLegacyFarmItemOverrideFileShape(
            out int exactSerializedBlockCount,
            out int missingGuidCount);

        if (matches.Length == 0)
        {
            if (exactSerializedBlockCount != 0 || missingGuidCount != 0)
            {
                throw new InvalidOperationException(
                    "Farm scene serialization still contains the legacy " +
                    "missing ItemStats GUID, but Unity did not expose the " +
                    "exact prefab override required for a safe repair.");
            }

            return;
        }

        if (matches.Length != 1 || exactSerializedBlockCount != 1 ||
            missingGuidCount != 1)
        {
            throw new InvalidOperationException(
                "Farm legacy pickup repair expected exactly one matching " +
                "Item.item override and one exact missing-GUID YAML block.");
        }

        LegacyFarmItemOverrideMatch match = matches[0];
        ValidateExactLegacyFarmItemOverrideMatch(match);

        PropertyModification[] retained = match.Modifications
            .Where((_, index) => index != match.ModificationIndex)
            .ToArray();
        PrefabUtility.SetPropertyModifications(
            match.InstanceRoot,
            retained);

        PropertyModification[] after =
            PrefabUtility.GetPropertyModifications(match.InstanceRoot) ??
            Array.Empty<PropertyModification>();

        if (after.Length != retained.Length ||
            !ContainSamePropertyModifications(after, retained))
        {
            throw new InvalidOperationException(
                "Unity changed unrelated Farm pickup prefab overrides while " +
                "removing the stale Item.item override; aborting for rollback.");
        }

        EditorUtility.SetDirty(match.InstanceRoot);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static bool ContainSamePropertyModifications(
        IReadOnlyList<PropertyModification> left,
        IReadOnlyList<PropertyModification> right)
    {
        if (left.Count != right.Count)
            return false;

        var consumed = new bool[right.Count];

        foreach (PropertyModification candidate in left)
        {
            bool found = false;

            for (int index = 0; index < right.Count; index++)
            {
                if (consumed[index] ||
                    !PropertyModificationComparer.Instance.Equals(
                        candidate,
                        right[index]))
                {
                    continue;
                }

                consumed[index] = true;
                found = true;
                break;
            }

            if (!found)
                return false;
        }

        return true;
    }

    private static void ValidateFarmItemOverrideMigrationState(
        Scene scene,
        bool allowExactLegacyOverride)
    {
        LegacyFarmItemOverrideMatch[] matches =
            FindLegacyFarmItemOverrideMatches(scene);

        if (matches.Length == 0)
            return;

        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                "Farm contains multiple Item_Pickup_Master Item.item " +
                "overrides; automatic migration is not safe.");
        }

        ValidateExactLegacyFarmItemOverrideMatch(matches[0]);

        if (!allowExactLegacyOverride)
        {
            throw new InvalidOperationException(
                "Farm retains the stale Item (6) Item.item prefab override. " +
                "Rebuild the campaign foundation to inherit the valid inline " +
                "ItemStats from online safety.");
        }
    }

    private sealed class PropertyModificationComparer :
        IEqualityComparer<PropertyModification>
    {
        public static readonly PropertyModificationComparer Instance = new();

        public bool Equals(
            PropertyModification left,
            PropertyModification right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (left == null || right == null)
                return false;

            return left.target == right.target &&
                   left.propertyPath == right.propertyPath &&
                   left.value == right.value &&
                   left.objectReference == right.objectReference;
        }

        public int GetHashCode(PropertyModification modification)
        {
            return modification == null
                ? 0
                : HashCode.Combine(
                    modification.target,
                    modification.propertyPath,
                    modification.value,
                    modification.objectReference);
        }
    }

    private static LegacyFarmItemOverrideMatch[]
        FindLegacyFarmItemOverrideMatches(Scene scene)
    {
        ReadLegacyFarmItemOverrideFileShape(
            out int exactSerializedBlockCount,
            out int missingGuidCount);
        if (exactSerializedBlockCount == 0 && missingGuidCount == 0)
            return Array.Empty<LegacyFarmItemOverrideMatch>();

        RequireItemPickupMasterSource(
            out GameObject sourceRoot,
            out Item sourceItem);

        var matches = new List<LegacyFarmItemOverrideMatch>();
        var inspectedRoots = new HashSet<GameObject>();

        foreach (Item sceneItem in FindSceneComponents<Item>(scene))
        {
            GameObject instanceRoot =
                PrefabUtility.GetNearestPrefabInstanceRoot(
                    sceneItem.gameObject);

            if (instanceRoot == null || !inspectedRoots.Add(instanceRoot))
                continue;

            GameObject directSource =
                PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot)
                    as GameObject;

            if (directSource != sourceRoot)
                continue;

            PropertyModification[] modifications =
                PrefabUtility.GetPropertyModifications(instanceRoot) ??
                Array.Empty<PropertyModification>();

            for (int index = 0; index < modifications.Length; index++)
            {
                PropertyModification modification = modifications[index];

                if (modification != null &&
                    modification.target == sourceItem &&
                    modification.propertyPath == "item")
                {
                    matches.Add(new LegacyFarmItemOverrideMatch(
                        instanceRoot,
                        modifications,
                        index));
                }
            }
        }

        return matches.ToArray();
    }

    private static void RequireItemPickupMasterSource(
        out GameObject sourceRoot,
        out Item sourceItem)
    {
        sourceRoot = RequireAssetAtPath<GameObject>(ItemPickupMasterPath);
        Item[] rootItems = sourceRoot.GetComponents<Item>();

        if (rootItems.Length != 1)
        {
            throw new InvalidOperationException(
                "Online-safety Item_Pickup_Master must retain exactly one " +
                "root Item component for the Farm migration contract.");
        }

        sourceItem = rootItems[0];

        if (AssetDatabase.AssetPathToGUID(ItemPickupMasterPath) !=
                ItemPickupMasterGuid ||
            !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                sourceItem,
                out string sourceGuid,
                out long sourceFileId) ||
            sourceGuid != ItemPickupMasterGuid ||
            sourceFileId != ItemPickupMasterItemFileId ||
            sourceItem.item == null ||
            string.IsNullOrWhiteSpace(sourceItem.item.itemName) ||
            sourceItem.item.itemMesh == null)
        {
            throw new InvalidOperationException(
                "Online-safety Item_Pickup_Master no longer matches the " +
                "exact valid inline ItemStats source used by the Farm repair.");
        }
    }

    private static void ValidateExactLegacyFarmItemOverrideMatch(
        LegacyFarmItemOverrideMatch match)
    {
        RequireItemPickupMasterSource(
            out GameObject sourceRoot,
            out Item sourceItem);
        GameObject instanceRoot = match.InstanceRoot;
        PropertyModification modification = match.Modification;
        Item[] instanceItems = instanceRoot.GetComponents<Item>();

        if (instanceRoot.name != LegacyBrokenFarmPickupName ||
            !Approximately(
                instanceRoot.transform.position,
                LegacyBrokenFarmPickupPosition) ||
            Quaternion.Angle(
                instanceRoot.transform.rotation,
                Quaternion.identity) > 0.01f ||
            !Approximately(instanceRoot.transform.localScale, Vector3.one) ||
            PrefabUtility.GetOutermostPrefabInstanceRoot(instanceRoot) !=
                instanceRoot ||
            PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot)
                != sourceRoot ||
            instanceItems.Length != 1 ||
            PrefabUtility.GetCorrespondingObjectFromSource(instanceItems[0])
                != sourceItem ||
            modification.target != sourceItem ||
            modification.propertyPath != "item" ||
            modification.objectReference != null ||
            !string.IsNullOrEmpty(modification.value))
        {
            throw new InvalidOperationException(
                "Farm legacy pickup override does not match the exact " +
                "online-safety Item (6) migration shape; no override was removed.");
        }
    }

    private static void ReadLegacyFarmItemOverrideFileShape(
        out int exactSerializedBlockCount,
        out int missingGuidCount)
    {
        string sceneText = File.ReadAllText(ToAbsolutePath(FarmScenePath));
        string exactBlockPattern =
            @"[ \t]*-[ \t]+target:[ \t]+\{fileID:[ \t]*" +
            ItemPickupMasterItemFileId +
            @",[ \t]+guid:[ \t]*" + ItemPickupMasterGuid +
            @",[ \t]+type:[ \t]*3\}[ \t]*\r?\n" +
            @"[ \t]+propertyPath:[ \t]+item[ \t]*\r?\n" +
            @"[ \t]+value:[ \t]*\r?\n" +
            @"[ \t]+objectReference:[ \t]+\{fileID:[ \t]*11400000," +
            @"[ \t]+guid:[ \t]*" + LegacyMissingFarmItemGuid +
            @",[ \t]+type:[ \t]*2\}";

        exactSerializedBlockCount = Regex.Matches(
            sceneText,
            exactBlockPattern,
            RegexOptions.CultureInvariant).Count;
        missingGuidCount = CountOrdinalOccurrences(
            sceneText,
            LegacyMissingFarmItemGuid);
    }

    private static int CountOrdinalOccurrences(
        string value,
        string searchValue)
    {
        int count = 0;
        int startIndex = 0;

        while ((startIndex = value.IndexOf(
                   searchValue,
                   startIndex,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            startIndex += searchValue.Length;
        }

        return count;
    }

    private static GameObject[] RequireFarmBoarRoster()
    {
        GameObject boar =
            RequireAssetAtPath<GameObject>(SafetyBoarPrefabPath);
        GameObject boarRoot =
            RequireAssetAtPath<GameObject>(SafetyBoarRootPrefabPath);

        ValidateFarmBoarAsset(
            boar,
            SafetyBoarPrefabPath,
            SafetyBoarPrefabGuid,
            typeof(global::BoarBruteAI),
            expectBoarVariant: false);
        ValidateFarmBoarAsset(
            boarRoot,
            SafetyBoarRootPrefabPath,
            SafetyBoarRootPrefabGuid,
            typeof(global::BoarBruteRootAI),
            expectBoarVariant: true);

        // Protected MobSpawner progressively unlocks roster entries by wave.
        // Repeating Boar in slot two keeps waves one and two Boar-only; the
        // third wave introduces the exact inherited Boar Root variant.
        return new[] { boar, boar, boarRoot };
    }

    private static void ValidateFarmBoarAsset(
        GameObject prefab,
        string expectedPath,
        string expectedGuid,
        Type expectedControllerType,
        bool expectBoarVariant)
    {
        string resolvedPath = AssetDatabase.GetAssetPath(prefab);
        string resolvedGuid = AssetDatabase.AssetPathToGUID(resolvedPath);
        enemyAI[] controllers = prefab.GetComponents<enemyAI>();
        NavMeshAgent[] agents = prefab.GetComponents<NavMeshAgent>();
        Animator[] animators = prefab.GetComponentsInChildren<Animator>(true);
        string animatorPath = animators.Length == 1
            ? AssetDatabase.GetAssetPath(
                animators[0].runtimeAnimatorController)
            : string.Empty;
        string animatorGuid = string.IsNullOrEmpty(animatorPath)
            ? string.Empty
            : AssetDatabase.AssetPathToGUID(animatorPath);
        GameObject originalSource = expectBoarVariant
            ? PrefabUtility.GetCorrespondingObjectFromOriginalSource(prefab)
            : null;
        string originalSourcePath = originalSource != null
            ? AssetDatabase.GetAssetPath(originalSource)
            : string.Empty;
        string originalSourceGuid = string.IsNullOrEmpty(originalSourcePath)
            ? string.Empty
            : AssetDatabase.AssetPathToGUID(originalSourcePath);

        if (resolvedPath != expectedPath || resolvedGuid != expectedGuid ||
            controllers.Length != 1 || !controllers[0].enabled ||
            controllers[0].GetType() != expectedControllerType ||
            controllers[0].transform != prefab.transform ||
            agents.Length != 1 || !agents[0].enabled ||
            agents[0].transform != prefab.transform ||
            agents[0].agentTypeID != 0 ||
            (agents[0].areaMask & MobSpawnerWalkableAreaMask) == 0 ||
            Mathf.Abs(agents[0].radius - 0.5f) > 0.001f ||
            Mathf.Abs(agents[0].height - 2f) > 0.001f ||
            animators.Length != 1 || !animators[0].enabled ||
            (controllers[0].animator != null &&
             controllers[0].animator != animators[0]) ||
            animatorPath != SafetyBoarAnimatorControllerPath ||
            animatorGuid != SafetyBoarAnimatorControllerGuid ||
            (expectBoarVariant
                ? PrefabUtility.GetPrefabAssetType(prefab) !=
                    PrefabAssetType.Variant ||
                  originalSourcePath != SafetyBoarPrefabPath ||
                  originalSourceGuid != SafetyBoarPrefabGuid
                : PrefabUtility.GetPrefabAssetType(prefab) !=
                    PrefabAssetType.Regular) ||
            !CampaignSafetyEnemyRuntimeAdapter.ValidateSafetyContract(out _))
        {
            throw new InvalidOperationException(
                $"Farm enemy '{expectedPath}' must remain the exact " +
                "GUID-pinned online-Safety Boar-family asset with its root " +
                "controller, Humanoid NavMeshAgent, inherited Animator, " +
                "prefab lineage, and campaign runtime adapter contract.");
        }
    }

#if false // Retired: Farm now uses only native Safety Boar-family prefabs.
    private static AnimatorController
        EnsureSafetyEnemyCompatibilityController()
    {
        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(
                SafetyEnemyCompatibilityControllerPath);

        if (IsExactSafetyEnemyCompatibilityController(controller))
            return controller;

        DeleteOwnedCompatibilityAssetIfPresent(
            SafetyEnemyCompatibilityControllerPath);

        controller = AnimatorController.CreateAnimatorControllerAtPath(
            SafetyEnemyCompatibilityControllerPath);

        if (controller == null)
        {
            throw new InvalidOperationException(
                "Could not create the campaign-owned safety enemy " +
                "compatibility AnimatorController.");
        }

        controller.AddParameter(
            SafetyEnemySpeedParameterName,
            AnimatorControllerParameterType.Float);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(
            SafetyEnemyCompatibilityControllerPath,
            ImportAssetOptions.ForceSynchronousImport);

        controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            SafetyEnemyCompatibilityControllerPath);

        if (!IsExactSafetyEnemyCompatibilityController(controller))
        {
            throw new InvalidOperationException(
                "The campaign-owned safety enemy compatibility controller " +
                "did not import with the exact empty Speed-float contract.");
        }

        return controller;
    }

    private static GameObject EnsureCampaignEnemyVariant(
        Scene destinationScene,
        GameObject safetySource,
        string safetySourcePath,
        string variantPath,
        AnimatorController controller)
    {
        if (safetySource.GetComponentsInChildren<Animator>(true).Length != 0)
        {
            throw new InvalidOperationException(
                $"Online-safety enemy '{safetySourcePath}' now contains an " +
                "Animator. Review the campaign compatibility variant before " +
                "changing the inherited animation contract.");
        }

        GameObject variant =
            AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);

        if (IsExactCampaignEnemyVariant(
                variant,
                safetySource,
                variantPath,
                controller))
        {
            return variant;
        }

        DeleteOwnedCompatibilityAssetIfPresent(variantPath);

        GameObject instance = PrefabUtility.InstantiatePrefab(
            safetySource,
            destinationScene) as GameObject;

        if (instance == null)
        {
            throw new InvalidOperationException(
                $"Could not instantiate online-safety enemy " +
                $"'{safetySourcePath}' while authoring its campaign variant.");
        }

        try
        {
            if (instance.GetComponentsInChildren<Animator>(true).Length != 0)
            {
                throw new InvalidOperationException(
                    $"Online-safety enemy '{safetySourcePath}' produced an " +
                    "unexpected Animator while authoring its campaign variant.");
            }

            Animator animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.enabled = true;
            EditorUtility.SetDirty(animator);

            enemyAI[] inheritedControllers = instance.GetComponents<enemyAI>();
            if (inheritedControllers.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Online-safety enemy '{safetySourcePath}' must expose " +
                    "exactly one inherited root enemyAI controller.");
            }

            inheritedControllers[0].animator = animator;
            EditorUtility.SetDirty(inheritedControllers[0]);
            PrefabUtility.RecordPrefabInstancePropertyModifications(
                inheritedControllers[0]);

            variant = PrefabUtility.SaveAsPrefabAsset(
                instance,
                variantPath,
                out bool savedSuccessfully);

            if (!savedSuccessfully || variant == null)
            {
                throw new InvalidOperationException(
                    $"Unity could not save campaign enemy variant " +
                    $"'{variantPath}'.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(
            variantPath,
            ImportAssetOptions.ForceSynchronousImport);
        variant = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);

        if (!IsExactCampaignEnemyVariant(
                variant,
                safetySource,
                variantPath,
                controller))
        {
            throw new InvalidOperationException(
                $"Campaign enemy asset '{variantPath}' is not a true direct " +
                $"variant of '{safetySourcePath}' with the exact Animator contract.");
        }

        return variant;
    }
#endif

    private static void ConfigureFarmEnemyRoster(
        MobSpawner spawner,
        IReadOnlyList<GameObject> roster)
    {
        if (roster == null || roster.Count != 3 ||
            roster.Any(enemy => enemy == null))
        {
            throw new InvalidOperationException(
                "Campaign Farm enemy roster must contain exactly " +
                "[Boar, Boar, BoarRoot] in deterministic wave-unlock order.");
        }

        var spawnerData = new SerializedObject(spawner);
        SerializedProperty fallback = spawnerData.FindProperty("Enemy");
        SerializedProperty enemies = spawnerData.FindProperty("enemies");

        if (fallback == null || enemies == null || !enemies.isArray)
        {
            throw new InvalidOperationException(
                "Farm MobSpawner enemy fields could not be rewired.");
        }

        bool alreadyConfigured =
            fallback.objectReferenceValue == roster[0] &&
            enemies.arraySize == roster.Count;

        for (int index = 0;
             alreadyConfigured && index < roster.Count;
             index++)
        {
            alreadyConfigured = enemies.GetArrayElementAtIndex(index)
                .objectReferenceValue == roster[index];
        }

        if (alreadyConfigured)
            return;

        fallback.objectReferenceValue = roster[0];
        enemies.arraySize = roster.Count;

        for (int index = 0; index < roster.Count; index++)
        {
            enemies.GetArrayElementAtIndex(index).objectReferenceValue =
                roster[index];
        }

        spawnerData.ApplyModifiedProperties();
        EditorUtility.SetDirty(spawner);
    }

    private static void ConfigureFarmHogHuntIntro(
        waveManager encounter,
        bool enabled)
    {
        SerializedObject encounterData = new(encounter);
        SerializedProperty hogIntro =
            encounterData.FindProperty("useHogHuntIntro");
        if (hogIntro == null ||
            hogIntro.propertyType != SerializedPropertyType.Boolean)
        {
            throw new InvalidOperationException(
                "Farm WaveManager no longer exposes its useHogHuntIntro gate.");
        }

        if (hogIntro.boolValue == enabled)
            return;

        hogIntro.boolValue = enabled;
        encounterData.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(encounter);
    }

#if false // Retired: compatibility variants remain untouched for older scenes.
    private static void ValidateCampaignEnemyCompatibilityAssets()
    {
        ValidateCampaignEnemyCompatibilityAssets(
            RequireAssetAtPath<AnimatorController>(
                SafetyEnemyCompatibilityControllerPath),
            RequireAssetAtPath<GameObject>(CampaignScreecherVariantPath),
            RequireAssetAtPath<GameObject>(CampaignJuggernautVariantPath));
    }

    private static void ValidateCampaignEnemyCompatibilityAssets(
        AnimatorController controller,
        GameObject screecherVariant,
        GameObject juggernautVariant)
    {
        if (!IsExactSafetyEnemyCompatibilityController(controller))
        {
            throw new InvalidOperationException(
                "Campaign safety enemy compatibility controller must be " +
                "empty and contain exactly one Float parameter named Speed.");
        }

        GameObject safetyScreecher =
            RequireAssetAtPath<GameObject>(SafetyScreecherPrefabPath);
        GameObject safetyJuggernaut =
            RequireAssetAtPath<GameObject>(SafetyJuggernautPrefabPath);

        if (safetyScreecher.GetComponentsInChildren<Animator>(true).Length != 0 ||
            safetyJuggernaut.GetComponentsInChildren<Animator>(true).Length != 0)
        {
            throw new InvalidOperationException(
                "Online-safety Screecher/Juggernaut animation authoring has " +
                "changed; the campaign-owned compatibility variants require review.");
        }

        if (!IsExactCampaignEnemyVariant(
                screecherVariant,
                safetyScreecher,
                CampaignScreecherVariantPath,
                controller) ||
            !IsExactCampaignEnemyVariant(
                juggernautVariant,
                safetyJuggernaut,
                CampaignJuggernautVariantPath,
                controller))
        {
            throw new InvalidOperationException(
                "Campaign Screecher/Juggernaut compatibility assets must be " +
                "true direct variants of the exact online-safety prefabs.");
        }
    }

    private static bool IsExactSafetyEnemyCompatibilityController(
        AnimatorController controller)
    {
        if (controller == null ||
            AssetDatabase.GetAssetPath(controller) !=
            SafetyEnemyCompatibilityControllerPath)
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = controller.parameters;
        AnimatorControllerLayer[] layers = controller.layers;

        if (parameters.Length != 1 ||
            parameters[0].name != SafetyEnemySpeedParameterName ||
            parameters[0].type != AnimatorControllerParameterType.Float ||
            !Mathf.Approximately(parameters[0].defaultFloat, 0f) ||
            controller.animationClips.Length != 0 ||
            layers.Length != 1 ||
            layers[0].name != "Base Layer" ||
            layers[0].stateMachine == null)
        {
            return false;
        }

        AnimatorStateMachine stateMachine = layers[0].stateMachine;
        return stateMachine.defaultState == null &&
               stateMachine.states.Length == 0 &&
               stateMachine.stateMachines.Length == 0 &&
               stateMachine.anyStateTransitions.Length == 0 &&
               stateMachine.entryTransitions.Length == 0 &&
               stateMachine.behaviours.Length == 0;
    }

    private static bool IsExactCampaignEnemyVariant(
        GameObject variant,
        GameObject safetySource,
        string variantPath,
        AnimatorController controller)
    {
        if (variant == null || safetySource == null || controller == null ||
            AssetDatabase.GetAssetPath(variant) != variantPath ||
            PrefabUtility.GetPrefabAssetType(variant) != PrefabAssetType.Variant)
        {
            return false;
        }

        GameObject directSource =
            PrefabUtility.GetCorrespondingObjectFromSource(variant)
                as GameObject;
        Animator[] animators =
            variant.GetComponentsInChildren<Animator>(true);
        enemyAI[] inheritedControllers = variant.GetComponents<enemyAI>();

        if (directSource != safetySource ||
            animators.Length != 1 ||
            animators[0].gameObject != variant ||
            inheritedControllers.Length != 1 ||
            inheritedControllers[0].animator != animators[0])
        {
            return false;
        }

        Animator animator = animators[0];
        return animator.enabled &&
               animator.runtimeAnimatorController == controller &&
               !animator.applyRootMotion &&
               animator.updateMode == AnimatorUpdateMode.Normal &&
               animator.cullingMode == AnimatorCullingMode.AlwaysAnimate;
    }

    private static void DeleteOwnedCompatibilityAssetIfPresent(string path)
    {
        UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(path);

        if (existing != null)
        {
            if (!AssetDatabase.DeleteAsset(path))
            {
                throw new InvalidOperationException(
                    $"Could not replace campaign-owned compatibility asset " +
                    $"'{path}'.");
            }

            return;
        }

        string absolutePath = ToAbsolutePath(path);
        string metaPath = ToAbsolutePath(path + ".meta");

        if (!File.Exists(absolutePath) && !File.Exists(metaPath))
            return;

        AssetDatabase.ReleaseCachedFileHandles();

        if (File.Exists(absolutePath))
            File.Delete(absolutePath);

        if (File.Exists(metaPath))
            File.Delete(metaPath);

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }
#endif

    private readonly struct SpawnZoneSnapshot
    {
        public SpawnZoneSnapshot(
            Transform source,
            BoxCollider sourceCollider)
        {
            Source = source;
            SourceCollider = sourceCollider;
            Position = source.position;
            Rotation = source.rotation;
            LocalScale = source.localScale;
            WorldScale = new Vector3(
                Mathf.Abs(source.lossyScale.x),
                Mathf.Abs(source.lossyScale.y),
                Mathf.Abs(source.lossyScale.z));
            ColliderCenter = sourceCollider.center;
            ColliderSize = sourceCollider.size;
        }

        public Transform Source { get; }
        public BoxCollider SourceCollider { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public Vector3 LocalScale { get; }
        public Vector3 WorldScale { get; }
        public Vector3 ColliderCenter { get; }
        public Vector3 ColliderSize { get; }
    }

    private static void EnsureWakeAuthoring(
        Scene scene,
        GameObject wakeRoot)
    {
        ValidateUniqueWorldSocket(
            scene,
            wakeRoot,
            WakePlayerAnchorName,
            FarmSafetyWakePlayerWorldPosition,
            FarmPrologueSpawnWorldRotation);
        ValidateUniqueWorldSocket(
            scene,
            wakeRoot,
            WakeViewAnchorName,
            FarmSafetyWakeViewWorldPosition,
            FarmPrologueSpawnWorldRotation);
        ValidateUniqueWorldSocket(
            scene,
            wakeRoot,
            WakeLookTargetName,
            FarmSafetyWakeLookWorldPosition,
            FarmPrologueSpawnWorldRotation);
    }

    private static void EnsureFutureFarmContentSockets(
        GameObject coreRoot,
        IReadOnlyList<Vector3> propSocketWorldPositions,
        IReadOnlyList<Vector3> pigSocketWorldPositions)
    {
        GameObject authoringRoot =
            EnsureDirectChild(coreRoot, FarmAuthoringSocketsName);
        ResetLocalPose(authoringRoot.transform);
        MarkAsEditorSocket(authoringRoot, "sv_label_4");

        GameObject propRoot =
            EnsureDirectChild(authoringRoot, FarmPropSocketsName);
        ResetLocalPose(propRoot.transform);
        MarkAsEditorSocket(propRoot, "sv_label_3");

        for (int index = 0; index < FarmPropSocketNames.Length; index++)
        {
            EnsureWorldSocket(
                propRoot,
                FarmPropSocketNames[index],
                propSocketWorldPositions[index],
                Quaternion.identity,
                "sv_label_3");
        }

        GameObject pigRoot =
            EnsureDirectChild(authoringRoot, FarmPigSocketsName);
        ResetLocalPose(pigRoot.transform);
        MarkAsEditorSocket(pigRoot, "sv_label_5");

        for (int index = 0; index < FarmPigSocketNames.Length; index++)
        {
            EnsureWorldSocket(
                pigRoot,
                FarmPigSocketNames[index],
                pigSocketWorldPositions[index],
                Quaternion.identity,
                "sv_label_5");
        }
    }

    private static void EnsureCombatAuthoring(
        Scene scene,
        GameObject enemiesRoot,
        GameObject rumbleRoot,
        MobSpawner spawner,
        waveManager encounter,
        Transform prologueSpawn,
        Vector3 playerAnchorPosition)
    {
        SpawnZoneSnapshot[] snapshots =
            ReadSpawnZoneSnapshots(spawner);

        GameObject emergenceRoot =
            EnsureDirectChild(enemiesRoot, EnemyEmergenceRootName);
        ResetLocalPose(emergenceRoot.transform);
        MarkAsEditorSocket(emergenceRoot, "sv_label_6");

        var zones = new Transform[EmergenceZoneNames.Length];

        for (int index = 0; index < zones.Length; index++)
        {
            SpawnZoneSnapshot snapshot = snapshots[index];
            GameObject zone =
                EnsureDirectChild(emergenceRoot, EmergenceZoneNames[index]);
            zone.layer = RequireSpawnVolumeLayer();
            zone.tag = "Untagged";
            zone.transform.SetPositionAndRotation(
                snapshot.Position,
                snapshot.Rotation);
            // Once MobSpawner points at this generated zone, preserve its
            // serialized local scale exactly. Reapplying lossyScale as a local
            // scale compounds floating-point parent/rotation conversions on
            // every rebuild and makes the Farm scene byte-unstable.
            zone.transform.localScale = snapshot.Source == zone.transform
                ? snapshot.LocalScale
                : snapshot.WorldScale;
            MarkAsEditorSocket(zone, "sv_label_6");

            BoxCollider zoneCollider = EnsureComponent<BoxCollider>(zone);
            zoneCollider.enabled = true;
            zoneCollider.isTrigger = true;
            zoneCollider.center = snapshot.ColliderCenter;
            zoneCollider.size = snapshot.ColliderSize;
            EditorUtility.SetDirty(zoneCollider);
            EnsureNavMeshExcluded(zone);

            zones[index] = zone.transform;
        }

        RewireMobSpawnerSpawnZones(spawner, zones);
        DisableSupersededSolidSpawnColliders(snapshots, zones);

        Bounds combatBounds =
            zones[0].GetComponent<BoxCollider>().bounds;

        for (int index = 1; index < zones.Length; index++)
        {
            combatBounds.Encapsulate(
                zones[index].GetComponent<BoxCollider>().bounds);
        }

        GameObject boundsObject =
            EnsureDirectChild(enemiesRoot, CombatBoundsName);
        boundsObject.layer = RequireSpawnVolumeLayer();
        boundsObject.tag = "Untagged";
        boundsObject.transform.SetPositionAndRotation(
            combatBounds.center,
            Quaternion.identity);
        boundsObject.transform.localScale = Vector3.one;
        MarkAsEditorSocket(boundsObject, "sv_label_1");

        BoxCollider boundsCollider =
            EnsureComponent<BoxCollider>(boundsObject);
        boundsCollider.enabled = true;
        boundsCollider.isTrigger = true;
        boundsCollider.center = Vector3.zero;
        boundsCollider.size = new Vector3(
            Mathf.Max(1f, combatBounds.size.x),
            Mathf.Max(6f, combatBounds.size.y),
            Mathf.Max(1f, combatBounds.size.z));
        EditorUtility.SetDirty(boundsCollider);
        EnsureNavMeshExcluded(boundsObject);

        Vector3 faceDirection = combatBounds.center - playerAnchorPosition;
        faceDirection.y = 0f;
        Quaternion playerAnchorRotation = faceDirection.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(faceDirection.normalized, Vector3.up)
            : prologueSpawn.rotation;

        EnsureWorldSocket(
            enemiesRoot,
            CombatPlayerAnchorName,
            playerAnchorPosition,
            playerAnchorRotation,
            "sv_label_1");

        EnsureRumbleSockets(
            rumbleRoot,
            playerAnchorPosition,
            playerAnchorRotation);
        EnsurePersistentEmergencePresenter(
            scene,
            emergenceRoot,
            encounter);
    }

    private static SpawnZoneSnapshot[] ReadSpawnZoneSnapshots(
        MobSpawner spawner)
    {
        var spawnerData = new SerializedObject(spawner);
        SerializedProperty spawnPoints =
            spawnerData.FindProperty("spawnPoint");

        if (spawnPoints == null ||
            spawnPoints.arraySize != EmergenceZoneNames.Length)
        {
            int count = spawnPoints?.arraySize ?? 0;
            throw new InvalidOperationException(
                "Farm MobSpawner must expose exactly three authored spawn " +
                $"volumes before campaign wiring; found {count}.");
        }

        var snapshots =
            new SpawnZoneSnapshot[EmergenceZoneNames.Length];

        for (int index = 0; index < snapshots.Length; index++)
        {
            Transform source = spawnPoints
                .GetArrayElementAtIndex(index)
                .objectReferenceValue as Transform;
            BoxCollider sourceCollider =
                source != null ? source.GetComponent<BoxCollider>() : null;

            if (source == null || sourceCollider == null)
            {
                throw new InvalidOperationException(
                    $"Farm MobSpawner spawn volume {index + 1} requires " +
                    "a non-null Transform with a BoxCollider.");
            }

            if (sourceCollider.size.x <= 0f ||
                sourceCollider.size.y <= 0f ||
                sourceCollider.size.z <= 0f)
            {
                throw new InvalidOperationException(
                    $"Farm MobSpawner spawn volume '{source.name}' has " +
                    "invalid non-positive dimensions.");
            }

            snapshots[index] =
                new SpawnZoneSnapshot(source, sourceCollider);
        }

        return snapshots;
    }

    private static void RewireMobSpawnerSpawnZones(
        MobSpawner spawner,
        IReadOnlyList<Transform> zones)
    {
        var spawnerData = new SerializedObject(spawner);
        SerializedProperty spawnPoints =
            spawnerData.FindProperty("spawnPoint")
            ?? throw new InvalidOperationException(
                "MobSpawner serialized spawnPoint array is missing.");

        spawnPoints.arraySize = zones.Count;

        for (int index = 0; index < zones.Count; index++)
        {
            spawnPoints.GetArrayElementAtIndex(index)
                .objectReferenceValue = zones[index];
        }

        spawnerData.ApplyModifiedProperties();
        EditorUtility.SetDirty(spawner);
    }

    private static void DisableSupersededSolidSpawnColliders(
        IReadOnlyList<SpawnZoneSnapshot> snapshots,
        IReadOnlyCollection<Transform> currentZones)
    {
        var current = new HashSet<Transform>(currentZones);

        foreach (SpawnZoneSnapshot snapshot in snapshots)
        {
            if (current.Contains(snapshot.Source))
                continue;

            snapshot.SourceCollider.isTrigger = true;
            snapshot.SourceCollider.enabled = false;
            EditorUtility.SetDirty(snapshot.SourceCollider);
        }
    }

    private static void EnsureRumbleSockets(
        GameObject rumbleRoot,
        Vector3 groundPosition,
        Quaternion facing)
    {
        EnsureWorldSocket(
            rumbleRoot,
            RumbleGroundOriginName,
            groundPosition,
            Quaternion.identity,
            "sv_label_7");
        EnsureWorldSocket(
            rumbleRoot,
            RumbleAudioAnchorName,
            groundPosition + Vector3.up * 0.25f,
            Quaternion.identity,
            "sv_label_7");
        EnsureWorldSocket(
            rumbleRoot,
            RumbleCameraAnchorName,
            groundPosition + Vector3.up * 1.65f,
            facing,
            "sv_label_7");
    }

    private static void EnsureRumblePresentation(
        Scene scene,
        GameObject rumbleRoot,
        FarmPrologueDirector director,
        cameraController cameraLook)
    {
        FarmRumblePresenter[] existing =
            FindSceneComponents<FarmRumblePresenter>(scene);

        if (existing.Any(presenter => presenter.gameObject != rumbleRoot))
        {
            throw new InvalidOperationException(
                "Farm contains a rumble presenter outside the authored " +
                "Ground Rumble Sequence root.");
        }

        GameObject audioAnchor =
            RequireDirectChild(rumbleRoot, RumbleAudioAnchorName);
        AudioSource audioSource = EnsureComponent<AudioSource>(audioAnchor);
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 1f;
        audioSource.dopplerLevel = 0f;
        audioSource.minDistance = 5f;
        audioSource.maxDistance = 120f;
        EditorUtility.SetDirty(audioSource);

        FarmRumblePresenter presenter =
            EnsureComponent<FarmRumblePresenter>(rumbleRoot);
        presenter.Configure(
            director,
            cameraLook.transform,
            audioSource);
        presenter.ConfigureShake(0.09f, 17f, 0.2f);
        EditorUtility.SetDirty(presenter);
    }

    private static void EnsurePersistentEmergencePresenter(
        Scene scene,
        GameObject emergenceRoot,
        waveManager encounter)
    {
        GameObject presentation =
            EnsureDirectChild(emergenceRoot, EmergencePresentationName);
        ResetLocalPose(presentation.transform);
        MarkAsEditorSocket(presentation, "sv_label_6");

        FarmEnemyEmergencePresenter[] existing =
            FindSceneComponents<FarmEnemyEmergencePresenter>(scene);
        FarmEnemyEmergencePresenter legacy = existing
            .SingleOrDefault(component => component.gameObject != presentation);

        if (existing.Count(component => component.gameObject != presentation) > 1)
        {
            throw new InvalidOperationException(
                "Farm contains multiple legacy enemy emergence presenters; " +
                "repair the scene before rebuilding.");
        }

        bool presenterAlreadyAuthored =
            presentation.GetComponent<FarmEnemyEmergencePresenter>() != null;
        FarmEnemyEmergencePresenter presenter =
            EnsureComponent<FarmEnemyEmergencePresenter>(presentation);

        if (legacy != null && !presenterAlreadyAuthored)
        {
            EditorUtility.CopySerialized(legacy, presenter);
        }

        if (legacy != null)
        {
            UnityEngine.Object.DestroyImmediate(legacy);
        }

        var presenterData = new SerializedObject(presenter);
        SerializedProperty encounterProperty =
            presenterData.FindProperty("waveEncounter")
            ?? throw new InvalidOperationException(
                "FarmEnemyEmergencePresenter waveEncounter field is missing.");
        encounterProperty.objectReferenceValue = encounter;
        presenterData.ApplyModifiedProperties();
        presenter.ConfigureGroundEmergence(
            true,
            1.75f,
            1.1f,
            0.08f);
        presenter.ConfigureDepthScaling(0.9f, 4f);
        EditorUtility.SetDirty(presenter);
    }

    private static void DisableLegacyCompletionTrigger(Scene scene)
    {
        GameObject legacyTrigger =
            RequirePath(scene, FarmLegacyCompletionTriggerPath);
        Collider collider = legacyTrigger.GetComponent<Collider>();

        if (collider != null)
        {
            collider.enabled = false;
            collider.isTrigger = true;
            EditorUtility.SetDirty(collider);
        }

        legacyTrigger.SetActive(false);
        EditorUtility.SetDirty(legacyTrigger);
    }

    private static void EnsureHubTravelSockets(GameObject travelPoint)
    {
        EnsureLocalSocket(
            travelPoint,
            HubTruckSocketName,
            Vector3.zero,
            Quaternion.identity,
            "sv_label_3");
        EnsureLocalSocket(
            travelPoint,
            HubTruckApproachName,
            new Vector3(0f, 0f, -5f),
            Quaternion.identity,
            "sv_label_1");
    }

    private static GameObject EnsureWorldSocket(
        GameObject parent,
        string name,
        Vector3 position,
        Quaternion rotation,
        string iconName)
    {
        GameObject socket = EnsureDirectChild(parent, name);
        socket.transform.SetPositionAndRotation(position, rotation);
        socket.transform.localScale = Vector3.one;
        MarkAsEditorSocket(socket, iconName);
        EditorUtility.SetDirty(socket.transform);
        return socket;
    }

    private static GameObject EnsureLocalSocket(
        GameObject parent,
        string name,
        Vector3 position,
        Quaternion rotation,
        string iconName)
    {
        GameObject socket = EnsureDirectChild(parent, name);
        socket.transform.SetLocalPositionAndRotation(position, rotation);
        socket.transform.localScale = Vector3.one;
        MarkAsEditorSocket(socket, iconName);
        EditorUtility.SetDirty(socket.transform);
        return socket;
    }

    private static void ResetLocalPose(Transform target)
    {
        target.SetLocalPositionAndRotation(
            Vector3.zero,
            Quaternion.identity);
        target.localScale = Vector3.one;
        EditorUtility.SetDirty(target);
    }

    private static void MarkAsEditorSocket(
        GameObject target,
        string iconName)
    {
        Texture2D icon =
            EditorGUIUtility.IconContent(iconName).image as Texture2D;

        if (icon != null)
        {
            EditorGUIUtility.SetIconForObject(target, icon);
        }

        EditorUtility.SetDirty(target);
    }

    private static void EnsureNavMeshExcluded(GameObject target)
    {
        NavMeshModifier modifier =
            EnsureComponent<NavMeshModifier>(target);
        modifier.ignoreFromBuild = true;
        modifier.applyToChildren = false;
        modifier.overrideArea = false;
        modifier.overrideGenerateLinks = false;
        EditorUtility.SetDirty(modifier);
    }

    private static void EnsureLegacyInfestationSpawnVolumeNavMeshExcluded(
        Scene scene,
        Terrain terrain)
    {
        BoxCollider collider = RequireLegacyInfestationSpawnVolume(
            scene,
            terrain);
        NavMeshModifier[] modifiers =
            collider.GetComponents<NavMeshModifier>();

        if (modifiers.Length > 1)
        {
            throw new InvalidOperationException(
                "Legacy Farm infestation spawn volume has duplicate " +
                "NavMeshModifier components.");
        }

        NavMeshModifier modifier = modifiers.Length == 1
            ? modifiers[0]
            : collider.gameObject.AddComponent<NavMeshModifier>();
        modifier.enabled = true;
        modifier.ignoreFromBuild = true;
        modifier.applyToChildren = false;
        modifier.overrideArea = false;
        modifier.area = 0;
        modifier.overrideGenerateLinks = false;
        modifier.generateLinks = false;
        EditorUtility.SetDirty(modifier);

        ValidateLegacyInfestationSpawnVolumeNavMeshExclusion(
            scene,
            terrain,
            allowMissingModifier: false);
    }

    private static void ValidateLegacyInfestationSpawnVolumeNavMeshExclusion(
        Scene scene,
        Terrain terrain,
        bool allowMissingModifier)
    {
        BoxCollider collider = RequireLegacyInfestationSpawnVolume(
            scene,
            terrain);
        NavMeshModifier[] modifiers =
            collider.GetComponents<NavMeshModifier>();

        if (allowMissingModifier && modifiers.Length == 0)
            return;

        if (modifiers.Length != 1 ||
            !modifiers[0].isActiveAndEnabled ||
            !modifiers[0].ignoreFromBuild ||
            modifiers[0].applyToChildren ||
            modifiers[0].overrideArea ||
            modifiers[0].area != 0 ||
            modifiers[0].overrideGenerateLinks ||
            modifiers[0].generateLinks ||
            !modifiers[0].AffectsAgentType(0) ||
            PrefabUtility.GetCorrespondingObjectFromSource(modifiers[0]) != null)
        {
            throw new InvalidOperationException(
                "The campaign Farm must add exactly one scene-instance " +
                "NavMeshModifier that excludes the legacy infestation spawn " +
                "volume from agent-0 baking without changing its runtime collider.");
        }
    }

    private static BoxCollider RequireLegacyInfestationSpawnVolume(
        Scene scene,
        Terrain terrain)
    {
        ValidateFarmTerrain(terrain);

        if (!string.Equals(
                AssetDatabase.AssetPathToGUID(
                    SafetyInfestationSpawnerPrefabPath),
                SafetyInfestationSpawnerPrefabGuid,
                StringComparison.Ordinal) ||
            !string.Equals(
                AssetDatabase.AssetPathToGUID(SafetyTerrainPrefabPath),
                SafetyTerrainPrefabGuid,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Online-safety Terrain or Infestation Spawner prefab identity changed; " +
                "the campaign cannot safely identify its legacy spawn volume.");
        }

        GameObject terrainSource =
            PrefabUtility.GetCorrespondingObjectFromSource(
                terrain.gameObject);
        if (terrainSource == null ||
            !string.Equals(
                AssetDatabase.GetAssetPath(terrainSource),
                SafetyTerrainPrefabPath,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Farm Terrain is not an instance of the exact online-safety " +
                "Terrain prefab that owns the nested spawn-volume override.");
        }

        InfestationSpawner[] infestations =
            FindSceneComponents<InfestationSpawner>(scene);
        if (infestations.Length != 1 ||
            infestations[0] == null ||
            infestations[0].enabled ||
            infestations[0].gameObject.name != "Infestation Spawner" ||
            infestations[0].transform.parent != terrain.transform)
        {
            throw new InvalidOperationException(
                "Farm requires the exact disabled nested Infestation Spawner " +
                "under Terrain before its legacy volume can be excluded.");
        }

        InfestationSpawner infestation = infestations[0];
        GameObject sourceRoot =
            PrefabUtility.GetCorrespondingObjectFromOriginalSource(
                infestation.gameObject);
        if (sourceRoot == null ||
            !string.Equals(
                AssetDatabase.GetAssetPath(sourceRoot),
                SafetyInfestationSpawnerPrefabPath,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Farm nested Infestation Spawner is not an instance of the " +
                "expected online-safety prefab.");
        }

        BoxCollider[] solidVolumes = infestation
            .GetComponentsInChildren<BoxCollider>(true)
            .Where(candidate =>
                candidate != null &&
                candidate.enabled &&
                !candidate.isTrigger)
            .ToArray();

        if (solidVolumes.Length != 1)
        {
            throw new InvalidOperationException(
                "Farm nested Infestation Spawner must retain exactly one " +
                "enabled solid legacy spawn volume.");
        }

        BoxCollider collider = solidVolumes[0];
        GameObject sourceChild =
            PrefabUtility.GetCorrespondingObjectFromOriginalSource(
                collider.gameObject);
        BoxCollider terrainPrefabCollider =
            PrefabUtility.GetCorrespondingObjectFromSource(collider);
        Bounds bounds = collider.bounds;
        GlobalObjectId originalChildId = sourceChild != null
            ? GlobalObjectId.GetGlobalObjectIdSlow(sourceChild)
            : default;
        GlobalObjectId terrainColliderId = terrainPrefabCollider != null
            ? GlobalObjectId.GetGlobalObjectIdSlow(terrainPrefabCollider)
            : default;

        if (collider.gameObject.name != LegacyInfestationSpawnVolumeName ||
            collider.transform.parent != infestation.transform ||
            collider.gameObject.layer != 0 ||
            collider.gameObject.tag != "Untagged" ||
            !collider.gameObject.activeInHierarchy ||
            collider.GetComponents<BoxCollider>().Length != 1 ||
            !Approximately(collider.center, Vector3.zero) ||
            !Approximately(
                collider.size,
                LegacyInfestationSpawnVolumeWorldBounds.size) ||
            !Approximately(
                bounds.center,
                LegacyInfestationSpawnVolumeWorldBounds.center) ||
            !Approximately(
                bounds.size,
                LegacyInfestationSpawnVolumeWorldBounds.size) ||
            collider.includeLayers.value != 0 ||
            collider.excludeLayers.value != -1 ||
            sourceChild == null ||
            !string.Equals(
                AssetDatabase.GetAssetPath(sourceChild),
                SafetyInfestationSpawnerPrefabPath,
                StringComparison.Ordinal) ||
            originalChildId.targetObjectId !=
                SafetyInfestationSpawnVolumeGameObjectFileId ||
            terrainPrefabCollider == null ||
            !string.Equals(
                AssetDatabase.GetAssetPath(terrainPrefabCollider),
                SafetyTerrainPrefabPath,
                StringComparison.Ordinal) ||
            terrainColliderId.targetObjectId !=
                SafetyTerrainSpawnVolumeColliderFileId)
        {
            throw new InvalidOperationException(
                "Farm legacy infestation collider drifted from the exact " +
                "online-safety SpawnInfest 2 nested Terrain-prefab override " +
                "contract " +
                "(default-layer 75x1x125 bounds centered at " +
                "57.4,0.57,-31.3 with runtime collision exclusion intact).");
        }

        foreach (Vector2 approach in ChoreApproachWorldXZ)
        {
            if (approach.x < bounds.min.x ||
                approach.x > bounds.max.x ||
                approach.y < bounds.min.z ||
                approach.y > bounds.max.z)
            {
                throw new InvalidOperationException(
                    "Legacy infestation spawn volume no longer covers every " +
                    "Farm chore approach, so its known NavMesh-lid migration " +
                    "is not applicable.");
            }
        }

        return collider;
    }

    private static Vector3 Average(IReadOnlyList<Vector3> values)
    {
        if (values == null || values.Count == 0)
            return Vector3.zero;

        Vector3 total = Vector3.zero;

        for (int index = 0; index < values.Count; index++)
        {
            total += values[index];
        }

        return total / values.Count;
    }

    private static Vector3[] GetTerrainGroundPositions(
        Terrain terrain,
        IReadOnlyList<Vector2> worldXZ)
    {
        var positions = new Vector3[worldXZ.Count];

        for (int index = 0; index < positions.Length; index++)
        {
            positions[index] = GetTerrainGroundPosition(
                terrain,
                worldXZ[index]);
        }

        return positions;
    }

    private static Vector3 GetTerrainGroundPosition(
        Terrain terrain,
        Vector2 worldXZ,
        float heightOffset = 0f)
    {
        ValidateFarmTerrain(terrain);
        Vector3 terrainOrigin = terrain.transform.position;
        Vector3 terrainSize = terrain.terrainData.size;

        if (worldXZ.x < terrainOrigin.x - TerrainGroundTolerance ||
            worldXZ.x > terrainOrigin.x + terrainSize.x +
                TerrainGroundTolerance ||
            worldXZ.y < terrainOrigin.z - TerrainGroundTolerance ||
            worldXZ.y > terrainOrigin.z + terrainSize.z +
                TerrainGroundTolerance)
        {
            throw new InvalidOperationException(
                $"Farm ground sample ({worldXZ.x:F2}, {worldXZ.y:F2}) is " +
                "outside the authored Terrain bounds.");
        }

        Vector3 sample = new(worldXZ.x, terrainOrigin.y, worldXZ.y);
        float groundY = terrain.SampleHeight(sample) + terrainOrigin.y;
        return new Vector3(worldXZ.x, groundY + heightOffset, worldXZ.y);
    }

    private static void ValidateFarmTerrain(Terrain terrain)
    {
        TerrainCollider terrainCollider =
            terrain != null ? terrain.GetComponent<TerrainCollider>() : null;

        if (terrain == null ||
            terrain.terrainData == null ||
            !terrain.isActiveAndEnabled ||
            terrainCollider == null ||
            !terrainCollider.enabled ||
            terrainCollider.terrainData != terrain.terrainData ||
            LayerMask.NameToLayer("Terrain") != FarmTerrainLayerIndex ||
            terrain.gameObject.layer != FarmTerrainLayerIndex)
        {
            throw new InvalidOperationException(
                "Farm campaign grounding requires exactly one active Terrain " +
                "on the project's Terrain layer with a matching enabled " +
                "TerrainCollider and TerrainData.");
        }
    }

    private static void ValidateTerrainGrounded(
        Transform target,
        Terrain terrain,
        string description)
    {
        Vector3 position = target.position;
        Vector3 expected = GetTerrainGroundPosition(
            terrain,
            new Vector2(position.x, position.z));

        if (Mathf.Abs(position.y - expected.y) > TerrainGroundTolerance)
        {
            throw new InvalidOperationException(
                $"{description} '{target.name}' is {position.y - expected.y:F2}m " +
                "from the Farm terrain instead of sitting on the ground.");
        }
    }

    private static FarmChoreInteractable[] EnsureChores(
        GameObject objectivesRoot,
        FarmPrologueDirector director,
        Terrain terrain,
        IReadOnlyList<Vector3> choreWorldPositions,
        IReadOnlyList<Vector3> approachWorldPositions)
    {
        if (choreWorldPositions.Count != ChoreSteps.Length ||
            approachWorldPositions.Count != ChoreSteps.Length)
        {
            throw new InvalidOperationException(
                "Farm chore step authoring requires one grounded root and " +
                "approach position for every deterministic step.");
        }

        var groups = new Dictionary<string, GameObject>(StringComparer.Ordinal);

        for (int groupIndex = 0;
             groupIndex < ChoreGroupNames.Length;
             groupIndex++)
        {
            string groupName = ChoreGroupNames[groupIndex];
            GameObject group = EnsureDirectChild(objectivesRoot, groupName);
            RemoveRecognizedDuplicateChoreSteps(group, groupName);
            RemoveAllComponents<FarmChoreInteractable>(group);
            RemoveAllComponents<Collider>(group);
            RemoveAllComponents<NavMeshModifier>(group);
            DestroyDirectChildIfPresent(group, PropSocketName);
            DestroyDirectChildIfPresent(group, PlayerApproachName);
            group.layer = 0;
            group.tag = "Untagged";
            ResetLocalPose(group.transform);
            if (string.Equals(
                    groupName,
                    ChoreGroupNames[1],
                    StringComparison.Ordinal))
            {
                group.transform.localPosition =
                    WednesdayMuckGroupLocalPosition;
            }
            group.transform.SetSiblingIndex(groupIndex);
            EditorUtility.SetDirty(group.transform);
            groups.Add(groupName, group);
        }

        var chores = new FarmChoreInteractable[ChoreSteps.Length];
        var groupStepIndices = ChoreGroupNames.ToDictionary(
            name => name,
            _ => 0,
            StringComparer.Ordinal);

        for (int index = 0; index < ChoreSteps.Length; index++)
        {
            ChoreStepDefinition definition = ChoreSteps[index];
            GameObject group = groups[definition.GroupName];
            GameObject choreObject =
                EnsureDirectChild(group, definition.Name);
            choreObject.transform.SetPositionAndRotation(
                choreWorldPositions[index],
                definition.Rotation);
            choreObject.transform.localScale = definition.Scale;
            choreObject.transform.SetSiblingIndex(
                groupStepIndices[definition.GroupName]++);
            choreObject.layer = RequireInteractableLayer();
            choreObject.tag = InteractTag;
            EditorUtility.SetDirty(choreObject.transform);

            BoxCollider collider = EnsureComponent<BoxCollider>(choreObject);
            collider.enabled = true;
            collider.isTrigger = false;
            collider.center = definition.ColliderCenter;
            collider.size = definition.ColliderSize;
            EditorUtility.SetDirty(collider);
            EnsureNavMeshExcluded(choreObject);

            FarmChoreInteractable chore =
                EnsureComponent<FarmChoreInteractable>(choreObject);
            chore.Configure(
                director,
                definition.Id,
                definition.Objective,
                1);
            chore.ConfigureInventoryRequirement(
                false,
                string.Empty,
                1,
                FarmInventoryConsumptionMode.KeepItems);

            EnsureWorldSocket(
                choreObject,
                PropSocketName,
                choreWorldPositions[index],
                Quaternion.identity,
                "sv_label_3");
            EnsureWorldSocket(
                choreObject,
                PlayerApproachName,
                approachWorldPositions[index],
                Quaternion.identity,
                "sv_label_1");
            ValidateTerrainGrounded(
                choreObject.transform,
                terrain,
                "Farm chore step");
            chores[index] = chore;
            EditorUtility.SetDirty(chore);
        }

        return chores;
    }

    private static void RemoveRecognizedDuplicateChoreSteps(
        GameObject group,
        string groupName)
    {
        ChoreStepDefinition[] expected = ChoreSteps
            .Where(step => step.GroupName == groupName)
            .ToArray();
        HashSet<string> exactNames = expected
            .Select(step => step.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (Transform child in group.transform.Cast<Transform>()
                     .ToArray())
        {
            if (!child.name.StartsWith("STEP_", StringComparison.Ordinal) ||
                exactNames.Contains(child.name))
            {
                continue;
            }

            FarmChoreInteractable chore =
                child.GetComponent<FarmChoreInteractable>();
            ChoreStepDefinition matching = expected.SingleOrDefault(step =>
                child.name.StartsWith(
                    step.Name + " (",
                    StringComparison.Ordinal) &&
                chore != null &&
                chore.ChoreId == step.Id);
            if (string.IsNullOrEmpty(matching.Id))
            {
                throw new InvalidOperationException(
                    $"Farm chore group '{groupName}' contains unknown step " +
                    $"'{child.name}'; only exact duplicate marker cleanup is " +
                    "authorized.");
            }

            UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
    }

    private static void RemoveAllComponents<T>(GameObject target)
        where T : Component
    {
        foreach (T component in target.GetComponents<T>())
        {
            UnityEngine.Object.DestroyImmediate(component);
        }
    }

    private static void DestroyDirectChildIfPresent(
        GameObject parent,
        string childName)
    {
        GameObject child = FindDirectChild(parent, childName);

        if (child != null)
            UnityEngine.Object.DestroyImmediate(child);
    }

    private static void EnsureAuthoredFarmPresentation(
        Scene scene,
        FarmPrologueDirector director,
        out CanvasGroup fader,
        out FarmObjectivePresenter presenter)
    {
        gameManager manager = RequireSingleComponent<gameManager>(scene);
        GameObject uiRoot = manager.gameObject;
        GameObject faderObject = FindDirectChild(uiRoot, "Campaign Screen Fader");

        if (faderObject == null)
        {
            throw new InvalidOperationException(
                "The authored UI must already contain a direct child named " +
                "'Campaign Screen Fader'. Campaign Foundation never creates UI.");
        }

        faderObject.layer = LayerMask.NameToLayer("UI");
        faderObject.transform.SetAsLastSibling();

        RectTransform rect = faderObject.GetComponent<RectTransform>()
            ?? throw new InvalidOperationException(
                "Authored Campaign Screen Fader requires a RectTransform.");
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;

        Image image = faderObject.GetComponent<Image>()
            ?? throw new InvalidOperationException(
                "Authored Campaign Screen Fader requires an Image.");
        image.color = Color.black;
        image.raycastTarget = true;

        fader = faderObject.GetComponent<CanvasGroup>()
            ?? throw new InvalidOperationException(
                "Authored Campaign Screen Fader requires a CanvasGroup.");
        fader.alpha = 0f;
        fader.interactable = false;
        fader.blocksRaycasts = false;
        fader.ignoreParentGroups = false;

        presenter = EnsureComponent<FarmObjectivePresenter>(uiRoot);
        presenter.Configure(
            director,
            RequireNamedText(scene, "Game Goal Label"),
            RequireNamedText(scene, "Game Goal Data"));
    }

    private static void WireOpenWorld(
        Scene scene,
        GameObject normalizedSafetyPlayer)
    {
        ValidateSafetyItemDatabaseSource(scene);
        ValidateNormalizedOpenWorldPlayerBeforeWiring(
            scene,
            normalizedSafetyPlayer);

        CampaignStateService state = EnsureRootService(scene);
        EnsureLoadoutEquipmentBridge(
            scene,
            state,
            state.GetComponent<CampaignInventoryCarryover>());
        Transform player = normalizedSafetyPlayer.transform;
        DisableNestedPlayerServiceBehaviours(player);
        GameObject progressionObject =
            RequirePath(scene, OpenWorldProgressionPath);
        CampaignOpenWorldProgression progression =
            EnsureComponent<CampaignOpenWorldProgression>(progressionObject);

        OpenWorldAreaBarrier[] barriers =
            FindSceneComponents<OpenWorldAreaBarrier>(scene)
                .OrderBy(barrier => (int)barrier.Area)
                .ToArray();

        if (barriers.Length != 3)
        {
            throw new InvalidOperationException(
                $"Open World requires exactly three area barriers; found {barriers.Length}.");
        }

        GameObject[] missionRoots = OpenWorldMissionSystemNames
            .Select(name => RequireSingleNamedObject(scene, name))
            .ToArray();
        progression.Configure(state, barriers, missionRoots);
        EditorUtility.SetDirty(progression);

        gameManager manager = RequireSingleComponent<gameManager>(scene);
        TMP_Text lockedAreaText = RequireNamedText(scene, "Game Goal Data");
        CampaignLockedAreaFeedbackPresenter feedbackPresenter =
            EnsureComponent<CampaignLockedAreaFeedbackPresenter>(
                manager.gameObject);
        feedbackPresenter.Configure(lockedAreaText, null, 3f);
        EditorUtility.SetDirty(feedbackPresenter);

        for (int index = 0;
             index < OpenWorldMissionSystemNames.Length;
             index++)
        {
            GameObject missionRoot = missionRoots[index];
            CampaignAreaCompletionRelay relay =
                EnsureComponent<CampaignAreaCompletionRelay>(missionRoot);
            relay.Configure(progression, OpenWorldMissionAreaIds[index]);
            EditorUtility.SetDirty(relay);
        }

        foreach (OpenWorldAreaBarrier barrier in barriers)
        {
            GameObject feedbackTrigger = barrier.LockedFeedbackTrigger;

            if (feedbackTrigger == null)
            {
                throw new InvalidOperationException(
                    $"Open-world barrier '{barrier.name}' has no authored " +
                    "locked-feedback trigger.");
            }

            // Feedback triggers live under the original gate hierarchy rather
            // than under the generated barrier root. Give each one its own
            // explicit bake exclusion so a future collider-based NavMesh bake
            // cannot turn a temporary player message volume into navigation
            // geometry.
            EnsureNavMeshExcluded(feedbackTrigger);

            CampaignLockedAreaFeedbackTrigger feedback =
                EnsureComponent<CampaignLockedAreaFeedbackTrigger>(
                    feedbackTrigger);
            feedback.Configure(
                ToCampaignArea(barrier.Area),
                feedbackPresenter);
            EditorUtility.SetDirty(feedback);
        }

        GameObject arrivalObject = RequirePath(scene, OpenWorldArrivalPath);
        Transform fallbackSpawn =
            RequireTaggedObject(scene, "PlayerSpawnPos").transform;
        SetWorldPoseIfDifferent(player, arrivalObject.transform);
        SetWorldPoseIfDifferent(fallbackSpawn, arrivalObject.transform);

        playerController[] movementComponents =
            normalizedSafetyPlayer.GetComponents<playerController>();
        if (movementComponents.Length != 1 ||
            !movementComponents[0].enabled)
        {
            throw new InvalidOperationException(
                "Open World normalized Safety Player requires exactly one " +
                "enabled root playerController.");
        }

        playerController movement = movementComponents[0];
        manager.player = player.gameObject;
        manager.playerController = movement;
        manager.playerSpawnPos = fallbackSpawn.gameObject;
        EditorUtility.SetDirty(manager);

        CampaignSpawnPoint arrival =
            EnsureComponent<CampaignSpawnPoint>(arrivalObject);
        arrival.Configure("BlackPinesArrival", arrivalObject.transform, true);
        EditorUtility.SetDirty(arrival);

        EnsureOpenWorldSafetyTruck(scene);
        GameObject travelBackend = EnsureOpenWorldTravelBackend(scene);
        CampaignSceneTravel returnTravel = ConfigureTravelBackend(
            travelBackend,
            CampaignSceneNames.FarmPrologueHub,
            "FarmHub",
            false,
            CampaignAreaId.BlackPines);
        RemoveRecognizedOpenWorldExtractionBypasses(scene, returnTravel);
        ConfigureSafetyExtractionMenu(
            scene,
            returnTravel,
            "Return to Farm Hub",
            SafetyExtractionMenuOption.FarmHub);

        EnsureSignature(scene);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static CampaignSceneTravel ConfigureTravelBackend(
        GameObject travelObject,
        string destinationScene,
        string destinationId,
        bool requireUnlock,
        CampaignAreaId area)
    {
        travelObject.layer = 0;
        travelObject.tag = "Untagged";
        travelObject.SetActive(true);

        foreach (CampaignTravelInteractable interactable in
                 travelObject.GetComponents<CampaignTravelInteractable>())
        {
            UnityEngine.Object.DestroyImmediate(interactable);
        }

        foreach (Collider collider in travelObject.GetComponents<Collider>())
            UnityEngine.Object.DestroyImmediate(collider);

        foreach (NavMeshModifier modifier in
                 travelObject.GetComponents<NavMeshModifier>())
        {
            UnityEngine.Object.DestroyImmediate(modifier);
        }

        if (travelObject.GetComponents<MonoBehaviour>()
            .Any(component => component is IInteract))
        {
            throw new InvalidOperationException(
                $"Travel backend '{travelObject.name}' still exposes a " +
                "competing IInteract " +
                "after the online-safety extraction migration.");
        }

        CampaignSceneTravel travel =
            EnsureComponent<CampaignSceneTravel>(travelObject);
        travel.ConfigureDestination(
            destinationScene,
            destinationId,
            requireUnlock,
            area);
        EditorUtility.SetDirty(travelObject);
        EditorUtility.SetDirty(travel);
        return travel;
    }

    private static void ConfigureSafetyExtractionMenu(
        Scene scene,
        CampaignSceneTravel travel,
        string optionLabel,
        SafetyExtractionMenuOption option)
    {
        gameManager manager = RequireSingleComponent<gameManager>(scene);
        EnsureSafetyExtractionGateAuthored(manager);
        EnsureSafetyLoseLifecycleBridgeAuthored(scene, manager);
        SerializedProperty menuProperty =
            new SerializedObject(manager).FindProperty("menuExtraction");
        GameObject menu = menuProperty?.objectReferenceValue as GameObject;
        RequireSafetyExtractionMenuSource(menu);

        Button[] travelButtons =
        {
            RequireSafetyExtractionButton(
                menu,
                "OpenLevel1",
                SafetyExtractionOpenLevel1ButtonSourceFileId),
            RequireSafetyExtractionButton(
                menu,
                "OpenLevel2",
                SafetyExtractionOpenLevel2ButtonSourceFileId),
            RequireSafetyExtractionButton(
                menu,
                "OpenLevel3",
                SafetyExtractionOpenLevel3ButtonSourceFileId),
            RequireSafetyExtractionButton(
                menu,
                "OpenLevel4",
                SafetyExtractionOpenLevel4ButtonSourceFileId),
            RequireSafetyExtractionButton(
                menu,
                "OpenLevel4 (1)",
                SafetyExtractionHubButtonSourceFileId),
            RequireSafetyExtractionButton(
                menu,
                "ReturnToHub",
                SafetyExtractionOpenWorldButtonSourceFileId),
            RequireSafetyExtractionButton(
                menu,
                "ReturnToHub (1)",
                SafetyExtractionFarmHubButtonSourceFileId)
        };
        Button close = RequireSafetyExtractionButton(
            menu,
            "Close",
            SafetyExtractionCloseButtonSourceFileId);
        int activeIndex = option == SafetyExtractionMenuOption.BlackPines
            ? 5
            : 6;
        Button active = travelButtons[activeIndex];

        for (int index = 0; index < travelButtons.Length; index++)
        {
            Button button = travelButtons[index];
            while (button.onClick.GetPersistentEventCount() > 0)
            {
                UnityEventTools.RemovePersistentListener(
                    button.onClick,
                    button.onClick.GetPersistentEventCount() - 1);
            }

            bool isActive = index == activeIndex;
            button.gameObject.SetActive(isActive);
            button.enabled = true;
            button.interactable = isActive;
            EditorUtility.SetDirty(button);
        }

        UnityEventTools.AddPersistentListener(
            active.onClick,
            travel.TravelFromSafetyExtractionMenu);

        TMP_Text[] labels =
            active.GetComponentsInChildren<TMP_Text>(true);
        if (labels.Length != 1)
        {
            throw new InvalidOperationException(
                $"Online-safety extraction button '{active.name}' no longer has " +
                "one exact TMP label.");
        }

        labels[0].text = optionLabel;
        EditorUtility.SetDirty(labels[0]);

        close.gameObject.SetActive(true);
        menu.SetActive(false);
        EditorUtility.SetDirty(active);
        EditorUtility.SetDirty(menu);

        ValidateSafetyExtractionMenu(scene, travel, optionLabel, option);
    }

    private static void ValidateTravelBackend(
        GameObject travelObject,
        string sceneName,
        string destinationId)
    {
        CampaignSceneTravel travel =
            travelObject.GetComponent<CampaignSceneTravel>();
        if (travel == null ||
            travel.DestinationSceneName != sceneName ||
            travel.SpawnDestinationId != destinationId ||
            travelObject.layer != 0 ||
            !travelObject.CompareTag("Untagged") ||
            travelObject.GetComponents<Collider>().Length != 0 ||
            travelObject.GetComponents<NavMeshModifier>().Length != 0 ||
            travelObject.GetComponents<CampaignTravelInteractable>().Length != 0 ||
            travelObject.GetComponents<MonoBehaviour>()
                .Any(component => component is IInteract))
        {
            throw new InvalidOperationException(
                $"Travel backend '{travelObject.name}' must remain a " +
                "non-interactive owned " +
                "save/carryover backend for the online-safety extraction menu.");
        }
    }

    private static void ValidateSafetyExtractionMenu(
        Scene scene,
        CampaignSceneTravel travel,
        string optionLabel,
        SafetyExtractionMenuOption option)
    {
        gameManager manager = RequireSingleComponent<gameManager>(scene);
        SerializedProperty menuProperty =
            new SerializedObject(manager).FindProperty("menuExtraction");
        GameObject menu = menuProperty?.objectReferenceValue as GameObject;
        RequireSafetyExtractionMenuSource(menu);

        Button[] travelButtons =
        {
            RequireSafetyExtractionButton(
                menu,
                "OpenLevel1",
                SafetyExtractionOpenLevel1ButtonSourceFileId),
            RequireSafetyExtractionButton(
                menu,
                "OpenLevel2",
                SafetyExtractionOpenLevel2ButtonSourceFileId),
            RequireSafetyExtractionButton(
                menu,
                "OpenLevel3",
                SafetyExtractionOpenLevel3ButtonSourceFileId),
            RequireSafetyExtractionButton(
                menu,
                "OpenLevel4",
                SafetyExtractionOpenLevel4ButtonSourceFileId),
            RequireSafetyExtractionButton(
                menu,
                "OpenLevel4 (1)",
                SafetyExtractionHubButtonSourceFileId),
            RequireSafetyExtractionButton(
                menu,
                "ReturnToHub",
                SafetyExtractionOpenWorldButtonSourceFileId),
            RequireSafetyExtractionButton(
                menu,
                "ReturnToHub (1)",
                SafetyExtractionFarmHubButtonSourceFileId)
        };
        Button close = RequireSafetyExtractionButton(
            menu,
            "Close",
            SafetyExtractionCloseButtonSourceFileId);
        int activeIndex = option == SafetyExtractionMenuOption.BlackPines
            ? 5
            : 6;
        Button active = travelButtons[activeIndex];
        TMP_Text[] labels =
            active.GetComponentsInChildren<TMP_Text>(true);

        if (menu.activeSelf ||
            !active.gameObject.activeSelf ||
            !active.enabled ||
            !active.interactable ||
            active.onClick.GetPersistentEventCount() != 1 ||
            active.onClick.GetPersistentTarget(0) != travel ||
            active.onClick.GetPersistentMethodName(0) !=
                nameof(CampaignSceneTravel.TravelFromSafetyExtractionMenu) ||
            active.onClick.GetPersistentListenerState(0) ==
                UnityEventCallState.Off ||
            labels.Length != 1 ||
            labels[0].text != optionLabel)
        {
            throw new InvalidOperationException(
                $"Online-safety extraction '{active.name}' must be the one active " +
                $"'{optionLabel}' option wired to the durable campaign " +
                "backend.");
        }

        for (int index = 0; index < travelButtons.Length; index++)
        {
            if (index == activeIndex)
                continue;

            Button button = travelButtons[index];
            if (button.gameObject.activeSelf ||
                button.interactable ||
                button.onClick.GetPersistentEventCount() != 0)
            {
                throw new InvalidOperationException(
                    $"Online-safety extraction option '{button.name}' must " +
                    "remain hidden, non-interactable, and free of campaign " +
                    "callbacks until that destination exists.");
            }
        }

        Button[] exactMenuButtons = travelButtons.Append(close).ToArray();
        Button[] authoredMenuButtons =
            menu.GetComponentsInChildren<Button>(true);
        if (authoredMenuButtons.Length != exactMenuButtons.Length ||
            !authoredMenuButtons.ToHashSet().SetEquals(exactMenuButtons))
        {
            throw new InvalidOperationException(
                "Online-safety extraction menu must retain exactly its four " +
                "region buttons, legacy Hub button, two current Safety " +
                "travel buttons, and Close button. Found: " +
                string.Join(", ", authoredMenuButtons.Select(button =>
                    $"'{button.name}'")) + ".");
        }

        if (!close.gameObject.activeSelf ||
            close.onClick.GetPersistentEventCount() != 1 ||
            close.onClick.GetPersistentTarget(0) is not buttonFunctions ||
            close.onClick.GetPersistentMethodName(0) != "ExtractionClose" ||
            close.onClick.GetPersistentListenerState(0) ==
                UnityEventCallState.Off)
        {
            throw new InvalidOperationException(
                "Online-safety extraction Close button must retain its exact " +
                "Safety buttonFunctions.ExtractionClose callback.");
        }
    }

    private static void ValidateOpenWorldExtractionState(
        Scene scene,
        bool allowLegacyMigration)
    {
        GameObject[] legacyTrucks = FindSceneNamedObjects(
            scene,
            LegacyOpenWorldReturnTruckName);
        GameObject[] safetyTrucks = FindPrefabInstanceRoots(
            scene,
            SafetyTruckEscapePrefabPath);
        GameObject[] backends = FindSceneNamedObjects(
            scene,
            OpenWorldTravelBackendName);

        bool exactLegacyState =
            legacyTrucks.Length == 1 &&
            safetyTrucks.Length == 0 &&
            backends.Length == 0;
        if (allowLegacyMigration && exactLegacyState)
        {
            ValidateLegacyOpenWorldReturnTruck(scene, legacyTrucks[0]);
            ValidateRecognizedOpenWorldCompletionTravelBypasses(scene);
            return;
        }

        if (legacyTrucks.Length != 0 ||
            safetyTrucks.Length != 1 ||
            backends.Length != 1)
        {
            throw new InvalidOperationException(
                "Open World extraction must contain exactly one connected " +
                "online-safety truck, one owned backend, and zero legacy " +
                "Return Truck roots after migration.");
        }

        SafetyTruckExtractionBinding binding =
            RequireSafetyTruckExtractionBinding(scene);
        ValidateOpenWorldSafetyTruckPlacement(
            binding,
            RequirePath(scene, OpenWorldCorePath));

        GameObject backendObject = RequirePath(
            scene,
            OpenWorldTravelBackendPath);
        CampaignSceneTravel backend =
            backendObject.GetComponent<CampaignSceneTravel>();
        ValidateTravelBackend(
            backendObject,
            CampaignSceneNames.FarmPrologueHub,
            "FarmHub");
        if (backendObject.transform.parent !=
                RequirePath(scene, OpenWorldCorePath).transform ||
            backendObject.transform.childCount != 0 ||
            backendObject.GetComponents<Component>().Length != 2 ||
            !backendObject.activeSelf ||
            !Approximately(backendObject.transform.localPosition, Vector3.zero) ||
            Quaternion.Angle(
                backendObject.transform.localRotation,
                Quaternion.identity) > 0.001f ||
            !Approximately(backendObject.transform.localScale, Vector3.one))
        {
            throw new InvalidOperationException(
                "Open World Safety extraction backend must remain one active, " +
                "empty direct _CORE child containing only Transform and the " +
                "durable CampaignSceneTravel backend.");
        }

        ValidateSafetyExtractionMenu(
            scene,
            backend,
            "Return to Farm Hub",
            SafetyExtractionMenuOption.FarmHub);
        ValidateSceneExtractionAuthority(scene, backend);
    }

    private static void ValidateRecognizedOpenWorldCompletionTravelBypasses(
        Scene scene)
    {
        CampaignTravelInteractable[] interactables =
            FindSceneComponents<CampaignTravelInteractable>(scene);
        WorldMissionCompletionTravel[] completion =
            FindSceneComponents<WorldMissionCompletionTravel>(scene);
        CampaignSceneTravel[] travel =
            FindSceneComponents<CampaignSceneTravel>(scene);

        if (interactables.Length != 1 ||
            interactables[0].gameObject.name !=
                LegacyOpenWorldReturnTruckName ||
            completion.Length != 0 &&
            completion.Length != OpenWorldMissionSystemNames.Length ||
            travel.Length != completion.Length + 1 ||
            FindSceneComponents<lockedExtraction>(scene).Length != 0 ||
            FindSceneComponents<TruckEscapeEnding>(scene).Length != 0 ||
            FindSceneComponents<TruckEscapeKeyPickup>(scene).Length != 0)
        {
            throw new InvalidOperationException(
                "Open World legacy extraction topology is mixed, duplicated, " +
                "or not a recognized direct-truck/mission-auto-return source.");
        }

        foreach (WorldMissionCompletionTravel bypass in completion)
        {
            GameObject host = bypass.gameObject;
            CampaignSceneTravel[] pairedTravel =
                host.GetComponents<CampaignSceneTravel>();
            if (host.transform.parent == null ||
                !host.transform.parent.name.StartsWith(
                    AlphaWorldCompletionTravelRootPrefix,
                    StringComparison.Ordinal) ||
                !host.activeInHierarchy ||
                host.transform.childCount != 0 ||
                host.GetComponents<Component>().Length != 3 ||
                pairedTravel.Length != 1 ||
                bypass.MissionDirector == null ||
                bypass.SceneTravel != pairedTravel[0] ||
                pairedTravel[0].DestinationSceneName !=
                    CampaignSceneNames.FarmPrologueHub ||
                pairedTravel[0].SpawnDestinationId != "FarmHub")
            {
                throw new InvalidOperationException(
                    "Open World legacy mission-completion travel is not the " +
                    "exact recognized auto-return migration source.");
            }
        }

        if (completion.Length != 0 &&
            completion.Select(item => item.MissionDirector)
                .Distinct()
                .Count() != OpenWorldMissionSystemNames.Length)
        {
            throw new InvalidOperationException(
                "Open World legacy auto-return bindings do not map one-to-one " +
                "to the four mission directors.");
        }
    }

    private static void ValidateSceneExtractionAuthority(
        Scene scene,
        CampaignSceneTravel retainedBackend)
    {
        CampaignSceneTravel[] travel =
            FindSceneComponents<CampaignSceneTravel>(scene);
        lockedExtraction[] safetyInteractions =
            FindSceneComponents<lockedExtraction>(scene);
        if (retainedBackend == null ||
            travel.Length != 1 ||
            travel[0] != retainedBackend ||
            safetyInteractions.Length != 1 ||
            FindSceneComponents<CampaignTravelInteractable>(scene).Length != 0 ||
            FindSceneComponents<WorldMissionCompletionTravel>(scene).Length != 0 ||
            FindSceneComponents<TruckEscapeEnding>(scene).Length != 0 ||
            FindSceneComponents<TruckEscapeKeyPickup>(scene).Length != 0)
        {
            throw new InvalidOperationException(
                $"Scene '{scene.name}' must use one online-safety " +
                "lockedExtraction and one non-interactive " +
                "CampaignSceneTravel backend, with zero custom extraction " +
                "IInteract, mission-auto-return, or legacy truck/key " +
                "components.");
        }
    }

    private static bool EnsureSafetyExtractionGateAuthored(
        gameManager manager)
    {
        ValidateSafetyExtractionGate(manager, allowLegacyZero: true);

        SerializedObject managerData = new(manager);
        SerializedProperty defensesBeat =
            managerData.FindProperty(nameof(gameManager.DefensesBeat));
        if (defensesBeat.intValue == SafetyExtractionRequiredDefensesBeat &&
            !defensesBeat.prefabOverride)
            return false;

        if (defensesBeat.intValue == SafetyExtractionRequiredDefensesBeat &&
            defensesBeat.prefabOverride)
        {
            PrefabUtility.RevertPropertyOverride(
                defensesBeat,
                InteractionMode.AutomatedAction);
            EditorUtility.SetDirty(manager);
            ValidateSafetyExtractionGate(manager, allowLegacyZero: false);
            return true;
        }

        defensesBeat.intValue = SafetyExtractionRequiredDefensesBeat;
        managerData.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
        EditorUtility.SetDirty(manager);

        ValidateSafetyExtractionGate(manager, allowLegacyZero: false);
        return true;
    }

    private static void ValidateSafetyExtractionGate(
        gameManager manager,
        bool allowLegacyZero)
    {
        if (!string.Equals(
                AssetDatabase.AssetPathToGUID(SafetyGameManagerScriptPath),
                SafetyGameManagerScriptGuid,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Online-safety gameManager script identity changed while " +
                "validating the campaign extraction gate.");
        }

        string sourceText = File.ReadAllText(
            ToAbsolutePath(SafetyGameManagerScriptPath));
        bool exactFieldContract = Regex.IsMatch(
            sourceText,
            @"public\s+int\s+DefensesBeat\s*=\s*0\s*;");
        bool exactExtractionContract = Regex.IsMatch(
            sourceText,
            @"public\s+void\s+ExtractionMenu\s*\(\s*bool\s+isOn\s*\)\s*" +
            @"\{\s*if\s*\(\s*DefensesBeat\s*>=\s*1\s*\)");

        GameObject instanceRoot = manager != null
            ? PrefabUtility.GetOutermostPrefabInstanceRoot(manager.gameObject)
            : null;
        GameObject sourceRoot = instanceRoot != null
            ? PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot)
            : null;
        gameManager sourceManager = manager != null
            ? PrefabUtility.GetCorrespondingObjectFromSource(manager)
            : null;
        MonoScript sourceScript = sourceManager != null
            ? MonoScript.FromMonoBehaviour(sourceManager)
            : null;
        GlobalObjectId sourceManagerId = sourceManager != null
            ? GlobalObjectId.GetGlobalObjectIdSlow(sourceManager)
            : default;
        SerializedObject managerData = manager != null
            ? new SerializedObject(manager)
            : null;
        SerializedProperty defensesBeat = managerData?.FindProperty(
            nameof(gameManager.DefensesBeat));
        SerializedProperty sourceDefensesBeat = sourceManager != null
            ? new SerializedObject(sourceManager).FindProperty(
                nameof(gameManager.DefensesBeat))
            : null;

        if (!exactFieldContract ||
            !exactExtractionContract ||
            instanceRoot == null ||
            PrefabUtility.GetPrefabInstanceStatus(instanceRoot) !=
                PrefabInstanceStatus.Connected ||
            sourceRoot == null ||
            !string.Equals(
                AssetDatabase.GetAssetPath(sourceRoot),
                SafetyUiPrefabPath,
                StringComparison.Ordinal) ||
            sourceManager == null ||
            !string.Equals(
                AssetDatabase.GetAssetPath(sourceManager),
                SafetyUiPrefabPath,
                StringComparison.Ordinal) ||
            sourceManagerId.targetObjectId != SafetyGameManagerSourceFileId ||
            sourceScript == null ||
            !string.Equals(
                AssetDatabase.GetAssetPath(sourceScript),
                SafetyGameManagerScriptPath,
                StringComparison.Ordinal) ||
            sourceDefensesBeat == null ||
            sourceDefensesBeat.intValue != SafetyExtractionRequiredDefensesBeat ||
            defensesBeat == null)
        {
            throw new InvalidOperationException(
                "Campaign extraction requires the exact connected online-safety " +
                "UI.prefab gameManager, its authored DefensesBeat field, " +
                "and the guarded ExtractionMenu(bool) contract.");
        }

        bool exactCurrent =
            defensesBeat.intValue == SafetyExtractionRequiredDefensesBeat &&
            !defensesBeat.prefabOverride;
        bool exactLegacy =
            allowLegacyZero &&
            defensesBeat.intValue == SafetyExtractionRequiredDefensesBeat &&
            defensesBeat.prefabOverride;
        if (!exactCurrent && !exactLegacy)
        {
            throw new InvalidOperationException(
                $"Scene '{manager.gameObject.scene.name}' gameManager must " +
                "inherit the exact online-Safety DefensesBeat value " +
                $"{SafetyExtractionRequiredDefensesBeat} without a scene override.");
        }
    }

    internal static bool EnsureSafetyExtractionGateAuthoredForTests(
        gameManager manager)
    {
        return EnsureSafetyExtractionGateAuthored(manager);
    }

    internal static void ValidateSafetyExtractionGateForTests(
        gameManager manager,
        bool allowLegacyZero)
    {
        ValidateSafetyExtractionGate(manager, allowLegacyZero);
    }

    private static bool EnsureSafetyLoseLifecycleBridgeAuthored(
        Scene scene,
        gameManager manager)
    {
        ValidateSafetyLoseLifecycleBridge(
            scene,
            manager,
            allowMissingBridge: true);

        CampaignSafetyLoseLifecycleBridge[] existing =
            FindSceneComponents<CampaignSafetyLoseLifecycleBridge>(scene);
        if (existing.Length == 1)
            return false;

        CampaignSafetyLoseLifecycleBridge bridge =
            manager.gameObject.AddComponent<CampaignSafetyLoseLifecycleBridge>();
        bridge.Configure(manager);
        EditorUtility.SetDirty(manager.gameObject);
        EditorUtility.SetDirty(bridge);

        ValidateSafetyLoseLifecycleBridge(
            scene,
            manager,
            allowMissingBridge: false);
        return true;
    }

    private static void ValidateSafetyLoseLifecycleBridge(
        Scene scene,
        gameManager manager,
        bool allowMissingBridge)
    {
        CampaignSafetyLoseLifecycleBridge.RequireSafetyContract();
        ValidateSafetyRespawnButtonContract(scene, manager);

        GameObject sourceUi = AssetDatabase.LoadAssetAtPath<GameObject>(
            SafetyUiPrefabPath);
        CampaignSafetyLoseLifecycleBridge[] sourceBridges = sourceUi != null
            ? sourceUi.GetComponentsInChildren<CampaignSafetyLoseLifecycleBridge>(
                true)
            : Array.Empty<CampaignSafetyLoseLifecycleBridge>();
        gameManager sourceManager = sourceUi != null
            ? sourceUi.GetComponentInChildren<gameManager>(true)
            : null;
        CampaignSafetyLoseLifecycleBridge[] sceneBridges =
            FindSceneComponents<CampaignSafetyLoseLifecycleBridge>(scene);

        CampaignSafetyLoseLifecycleBridge sourceBridge =
            sourceBridges.Length == 1 ? sourceBridges[0] : null;
        if (sourceUi == null ||
            sourceBridge == null ||
            sourceManager == null ||
            sourceBridge.gameObject != sourceManager.gameObject ||
            sourceBridge.ConfiguredManager != sourceManager ||
            !sourceBridge.enabled ||
            !sourceBridge.gameObject.activeSelf)
        {
            throw new InvalidOperationException(
                "The protected online-safety UI prefab must retain exactly " +
                "one enabled campaign lose lifecycle bridge configured on " +
                "its exact gameManager GameObject.");
        }

        if (sceneBridges.Length == 0 && allowMissingBridge)
            return;

        CampaignSafetyLoseLifecycleBridge bridge = sceneBridges.Length == 1
            ? sceneBridges[0]
            : null;
        if (bridge == null ||
            manager == null ||
            bridge.gameObject.scene != scene ||
            manager.gameObject.scene != scene ||
            bridge.gameObject != manager.gameObject ||
            bridge.ConfiguredManager != manager ||
            !bridge.isActiveAndEnabled ||
            !manager.enabled ||
            !bridge.gameObject.activeInHierarchy ||
            bridge.hideFlags != HideFlags.None ||
            manager.gameObject.GetComponents<
                CampaignSafetyLoseLifecycleBridge>().Length != 1 ||
            PrefabUtility.IsAddedComponentOverride(bridge) ||
            PrefabUtility.GetCorrespondingObjectFromSource(bridge) !=
                sourceBridge ||
            PrefabUtility.GetCorrespondingObjectFromOriginalSource(bridge) !=
                sourceBridge)
        {
            throw new InvalidOperationException(
                $"Scene '{scene.name}' must contain exactly one enabled, " +
                "configured campaign lose lifecycle bridge inherited from " +
                "the exact online-safety gameManager GameObject.");
        }
    }

    private static void ValidateSafetyRespawnButtonContract(
        Scene scene,
        gameManager manager)
    {
        if (!string.Equals(
                AssetDatabase.AssetPathToGUID(SafetyUiPrefabPath),
                SafetyUiPrefabGuid,
                StringComparison.Ordinal) ||
            !string.Equals(
                AssetDatabase.AssetPathToGUID(SafetyButtonFunctionsScriptPath),
                SafetyButtonFunctionsScriptGuid,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Online-safety UI or buttonFunctions identity changed while " +
                "validating the Respawn callback contract.");
        }

        string managerSource = File.ReadAllText(
            ToAbsolutePath(SafetyGameManagerScriptPath));
        string buttonFunctionsSource = File.ReadAllText(
            ToAbsolutePath(SafetyButtonFunctionsScriptPath));
        string loseBody = ExtractExactSourceMethodBody(
            managerSource,
            "public void youLose()");
        string notifyBody = ExtractExactSourceMethodBody(
            managerSource,
            "public void NotifyPlayerRespawned()");
        string respawnBody = ExtractExactSourceMethodBody(
            buttonFunctionsSource,
            "public void respawn()");
        bool exactLoseLifecycle = Regex.IsMatch(
                managerSource,
                @"private\s+bool\s+amDying\s*=\s*false\s*;") &&
            ContainsOrderedSourceTokens(
                loseBody,
                "if (amDying == true)",
                "return;",
                "amDying = true;",
                "CampaignEventUtility.Invoke(PlayerLost, this);",
                "statePause();") &&
            string.Equals(
                Regex.Replace(notifyBody, @"\s+", " ").Trim(),
                "CampaignEventUtility.Invoke(PlayerRespawned, this);",
                StringComparison.Ordinal) &&
            ContainsOrderedSourceTokens(
                respawnBody,
                "manager.playerController.spawnPlayer();",
                "manager.NotifyPlayerRespawned();",
                "manager.stateUnpause();");
        if (!exactLoseLifecycle)
        {
            throw new InvalidOperationException(
                "Campaign lose lifecycle integration requires the exact " +
                "online-safety amDying latch, lose notification, respawn " +
                "notification, and spawn/notify/unpause callback ordering.");
        }

        GameObject sourceUi = AssetDatabase.LoadAssetAtPath<GameObject>(
            SafetyUiPrefabPath);
        Button[] sourceMatches = sourceUi != null
            ? sourceUi.GetComponentsInChildren<Button>(true)
                .Where(button => button.gameObject.name == "Respawn")
                .ToArray()
            : Array.Empty<Button>();
        Button sourceButton = sourceMatches.Length == 1
            ? sourceMatches[0]
            : null;
        GlobalObjectId sourceButtonId = sourceButton != null
            ? GlobalObjectId.GetGlobalObjectIdSlow(sourceButton)
            : default;
        UnityEngine.Object sourceFeedback = sourceButton != null &&
            sourceButton.onClick.GetPersistentEventCount() > 0
                ? sourceButton.onClick.GetPersistentTarget(0)
                : null;
        buttonFunctions sourceFunctions = sourceButton != null &&
            sourceButton.onClick.GetPersistentEventCount() > 1
                ? sourceButton.onClick.GetPersistentTarget(1) as buttonFunctions
                : null;
        GlobalObjectId sourceFeedbackId = sourceFeedback != null
            ? GlobalObjectId.GetGlobalObjectIdSlow(sourceFeedback)
            : default;
        GlobalObjectId sourceFunctionsId = sourceFunctions != null
            ? GlobalObjectId.GetGlobalObjectIdSlow(sourceFunctions)
            : default;
        MonoScript sourceFunctionsScript = sourceFunctions != null
            ? MonoScript.FromMonoBehaviour(sourceFunctions)
            : null;
        SerializedProperty sourceRespawnMode = sourceButton != null
            ? new SerializedObject(sourceButton).FindProperty(
                "m_OnClick.m_PersistentCalls.m_Calls.Array.data[1].m_Mode")
            : null;

        if (sourceButton == null ||
            sourceButtonId.targetObjectId != SafetyRespawnButtonSourceFileId ||
            sourceButton.onClick.GetPersistentEventCount() != 2 ||
            sourceFeedback == null ||
            sourceFeedbackId.targetObjectId !=
                SafetyRespawnFeedbackSourceFileId ||
            sourceButton.onClick.GetPersistentMethodName(0) !=
                "PlayActionFeedback" ||
            sourceButton.onClick.GetPersistentListenerState(0) !=
                UnityEventCallState.RuntimeOnly ||
            sourceFunctions == null ||
            sourceFunctionsId.targetObjectId !=
                SafetyButtonFunctionsSourceFileId ||
            sourceButton.onClick.GetPersistentMethodName(1) != "respawn" ||
            sourceButton.onClick.GetPersistentListenerState(1) !=
                UnityEventCallState.RuntimeOnly ||
            sourceRespawnMode == null ||
            sourceRespawnMode.intValue != 1 ||
            sourceFunctionsScript == null ||
            !string.Equals(
                AssetDatabase.GetAssetPath(sourceFunctionsScript),
                SafetyButtonFunctionsScriptPath,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Online-safety UI.prefab must retain the exact two-callback " +
                "Respawn button contract ending in buttonFunctions.respawn. " +
                $"Observed matches={sourceMatches.Length}, " +
                $"buttonFileId={sourceButtonId.targetObjectId}, " +
                $"calls={sourceButton?.onClick.GetPersistentEventCount() ?? -1}, " +
                $"feedbackFileId={sourceFeedbackId.targetObjectId}, " +
                $"feedbackMethod='{sourceButton?.onClick.GetPersistentMethodName(0)}', " +
                $"feedbackState={(sourceButton != null ? (int)sourceButton.onClick.GetPersistentListenerState(0) : -1)}, " +
                $"functionsFileId={sourceFunctionsId.targetObjectId}, " +
                $"respawnMethod='{sourceButton?.onClick.GetPersistentMethodName(1)}', " +
                $"respawnState={(sourceButton != null ? (int)sourceButton.onClick.GetPersistentListenerState(1) : -1)}, " +
                $"respawnMode={(sourceRespawnMode != null ? sourceRespawnMode.intValue : -1)}, " +
                $"script='{(sourceFunctionsScript != null ? AssetDatabase.GetAssetPath(sourceFunctionsScript) : string.Empty)}'.");
        }

        Button[] sceneMatches = FindSceneComponents<Button>(scene)
            .Where(button =>
                PrefabUtility.GetCorrespondingObjectFromSource(button) ==
                    sourceButton)
            .ToArray();
        Button sceneButton = sceneMatches.Length == 1
            ? sceneMatches[0]
            : null;
        UnityEngine.Object sceneFeedback = sceneButton != null &&
            sceneButton.onClick.GetPersistentEventCount() > 0
                ? sceneButton.onClick.GetPersistentTarget(0)
                : null;
        buttonFunctions sceneFunctions = sceneButton != null &&
            sceneButton.onClick.GetPersistentEventCount() > 1
                ? sceneButton.onClick.GetPersistentTarget(1) as buttonFunctions
                : null;
        SerializedProperty sceneOnClick = sceneButton != null
            ? new SerializedObject(sceneButton).FindProperty("m_OnClick")
            : null;
        GameObject managerRoot = manager != null
            ? PrefabUtility.GetOutermostPrefabInstanceRoot(manager.gameObject)
            : null;
        GameObject functionsRoot = sceneFunctions != null
            ? PrefabUtility.GetOutermostPrefabInstanceRoot(
                sceneFunctions.gameObject)
            : null;
        PropertyModification[] uiModifications = managerRoot != null
            ? PrefabUtility.GetPropertyModifications(managerRoot) ??
                Array.Empty<PropertyModification>()
            : Array.Empty<PropertyModification>();
        bool respawnCallbacksOverridden = uiModifications.Any(modification =>
            modification != null &&
            modification.target == sourceButton &&
            (modification.propertyPath == "m_OnClick" ||
             modification.propertyPath.StartsWith(
                 "m_OnClick.",
                 StringComparison.Ordinal)));

        if (sceneButton == null ||
            sceneButton.onClick.GetPersistentEventCount() != 2 ||
            sceneButton.onClick.GetPersistentMethodName(0) !=
                "PlayActionFeedback" ||
            sceneButton.onClick.GetPersistentListenerState(0) !=
                UnityEventCallState.RuntimeOnly ||
            sceneFeedback == null ||
            PrefabUtility.GetCorrespondingObjectFromSource(sceneFeedback) !=
                sourceFeedback ||
            sceneFunctions == null ||
            PrefabUtility.GetCorrespondingObjectFromSource(sceneFunctions) !=
                sourceFunctions ||
            sceneButton.onClick.GetPersistentMethodName(1) != "respawn" ||
            sceneButton.onClick.GetPersistentListenerState(1) !=
                UnityEventCallState.RuntimeOnly ||
            sceneOnClick == null ||
            sceneOnClick.prefabOverride ||
            respawnCallbacksOverridden ||
            managerRoot == null ||
            functionsRoot != managerRoot)
        {
            throw new InvalidOperationException(
                $"Scene '{scene.name}' must inherit the exact online-safety " +
                "Respawn Button callbacks, including buttonFunctions.respawn, " +
                "without a scene-authored callback override.");
        }
    }

    private static string ExtractExactSourceMethodBody(
        string source,
        string declaration)
    {
        int declarationIndex = source.IndexOf(
            declaration,
            StringComparison.Ordinal);
        if (declarationIndex < 0 ||
            source.IndexOf(
                declaration,
                declarationIndex + declaration.Length,
                StringComparison.Ordinal) >= 0)
        {
            throw new InvalidOperationException(
                $"Expected exactly one protected source declaration " +
                $"'{declaration}'.");
        }

        int bodyStart = source.IndexOf('{', declarationIndex);
        if (bodyStart < 0)
        {
            throw new InvalidOperationException(
                $"Protected source declaration '{declaration}' has no body.");
        }

        int depth = 0;
        for (int index = bodyStart; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source.Substring(
                        bodyStart + 1,
                        index - bodyStart - 1);
                }
            }
        }

        throw new InvalidOperationException(
            $"Protected source declaration '{declaration}' has an unclosed body.");
    }

    private static bool ContainsOrderedSourceTokens(
        string source,
        params string[] tokens)
    {
        string normalized = Regex.Replace(source, @"\s+", " ");
        int cursor = 0;
        foreach (string token in tokens)
        {
            int found = normalized.IndexOf(
                token,
                cursor,
                StringComparison.Ordinal);
            if (found < 0)
                return false;

            cursor = found + token.Length;
        }

        return true;
    }

    internal static bool EnsureSafetyLoseLifecycleBridgeAuthoredForTests(
        Scene scene,
        gameManager manager)
    {
        return EnsureSafetyLoseLifecycleBridgeAuthored(scene, manager);
    }

    internal static void ValidateSafetyLoseLifecycleBridgeForTests(
        Scene scene,
        gameManager manager,
        bool allowMissingBridge)
    {
        ValidateSafetyLoseLifecycleBridge(
            scene,
            manager,
            allowMissingBridge);
    }

    internal static void ValidateSafetyItemDatabaseSource(Scene scene)
    {
        if (!string.Equals(
                AssetDatabase.AssetPathToGUID(SafetyUiPrefabPath),
                SafetyUiPrefabGuid,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Online-safety UI prefab GUID changed while validating its " +
                "persistent ItemDatabase.");
        }

        gameManager manager = RequireSingleComponent<gameManager>(scene);
        GameObject instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(
            manager.gameObject);
        GameObject sourceRoot = instanceRoot != null
            ? PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot)
            : null;
        gameManager sourceManager =
            PrefabUtility.GetCorrespondingObjectFromSource(manager);
        ItemDatabase[] databases = FindSceneComponents<ItemDatabase>(scene);
        ItemDatabase database = manager.itemDatabase;
        ItemDatabase sourceDatabase = database != null
            ? PrefabUtility.GetCorrespondingObjectFromSource(database)
            : null;
        MonoScript sourceDatabaseScript = sourceDatabase != null
            ? MonoScript.FromMonoBehaviour(sourceDatabase)
            : null;
        string sourceDatabaseScriptGuid = sourceDatabaseScript != null
            ? AssetDatabase.AssetPathToGUID(
                AssetDatabase.GetAssetPath(sourceDatabaseScript))
            : string.Empty;
        GlobalObjectId sourceManagerId = sourceManager != null
            ? GlobalObjectId.GetGlobalObjectIdSlow(sourceManager)
            : default;
        GlobalObjectId sourceDatabaseId = sourceDatabase != null
            ? GlobalObjectId.GetGlobalObjectIdSlow(sourceDatabase)
            : default;
        SerializedProperty managerDatabaseProperty =
            new SerializedObject(manager).FindProperty("itemDatabase");
        SerializedProperty catalogProperty = database != null
            ? new SerializedObject(database).FindProperty("allItemPrefabs")
            : null;

        if (instanceRoot == null ||
            instanceRoot.transform.parent != null ||
            PrefabUtility.GetPrefabInstanceStatus(instanceRoot) !=
                PrefabInstanceStatus.Connected ||
            sourceRoot == null ||
            !string.Equals(
                AssetDatabase.GetAssetPath(sourceRoot),
                SafetyUiPrefabPath,
                StringComparison.Ordinal) ||
            sourceManager == null ||
            !string.Equals(
                AssetDatabase.GetAssetPath(sourceManager),
                SafetyUiPrefabPath,
                StringComparison.Ordinal) ||
            sourceManagerId.targetObjectId !=
                SafetyGameManagerSourceFileId ||
            manager.gameObject.name != "gamemanager" ||
            manager.transform.parent != instanceRoot.transform ||
            database == null ||
            databases.Length != 1 ||
            databases[0] != database ||
            !database.enabled ||
            !database.gameObject.activeSelf ||
            database.gameObject.name != "itemDatabase" ||
            database.transform.parent != instanceRoot.transform ||
            sourceDatabase == null ||
            !string.Equals(
                AssetDatabase.GetAssetPath(sourceDatabase),
                SafetyUiPrefabPath,
                StringComparison.Ordinal) ||
            sourceDatabaseId.targetObjectId !=
                SafetyItemDatabaseSourceFileId ||
            !string.Equals(
                sourceDatabaseScriptGuid,
                SafetyItemDatabaseScriptGuid,
                StringComparison.Ordinal) ||
            sourceManager.itemDatabase != sourceDatabase ||
            managerDatabaseProperty == null ||
            managerDatabaseProperty.prefabOverride ||
            managerDatabaseProperty.objectReferenceValue != database ||
            catalogProperty == null ||
            catalogProperty.prefabOverride)
        {
            throw new InvalidOperationException(
                $"Scene '{scene.name}' must inherit exactly one enabled " +
                "ItemDatabase and its gameManager reference from the one " +
                "connected online-safety UI.prefab instance, with no " +
                "scene-authored catalog override.");
        }

        Item[] sourceItems = sourceDatabase.allItemPrefabs;
        Item[] items = database.allItemPrefabs;
        int expectedCount = SafetyItemDatabasePrefabGuids.Length;
        if (sourceItems == null || items == null ||
            sourceItems.Length != expectedCount ||
            items.Length != expectedCount ||
            SafetyItemDatabaseItemFileIds.Length != expectedCount ||
            SafetyItemDatabaseItemIds.Length != expectedCount)
        {
            throw new InvalidOperationException(
                "Online-safety UI ItemDatabase must retain its exact ordered " +
                $"{expectedCount}-item source catalog.");
        }

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < expectedCount; index++)
        {
            Item item = items[index];
            Item sourceItem = sourceItems[index];
            string itemGuid = string.Empty;
            long itemFileId = 0L;
            bool hasPersistentIdentity = item != null &&
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    item,
                    out itemGuid,
                    out itemFileId);
            string itemId = item?.item?.itemID;
            if (!hasPersistentIdentity ||
                sourceItem != item ||
                !string.Equals(
                    itemGuid,
                    SafetyItemDatabasePrefabGuids[index],
                    StringComparison.Ordinal) ||
                itemFileId != SafetyItemDatabaseItemFileIds[index] ||
                !string.Equals(
                    itemId,
                    SafetyItemDatabaseItemIds[index],
                    StringComparison.Ordinal) ||
                !seenIds.Add(itemId) ||
                (string.IsNullOrEmpty(itemId)
                    ? database.GetByID(itemId) != null
                    : database.GetByID(itemId) != item.item))
            {
                throw new InvalidOperationException(
                    $"Online-safety UI ItemDatabase entry {index} no longer " +
                    "matches its exact prefab GUID/fileID/itemID identity.");
            }
        }
    }

    private static void RequireSafetyExtractionMenuSource(GameObject menu)
    {
        if (!string.Equals(
                AssetDatabase.AssetPathToGUID(SafetyUiPrefabPath),
                SafetyUiPrefabGuid,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Online-safety UI prefab GUID changed.");
        }

        GameObject source = menu != null
            ? PrefabUtility.GetCorrespondingObjectFromSource(menu)
            : null;
        GlobalObjectId sourceId = source != null
            ? GlobalObjectId.GetGlobalObjectIdSlow(source)
            : default;
        if (menu == null ||
            menu.name != "extractionMenu" ||
            source == null ||
            !string.Equals(
                AssetDatabase.GetAssetPath(source),
                SafetyUiPrefabPath,
                StringComparison.Ordinal) ||
            sourceId.targetObjectId != SafetyExtractionMenuSourceFileId)
        {
            throw new InvalidOperationException(
                "GameManager extraction menu is not the exact online-safety " +
                "UI.prefab extractionMenu instance.");
        }
    }

    private static Button RequireSafetyExtractionButton(
        GameObject menu,
        string buttonName,
        ulong sourceFileId)
    {
        Button[] matches = menu.GetComponentsInChildren<Button>(true)
            .Where(button => button.gameObject.name == buttonName)
            .ToArray();
        Button source = matches.Length == 1
            ? PrefabUtility.GetCorrespondingObjectFromSource(matches[0])
            : null;
        GlobalObjectId sourceId = source != null
            ? GlobalObjectId.GetGlobalObjectIdSlow(source)
            : default;
        if (matches.Length != 1 ||
            source == null ||
            !string.Equals(
                AssetDatabase.GetAssetPath(source),
                SafetyUiPrefabPath,
                StringComparison.Ordinal) ||
            sourceId.targetObjectId != sourceFileId)
        {
            throw new InvalidOperationException(
                $"Online-safety extraction menu requires exact button " +
                $"'{buttonName}' from UI.prefab.");
        }

        return matches[0];
    }

    private static CampaignAreaId ToCampaignArea(OpenWorldAreaId area)
    {
        return area switch
        {
            OpenWorldAreaId.StillwaterFeedMill =>
                CampaignAreaId.StillwaterFeedMill,
            OpenWorldAreaId.HarrowEstate => CampaignAreaId.HarrowEstate,
            OpenWorldAreaId.BloodrootHollow =>
                CampaignAreaId.BloodrootHollow,
            _ => throw new ArgumentOutOfRangeException(
                nameof(area),
                area,
                "Unsupported open-world barrier area.")
        };
    }

    private static CampaignStateService EnsureRootService(Scene scene)
    {
        GameObject serviceRoot = EnsureSceneRoot(scene, ServiceRootName);

        if (serviceRoot.transform.parent != null)
        {
            throw new InvalidOperationException(
                $"{ServiceRootName} must be a scene root for DontDestroyOnLoad.");
        }

        Component[] existingComponents = serviceRoot.GetComponents<Component>();

        foreach (Component component in existingComponents)
        {
            if (component is Transform || component is CampaignStateService ||
                component is CampaignInventoryCarryover)
                continue;

            throw new InvalidOperationException(
                $"{ServiceRootName} contains unrecognized component " +
                $"{component.GetType().Name}.");
        }

        if (serviceRoot.transform.childCount != 0)
        {
            throw new InvalidOperationException(
                $"{ServiceRootName} must not contain children because duplicate " +
                "scene services destroy their host GameObject.");
        }

        CampaignStateService state =
            EnsureComponent<CampaignStateService>(serviceRoot);
        CampaignInventoryCarryover carryover =
            EnsureComponent<CampaignInventoryCarryover>(serviceRoot);
        carryover.Configure(
            CreateInventoryCatalog(),
            CreateSafetyInventoryCatalog(),
            RequireExactSafetyTruckKeyPrefab());
        EditorUtility.SetDirty(carryover);
        return state;
    }

    private static CampaignLoadoutEquipmentBridge
        EnsureLoadoutEquipmentBridge(
            Scene scene,
            CampaignStateService campaignState,
            CampaignInventoryCarryover carryover)
    {
        if (campaignState == null ||
            carryover == null ||
            campaignState.gameObject.scene != scene ||
            carryover.gameObject != campaignState.gameObject ||
            campaignState.GetComponent<CampaignInventoryCarryover>() !=
                carryover)
        {
            throw new InvalidOperationException(
                $"Scene '{scene.name}' loadout equipment requires its exact " +
                "CampaignStateService and CampaignInventoryCarryover authority.");
        }

        GameObject[] roots = scene.GetRootGameObjects()
            .Where(root => root.name ==
                CampaignLoadoutEquipmentBridge.AuthoringRootName)
            .ToArray();
        CampaignLoadoutEquipmentBridge[] sceneBridges =
            FindSceneComponents<CampaignLoadoutEquipmentBridge>(scene);
        if (roots.Length > 1 ||
            roots.Length == 0 && sceneBridges.Length != 0)
        {
            throw new InvalidOperationException(
                $"Scene '{scene.name}' contains duplicate or misplaced " +
                $"'{CampaignLoadoutEquipmentBridge.AuthoringRootName}' " +
                "loadout-equipment authoring.");
        }

        GameObject root;
        CampaignLoadoutEquipmentBridge bridge;
        if (roots.Length == 0)
        {
            root = new GameObject(
                CampaignLoadoutEquipmentBridge.AuthoringRootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            bridge = root.AddComponent<CampaignLoadoutEquipmentBridge>();
        }
        else
        {
            root = roots[0];
            CampaignLoadoutEquipmentBridge[] rootBridges =
                root.GetComponents<CampaignLoadoutEquipmentBridge>();
            if (sceneBridges.Length != 1 ||
                rootBridges.Length != 1 ||
                sceneBridges[0] != rootBridges[0] ||
                root.transform.parent != null ||
                root.transform.childCount != 0 ||
                root.GetComponents<Component>().Length != 2)
            {
                throw new InvalidOperationException(
                    $"The recognized '{CampaignLoadoutEquipmentBridge.AuthoringRootName}' " +
                    "root is not the exact Transform-plus-bridge shape. " +
                    "Refusing to rewrite an unrecognized scene authority.");
            }

            bridge = rootBridges[0];
        }

        GameObject rifle = LoadRequiredExactAsset<GameObject>(
            RiflePickupPath,
            RiflePickupGuid);
        GameObject ammo = LoadRequiredExactAsset<GameObject>(
            AmmoPickupPath,
            AmmoPickupGuid);
        GameObject radar = LoadRequiredExactAsset<GameObject>(
            RadarPickupPath,
            RadarPickupGuid);
        gunStats pistolStats = LoadRequiredExactAsset<gunStats>(
            PistolStatsPath,
            PistolStatsGuid);
        gunStats rifleStats = LoadRequiredExactAsset<gunStats>(
            RifleStatsPath,
            RifleStatsGuid);
        gunStats shotgunStats = LoadRequiredExactAsset<gunStats>(
            ShotgunStatsPath,
            ShotgunStatsGuid);

        bool alreadyCurrent =
            root.activeSelf &&
            bridge.enabled &&
            Approximately(root.transform.position, Vector3.zero) &&
            Quaternion.Angle(
                root.transform.rotation,
                Quaternion.identity) <= 0.001f &&
            Approximately(root.transform.localScale, Vector3.one) &&
            bridge.CampaignState == campaignState &&
            bridge.InventoryCarryover == carryover &&
            bridge.RifleInventoryPickup == rifle &&
            bridge.RifleAmmoInventoryPickup == ammo &&
            bridge.RadarInventoryPickup == radar &&
            bridge.PistolDefinition == pistolStats &&
            bridge.RifleDefinition == rifleStats &&
            bridge.ShotgunDefinition == shotgunStats &&
            bridge.ValidateConfiguration(out _);
        if (alreadyCurrent)
            return bridge;

        root.transform.SetPositionAndRotation(
            Vector3.zero,
            Quaternion.identity);
        root.transform.localScale = Vector3.one;
        root.SetActive(true);
        bridge.enabled = true;
        bridge.Configure(
            campaignState,
            carryover,
            rifle,
            ammo,
            radar,
            pistolStats,
            rifleStats,
            shotgunStats);
        if (!bridge.ValidateConfiguration(out string error))
        {
            throw new InvalidOperationException(
                $"Scene '{scene.name}' loadout-equipment bridge authoring is " +
                "invalid. " + error);
        }

        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(bridge);
        return bridge;
    }

    private static void ValidateProject(bool openScenesWhenNeeded)
    {
        ValidateReleaseBuildSettings();

        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            Scene farm = GetLoadedScene(FarmScenePath);
            Scene openWorld = GetLoadedScene(OpenWorldScenePath);

            if (!farm.IsValid() && openScenesWhenNeeded)
                farm = OpenTargetScene(FarmScenePath);

            if (!openWorld.IsValid() && openScenesWhenNeeded)
                openWorld = OpenTargetScene(OpenWorldScenePath);

            ValidateFarm(farm);
            ValidateOpenWorld(openWorld);
            ValidateShippedInventoryItemReachability(farm);
            ValidateShippedInventoryItemReachability(openWorld);
        }
        finally
        {
            RestoreSceneSetup(setup);
        }
    }

    private static void ValidateFarm(
        Scene scene,
        bool requireCurrentSignature = true,
        RootServiceSchema rootServiceSchema = RootServiceSchema.CurrentOnly,
        bool requireGroundedHooks = true,
        bool requireCurrentChoreSchema = true,
        bool requireExactLegacyV4 = false,
        bool allowLegacySafetyEnemyRoster = false,
        bool allowLegacyFarmItemOverride = false,
        bool allowMissingLoadoutEquipmentBridge = false,
        bool allowLegacyPlayerPrefab = false,
        bool allowMissingFarmNavMeshData = false,
        bool allowSafetyTruckExtractionMigration = false,
        bool allowCurrentV5RuntimeFieldMigration = false,
        bool allowInactivePersistentChoreEnvironmentMigration = false,
        bool allowMissingSafetyLoseLifecycleBridge = false)
    {
        RequireValidLoadedScene(scene, FarmScenePath);
        ValidateSafetyItemDatabaseSource(scene);
        ValidateSafetyLoseLifecycleBridge(
            scene,
            RequireSingleComponent<gameManager>(scene),
            allowMissingSafetyLoseLifecycleBridge);

        if (requireCurrentSignature)
        {
            ValidateSignature(scene);
        }

        ValidateFarmItemOverrideMigrationState(
            scene,
            allowLegacyFarmItemOverride);

        ValidateRootService(
            scene,
            rootServiceSchema,
            allowCurrentV5RuntimeFieldMigration);
        ValidateLoadoutEquipmentBridge(
            scene,
            allowMissingLoadoutEquipmentBridge,
            allowCurrentV5RuntimeFieldMigration);

        if (!allowLegacyPlayerPrefab)
        {
            ValidateSafetyPlayerSceneAuthority(
                scene,
                RequirePath(scene, FarmPrologueSpawnPath).transform);
        }

        Terrain terrain = RequireSingleComponent<Terrain>(scene);
        ValidateFarmTerrain(terrain);
        Vector3[] choreWorldPositions = requireCurrentChoreSchema
            ? GetTerrainGroundPositions(terrain, ChoreStepWorldXZ)
            : requireGroundedHooks
                ? GetTerrainGroundPositions(terrain, LegacyV4ChoreWorldXZ)
                : LegacyV3ChoreWorldPositions;
        Vector3[] choreApproachWorldPositions = requireCurrentChoreSchema
            ? GetTerrainGroundPositions(terrain, ChoreApproachWorldXZ)
            : Array.Empty<Vector3>();
        Vector3[] propSocketWorldPositions = requireCurrentChoreSchema
            ? GetTerrainGroundPositions(terrain, FarmPropSocketWorldXZ)
            : choreWorldPositions;
        Vector3[] pigSocketWorldPositions = requireCurrentChoreSchema
            ? GetTerrainGroundPositions(terrain, FarmPigSocketWorldXZ)
            : requireGroundedHooks
                ? GetTerrainGroundPositions(terrain, LegacyV4PigSocketWorldXZ)
                : LegacyV3PigSocketWorldPositions;

        FarmPrologueDirector director =
            RequirePath(scene, FarmDirectorPath)
                .GetComponent<FarmPrologueDirector>();

        if (director == null)
            throw new InvalidOperationException("Farm Prologue Director is missing.");

        SerializedObject directorData = new(director);
        string[] requiredDirectorReferences =
        {
            "prologueStateRoot",
            "hubStateRoot",
            "wakeUpSequenceRoot",
            "choreSequenceRoot",
            "rumbleSequenceRoot",
            "waveManagerRoot",
            "mobSpawnerRoot",
            "waveEncounter",
            "playerInventory",
            "campaignState",
            "playerTransform",
            "prologueSpawn",
            "hubSpawn",
            "screenFader"
        };

        foreach (string propertyName in requiredDirectorReferences)
        {
            SerializedProperty property =
                directorData.FindProperty(propertyName);

            if (property == null || property.objectReferenceValue == null)
            {
                throw new InvalidOperationException(
                    $"Farm Prologue Director reference '{propertyName}' is missing.");
            }
        }

        RequireSafetyTruckExtractionBinding(scene);
        ValidateSafetyTruckKeyMigrationState(
            scene,
            RequirePath(scene, FarmHubPath).transform,
            allowSafetyTruckExtractionMigration);

        if (!allowLegacyPlayerPrefab)
        {
            Transform exactPlayer =
                RequireTaggedObject(scene, "Player").transform;
            Transform exactFallback =
                RequireTaggedObject(scene, "PlayerSpawnPos").transform;
            Transform exactPrologue =
                RequirePath(scene, FarmPrologueSpawnPath).transform;
            Transform exactHub =
                RequirePath(scene, FarmHubSpawnPath).transform;
            SerializedProperty fallbackReference =
                directorData.FindProperty("playerSpawnFallback");
            if (directorData.FindProperty("playerTransform")
                    .objectReferenceValue != exactPlayer ||
                fallbackReference == null ||
                fallbackReference.objectReferenceValue != exactFallback ||
                directorData.FindProperty("prologueSpawn")
                    .objectReferenceValue != exactPrologue ||
                directorData.FindProperty("hubSpawn")
                    .objectReferenceValue != exactHub ||
                (Approximately(exactPrologue.position, exactHub.position) &&
                 Quaternion.Angle(
                     exactPrologue.rotation,
                     exactHub.rotation) <= 0.01f))
            {
                throw new InvalidOperationException(
                    "Farm Prologue Director must own the exact Safety Player, " +
                    "tagged phase fallback, and distinct Prologue/Hub arrivals.");
            }
        }

        ValidateDirectorInputGate(scene, directorData);

        SerializedProperty choreReferences =
            directorData.FindProperty("chores");

        int expectedChoreCount = requireCurrentChoreSchema
            ? ChoreSteps.Length
            : ChoreGroupNames.Length;

        if (choreReferences == null ||
            choreReferences.arraySize != expectedChoreCount)
        {
            throw new InvalidOperationException(
                $"Farm Prologue Director must own exactly {expectedChoreCount} " +
                "ordered chore references.");
        }

        for (int index = 0; index < choreReferences.arraySize; index++)
        {
            if (choreReferences.GetArrayElementAtIndex(index)
                    .objectReferenceValue == null)
            {
                throw new InvalidOperationException(
                    "Farm Prologue Director contains a null chore reference.");
            }
        }

        FarmChoreInteractable[] chores =
            FindSceneComponents<FarmChoreInteractable>(scene);

        if (chores.Length != expectedChoreCount ||
            chores.Select(chore => chore.ChoreId).Distinct().Count() !=
            expectedChoreCount)
        {
            throw new InvalidOperationException(
                $"Farm must contain exactly {expectedChoreCount} uniquely " +
                "identified chore interactions. Found: " +
                string.Join(", ", chores.Select(chore =>
                    $"'{chore.gameObject.name}' ({chore.ChoreId}) under " +
                    $"'{chore.transform.parent?.name ?? "<root>"}'")) + ".");
        }

        GameObject persistentChoreEnvironment =
            RequirePath(scene, FarmObjectivesPath);

        if (RequirePath(scene, FarmHubPath).activeSelf ||
            !RequirePath(scene, FarmProloguePath).activeSelf ||
            (requireCurrentChoreSchema &&
             !persistentChoreEnvironment.activeSelf &&
             !allowInactivePersistentChoreEnvironmentMigration) ||
            RequireSingleComponent<waveManager>(scene).gameObject.activeSelf ||
            RequireSingleComponent<MobSpawner>(scene).gameObject.activeSelf)
        {
            throw new InvalidOperationException(
                "Farm must save in fresh-prologue state with the Hub and " +
                "encounter roots inactive and the persistent Prologue " +
                "Objectives environment active.");
        }

        foreach (FarmChoreInteractable chore in chores)
        {
            ValidateInteractionObject(chore.gameObject);
            ValidateNavMeshExcluded(chore.gameObject);
        }

        if (requireCurrentChoreSchema)
        {
            ValidateCurrentChoreAuthoring(
                scene,
                director,
                choreReferences,
                chores,
                terrain,
                choreWorldPositions,
                choreApproachWorldPositions);
        }
        else
        {
            ValidateLegacyChoreAuthoring(
                scene,
                choreReferences,
                chores,
                terrain,
                choreWorldPositions,
                requireGroundedHooks,
                requireExactLegacyV4);
        }

        ValidateFarmSemanticSockets(
            scene,
            terrain,
            propSocketWorldPositions,
            pigSocketWorldPositions,
            requireGroundedHooks);
        ValidateWakeAuthoring(scene);
        ValidateCombatAuthoring(
            scene,
            terrain,
            requireCurrentChoreSchema
                ? choreApproachWorldPositions[
                    choreApproachWorldPositions.Length - 1]
                : Average(choreWorldPositions),
            requireGroundedHooks,
            requireCurrentChoreSchema
                ? choreApproachWorldPositions
                : Array.Empty<Vector3>(),
            allowLegacySafetyEnemyRoster,
            allowMissingFarmNavMeshData);

        GameObject travel = RequirePath(scene, FarmTruckTravelPath);
        if (allowSafetyTruckExtractionMigration &&
            travel.GetComponent<CampaignTravelInteractable>() != null)
        {
            ValidateTravel(
                travel,
                CampaignSceneNames.OpenWorld,
                "BlackPinesArrival");
            ValidateNavMeshExcluded(travel);
        }
        else
        {
            ValidateTravelBackend(
                travel,
                CampaignSceneNames.OpenWorld,
                "BlackPinesArrival");
        }
        ValidateHubTravelSockets(travel);

        if (!allowSafetyTruckExtractionMigration)
        {
            ValidateSafetyExtractionGate(
                RequireSingleComponent<gameManager>(scene),
                allowLegacyZero: false);
            CampaignSceneTravel farmBackend =
                travel.GetComponent<CampaignSceneTravel>();
            ValidateSafetyExtractionMenu(
                scene,
                farmBackend,
                "Travel to Open World",
                SafetyExtractionMenuOption.BlackPines);
            ValidateSceneExtractionAuthority(scene, farmBackend);
        }

        CampaignSpawnPoint hubArrival =
            RequirePath(scene, FarmHubSpawnPath)
                .GetComponent<CampaignSpawnPoint>();

        if (hubArrival == null || hubArrival.DestinationId != "FarmHub")
        {
            throw new InvalidOperationException(
                "Farm Hub arrival spawn is not configured.");
        }

        ValidateLegacyCompletionTrigger(scene);

        if (FindSceneComponents<InfestationSpawner>(scene)
            .Any(spawner => spawner.enabled))
        {
            throw new InvalidOperationException(
                "Farm InfestationSpawner components must remain disabled until rescoping.");
        }

        if (requireCurrentChoreSchema)
        {
            ValidateLegacyInfestationSpawnVolumeNavMeshExclusion(
                scene,
                terrain,
                allowMissingFarmNavMeshData);
        }

        FarmObjectivePresenter presenter =
            RequireSingleComponent<FarmObjectivePresenter>(scene);
        CanvasGroup fader = FindSceneComponents<CanvasGroup>(scene)
            .SingleOrDefault(group => group.gameObject.name == "Campaign Screen Fader");

        if (presenter == null || fader == null ||
            fader.GetComponent<Image>() == null ||
            !fader.gameObject.activeSelf ||
            fader.gameObject.layer != LayerMask.NameToLayer("UI") ||
            !Mathf.Approximately(fader.alpha, 0f) ||
            fader.interactable ||
            fader.blocksRaycasts ||
            fader.transform.GetSiblingIndex() !=
            fader.transform.parent.childCount - 1)
        {
            throw new InvalidOperationException(
                "Farm authored objective/fade presentation is incomplete.");
        }


        RectTransform faderRect = fader.GetComponent<RectTransform>();
        Image faderImage = fader.GetComponent<Image>();

        if (faderRect == null ||
            faderRect.anchorMin != Vector2.zero ||
            faderRect.anchorMax != Vector2.one ||
            faderRect.offsetMin != Vector2.zero ||
            faderRect.offsetMax != Vector2.zero ||
            faderImage.color != Color.black ||
            !faderImage.raycastTarget)
        {
            throw new InvalidOperationException(
                "Farm screen fader must be an authored full-screen black " +
                "raycast target under the existing UI.");
        }

        if (!allowSafetyTruckExtractionMigration && !scene.isDirty)
            ValidateFarmSerializedExtractionClosure();
    }

    private static void ValidateFarmSerializedExtractionClosure()
    {
        string sceneText = File.ReadAllText(ToAbsolutePath(FarmScenePath));
        if (sceneText.Contains(
                RemovedTruckEscapeEndingScriptGuid,
                StringComparison.Ordinal) ||
            sceneText.Contains(
                LegacyTruckKeyPrefabGuid,
                StringComparison.Ordinal) ||
            sceneText.Contains(
                "legacyTruckEscapeInteraction",
                StringComparison.Ordinal) ||
            sceneText.Contains(
                "legacyTruckKeyObject",
                StringComparison.Ordinal) ||
            !sceneText.Contains(
                SafetyTruckKeyPrefabGuid,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Saved Farm YAML retains a legacy extraction controller/key " +
                "reference or is missing the online-safety Car Key.");
        }
    }

    private static void ValidateDirectorInputGate(
        Scene scene,
        SerializedObject directorData)
    {
        playerController movement =
            RequireTaggedPlayerRootComponent<playerController>(scene);
        Interact interaction = RequireSingleEnabledPlayerInteract(scene);
        cameraController cameraLook =
            RequireSingleComponent<cameraController>(scene);
        var expected = new HashSet<Behaviour>
        {
            movement,
            interaction,
            cameraLook
        };

        SerializedProperty inputBehaviours =
            directorData.FindProperty("gameplayInputBehaviours");

        if (inputBehaviours == null ||
            inputBehaviours.arraySize != expected.Count)
        {
            throw new InvalidOperationException(
                "Farm Prologue Director must gate exactly movement, " +
                "interaction, and camera-look behaviours.");
        }

        var authored = new HashSet<Behaviour>();

        for (int index = 0; index < inputBehaviours.arraySize; index++)
        {
            Behaviour behaviour = inputBehaviours
                .GetArrayElementAtIndex(index)
                .objectReferenceValue as Behaviour;

            if (behaviour == null || !authored.Add(behaviour))
            {
                throw new InvalidOperationException(
                    "Farm gameplay input gate contains a null or duplicate " +
                    "behaviour reference.");
            }
        }

        if (!authored.SetEquals(expected) ||
            cameraLook.GetComponent<Camera>() == null)
        {
            throw new InvalidOperationException(
                "Farm gameplay input gate must reference the exact player " +
                "controller, Interact component, and player-camera controller.");
        }
    }

    private static void ValidateCurrentChoreAuthoring(
        Scene scene,
        FarmPrologueDirector director,
        SerializedProperty choreReferences,
        IReadOnlyCollection<FarmChoreInteractable> authoredChores,
        Terrain terrain,
        IReadOnlyList<Vector3> choreWorldPositions,
        IReadOnlyList<Vector3> approachWorldPositions)
    {
        if (!director.ChoresMustBeCompletedInOrder)
        {
            throw new InvalidOperationException(
                "Farm prologue chore steps must be completed in deterministic order.");
        }

        if (choreWorldPositions.Count != ChoreSteps.Length ||
            approachWorldPositions.Count != ChoreSteps.Length)
        {
            throw new InvalidOperationException(
                "Farm V5 chore validation requires all eight grounded step poses.");
        }

        var authored = new HashSet<FarmChoreInteractable>(authoredChores);
        var actualPositions = new Vector3[ChoreSteps.Length];
        float interactRange = ReadPlayerInteractRange(scene);
        GameObject objectivesRoot = RequirePath(scene, FarmObjectivesPath);
        string[] actualGroupNames = objectivesRoot.transform
            .Cast<Transform>()
            .Where(child => child.name.StartsWith(
                "CHORE_",
                StringComparison.Ordinal))
            .Select(child => child.name)
            .ToArray();

        if (!actualGroupNames.SequenceEqual(ChoreGroupNames))
        {
            throw new InvalidOperationException(
                "Farm Prologue Objectives must contain exactly the three " +
                "ordered CHORE_* group roots from the V5 schema.");
        }

        foreach (string groupName in ChoreGroupNames)
        {
            GameObject group = RequirePath(
                scene,
                FarmObjectivesPath + "/" + groupName);
            RequireSingleNamedObject(scene, groupName);
            if (string.Equals(
                    groupName,
                    ChoreGroupNames[1],
                    StringComparison.Ordinal))
            {
                if (group.GetComponents<Component>().Length != 1 ||
                    !Approximately(
                        group.transform.localPosition,
                        WednesdayMuckGroupLocalPosition) ||
                    Quaternion.Angle(
                        group.transform.localRotation,
                        Quaternion.identity) > 0.01f ||
                    !Approximately(
                        group.transform.localScale,
                        Vector3.one))
                {
                    throw new InvalidOperationException(
                        "Farm CHORE_Muck_Stalls must preserve the exact " +
                        "Wednesday authored container pose.");
                }
            }
            else
            {
                ValidateTransformOnlyContainer(group);
            }

            if (group.layer != 0 || group.tag != "Untagged")
            {
                throw new InvalidOperationException(
                    $"Farm chore group '{groupName}' must remain an Untagged " +
                    "Default-layer Transform-only container.");
            }

            string[] expectedStepNames = ChoreSteps
                .Where(step => step.GroupName == groupName)
                .Select(step => step.Name)
                .ToArray();
            string[] actualStepNames = group.transform
                .Cast<Transform>()
                .Where(child => child.name.StartsWith(
                    "STEP_",
                    StringComparison.Ordinal))
                .Select(child => child.name)
                .ToArray();

            if (!actualStepNames.SequenceEqual(expectedStepNames))
            {
                throw new InvalidOperationException(
                    $"Farm chore group '{groupName}' must contain its exact " +
                    "ordered STEP_* children and no additional step markers.");
            }
        }

        for (int index = 0; index < ChoreSteps.Length; index++)
        {
            ChoreStepDefinition definition = ChoreSteps[index];
            GameObject group = RequirePath(
                scene,
                FarmObjectivesPath + "/" + definition.GroupName);
            GameObject step = RequireDirectChild(group, definition.Name);
            RequireSingleNamedObject(scene, definition.Name);
            FarmChoreInteractable chore =
                step.GetComponent<FarmChoreInteractable>();
            BoxCollider collider = step.GetComponent<BoxCollider>();

            if (chore == null ||
                !authored.Contains(chore) ||
                chore.ChoreId != definition.Id ||
                chore.ObjectiveText != definition.Objective ||
                chore.RequiredInteractions != 1 ||
                chore.RequiresInventoryItem ||
                choreReferences.GetArrayElementAtIndex(index)
                    .objectReferenceValue != chore ||
                !Approximately(step.transform.position, choreWorldPositions[index]) ||
                Quaternion.Angle(
                    step.transform.rotation,
                    definition.Rotation) > 0.01f ||
                !Approximately(
                    step.transform.localScale,
                    definition.Scale) ||
                collider == null ||
                step.GetComponents<Collider>().Length != 1 ||
                !collider.enabled ||
                collider.isTrigger ||
                !Approximately(collider.center, definition.ColliderCenter) ||
                !Approximately(collider.size, definition.ColliderSize))
            {
                throw new InvalidOperationException(
                    $"Farm chore step '{definition.Name}' does not match its " +
                    "deterministic V5 interaction contract.");
            }

            ValidateInteractionObject(step);
            ValidateNavMeshExcluded(step);

            GameObject propSocket =
                RequireDirectChild(step, PropSocketName);
            GameObject approach =
                RequireDirectChild(step, PlayerApproachName);
            ValidateTransformOnlySocket(
                propSocket,
                choreWorldPositions[index],
                localPosition: false,
                rotation: Quaternion.identity);
            ValidateTransformOnlySocket(
                approach,
                approachWorldPositions[index],
                localPosition: false,
                rotation: Quaternion.identity);
            ValidateTerrainGrounded(step.transform, terrain, "Farm chore step");
            ValidateTerrainGrounded(
                propSocket.transform,
                terrain,
                "Farm chore prop socket");
            ValidateTerrainGrounded(
                approach.transform,
                terrain,
                "Farm chore approach socket");

            float approachDistance = PlanarDistance(
                step.transform.position,
                approach.transform.position);

            if (Mathf.Abs(approachDistance - 2f) > 0.01f ||
                approachDistance > interactRange + NavMeshDistanceTolerance)
            {
                throw new InvalidOperationException(
                    $"Farm chore step '{definition.Name}' approach must be " +
                    $"exactly 2m away and within the player's {interactRange:F2}m " +
                    "interaction range.");
            }

            actualPositions[index] = step.transform.position;
        }

        for (int first = 0; first < actualPositions.Length; first++)
        {
            for (int second = first + 1;
                 second < actualPositions.Length;
                 second++)
            {
                if (PlanarDistance(
                        actualPositions[first],
                        actualPositions[second]) <
                    MinimumChoreStepSpacing - NavMeshDistanceTolerance)
                {
                    throw new InvalidOperationException(
                        $"Farm chore steps '{ChoreSteps[first].Name}' and " +
                        $"'{ChoreSteps[second].Name}' are closer than the " +
                        $"{MinimumChoreStepSpacing:F0}m spacing contract.");
                }
            }
        }

        Vector3[] groupCentroids = ChoreGroupNames
            .Select(groupName => Average(ChoreSteps
                .Select((step, index) => new { step, index })
                .Where(entry => entry.step.GroupName == groupName)
                .Select(entry => actualPositions[entry.index])
                .ToArray()))
            .ToArray();

        for (int first = 0; first < groupCentroids.Length; first++)
        {
            for (int second = first + 1;
                 second < groupCentroids.Length;
                 second++)
            {
                if (PlanarDistance(
                        groupCentroids[first],
                        groupCentroids[second]) <
                    MinimumChoreGroupCentroidSpacing -
                    NavMeshDistanceTolerance)
                {
                    throw new InvalidOperationException(
                        $"Farm chore groups '{ChoreGroupNames[first]}' and " +
                        $"'{ChoreGroupNames[second]}' are closer than the " +
                        $"{MinimumChoreGroupCentroidSpacing:F0}m centroid " +
                        "spacing contract.");
                }
            }
        }
    }

    private static Interact RequireSingleEnabledPlayerInteract(Scene scene)
    {
        Interact[] enabledInteractions = FindSceneComponents<Interact>(scene)
            .Where(interaction => interaction != null && interaction.enabled)
            .ToArray();
        Transform player = RequireTaggedObject(scene, "Player").transform;
        if (enabledInteractions.Length != 1 ||
            (enabledInteractions[0].transform != player &&
             !enabledInteractions[0].transform.IsChildOf(player)))
        {
            throw new InvalidOperationException(
                $"Scene '{scene.name}' requires exactly one enabled Interact " +
                "input authority under the tagged Player; found " +
                $"{enabledInteractions.Length} enabled scene components: " +
                string.Join(", ", enabledInteractions.Select(interaction =>
                    $"'{interaction.gameObject.name}' under " +
                    $"'{interaction.transform.root.name}'")) + ".");
        }

        return enabledInteractions[0];
    }

    private static float ReadPlayerInteractRange(Scene scene)
    {
        Interact interaction = RequireSingleEnabledPlayerInteract(scene);
        var interactionData = new SerializedObject(interaction);
        SerializedProperty range = interactionData.FindProperty("InteractRange");
        SerializedProperty layerMask = interactionData.FindProperty("InteractLayer");
        int interactableLayerBit = 1 << RequireInteractableLayer();

        if (range == null ||
            range.intValue <= 0 ||
            layerMask == null ||
            (layerMask.intValue & interactableLayerBit) == 0)
        {
            throw new InvalidOperationException(
                "Farm player Interact requires a positive authored range and " +
                "must include the Interactable layer in its raycast mask.");
        }

        return range.intValue;
    }

    private static void ValidateLegacyChoreAuthoring(
        Scene scene,
        SerializedProperty choreReferences,
        IReadOnlyCollection<FarmChoreInteractable> authoredChores,
        Terrain terrain,
        IReadOnlyList<Vector3> choreWorldPositions,
        bool requireGroundedHooks,
        bool requireExactLegacyV4)
    {
        var authored = new HashSet<FarmChoreInteractable>(authoredChores);

        for (int index = 0; index < ChoreGroupNames.Length; index++)
        {
            GameObject choreObject = RequirePath(
                scene,
                FarmObjectivesPath + "/" + ChoreGroupNames[index]);
            FarmChoreInteractable chore =
                choreObject.GetComponent<FarmChoreInteractable>();

            if (chore == null ||
                !authored.Contains(chore) ||
                chore.ChoreId != LegacyChoreIds[index] ||
                chore.ObjectiveText != LegacyChoreObjectives[index] ||
                chore.RequiredInteractions != 1 ||
                (requireExactLegacyV4 && chore.RequiresInventoryItem) ||
                (requireExactLegacyV4 &&
                 choreReferences.GetArrayElementAtIndex(index)
                     .objectReferenceValue != chore) ||
                !Approximately(
                    choreObject.transform.position,
                    choreWorldPositions[index]))
            {
                throw new InvalidOperationException(
                    $"Farm chore '{ChoreGroupNames[index]}' is not authored at " +
                    "its deterministic interaction marker.");
            }

            GameObject propSocket =
                RequireDirectChild(choreObject, PropSocketName);
            GameObject approach =
                RequireDirectChild(choreObject, PlayerApproachName);
            ValidateTransformOnlySocket(
                propSocket,
                Vector3.zero,
                localPosition: true);

            if (requireGroundedHooks)
            {
                Vector3 expectedApproach = choreWorldPositions[index] +
                    new Vector3(0f, 0f, -2.75f);
                expectedApproach = GetTerrainGroundPosition(
                    terrain,
                    new Vector2(expectedApproach.x, expectedApproach.z));
                ValidateTransformOnlySocket(
                    approach,
                    expectedApproach,
                    localPosition: false);
                ValidateTerrainGrounded(
                    choreObject.transform,
                    terrain,
                    "Farm chore hook");
                ValidateTerrainGrounded(
                    propSocket.transform,
                    terrain,
                    "Farm prop socket");
                ValidateTerrainGrounded(
                    approach.transform,
                    terrain,
                    "Farm chore approach socket");
            }
            else
            {
                ValidateTransformOnlySocket(
                    approach,
                    new Vector3(0f, 0f, -2.75f),
                    localPosition: true);
            }
        }
    }

    private static void ValidateFarmSemanticSockets(
        Scene scene,
        Terrain terrain,
        IReadOnlyList<Vector3> propSocketWorldPositions,
        IReadOnlyList<Vector3> pigSocketWorldPositions,
        bool requireGroundedHooks)
    {
        GameObject coreRoot = RequirePath(scene, FarmCorePath);
        GameObject authoringRoot =
            RequireDirectChild(coreRoot, FarmAuthoringSocketsName);
        GameObject propRoot =
            RequireDirectChild(authoringRoot, FarmPropSocketsName);
        GameObject pigRoot =
            RequireDirectChild(authoringRoot, FarmPigSocketsName);

        ValidateTransformOnlyContainer(authoringRoot);
        ValidateTransformOnlyContainer(propRoot);
        ValidateTransformOnlyContainer(pigRoot);

        for (int index = 0; index < FarmPropSocketNames.Length; index++)
        {
            GameObject socket =
                RequireDirectChild(propRoot, FarmPropSocketNames[index]);
            RequireSingleNamedObject(scene, FarmPropSocketNames[index]);
            ValidateTransformOnlySocket(
                socket,
                propSocketWorldPositions[index],
                localPosition: false);

            if (requireGroundedHooks)
            {
                ValidateTerrainGrounded(
                    socket.transform,
                    terrain,
                    "Future Farm prop socket");
            }
        }

        for (int index = 0; index < FarmPigSocketNames.Length; index++)
        {
            GameObject socket =
                RequireDirectChild(pigRoot, FarmPigSocketNames[index]);
            RequireSingleNamedObject(scene, FarmPigSocketNames[index]);
            ValidateTransformOnlySocket(
                socket,
                pigSocketWorldPositions[index],
                localPosition: false);

            if (requireGroundedHooks)
            {
                ValidateTerrainGrounded(
                    socket.transform,
                    terrain,
                    "Future Farm pig socket");
            }
        }
    }

    private static void ValidateWakeAuthoring(Scene scene)
    {
        GameObject wakeRoot = RequirePath(scene, FarmDialoguePath +
            "/Wake Up Sequence");
        Transform prologueSpawn =
            RequirePath(scene, FarmPrologueSpawnPath).transform;
        if (!Approximately(
                prologueSpawn.position,
                FarmPrologueSpawnWorldPosition) ||
            Quaternion.Angle(
                prologueSpawn.rotation,
                FarmPrologueSpawnWorldRotation) > 0.01f)
        {
            throw new InvalidOperationException(
                "Farm Prologue Spawn must retain the exact online-Safety " +
                "farmhouse pose. Campaign authoring may not replace it with " +
                "the retired floor-probe alignment.");
        }

        ValidateUniqueWorldSocket(
            scene,
            wakeRoot,
            WakePlayerAnchorName,
            FarmSafetyWakePlayerWorldPosition,
            FarmPrologueSpawnWorldRotation);
        ValidateUniqueWorldSocket(
            scene,
            wakeRoot,
            WakeViewAnchorName,
            FarmSafetyWakeViewWorldPosition,
            FarmPrologueSpawnWorldRotation);
        ValidateUniqueWorldSocket(
            scene,
            wakeRoot,
            WakeLookTargetName,
            FarmSafetyWakeLookWorldPosition,
            FarmPrologueSpawnWorldRotation);
    }

    private static void ValidateCombatAuthoring(
        Scene scene,
        Terrain terrain,
        Vector3 playerAnchorPosition,
        bool requireGroundedHooks,
        IReadOnlyList<Vector3> choreApproachWorldPositions,
        bool allowLegacySafetyEnemyRoster,
        bool allowMissingFarmNavMeshData)
    {
        GameObject enemiesRoot = RequirePath(scene, FarmEnemiesPath);
        GameObject emergenceRoot =
            RequireDirectChild(enemiesRoot, EnemyEmergenceRootName);
        RequireSingleNamedObject(scene, EnemyEmergenceRootName);
        MobSpawner spawner = RequireSingleComponent<MobSpawner>(scene);
        waveManager encounter = RequireSingleComponent<waveManager>(scene);
        int spawnLayer = RequireSpawnVolumeLayer();
        var spawnerData = new SerializedObject(spawner);
        var encounterData = new SerializedObject(encounter);
        SerializedProperty spawnPoints =
            spawnerData.FindProperty("spawnPoint");
        SerializedProperty spawnRadius =
            spawnerData.FindProperty("spawnRadius");
        EnemyNavMeshProfile[] enemyNavMeshProfiles =
            ReadAuthoredEnemyNavMeshProfiles(
                spawnerData,
                allowLegacySafetyEnemyRoster);
        var emergenceColliders = new List<BoxCollider>();

        if (encounterData.FindProperty("totalWaves")?.intValue != 3 ||
            encounterData.FindProperty("startingEnemyCount")?.intValue != 3 ||
            encounterData.FindProperty("enemiesAddedPerWave")?.intValue != 2 ||
            (!allowLegacySafetyEnemyRoster &&
             encounterData.FindProperty("useHogHuntIntro")?.boolValue != false))
        {
            throw new InvalidOperationException(
                "Farm prologue encounter must preserve its authored 3/5/7 " +
                "three-wave configuration with the legacy hog-hunt intro disabled.");
        }

        if (spawnRadius == null ||
            !float.IsFinite(spawnRadius.floatValue) ||
            spawnRadius.floatValue <= 0f)
        {
            throw new InvalidOperationException(
                "Farm MobSpawner requires a positive NavMesh sampling radius.");
        }

        if (spawnPoints == null ||
            spawnPoints.arraySize != EmergenceZoneNames.Length)
        {
            throw new InvalidOperationException(
                "Farm MobSpawner must reference exactly three campaign " +
                "emergence zones.");
        }

        for (int index = 0; index < EmergenceZoneNames.Length; index++)
        {
            GameObject zone = RequireDirectChild(
                emergenceRoot,
                EmergenceZoneNames[index]);
            RequireSingleNamedObject(scene, EmergenceZoneNames[index]);
            BoxCollider collider = zone.GetComponent<BoxCollider>();

            if (zone.layer != spawnLayer ||
                zone.tag != "Untagged" ||
                !zone.activeSelf ||
                collider == null ||
                !collider.enabled ||
                !collider.isTrigger ||
                collider.size.x <= 0f ||
                collider.size.y <= 0f ||
                collider.size.z <= 0f ||
                spawnPoints.GetArrayElementAtIndex(index)
                    .objectReferenceValue != zone.transform)
            {
                throw new InvalidOperationException(
                    $"Farm emergence zone '{EmergenceZoneNames[index]}' " +
                    "must be an active Border-layer trigger referenced by " +
                    "MobSpawner in deterministic order.");
            }

            ValidateNavMeshExcluded(zone);
            ValidateEmergenceZonePresentation(zone);

            if (requireGroundedHooks)
            {
                Vector3 zonePosition = zone.transform.position;
                float groundY = GetTerrainGroundPosition(
                    terrain,
                    new Vector2(zonePosition.x, zonePosition.z)).y;

                if (Mathf.Abs(collider.bounds.min.y - groundY) > 0.1f)
                {
                    throw new InvalidOperationException(
                        $"Farm emergence zone '{zone.name}' must keep its " +
                        "lower face on the Terrain while preserving its spawn volume.");
                }
            }

            emergenceColliders.Add(collider);
        }

        GameObject playerAnchor =
            RequireDirectChild(enemiesRoot, CombatPlayerAnchorName);
        RequireSingleNamedObject(scene, CombatPlayerAnchorName);
        ValidateTransformOnlySocket(
            playerAnchor,
            playerAnchorPosition,
            localPosition: false);

        if (requireGroundedHooks)
        {
            ValidateTerrainGrounded(
                playerAnchor.transform,
                terrain,
                "Farm combat player anchor");
        }

        if (allowMissingFarmNavMeshData)
        {
            ValidateRepairableFarmNavMeshSurface(scene);
        }
        else
        {
            ValidateEmergenceZoneNavMeshCoverage(
                scene,
                emergenceColliders,
                spawnRadius.floatValue,
                playerAnchor.transform,
                enemyNavMeshProfiles,
                choreApproachWorldPositions);
        }

        if (spawner.GetComponentsInChildren<BoxCollider>(true)
            .Any(collider => collider.enabled && !collider.isTrigger))
        {
            throw new InvalidOperationException(
                "Farm MobSpawner retains an enabled solid spawn-volume " +
                "collider that would become an invisible combat barrier.");
        }

        GameObject boundsObject =
            RequireDirectChild(enemiesRoot, CombatBoundsName);
        RequireSingleNamedObject(scene, CombatBoundsName);
        BoxCollider boundsCollider =
            boundsObject.GetComponent<BoxCollider>();

        if (boundsObject.layer != spawnLayer ||
            boundsObject.tag != "Untagged" ||
            !boundsObject.activeSelf ||
            boundsCollider == null ||
            !boundsCollider.enabled ||
            !boundsCollider.isTrigger)
        {
            throw new InvalidOperationException(
                "Farm combat-area bounds must be an active, trigger-only " +
                "Border-layer authored volume.");
        }

        ValidateNavMeshExcluded(boundsObject);
        ValidateNoVisibleOrDynamicGeometry(boundsObject);

        GameObject rumbleRoot = RequirePath(
            scene,
            FarmDialoguePath + "/Ground Rumble Sequence");
        ValidateUniqueWorldSocket(
            scene,
            rumbleRoot,
            RumbleGroundOriginName,
            playerAnchorPosition,
            Quaternion.identity);

        if (requireGroundedHooks)
        {
            ValidateTerrainGrounded(
                RequireDirectChild(rumbleRoot, RumbleGroundOriginName).transform,
                terrain,
                "Farm rumble ground origin");
        }
        GameObject audioAnchor =
            RequireDirectChild(rumbleRoot, RumbleAudioAnchorName);
        RequireSingleNamedObject(scene, RumbleAudioAnchorName);

        if (!Approximately(
                audioAnchor.transform.position,
                playerAnchorPosition + Vector3.up * 0.25f) ||
            !Approximately(audioAnchor.transform.localScale, Vector3.one))
        {
            throw new InvalidOperationException(
                "Farm rumble audio anchor has an invalid deterministic pose.");
        }

        GameObject cameraAnchor =
            RequireDirectChild(rumbleRoot, RumbleCameraAnchorName);
        RequireSingleNamedObject(scene, RumbleCameraAnchorName);
        ValidateTransformOnlySocket(
            cameraAnchor,
            playerAnchorPosition + Vector3.up * 1.65f,
            localPosition: false);

        ValidateRumblePresentation(
            scene,
            rumbleRoot,
            audioAnchor);

        GameObject presentation =
            RequireDirectChild(emergenceRoot, EmergencePresentationName);
        RequireSingleNamedObject(scene, EmergencePresentationName);
        FarmEnemyEmergencePresenter[] presenters =
            FindSceneComponents<FarmEnemyEmergencePresenter>(scene);

        if (presenters.Length != 1 ||
            presenters[0].gameObject != presentation ||
            !presentation.activeInHierarchy ||
            rumbleRoot.GetComponent<FarmEnemyEmergencePresenter>() != null)
        {
            throw new InvalidOperationException(
                "Farm requires exactly one active emergence presenter under " +
                "Prologue Enemies so it remains bound during Combat.");
        }

        var presenterData = new SerializedObject(presenters[0]);
        SerializedProperty encounterReference =
            presenterData.FindProperty("waveEncounter");

        if (encounterReference == null ||
            encounterReference.objectReferenceValue != encounter)
        {
            throw new InvalidOperationException(
                "Farm emergence presenter is not bound to the exact wave encounter.");
        }

        ValidateGroundEmergenceSettings(presenterData);
    }

    private readonly struct EnemyNavMeshProfile
    {
        public EnemyNavMeshProfile(
            string sourceName,
            int agentTypeId,
            int agentAreaMask,
            float radius,
            float height)
        {
            SourceName = sourceName;
            AgentTypeId = agentTypeId;
            AgentAreaMask = agentAreaMask;
            Radius = radius;
            Height = height;
        }

        public string SourceName { get; }
        public int AgentTypeId { get; }
        public int AgentAreaMask { get; }
        public float Radius { get; }
        public float Height { get; }
    }

    internal static bool RecognizesLegacyMissingFarmNavMeshReference(
        byte[] sceneBytes)
    {
        if (sceneBytes == null || sceneBytes.Length == 0)
            return false;

        string sceneText;

        try
        {
            sceneText = new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false,
                    throwOnInvalidBytes: true)
                .GetString(sceneBytes);
        }
        catch (DecoderFallbackException)
        {
            return false;
        }

        MatchCollection references = Regex.Matches(
            sceneText,
            @"(?m)^\s*m_NavMeshData:\s*\{fileID:\s*(?<fileId>-?\d+)" +
            @"(?:,\s*guid:\s*(?<guid>[0-9a-fA-F]{32})," +
            @"\s*type:\s*(?<type>\d+))?\}\s*$");

        if (references.Count != 2)
            return false;

        int emptyReferences = 0;
        int legacyReferences = 0;

        foreach (Match reference in references)
        {
            string fileId = reference.Groups["fileId"].Value;
            string guid = reference.Groups["guid"].Value;
            string type = reference.Groups["type"].Value;

            if (fileId == "0" && string.IsNullOrEmpty(guid))
            {
                emptyReferences++;
                continue;
            }

            if (fileId == "23800000" &&
                string.Equals(
                    guid,
                    LegacyMissingFarmNavMeshGuid,
                    StringComparison.OrdinalIgnoreCase) &&
                type == "2")
            {
                legacyReferences++;
            }
        }

        return emptyReferences == 1 && legacyReferences == 1;
    }

    private static void RepairMissingFarmNavMeshDataIfRecognized(
        Scene farmScene,
        byte[] originalFarmSceneBytes)
    {
        NavMeshSurface[] surfaces =
            FindSceneComponents<NavMeshSurface>(farmScene);

        if (surfaces.Length != 1 || surfaces[0].navMeshData != null)
            return;

        if (!RecognizesLegacyMissingFarmNavMeshReference(
                originalFarmSceneBytes))
        {
            return;
        }

        if (!string.IsNullOrEmpty(
                AssetDatabase.GUIDToAssetPath(
                    LegacyMissingFarmNavMeshGuid)))
        {
            throw new InvalidOperationException(
                "The legacy Farm NavMesh GUID still resolves to an asset, " +
                "so the missing-data migration cannot safely infer a deleted " +
                "Safety bake.");
        }

        if (File.Exists(ToAbsolutePath(FarmNavMeshDataPath)) ||
            File.Exists(ToAbsolutePath(FarmNavMeshDataPath + ".meta")) ||
            AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                FarmNavMeshDataPath) != null)
        {
            throw new InvalidOperationException(
                $"The Farm scene has the recognized deleted Safety NavMesh " +
                $"reference, but the owned canonical path " +
                $"'{FarmNavMeshDataPath}' is already occupied. Restore a " +
                "consistent scene/asset pair before rebuilding.");
        }

        // Validate every current V5 contract except the one missing baked
        // object before creating an owned replacement. This prevents a
        // partial or merely similar scene from entering the repair path.
        ValidateFarm(
            farmScene,
            requireCurrentSignature: true,
            rootServiceSchema: RootServiceSchema.LegacyOrCurrent,
            allowLegacySafetyEnemyRoster: true,
            allowLegacyFarmItemOverride: true,
            allowMissingLoadoutEquipmentBridge: true,
            allowLegacyPlayerPrefab: true,
            allowMissingFarmNavMeshData: true,
            allowSafetyTruckExtractionMigration: true,
            allowCurrentV5RuntimeFieldMigration: true,
            allowMissingSafetyLoseLifecycleBridge: true);

        NavMeshSurface surface = ValidateRepairableFarmNavMeshSurface(
            farmScene);
        Terrain terrain = RequireSingleComponent<Terrain>(farmScene);
        ValidateFarmTerrain(terrain);
        surface.layerMask = CanonicalFarmNavMeshLayerMask;
        EditorUtility.SetDirty(surface);
        EnsureLegacyInfestationSpawnVolumeNavMeshExcluded(
            farmScene,
            terrain);
        Physics.SyncTransforms();
        surface.BuildNavMesh();
        NavMeshData builtData = surface.navMeshData;

        if (builtData == null || AssetDatabase.Contains(builtData))
        {
            throw new InvalidOperationException(
                "The isolated Farm NavMesh repair did not produce new, " +
                "unpersisted navigation data.");
        }

        builtData.name = Path.GetFileNameWithoutExtension(
            FarmNavMeshDataPath);
        EditorUtility.SetDirty(builtData);
        AssetDatabase.CreateAsset(builtData, FarmNavMeshDataPath);
        AssetDatabase.SaveAssetIfDirty(builtData);

        NavMeshData canonicalData =
            AssetDatabase.LoadAssetAtPath<NavMeshData>(
                FarmNavMeshDataPath);
        string canonicalGuid = AssetDatabase.AssetPathToGUID(
            FarmNavMeshDataPath);

        if (canonicalData == null ||
            canonicalData != builtData ||
            string.IsNullOrWhiteSpace(canonicalGuid))
        {
            throw new InvalidOperationException(
                "The Farm NavMesh repair could not persist its canonical " +
                "owned data asset and stable GUID.");
        }

        surface.RemoveData();
        surface.navMeshData = canonicalData;
        if (surface.isActiveAndEnabled)
            surface.AddData();

        try
        {
            LogFarmNavMeshRepairDiagnostics(
                farmScene,
                surface,
                terrain);
        }
        catch (Exception diagnosticException)
        {
            Debug.LogWarning(
                "Farm NavMesh repair diagnostics could not complete: " +
                diagnosticException);
        }

        EditorUtility.SetDirty(surface);
        EditorSceneManager.MarkSceneDirty(farmScene);
        Debug.Log(
            $"Repaired deleted Safety Farm NavMesh reference with owned " +
            $"canonical data '{FarmNavMeshDataPath}' ({canonicalGuid}).");
    }

    private static void LogFarmNavMeshRepairDiagnostics(
        Scene farmScene,
        NavMeshSurface surface,
        Terrain terrain)
    {
        const string prefix = "[FarmNavMeshRepairDiagnostic]";
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
        Vector3[] vertices = triangulation.vertices ?? Array.Empty<Vector3>();
        int[] indices = triangulation.indices ?? Array.Empty<int>();
        int[] areas = triangulation.areas ?? Array.Empty<int>();

        if (vertices.Length == 0)
        {
            Debug.LogWarning(
                $"{prefix} baked triangulation has zero vertices " +
                $"(indices={indices.Length}, areas={areas.Length}).");
        }
        else
        {
            Bounds bounds = new(vertices[0], Vector3.zero);
            for (int index = 1; index < vertices.Length; index++)
                bounds.Encapsulate(vertices[index]);

            string areaSummary = string.Join(
                ", ",
                areas.GroupBy(area => area)
                    .OrderBy(group => group.Key)
                    .Select(group => $"{group.Key}:{group.Count()}"));
            Debug.Log(
                $"{prefix} triangulation vertices={vertices.Length}, " +
                $"triangles={indices.Length / 3}, boundsMin=" +
                $"{bounds.min.ToString("F3")}, boundsMax=" +
                $"{bounds.max.ToString("F3")}, areas=[{areaSummary}].");
        }

        Vector3[] approaches = GetTerrainGroundPositions(
            terrain,
            ChoreApproachWorldXZ);
        float[] radii = { 0.75f, 2f, 5f, 10f };
        var filter = new NavMeshQueryFilter
        {
            agentTypeID = surface.agentTypeID,
            areaMask = MobSpawnerWalkableAreaMask
        };
        Vector3 terrainOrigin = terrain.transform.position;
        Vector3 terrainSize = terrain.terrainData.size;

        for (int index = 0; index < approaches.Length; index++)
        {
            Vector3 approach = approaches[index];
            float normalizedX = Mathf.InverseLerp(
                terrainOrigin.x,
                terrainOrigin.x + terrainSize.x,
                approach.x);
            float normalizedZ = Mathf.InverseLerp(
                terrainOrigin.z,
                terrainOrigin.z + terrainSize.z,
                approach.z);
            float sampledHeight = terrain.SampleHeight(approach) +
                                  terrainOrigin.y;
            float slope = terrain.terrainData.GetSteepness(
                normalizedX,
                normalizedZ);
            Vector3 terrainNormal = terrain.terrainData
                .GetInterpolatedNormal(normalizedX, normalizedZ);
            Ray ray = new(
                new Vector3(approach.x, sampledHeight + 25f, approach.z),
                Vector3.down);
            bool rayFound = Physics.Raycast(
                ray,
                out RaycastHit rayHit,
                100f,
                1 << FarmTerrainLayerIndex,
                QueryTriggerInteraction.Ignore);
            string raySummary = rayFound
                ? $"hit={rayHit.collider.name} point=" +
                  $"{rayHit.point.ToString("F3")} normal=" +
                  $"{rayHit.normal.ToString("F3")}"
                : "no Terrain-layer collider hit";

            Debug.Log(
                $"{prefix} {ChoreSteps[index].Name}/" +
                $"{PlayerApproachName} authored={approach.ToString("F3")}, " +
                $"terrainHeight={sampledHeight:F3}, slope={slope:F3}deg, " +
                $"terrainNormal={terrainNormal.ToString("F3")}, {raySummary}.");

            foreach (float radius in radii)
            {
                bool found = NavMesh.SamplePosition(
                    approach,
                    out NavMeshHit hit,
                    radius,
                    filter);
                string sampleSummary = found
                    ? $"hit={hit.position.ToString("F3")}, " +
                      $"distance={Vector3.Distance(approach, hit.position):F3}, " +
                      $"planar={PlanarDistance(approach, hit.position):F3}, " +
                      $"mask={hit.mask}"
                    : "no agent-0 Walkable sample";
                Debug.Log(
                    $"{prefix} {ChoreSteps[index].Name}/" +
                    $"{PlayerApproachName} radius={radius:F2}: " +
                    sampleSummary + ".");
            }
        }

        Vector3 firstApproach = approaches[0];
        NavMeshModifier[] activeModifiers =
            FindSceneComponents<NavMeshModifier>(farmScene)
                .Where(modifier =>
                    modifier.isActiveAndEnabled &&
                    modifier.AffectsAgentType(surface.agentTypeID))
                .ToArray();
        int relevantModifierCount = 0;

        foreach (NavMeshModifier modifier in activeModifiers)
        {
            bool hasBounds = TryGetNavMeshModifierBounds(
                modifier,
                out Bounds modifierBounds);
            Vector3 closest = hasBounds
                ? modifierBounds.ClosestPoint(firstApproach)
                : modifier.transform.position;
            float planarDistance = PlanarDistance(firstApproach, closest);
            bool hierarchyRelevant =
                RequirePath(
                        farmScene,
                        FarmObjectivesPath + "/" + ChoreGroupNames[0] + "/" +
                        ChoreSteps[0].Name)
                    .transform.IsChildOf(modifier.transform) ||
                modifier.transform.IsChildOf(
                    RequirePath(
                            farmScene,
                            FarmObjectivesPath + "/" + ChoreGroupNames[0] + "/" +
                            ChoreSteps[0].Name)
                        .transform);

            if (!hierarchyRelevant && planarDistance > 10f)
                continue;

            relevantModifierCount++;
            Debug.Log(
                $"{prefix} STEP01-near NavMeshModifier " +
                $"'{GetTransformPath(modifier.transform)}': " +
                $"ignore={modifier.ignoreFromBuild}, " +
                $"applyToChildren={modifier.applyToChildren}, " +
                $"overrideArea={modifier.overrideArea}, area={modifier.area}, " +
                $"sourceBounds=" +
                $"{(hasBounds ? modifierBounds.ToString() : "none")}, " +
                $"planarDistance={planarDistance:F3}.");
        }

        NavMeshModifierVolume[] activeVolumes =
            FindSceneComponents<NavMeshModifierVolume>(farmScene)
                .Where(volume =>
                    volume.isActiveAndEnabled &&
                    volume.AffectsAgentType(surface.agentTypeID))
                .ToArray();
        int relevantVolumeCount = 0;

        foreach (NavMeshModifierVolume volume in activeVolumes)
        {
            Bounds bounds = GetModifierVolumeWorldBounds(volume);
            float planarDistance = PlanarDistance(
                firstApproach,
                bounds.ClosestPoint(firstApproach));
            if (planarDistance > 10f)
                continue;

            relevantVolumeCount++;
            Debug.Log(
                $"{prefix} STEP01-near NavMeshModifierVolume " +
                $"'{GetTransformPath(volume.transform)}': area={volume.area}, " +
                $"bounds={bounds}, planarDistance={planarDistance:F3}.");
        }

        Debug.Log(
            $"{prefix} STEP01 modifier summary: " +
            $"activeAgentModifiers={activeModifiers.Length}, " +
            $"within10mOrHierarchy={relevantModifierCount}, " +
            $"activeAgentVolumes={activeVolumes.Length}, " +
            $"volumesWithin10m={relevantVolumeCount}.");
    }

    private static bool TryGetNavMeshModifierBounds(
        NavMeshModifier modifier,
        out Bounds bounds)
    {
        IEnumerable<Collider> colliders = modifier.applyToChildren
            ? modifier.GetComponentsInChildren<Collider>(true)
            : modifier.GetComponents<Collider>();
        IEnumerable<Renderer> renderers = modifier.applyToChildren
            ? modifier.GetComponentsInChildren<Renderer>(true)
            : modifier.GetComponents<Renderer>();
        bool initialized = false;
        bounds = default;

        foreach (Collider collider in colliders.Where(collider =>
                     collider != null &&
                     collider.enabled &&
                     collider.gameObject.activeInHierarchy))
        {
            if (!initialized)
            {
                bounds = collider.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        foreach (Renderer renderer in renderers.Where(renderer =>
                     renderer != null &&
                     renderer.enabled &&
                     renderer.gameObject.activeInHierarchy))
        {
            if (!initialized)
            {
                bounds = renderer.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return initialized;
    }

    private static Bounds GetModifierVolumeWorldBounds(
        NavMeshModifierVolume volume)
    {
        Vector3 halfSize = volume.size * 0.5f;
        Vector3 firstCorner = volume.transform.TransformPoint(
            volume.center + new Vector3(
                -halfSize.x,
                -halfSize.y,
                -halfSize.z));
        Bounds bounds = new(firstCorner, Vector3.zero);

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    bounds.Encapsulate(volume.transform.TransformPoint(
                        volume.center + new Vector3(
                            halfSize.x * x,
                            halfSize.y * y,
                            halfSize.z * z)));
                }
            }
        }

        return bounds;
    }

    private static string GetTransformPath(Transform transform)
    {
        var names = new Stack<string>();
        Transform current = transform;

        while (current != null)
        {
            names.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", names);
    }

    private static NavMeshSurface ValidateRepairableFarmNavMeshSurface(
        Scene farmScene)
    {
        NavMeshSurface[] surfaces =
            FindSceneComponents<NavMeshSurface>(farmScene);

        if (surfaces.Length != 1)
        {
            throw new InvalidOperationException(
                "The recognized Farm NavMesh repair requires exactly one " +
                "authored NavMeshSurface.");
        }

        NavMeshSurface surface = surfaces[0];
        Transform transform = surface.transform;

        if (surface.navMeshData != null ||
            !surface.isActiveAndEnabled ||
            surface.gameObject.name != "Level NavMesh Surface" ||
            surface.gameObject.layer != 0 ||
            surface.gameObject.tag != "Untagged" ||
            transform.parent != null ||
            !Approximately(transform.position, Vector3.zero) ||
            Quaternion.Angle(transform.rotation, Quaternion.identity) > 0.01f ||
            !Approximately(transform.localScale, Vector3.one) ||
            surface.agentTypeID != 0 ||
            surface.collectObjects != CollectObjects.All ||
            surface.layerMask.value != MobSpawnerWalkableAreaMask ||
            surface.useGeometry != NavMeshCollectGeometry.PhysicsColliders ||
            !surface.ignoreNavMeshAgent ||
            !surface.ignoreNavMeshObstacle ||
            surface.overrideVoxelSize ||
            surface.buildHeightMesh)
        {
            throw new InvalidOperationException(
                "The missing Farm NavMeshSurface does not match the exact " +
                "active agent-0, world-collider Safety surface contract; " +
                "automatic repair was refused.");
        }

        return surface;
    }

    private static void ValidateCanonicalFarmNavMeshSurface(
        Scene farmScene,
        NavMeshSurface surface)
    {
        Terrain terrain = RequireSingleComponent<Terrain>(farmScene);
        ValidateFarmTerrain(terrain);
        Transform transform = surface != null ? surface.transform : null;

        if (surface == null ||
            surface.navMeshData == null ||
            !surface.isActiveAndEnabled ||
            surface.gameObject.name != "Level NavMesh Surface" ||
            surface.gameObject.layer != 0 ||
            surface.gameObject.tag != "Untagged" ||
            transform.parent != null ||
            !Approximately(transform.position, Vector3.zero) ||
            Quaternion.Angle(transform.rotation, Quaternion.identity) > 0.01f ||
            !Approximately(transform.localScale, Vector3.one) ||
            surface.agentTypeID != 0 ||
            surface.collectObjects != CollectObjects.All ||
            surface.layerMask.value != CanonicalFarmNavMeshLayerMask ||
            surface.useGeometry != NavMeshCollectGeometry.PhysicsColliders ||
            !surface.ignoreNavMeshAgent ||
            !surface.ignoreNavMeshObstacle ||
            surface.overrideVoxelSize ||
            surface.buildHeightMesh)
        {
            throw new InvalidOperationException(
                "Farm canonical NavMeshSurface must preserve the exact " +
                "agent-0 world-collider contract and collect only Default " +
                $"plus Terrain layers (mask {CanonicalFarmNavMeshLayerMask}).");
        }
    }

    private static bool ValidateFarmEnemyRoster(
        SerializedObject spawnerData,
        bool allowLegacySafetyEnemyRoster)
    {
        SerializedProperty fallbackProperty =
            spawnerData.FindProperty("Enemy");
        SerializedProperty enemiesProperty =
            spawnerData.FindProperty("enemies");

        if (fallbackProperty == null || enemiesProperty == null ||
            !enemiesProperty.isArray)
        {
            throw new InvalidOperationException(
                "Farm MobSpawner enemy authoring could not be inspected.");
        }

        GameObject[] currentRoster = RequireFarmBoarRoster();
        GameObject boar = currentRoster[0];
        GameObject boarRoot = currentRoster[2];
        GameObject fallback =
            fallbackProperty.objectReferenceValue as GameObject;

        if (fallback != boar || enemiesProperty.arraySize != 3 ||
            enemiesProperty.GetArrayElementAtIndex(0)
                .objectReferenceValue != boar)
        {
            throw new InvalidOperationException(
                "Farm MobSpawner must use the exact online-safety Boar as " +
                "its fallback and first wave roster entry.");
        }

        GameObject second = enemiesProperty.GetArrayElementAtIndex(1)
            .objectReferenceValue as GameObject;
        GameObject third = enemiesProperty.GetArrayElementAtIndex(2)
            .objectReferenceValue as GameObject;
        bool exactCurrentBoarRoster =
            second == boar && third == boarRoot;

        if (exactCurrentBoarRoster)
            return false;

        GameObject legacySafetyScreecher =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                LegacySafetyScreecherPrefabPath);
        GameObject legacySafetyJuggernaut =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                LegacySafetyJuggernautPrefabPath);
        bool exactLegacySafetyRoster =
            second == legacySafetyScreecher &&
            third == legacySafetyJuggernaut;

        if (exactLegacySafetyRoster)
        {
            if (!allowLegacySafetyEnemyRoster)
            {
                throw new InvalidOperationException(
                    "Farm MobSpawner still references its retired raw Safety " +
                    "enemy roster. Rebuild the campaign foundation to migrate " +
                    "the prologue to [Boar, Boar, BoarRoot].");
            }

            return true;
        }

        GameObject legacyScreecherVariant =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                LegacyCampaignScreecherVariantPath);
        GameObject legacyJuggernautVariant =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                LegacyCampaignJuggernautVariantPath);
        bool exactLegacyCampaignRoster =
            second == legacyScreecherVariant &&
            third == legacyJuggernautVariant &&
            legacyScreecherVariant != null &&
            legacyJuggernautVariant != null;

        if (!exactLegacyCampaignRoster || !allowLegacySafetyEnemyRoster)
        {
            throw new InvalidOperationException(
                "Farm MobSpawner enemy roster must be exactly " +
                "[online-Safety Boar, online-Safety Boar, online-Safety " +
                "BoarRoot]. Only either exact retired roster may enter the " +
                "bounded preflight migration.");
        }

        // The prior campaign variants already supplied Animators, so their
        // one-time migration does not need the raw-Safety Animator exemption.
        return false;
    }

    private static EnemyNavMeshProfile[] ReadAuthoredEnemyNavMeshProfiles(
        SerializedObject spawnerData,
        bool allowLegacySafetyEnemyRoster)
    {
        SerializedProperty fallbackProperty =
            spawnerData.FindProperty("Enemy");
        SerializedProperty enemiesProperty =
            spawnerData.FindProperty("enemies");

        if (fallbackProperty == null ||
            enemiesProperty == null ||
            !enemiesProperty.isArray)
        {
            throw new InvalidOperationException(
                "Farm MobSpawner enemy authoring could not be inspected.");
        }

        bool usesLegacySafetyRoster = ValidateFarmEnemyRoster(
            spawnerData,
            allowLegacySafetyEnemyRoster);

        var candidates = new List<GameObject>();
        var seenCandidates = new HashSet<GameObject>();
        bool requiresFallback = enemiesProperty.arraySize == 0;

        for (int index = 0; index < enemiesProperty.arraySize; index++)
        {
            GameObject enemy = enemiesProperty
                .GetArrayElementAtIndex(index)
                .objectReferenceValue as GameObject;

            if (enemy == null)
            {
                requiresFallback = true;
                continue;
            }

            if (seenCandidates.Add(enemy))
                candidates.Add(enemy);
        }

        GameObject fallbackEnemy =
            fallbackProperty.objectReferenceValue as GameObject;

        if (requiresFallback && fallbackEnemy == null)
        {
            throw new InvalidOperationException(
                "Farm MobSpawner needs a fallback Enemy for its empty or " +
                "null authored enemy entries.");
        }

        if (fallbackEnemy != null && seenCandidates.Add(fallbackEnemy))
            candidates.Add(fallbackEnemy);

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException(
                "Farm MobSpawner has no authored enemy prefab to validate.");
        }

        var profiles = new List<EnemyNavMeshProfile>(candidates.Count);

        foreach (GameObject prefab in candidates)
        {
            NavMeshAgent[] rootAgents = prefab.GetComponents<NavMeshAgent>();
            string assetPath = AssetDatabase.GetAssetPath(prefab);
            string sourceName = string.IsNullOrWhiteSpace(assetPath)
                ? prefab.name
                : assetPath;

            if (rootAgents.Length != 1 || !rootAgents[0].enabled)
            {
                throw new InvalidOperationException(
                    $"Authored Farm enemy '{sourceName}' must have exactly " +
                    "one enabled NavMeshAgent on its root because the " +
                    "protected enemy runtime reads the root agent.");
            }

            NavMeshAgent agent = rootAgents[0];

            enemyAI[] rootEnemyControllers = prefab.GetComponents<enemyAI>();
            if (rootEnemyControllers.Length != 1 ||
                !rootEnemyControllers[0].enabled)
            {
                throw new InvalidOperationException(
                    $"Authored Farm enemy '{sourceName}' must have exactly " +
                    "one enabled online-safety enemyAI controller on its root.");
            }

            Animator[] authoredAnimators =
                prefab.GetComponentsInChildren<Animator>(true);
            bool legacyAnimatorExemption = usesLegacySafetyRoster &&
                (assetPath == LegacySafetyScreecherPrefabPath ||
                 assetPath == LegacySafetyJuggernautPrefabPath);

            if (!legacyAnimatorExemption &&
                (authoredAnimators.Length == 0 ||
                !authoredAnimators.Any(candidate =>
                    candidate != null &&
                    candidate.enabled &&
                    candidate.runtimeAnimatorController != null)))
            {
                throw new InvalidOperationException(
                    $"Authored Farm enemy '{sourceName}' requires an enabled " +
                    "child Animator with a controller for the online-safety " +
                    "enemyAI contract.");
            }

            if ((agent.areaMask & MobSpawnerWalkableAreaMask) == 0)
            {
                throw new InvalidOperationException(
                    $"Authored Farm enemy '{sourceName}' excludes Walkable " +
                    $"area mask {MobSpawnerWalkableAreaMask} and cannot use " +
                    "the MobSpawner's protected sampling contract.");
            }

            NavMeshBuildSettings settings =
                NavMesh.GetSettingsByID(agent.agentTypeID);

            if (settings.agentTypeID != agent.agentTypeID ||
                !float.IsFinite(agent.radius) ||
                agent.radius <= 0f ||
                !float.IsFinite(agent.height) ||
                agent.height <= 0f)
            {
                throw new InvalidOperationException(
                    $"Authored Farm enemy '{sourceName}' has an invalid " +
                    "NavMeshAgent type or dimensions.");
            }

            profiles.Add(new EnemyNavMeshProfile(
                sourceName,
                agent.agentTypeID,
                agent.areaMask,
                agent.radius,
                agent.height));
        }

        return profiles.ToArray();
    }

    private static void ValidateEmergenceZoneNavMeshCoverage(
        Scene farmScene,
        IReadOnlyList<BoxCollider> zones,
        float sampleRadius,
        Transform playerAnchor,
        IReadOnlyList<EnemyNavMeshProfile> enemyProfiles,
        IReadOnlyList<Vector3> choreApproachWorldPositions)
    {
        const int samplesAcrossLongAxis = 7;
        const int samplesAcrossShortAxis = 3;
        NavMeshSurface[] farmSurfaces =
            FindSceneComponents<NavMeshSurface>(farmScene)
                .Where(surface => surface.navMeshData != null)
                .ToArray();

        if (farmSurfaces.Length != 1 ||
            !farmSurfaces[0].isActiveAndEnabled)
        {
            throw new InvalidOperationException(
                "Farm prologue combat requires exactly one active, authored, " +
                "baked NavMeshSurface before its emergence zones can be validated.");
        }

        NavMeshSurface farmSurface = farmSurfaces[0];
        ValidateCanonicalFarmNavMeshSurface(farmScene, farmSurface);

        if (!string.Equals(
                AssetDatabase.GetAssetPath(farmSurface.navMeshData),
                FarmNavMeshDataPath,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Farm NavMeshSurface must reference the campaign-owned " +
                $"canonical data asset '{FarmNavMeshDataPath}'.");
        }

        foreach (EnemyNavMeshProfile profile in enemyProfiles)
        {
            if (profile.AgentTypeId != farmSurface.agentTypeID)
            {
                throw new InvalidOperationException(
                    $"Authored Farm enemy '{profile.SourceName}' uses agent " +
                    $"type {profile.AgentTypeId}, but the sole Farm " +
                    $"NavMeshSurface is baked for type {farmSurface.agentTypeID}.");
            }
        }

        NavMeshSurface[] otherActiveSurfaces =
            Resources.FindObjectsOfTypeAll<NavMeshSurface>()
                .Where(surface =>
                    surface != null &&
                    surface.gameObject.scene.IsValid() &&
                    surface.gameObject.scene.isLoaded &&
                    surface.gameObject.scene != farmScene &&
                    surface.navMeshData != null &&
                    surface.isActiveAndEnabled)
                .ToArray();

        var removedSurfaces = new List<NavMeshSurface>();

        try
        {
            foreach (NavMeshSurface surface in otherActiveSurfaces)
            {
                // Track the surface before removal so even a partially
                // failing RemoveData call is followed by a restoration
                // attempt in finally.
                removedSurfaces.Add(surface);
                surface.RemoveData();
            }

            ValidateChoreNavMeshRoute(
                farmSurface,
                choreApproachWorldPositions,
                playerAnchor.position);

            foreach (EnemyNavMeshProfile profile in enemyProfiles)
            {
                var spawnFilter = new NavMeshQueryFilter
                {
                    agentTypeID = profile.AgentTypeId,
                    areaMask = MobSpawnerWalkableAreaMask
                };
                var pathFilter = new NavMeshQueryFilter
                {
                    agentTypeID = profile.AgentTypeId,
                    areaMask = profile.AgentAreaMask
                };
                float anchorMaxDisplacement =
                    Mathf.Min(
                        sampleRadius,
                        CombatAnchorMaxNavMeshDisplacement);
                float anchorMaxPlanarDisplacement =
                    CombatAnchorMaxPlanarNavMeshDisplacement;

                bool foundAnchor = NavMesh.SamplePosition(
                    playerAnchor.position,
                    out NavMeshHit anchorHit,
                    anchorMaxDisplacement,
                    pathFilter);
                float anchorDisplacement = foundAnchor &&
                                           IsFinite(anchorHit.position)
                    ? Vector3.Distance(
                        playerAnchor.position,
                        anchorHit.position)
                    : float.PositiveInfinity;
                float anchorPlanarDisplacement = foundAnchor &&
                                                 IsFinite(anchorHit.position)
                    ? PlanarDistance(
                        playerAnchor.position,
                        anchorHit.position)
                    : float.PositiveInfinity;

                if (!foundAnchor ||
                    !IsValidNavMeshHit(
                        playerAnchor.position,
                        anchorHit,
                        profile.AgentAreaMask,
                        anchorMaxDisplacement,
                        anchorMaxPlanarDisplacement))
                {
                    throw new InvalidOperationException(
                        $"Farm combat player anchor has no nearby agent-compatible " +
                        $"NavMesh for authored enemy '{profile.SourceName}' " +
                        $"(agent type {profile.AgentTypeId}, nearest sampled " +
                        $"displacement {anchorDisplacement:F2}m total / " +
                        $"{anchorPlanarDisplacement:F2}m planar, maximum " +
                        $"allowed {anchorMaxDisplacement:F2}m total / " +
                        $"{anchorMaxPlanarDisplacement:F2}m planar).");
                }

                foreach (BoxCollider zone in zones)
                {
                    Bounds runtimeBounds = zone.bounds;
                    float maximumSampleDisplacement = Mathf.Min(
                        sampleRadius,
                        Mathf.Max(
                            2f,
                            runtimeBounds.extents.y + profile.Height));
                    float maximumPlanarDisplacement =
                        Mathf.Max(0.5f, profile.Radius * 2f);

                    for (int xIndex = 0;
                         xIndex < samplesAcrossLongAxis;
                         xIndex++)
                    {
                        float xT = xIndex / (samplesAcrossLongAxis - 1f);
                        float localX = Mathf.Lerp(
                            runtimeBounds.min.x,
                            runtimeBounds.max.x,
                            xT);

                        for (int zIndex = 0;
                             zIndex < samplesAcrossShortAxis;
                             zIndex++)
                        {
                            float zT =
                                zIndex / (samplesAcrossShortAxis - 1f);
                            float localZ = Mathf.Lerp(
                                runtimeBounds.min.z,
                                runtimeBounds.max.z,
                                zT);
                            // Mirror protected MobSpawner.TryGetSpawnPoint:
                            // x/z come from BoxCollider.bounds (world AABB),
                            // while y is the spawn-point Transform position.
                            Vector3 sample = new(
                                localX,
                                zone.transform.position.y,
                                localZ);

                            // Runtime MobSpawner uses literal area mask 1.
                            // Intersect that contract with the prefab agent's
                            // authored mask and use its authored agent type.
                            if (!NavMesh.SamplePosition(
                                    sample,
                                    out NavMeshHit hit,
                                    maximumSampleDisplacement,
                                    spawnFilter) ||
                                !IsValidNavMeshHit(
                                    sample,
                                    hit,
                                    MobSpawnerWalkableAreaMask,
                                    maximumSampleDisplacement,
                                    maximumPlanarDisplacement))
                            {
                                throw new InvalidOperationException(
                                    $"Farm emergence zone '{zone.name}' " +
                                    $"contains a spawn sample without nearby " +
                                    $"Walkable NavMesh for authored enemy " +
                                    $"'{profile.SourceName}' (agent type " +
                                    $"{profile.AgentTypeId}, maximum allowed " +
                                    $"sample displacement " +
                                    $"{maximumSampleDisplacement:F2}m total / " +
                                    $"{maximumPlanarDisplacement:F2}m planar). " +
                                    "Reposition the zone or rebake the Farm NavMesh.");
                            }

                            var path = new NavMeshPath();

                            if (!NavMesh.CalculatePath(
                                    hit.position,
                                    anchorHit.position,
                                    pathFilter,
                                    path) ||
                                path.status != NavMeshPathStatus.PathComplete ||
                                path.corners.Any(corner => !IsFinite(corner)))
                            {
                                throw new InvalidOperationException(
                                    $"Farm emergence zone '{zone.name}' " +
                                    $"contains a spawn sample with no complete " +
                                    $"NavMesh path to {CombatPlayerAnchorName} " +
                                    $"for authored enemy '{profile.SourceName}' " +
                                    $"(agent type {profile.AgentTypeId}).");
                            }
                        }
                    }
                }
            }
        }
        finally
        {
            Exception restorationFailure = null;

            foreach (NavMeshSurface surface in removedSurfaces)
            {
                try
                {
                    surface.AddData();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, surface);
                    restorationFailure ??= exception;
                }
            }

            if (restorationFailure != null)
            {
                throw new InvalidOperationException(
                    "Campaign validation could not restore another loaded " +
                    "scene's NavMeshSurface after isolated Farm sampling.",
                    restorationFailure);
            }
        }
    }

    private static void ValidateChoreNavMeshRoute(
        NavMeshSurface farmSurface,
        IReadOnlyList<Vector3> choreApproachWorldPositions,
        Vector3 combatAnchorPosition)
    {
        if (choreApproachWorldPositions == null ||
            choreApproachWorldPositions.Count == 0)
        {
            return;
        }

        if (choreApproachWorldPositions.Count != ChoreSteps.Length)
        {
            throw new InvalidOperationException(
                "Farm V5 NavMesh validation requires all eight chore approaches.");
        }

        // The player uses a CharacterController and intentionally starts on
        // authored farmhouse flooring, so Prologue Spawn is not a NavMesh
        // waypoint. Prove the actual walkable chore chain beginning at the
        // first authored approach instead.
        var routeNames = new List<string>(ChoreSteps.Length + 1);
        var routePositions = new List<Vector3>(ChoreSteps.Length + 1);

        for (int index = 0; index < ChoreSteps.Length; index++)
        {
            routeNames.Add(ChoreSteps[index].Name + "/" + PlayerApproachName);
            routePositions.Add(choreApproachWorldPositions[index]);
        }

        routeNames.Add(CombatPlayerAnchorName);
        routePositions.Add(combatAnchorPosition);

        var filter = new NavMeshQueryFilter
        {
            agentTypeID = farmSurface.agentTypeID,
            areaMask = MobSpawnerWalkableAreaMask
        };
        var sampledPositions = new Vector3[routePositions.Count];

        for (int index = 0; index < routePositions.Count; index++)
        {
            Vector3 authored = routePositions[index];

            if (!NavMesh.SamplePosition(
                    authored,
                    out NavMeshHit hit,
                    ChoreApproachMaxNavMeshDisplacement,
                    filter) ||
                !IsValidNavMeshHit(
                    authored,
                    hit,
                    MobSpawnerWalkableAreaMask,
                    ChoreApproachMaxNavMeshDisplacement,
                    ChoreApproachMaxNavMeshDisplacement))
            {
                throw new InvalidOperationException(
                    $"Farm chore route marker '{routeNames[index]}' has no " +
                    "Walkable NavMesh within " +
                    $"{ChoreApproachMaxNavMeshDisplacement:F2}m. Reposition " +
                    "the marker or rebake the Farm NavMesh.");
            }

            sampledPositions[index] = hit.position;
        }

        for (int index = 1; index < sampledPositions.Length; index++)
        {
            if (Vector3.Distance(
                    sampledPositions[index - 1],
                    sampledPositions[index]) <= NavMeshDistanceTolerance)
            {
                continue;
            }

            var path = new NavMeshPath();

            if (!NavMesh.CalculatePath(
                    sampledPositions[index - 1],
                    sampledPositions[index],
                    filter,
                    path) ||
                path.status != NavMeshPathStatus.PathComplete ||
                path.corners.Any(corner => !IsFinite(corner)))
            {
                throw new InvalidOperationException(
                    $"Farm chore route has no complete NavMesh path from " +
                    $"'{routeNames[index - 1]}' to '{routeNames[index]}'.");
            }
        }
    }

    private static bool IsValidNavMeshHit(
        Vector3 sample,
        NavMeshHit hit,
        int requiredAreaMask,
        float maximumDisplacement,
        float maximumPlanarDisplacement)
    {
        if (!IsFinite(sample) ||
            !IsFinite(hit.position) ||
            (hit.mask & requiredAreaMask) == 0)
        {
            return false;
        }

        float displacement = Vector3.Distance(sample, hit.position);
        float planarDisplacement = PlanarDistance(sample, hit.position);

        return float.IsFinite(displacement) &&
               float.IsFinite(planarDisplacement) &&
               displacement <= maximumDisplacement + NavMeshDistanceTolerance &&
               planarDisplacement <=
               maximumPlanarDisplacement + NavMeshDistanceTolerance;
    }

    private static float PlanarDistance(Vector3 first, Vector3 second)
    {
        float x = first.x - second.x;
        float z = first.z - second.z;
        return Mathf.Sqrt(x * x + z * z);
    }

    private static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.x) &&
               float.IsFinite(value.y) &&
               float.IsFinite(value.z);
    }

    private static void ValidateRumblePresentation(
        Scene scene,
        GameObject rumbleRoot,
        GameObject audioAnchor)
    {
        FarmRumblePresenter[] presenters =
            FindSceneComponents<FarmRumblePresenter>(scene);
        AudioSource[] audioSources =
            audioAnchor.GetComponents<AudioSource>();
        cameraController cameraLook =
            RequireSingleComponent<cameraController>(scene);
        FarmPrologueDirector director =
            RequirePath(scene, FarmDirectorPath)
                .GetComponent<FarmPrologueDirector>();

        if (presenters.Length != 1 ||
            presenters[0].gameObject != rumbleRoot ||
            audioSources.Length != 1 ||
            audioAnchor.GetComponents<Component>().Length != 2 ||
            audioAnchor.GetComponent<Renderer>() != null ||
            audioAnchor.GetComponent<Rigidbody>() != null)
        {
            throw new InvalidOperationException(
                "Farm requires one authored rumble presenter and one " +
                "non-visual AudioSource anchor.");
        }

        AudioSource audioSource = audioSources[0];

        if (audioSource.playOnAwake ||
            audioSource.loop ||
            audioSource.spatialBlend < 0.999f ||
            audioSource.dopplerLevel > 0.001f ||
            audioSource.minDistance <= 0f ||
            audioSource.maxDistance <= audioSource.minDistance)
        {
            throw new InvalidOperationException(
                "Farm rumble AudioSource must be an authored, spatial, " +
                "non-looping source that never plays on awake.");
        }

        var presenterData = new SerializedObject(presenters[0]);
        SerializedProperty directorReference =
            presenterData.FindProperty("director");
        SerializedProperty cameraReference =
            presenterData.FindProperty("cameraTransform");
        SerializedProperty audioReference =
            presenterData.FindProperty("rumbleAudioSource");
        SerializedProperty amplitude =
            presenterData.FindProperty("shakeAmplitude");
        SerializedProperty frequency =
            presenterData.FindProperty("shakeFrequency");

        if (directorReference == null ||
            directorReference.objectReferenceValue != director ||
            cameraReference == null ||
            cameraReference.objectReferenceValue != cameraLook.transform ||
            audioReference == null ||
            audioReference.objectReferenceValue != audioSource ||
            amplitude == null || amplitude.floatValue <= 0f ||
            frequency == null || frequency.floatValue <= 0f)
        {
            throw new InvalidOperationException(
                "Farm rumble presenter references or shake settings are incomplete.");
        }
    }

    private static void ValidateGroundEmergenceSettings(
        SerializedObject presenterData)
    {
        SerializedProperty enabled =
            presenterData.FindProperty("animateGroundEmergence");
        SerializedProperty depth =
            presenterData.FindProperty("emergenceDepth");
        SerializedProperty heightMultiplier =
            presenterData.FindProperty("rendererHeightDepthMultiplier");
        SerializedProperty maximumDepth =
            presenterData.FindProperty("maximumEmergenceDepth");
        SerializedProperty duration =
            presenterData.FindProperty("emergenceDuration");
        SerializedProperty stagger =
            presenterData.FindProperty("emergenceStaggerSeconds");

        if (enabled == null || !enabled.boolValue ||
            depth == null || depth.floatValue <= 0f ||
            heightMultiplier == null || heightMultiplier.floatValue < 0f ||
            maximumDepth == null ||
            maximumDepth.floatValue < depth.floatValue ||
            duration == null || duration.floatValue <= 0f ||
            stagger == null || stagger.floatValue < 0f)
        {
            throw new InvalidOperationException(
                "Farm ground-emergence timing and depth settings must be " +
                "enabled and strictly valid.");
        }
    }

    private static void ValidateLegacyCompletionTrigger(Scene scene)
    {
        GameObject legacyTrigger =
            RequirePath(scene, FarmLegacyCompletionTriggerPath);
        Collider collider = legacyTrigger.GetComponent<Collider>();

        if (legacyTrigger.activeSelf ||
            (collider != null && collider.enabled))
        {
            throw new InvalidOperationException(
                "Legacy Complete Prologue Trigger must remain inactive; " +
                "FarmPrologueDirector owns completion.");
        }
    }

    private static void ValidateHubTravelSockets(GameObject travel)
    {
        ValidateTransformOnlySocket(
            RequireDirectChild(travel, HubTruckSocketName),
            Vector3.zero,
            localPosition: true);
        ValidateTransformOnlySocket(
            RequireDirectChild(travel, HubTruckApproachName),
            new Vector3(0f, 0f, -5f),
            localPosition: true);
    }

    private static void ValidateNavMeshExcluded(GameObject target)
    {
        NavMeshModifier[] modifiers =
            target.GetComponents<NavMeshModifier>();

        if (modifiers.Length != 1 ||
            !modifiers[0].ignoreFromBuild ||
            modifiers[0].applyToChildren ||
            modifiers[0].overrideArea)
        {
            throw new InvalidOperationException(
                $"'{target.name}' must have one non-recursive " +
                "NavMeshModifier configured to ignore its marker collider.");
        }
    }

    private static void ValidateNoVisibleOrDynamicGeometry(
        GameObject target,
        bool allowKinematicRigidbody = false)
    {
        Rigidbody body = target.GetComponent<Rigidbody>();
        bool invalidBody = body != null &&
            (!allowKinematicRigidbody || !body.isKinematic || body.useGravity);

        if (target.GetComponentsInChildren<Renderer>(true).Length != 0 ||
            target.GetComponentsInChildren<MeshFilter>(true).Length != 0 ||
            invalidBody)
        {
            throw new InvalidOperationException(
                $"Authoring marker '{target.name}' must not contain visible " +
                "geometry or an unexpected dynamic Rigidbody.");
        }
    }

    private static void ValidateEmergenceZonePresentation(GameObject zone)
    {
        if (zone.GetComponent<Renderer>() != null ||
            zone.GetComponent<MeshFilter>() != null ||
            zone.GetComponent<Rigidbody>() != null)
        {
            throw new InvalidOperationException(
                $"Emergence marker '{zone.name}' must keep its own marker " +
                "object non-visual and non-dynamic.");
        }

        Transform visualRoot = zone.transform.Find(
            AlphaEmergenceVisualRootName);
        Renderer[] renderers = zone.GetComponentsInChildren<Renderer>(true);
        MeshFilter[] meshFilters =
            zone.GetComponentsInChildren<MeshFilter>(true);

        if ((renderers.Length > 0 || meshFilters.Length > 0) &&
            visualRoot == null)
        {
            throw new InvalidOperationException(
                $"Emergence marker '{zone.name}' contains visible geometry " +
                $"outside its owned '{AlphaEmergenceVisualRootName}' child.");
        }

        if (visualRoot == null)
            return;

        if (renderers.Any(renderer =>
                !renderer.transform.IsChildOf(visualRoot)) ||
            meshFilters.Any(filter =>
                !filter.transform.IsChildOf(visualRoot)) ||
            visualRoot.GetComponentsInChildren<Collider>(true).Length != 0 ||
            visualRoot.GetComponentsInChildren<Rigidbody>(true).Length != 0)
        {
            throw new InvalidOperationException(
                $"Owned emergence presentation under '{zone.name}' must be " +
                "visual-only and remain beneath its dedicated child root.");
        }
    }

    private static void ValidateUniqueWorldSocket(
        Scene scene,
        GameObject parent,
        string name,
        Vector3 position,
        Quaternion rotation)
    {
        GameObject socket = RequireDirectChild(parent, name);
        RequireSingleNamedObject(scene, name);
        ValidateTransformOnlySocket(
            socket,
            position,
            localPosition: false,
            rotation: rotation);
    }

    private static void ValidateTransformOnlyContainer(GameObject target)
    {
        if (target.GetComponents<Component>().Length != 1 ||
            !Approximately(target.transform.localPosition, Vector3.zero) ||
            Quaternion.Angle(
                target.transform.localRotation,
                Quaternion.identity) > 0.01f ||
            !Approximately(target.transform.localScale, Vector3.one))
        {
            throw new InvalidOperationException(
                $"Semantic socket container '{target.name}' must be an " +
                "identity Transform with no runtime components.");
        }
    }

    private static void ValidateTransformOnlySocket(
        GameObject socket,
        Vector3 position,
        bool localPosition,
        Quaternion? rotation = null)
    {
        Vector3 actualPosition = localPosition
            ? socket.transform.localPosition
            : socket.transform.position;
        Quaternion actualRotation = localPosition
            ? socket.transform.localRotation
            : socket.transform.rotation;

        if (socket.GetComponents<Component>().Length != 1 ||
            !Approximately(actualPosition, position) ||
            !Approximately(socket.transform.localScale, Vector3.one) ||
            (rotation.HasValue &&
             Quaternion.Angle(actualRotation, rotation.Value) > 0.01f))
        {
            throw new InvalidOperationException(
                $"Semantic socket '{socket.name}' has an invalid pose or " +
                "contains runtime components. Attach future content as children.");
        }
    }

    private static bool Approximately(Vector3 left, Vector3 right)
    {
        return (left - right).sqrMagnitude <= 0.0001f;
    }

    private static bool Exactly(Vector3 left, Vector3 right)
    {
        return left.x.Equals(right.x) &&
               left.y.Equals(right.y) &&
               left.z.Equals(right.z);
    }

    private static bool Exactly(Quaternion left, Quaternion right)
    {
        return left.x.Equals(right.x) &&
               left.y.Equals(right.y) &&
               left.z.Equals(right.z) &&
               left.w.Equals(right.w);
    }

    private static void ValidateOpenWorld(
        Scene scene,
        bool requireCurrentSignature = true,
        bool requireCurrentRuntimeHooks = true,
        RootServiceSchema rootServiceSchema = RootServiceSchema.CurrentOnly,
        bool allowMissingLoadoutEquipmentBridge = false,
        bool allowLegacyPlayerPrefab = false,
        bool allowSafetyExtractionMigration = false,
        bool allowCurrentV5RuntimeFieldMigration = false,
        bool allowPreviousV3FeedbackUiMigration = false,
        bool allowMissingSafetyLoseLifecycleBridge = false)
    {
        RequireValidLoadedScene(scene, OpenWorldScenePath);
        ValidateSafetyItemDatabaseSource(scene);
        ValidateSafetyLoseLifecycleBridge(
            scene,
            RequireSingleComponent<gameManager>(scene),
            allowMissingSafetyLoseLifecycleBridge);

        if (requireCurrentSignature)
        {
            ValidateSignature(scene);
        }

        ValidateRootService(
            scene,
            rootServiceSchema,
            allowCurrentV5RuntimeFieldMigration);
        ValidateLoadoutEquipmentBridge(
            scene,
            allowMissingLoadoutEquipmentBridge,
            allowCurrentV5RuntimeFieldMigration);

        if (!allowLegacyPlayerPrefab)
        {
            ValidateSafetyPlayerSceneAuthority(
                scene,
                RequirePath(scene, OpenWorldArrivalPath).transform);
            ValidateOpenWorldSafetySpawnMarkers(scene);
        }

        CampaignOpenWorldProgression progression =
            RequirePath(scene, OpenWorldProgressionPath)
                .GetComponent<CampaignOpenWorldProgression>();

        if (progression == null)
        {
            throw new InvalidOperationException(
                "Open-world campaign progression component is missing.");
        }

        OpenWorldAreaBarrier[] barriers =
            FindSceneComponents<OpenWorldAreaBarrier>(scene);

        if (barriers.Length != 3 ||
            barriers.Select(barrier => barrier.Area).Distinct().Count() != 3 ||
            barriers.Any(barrier => barrier.StartsUnlocked))
        {
            throw new InvalidOperationException(
                "Open World must contain exactly three unique, fail-closed " +
                "progression barriers.");
        }

        SerializedObject progressionData = new(progression);
        SerializedProperty stateProperty =
            progressionData.FindProperty("campaignState");
        SerializedProperty barrierProperty =
            progressionData.FindProperty("areaBarriers");
        SerializedProperty missionRootProperty =
            progressionData.FindProperty("areaMissionRoots");

        if (stateProperty == null ||
            stateProperty.objectReferenceValue == null ||
            barrierProperty == null ||
            barrierProperty.arraySize != barriers.Length)
        {
            throw new InvalidOperationException(
                "Open-world campaign progression references are incomplete.");
        }

        var serializedBarriers = new HashSet<OpenWorldAreaBarrier>();

        for (int index = 0; index < barrierProperty.arraySize; index++)
        {
            serializedBarriers.Add(
                barrierProperty.GetArrayElementAtIndex(index)
                    .objectReferenceValue as OpenWorldAreaBarrier);
        }

        if (serializedBarriers.Contains(null) ||
            !serializedBarriers.SetEquals(barriers))
        {
            throw new InvalidOperationException(
                "Open-world progression does not reference the exact authored " +
                "barrier set.");
        }

        if (requireCurrentRuntimeHooks)
        {
            if (missionRootProperty == null ||
                missionRootProperty.arraySize !=
                    OpenWorldMissionSystemNames.Length)
            {
                throw new InvalidOperationException(
                    "Open-world progression requires four serialized mission " +
                    "roots in exact campaign order.");
            }

            for (int index = 0;
                 index < OpenWorldMissionSystemNames.Length;
                 index++)
            {
                GameObject expectedMissionRoot = RequireSingleNamedObject(
                    scene,
                    OpenWorldMissionSystemNames[index]);

                if (missionRootProperty.GetArrayElementAtIndex(index)
                        .objectReferenceValue != expectedMissionRoot)
                {
                    throw new InvalidOperationException(
                        "Open-world progression mission roots are not " +
                        "serialized in exact campaign order.");
                }
            }
        }

        GameObject arrival = RequirePath(scene, OpenWorldArrivalPath);
        CampaignSpawnPoint spawn = arrival.GetComponent<CampaignSpawnPoint>();

        if (spawn == null || spawn.DestinationId != "BlackPinesArrival")
        {
            throw new InvalidOperationException(
                "Black Pines arrival spawn is not configured.");
        }

        ValidateOpenWorldExtractionState(
            scene,
            allowSafetyExtractionMigration);
        if (!allowSafetyExtractionMigration)
        {
            ValidateSafetyExtractionGate(
                RequireSingleComponent<gameManager>(scene),
                allowLegacyZero: false);
        }

        if (requireCurrentRuntimeHooks)
        {
            ValidateOpenWorldRuntimeHooks(
                scene,
                progression,
                barriers,
                allowPreviousV3FeedbackUiMigration);
        }
    }

    private static void ValidateOpenWorldRuntimeHooks(
        Scene scene,
        CampaignOpenWorldProgression progression,
        OpenWorldAreaBarrier[] barriers,
        bool allowPreviousV3FeedbackUiMigration = false)
    {
        CampaignAreaCompletionRelay[] relays =
            FindSceneComponents<CampaignAreaCompletionRelay>(scene);

        if (relays.Length != OpenWorldMissionAreaIds.Length)
        {
            throw new InvalidOperationException(
                "Open World requires one explicit completion relay for each " +
                "campaign area.");
        }

        for (int index = 0;
             index < OpenWorldMissionSystemNames.Length;
             index++)
        {
            GameObject missionRoot = RequireSingleNamedObject(
                scene,
                OpenWorldMissionSystemNames[index]);
            CampaignAreaCompletionRelay relay =
                missionRoot.GetComponent<CampaignAreaCompletionRelay>();

            if (relay == null ||
                relay.Progression != progression ||
                relay.Area != OpenWorldMissionAreaIds[index])
            {
                throw new InvalidOperationException(
                    $"Mission root '{OpenWorldMissionSystemNames[index]}' " +
                    "does not own its exact campaign completion relay.");
            }
        }

        gameManager manager = RequireSingleComponent<gameManager>(scene);
        CampaignLockedAreaFeedbackPresenter presenter =
            manager.GetComponent<CampaignLockedAreaFeedbackPresenter>();
        TMP_Text expectedText = RequireNamedText(scene, "Game Goal Data");
        int presenterCount =
            FindSceneComponents<CampaignLockedAreaFeedbackPresenter>(scene)
                .Length;
        bool exactCurrentPresenter =
            presenter != null &&
            presenter.MessageText == expectedText &&
            presenter.MessageCanvasGroup == null &&
            Mathf.Approximately(presenter.DisplaySeconds, 3f) &&
            presenter.isActiveAndEnabled &&
            presenterCount == 1;
        bool recognizedPreviousV3PresenterMigration =
            allowPreviousV3FeedbackUiMigration &&
            IsRecognizedPreviousV3FeedbackPresenterMigration(
                manager.gameObject,
                presenter,
                expectedText,
                presenterCount);

        if (!exactCurrentPresenter &&
            !recognizedPreviousV3PresenterMigration)
        {
            throw new InvalidOperationException(
                "Open World locked-area feedback must reuse the single " +
                "authored Game Goal Data text without creating UI.");
        }

        CampaignLockedAreaFeedbackTrigger[] feedbackTriggers =
            FindSceneComponents<CampaignLockedAreaFeedbackTrigger>(scene);

        if (feedbackTriggers.Length != barriers.Length)
        {
            throw new InvalidOperationException(
                "Every Open World barrier requires exactly one authored " +
                "locked-area feedback trigger.");
        }

        var globallyOwnedBlockers = new HashSet<Collider>();

        foreach (OpenWorldAreaBarrier barrier in barriers)
        {
            GameObject triggerObject = barrier.LockedFeedbackTrigger;
            CampaignLockedAreaFeedbackTrigger feedback =
                triggerObject != null
                    ? triggerObject.GetComponent<
                        CampaignLockedAreaFeedbackTrigger>()
                    : null;
            Collider triggerCollider =
                triggerObject != null
                    ? triggerObject.GetComponent<Collider>()
                    : null;
            Rigidbody triggerBody =
                triggerObject != null
                    ? triggerObject.GetComponent<Rigidbody>()
                    : null;
            SerializedProperty blockerProperty =
                new SerializedObject(barrier)
                    .FindProperty("blockingColliders");

            if (feedback == null ||
                !barrier.isActiveAndEnabled ||
                !barrier.gameObject.activeInHierarchy ||
                !feedback.isActiveAndEnabled ||
                (feedback.Presenter != presenter &&
                 (!recognizedPreviousV3PresenterMigration ||
                  feedback.Presenter != null)) ||
                feedback.LockedArea != ToCampaignArea(barrier.Area) ||
                triggerCollider == null ||
                !triggerObject.activeInHierarchy ||
                !triggerCollider.enabled ||
                !triggerCollider.isTrigger ||
                triggerObject.layer != RequireSpawnVolumeLayer() ||
                triggerBody == null ||
                !triggerBody.isKinematic ||
                triggerBody.useGravity ||
                blockerProperty == null ||
                blockerProperty.arraySize <= 0)
            {
                throw new InvalidOperationException(
                    $"Barrier '{barrier.name}' has incomplete locked-area " +
                    "feedback wiring.");
            }

            ValidateNavMeshExcludedBySelfOrAncestor(
                triggerObject,
                barrier.gameObject);
            ValidateNoVisibleOrDynamicGeometry(
                triggerObject,
                allowKinematicRigidbody: true);

            for (int index = 0;
                 index < blockerProperty.arraySize;
                 index++)
            {
                Collider blocker = blockerProperty
                    .GetArrayElementAtIndex(index)
                    .objectReferenceValue as Collider;

                if (blocker == null ||
                    !globallyOwnedBlockers.Add(blocker) ||
                    !barrier.OwnsCollider(blocker) ||
                    !blocker.gameObject.activeInHierarchy ||
                    !blocker.enabled ||
                    blocker.isTrigger ||
                    blocker.gameObject.layer != RequireSpawnVolumeLayer())
                {
                    throw new InvalidOperationException(
                        $"Barrier '{barrier.name}' contains an invalid or " +
                        "non-owned blocking collider.");
                }

                ValidateNavMeshExcludedBySelfOrAncestor(
                    blocker.gameObject,
                    barrier.gameObject);
                ValidateNoVisibleOrDynamicGeometry(blocker.gameObject);
            }
        }
    }

    private static bool IsRecognizedPreviousV3FeedbackPresenterMigration(
        GameObject managerObject,
        CampaignLockedAreaFeedbackPresenter presenter,
        TMP_Text exactCurrentText,
        int scenePresenterCount)
    {
        if (managerObject == null || exactCurrentText == null)
            return false;

        if (presenter == null)
            return scenePresenterCount == 0;

        return scenePresenterCount == 1 &&
               presenter.gameObject == managerObject &&
               presenter.MessageText == null &&
               presenter.MessageCanvasGroup == null &&
               Mathf.Approximately(presenter.DisplaySeconds, 3f) &&
               presenter.isActiveAndEnabled;
    }

    internal static bool
        IsRecognizedPreviousV3FeedbackPresenterMigrationForTests(
            GameObject managerObject,
            CampaignLockedAreaFeedbackPresenter presenter,
            TMP_Text exactCurrentText,
            int scenePresenterCount)
    {
        return IsRecognizedPreviousV3FeedbackPresenterMigration(
            managerObject,
            presenter,
            exactCurrentText,
            scenePresenterCount);
    }

    private static void ValidateNavMeshExcludedBySelfOrAncestor(
        GameObject target,
        GameObject owningRoot)
    {
        int validModifiers = 0;
        Transform stopAfter =
            owningRoot != null ? owningRoot.transform.parent : null;

        for (Transform current = target.transform;
             current != null && current != stopAfter;
             current = current.parent)
        {
            foreach (NavMeshModifier modifier in
                     current.GetComponents<NavMeshModifier>())
            {
                bool coversTarget =
                    current == target.transform ||
                    modifier.applyToChildren;

                if (modifier.enabled &&
                    modifier.ignoreFromBuild &&
                    coversTarget)
                {
                    validModifiers++;
                }
            }
        }

        if (validModifiers != 1)
        {
            throw new InvalidOperationException(
                $"'{target.name}' must be covered by exactly one enabled " +
                "NavMeshModifier configured to ignore its collider.");
        }
    }

    private static void ValidateRootService(
        Scene scene,
        RootServiceSchema schema = RootServiceSchema.CurrentOnly,
        bool allowCurrentV5RuntimeFieldMigration = false)
    {
        GameObject serviceRoot = RequireSingleNamedObject(scene, ServiceRootName);
        CampaignStateService[] sceneStateServices =
            FindSceneComponents<CampaignStateService>(scene);
        CampaignInventoryCarryover[] sceneCarryovers =
            FindSceneComponents<CampaignInventoryCarryover>(scene);
        CampaignStateService[] rootStateServices =
            serviceRoot.GetComponents<CampaignStateService>();
        CampaignInventoryCarryover[] rootCarryovers =
            serviceRoot.GetComponents<CampaignInventoryCarryover>();
        int stateServiceCount = rootStateServices.Length;
        int carryoverCount = rootCarryovers.Length;
        bool exactCurrentShape =
            serviceRoot.activeInHierarchy &&
            sceneStateServices.Length == 1 &&
            sceneCarryovers.Length == 1 &&
            stateServiceCount == 1 &&
            carryoverCount == 1 &&
            rootStateServices[0].enabled &&
            rootCarryovers[0].enabled &&
            serviceRoot.GetComponents<Component>().Length == 3;
        bool exactMigrationShape =
            serviceRoot.activeInHierarchy &&
            sceneStateServices.Length == 1 &&
            sceneCarryovers.Length == 0 &&
            stateServiceCount == 1 &&
            carryoverCount == 0 &&
            rootStateServices[0].enabled &&
            serviceRoot.GetComponents<Component>().Length == 2;
        bool acceptsCurrent = schema != RootServiceSchema.LegacyOnly;
        bool acceptsLegacy = schema != RootServiceSchema.CurrentOnly;

        if (serviceRoot.transform.parent != null ||
            serviceRoot.transform.childCount != 0 ||
            (!acceptsCurrent || !exactCurrentShape) &&
            (!acceptsLegacy || !exactMigrationShape))
        {
            throw new InvalidOperationException(
                $"{scene.name}/{ServiceRootName} must be a root containing only " +
                (schema == RootServiceSchema.CurrentOnly
                    ? "Transform, CampaignStateService, and CampaignInventoryCarryover."
                    : schema == RootServiceSchema.LegacyOnly
                        ? "Transform and CampaignStateService in the exact legacy shape."
                        : "the exact legacy or current campaign service components.") +
                $" Observed parent={(serviceRoot.transform.parent == null ? "<root>" : serviceRoot.transform.parent.name)}, " +
                $"children={serviceRoot.transform.childCount}, active={serviceRoot.activeInHierarchy}, " +
                $"sceneState={sceneStateServices.Length}, sceneCarryover={sceneCarryovers.Length}, " +
                $"rootState={stateServiceCount}, rootCarryover={carryoverCount}, " +
                $"components=[{string.Join(", ", serviceRoot.GetComponents<Component>().Select(component => component != null ? component.GetType().FullName : "<missing>"))}].");
        }

        if (carryoverCount == 0)
            return;

        CampaignInventoryCarryover carryover =
            serviceRoot.GetComponent<CampaignInventoryCarryover>();
        if ((allowCurrentV5RuntimeFieldMigration &&
             IsRecognizedCurrentV5InventoryFieldMigration(carryover)) ||
            IsRecognizedSafetyLeatherAliasInventoryMigration(carryover))
        {
            return;
        }

        if (!carryover.ValidateStableInventoryCatalog(
                out string catalogError))
        {
            throw new InvalidOperationException(catalogError);
        }

        ValidateExactInventoryCatalog(carryover);
    }

    private static void ValidateLoadoutEquipmentBridge(
        Scene scene,
        bool allowMissing,
        bool allowCurrentV5RuntimeFieldMigration = false)
    {
        GameObject[] roots = scene.GetRootGameObjects()
            .Where(root => root.name ==
                CampaignLoadoutEquipmentBridge.AuthoringRootName)
            .ToArray();
        CampaignLoadoutEquipmentBridge[] bridges =
            FindSceneComponents<CampaignLoadoutEquipmentBridge>(scene);
        if (allowMissing && roots.Length == 0 && bridges.Length == 0)
            return;

        if (roots.Length != 1 ||
            bridges.Length != 1 ||
            roots[0] != bridges[0].gameObject)
        {
            throw new InvalidOperationException(
                $"Scene '{scene.name}' requires exactly one top-level " +
                $"'{CampaignLoadoutEquipmentBridge.AuthoringRootName}' " +
                "loadout-equipment authority.");
        }

        GameObject root = roots[0];
        CampaignLoadoutEquipmentBridge bridge = bridges[0];
        if (root.scene != scene ||
            root.transform.parent != null ||
            !root.activeSelf ||
            !bridge.enabled ||
            root.transform.childCount != 0 ||
            root.GetComponents<Component>().Length != 2 ||
            root.GetComponents<CampaignLoadoutEquipmentBridge>().Length != 1 ||
            !Approximately(root.transform.position, Vector3.zero) ||
            Quaternion.Angle(
                root.transform.rotation,
                Quaternion.identity) > 0.001f ||
            !Approximately(root.transform.localScale, Vector3.one))
        {
            throw new InvalidOperationException(
                $"Scene '{scene.name}' loadout equipment must be one active " +
                "identity top-level root containing only Transform and " +
                "CampaignLoadoutEquipmentBridge.");
        }

        CampaignStateService[] states =
            FindSceneComponents<CampaignStateService>(scene);
        CampaignInventoryCarryover[] carryovers =
            FindSceneComponents<CampaignInventoryCarryover>(scene);
        if (states.Length != 1 || carryovers.Length != 1)
        {
            throw new InvalidOperationException(
                $"Scene '{scene.name}' loadout equipment requires one exact " +
                "campaign state and carryover authority.");
        }

        GameObject rifle = LoadRequiredExactAsset<GameObject>(
            RiflePickupPath,
            RiflePickupGuid);
        GameObject ammo = LoadRequiredExactAsset<GameObject>(
            AmmoPickupPath,
            AmmoPickupGuid);
        GameObject radar = LoadRequiredExactAsset<GameObject>(
            RadarPickupPath,
            RadarPickupGuid);
        gunStats pistolStats = LoadRequiredExactAsset<gunStats>(
            PistolStatsPath,
            PistolStatsGuid);
        gunStats rifleStats = LoadRequiredExactAsset<gunStats>(
            RifleStatsPath,
            RifleStatsGuid);
        gunStats shotgunStats = LoadRequiredExactAsset<gunStats>(
            ShotgunStatsPath,
            ShotgunStatsGuid);
        bool configurationValid =
            bridge.ValidateConfiguration(out string error);
        bool exactCommonConfiguration =
            bridge.CampaignState == states[0] &&
            bridge.InventoryCarryover == carryovers[0] &&
            bridge.RifleInventoryPickup == rifle &&
            bridge.RifleAmmoInventoryPickup == ammo &&
            bridge.RadarInventoryPickup == radar;
        bool exactCurrentConfiguration =
            exactCommonConfiguration &&
            bridge.PistolDefinition == pistolStats &&
            bridge.RifleDefinition == rifleStats &&
            bridge.ShotgunDefinition == shotgunStats &&
            configurationValid;
        bool recognizedCurrentV5Migration =
            allowCurrentV5RuntimeFieldMigration &&
            exactCommonConfiguration &&
            IsRecognizedCurrentV5LoadoutFieldMigration(
                bridge,
                rifleStats);
        if (!exactCurrentConfiguration && !recognizedCurrentV5Migration)
        {
            throw new InvalidOperationException(
                $"Scene '{scene.name}' loadout-equipment bridge does not " +
                "preserve its exact scene authorities, inventory pickup " +
                "GUIDs, or ordered Safety pistol/rifle/shotgun definitions. " +
                error);
        }
    }

    private static bool IsRecognizedCurrentV5InventoryFieldMigration(
        CampaignInventoryCarryover carryover)
    {
        if (carryover == null)
            return false;

        CampaignInventoryItemBinding[] expected = CreateInventoryCatalog();
        CampaignInventoryItemBinding[] expectedSupplemental =
            CreateSafetyInventoryCatalog();
        SerializedObject carryoverData = new(carryover);
        SerializedProperty catalog = carryoverData.FindProperty("itemCatalog");
        SerializedProperty supplemental =
            carryoverData.FindProperty("safetyInventoryCatalog");
        SerializedProperty carrier =
            carryoverData.FindProperty("safetyInventoryCarrierPrefab");
        SerializedProperty playerTag = carryoverData.FindProperty("playerTag");
        SerializedProperty lookupTimeout =
            carryoverData.FindProperty("playerLookupTimeout");
        if (catalog == null || supplemental == null || carrier == null ||
            playerTag == null ||
            playerTag.stringValue != "Player" ||
            lookupTimeout == null ||
            !Mathf.Approximately(lookupTimeout.floatValue, 5f))
        {
            return false;
        }

        bool exactLegacyRuntimeFieldMigration =
            (catalog.arraySize == expected.Length ||
             catalog.arraySize == expected.Length - 1 ||
             catalog.arraySize == expected.Length - 2) &&
            InventoryCatalogEntriesMatch(
                catalog,
                expected,
                catalog.arraySize) &&
            supplemental.arraySize == 0 &&
            carrier.objectReferenceValue == null;
        bool exactD656SupplementalExpansion =
            catalog.arraySize == expected.Length &&
            InventoryCatalogEntriesMatch(
                catalog,
                expected,
                expected.Length) &&
            supplemental.arraySize == expectedSupplemental.Length - 1 &&
            InventoryCatalogEntriesMatch(
                supplemental,
                expectedSupplemental,
                expectedSupplemental.Length - 1) &&
            carrier.objectReferenceValue == RequireExactSafetyTruckKeyPrefab();
        bool exactV6HeartrootCatalogExpansion =
            catalog.arraySize == expected.Length - 1 &&
            InventoryCatalogEntriesMatch(
                catalog,
                expected,
                expected.Length - 1) &&
            supplemental.arraySize == expectedSupplemental.Length &&
            InventoryCatalogEntriesMatch(
                supplemental,
                expectedSupplemental,
                expectedSupplemental.Length) &&
            carrier.objectReferenceValue == RequireExactSafetyTruckKeyPrefab();
        SerializedProperty safetyLeatherEntry = supplemental.arraySize == 2
            ? supplemental.GetArrayElementAtIndex(0)
            : null;
        SerializedProperty safetyLeatherId = safetyLeatherEntry?
            .FindPropertyRelative("itemId");
        SerializedProperty safetyLeatherPrefab = safetyLeatherEntry?
            .FindPropertyRelative("pickupPrefab");
        SerializedProperty legacyMasterEntry = supplemental.arraySize == 2
            ? supplemental.GetArrayElementAtIndex(1)
            : null;
        SerializedProperty legacyMasterId = legacyMasterEntry?
            .FindPropertyRelative("itemId");
        SerializedProperty legacyMasterPrefab = legacyMasterEntry?
            .FindPropertyRelative("pickupPrefab");
        bool exactSafetyLeatherAliasMigration =
            catalog.arraySize == expected.Length &&
            InventoryCatalogEntriesMatch(
                catalog,
                expected,
                expected.Length) &&
            supplemental.arraySize == 2 &&
            safetyLeatherId != null &&
            safetyLeatherId.stringValue == expectedSupplemental[0].ItemId &&
            safetyLeatherPrefab != null &&
            safetyLeatherPrefab.objectReferenceValue ==
                expectedSupplemental[0].PickupPrefab &&
            legacyMasterId != null &&
            legacyMasterId.stringValue == "item_pickup_master" &&
            legacyMasterPrefab != null &&
            legacyMasterPrefab.objectReferenceValue ==
                AssetDatabase.LoadAssetAtPath<GameObject>(ItemPickupMasterPath) &&
            carrier.objectReferenceValue == RequireExactSafetyTruckKeyPrefab();
        return exactLegacyRuntimeFieldMigration ||
               exactD656SupplementalExpansion ||
               exactV6HeartrootCatalogExpansion ||
               exactSafetyLeatherAliasMigration;
    }

    private static bool IsRecognizedSafetyLeatherAliasInventoryMigration(
        CampaignInventoryCarryover carryover)
    {
        if (carryover == null)
            return false;

        CampaignInventoryItemBinding[] expected = CreateInventoryCatalog();
        CampaignInventoryItemBinding[] expectedSupplemental =
            CreateSafetyInventoryCatalog();
        SerializedObject carryoverData = new(carryover);
        SerializedProperty catalog = carryoverData.FindProperty("itemCatalog");
        SerializedProperty supplemental =
            carryoverData.FindProperty("safetyInventoryCatalog");
        SerializedProperty carrier =
            carryoverData.FindProperty("safetyInventoryCarrierPrefab");
        if (catalog == null || supplemental == null || carrier == null ||
            catalog.arraySize != expected.Length ||
            !InventoryCatalogEntriesMatch(
                catalog,
                expected,
                expected.Length) ||
            supplemental.arraySize != 2)
        {
            return false;
        }

        SerializedProperty leatherEntry = supplemental.GetArrayElementAtIndex(0);
        SerializedProperty masterEntry = supplemental.GetArrayElementAtIndex(1);
        SerializedProperty leatherId = leatherEntry.FindPropertyRelative("itemId");
        SerializedProperty leatherPrefab =
            leatherEntry.FindPropertyRelative("pickupPrefab");
        SerializedProperty masterId = masterEntry.FindPropertyRelative("itemId");
        SerializedProperty masterPrefab =
            masterEntry.FindPropertyRelative("pickupPrefab");
        return leatherId != null &&
               leatherId.stringValue == expectedSupplemental[0].ItemId &&
               leatherPrefab != null &&
               leatherPrefab.objectReferenceValue ==
                   expectedSupplemental[0].PickupPrefab &&
               masterId != null &&
               masterId.stringValue == "item_pickup_master" &&
               masterPrefab != null &&
               masterPrefab.objectReferenceValue ==
                   AssetDatabase.LoadAssetAtPath<GameObject>(ItemPickupMasterPath) &&
               carrier.objectReferenceValue ==
                   RequireExactSafetyTruckKeyPrefab();
    }

    private static bool IsRecognizedCurrentV5LoadoutFieldMigration(
        CampaignLoadoutEquipmentBridge bridge,
        gunStats expectedRifle)
    {
        return bridge != null &&
               expectedRifle != null &&
               bridge.PistolDefinition == null &&
               bridge.RifleDefinition == expectedRifle &&
               bridge.ShotgunDefinition == null;
    }

    internal static bool
        IsRecognizedCurrentV5InventoryFieldMigrationForTests(
            CampaignInventoryCarryover carryover)
    {
        return IsRecognizedCurrentV5InventoryFieldMigration(carryover);
    }

    internal static bool
        IsRecognizedCurrentV5LoadoutFieldMigrationForTests(
            CampaignLoadoutEquipmentBridge bridge)
    {
        gunStats rifleStats = LoadRequiredExactAsset<gunStats>(
            RifleStatsPath,
            RifleStatsGuid);
        return IsRecognizedCurrentV5LoadoutFieldMigration(
            bridge,
            rifleStats);
    }

    private static bool InventoryCatalogEntriesMatch(
        SerializedProperty catalog,
        IReadOnlyList<CampaignInventoryItemBinding> expected,
        int count)
    {
        if (catalog == null || expected == null ||
            count < 0 || count > expected.Count ||
            catalog.arraySize != count)
        {
            return false;
        }

        for (int index = 0; index < count; index++)
        {
            SerializedProperty entry =
                catalog.GetArrayElementAtIndex(index);
            SerializedProperty itemId =
                entry.FindPropertyRelative("itemId");
            SerializedProperty pickup =
                entry.FindPropertyRelative("pickupPrefab");
            if (itemId == null || pickup == null ||
                itemId.stringValue != expected[index].ItemId ||
                pickup.objectReferenceValue != expected[index].PickupPrefab)
            {
                return false;
            }
        }

        return true;
    }

    private static CampaignInventoryItemBinding[] CreateInventoryCatalog()
    {
        return new[]
        {
            CreateInventoryBinding("m1_garand", RiflePickupPath),
            CreateInventoryBinding("m1_garand_ammo", AmmoPickupPath),
            CreateInventoryBinding("radar", RadarPickupPath),
            CreateInventoryBinding("cursed_root_shard", CursedShardPickupPath),
            CreateInventoryBinding("car_key", SafetyTruckKeyPrefabPath),
            CreateInventoryBinding(
                "exposed_heartroot",
                ExposedHeartrootPickupPath)
        };
    }

    private static CampaignInventoryItemBinding[] CreateSafetyInventoryCatalog()
    {
        ValidateExactAssetGuid(
            SafetyLeatherPickupPath,
            SafetyLeatherPickupGuid,
            "Safety Item_Leather supplemental inventory pickup");
        return new[]
        {
            CreateInventoryBinding("leather", SafetyLeatherPickupPath),
            CreateInventoryBinding("box", SafetyBoxPickupPath),
            CreateInventoryBinding("stick", SafetyStickPickupPath),
            CreateInventoryBinding("iron_ore", SafetyIronOrePickupPath),
            CreateInventoryBinding("scrap_metal", SafetyScrapMetalPickupPath),
            CreateInventoryBinding("stone", SafetyStonePickupPath),
            CreateInventoryBinding("cursed_item", SafetyCursedPresentationPrefabPath)
        };
    }

    private static void ValidateExactInventoryCatalog(
        CampaignInventoryCarryover carryover)
    {
        RequireExactSafetyTruckKeyPrefab();
        CampaignInventoryItemBinding[] expected = CreateInventoryCatalog();
        CampaignInventoryItemBinding[] expectedSupplemental =
            CreateSafetyInventoryCatalog();
        SerializedObject carryoverData = new(carryover);
        SerializedProperty catalog = carryoverData.FindProperty("itemCatalog");
        SerializedProperty supplemental =
            carryoverData.FindProperty("safetyInventoryCatalog");
        SerializedProperty carrier =
            carryoverData.FindProperty("safetyInventoryCarrierPrefab");
        GameObject expectedCarrier = RequireExactSafetyTruckKeyPrefab();
        if (catalog == null || catalog.arraySize != expected.Length)
        {
            throw new InvalidOperationException(
                "Campaign inventory carryover must retain the exact six-entry " +
                "campaign catalog including the online-safety Car Key and the " +
                "owned Exposed Heartroot token.");
        }

        if (supplemental == null ||
            !InventoryCatalogEntriesMatch(
                supplemental,
                expectedSupplemental,
                expectedSupplemental.Length) ||
            carrier == null ||
            carrier.objectReferenceValue != expectedCarrier ||
            !EditorUtility.IsPersistent(carrier.objectReferenceValue) ||
            expectedCarrier.GetComponent<Item>() == null)
        {
            throw new InvalidOperationException(
                "Campaign inventory carryover must serialize the exact " +
                "complete ordinary Safety supplemental catalog and the " +
                "persistent Safety Item_Key prefab asset as its inactive " +
                "runtime restoration carrier, never a scene instance.");
        }

        for (int index = 0; index < expected.Length; index++)
        {
            SerializedProperty entry =
                catalog.GetArrayElementAtIndex(index);
            SerializedProperty itemId =
                entry.FindPropertyRelative("itemId");
            SerializedProperty pickup =
                entry.FindPropertyRelative("pickupPrefab");
            if (itemId == null || pickup == null ||
                itemId.stringValue != expected[index].ItemId ||
                pickup.objectReferenceValue != expected[index].PickupPrefab)
            {
                throw new InvalidOperationException(
                    $"Campaign inventory carryover catalog entry {index} " +
                    "does not match the exact authored ID/pickup sequence.");
            }
        }
    }

    private static CampaignInventoryItemBinding CreateInventoryBinding(
        string id,
        string assetPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        ItemStats item = CampaignInventoryTokenUtility.GetItemStats(prefab);
        if (prefab == null || item == null ||
            string.IsNullOrWhiteSpace(item.itemID))
        {
            throw new InvalidOperationException(
                $"Campaign inventory pickup '{assetPath}' is missing, " +
                "invalid, or has no persistent Safety itemID.");
        }

        CampaignInventoryItemBinding binding = new();
        binding.Configure(id, prefab);
        return binding;
    }

    private static void ValidateShippedInventoryItemReachability(Scene scene)
    {
        ValidateExactAssetGuid(
            SafetyCursedPresentationPrefabPath,
            SafetyCursedPresentationPrefabGuid,
            "Safety Cursed Item presentation");
        ValidateExactAssetGuid(
            SafetyCursedPreviewPresentationPrefabPath,
            SafetyCursedPreviewPresentationPrefabGuid,
            "Safety Cursed Item preview presentation");
        ValidateExactAssetGuid(
            ItemPickupMasterPath,
            ItemPickupMasterGuid,
            "Safety Item_Pickup_Master");

        CampaignInventoryItemBinding[] catalog = CreateInventoryCatalog();
        CampaignInventoryItemBinding[] supplementalCatalog =
            CreateSafetyInventoryCatalog();
        Dictionary<string, CampaignInventoryItemBinding> supportedByPath =
            catalog.Concat(supplementalCatalog).ToDictionary(
                binding => AssetDatabase.GetAssetPath(binding.PickupPrefab),
                binding => binding,
                StringComparer.Ordinal);
        CampaignInventoryItemBinding leatherBinding = supplementalCatalog
            .Single(binding => binding.ItemId == "leather");

        foreach (Item item in FindSceneComponents<Item>(scene))
        {
            GameObject instanceRoot =
                PrefabUtility.GetNearestPrefabInstanceRoot(item.gameObject);
            string prefabPath = instanceRoot != null
                ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                    instanceRoot)
                : string.Empty;
            bool hasEnabledCollider = item.GetComponents<Collider>()
                .Any(collider => collider.enabled);
            bool interactive = item.enabled &&
                               item.gameObject.activeInHierarchy &&
                               hasEnabledCollider;

            if (IsExactInactiveSafetyCursedPresentation(
                    item,
                    instanceRoot,
                    prefabPath))
            {
                continue;
            }

            CampaignInventoryItemBinding binding;
            bool supported;
            bool safetySerializedIdAlias = false;
            if (prefabPath == ItemPickupMasterPath)
            {
                binding = leatherBinding;
                supported = IsExactSafetyMasterPickup(
                    item,
                    instanceRoot,
                    binding.ItemData);
            }
            else
            {
                supported = supportedByPath.TryGetValue(
                    prefabPath,
                    out binding);
                if (!supported)
                {
                    supported = TryFindUniqueInventoryBindingBySerializedItemId(
                        item.item,
                        catalog.Concat(supplementalCatalog),
                        out binding);
                    safetySerializedIdAlias = supported;
                }
            }

            if (!supported ||
                item.item == null ||
                binding.ItemData == null ||
                (!safetySerializedIdAlias &&
                 !HaveSameStableItemFingerprint(
                     item.item,
                     binding.ItemData)))
            {
                throw new InvalidOperationException(
                    $"Shipped scene '{scene.name}' contains unsupported " +
                    $"Safety Item '{item.name}' from '{prefabPath}'. Every " +
                    "Item must map to one exact campaign catalog pickup; only " +
                    "exact unique Safety serialized item-ID aliases and two exact " +
                    "inactive presentation copies are exempt. " +
                    "Observed " +
                    $"root='{instanceRoot?.name ?? "<none>"}', " +
                    $"parent='{instanceRoot?.transform.parent?.name ?? "<none>"}', " +
                    $"activeSelf={instanceRoot?.activeSelf}, " +
                    $"activeInHierarchy={instanceRoot?.activeInHierarchy}, " +
                    $"itemEnabled={item.enabled}, tag='{item.tag}', " +
                    $"layer={item.gameObject.layer}, colliders=" +
                    $"{item.GetComponentsInChildren<Collider>(true).Length}.");
            }

            if (interactive &&
                (!item.gameObject.CompareTag(InteractTag) ||
                 item.gameObject.layer != RequireInteractableLayer()))
            {
                throw new InvalidOperationException(
                    $"Supported shipped Item '{item.name}' is active but not " +
                    "on the exact Interact tag/layer contract.");
            }
        }
    }

    private static bool TryFindUniqueInventoryBindingBySerializedItemId(
        ItemStats item,
        IEnumerable<CampaignInventoryItemBinding> bindings,
        out CampaignInventoryItemBinding binding)
    {
        binding = null;
        string serializedItemId = item?.itemID?.Trim() ?? string.Empty;
        if (serializedItemId.Length == 0 || bindings == null)
            return false;

        int matches = 0;
        foreach (CampaignInventoryItemBinding candidate in bindings)
        {
            if (candidate?.ItemData == null ||
                !string.Equals(
                    candidate.ItemData.itemID?.Trim(),
                    serializedItemId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            binding = candidate;
            matches++;
        }

        return matches == 1;
    }

    private static bool IsExactActiveSafetyNameStonePickup(
        Item item,
        GameObject instanceRoot,
        ItemStats campaignNameStoneDefinition)
    {
        if (item == null || instanceRoot == null ||
            campaignNameStoneDefinition == null ||
            PrefabUtility.GetPrefabInstanceStatus(instanceRoot) !=
                PrefabInstanceStatus.Connected ||
            PrefabUtility.GetOutermostPrefabInstanceRoot(instanceRoot) !=
                instanceRoot ||
            PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instanceRoot) !=
                SafetyCursedPresentationPrefabPath ||
            !instanceRoot.activeSelf || !instanceRoot.activeInHierarchy ||
            item.gameObject != instanceRoot || !item.enabled ||
            !item.canInteract || !instanceRoot.CompareTag(InteractTag) ||
            instanceRoot.layer != RequireInteractableLayer() ||
            item.item == null || item.item.quantity <= 0 ||
            item.item.quantity > item.item.stackSize ||
            !string.Equals(
                item.item.itemID?.Trim(),
                campaignNameStoneDefinition.itemID?.Trim(),
                StringComparison.Ordinal))
        {
            return false;
        }

        GameObject sourceRoot = AssetDatabase.LoadAssetAtPath<GameObject>(
            SafetyCursedPresentationPrefabPath);
        Item sourceItem = sourceRoot != null
            ? sourceRoot.GetComponent<Item>()
            : null;
        if (sourceRoot == null || sourceItem == null ||
            PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot) !=
                sourceRoot ||
            PrefabUtility.GetCorrespondingObjectFromSource(item) != sourceItem ||
            !HaveSameStableItemFingerprint(item.item, sourceItem.item))
        {
            return false;
        }

        Collider[] colliders = instanceRoot.GetComponents<Collider>();
        return colliders.Length == 1 &&
               colliders[0] is SphereCollider sphere &&
               sphere.enabled && !sphere.isTrigger;
    }

    private static bool IsExactSafetyMasterPickup(
        Item item,
        GameObject instanceRoot,
        ItemStats masterDefinition)
    {
        if (item == null || instanceRoot == null || masterDefinition == null ||
            PrefabUtility.GetPrefabInstanceStatus(instanceRoot) !=
            PrefabInstanceStatus.Connected ||
            PrefabUtility.GetOutermostPrefabInstanceRoot(instanceRoot) !=
            instanceRoot ||
            PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instanceRoot) !=
            ItemPickupMasterPath ||
            !instanceRoot.activeSelf || !instanceRoot.activeInHierarchy ||
            !item.enabled || !item.canInteract ||
            item.gameObject != instanceRoot ||
            !instanceRoot.CompareTag(InteractTag) ||
            instanceRoot.layer != RequireInteractableLayer() ||
            item.item == null || item.item.quantity <= 0 ||
            item.item.quantity > item.item.stackSize ||
            !HaveSameStableItemFingerprint(item.item, masterDefinition))
        {
            return false;
        }

        RequireItemPickupMasterSource(
            out GameObject sourceRoot,
            out Item sourceItem);
        if (PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot) !=
                sourceRoot ||
            PrefabUtility.GetCorrespondingObjectFromSource(item) != sourceItem)
        {
            return false;
        }

        Collider[] colliders = instanceRoot.GetComponents<Collider>();
        return colliders.Length == 1 &&
               colliders[0] is SphereCollider sphere &&
               sphere.enabled &&
               !sphere.isTrigger;
    }

    private static bool HaveSameStableItemFingerprint(
        ItemStats first,
        ItemStats second)
    {
        return first != null &&
               second != null &&
               string.Equals(
                   first.itemName?.Trim(),
                   second.itemName?.Trim(),
                   StringComparison.Ordinal) &&
               (string.IsNullOrWhiteSpace(first.itemID) ||
                string.IsNullOrWhiteSpace(second.itemID) ||
                string.Equals(
                    first.itemID,
                    second.itemID,
                    StringComparison.Ordinal)) &&
               first.stackSize == second.stackSize &&
               Mathf.Abs(first.weight - second.weight) <= 0.001f &&
               first.icon != null &&
               first.icon == second.icon &&
               first.itemMesh != null &&
               first.itemMesh == second.itemMesh &&
               first.itemIncreases != null &&
               first.itemIncreases == second.itemIncreases;
    }

    internal static bool IsExactSafetyMasterPickupForTests(Item item)
    {
        if (item == null)
            return false;

        GameObject instanceRoot =
            PrefabUtility.GetNearestPrefabInstanceRoot(item.gameObject);
        return IsExactSafetyMasterPickup(
            item,
            instanceRoot,
            CreateSafetyInventoryCatalog()
                .Single(binding =>
                    binding.ItemId == "leather")
                .ItemData);
    }

    private static void ValidateExactAssetGuid(
        string assetPath,
        string expectedGuid,
        string label)
    {
        if (!string.Equals(
                AssetDatabase.AssetPathToGUID(assetPath),
                expectedGuid,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Exact {label} GUID changed or the asset is missing.");
        }
    }

    private static bool IsExactInactiveSafetyCursedPresentation(
        Item item,
        GameObject instanceRoot,
        string prefabPath)
    {
        if (item == null ||
            instanceRoot == null ||
            PrefabUtility.GetPrefabInstanceStatus(instanceRoot) !=
            PrefabInstanceStatus.Connected ||
            (prefabPath != SafetyCursedPresentationPrefabPath &&
             prefabPath != SafetyCursedPreviewPresentationPrefabPath))
        {
            return false;
        }

        Item[] presentationItems =
            instanceRoot.GetComponentsInChildren<Item>(true);
        if (presentationItems.Length != 1 || presentationItems[0] != item)
            return false;

        if (prefabPath == SafetyCursedPresentationPrefabPath)
        {
            if (item.enabled ||
                instanceRoot.GetComponentsInChildren<MonoBehaviour>(true)
                    .Any(behaviour => behaviour != null &&
                                      behaviour.enabled &&
                                      behaviour is IInteract))
            {
                return false;
            }

            if (instanceRoot.GetComponentsInChildren<Collider>(true)
                .Any(collider => collider.enabled))
            {
                return false;
            }

            return IsExactOwnedFarmCursedPresentation(item, instanceRoot);
        }

        return IsExactSafetyUiCursedPreview(item, instanceRoot);
    }

    private static void NormalizeExactOwnedFarmCursedPresentations(
        Scene scene)
    {
        ValidateExactAssetGuid(
            SafetyCursedPresentationPrefabPath,
            SafetyCursedPresentationPrefabGuid,
            "Safety Cursed Item presentation");

        var ownedItems = new List<Item>();
        var ownedRoots = new List<GameObject>();
        foreach (Item item in FindSceneComponents<Item>(scene))
        {
            GameObject instanceRoot =
                PrefabUtility.GetNearestPrefabInstanceRoot(item.gameObject);
            if (instanceRoot == null ||
                PrefabUtility.GetPrefabInstanceStatus(instanceRoot) !=
                PrefabInstanceStatus.Connected ||
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                    instanceRoot) != SafetyCursedPresentationPrefabPath ||
                !IsExactOwnedFarmCursedPresentation(item, instanceRoot))
            {
                continue;
            }

            ownedItems.Add(item);
            ownedRoots.Add(instanceRoot);
        }

        int offeredVisualCount = ownedRoots.Count(root =>
            root.name == ImportedCursedPresentationName);
        int pickupVisualCount = ownedRoots.Count(root =>
            root.name == PrologueCursedPresentationName);
        if (ownedItems.Count != 2 ||
            ownedRoots.Distinct().Count() != 2 ||
            offeredVisualCount != 1 ||
            pickupVisualCount != 1)
        {
            throw new InvalidOperationException(
                "Farm must contain exactly the two recognized connected " +
                "Safety Cursed Item presentation instances before their " +
                "scene interaction overrides can be normalized.");
        }

        bool changed = false;
        for (int index = 0; index < ownedRoots.Count; index++)
        {
            GameObject instanceRoot = ownedRoots[index];
            Item item = ownedItems[index];
            foreach (MonoBehaviour behaviour in instanceRoot
                         .GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null ||
                    !(behaviour is IInteract) ||
                    !behaviour.enabled)
                {
                    continue;
                }

                if (PrefabUtility.GetCorrespondingObjectFromSource(
                        behaviour) == null)
                {
                    throw new InvalidOperationException(
                        $"Owned Farm Cursed Item presentation " +
                        $"'{instanceRoot.name}' contains an unexpected " +
                        "scene-added enabled IInteract behaviour.");
                }

                behaviour.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(
                    behaviour);
                EditorUtility.SetDirty(behaviour);
                changed = true;
            }

            foreach (Collider collider in instanceRoot
                         .GetComponentsInChildren<Collider>(true))
            {
                if (collider == null || !collider.enabled)
                    continue;

                if (PrefabUtility.GetCorrespondingObjectFromSource(collider) ==
                    null)
                {
                    throw new InvalidOperationException(
                        $"Owned Farm Cursed Item presentation " +
                        $"'{instanceRoot.name}' contains an unexpected " +
                        "scene-added enabled collider.");
                }

                collider.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(
                    collider);
                EditorUtility.SetDirty(collider);
                changed = true;
            }

            if (item.enabled ||
                instanceRoot.GetComponentsInChildren<MonoBehaviour>(true)
                    .Any(behaviour => behaviour != null &&
                                      behaviour.enabled &&
                                      behaviour is IInteract) ||
                instanceRoot.GetComponentsInChildren<Collider>(true)
                    .Any(collider => collider.enabled))
            {
                throw new InvalidOperationException(
                    $"Owned Farm Cursed Item presentation " +
                    $"'{instanceRoot.name}' did not retain its exact " +
                    "non-interactive scene override contract.");
            }
        }

        if (changed)
            EditorSceneManager.MarkSceneDirty(scene);
    }

    private static bool IsExactOwnedFarmCursedPresentation(
        Item item,
        GameObject instanceRoot)
    {
        if (item.gameObject != instanceRoot ||
            instanceRoot.activeSelf ||
            PrefabUtility.GetOutermostPrefabInstanceRoot(instanceRoot) !=
            instanceRoot)
        {
            return false;
        }

        Transform parent = instanceRoot.transform.parent;
        bool offeredVisual =
            instanceRoot.name == ImportedCursedPresentationName &&
            parent != null &&
            parent.name == OfferedVisualSlotName &&
            parent.parent != null &&
            parent.parent.name == OfferedVisualsRootName &&
            parent.parent.parent != null &&
            parent.parent.parent.name == RootTreeOfferingName;

        FarmPrologueCursedObjectPickup pickup = parent != null
            ? parent.GetComponent<FarmPrologueCursedObjectPickup>()
            : null;
        bool pickupVisual =
            instanceRoot.name == PrologueCursedPresentationName &&
            parent != null &&
            parent.name == PrologueCursedPickupName &&
            pickup != null &&
            pickup.PresentationRoot == instanceRoot;

        return offeredVisual || pickupVisual;
    }

    private static bool IsExactSafetyUiCursedPreview(
        Item item,
        GameObject instanceRoot)
    {
        GameObject outerRoot =
            PrefabUtility.GetOutermostPrefabInstanceRoot(instanceRoot);
        int itemPreviewLayer =
            LayerMask.NameToLayer(SafetyItemPreviewLayerName);
        return item.gameObject == instanceRoot &&
               item.enabled &&
               item.canInteract &&
               instanceRoot.activeSelf &&
               instanceRoot.name == SafetyCursedPreviewInstanceName &&
               instanceRoot.CompareTag("Untagged") &&
               itemPreviewLayer == SafetyItemPreviewLayer &&
               instanceRoot.layer == itemPreviewLayer &&
               instanceRoot.transform.parent != null &&
               instanceRoot.transform.parent.name ==
               SafetyTreeInteractionUiName &&
               outerRoot != null &&
               outerRoot != instanceRoot &&
               PrefabUtility.GetPrefabInstanceStatus(outerRoot) ==
               PrefabInstanceStatus.Connected &&
               PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                   outerRoot) == SafetyUiPrefabPath &&
               string.Equals(
                   AssetDatabase.AssetPathToGUID(SafetyUiPrefabPath),
                   SafetyUiPrefabGuid,
                   StringComparison.OrdinalIgnoreCase) &&
               HasExactInheritedSafetyPreviewCollider(instanceRoot);
    }

    private static bool HasExactInheritedSafetyPreviewCollider(
        GameObject instanceRoot)
    {
        Collider[] colliders =
            instanceRoot.GetComponentsInChildren<Collider>(true);
        if (colliders.Length != 1 ||
            colliders[0] is not SphereCollider sphere ||
            sphere.gameObject != instanceRoot ||
            !sphere.enabled ||
            sphere.isTrigger ||
            !Mathf.Approximately(sphere.radius, 3f) ||
            !Approximately(sphere.center, new Vector3(0.5f, 4.5f, 0f)))
        {
            return false;
        }

        UnityEngine.Object originalSource =
            PrefabUtility.GetCorrespondingObjectFromOriginalSource(sphere);
        if (originalSource is not SphereCollider ||
            !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                originalSource,
                out string sourceGuid,
                out long sourceFileId) ||
            !string.Equals(
                sourceGuid,
                ItemPickupMasterGuid,
                StringComparison.OrdinalIgnoreCase) ||
            sourceFileId != ItemPickupMasterColliderFileId)
        {
            return false;
        }

        PropertyModification[] modifications =
            PrefabUtility.GetPropertyModifications(instanceRoot) ??
            Array.Empty<PropertyModification>();
        return !modifications.Any(modification =>
            modification?.target is Collider);
    }

    internal static bool IsExactInactiveSafetyCursedPresentationForTests(
        Item item)
    {
        if (item == null)
            return false;

        GameObject instanceRoot =
            PrefabUtility.GetNearestPrefabInstanceRoot(item.gameObject);
        string prefabPath = instanceRoot != null
            ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                instanceRoot)
            : string.Empty;
        return IsExactInactiveSafetyCursedPresentation(
            item,
            instanceRoot,
            prefabPath);
    }

    private static void ValidateTravel(
        GameObject travelObject,
        string sceneName,
        string destinationId)
    {
        ValidateInteractionObject(travelObject);
        CampaignSceneTravel travel =
            travelObject.GetComponent<CampaignSceneTravel>();
        CampaignTravelInteractable interactable =
            travelObject.GetComponent<CampaignTravelInteractable>();

        if (travel == null || interactable == null ||
            interactable.SceneTravel != travel ||
            travel.DestinationSceneName != sceneName ||
            travel.SpawnDestinationId != destinationId)
        {
            throw new InvalidOperationException(
                $"Travel interaction '{travelObject.name}' is incomplete.");
        }
    }

    private static void ValidateInteractionObject(GameObject target)
    {
        Collider collider = target.GetComponent<Collider>();

        if (target.tag != InteractTag ||
            target.layer != RequireInteractableLayer() ||
            collider == null || collider.isTrigger)
        {
            throw new InvalidOperationException(
                $"Interaction object '{target.name}' must be tagged Interact, " +
                "on Interactable, with a solid collider on the same object.");
        }
    }

    private static void PreflightRecognizedOrEmpty(Scene scene)
    {
        gameManager manager = RequireSingleComponent<gameManager>(scene);
        ValidateSafetyExtractionGate(
            manager,
            allowLegacyZero: true);
        ValidateSafetyLoseLifecycleBridge(
            scene,
            manager,
            allowMissingBridge: true);
        ValidateSafetyPlayerMainBodyMigrationState(scene);
        ValidateRetiredSafetyPlayerIsolationMigrationState(scene);

        GameObject[] signatures = scene.GetRootGameObjects()
            .Where(root => root.name.StartsWith(
                SignaturePrefix,
                StringComparison.Ordinal))
            .ToArray();

        bool hasCampaignComponents =
            FindSceneComponents<CampaignStateService>(scene).Length > 0 ||
            FindSceneComponents<CampaignInventoryCarryover>(scene).Length > 0 ||
            FindSceneComponents<CampaignSafetyLoseLifecycleBridge>(scene)
                .Length > 0 ||
            FindSceneComponents<CampaignLoadoutEquipmentBridge>(scene)
                .Length > 0 ||
            FindSceneComponents<FarmPrologueDirector>(scene).Length > 0 ||
            FindSceneComponents<CampaignOpenWorldProgression>(scene).Length > 0 ||
            FindSceneComponents<CampaignSceneTravel>(scene).Length > 0 ||
            FindSceneComponents<CampaignAreaCompletionRelay>(scene).Length > 0 ||
            FindSceneComponents<CampaignLockedAreaFeedbackTrigger>(scene)
                .Length > 0 ||
            FindSceneComponents<CampaignLockedAreaFeedbackPresenter>(scene)
                .Length > 0;

        if (signatures.Length == 0 && !hasCampaignComponents)
            return;

        if (signatures.Length != 1)
        {
            throw new InvalidOperationException(
                $"Scene '{scene.name}' contains partial or duplicate campaign " +
                "wiring. Restore its immutable pre-foundation backup or repair " +
                "the scene manually before rebuilding.");
        }

        if (signatures[0].name == SignatureName)
        {
            ValidateSignedCurrentFoundation(scene);
            return;
        }

        if (signatures[0].name == PreviousSignatureName)
        {
            ValidatePreviousV4Foundation(scene);
            return;
        }

        if (signatures[0].name == PreviousV3SignatureName)
        {
            ValidatePreviousV3Foundation(scene);
            return;
        }

        if (signatures[0].name == PreviousV2SignatureName)
        {
            ValidatePreviousV2Foundation(scene);
            return;
        }

        if (signatures[0].name == LegacySignatureName)
        {
            ValidateLegacyV1Foundation(scene);
            return;
        }

        throw new InvalidOperationException(
            $"Scene '{scene.name}' contains an unrecognized campaign signature. " +
            "Restore the pre-foundation backup or repair it manually before rebuilding.");
    }

    private static void ValidateExactNewFarmVisualMigrationShell(Scene scene)
    {
        if (scene.path != FarmScenePath)
        {
            throw new InvalidOperationException(
                "The new Farm visual migration is valid only for the " +
                "campaign Farm scene.");
        }

        GameObject[] signatures = scene.GetRootGameObjects()
            .Where(root => root.name == SignatureName)
            .ToArray();
        if (signatures.Length != 1)
        {
            throw new InvalidOperationException(
                "The exact new Farm visual migration must retain one current " +
                "campaign-foundation signature.");
        }

        RequirePath(scene, FarmCorePath);
        RequirePath(scene, FarmObjectivesPath);
        RequirePath(scene, FarmEnemiesPath);
        RequirePath(scene, FarmDialoguePath);
        RequirePath(scene, FarmPrologueSpawnPath);
        RequirePath(scene, FarmHubSpawnPath);
        RequireSingleComponent<FarmPrologueDirector>(scene);
        RequireSingleComponent<waveManager>(scene);
        RequireSingleComponent<MobSpawner>(scene);
        RequireSingleComponent<gameManager>(scene);
        RequireSingleComponent<Terrain>(scene);
        RequireSingleEnabledPlayerInteract(scene);
    }

    private static void ValidateExactSafetyOpenWorldMigrationShell(Scene scene)
    {
        if (scene.path != OpenWorldScenePath)
        {
            throw new InvalidOperationException(
                "The Safety Open World migration is valid only for the " +
                "campaign Open World scene.");
        }

        GameObject[] signatures = scene.GetRootGameObjects()
            .Where(root => root.name == SignatureName)
            .ToArray();
        if (signatures.Length != 1)
        {
            throw new InvalidOperationException(
                "The exact Safety Open World migration must retain one " +
                "current campaign-foundation signature.");
        }

        RequirePath(scene, OpenWorldCorePath);
        RequirePath(scene, OpenWorldArrivalPath);
        RequireSingleComponent<CampaignStateService>(scene);
        RequireSingleComponent<CampaignOpenWorldProgression>(scene);
        RequireSingleComponent<gameManager>(scene);
        RequireSingleEnabledPlayerInteract(scene);
    }

    private static void ValidateSignedCurrentFoundation(Scene scene)
    {
        if (scene.path == FarmScenePath)
        {
            ValidateFarm(
                scene,
                requireCurrentSignature: true,
                rootServiceSchema: RootServiceSchema.LegacyOrCurrent,
                allowLegacySafetyEnemyRoster: true,
                allowLegacyFarmItemOverride: true,
                allowMissingLoadoutEquipmentBridge: true,
                allowLegacyPlayerPrefab: true,
                allowSafetyTruckExtractionMigration: true,
                allowCurrentV5RuntimeFieldMigration: true,
                allowInactivePersistentChoreEnvironmentMigration: true,
                allowMissingSafetyLoseLifecycleBridge: true);
            return;
        }

        if (scene.path == OpenWorldScenePath)
        {
            ValidateOpenWorld(
                scene,
                requireCurrentSignature: true,
                requireCurrentRuntimeHooks: true,
                rootServiceSchema: RootServiceSchema.LegacyOrCurrent,
                allowMissingLoadoutEquipmentBridge: true,
                allowLegacyPlayerPrefab: true,
                allowSafetyExtractionMigration: true,
                allowCurrentV5RuntimeFieldMigration: true,
                allowMissingSafetyLoseLifecycleBridge: true);
            return;
        }

        throw new InvalidOperationException(
            $"Current campaign signature is not valid on scene '{scene.path}'.");
    }

    private static void ValidatePreviousV4Foundation(Scene scene)
    {
        if (scene.path == FarmScenePath)
        {
            ValidateFarm(
                scene,
                requireCurrentSignature: false,
                rootServiceSchema: RootServiceSchema.LegacyOrCurrent,
                requireGroundedHooks: true,
                requireCurrentChoreSchema: false,
                requireExactLegacyV4: true,
                allowLegacySafetyEnemyRoster: true,
                allowMissingLoadoutEquipmentBridge: true,
                allowLegacyPlayerPrefab: true,
                allowSafetyTruckExtractionMigration: true,
                allowMissingSafetyLoseLifecycleBridge: true);
            return;
        }

        if (scene.path == OpenWorldScenePath)
        {
            ValidateOpenWorld(
                scene,
                requireCurrentSignature: false,
                requireCurrentRuntimeHooks: true,
                rootServiceSchema: RootServiceSchema.LegacyOrCurrent,
                allowMissingLoadoutEquipmentBridge: true,
                allowLegacyPlayerPrefab: true,
                allowSafetyExtractionMigration: true,
                allowMissingSafetyLoseLifecycleBridge: true);
            return;
        }

        throw new InvalidOperationException(
            $"Previous V4 campaign signature is not valid on scene '{scene.path}'.");
    }

    private static void ValidatePreviousV3Foundation(Scene scene)
    {
        if (scene.path == FarmScenePath)
        {
            ValidateFarm(
                scene,
                requireCurrentSignature: false,
                rootServiceSchema: RootServiceSchema.LegacyOrCurrent,
                requireGroundedHooks: false,
                requireCurrentChoreSchema: false,
                allowLegacySafetyEnemyRoster: true,
                allowMissingLoadoutEquipmentBridge: true,
                allowLegacyPlayerPrefab: true,
                allowSafetyTruckExtractionMigration: true,
                allowMissingSafetyLoseLifecycleBridge: true);
            return;
        }

        if (scene.path == OpenWorldScenePath)
        {
            ValidateOpenWorld(
                scene,
                requireCurrentSignature: false,
                requireCurrentRuntimeHooks: true,
                rootServiceSchema: RootServiceSchema.LegacyOrCurrent,
                allowMissingLoadoutEquipmentBridge: true,
                allowLegacyPlayerPrefab: true,
                allowSafetyExtractionMigration: true,
                allowPreviousV3FeedbackUiMigration: true,
                allowMissingSafetyLoseLifecycleBridge: true);
            return;
        }

        throw new InvalidOperationException(
            $"Previous V3 campaign signature is not valid on scene '{scene.path}'.");
    }

    private static void ValidatePreviousV2Foundation(Scene scene)
    {
        if (scene.path == FarmScenePath)
        {
            ValidateFarm(
                scene,
                requireCurrentSignature: false,
                rootServiceSchema: RootServiceSchema.LegacyOnly,
                requireGroundedHooks: false,
                requireCurrentChoreSchema: false,
                allowLegacySafetyEnemyRoster: true,
                allowMissingLoadoutEquipmentBridge: true,
                allowLegacyPlayerPrefab: true,
                allowSafetyTruckExtractionMigration: true,
                allowMissingSafetyLoseLifecycleBridge: true);
            return;
        }

        if (scene.path == OpenWorldScenePath)
        {
            ValidateOpenWorld(
                scene,
                requireCurrentSignature: false,
                requireCurrentRuntimeHooks: false,
                rootServiceSchema: RootServiceSchema.LegacyOnly,
                allowMissingLoadoutEquipmentBridge: true,
                allowLegacyPlayerPrefab: true,
                allowSafetyExtractionMigration: true,
                allowMissingSafetyLoseLifecycleBridge: true);

            if (FindSceneComponents<CampaignAreaCompletionRelay>(scene)
                    .Length != 0 ||
                FindSceneComponents<CampaignLockedAreaFeedbackTrigger>(scene)
                    .Length != 0 ||
                FindSceneComponents<CampaignLockedAreaFeedbackPresenter>(scene)
                    .Length != 0)
            {
                throw new InvalidOperationException(
                    "Previous V2 Open World contains partial V3 runtime-hook " +
                    "wiring and cannot be migrated automatically.");
            }

            return;
        }

        throw new InvalidOperationException(
            $"Previous V2 campaign signature is not valid on scene '{scene.path}'.");
    }

    private static void ValidateLegacyV1Foundation(Scene scene)
    {
        if (scene.path == FarmScenePath)
        {
            ValidateLegacyFarmV1(scene);
            return;
        }

        if (scene.path == OpenWorldScenePath)
        {
            ValidateLegacyOpenWorldV1(scene);
            return;
        }

        throw new InvalidOperationException(
            $"Legacy campaign signature is not valid on scene '{scene.path}'.");
    }

    private static void ValidateLegacyFarmV1(Scene scene)
    {
        ValidateRootService(scene, RootServiceSchema.LegacyOnly);
        FarmPrologueDirector director =
            RequirePath(scene, FarmDirectorPath)
                .GetComponent<FarmPrologueDirector>();
        FarmChoreInteractable[] chores =
            FindSceneComponents<FarmChoreInteractable>(scene);
        GameObject rumbleRoot = RequirePath(
            scene,
            FarmDialoguePath + "/Ground Rumble Sequence");
        FarmEnemyEmergencePresenter[] emergencePresenters =
            FindSceneComponents<FarmEnemyEmergencePresenter>(scene);

        if (director == null ||
            FindSceneComponents<FarmPrologueDirector>(scene).Length != 1 ||
            chores.Length != ChoreGroupNames.Length ||
            chores.Select(chore => chore.ChoreId).Distinct().Count() !=
            ChoreGroupNames.Length ||
            emergencePresenters.Length != 1 ||
            emergencePresenters[0].gameObject != rumbleRoot)
        {
            throw new InvalidOperationException(
                "Legacy V1 Farm signature does not match the exact recognized " +
                "campaign-foundation topology.");
        }

        for (int index = 0; index < ChoreGroupNames.Length; index++)
        {
            GameObject chore = RequirePath(
                scene,
                FarmObjectivesPath + "/" + ChoreGroupNames[index]);
            ValidateInteractionObject(chore);

            if (chore.GetComponent<FarmChoreInteractable>() == null)
            {
                throw new InvalidOperationException(
                    $"Legacy V1 Farm chore '{ChoreGroupNames[index]}' is incomplete.");
            }
        }

        GameObject travel = RequirePath(scene, FarmTruckTravelPath);
        ValidateTravel(
            travel,
            CampaignSceneNames.OpenWorld,
            "BlackPinesArrival");

        RequireSafetyTruckExtractionBinding(scene);

        gameManager manager = RequireSingleComponent<gameManager>(scene);
        GameObject fader =
            FindDirectChild(manager.gameObject, "Campaign Screen Fader");

        if (fader == null ||
            fader.GetComponent<RectTransform>() == null ||
            fader.GetComponent<CanvasGroup>() == null ||
            fader.GetComponent<Image>() == null ||
            RequirePath(scene, FarmHubPath).activeSelf ||
            !RequirePath(scene, FarmProloguePath).activeSelf ||
            RequireSingleComponent<waveManager>(scene).gameObject.activeSelf ||
            RequireSingleComponent<MobSpawner>(scene).gameObject.activeSelf ||
            RequireSingleComponent<TruckEscapeKeyPickup>(scene)
                .gameObject.activeSelf)
        {
            throw new InvalidOperationException(
                "Legacy V1 Farm state, authored fader, or encounter ownership " +
                "does not match the recognized migration source.");
        }
    }

    private static void ValidateLegacyOpenWorldV1(Scene scene)
    {
        ValidateRootService(scene, RootServiceSchema.LegacyOnly);
        CampaignOpenWorldProgression progression =
            RequirePath(scene, OpenWorldProgressionPath)
                .GetComponent<CampaignOpenWorldProgression>();
        OpenWorldAreaBarrier[] barriers =
            FindSceneComponents<OpenWorldAreaBarrier>(scene);

        if (progression == null ||
            FindSceneComponents<CampaignOpenWorldProgression>(scene).Length != 1 ||
            barriers.Length != 3 ||
            barriers.Select(barrier => barrier.Area).Distinct().Count() != 3 ||
            barriers.Any(barrier => barrier.StartsUnlocked))
        {
            throw new InvalidOperationException(
                "Legacy V1 Open World signature does not match the exact " +
                "recognized progression topology.");
        }

        GameObject arrival = RequirePath(scene, OpenWorldArrivalPath);
        CampaignSpawnPoint spawn = arrival.GetComponent<CampaignSpawnPoint>();
        GameObject returnTruck = RequireSingleNamedObject(
            scene,
            "Return Truck (Travel Wiring Pending)");
        ValidateTravel(
            returnTruck,
            CampaignSceneNames.FarmPrologueHub,
            "FarmHub");

        if (spawn == null || spawn.DestinationId != "BlackPinesArrival")
        {
            throw new InvalidOperationException(
                "Legacy V1 Open World arrival handoff is incomplete.");
        }
    }

    private static void EnsureSignature(Scene scene)
    {
        GameObject[] signatures = scene.GetRootGameObjects()
            .Where(root => root.name.StartsWith(
                SignaturePrefix,
                StringComparison.Ordinal))
            .ToArray();

        if (signatures.Length > 1 ||
            (signatures.Length == 1 &&
             signatures[0].name != SignatureName &&
             signatures[0].name != PreviousSignatureName &&
             signatures[0].name != PreviousV3SignatureName &&
             signatures[0].name != PreviousV2SignatureName &&
             signatures[0].name != LegacySignatureName))
        {
            throw new InvalidOperationException(
                $"Scene '{scene.name}' contains an unrecognized campaign signature.");
        }

        if (signatures.Length == 0)
        {
            EnsureSceneRoot(scene, SignatureName);
            return;
        }

        if (signatures[0].name == PreviousSignatureName ||
            signatures[0].name == PreviousV3SignatureName ||
            signatures[0].name == PreviousV2SignatureName ||
            signatures[0].name == LegacySignatureName)
        {
            signatures[0].name = SignatureName;
            EditorUtility.SetDirty(signatures[0]);
        }
    }

    private static void ValidateSignature(Scene scene)
    {
        GameObject[] signatures = scene.GetRootGameObjects()
            .Where(root => root.name.StartsWith(
                SignaturePrefix,
                StringComparison.Ordinal))
            .ToArray();

        if (signatures.Length != 1 ||
            signatures[0].name != SignatureName)
        {
            throw new InvalidOperationException(
                $"Scene '{scene.name}' requires exactly one recognized " +
                $"current signature named {SignatureName}.");
        }
    }

    private static void EnsureBackupPair()
    {
        bool farmExists =
            AssetDatabase.LoadAssetAtPath<SceneAsset>(FarmBackupPath) != null;
        bool openWorldExists =
            AssetDatabase.LoadAssetAtPath<SceneAsset>(OpenWorldBackupPath) != null;

        if (farmExists != openWorldExists)
        {
            throw new InvalidOperationException(
                "Campaign immutable backups must exist as a pair. A partial " +
                "backup set was found; restore the missing original backup " +
                "before running Campaign Foundation.");
        }

        if (farmExists)
        {
            ValidateImmutableBackupPair();
            return;
        }

        EnsureBackupFolder(FarmBackupPath);
        EnsureBackupFolder(OpenWorldBackupPath);
        bool farmCreated = false;
        bool openWorldCreated = false;

        try
        {
            farmCreated =
                AssetDatabase.CopyAsset(FarmScenePath, FarmBackupPath);
            openWorldCreated =
                AssetDatabase.CopyAsset(OpenWorldScenePath, OpenWorldBackupPath);

            if (!farmCreated || !openWorldCreated)
            {
                throw new InvalidOperationException(
                    "Unity could not create both immutable campaign backups.");
            }

            AssetDatabase.ImportAsset(
                FarmBackupPath,
                ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(
                OpenWorldBackupPath,
                ImportAssetOptions.ForceSynchronousImport);
            ValidateImmutableBackupPair();
        }
        catch
        {
            if (farmCreated ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(FarmBackupPath) != null)
            {
                AssetDatabase.DeleteAsset(FarmBackupPath);
            }

            if (openWorldCreated ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(OpenWorldBackupPath) != null)
            {
                AssetDatabase.DeleteAsset(OpenWorldBackupPath);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            throw;
        }
    }

    private static void EnsureBackupFolder(string backupPath)
    {
        string folder =
            Path.GetDirectoryName(backupPath)?.Replace('\\', '/');

        if (string.IsNullOrWhiteSpace(folder))
        {
            throw new InvalidOperationException(
                $"Invalid backup path '{backupPath}'.");
        }

        EnsureAssetFolder(folder);
    }

    private static void ValidateImmutableBackupPair()
    {
        ValidateImmutableBackup(
            FarmBackupPath,
            FarmBackupSha256Lf,
            FarmBackupSha256CrLf);
        ValidateImmutableBackup(
            OpenWorldBackupPath,
            OpenWorldBackupSha256Lf,
            OpenWorldBackupSha256CrLf);
    }

    private static void ValidateImmutableBackup(
        string backupPath,
        string expectedLfSha256,
        string expectedCrLfSha256)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(backupPath) == null)
        {
            throw new FileNotFoundException(
                "Required immutable campaign backup is missing: " +
                backupPath);
        }

        string absolutePath = ToAbsolutePath(backupPath);
        byte[] sceneBytes = File.ReadAllBytes(absolutePath);
        string actualSha256 = ComputeSha256(sceneBytes);

        // Git may materialize a text asset as LF or CRLF depending on the
        // checkout. Both constants are exact hashes of the same approved
        // immutable Unity scene; no other byte representation is accepted.
        if (!string.Equals(
                actualSha256,
                expectedLfSha256,
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                actualSha256,
                expectedCrLfSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Immutable backup '{backupPath}' failed its exact SHA-256 " +
                $"check. Expected an approved pre-foundation hash, found " +
                $"{actualSha256}.");
        }

        string sceneText = File.ReadAllText(absolutePath);

        if (sceneText.Contains(
                SignaturePrefix,
                StringComparison.Ordinal) ||
            sceneText.Contains(
                ServiceRootName,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Immutable backup '{backupPath}' already contains " +
                "campaign-foundation state and cannot be trusted as a " +
                "pre-foundation recovery scene.");
        }
    }

    private static string ComputeSha256(byte[] bytes)
    {
        using SHA256 sha256 = SHA256.Create();
        return BitConverter.ToString(sha256.ComputeHash(bytes))
            .Replace("-", string.Empty);
    }

    private static void EnsureAssetFolder(string folder)
    {
        string[] parts = folder.Split('/');
        string current = parts[0];

        for (int index = 1; index < parts.Length; index++)
        {
            string next = current + "/" + parts[index];

            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[index]);
            }

            current = next;
        }
    }

    private static void ValidateBackupsExcludedFromBuild()
    {
        HashSet<string> buildPaths = EditorBuildSettings.scenes
            .Select(scene => scene.path)
            .ToHashSet(StringComparer.Ordinal);

        if (buildPaths.Contains(FarmBackupPath) ||
            buildPaths.Contains(OpenWorldBackupPath))
        {
            throw new InvalidOperationException(
                "Campaign recovery scenes must not appear in Build Settings.");
        }
    }

    private static void EnsureReleaseBuildSettings()
    {
        EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes;
        var releaseScenes = new List<EditorBuildSettingsScene>
        {
            new(MainMenuScenePath, true),
            new(FarmScenePath, true),
            new(OpenWorldScenePath, true)
        };
        var addedPaths = new HashSet<string>(
            new[]
            {
                MainMenuScenePath,
                FarmScenePath,
                OpenWorldScenePath
            },
            StringComparer.OrdinalIgnoreCase);

        foreach (EditorBuildSettingsScene scene in existing)
        {
            if (string.IsNullOrWhiteSpace(scene.path) ||
                !addedPaths.Add(scene.path) ||
                string.Equals(
                    scene.path,
                    FarmBackupPath,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    scene.path,
                    OpenWorldBackupPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            bool enabled = scene.enabled && !IsOutdatedScene(scene.path);
            releaseScenes.Add(
                new EditorBuildSettingsScene(scene.path, enabled));
        }

        EditorBuildSettings.scenes = releaseScenes.ToArray();
    }

    private static void ValidateReleaseBuildSettings()
    {
        ValidateImmutableBackupPair();
        ValidateBackupsExcludedFromBuild();
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

        if (scenes.Length < 3 ||
            scenes[0].path != MainMenuScenePath ||
            !scenes[0].enabled ||
            scenes[1].path != FarmScenePath ||
            !scenes[1].enabled ||
            scenes[2].path != OpenWorldScenePath ||
            !scenes[2].enabled)
        {
            throw new InvalidOperationException(
                "Build Settings must begin with enabled MainMenu, " +
                "Farm_PrologueHub, and Bloodroot_OpenWorld scenes, in " +
                "that order.");
        }

        ValidateBuildSceneEnabled(MainMenuScenePath);
        ValidateBuildSceneEnabled(FarmScenePath);
        ValidateBuildSceneEnabled(OpenWorldScenePath);

        EditorBuildSettingsScene[] enabledOutdated = scenes
            .Where(scene => scene.enabled && IsOutdatedScene(scene.path))
            .ToArray();

        if (enabledOutdated.Length > 0)
        {
            throw new InvalidOperationException(
                "Release Build Settings must not enable OutDated Level. " +
                "The asset may remain in the project, but its build entry " +
                "must be disabled.");
        }
    }

    private static bool IsOutdatedScene(string path)
    {
        return string.Equals(
            Path.GetFileName(path),
            OutdatedSceneFileName,
            StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateBuildSceneEnabled(string path)
    {
        if (!EditorBuildSettings.scenes.Any(
                scene => scene.enabled && scene.path == path))
        {
            throw new InvalidOperationException(
                $"Required campaign scene '{path}' is not enabled in Build Settings.");
        }
    }

    private static void RequireNoDirtyLoadedScenes()
    {
        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            Scene scene = SceneManager.GetSceneAt(index);

            if (scene.IsValid() && scene.isDirty)
            {
                throw new InvalidOperationException(
                    $"Save or discard changes in loaded scene '{scene.name}' before " +
                    "running Campaign Foundation tools.");
            }
        }
    }

    private static Scene OpenTargetScene(string path)
    {
        Scene loaded = GetLoadedScene(path);

        return loaded.IsValid()
            ? loaded
            : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
    }

    private static Scene GetLoadedScene(string path)
    {
        Scene scene = SceneManager.GetSceneByPath(path);
        return scene.IsValid() && scene.isLoaded ? scene : default;
    }

    private static void RestoreSceneSetup(SceneSetup[] setup)
    {
        if (setup == null || setup.Length == 0)
            return;

        RequireNoDirtyLoadedScenes();
        EditorSceneManager.RestoreSceneManagerSetup(setup);
    }

    private static bool RestoreSceneBytes(
        byte[] farmBytes,
        byte[] openWorldBytes,
        SceneSetup[] setup,
        EditorBuildSettingsScene[] buildSettings,
        byte[] buildSettingsBytes,
        IReadOnlyList<AssetFileSnapshot> compatibilityAssetSnapshots)
    {
        try
        {
            // Unity refuses to close the final loaded scene. Keep a clean,
            // temporary empty scene alive while both campaign targets are
            // discarded so exact bytes can be restored safely.
            int loadedTargetCount = 0;
            foreach (string path in new[] { FarmScenePath, OpenWorldScenePath })
            {
                if (GetLoadedScene(path).IsValid())
                    loadedTargetCount++;
            }

            if (loadedTargetCount > 0 &&
                loadedTargetCount == SceneManager.sceneCount)
            {
                EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Additive);
            }

            foreach (string path in new[] { FarmScenePath, OpenWorldScenePath })
            {
                Scene loaded = GetLoadedScene(path);

                if (loaded.IsValid() &&
                    !EditorSceneManager.CloseScene(loaded, true))
                {
                    throw new InvalidOperationException(
                        $"Could not close modified scene '{path}' before rollback.");
                }
            }

            if (buildSettings != null)
            {
                EditorBuildSettings.scenes = buildSettings;
            }

            AssetDatabase.ReleaseCachedFileHandles();

            if (farmBytes != null)
            {
                File.WriteAllBytes(
                    ToAbsolutePath(FarmScenePath),
                    farmBytes);
            }

            if (openWorldBytes != null)
            {
                File.WriteAllBytes(
                    ToAbsolutePath(OpenWorldScenePath),
                    openWorldBytes);
            }

            RestoreCampaignCompatibilityAssetFiles(
                compatibilityAssetSnapshots);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            if (setup != null && setup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }

            AssertExactFileBytes(FarmScenePath, farmBytes);
            AssertExactFileBytes(OpenWorldScenePath, openWorldBytes);
            AssertExactFileBytes(
                EditorBuildSettingsPath,
                buildSettingsBytes);
            AssertCampaignCompatibilityAssetSnapshots(
                compatibilityAssetSnapshots);
            AssertBuildSettingsSnapshot(buildSettings);

            return true;
        }
        catch (Exception rollbackException)
        {
            Debug.LogError(
                "Campaign Foundation rollback failed: " +
                rollbackException);
            return false;
        }
    }

    private static byte[] ReadFileBytesIfPresent(string absolutePath)
    {
        return File.Exists(absolutePath)
            ? File.ReadAllBytes(absolutePath)
            : null;
    }

    private static void RestoreCampaignCompatibilityAssetFiles(
        IReadOnlyList<AssetFileSnapshot> snapshots)
    {
        if (snapshots == null)
            return;

        foreach (AssetFileSnapshot snapshot in snapshots)
        {
            RestoreExactFile(
                ToAbsolutePath(snapshot.AssetPath),
                snapshot.AssetBytes);
            RestoreExactFile(
                ToAbsolutePath(snapshot.AssetPath + ".meta"),
                snapshot.MetaBytes);
        }
    }

    private static void RestoreExactFile(
        string absolutePath,
        byte[] expectedBytes)
    {
        if (expectedBytes != null)
        {
            File.WriteAllBytes(absolutePath, expectedBytes);
            return;
        }

        if (File.Exists(absolutePath))
            File.Delete(absolutePath);
    }

    private static void AssertCampaignCompatibilityAssetSnapshots(
        IReadOnlyList<AssetFileSnapshot> snapshots)
    {
        if (snapshots == null)
            return;

        foreach (AssetFileSnapshot snapshot in snapshots)
        {
            AssertExactOptionalFileBytes(
                snapshot.AssetPath,
                snapshot.AssetBytes);
            AssertExactOptionalFileBytes(
                snapshot.AssetPath + ".meta",
                snapshot.MetaBytes);
        }
    }

    private static void AssertExactOptionalFileBytes(
        string relativePath,
        byte[] expectedBytes)
    {
        string absolutePath = ToAbsolutePath(relativePath);

        if (expectedBytes == null)
        {
            if (File.Exists(absolutePath))
            {
                throw new InvalidOperationException(
                    $"Rollback should have removed newly created file " +
                    $"'{relativePath}'.");
            }

            return;
        }

        AssertExactFileBytes(relativePath, expectedBytes);
    }

    private static void AssertExactFileBytes(
        string relativePath,
        byte[] expectedBytes)
    {
        if (expectedBytes == null)
            return;

        string absolutePath = ToAbsolutePath(relativePath);

        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException(
                $"Rollback byte verification could not find '{relativePath}'.");
        }

        byte[] actualBytes = File.ReadAllBytes(absolutePath);

        if (!actualBytes.SequenceEqual(expectedBytes))
        {
            throw new InvalidOperationException(
                $"Rollback byte verification failed for '{relativePath}'. " +
                $"Expected SHA-256 {ComputeSha256(expectedBytes)}, found " +
                $"{ComputeSha256(actualBytes)}.");
        }
    }

    private static void AssertBuildSettingsSnapshot(
        IReadOnlyList<EditorBuildSettingsScene> expectedScenes)
    {
        if (expectedScenes == null)
            return;

        EditorBuildSettingsScene[] actualScenes =
            EditorBuildSettings.scenes;

        if (actualScenes.Length != expectedScenes.Count)
        {
            throw new InvalidOperationException(
                "Rollback restored the EditorBuildSettings.asset bytes, " +
                "but Unity's in-memory scene list has a different length.");
        }

        for (int index = 0; index < actualScenes.Length; index++)
        {
            EditorBuildSettingsScene actual = actualScenes[index];
            EditorBuildSettingsScene expected = expectedScenes[index];

            if (actual.enabled != expected.enabled ||
                !string.Equals(
                    actual.path,
                    expected.path,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Rollback restored the EditorBuildSettings.asset bytes, " +
                    $"but Unity's in-memory scene entry {index} differs.");
            }
        }
    }

    private static GameObject EnsureSceneRoot(Scene scene, string name)
    {
        GameObject[] matches = scene.GetRootGameObjects()
            .Where(root => root.name == name)
            .ToArray();

        if (matches.Length > 1)
        {
            throw new InvalidOperationException(
                $"Scene '{scene.name}' has duplicate root '{name}'.");
        }

        if (matches.Length == 1)
            return matches[0];

        var created = new GameObject(name);
        SceneManager.MoveGameObjectToScene(created, scene);
        return created;
    }

    private static GameObject EnsureDirectChild(GameObject parent, string name)
    {
        GameObject existing = FindDirectChild(parent, name);

        if (existing != null)
            return existing;

        var child = new GameObject(name);
        child.transform.SetParent(parent.transform, false);
        return child;
    }

    private static GameObject FindDirectChild(GameObject parent, string name)
    {
        GameObject match = null;

        for (int index = 0; index < parent.transform.childCount; index++)
        {
            GameObject child = parent.transform.GetChild(index).gameObject;

            if (child.name != name)
                continue;

            if (match != null)
            {
                throw new InvalidOperationException(
                    $"'{parent.name}' has duplicate direct child '{name}'.");
            }

            match = child;
        }

        return match;
    }

    private static GameObject RequireDirectChild(
        GameObject parent,
        string name)
    {
        GameObject child = FindDirectChild(parent, name);

        if (child == null)
        {
            throw new InvalidOperationException(
                $"'{parent.name}' is missing required direct child '{name}'.");
        }

        return child;
    }

    private static GameObject RequirePath(Scene scene, string path)
    {
        string[] parts = path.Split('/');
        GameObject current = scene.GetRootGameObjects()
            .SingleOrDefault(root => root.name == parts[0]);

        if (current == null)
        {
            throw new InvalidOperationException(
                $"Scene '{scene.name}' is missing required path '{path}'.");
        }

        for (int index = 1; index < parts.Length; index++)
        {
            current = FindDirectChild(current, parts[index]);

            if (current == null)
            {
                throw new InvalidOperationException(
                    $"Scene '{scene.name}' is missing required path '{path}'.");
            }
        }

        return current;
    }

    private static T EnsureComponent<T>(GameObject target)
        where T : Component
    {
        T[] components = target.GetComponents<T>();

        if (components.Length > 1)
        {
            throw new InvalidOperationException(
                $"'{target.name}' has duplicate {typeof(T).Name} components.");
        }

        T component = components.Length == 1
            ? components[0]
            : target.AddComponent<T>();
        EditorUtility.SetDirty(component);
        return component;
    }

    private static T RequireSingleComponent<T>(Scene scene)
        where T : Component
    {
        T[] components = FindSceneComponents<T>(scene);

        if (components.Length != 1)
        {
            throw new InvalidOperationException(
                $"Scene '{scene.name}' requires exactly one {typeof(T).Name}; " +
                $"found {components.Length}.");
        }

        return components[0];
    }

    private static T RequireTaggedPlayerRootComponent<T>(Scene scene)
        where T : Component
    {
        GameObject player = RequireTaggedObject(scene, "Player");
        T[] components = player.GetComponents<T>();
        if (components.Length != 1 ||
            (components[0] is Behaviour behaviour && !behaviour.enabled))
        {
            throw new InvalidOperationException(
                $"Scene '{scene.name}' requires exactly one enabled " +
                $"{typeof(T).Name} on the tagged Player root; found " +
                $"{components.Length}.");
        }

        return components[0];
    }

    private static void ValidateFarmSafetyPlayerAndSpawnMigrationState(
        Scene scene)
    {
        RequireValidLoadedScene(scene, FarmScenePath);
        RequireExactSafetyPlayerPrefab();
        RequireExactSafetySpawnPrefab(
            SafetyBaseSpawnPrefabPath,
            SafetyBaseSpawnPrefabGuid);

        GameObject[] safetyPlayers = FindPrefabInstanceRoots(
            scene,
            SafetyPlayerPrefabPath);
        GameObject[] legacyPlayers = FindPrefabInstanceRoots(
            scene,
            LegacyPlayerForLevelPrefabPath);
        if (safetyPlayers.Length != 1 || legacyPlayers.Length != 0 ||
            PrefabUtility.GetPrefabInstanceStatus(safetyPlayers[0]) !=
                PrefabInstanceStatus.Connected ||
            safetyPlayers[0].name != "Player" ||
            !safetyPlayers[0].CompareTag("Player") ||
            safetyPlayers[0].transform.parent != null ||
            !safetyPlayers[0].activeSelf ||
            !IsRecognizedFarmSafetyPlayerPose(safetyPlayers[0].transform))
        {
            throw new InvalidOperationException(
                "Farm Safety migration requires exactly one connected " +
                "online-Safety Player at an exact recognized Safety scene pose.");
        }

        GameObject player = safetyPlayers[0];
        CharacterController[] controllers =
            player.GetComponents<CharacterController>();
        CapsuleCollider[] damageColliders =
            player.GetComponents<CapsuleCollider>();
        cameraController[] cameras =
            player.GetComponentsInChildren<cameraController>(true);
        if (controllers.Length != 1 || !controllers[0].enabled ||
            controllers[0].center != Vector3.zero ||
            Mathf.Abs(controllers[0].height - 2f) > 0.001f ||
            Mathf.Abs(controllers[0].radius - 0.5f) > 0.001f ||
            damageColliders.Length != 1 || !damageColliders[0].enabled ||
            damageColliders[0].isTrigger ||
            damageColliders[0].center != Vector3.zero ||
            Mathf.Abs(damageColliders[0].height - 2f) > 0.001f ||
            Mathf.Abs(damageColliders[0].radius - 0.5f) > 0.001f ||
            cameras.Length != 1 || !cameras[0].enabled)
        {
            throw new InvalidOperationException(
                "Farm Safety Player migration accepts only source-centered " +
                "enabled controller/collider geometry and one enabled camera.");
        }

        ValidateFarmSafetyPlayerCameraOverride(player, cameras[0]);
        ValidateSafetyPlayerMainBodyState(
            player,
            allowLegacyZeroPosition: true);
        ValidateRetiredSafetyPlayerIsolationState(
            player,
            allowRetiredOverrides: true,
            out _);

        Transform prologueSpawn =
            RequirePath(scene, FarmPrologueSpawnPath).transform;
        if (!Approximately(
                prologueSpawn.position,
                FarmPrologueSpawnWorldPosition) ||
            Quaternion.Angle(
                prologueSpawn.rotation,
                FarmPrologueSpawnWorldRotation) > 0.01f)
        {
            throw new InvalidOperationException(
                "Farm campaign Prologue Spawn no longer matches the exact " +
                "online-Safety farmhouse runtime pose.");
        }

        ValidateWakeAuthoring(scene);

        GameObject[] spawnInstances = FindPrefabInstanceRoots(
            scene,
            SafetyBaseSpawnPrefabPath);
        GameObject[] retained = spawnInstances
            .Where(candidate =>
                IsExactFarmSafetySpawnInstance(
                    candidate,
                    "PlayerSpawnPos",
                    FarmPrologueSpawnWorldPosition,
                    FarmPrologueSpawnWorldRotation))
            .ToArray();
        GameObject[] duplicates = spawnInstances
            .Where(candidate =>
                IsExactFarmSafetySpawnInstance(
                    candidate,
                    "PlayerSpawnPos (1)",
                    FarmDuplicateSpawnWorldPosition,
                    FarmDuplicateSpawnWorldRotation))
            .ToArray();
        bool exactFinal = spawnInstances.Length == 1 &&
            retained.Length == 1 && duplicates.Length == 0;
        bool exactSafetyMigration = spawnInstances.Length == 2 &&
            retained.Length == 1 && duplicates.Length == 1;
        if (!exactFinal && !exactSafetyMigration)
        {
            throw new InvalidOperationException(
                "Farm spawn migration accepts only the original exact " +
                "PlayerSpawnPos, optionally plus the exact online-Safety " +
                "PlayerSpawnPos (1) duplicate.");
        }

        gameManager manager = RequireSingleComponent<gameManager>(scene);
        playerController movement = player.GetComponent<playerController>();
        if (movement == null || !movement.enabled ||
            manager.playerSpawnPos != retained[0] ||
            (manager.player != null && manager.player != player) ||
            (manager.playerController != null &&
             manager.playerController != movement))
        {
            throw new InvalidOperationException(
                "Farm GameManager Safety migration must preserve the original " +
                "fallback and may only contain null or exact replacement-Player bindings.");
        }

        if (duplicates.Length == 1 &&
            HasExternalSceneReference(scene, duplicates[0]))
        {
            throw new InvalidOperationException(
                "The exact online-Safety PlayerSpawnPos (1) duplicate gained " +
                "an external scene reference and cannot be removed automatically.");
        }
    }

    private static void NormalizeFarmSafetySpawnAuthority(Scene scene)
    {
        GameObject[] spawnInstances = FindPrefabInstanceRoots(
            scene,
            SafetyBaseSpawnPrefabPath);
        if (spawnInstances.Length == 1)
            return;

        GameObject duplicate = spawnInstances.SingleOrDefault(candidate =>
            IsExactFarmSafetySpawnInstance(
                candidate,
                "PlayerSpawnPos (1)",
                FarmDuplicateSpawnWorldPosition,
                FarmDuplicateSpawnWorldRotation));
        if (spawnInstances.Length != 2 || duplicate == null ||
            HasExternalSceneReference(scene, duplicate))
        {
            throw new InvalidOperationException(
                "Farm Safety spawn normalization refused an unknown or " +
                "referenced duplicate layout.");
        }

        UnityEngine.Object.DestroyImmediate(duplicate);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void NormalizeExactUnusedFarmHubSpawnMarker(Scene scene)
    {
        GameObject[] hubMarkers = FindPrefabInstanceRoots(
            scene,
            SafetyHubSpawnPrefabPath);
        if (hubMarkers.Length == 0)
            return;

        if (hubMarkers.Length != 1)
        {
            throw new InvalidOperationException(
                "The exact new Farm visual migration contains duplicate " +
                "PlayerSpawnPosHub prefab instances.");
        }

        GameObject marker = hubMarkers[0];
        if (marker.name != "PlayerSpawnPosHub" ||
            marker.transform.parent != null ||
            !marker.activeSelf ||
            !marker.CompareTag("PlayerSpawnPos") ||
            !Approximately(
                marker.transform.position,
                FarmUnusedHubSpawnWorldPosition) ||
            Quaternion.Angle(
                marker.transform.rotation,
                Quaternion.identity) > 0.01f ||
            !Approximately(marker.transform.localScale, Vector3.one) ||
            HasExternalSceneReference(scene, marker))
        {
            throw new InvalidOperationException(
                "The new Farm visual migration's PlayerSpawnPosHub is not " +
                "the exact unreferenced top-level Safety marker.");
        }

        UnityEngine.Object.DestroyImmediate(marker);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void RefreshFarmSafetyPlayerBindingsForPreflight(
        Scene scene)
    {
        GameObject player = FindPrefabInstanceRoots(
            scene,
            SafetyPlayerPrefabPath).Single();
        GameObject fallback = FindPrefabInstanceRoots(
            scene,
            SafetyBaseSpawnPrefabPath).Single();
        Inventory inventory = player.GetComponent<Inventory>();
        playerController movement = player.GetComponent<playerController>();
        Interact interaction = player.GetComponent<Interact>();
        cameraController cameraLook = player
            .GetComponentsInChildren<cameraController>(true)
            .Single();
        if (inventory == null || !inventory.enabled || movement == null ||
            !movement.enabled || interaction == null || !interaction.enabled ||
            cameraLook == null || !cameraLook.enabled)
        {
            throw new InvalidOperationException(
                "Farm Safety Player binding refresh requires the exact enabled " +
                "Inventory, movement, interaction, and camera authorities.");
        }

        bool changed = false;
        gameManager manager = RequireSingleComponent<gameManager>(scene);
        changed |= SetNullOrExactSerializedReference(
            manager,
            "player",
            player,
            "Safety Player");
        changed |= SetNullOrExactSerializedReference(
            manager,
            "playerController",
            movement,
            "Safety playerController");
        changed |= SetNullOrExactSerializedReference(
            manager,
            "playerSpawnPos",
            fallback,
            "original Safety PlayerSpawnPos");

        FarmPrologueDirector director =
            RequireSingleComponent<FarmPrologueDirector>(scene);
        changed |= SetNullOrExactSerializedReference(
            director,
            "playerInventory",
            inventory,
            "Safety Player Inventory");
        changed |= SetNullOrExactSerializedReference(
            director,
            "playerTransform",
            player.transform,
            "Safety Player Transform");

        SerializedObject directorData = new(director);
        SerializedProperty inputs =
            directorData.FindProperty("gameplayInputBehaviours");
        Behaviour[] expectedInputs = { movement, interaction, cameraLook };
        if (inputs == null || !inputs.isArray || inputs.arraySize != 3)
        {
            throw new InvalidOperationException(
                "Farm Prologue Director lost its exact three-entry player-input array.");
        }

        bool inputsExact = true;
        bool inputsCleared = true;
        for (int index = 0; index < expectedInputs.Length; index++)
        {
            UnityEngine.Object current = inputs
                .GetArrayElementAtIndex(index).objectReferenceValue;
            inputsExact &= current == expectedInputs[index];
            inputsCleared &= current == null;
        }

        if (!inputsExact && !inputsCleared)
        {
            throw new InvalidOperationException(
                "Farm Prologue Director contains a partial or unknown player-" +
                "input migration; only all-null Safety replacement refs may be restored.");
        }

        if (inputsCleared)
        {
            for (int index = 0; index < expectedInputs.Length; index++)
            {
                inputs.GetArrayElementAtIndex(index).objectReferenceValue =
                    expectedInputs[index];
            }

            directorData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(director);
            changed = true;
        }

        FarmRumblePresenter rumble =
            RequireSingleComponent<FarmRumblePresenter>(scene);
        changed |= SetNullOrExactSerializedReference(
            rumble,
            "cameraTransform",
            cameraLook.transform,
            "Safety camera Transform");

        if (changed)
            EditorSceneManager.MarkSceneDirty(scene);
    }

    private static bool SetNullOrExactSerializedReference(
        UnityEngine.Object owner,
        string propertyName,
        UnityEngine.Object expected,
        string description)
    {
        SerializedObject serialized = new(owner);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null ||
            property.propertyType != SerializedPropertyType.ObjectReference)
        {
            throw new InvalidOperationException(
                $"'{owner.name}' no longer exposes its {description} reference.");
        }

        UnityEngine.Object current = property.objectReferenceValue;
        if (current == expected)
            return false;
        if (current != null)
        {
            throw new InvalidOperationException(
                $"'{owner.name}' contains an unknown {description} reference; " +
                "only the exact cleared Safety migration may be repaired.");
        }

        property.objectReferenceValue = expected;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(owner);
        return true;
    }

    private static bool IsExactFarmSafetySpawnInstance(
        GameObject candidate,
        string expectedName,
        Vector3 expectedPosition,
        Quaternion expectedRotation)
    {
        if (candidate == null || candidate.name != expectedName ||
            candidate.transform.parent != null ||
            !candidate.activeSelf ||
            !candidate.CompareTag("PlayerSpawnPos") ||
            PrefabUtility.GetPrefabInstanceStatus(candidate) !=
                PrefabInstanceStatus.Connected ||
            !Approximately(candidate.transform.position, expectedPosition) ||
            Quaternion.Angle(
                candidate.transform.rotation,
                expectedRotation) > 0.01f ||
            !Approximately(candidate.transform.localScale, Vector3.one))
        {
            return false;
        }

        CapsuleCollider[] colliders =
            candidate.GetComponents<CapsuleCollider>();
        return candidate.GetComponents<Component>().Length == 2 &&
               colliders.Length == 1 && !colliders[0].enabled;
    }

    private static bool HasExternalSceneReference(
        Scene scene,
        GameObject targetRoot)
    {
        var hierarchy = new HashSet<UnityEngine.Object>();
        foreach (Transform transform in
                 targetRoot.GetComponentsInChildren<Transform>(true))
        {
            hierarchy.Add(transform.gameObject);
            foreach (Component component in
                     transform.gameObject.GetComponents<Component>())
            {
                if (component != null)
                    hierarchy.Add(component);
            }
        }

        foreach (Component owner in scene.GetRootGameObjects()
                     .SelectMany(root =>
                         root.GetComponentsInChildren<Component>(true))
                     .Where(component =>
                         component != null &&
                         !hierarchy.Contains(component)))
        {
            SerializedObject serialized = new(owner);
            SerializedProperty property = serialized.GetIterator();
            bool enterChildren = true;
            while (property.Next(enterChildren))
            {
                enterChildren = false;
                if (property.propertyType ==
                        SerializedPropertyType.ObjectReference &&
                    hierarchy.Contains(property.objectReferenceValue))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsRecognizedFarmSafetyPlayerPose(Transform player)
    {
        if (player == null ||
            Quaternion.Angle(
                player.rotation,
                FarmSafetyPlayerWorldRotation) > 0.01f ||
            !Approximately(player.localScale, Vector3.one))
        {
            return false;
        }

        return Approximately(
                   player.position,
                   FarmCurrentSafetyPlayerWorldPosition) ||
               Approximately(
                   player.position,
                   FarmInitialSafetyPlayerWorldPosition);
    }

    private static void ValidateFarmSafetyPlayerCameraOverride(
        GameObject player,
        cameraController cameraLook)
    {
        GameObject sourcePlayer = RequireExactSafetyPlayerPrefab();
        cameraController sourceCameraLook = sourcePlayer
            .GetComponentsInChildren<cameraController>(true)
            .Single();
        Transform sourceCamera = sourceCameraLook.transform;
        Transform sceneCamera = cameraLook != null
            ? cameraLook.transform
            : null;
        PropertyModification[] positionOverrides =
            (PrefabUtility.GetPropertyModifications(player) ??
             Array.Empty<PropertyModification>())
            .Where(modification =>
                modification != null &&
                modification.target == sourceCamera &&
                modification.propertyPath.StartsWith(
                    "m_LocalPosition.",
                    StringComparison.Ordinal))
            .ToArray();
        if (!Exactly(
                sourceCamera.localPosition,
                FarmSafetyPlayerSourceCameraLocalPosition) ||
            sceneCamera == null ||
            !Exactly(
                sceneCamera.localPosition,
                FarmSafetyPlayerSceneCameraLocalPosition) ||
            positionOverrides.Length != 0)
        {
            throw new InvalidOperationException(
                "Farm must preserve the exact online-Safety source camera " +
                "pose without scene-local position overrides.");
        }
    }

    private static GameObject EnsureSafetyPlayerInstance(
        Scene scene,
        Transform authoredSpawn)
    {
        GameObject safetyPrefab = RequireExactSafetyPlayerPrefab();
        GameObject[] safetyInstances = FindPrefabInstanceRoots(
            scene,
            SafetyPlayerPrefabPath);
        GameObject[] legacyInstances = FindPrefabInstanceRoots(
            scene,
            LegacyPlayerForLevelPrefabPath);

        if (safetyInstances.Length == 1 && legacyInstances.Length == 0)
        {
            ApplySafetyPlayerSceneOverrides(
                safetyInstances[0],
                authoredSpawn);
            return safetyInstances[0];
        }

        if (scene.path == OpenWorldScenePath &&
            safetyInstances.Length == 1 &&
            legacyInstances.Length == 1)
        {
            GameObject retainedSafetyPlayer = safetyInstances[0];
            GameObject legacyPlayer = legacyInstances[0];
            ApplySafetyPlayerSceneOverrides(
                retainedSafetyPlayer,
                authoredSpawn);

            Dictionary<UnityEngine.Object, UnityEngine.Object> mixedSourceMap =
                BuildHierarchyReplacementMap(
                    legacyPlayer,
                    retainedSafetyPlayer);
            RewireExternalSceneReferences(
                scene,
                legacyPlayer.transform,
                retainedSafetyPlayer.transform,
                mixedSourceMap,
                "Player");

            UnityEngine.Object.DestroyImmediate(legacyPlayer);
            EditorSceneManager.MarkSceneDirty(scene);
            return retainedSafetyPlayer;
        }

        if (safetyInstances.Length != 0 || legacyInstances.Length != 1)
        {
            throw new InvalidOperationException(
                $"Scene '{scene.name}' can migrate only one connected " +
                $"'{LegacyPlayerForLevelPrefabPath}' Player, or the exact " +
                "recognized Open World mixed source containing one legacy " +
                "and one Safety Player. Found " +
                $"{legacyInstances.Length} legacy and " +
                $"{safetyInstances.Length} Safety instances.");
        }

        GameObject currentPlayer = legacyInstances[0];
        Transform oldTransform = currentPlayer.transform;
        Transform oldParent = oldTransform.parent;
        int oldSiblingIndex = oldTransform.GetSiblingIndex();
        GameObject replacement = PrefabUtility.InstantiatePrefab(
            safetyPrefab,
            scene) as GameObject;

        if (replacement == null)
        {
            throw new InvalidOperationException(
                $"Unity could not instantiate '{SafetyPlayerPrefabPath}' " +
                $"into scene '{scene.name}'.");
        }

        if (oldParent != null)
        {
            replacement.transform.SetParent(oldParent, false);
        }

        replacement.transform.SetSiblingIndex(oldSiblingIndex);
        ApplySafetyPlayerSceneOverrides(replacement, authoredSpawn);

        Dictionary<UnityEngine.Object, UnityEngine.Object> replacementMap =
            BuildHierarchyReplacementMap(currentPlayer, replacement);
        RewireExternalSceneReferences(
            scene,
            currentPlayer.transform,
            replacement.transform,
            replacementMap,
            "Player");

        UnityEngine.Object.DestroyImmediate(currentPlayer);
        EditorSceneManager.MarkSceneDirty(scene);
        return replacement;
    }

    private static GameObject[] FindOpenWorldPlayerPrefabRoots(Scene scene)
    {
        return FindPrefabInstanceRoots(scene, SafetyPlayerPrefabPath)
            .Concat(FindPrefabInstanceRoots(
                scene,
                LegacyPlayerForLevelPrefabPath))
            .Distinct()
            .ToArray();
    }

    private static void ValidateNormalizedOpenWorldPlayerBeforeWiring(
        Scene scene,
        GameObject normalizedSafetyPlayer)
    {
        GameObject[] safetyPlayers = FindPrefabInstanceRoots(
            scene,
            SafetyPlayerPrefabPath);
        GameObject[] legacyPlayers = FindPrefabInstanceRoots(
            scene,
            LegacyPlayerForLevelPrefabPath);
        GameObject taggedPlayer = RequireTaggedObject(scene, "Player");
        if (normalizedSafetyPlayer == null ||
            normalizedSafetyPlayer.scene != scene ||
            taggedPlayer != normalizedSafetyPlayer ||
            safetyPlayers.Length != 1 ||
            safetyPlayers[0] != normalizedSafetyPlayer ||
            legacyPlayers.Length != 0 ||
            normalizedSafetyPlayer.transform.parent != null ||
            PrefabUtility.GetPrefabInstanceStatus(normalizedSafetyPlayer) !=
                PrefabInstanceStatus.Connected)
        {
            throw new InvalidOperationException(
                "Open World mixed-player normalization must leave one " +
                "top-level connected Safety Player, zero Player ForLevel " +
                "instances, and exactly one Player tag before wiring.");
        }
    }

    private static void ValidateOpenWorldPlayerAndSpawnMigrationState(
        Scene scene)
    {
        RequireValidLoadedScene(scene, OpenWorldScenePath);
        RequireExactSafetyPlayerPrefab();
        RequireExactOpenWorldSpawnPrefabs();

        GameObject[] safetyPlayers = FindPrefabInstanceRoots(
            scene,
            SafetyPlayerPrefabPath);
        GameObject[] legacyPlayers = FindPrefabInstanceRoots(
            scene,
            LegacyPlayerForLevelPrefabPath);

        if (safetyPlayers.Length == 1 && legacyPlayers.Length == 0)
        {
            GameObject[] finalTaggedPlayers = FindSceneObjectsWithTag(
                scene,
                "Player");
            if (finalTaggedPlayers.Length != 1 ||
                finalTaggedPlayers[0] != safetyPlayers[0])
            {
                throw new InvalidOperationException(
                    "Open World final Player authority requires the one " +
                    "connected Safety Player to be the sole Player-tagged " +
                    "scene object.");
            }

            ValidateOpenWorldSafetySpawnMarkers(
                scene,
                allowExactRegionalSiblingOrderMigration: true);
            return;
        }

        if (safetyPlayers.Length != 1 || legacyPlayers.Length != 1 ||
            PrefabUtility.GetPrefabInstanceStatus(safetyPlayers[0]) !=
                PrefabInstanceStatus.Connected ||
            PrefabUtility.GetPrefabInstanceStatus(legacyPlayers[0]) !=
                PrefabInstanceStatus.Connected)
        {
            throw new InvalidOperationException(
                "Open World Player migration accepts only the exact merged " +
                "Safety source with one connected Safety Player and one " +
                "connected Player ForLevel, or the one-Player final state.");
        }

        GameObject[] taggedPlayers = FindSceneObjectsWithTag(scene, "Player");
        if (taggedPlayers.Length != 2 ||
            !taggedPlayers.ToHashSet().SetEquals(
                new[] { safetyPlayers[0], legacyPlayers[0] }) ||
            safetyPlayers[0].name != "Player" ||
            legacyPlayers[0].name != "Player" ||
            safetyPlayers[0].transform.parent != null ||
            !Approximately(
                safetyPlayers[0].transform.position,
                MixedSafetyPlayerSourcePosition) ||
            Quaternion.Angle(
                safetyPlayers[0].transform.rotation,
                MixedSafetyPlayerSourceRotation) > 0.01f ||
            !Approximately(safetyPlayers[0].transform.localScale, Vector3.one))
        {
            throw new InvalidOperationException(
                "Open World mixed Player source does not match the exact " +
                "Safety-merged Player names, tags, hierarchy, or pose.");
        }

        GameObject baseSpawn = RequireSinglePrefabInstanceRoot(
            scene,
            SafetyBaseSpawnPrefabPath);
        GameObject hubSpawn = RequireSinglePrefabInstanceRoot(
            scene,
            SafetyHubSpawnPrefabPath);
        GameObject[] regionalSpawns = OpenWorldRegionalSpawnPrefabPaths
            .Select(path => RequireSinglePrefabInstanceRoot(scene, path))
            .ToArray();
        GameObject[] expectedTaggedSpawns = new[] { baseSpawn, hubSpawn }
            .Concat(regionalSpawns)
            .ToArray();
        GameObject[] taggedSpawns = FindSceneObjectsWithTag(
            scene,
            "PlayerSpawnPos");
        GameObject existingArrival = RequirePath(scene, OpenWorldArrivalPath);

        if (taggedSpawns.Length != expectedTaggedSpawns.Length ||
            !taggedSpawns.ToHashSet().SetEquals(expectedTaggedSpawns) ||
            baseSpawn.name != "PlayerSpawnPos" ||
            baseSpawn.transform.parent !=
                RequirePath(scene, OpenWorldCorePath).transform ||
            !Approximately(
                baseSpawn.transform.position,
                existingArrival.transform.position) ||
            Quaternion.Angle(
                baseSpawn.transform.rotation,
                existingArrival.transform.rotation) > 0.01f ||
            hubSpawn.name != "PlayerSpawnPosHub" ||
            hubSpawn.transform.parent != null ||
            existingArrival == hubSpawn)
        {
            throw new InvalidOperationException(
                "Open World mixed spawn source must retain one exact base " +
                "fallback plus the five untouched Safety regional markers.");
        }

        GameObject[] allRegionalSpawns = regionalSpawns
            .Append(hubSpawn)
            .ToArray();
        string[] allSourceNames = OpenWorldRegionalSpawnSourceNames
            .Append("PlayerSpawnPosHub")
            .ToArray();
        for (int index = 0; index < allRegionalSpawns.Length; index++)
        {
            GameObject marker = allRegionalSpawns[index];
            CapsuleCollider[] colliders =
                marker.GetComponents<CapsuleCollider>();
            if (marker.name != allSourceNames[index] ||
                marker.transform.parent != null ||
                !marker.activeSelf ||
                !Approximately(
                    marker.transform.position,
                    MixedRegionalSpawnSourcePosition) ||
                Quaternion.Angle(
                    marker.transform.rotation,
                    Quaternion.identity) > 0.01f ||
                !Approximately(marker.transform.localScale, Vector3.one) ||
                colliders.Length != 1 ||
                colliders[0].enabled)
            {
                throw new InvalidOperationException(
                    $"Open World Safety marker '{allSourceNames[index]}' " +
                    "is not the exact untouched merged-source instance.");
            }
        }

        if (OpenWorldRegionalArrivalNames.Any(name =>
                FindSceneNamedObjects(scene, name).Length != 0))
        {
            throw new InvalidOperationException(
                "Open World mixed Safety source unexpectedly contains a " +
                "partial canonical regional-arrival migration.");
        }
    }

    private static void NormalizeOpenWorldSafetySpawnMarkers(Scene scene)
    {
        Terrain terrain = RequireSingleComponent<Terrain>(scene);
        GameObject oldArrival = RequirePath(scene, OpenWorldArrivalPath);
        GameObject hubMarker = RequireSinglePrefabInstanceRoot(
            scene,
            SafetyHubSpawnPrefabPath);

        if (oldArrival != hubMarker)
        {
            Transform targetParent = oldArrival.transform.parent;
            int targetSibling = oldArrival.transform.GetSiblingIndex();
            Vector3 targetPosition = oldArrival.transform.position;
            Quaternion targetRotation = oldArrival.transform.rotation;

            PrepareSafetySpawnMarker(
                hubMarker,
                targetParent,
                "World Arrival Spawn",
                targetPosition,
                targetRotation,
                targetSibling);
            CampaignSpawnPoint replacementSpawn =
                EnsureComponent<CampaignSpawnPoint>(hubMarker);
            replacementSpawn.Configure(
                "BlackPinesArrival",
                hubMarker.transform,
                true);

            Dictionary<UnityEngine.Object, UnityEngine.Object> replacementMap =
                BuildHierarchyReplacementMap(oldArrival, hubMarker);
            RewireExternalSceneReferences(
                scene,
                oldArrival.transform,
                hubMarker.transform,
                replacementMap,
                "Open World arrival");
            UnityEngine.Object.DestroyImmediate(oldArrival);
        }
        else
        {
            PrepareSafetySpawnMarker(
                hubMarker,
                hubMarker.transform.parent,
                "World Arrival Spawn",
                hubMarker.transform.position,
                hubMarker.transform.rotation,
                hubMarker.transform.GetSiblingIndex());
        }

        for (int index = 0;
             index < OpenWorldRegionalSpawnPrefabPaths.Length;
             index++)
        {
            GameObject marker = RequireSinglePrefabInstanceRoot(
                scene,
                OpenWorldRegionalSpawnPrefabPaths[index]);
            GameObject missionRoot = RequireSingleNamedObject(
                scene,
                OpenWorldMissionSystemNames[index]);
            GameObject[] existingTargets = FindSceneNamedObjects(
                scene,
                OpenWorldRegionalArrivalNames[index]);
            GameObject[] oldTargets = existingTargets
                .Where(candidate => candidate != marker)
                .ToArray();
            if (oldTargets.Length > 1 ||
                existingTargets.Count(candidate => candidate == marker) > 1)
            {
                throw new InvalidOperationException(
                    $"Open World regional arrival " +
                    $"'{OpenWorldRegionalArrivalNames[index]}' is duplicated.");
            }
            GameObject oldTarget = oldTargets.SingleOrDefault();

            Vector3 position = GetOpenWorldGroundPosition(
                terrain,
                OpenWorldRegionalArrivalPoints[index]);
            Vector2 point = OpenWorldRegionalArrivalPoints[index];
            Vector2 lookTarget =
                OpenWorldRegionalArrivalLookTargets[index];
            Vector3 forward = new(
                lookTarget.x - point.x,
                0f,
                lookTarget.y - point.y);
            Quaternion rotation = Quaternion.LookRotation(
                forward.normalized,
                Vector3.up);

            PrepareSafetySpawnMarker(
                marker,
                missionRoot.transform,
                OpenWorldRegionalArrivalNames[index],
                position,
                rotation,
                0);
            EnsureRegionalSafetySpawnFirstSibling(marker, missionRoot);

            if (oldTarget == null)
                continue;

            if (oldTarget.transform.childCount != 0 ||
                oldTarget.GetComponents<Component>().Length != 1)
            {
                throw new InvalidOperationException(
                    $"Existing regional arrival " +
                    $"'{OpenWorldRegionalArrivalNames[index]}' is not a " +
                    "recognized Transform-only anchor.");
            }

            Dictionary<UnityEngine.Object, UnityEngine.Object> replacementMap =
                BuildHierarchyReplacementMap(oldTarget, marker);
            RewireExternalSceneReferences(
                scene,
                oldTarget.transform,
                marker.transform,
                replacementMap,
                "regional respawn");
            UnityEngine.Object.DestroyImmediate(oldTarget);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        ValidateOpenWorldSafetySpawnMarkers(scene);
    }

    private static void PrepareSafetySpawnMarker(
        GameObject marker,
        Transform parent,
        string name,
        Vector3 position,
        Quaternion rotation,
        int siblingIndex)
    {
        if (marker.transform.parent != parent)
            marker.transform.SetParent(parent, true);
        marker.transform.SetSiblingIndex(Mathf.Clamp(
            siblingIndex,
            0,
            Math.Max(0, parent != null
                ? parent.childCount - 1
                : marker.scene.rootCount - 1)));
        marker.name = name;
        marker.tag = "Untagged";
        marker.layer = 0;
        marker.SetActive(true);
        marker.transform.SetPositionAndRotation(position, rotation);
        marker.transform.localScale = Vector3.one;
        RemoveDisabledSafetySpawnCollider(marker);
        PrefabUtility.RecordPrefabInstancePropertyModifications(marker);
        PrefabUtility.RecordPrefabInstancePropertyModifications(
            marker.transform);
        EditorUtility.SetDirty(marker);
        EditorUtility.SetDirty(marker.transform);
    }

    private static void EnsureRegionalSafetySpawnFirstSibling(
        GameObject marker,
        GameObject missionRoot)
    {
        if (marker == null || missionRoot == null ||
            marker.transform.parent != missionRoot.transform)
        {
            throw new InvalidOperationException(
                "A regional Safety spawn can only be ordered beneath its " +
                "exact Foundation mission root.");
        }

        marker.transform.SetSiblingIndex(0);
        if (!HasCanonicalRegionalSafetySpawnParentAndOrder(
                marker,
                missionRoot))
        {
            throw new InvalidOperationException(
                $"Regional Safety spawn '{marker.name}' could not be retained " +
                $"at sibling zero beneath '{missionRoot.name}'.");
        }
    }

    private static bool HasCanonicalRegionalSafetySpawnParentAndOrder(
        GameObject marker,
        GameObject missionRoot)
    {
        return marker != null &&
               missionRoot != null &&
               marker.transform.parent == missionRoot.transform &&
               marker.transform.GetSiblingIndex() == 0;
    }

    private static bool HasExactRegionalSafetySpawnSiblingOrderMigration(
        GameObject marker,
        GameObject missionRoot)
    {
        if (marker == null || missionRoot == null ||
            marker.transform.parent != missionRoot.transform ||
            missionRoot.transform.childCount != 3 ||
            marker.transform.GetSiblingIndex() != 2 ||
            missionRoot.activeSelf || missionRoot.activeInHierarchy)
        {
            return false;
        }

        Transform evidenceRoot = missionRoot.transform.GetChild(0);
        Transform alphaRoot = missionRoot.transform.GetChild(1);
        return evidenceRoot.name == AlphaWorldEvidenceVisualRootName &&
               alphaRoot.name == AlphaWorldMissionOwnedRootName &&
               evidenceRoot.gameObject.activeSelf &&
               !evidenceRoot.gameObject.activeInHierarchy &&
               alphaRoot.gameObject.activeSelf &&
               !alphaRoot.gameObject.activeInHierarchy &&
               evidenceRoot.gameObject.layer == 0 &&
               evidenceRoot.gameObject.CompareTag("Untagged") &&
               alphaRoot.gameObject.layer == 0 &&
               alphaRoot.gameObject.CompareTag("Untagged") &&
               Approximately(evidenceRoot.localPosition, Vector3.zero) &&
               Quaternion.Angle(
                   evidenceRoot.localRotation,
                   Quaternion.identity) <= 0.01f &&
               Approximately(evidenceRoot.localScale, Vector3.one) &&
               Approximately(alphaRoot.localPosition, Vector3.zero) &&
               Quaternion.Angle(
                   alphaRoot.localRotation,
                   Quaternion.identity) <= 0.01f &&
               Approximately(alphaRoot.localScale, Vector3.one) &&
               PrefabUtility.GetPrefabInstanceStatus(evidenceRoot.gameObject) ==
                   PrefabInstanceStatus.NotAPrefab &&
               PrefabUtility.GetPrefabInstanceStatus(alphaRoot.gameObject) ==
                   PrefabInstanceStatus.NotAPrefab &&
               GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                   evidenceRoot.gameObject) == 0 &&
               GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                   alphaRoot.gameObject) == 0;
    }

    internal static void EnsureRegionalSafetySpawnFirstSiblingForTests(
        GameObject marker,
        GameObject missionRoot)
    {
        EnsureRegionalSafetySpawnFirstSibling(marker, missionRoot);
    }

    internal static bool
        HasCanonicalRegionalSafetySpawnParentAndOrderForTests(
            GameObject marker,
            GameObject missionRoot)
    {
        return HasCanonicalRegionalSafetySpawnParentAndOrder(
            marker,
            missionRoot);
    }

    internal static bool
        HasExactRegionalSafetySpawnSiblingOrderMigrationForTests(
            GameObject marker,
            GameObject missionRoot)
    {
        return HasExactRegionalSafetySpawnSiblingOrderMigration(
            marker,
            missionRoot);
    }

    private static void RemoveDisabledSafetySpawnCollider(GameObject marker)
    {
        CapsuleCollider[] colliders = marker.GetComponents<CapsuleCollider>();
        if (colliders.Length == 0)
            return;
        if (colliders.Length != 1 || colliders[0].enabled ||
            PrefabUtility.GetCorrespondingObjectFromSource(colliders[0]) == null)
        {
            throw new InvalidOperationException(
                $"Safety arrival marker '{marker.name}' must inherit only " +
                "the one disabled Safety CapsuleCollider before its " +
                "scene-instance removal override is authored.");
        }

        Undo.DestroyObjectImmediate(colliders[0]);
    }

    private static Vector3 GetOpenWorldGroundPosition(
        Terrain terrain,
        Vector2 point)
    {
        if (terrain == null || terrain.terrainData == null ||
            !terrain.isActiveAndEnabled)
        {
            throw new InvalidOperationException(
                "Open World arrival grounding requires one active Terrain.");
        }

        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        if (point.x < origin.x || point.x > origin.x + size.x ||
            point.y < origin.z || point.y > origin.z + size.z)
        {
            throw new InvalidOperationException(
                $"Open World arrival point ({point.x:F1}, {point.y:F1}) " +
                "falls outside the authored Terrain.");
        }

        Vector3 sample = new(point.x, origin.y, point.y);
        return new Vector3(
            point.x,
            terrain.SampleHeight(sample) + origin.y,
            point.y);
    }

    private static void ValidateOpenWorldSafetySpawnMarkers(
        Scene scene,
        bool allowExactRegionalSiblingOrderMigration = false)
    {
        RequireExactOpenWorldSpawnPrefabs();
        GameObject baseSpawn = RequireSinglePrefabInstanceRoot(
            scene,
            SafetyBaseSpawnPrefabPath);
        GameObject[] taggedSpawns = FindSceneObjectsWithTag(
            scene,
            "PlayerSpawnPos");
        if (taggedSpawns.Length != 1 || taggedSpawns[0] != baseSpawn ||
            baseSpawn.name != "PlayerSpawnPos" ||
            baseSpawn.transform.parent !=
                RequirePath(scene, OpenWorldCorePath).transform ||
            PrefabUtility.GetPrefabInstanceStatus(baseSpawn) !=
                PrefabInstanceStatus.Connected)
        {
            throw new InvalidOperationException(
                "Open World requires the moved exact Safety base marker as " +
                "its sole connected PlayerSpawnPos fallback.");
        }

        Terrain terrain = RequireSingleComponent<Terrain>(scene);
        for (int index = 0;
             index < OpenWorldRegionalSpawnPrefabPaths.Length;
             index++)
        {
            GameObject marker = RequireSinglePrefabInstanceRoot(
                scene,
                OpenWorldRegionalSpawnPrefabPaths[index]);
            GameObject missionRoot = RequireSingleNamedObject(
                scene,
                OpenWorldMissionSystemNames[index]);
            Vector2 point = OpenWorldRegionalArrivalPoints[index];
            Vector2 lookTarget =
                OpenWorldRegionalArrivalLookTargets[index];
            Vector3 expectedForward = new(
                lookTarget.x - point.x,
                0f,
                lookTarget.y - point.y);
            Vector3 actualForward = marker.transform.forward;
            actualForward.y = 0f;
            actualForward.Normalize();
            bool hasCanonicalParentAndOrder =
                HasCanonicalRegionalSafetySpawnParentAndOrder(
                    marker,
                    missionRoot);
            bool hasRecognizedMigrationOrder =
                allowExactRegionalSiblingOrderMigration &&
                HasExactRegionalSafetySpawnSiblingOrderMigration(
                    marker,
                    missionRoot);

            if (marker.name != OpenWorldRegionalArrivalNames[index] ||
                (!hasCanonicalParentAndOrder &&
                 !hasRecognizedMigrationOrder) ||
                marker.CompareTag("PlayerSpawnPos") ||
                !marker.CompareTag("Untagged") ||
                !marker.activeSelf ||
                PrefabUtility.GetPrefabInstanceStatus(marker) !=
                    PrefabInstanceStatus.Connected ||
                Vector3.Distance(
                    marker.transform.position,
                    GetOpenWorldGroundPosition(terrain, point)) > 0.02f ||
                Vector3.Dot(
                    actualForward,
                    expectedForward.normalized) < 0.999f ||
                !Approximately(marker.transform.localScale, Vector3.one) ||
                marker.transform.childCount != 0 ||
                marker.GetComponents<Component>().Length != 1)
            {
                throw new InvalidOperationException(
                    $"Open World regional Safety marker {index} is not the " +
                    "exact connected, grounded, untagged Transform-only " +
                    "campaign respawn anchor.");
            }
        }

        GameObject arrival = RequirePath(scene, OpenWorldArrivalPath);
        GameObject hubMarker = RequireSinglePrefabInstanceRoot(
            scene,
            SafetyHubSpawnPrefabPath);
        GameObject arrivalParent = RequirePath(
            scene,
            "Bloodroot_OpenWorld/AREA_00_BLACK_PINES_FOREST");
        CampaignSpawnPoint arrivalSpawn =
            hubMarker.GetComponent<CampaignSpawnPoint>();
        if (arrival != hubMarker ||
            hubMarker.transform.parent != arrivalParent.transform ||
            hubMarker.CompareTag("PlayerSpawnPos") ||
            !hubMarker.CompareTag("Untagged") ||
            !hubMarker.activeSelf ||
            PrefabUtility.GetPrefabInstanceStatus(hubMarker) !=
                PrefabInstanceStatus.Connected ||
            hubMarker.GetComponents<Collider>().Length != 0 ||
            hubMarker.GetComponents<Component>().Length != 2 ||
            arrivalSpawn == null ||
            !Approximately(
                hubMarker.transform.position,
                baseSpawn.transform.position) ||
            Quaternion.Angle(
                hubMarker.transform.rotation,
                baseSpawn.transform.rotation) > 0.01f ||
            !Approximately(hubMarker.transform.localScale, Vector3.one))
        {
            throw new InvalidOperationException(
                "Open World World Arrival Spawn must be the exact connected " +
                "Safety Hub marker converted to an untagged collider-free " +
                "campaign arrival.");
        }

        foreach (string sourceName in OpenWorldRegionalSpawnSourceNames
                     .Append("PlayerSpawnPosHub"))
        {
            if (FindSceneNamedObjects(scene, sourceName).Length != 0)
            {
                throw new InvalidOperationException(
                    $"Open World retains stale Safety marker name " +
                    $"'{sourceName}' after canonical arrival conversion.");
            }
        }
    }

    private static void RequireExactOpenWorldSpawnPrefabs()
    {
        RequireExactSafetySpawnPrefab(
            SafetyBaseSpawnPrefabPath,
            SafetyBaseSpawnPrefabGuid);
        for (int index = 0;
             index < OpenWorldRegionalSpawnPrefabPaths.Length;
             index++)
        {
            RequireExactSafetySpawnPrefab(
                OpenWorldRegionalSpawnPrefabPaths[index],
                OpenWorldRegionalSpawnPrefabGuids[index]);
        }
        RequireExactSafetySpawnPrefab(
            SafetyHubSpawnPrefabPath,
            SafetyHubSpawnPrefabGuid);
    }

    private static void RequireExactSafetySpawnPrefab(
        string path,
        string guid)
    {
        GameObject prefab = LoadRequiredExactAsset<GameObject>(path, guid);
        CapsuleCollider[] colliders = prefab.GetComponents<CapsuleCollider>();
        if (prefab.GetComponents<Component>().Length != 2 ||
            colliders.Length != 1 ||
            colliders[0].enabled ||
            !prefab.CompareTag("PlayerSpawnPos"))
        {
            throw new InvalidOperationException(
                $"Safety spawn prefab '{path}' must retain its exact tagged " +
                "Transform plus disabled CapsuleCollider source contract.");
        }
    }

    private static GameObject RequireSinglePrefabInstanceRoot(
        Scene scene,
        string prefabPath)
    {
        GameObject[] roots = FindPrefabInstanceRoots(scene, prefabPath);
        if (roots.Length != 1 ||
            PrefabUtility.GetPrefabInstanceStatus(roots[0]) !=
                PrefabInstanceStatus.Connected)
        {
            throw new InvalidOperationException(
                $"Scene '{scene.name}' requires exactly one connected " +
                $"'{prefabPath}' instance; found {roots.Length}.");
        }

        return roots[0];
    }

    private static GameObject[] FindSceneObjectsWithTag(
        Scene scene,
        string tag)
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(transform => transform.gameObject)
            .Where(item => item.CompareTag(tag))
            .Distinct()
            .ToArray();
    }

    private static GameObject RequireExactSafetyPlayerPrefab()
    {
        string actualGuid = AssetDatabase.AssetPathToGUID(
            SafetyPlayerPrefabPath);
        if (!string.Equals(
                actualGuid,
                SafetyPlayerPrefabGuid,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Safety Player GUID mismatch at '{SafetyPlayerPrefabPath}'. " +
                $"Expected {SafetyPlayerPrefabGuid}; found " +
                $"'{actualGuid}'. Pull the exact Safety asset without " +
                "modifying it.");
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            SafetyPlayerPrefabPath);
        if (prefab == null ||
            PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.NotAPrefab)
        {
            throw new InvalidOperationException(
                $"Exact Safety Player prefab is missing at " +
                $"'{SafetyPlayerPrefabPath}'.");
        }

        RequireEnabledRootPrefabComponent<playerController>(prefab);
        RequireEnabledRootPrefabComponent<Inventory>(prefab);
        RequireEnabledRootPrefabComponent<Interact>(prefab);
        RequireEnabledRootPrefabComponent<CharacterController>(prefab);

        CharacterController sourceController =
            prefab.GetComponent<CharacterController>();
        CapsuleCollider[] sourceDamageColliders =
            prefab.GetComponents<CapsuleCollider>();
        if (sourceController.center != Vector3.zero ||
            Mathf.Abs(sourceController.height - 2f) > 0.001f ||
            Mathf.Abs(sourceController.radius - 0.5f) > 0.001f ||
            sourceDamageColliders.Length != 1 ||
            !sourceDamageColliders[0].enabled ||
            sourceDamageColliders[0].isTrigger ||
            sourceDamageColliders[0].center != Vector3.zero ||
            Mathf.Abs(sourceDamageColliders[0].height - 2f) > 0.001f ||
            Mathf.Abs(sourceDamageColliders[0].radius - 0.5f) > 0.001f)
        {
            throw new InvalidOperationException(
                "Exact Safety Player prefab must retain its source-centered " +
                "CharacterController and solid CapsuleCollider. Campaign " +
                "authoring must not apply scene-instance center overrides.");
        }

        cameraController[] cameras =
            prefab.GetComponentsInChildren<cameraController>(true);
        Transform[] nestedCharacterRoots = prefab
            .GetComponentsInChildren<Transform>(true)
            .Where(transform =>
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    transform,
                    out string guid,
                    out long fileId) &&
                string.Equals(
                    guid,
                    SafetyPlayerPrefabGuid,
                    StringComparison.OrdinalIgnoreCase) &&
                fileId == SafetyPlayerNestedCharacterRootTransformFileId)
            .ToArray();
        if (cameras.Length != 1 || !cameras[0].enabled ||
            !Exactly(
                cameras[0].transform.localPosition,
                FarmSafetyPlayerSourceCameraLocalPosition) ||
            nestedCharacterRoots.Length != 1 ||
            !Exactly(
                nestedCharacterRoots[0].localPosition,
                SafetyPlayerNestedCharacterRootPosition))
        {
            throw new InvalidOperationException(
                "Exact Safety Player prefab requires its enabled inherited " +
                "camera at local (0, 0.809, 0.013) and exact nested character root at " +
                "local y=-1.05.");
        }

        return prefab;
    }

    private static Transform RequireExactSafetyPlayerPrefabMainBodyTransform()
    {
        GameObject prefab = RequireExactSafetyPlayerPrefab();
        Transform[] candidates = prefab
            .GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .Where(renderer => renderer.gameObject.name == "main body.001")
            .Select(renderer => renderer.transform)
            .Distinct()
            .ToArray();
        if (candidates.Length != 1)
        {
            throw new InvalidOperationException(
                "Exact Safety Player prefab must retain one skinned " +
                "'main body.001' Transform.");
        }

        Transform body = candidates[0];
        Transform nestedSource =
            PrefabUtility.GetCorrespondingObjectFromSource(body);
        Transform originalSource =
            PrefabUtility.GetCorrespondingObjectFromOriginalSource(body);
        bool exactBodyIdentity =
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                body,
                out string bodyGuid,
                out long bodyFileId) &&
            string.Equals(
                bodyGuid,
                SafetyPlayerPrefabGuid,
                StringComparison.OrdinalIgnoreCase) &&
            bodyFileId == SafetyPlayerMainBodyTransformFileId;
        bool exactNestedIdentity =
            nestedSource != null &&
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                nestedSource,
                out string nestedGuid,
                out long nestedFileId) &&
            string.Equals(
                nestedGuid,
                SafetyPlayerNestedCharacterPrefabGuid,
                StringComparison.OrdinalIgnoreCase) &&
            nestedFileId == SafetyPlayerNestedMainBodyTransformFileId;
        bool exactOriginalIdentity =
            originalSource != null &&
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                originalSource,
                out string originalGuid,
                out long originalFileId) &&
            string.Equals(
                originalGuid,
                SafetyPlayerMainBodyModelGuid,
                StringComparison.OrdinalIgnoreCase) &&
            originalFileId == SafetyPlayerMainBodyModelTransformFileId;

        if (!exactBodyIdentity ||
            !exactNestedIdentity ||
            !exactOriginalIdentity ||
            !Exactly(body.localPosition, SafetyPlayerNestedMainBodyPosition) ||
            body.GetComponents<SkinnedMeshRenderer>().Length != 1)
        {
            throw new InvalidOperationException(
                "Exact Safety Player main-body source identity or protected " +
                "25.52/0/-66.78 presentation offset changed. Re-audit the " +
                "online-safety visual contract before migrating scenes.");
        }

        return body;
    }

    private static Transform RequireExactSafetyPlayerSceneMainBodyTransform(
        GameObject player,
        out Transform sourceBody)
    {
        Transform exactSourceBody =
            RequireExactSafetyPlayerPrefabMainBodyTransform();
        sourceBody = exactSourceBody;
        Transform[] matches = player != null
            ? player.GetComponentsInChildren<Transform>(true)
                .Where(transform =>
                    PrefabUtility.GetCorrespondingObjectFromSource(transform) ==
                    exactSourceBody)
                .ToArray()
            : Array.Empty<Transform>();
        if (matches.Length != 1 ||
            matches[0].gameObject.name != "main body.001" ||
            matches[0].GetComponents<SkinnedMeshRenderer>().Length != 1)
        {
            throw new InvalidOperationException(
                "Connected Safety Player scene instance must retain the exact " +
                "nested 'main body.001' Transform and SkinnedMeshRenderer.");
        }

        return matches[0];
    }

    private static PropertyModification[]
        GetSafetyPlayerMainBodyTransformModifications(
            GameObject player,
            Transform sourceBody)
    {
        return (PrefabUtility.GetPropertyModifications(player) ??
                Array.Empty<PropertyModification>())
            .Where(modification => modification.target == sourceBody)
            .ToArray();
    }

    private static bool ValidateSafetyPlayerMainBodyState(
        GameObject player,
        bool allowLegacyZeroPosition)
    {
        Transform body = RequireExactSafetyPlayerSceneMainBodyTransform(
            player,
            out Transform sourceBody);
        PropertyModification[] bodyModifications =
            GetSafetyPlayerMainBodyTransformModifications(player, sourceBody);
        string[] legacyPaths =
        {
            "m_LocalPosition.x",
            "m_LocalPosition.y",
            "m_LocalPosition.z"
        };
        bool exactLegacyZero =
            allowLegacyZeroPosition &&
            bodyModifications.Length == legacyPaths.Length &&
            bodyModifications.All(modification =>
                modification.objectReference == null &&
                modification.value == "0" &&
                legacyPaths.Contains(modification.propertyPath)) &&
            bodyModifications.Select(modification => modification.propertyPath)
                .Distinct(StringComparer.Ordinal)
                .Count() == legacyPaths.Length &&
            Exactly(body.localPosition, Vector3.zero);
        bool exactCurrent =
            bodyModifications.Length == 0 &&
            Exactly(body.localPosition, sourceBody.localPosition);

        if ((!exactLegacyZero && !exactCurrent) ||
            !Exactly(body.localRotation, sourceBody.localRotation) ||
            !Exactly(body.localScale, sourceBody.localScale))
        {
            throw new InvalidOperationException(
                "Safety Player main body must either inherit its exact protected " +
                "pose or contain only the exact legacy zero-position override " +
                "created by the retired compatibility patch.");
        }

        return exactLegacyZero;
    }

    private static void ValidateSafetyPlayerMainBodyMigrationState(Scene scene)
    {
        RequireExactSafetyPlayerPrefabMainBodyTransform();
        foreach (GameObject player in FindPrefabInstanceRoots(
                     scene,
                     SafetyPlayerPrefabPath))
        {
            ValidateSafetyPlayerMainBodyState(
                player,
                allowLegacyZeroPosition: true);
        }
    }

    private static bool RevertLegacySafetyPlayerMainBodyPositionOverride(
        GameObject player)
    {
        bool requiresMigration = ValidateSafetyPlayerMainBodyState(
            player,
            allowLegacyZeroPosition: true);
        if (!requiresMigration)
            return false;

        Transform body = RequireExactSafetyPlayerSceneMainBodyTransform(
            player,
            out _);
        SerializedObject bodyData = new(body);
        SerializedProperty localPosition =
            bodyData.FindProperty("m_LocalPosition");
        if (localPosition == null || !localPosition.prefabOverride)
        {
            throw new InvalidOperationException(
                "Recognized Safety Player main-body migration is missing its " +
                "exact prefab position override.");
        }

        PrefabUtility.RevertPropertyOverride(
            localPosition,
            InteractionMode.AutomatedAction);
        EditorUtility.SetDirty(body);

        ValidateSafetyPlayerMainBodyState(
            player,
            allowLegacyZeroPosition: false);
        return true;
    }

    internal static Transform GetSafetyPlayerMainBodyTransformForTests(
        GameObject player)
    {
        return RequireExactSafetyPlayerSceneMainBodyTransform(player, out _);
    }

    internal static bool RevertLegacySafetyPlayerMainBodyPositionOverrideForTests(
        GameObject player)
    {
        return RevertLegacySafetyPlayerMainBodyPositionOverride(player);
    }

    internal static void ValidateSafetyPlayerMainBodyStateForTests(
        GameObject player,
        bool allowLegacyZeroPosition)
    {
        ValidateSafetyPlayerMainBodyState(player, allowLegacyZeroPosition);
    }

    private static void RequireRetiredSafetyPlayerIsolationTargets(
        GameObject player,
        out Camera camera,
        out Camera sourceCamera,
        out GameObject[] visualObjects,
        out GameObject[] sourceVisualObjects)
    {
        const long exactCameraFileId = 897183568886256928L;
        const int exactSourceCameraMask = 32767;
        string[] exactVisualNames =
        {
            "GAS MASK.001",
            "HELMET",
            "MAIN HOOD",
            "main body.001"
        };
        long[] exactVisualGameObjectFileIds =
        {
            6481680608952219174L,
            7786971947018392444L,
            8309013754813627902L,
            9024371758269125207L
        };

        GameObject prefab = RequireExactSafetyPlayerPrefab();
        Camera[] sourceCameras = prefab.GetComponentsInChildren<Camera>(true);
        Camera exactSourceCamera =
            sourceCameras.Length == 1 ? sourceCameras[0] : null;
        sourceCamera = exactSourceCamera;
        bool exactCameraIdentity = exactSourceCamera != null &&
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                exactSourceCamera,
                out string cameraGuid,
                out long cameraFileId) &&
            string.Equals(
                cameraGuid,
                SafetyPlayerPrefabGuid,
                StringComparison.OrdinalIgnoreCase) &&
            cameraFileId == exactCameraFileId;
        if (!exactCameraIdentity ||
            exactSourceCamera.transform.parent != prefab.transform ||
            exactSourceCamera.gameObject.name != "Main Camera" ||
            exactSourceCamera.cullingMask != exactSourceCameraMask)
        {
            throw new InvalidOperationException(
                "The protected Safety Player camera no longer matches the exact " +
                "source identity and 32767 mask used by the retired override migration.");
        }

        SkinnedMeshRenderer[] sourceRenderers = prefab
            .GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (sourceRenderers.Length != exactVisualNames.Length ||
            sourceRenderers.Any(renderer => renderer.gameObject.layer != 0) ||
            !sourceRenderers.Select(renderer => renderer.gameObject.name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .SequenceEqual(
                    exactVisualNames.OrderBy(
                        name => name,
                        StringComparer.Ordinal),
                    StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "The protected Safety Player no longer contains the exact four " +
                "layer-zero visual objects used by the retired override migration.");
        }

        GameObject[] exactSourceVisualObjects = sourceRenderers
            .Select(renderer => renderer.gameObject)
            .ToArray();
        sourceVisualObjects = exactSourceVisualObjects;
        long[] sourceVisualFileIds = exactSourceVisualObjects
            .Select(sourceObject =>
            {
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        sourceObject,
                        out string sourceGuid,
                        out long sourceFileId) ||
                    !string.Equals(
                        sourceGuid,
                        SafetyPlayerPrefabGuid,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "A retired Safety Player visual override target no " +
                        "longer belongs to the protected Player prefab.");
                }

                return sourceFileId;
            })
            .OrderBy(fileId => fileId)
            .ToArray();
        if (!sourceVisualFileIds.SequenceEqual(
                exactVisualGameObjectFileIds.OrderBy(fileId => fileId)))
        {
            throw new InvalidOperationException(
                "The exact protected Safety Player visual-object identities " +
                "used by the retired override migration changed.");
        }

        Camera[] cameraMatches = player != null
            ? player.GetComponentsInChildren<Camera>(true)
                .Where(candidate =>
                    PrefabUtility.GetCorrespondingObjectFromSource(candidate) ==
                    exactSourceCamera)
                .ToArray()
            : Array.Empty<Camera>();
        camera = cameraMatches.Length == 1 ? cameraMatches[0] : null;

        SkinnedMeshRenderer[] sceneRenderers = player != null
            ? player.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            : Array.Empty<SkinnedMeshRenderer>();
        visualObjects = sourceRenderers
            .Select(sourceRenderer => sceneRenderers
                .Where(renderer =>
                    PrefabUtility.GetCorrespondingObjectFromSource(renderer) ==
                    sourceRenderer)
                .Select(renderer => renderer.gameObject)
                .SingleOrDefault())
            .ToArray();
        if (camera == null ||
            sceneRenderers.Length != sourceRenderers.Length ||
            visualObjects.Any(target => target == null))
        {
            throw new InvalidOperationException(
                "Connected Safety Player instance no longer contains the exact " +
                "camera and four visual targets used by the retired migration.");
        }
    }

    private static bool ValidateRetiredSafetyPlayerIsolationState(
        GameObject player,
        bool allowRetiredOverrides,
        out PropertyModification[] retiredOverrides)
    {
        const string cameraMaskPath = "m_CullingMask.m_Bits";
        const string layerPath = "m_Layer";
        const string retiredCameraMaskValue = "32759";
        const string retiredLayerValue = "3";

        RequireRetiredSafetyPlayerIsolationTargets(
            player,
            out Camera camera,
            out Camera sourceCamera,
            out GameObject[] visualObjects,
            out GameObject[] sourceVisualObjects);
        PropertyModification[] allModifications =
            PrefabUtility.GetPropertyModifications(player) ??
            Array.Empty<PropertyModification>();
        PropertyModification[] cameraOverrides = allModifications
            .Where(modification =>
                modification?.target == sourceCamera &&
                modification.propertyPath.StartsWith(
                    "m_CullingMask",
                    StringComparison.Ordinal))
            .ToArray();
        PropertyModification[][] layerOverrides = sourceVisualObjects
            .Select(sourceObject => allModifications
                .Where(modification =>
                    modification?.target == sourceObject &&
                    modification.propertyPath == layerPath)
                .ToArray())
            .ToArray();

        bool inherited =
            cameraOverrides.Length == 0 &&
            layerOverrides.All(overrides => overrides.Length == 0) &&
            camera.cullingMask == sourceCamera.cullingMask &&
            visualObjects.Zip(
                    sourceVisualObjects,
                    (instance, source) => instance.layer == source.layer)
                .All(matches => matches);
        bool exactRetired =
            cameraOverrides.Length == 1 &&
            cameraOverrides[0].propertyPath == cameraMaskPath &&
            cameraOverrides[0].value == retiredCameraMaskValue &&
            cameraOverrides[0].objectReference == null &&
            layerOverrides.All(overrides =>
                overrides.Length == 1 &&
                overrides[0].propertyPath == layerPath &&
                overrides[0].value == retiredLayerValue &&
                overrides[0].objectReference == null) &&
            camera.cullingMask == 32759 &&
            visualObjects.All(target => target.layer == 3);

        if (!inherited && !exactRetired)
        {
            throw new InvalidOperationException(
                "Safety Player contains a partial or unknown camera/layer edit. " +
                "Only the exact retired 32759 camera-mask override plus all four " +
                "exact layer-3 visual overrides may be removed automatically.");
        }

        if (exactRetired && !allowRetiredOverrides)
        {
            throw new InvalidOperationException(
                "Safety Player still contains the five retired scene-local " +
                "camera/layer overrides. Rebuild the campaign foundation to " +
                "restore exact inheritance from the protected Safety prefab.");
        }

        retiredOverrides = exactRetired
            ? cameraOverrides.Concat(layerOverrides.SelectMany(value => value))
                .ToArray()
            : Array.Empty<PropertyModification>();
        return exactRetired;
    }

    private static void ValidateRetiredSafetyPlayerIsolationMigrationState(
        Scene scene)
    {
        foreach (GameObject player in FindPrefabInstanceRoots(
                     scene,
                     SafetyPlayerPrefabPath))
        {
            ValidateRetiredSafetyPlayerIsolationState(
                player,
                allowRetiredOverrides: true,
                out _);
        }
    }

    private static bool RevertRetiredSafetyPlayerIsolationOverrides(
        GameObject player)
    {
        bool requiresMigration = ValidateRetiredSafetyPlayerIsolationState(
            player,
            allowRetiredOverrides: true,
            out PropertyModification[] retiredOverrides);
        if (!requiresMigration)
            return false;

        PropertyModification[] before =
            PrefabUtility.GetPropertyModifications(player) ??
            Array.Empty<PropertyModification>();
        var retiredSet = new HashSet<PropertyModification>(
            retiredOverrides,
            PropertyModificationComparer.Instance);
        PropertyModification[] retained = before
            .Where(modification => !retiredSet.Contains(modification))
            .ToArray();
        if (before.Length - retained.Length != 5)
        {
            throw new InvalidOperationException(
                "Retired Safety Player migration did not resolve exactly five " +
                "camera/layer prefab modifications; no changes were applied.");
        }

        PrefabUtility.SetPropertyModifications(player, retained);
        PropertyModification[] after =
            PrefabUtility.GetPropertyModifications(player) ??
            Array.Empty<PropertyModification>();
        if (after.Length != retained.Length ||
            !ContainSamePropertyModifications(after, retained))
        {
            throw new InvalidOperationException(
                "Unity changed unrelated Safety Player prefab overrides while " +
                "retiring the exact camera/layer modifications.");
        }

        EditorUtility.SetDirty(player);
        if (player.scene.IsValid() && player.scene.isLoaded)
            EditorSceneManager.MarkSceneDirty(player.scene);

        ValidateRetiredSafetyPlayerIsolationState(
            player,
            allowRetiredOverrides: false,
            out _);
        return true;
    }

    internal static bool RevertRetiredSafetyPlayerIsolationOverridesForTests(
        GameObject player)
    {
        return RevertRetiredSafetyPlayerIsolationOverrides(player);
    }

    internal static void ValidateRetiredSafetyPlayerIsolationStateForTests(
        GameObject player,
        bool allowRetiredOverrides)
    {
        ValidateRetiredSafetyPlayerIsolationState(
            player,
            allowRetiredOverrides,
            out _);
    }

    private static void ValidateSafetyPlayerSceneAuthority(
        Scene scene,
        Transform authoredSpawn)
    {
        RequireExactSafetyPlayerPrefab();

        GameObject player = RequireTaggedObject(scene, "Player");
        GameObject fallback = RequireTaggedObject(scene, "PlayerSpawnPos");
        GameObject[] safetyInstances = FindPrefabInstanceRoots(
            scene,
            SafetyPlayerPrefabPath);
        GameObject[] legacyInstances = FindPrefabInstanceRoots(
            scene,
            LegacyPlayerForLevelPrefabPath);

        if (safetyInstances.Length != 1 ||
            safetyInstances[0] != player ||
            legacyInstances.Length != 0 ||
            PrefabUtility.GetPrefabInstanceStatus(player) !=
                PrefabInstanceStatus.Connected)
        {
            throw new InvalidOperationException(
                $"Scene '{scene.name}' requires exactly one connected exact " +
                "Safety Player prefab instance and zero Player ForLevel " +
                $"instances; found {safetyInstances.Length} Safety and " +
                $"{legacyInstances.Length} ForLevel.");
        }

        ValidateSafetyPlayerMainBodyState(
            player,
            allowLegacyZeroPosition: false);
        ValidateRetiredSafetyPlayerIsolationState(
            player,
            allowRetiredOverrides: false,
            out _);

        bool isFarm = scene.path == FarmScenePath;
        bool exactPlayerPose = isFarm
            ? IsRecognizedFarmSafetyPlayerPose(player.transform)
            : authoredSpawn != null &&
              Approximately(
                  player.transform.position,
                  authoredSpawn.position) &&
              Quaternion.Angle(
                  player.transform.rotation,
                  authoredSpawn.rotation) <= 0.01f;
        if (!player.activeInHierarchy ||
            player.name != "Player" ||
            player.layer != LayerMask.NameToLayer(PlayerLayerName) ||
            authoredSpawn == null ||
            fallback.transform == authoredSpawn ||
            !exactPlayerPose ||
            !Approximately(player.transform.localScale, Vector3.one) ||
            !Approximately(fallback.transform.position, authoredSpawn.position) ||
            Quaternion.Angle(
                fallback.transform.rotation,
                authoredSpawn.rotation) > 0.01f)
        {
            throw new InvalidOperationException(
                isFarm
                    ? "Farm must preserve the exact online-Safety Player " +
                      "edit-time pose while its original tagged fallback and " +
                      "campaign Prologue Spawn share the exact runtime pose."
                    : $"Scene '{scene.name}' Player, tagged fallback, and " +
                      "authored arrival must share one exact position and " +
                      "rotation while remaining separate scene objects.");
        }

        GameObject stateRoot = scene.GetRootGameObjects()
            .SingleOrDefault(root => root.name == ServiceRootName);
        if (stateRoot != null &&
            (player.transform == stateRoot.transform ||
             player.transform.IsChildOf(stateRoot.transform)))
        {
            throw new InvalidOperationException(
                $"Scene '{scene.name}' Player must remain scene-local and " +
                "outside the persistent campaign-service hierarchy.");
        }

        foreach (Transform transform in
                 player.GetComponentsInChildren<Transform>(true))
        {
            if (GameObjectUtility.GetStaticEditorFlags(transform.gameObject) !=
                (StaticEditorFlags)0)
            {
                throw new InvalidOperationException(
                    $"Scene '{scene.name}' Safety Player hierarchy must clear " +
                    "all static flags through scene-instance overrides.");
            }
        }

        RequireEnabledPlayerRootComponent<playerController>(player, scene);
        RequireEnabledPlayerRootComponent<Inventory>(player, scene);
        RequireEnabledPlayerRootComponent<Interact>(player, scene);
        playerController movement = player.GetComponent<playerController>();

        CharacterController[] controllers =
            player.GetComponents<CharacterController>();
        CapsuleCollider[] damageColliders =
            player.GetComponents<CapsuleCollider>();
        if (controllers.Length != 1 || !controllers[0].enabled ||
            controllers[0].center != Vector3.zero ||
            Mathf.Abs(controllers[0].height - 2f) > 0.001f ||
            Mathf.Abs(controllers[0].radius - 0.5f) > 0.001f ||
            damageColliders.Length != 1 ||
            !damageColliders[0].enabled || damageColliders[0].isTrigger ||
            damageColliders[0].center != Vector3.zero ||
            Mathf.Abs(damageColliders[0].height - 2f) > 0.001f ||
            Mathf.Abs(damageColliders[0].radius - 0.5f) > 0.001f)
        {
            throw new InvalidOperationException(
                $"Scene '{scene.name}' Safety Player requires the exact " +
                "source-centered enabled root CharacterController and solid " +
                "CapsuleCollider used by online Safety.");
        }

        cameraController[] cameras =
            player.GetComponentsInChildren<cameraController>(true);
        if (cameras.Length != 1 || !cameras[0].enabled)
        {
            throw new InvalidOperationException(
                $"Scene '{scene.name}' Safety Player requires exactly one " +
                "enabled inherited cameraController.");
        }

        if (isFarm)
        {
            ValidateFarmSafetyPlayerCameraOverride(player, cameras[0]);
            GameObject[] fallbackInstances = FindPrefabInstanceRoots(
                scene,
                SafetyBaseSpawnPrefabPath);
            if (fallbackInstances.Length != 1 ||
                fallbackInstances[0] != fallback)
            {
                throw new InvalidOperationException(
                    "Farm must contain only the original connected online-" +
                    "Safety PlayerSpawnPos after retiring the exact unreferenced duplicate.");
            }
        }

        gameManager manager = RequireSingleComponent<gameManager>(scene);
        if (manager.player != player ||
            manager.playerController != movement ||
            manager.playerSpawnPos != fallback)
        {
            throw new InvalidOperationException(
                $"Scene '{scene.name}' GameManager must reference the exact " +
                "Safety Player root, root controller, and separate tagged " +
                "PlayerSpawnPos fallback.");
        }
    }

    private static void RequireEnabledPlayerRootComponent<T>(
        GameObject player,
        Scene scene)
        where T : Behaviour
    {
        T[] rootComponents = player.GetComponents<T>();
        T[] enabledSceneComponents = FindSceneComponents<T>(scene)
            .Where(component => component.enabled)
            .ToArray();
        if (rootComponents.Length != 1 ||
            !rootComponents[0].enabled ||
            enabledSceneComponents.Length != 1 ||
            enabledSceneComponents[0] != rootComponents[0])
        {
            throw new InvalidOperationException(
                $"Scene '{scene.name}' requires its one enabled " +
                $"{typeof(T).Name} on the Safety Player root.");
        }
    }

    private static GameObject[] FindPrefabInstanceRoots(
        Scene scene,
        string prefabPath)
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(transform =>
                PrefabUtility.GetNearestPrefabInstanceRoot(
                    transform.gameObject))
            .Where(root =>
                root != null &&
                root.scene == scene &&
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) ==
                    prefabPath)
            .Distinct()
            .ToArray();
    }

    private static void RequireEnabledRootPrefabComponent<T>(
        GameObject prefab)
        where T : Component
    {
        T[] components = prefab.GetComponents<T>();
        if (components.Length != 1 ||
            (components[0] is Behaviour behaviour && !behaviour.enabled))
        {
            throw new InvalidOperationException(
                $"Exact Safety Player prefab requires one enabled " +
                $"{typeof(T).Name} on its root.");
        }
    }

    private static void ApplySafetyPlayerSceneOverrides(
        GameObject player,
        Transform authoredSpawn)
    {
        if (player == null || authoredSpawn == null)
        {
            throw new InvalidOperationException(
                "Safety Player scene authoring requires an instance and an " +
                "exact authored spawn Transform.");
        }

        RevertLegacySafetyPlayerMainBodyPositionOverride(player);
        RevertRetiredSafetyPlayerIsolationOverrides(player);

        if (player.name != "Player")
        {
            player.name = "Player";
        }

        if (!player.CompareTag("Player"))
        {
            player.tag = "Player";
        }

        if (!player.activeSelf)
        {
            player.SetActive(true);
        }

        if (player.scene.path == FarmScenePath)
        {
            if (!IsRecognizedFarmSafetyPlayerPose(player.transform))
            {
                throw new InvalidOperationException(
                    "Farm Safety Player no longer has an exact recognized " +
                    "online-Safety edit-time pose; automatic movement was refused.");
            }
        }
        else
        {
            SetWorldPoseIfDifferent(player.transform, authoredSpawn);
        }
        if (!Approximately(player.transform.localScale, Vector3.one))
        {
            player.transform.localScale = Vector3.one;
            PrefabUtility.RecordPrefabInstancePropertyModifications(
                player.transform);
            EditorUtility.SetDirty(player.transform);
        }

        foreach (Transform transform in
                 player.GetComponentsInChildren<Transform>(true))
        {
            if (GameObjectUtility.GetStaticEditorFlags(transform.gameObject) ==
                (StaticEditorFlags)0)
            {
                continue;
            }

            GameObjectUtility.SetStaticEditorFlags(
                transform.gameObject,
                0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(
                transform.gameObject);
            EditorUtility.SetDirty(transform.gameObject);
        }

        PrefabUtility.RecordPrefabInstancePropertyModifications(player);
        EditorUtility.SetDirty(player);
    }

    private static Dictionary<UnityEngine.Object, UnityEngine.Object>
        BuildHierarchyReplacementMap(GameObject oldRoot, GameObject newRoot)
    {
        var map = new Dictionary<UnityEngine.Object, UnityEngine.Object>
        {
            [oldRoot] = newRoot,
            [oldRoot.transform] = newRoot.transform
        };

        Component[] oldComponents = oldRoot
            .GetComponentsInChildren<Component>(true)
            .Where(component => component != null)
            .ToArray();
        Component[] newComponents = newRoot
            .GetComponentsInChildren<Component>(true)
            .Where(component => component != null)
            .ToArray();

        foreach (IGrouping<Type, Component> oldGroup in
                 oldComponents.GroupBy(component => component.GetType()))
        {
            Component[] oldMatches = oldGroup.ToArray();
            Component[] newMatches = newComponents
                .Where(component => component.GetType() == oldGroup.Key)
                .ToArray();

            if (oldMatches.Length != 1 || newMatches.Length != 1)
            {
                continue;
            }

            Component oldComponent = oldMatches[0];
            Component newComponent = newMatches[0];
            map[oldComponent] = newComponent;
            map.TryAdd(oldComponent.gameObject, newComponent.gameObject);
            map.TryAdd(oldComponent.transform, newComponent.transform);
        }

        return map;
    }

    private static void RewireExternalSceneReferences(
        Scene scene,
        Transform oldRoot,
        Transform newRoot,
        IReadOnlyDictionary<UnityEngine.Object, UnityEngine.Object>
            replacementMap,
        string authorityLabel)
    {
        foreach (Component component in scene.GetRootGameObjects()
                     .SelectMany(root =>
                         root.GetComponentsInChildren<Component>(true)))
        {
            if (component == null ||
                IsSceneHierarchySerialization(component) ||
                IsInHierarchy(component, oldRoot) ||
                IsInHierarchy(component, newRoot))
            {
                continue;
            }

            SerializedObject serialized = new(component);
            serialized.UpdateIfRequiredOrScript();
            SerializedProperty property = serialized.GetIterator();
            bool changed = false;

            while (property.Next(true))
            {
                if (property.propertyType !=
                    SerializedPropertyType.ObjectReference)
                {
                    continue;
                }

                UnityEngine.Object referenced = property.objectReferenceValue;
                if (referenced == null ||
                    !IsInHierarchy(referenced, oldRoot))
                {
                    continue;
                }

                if (!replacementMap.TryGetValue(
                        referenced,
                        out UnityEngine.Object replacement) ||
                    replacement == null)
                {
                    throw new InvalidOperationException(
                        $"Cannot replace {authorityLabel} because " +
                        $"'{component.GetType().Name}.{property.propertyPath}' " +
                        "references an old Player child with no exact " +
                        "Safety counterpart.");
                }

                property.objectReferenceValue = replacement;
                changed = true;
            }

            if (!changed)
            {
                continue;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            EditorUtility.SetDirty(component);
        }
    }

    private static bool IsSceneHierarchySerialization(Component component)
    {
        // Transform and RectTransform serialize Unity's m_Father/m_Children
        // ownership. Rewriting those object references creates duplicate or
        // stale hierarchy links; explicit SetParent/DestroyImmediate calls
        // already own every hierarchy transition in this authorer.
        return component is Transform;
    }

    internal static bool IsSceneHierarchySerializationForTests(
        Component component)
    {
        return IsSceneHierarchySerialization(component);
    }

    private static bool IsInHierarchy(
        UnityEngine.Object candidate,
        Transform root)
    {
        Transform candidateTransform = candidate switch
        {
            GameObject gameObject => gameObject.transform,
            Component component => component.transform,
            _ => null
        };

        return candidateTransform != null &&
               (candidateTransform == root ||
                candidateTransform.IsChildOf(root));
    }

    private static void SetWorldPoseIfDifferent(
        Transform target,
        Transform source)
    {
        if (target == null || source == null)
        {
            throw new InvalidOperationException(
                "Authored player placement requires non-null transforms.");
        }

        if (Approximately(target.position, source.position) &&
            Quaternion.Angle(target.rotation, source.rotation) <= 0.01f)
        {
            return;
        }

        target.SetPositionAndRotation(source.position, source.rotation);
        PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        EditorUtility.SetDirty(target);
    }

    private static void DisableNestedPlayerServiceBehaviours(
        Transform player)
    {
        if (player == null)
        {
            throw new InvalidOperationException(
                "The tagged Player is required before nested service " +
                "compatibility can be authored.");
        }

        DisableNestedPlayerBehaviours<playerController>(player);
        DisableNestedPlayerBehaviours<Interact>(player);
        DisableNestedPlayerBehaviours<Inventory>(player);
    }

    private static void DisableRecognizedFarmWorktableInputAuthority(
        Scene scene)
    {
        Transform player = RequireTaggedObject(scene, "Player").transform;
        Interact[] nonPlayerAuthorities = FindSceneComponents<Interact>(scene)
            .Where(interaction =>
                interaction != null &&
                interaction.enabled &&
                interaction.transform != player &&
                !interaction.transform.IsChildOf(player))
            .ToArray();

        if (nonPlayerAuthorities.Length == 0)
            return;

        if (nonPlayerAuthorities.Length != 1)
        {
            throw new InvalidOperationException(
                $"Scene '{scene.name}' contains " +
                $"{nonPlayerAuthorities.Length} non-player Interact input " +
                "authorities; only the exact Safety upgrade-worktable " +
                "migration is recognized.");
        }

        Interact legacyAuthority = nonPlayerAuthorities[0];
        GameObject instanceRoot =
            PrefabUtility.GetNearestPrefabInstanceRoot(
                legacyAuthority.gameObject);
        string prefabPath = instanceRoot != null
            ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instanceRoot)
            : string.Empty;
        var data = new SerializedObject(legacyAuthority);
        SerializedProperty range = data.FindProperty("InteractRange");
        SerializedProperty layer = data.FindProperty("InteractLayer");

        if (legacyAuthority.GetComponent<UpgradeTable>() == null ||
            instanceRoot != legacyAuthority.gameObject ||
            prefabPath != "Assets/PreFabs/Hub/worktable_01a_fbx.prefab" ||
            range == null || range.intValue != 0 ||
            layer == null || layer.intValue != (1 << 3))
        {
            throw new InvalidOperationException(
                $"Scene '{scene.name}' contains an unknown enabled non-player " +
                $"Interact authority on '{legacyAuthority.gameObject.name}'.");
        }

        legacyAuthority.enabled = false;
        PrefabUtility.RecordPrefabInstancePropertyModifications(
            legacyAuthority);
        EditorUtility.SetDirty(legacyAuthority);
    }

    private static void DisableNestedPlayerBehaviours<T>(Transform player)
        where T : Behaviour
    {
        foreach (T behaviour in player.GetComponentsInChildren<T>(true))
        {
            if (behaviour == null || behaviour.transform == player ||
                !behaviour.enabled)
            {
                continue;
            }

            behaviour.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour);
            EditorUtility.SetDirty(behaviour);
        }
    }

    private static T[] FindSceneComponents<T>(Scene scene)
        where T : Component
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true))
            .ToArray();
    }

    private static GameObject RequireTaggedObject(Scene scene, string tag)
    {
        GameObject[] matches = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(transform => transform.gameObject)
            .Where(gameObject => gameObject.CompareTag(tag))
            .ToArray();

        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"Scene '{scene.name}' requires exactly one object tagged " +
                $"'{tag}'; found {matches.Length}: " +
                string.Join(", ", matches.Select(match =>
                    $"'{match.name}' under '{match.transform.root.name}'")) +
                ".");
        }

        return matches[0];
    }

    private static GameObject RequireSingleNamedObject(Scene scene, string name)
    {
        GameObject[] matches = FindSceneNamedObjects(scene, name);

        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"Scene '{scene.name}' requires exactly one object named " +
                $"'{name}'; found {matches.Length}.");
        }

        return matches[0];
    }

    private static GameObject[] FindSceneNamedObjects(Scene scene, string name)
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(transform => transform.gameObject)
            .Where(gameObject => gameObject.name == name)
            .ToArray();
    }

    private static TMP_Text RequireNamedText(Scene scene, string name)
    {
        TMP_Text[] matches = FindSceneComponents<TMP_Text>(scene)
            .Where(text => text.gameObject.name == name)
            .ToArray();

        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"Scene '{scene.name}' requires one authored TMP text named " +
                $"'{name}'; found {matches.Length}.");
        }

        return matches[0];
    }

    private static int RequireInteractableLayer()
    {
        int layer = LayerMask.NameToLayer(InteractableLayerName);

        if (layer < 0)
        {
            throw new InvalidOperationException(
                $"Required layer '{InteractableLayerName}' is not configured.");
        }

        return layer;
    }

    private static int RequireSpawnVolumeLayer()
    {
        int borderLayer = LayerMask.NameToLayer(SpawnVolumeLayerName);
        int playerLayer = LayerMask.NameToLayer(PlayerLayerName);

        if (borderLayer < 0 || playerLayer < 0)
        {
            throw new InvalidOperationException(
                $"Required layers '{SpawnVolumeLayerName}' and " +
                $"'{PlayerLayerName}' must be configured.");
        }

        if (Physics.GetIgnoreLayerCollision(borderLayer, playerLayer))
        {
            throw new InvalidOperationException(
                $"Layer '{SpawnVolumeLayerName}' must collide with " +
                $"'{PlayerLayerName}' for the authored Farm spawn volume.");
        }

        return borderLayer;
    }

    private static void RequireAsset(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
        {
            throw new FileNotFoundException(
                $"Required scene asset is missing: {path}");
        }
    }

    private static T RequireAssetAtPath<T>(string path)
        where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);

        if (asset == null)
        {
            throw new FileNotFoundException(
                $"Required asset is missing or has the wrong type: {path}");
        }

        return asset;
    }

    private static T LoadRequiredExactAsset<T>(
        string path,
        string expectedGuid)
        where T : UnityEngine.Object
    {
        T asset = RequireAssetAtPath<T>(path);
        string actualGuid = AssetDatabase.AssetPathToGUID(path);
        if (!string.Equals(
                actualGuid,
                expectedGuid,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Required campaign asset '{path}' has GUID '{actualGuid}', " +
                $"expected '{expectedGuid}'.");
        }

        return asset;
    }

    private static void RequireValidLoadedScene(Scene scene, string path)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            throw new InvalidOperationException(
                $"Required validation scene is not loaded: {path}");
        }
    }

    private static string ToAbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                             ?? throw new InvalidOperationException(
                                 "Could not resolve Unity project root.");
        return Path.Combine(
            projectRoot,
            assetPath.Replace('/', Path.DirectorySeparatorChar));
    }
}
