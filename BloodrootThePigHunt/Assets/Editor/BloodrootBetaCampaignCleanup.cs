#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Bloodroot.Campaign;
using Bloodroot.Features.AlphaEnemies;
using Bloodroot.Features.FarmPrologue;
using Bloodroot.Features.Hub;
using Bloodroot.Features.WorldMissions;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Focused beta migration for campaign carryover, retired Farm presentation,
/// and the Open World's area-owned recurring threat population.
/// </summary>
public static class BloodrootBetaCampaignCleanup
{
    private const string FarmScenePath =
        "Assets/Scenes/Campaign/Farm_PrologueHub.unity";
    private const string OpenWorldScenePath =
        "Assets/Scenes/OpenWorld/Bloodroot_OpenWorld.unity";
    private const string MainMenuScenePath =
        "Assets/Scenes/Alpha/MainMenu.unity";

    private const string RiflePickupPath =
        "Assets/PreFabs/AlphaPlaceholders/CampaignInventoryTokens/M1_Garand_CampaignToken.prefab";
    private const string AmmoPickupPath =
        "Assets/PreFabs/AlphaPlaceholders/CampaignInventoryTokens/M1_Garand_Ammo_CampaignToken.prefab";
    private const string RadarPickupPath =
        "Assets/PreFabs/AlphaPlaceholders/CampaignInventoryTokens/Radar_CampaignToken.prefab";
    private const string CursedShardPickupPath =
        "Assets/PreFabs/AlphaPlaceholders/CampaignInventoryTokens/Cursed_Root_Shard_CampaignToken.prefab";
    private const string HeartrootPickupPath =
        "Assets/PreFabs/AlphaPlaceholders/CampaignInventoryTokens/Exposed_Heartroot_CampaignToken.prefab";
    private const string PrologueCursedObjectPickupPath =
        "Assets/PreFabs/AlphaPlaceholders/CampaignInventoryTokens/Cursed_Item_CampaignToken.prefab";
    private const string TruckKeyPickupPath =
        "Assets/PreFabs/Items/ItemPickups/Item Variants/Item_Key.prefab";
    private const string LeatherPickupPath =
        "Assets/PreFabs/Items/ItemPickups/Item Variants/Item_Leather.prefab";
    private const string BoxPickupPath =
        "Assets/PreFabs/Items/ItemPickups/Item Variants/Item_Box.prefab";
    private const string StickPickupPath =
        "Assets/PreFabs/Items/ItemPickups/Item Variants/Item_Branch.prefab";
    private const string IronOrePickupPath =
        "Assets/PreFabs/Items/ItemPickups/Item Variants/Item_IronOre.prefab";
    private const string ScrapMetalPickupPath =
        "Assets/PreFabs/Items/ItemPickups/Item Variants/Item_ScrapMetal.prefab";
    private const string StonePickupPath =
        "Assets/PreFabs/Items/ItemPickups/Item Variants/Item_Stone.prefab";
    private const string SafetyCursedItemPickupPath =
        "Assets/PreFabs/Items/ItemPickups/Item Variants/Item_CursedItem.prefab";
    private const string PrologueCursedObjectName =
        "Prologue Cursed Object Pickup";
    private const string PrologueCursedObjectPresentationName =
        "Protected Cursed Object Presentation";
    private static readonly Vector3 PrologueCursedObjectPosition =
        new Vector3(57.976902f, 0f, 9.5f);
    private static readonly Vector3 PrologueCursedObjectPresentationPosition =
        new Vector3(0f, 0.11514783f, 0f);
    private static readonly Vector3 PrologueCursedObjectPresentationScale =
        new Vector3(0.0985897f, 0.066987135f, 0.04837508f);

    private const string BoarPath = "Assets/PreFabs/Enemies/Boar.prefab";
    private const string RootBoarPath = "Assets/PreFabs/Enemies/BoarRoot.prefab";
    private const string ScreecherPath = "Assets/PreFabs/Enemies/Screecher.prefab";
    private const string JuggernautPath =
        "Assets/PreFabs/AlphaPlaceholders/Juggernaut_CampaignCompatible.prefab";
    private const string WereboarPath =
        "Assets/PreFabs/AlphaPlaceholders/WereBoar_PLACEHOLDER.prefab";
    private const string AmbientRootName = "__BR_AMBIENT_THREATS_V1";
    private const string AmbientMarkersName = "Markers";

    // These were temporary hub presentation systems.  The Farm remains a
    // playable hub through its existing truck, campaign state, and cursed
    // object interactions; it no longer exposes prototype stations or
    // floating world labels.
    private static readonly string[] RetiredFarmHubRootNames =
    {
        "Mission Board Station",
        "Loadout Station",
        "Storage Station",
        "Upgrade Station",
        "Investigation Station",
        "Mission Board",
        "Storage Area",
        "Upgrade Area",
        "Hub Decorations",
        "First Arrival Presentation",
        "SLOT_Replaceable_Station_Visual"
    };

    private sealed class AmbientAreaSpec
    {
        public AmbientAreaSpec(
            string missionRootName,
            int difficulty,
            int maximumAlive,
            bool suppressDuringWitchCombat,
            params AmbientSpawnSpec[] spawns)
        {
            MissionRootName = missionRootName;
            Difficulty = difficulty;
            MaximumAlive = maximumAlive;
            SuppressDuringWitchCombat = suppressDuringWitchCombat;
            Spawns = spawns;
        }

        public string MissionRootName { get; }
        public int Difficulty { get; }
        public int MaximumAlive { get; }
        public bool SuppressDuringWitchCombat { get; }
        public AmbientSpawnSpec[] Spawns { get; }
    }

    private readonly struct AmbientSpawnSpec
    {
        public AmbientSpawnSpec(string prefabId, Vector3 position)
        {
            PrefabId = prefabId;
            Position = position;
        }

        public string PrefabId { get; }
        public Vector3 Position { get; }
    }

    private static readonly AmbientAreaSpec[] AmbientAreas =
    {
        new AmbientAreaSpec(
            "Black Pines Mission Systems", 2, 5, false,
            new AmbientSpawnSpec("Boar", new Vector3(-364f, 4.09f, -121f)),
            new AmbientSpawnSpec("RootBoar", new Vector3(-330f, 4.26f, -151f)),
            new AmbientSpawnSpec("Screecher", new Vector3(-282f, 4.14f, -169f)),
            new AmbientSpawnSpec("Boar", new Vector3(-252f, 7.11f, -203f)),
            new AmbientSpawnSpec("Juggernaut", new Vector3(-220f, 6.05f, -169f))),
        new AmbientAreaSpec(
            "Stillwater Mission Systems", 3, 6, false,
            new AmbientSpawnSpec("Boar", new Vector3(397f, 4.83f, -548f)),
            new AmbientSpawnSpec("RootBoar", new Vector3(433f, 5.82f, -519f)),
            new AmbientSpawnSpec("Screecher", new Vector3(448f, 6.85f, -505f)),
            new AmbientSpawnSpec("Juggernaut", new Vector3(470f, 10.8f, -505f)),
            new AmbientSpawnSpec("Boar", new Vector3(441f, 5.89f, -480f)),
            new AmbientSpawnSpec("RootBoar", new Vector3(505f, 7.06f, -470f))),
        new AmbientAreaSpec(
            "Harrow Estate Mission Systems", 4, 6, false,
            new AmbientSpawnSpec("Boar", new Vector3(7f, 55.06f, 329f)),
            new AmbientSpawnSpec("Screecher", new Vector3(25f, 68.12f, 366f)),
            new AmbientSpawnSpec("RootBoar", new Vector3(44f, 68.12f, 380f)),
            new AmbientSpawnSpec("Wereboar", new Vector3(84.58f, 54.59f, 378.83f)),
            new AmbientSpawnSpec("Juggernaut", new Vector3(125f, 37.84f, 348f)),
            new AmbientSpawnSpec("Boar", new Vector3(145f, 34.39f, 340f))),
        new AmbientAreaSpec(
            "Bloodroot Hollow Boss Systems", 5, 4, true,
            new AmbientSpawnSpec("RootBoar", new Vector3(50f, 9f, 610f)),
            new AmbientSpawnSpec("Screecher", new Vector3(50f, 9.01f, 620f)),
            new AmbientSpawnSpec("Juggernaut", new Vector3(50f, 10.65f, 646f)))
    };

    [MenuItem("Tools/Bloodroot/Campaign/Apply Beta Campaign Cleanup")]
    public static void ApplyFromMenu()
    {
        ApplyBatch();
    }

    public static void ApplyBatch()
    {
        if (Application.isPlaying || EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            throw new InvalidOperationException(
                "Beta campaign cleanup requires an idle Unity Editor in Edit Mode.");
        }

        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            ApplySceneCatalog(MainMenuScenePath, cleanFarmHub: false);
            ApplySceneCatalog(FarmScenePath, cleanFarmHub: true);
            ApplyOpenWorld();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
        finally
        {
            // A batch-mode executeMethod can start with no loaded scene. Unity
            // cannot restore that empty setup after this tool has opened scenes.
            if (originalSetup != null && originalSetup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }
        }
    }

    [MenuItem("Tools/Bloodroot/Campaign/Repair Farm Prologue Cursed Object")]
    public static void RepairFarmPrologueCursedObjectFromMenu()
    {
        RepairFarmPrologueCursedObjectBatch();
    }

    /// <summary>
    /// Repairs only the authored Farm prologue pickup. This intentionally
    /// leaves the rest of the beta cleanup and the Open World untouched.
    /// </summary>
    public static void RepairFarmPrologueCursedObjectBatch()
    {
        if (Application.isPlaying || EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            throw new InvalidOperationException(
                "The Farm cursed-object repair requires an idle Unity Editor in Edit Mode.");
        }

        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
        bool restoreSetup = !Application.isBatchMode &&
                            originalSetup != null &&
                            originalSetup.Length > 0;
        try
        {
            Scene scene = EditorSceneManager.OpenScene(
                FarmScenePath,
                OpenSceneMode.Single);
            int changes = EnsureFarmPrologueCursedObjectPickup(scene);
            ValidateFarmPrologueCursedObjectPickup(scene);

            if (changes > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, FarmScenePath))
                {
                    throw new InvalidOperationException(
                        "Unity could not save the repaired Farm cursed object.");
                }
            }

            AssetDatabase.SaveAssets();
            Console.WriteLine(
                "BLOODROOT_FARM_CURSED_OBJECT_REPAIR: PASS changes=" +
                changes + ".");
        }
        finally
        {
            if (restoreSetup)
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }
        }
    }

    private static void ApplySceneCatalog(string scenePath, bool cleanFarmHub)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        CampaignInventoryCarryover[] carryovers = FindComponents<CampaignInventoryCarryover>(scene);
        if (carryovers.Length == 0)
        {
            throw new InvalidOperationException(
                "Campaign inventory carryover is missing from " + scenePath + ".");
        }

        CampaignInventoryItemBinding[] catalog = CreateCampaignCatalog();
        CampaignInventoryItemBinding[] supplemental = CreateSafetyCatalog();
        GameObject truckKeyPickup = RequirePrefab(TruckKeyPickupPath);
        foreach (CampaignInventoryCarryover carryover in carryovers)
        {
            carryover.Configure(catalog, supplemental, truckKeyPickup);
            if (!carryover.ValidateStableInventoryCatalog(out string error))
            {
                throw new InvalidOperationException(
                    "Campaign carryover catalog is invalid in " + scenePath +
                    ": " + error);
            }
            EditorUtility.SetDirty(carryover);
        }

        if (cleanFarmHub)
        {
            CleanFarmHub(scene);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, scenePath))
        {
            throw new InvalidOperationException("Unity could not save " + scenePath + ".");
        }
    }

    private static void CleanFarmHub(Scene scene)
    {
        CampaignStateService state = FindSingleComponent<CampaignStateService>(scene);
        CampaignInventoryCarryover carryover =
            FindSingleComponent<CampaignInventoryCarryover>(scene);
        global::Inventory playerInventory =
            FindSingleComponent<global::Inventory>(scene);
        CampaignRootTreeOffering[] offerings =
            FindComponents<CampaignRootTreeOffering>(scene);
        foreach (CampaignRootTreeOffering offering in offerings)
        {
            offering.gameObject.name = "Cursed Object Offering";
            offering.Configure(
                state,
                carryover,
                RequirePrefab(PrologueCursedObjectPickupPath),
                playerInventory,
                Array.Empty<GameObject>());
            EditorUtility.SetDirty(offering);
        }

        EnsureFarmPrologueCursedObjectPickup(scene);

        foreach (WorldMissionEvidenceSource source in
                 FindComponents<WorldMissionEvidenceSource>(scene))
        {
            SerializedObject serialized = new SerializedObject(source);
            SerializedProperty retiredId = serialized.FindProperty("nameStoneId");
            SerializedProperty retiredPickup =
                serialized.FindProperty("nameStonePickupObject");
            SerializedProperty retiredInventory = serialized.FindProperty("inventory");
            if (retiredId != null)
            {
                retiredId.stringValue = string.Empty;
            }

            if (retiredPickup != null)
            {
                retiredPickup.objectReferenceValue = null;
            }

            if (retiredInventory != null)
            {
                retiredInventory.objectReferenceValue = null;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        RetireFarmHubPresentation(scene);

        var retiredRoots = new List<GameObject>();
        foreach (Transform candidate in FindTransforms(scene))
        {
            if (candidate == null || candidate.gameObject == null ||
                IsDescendantOfAny(candidate, retiredRoots) ||
                !IsRetiredFarmVisual(candidate.gameObject.name))
            {
                continue;
            }

            retiredRoots.Add(candidate.gameObject);
        }

        foreach (GameObject retiredRoot in retiredRoots)
        {
            UnityEngine.Object.DestroyImmediate(retiredRoot);
        }

        ValidateFarmHubCleanup(scene);
    }

    private static int EnsureFarmPrologueCursedObjectPickup(Scene scene)
    {
        FarmPrologueCursedObjectPickup[] pickups =
            FindComponents<FarmPrologueCursedObjectPickup>(scene);
        if (pickups.Length > 1)
        {
            throw new InvalidOperationException(
                "Farm prologue repair found duplicate campaign cursed-object " +
                "proxies: " + pickups.Length + ".");
        }

        CampaignStateService state = FindSingleComponent<CampaignStateService>(scene);
        CampaignInventoryCarryover carryover =
            FindSingleComponent<CampaignInventoryCarryover>(scene);
        FarmPrologueDirector director =
            FindSingleComponent<FarmPrologueDirector>(scene);
        global::Inventory playerInventory =
            FindSingleComponent<global::Inventory>(scene);
        GameObject token = RequirePrefab(PrologueCursedObjectPickupPath);
        GameObject safetyPresentation = RequirePrefab(SafetyCursedItemPickupPath);
        int changes = 0;
        FarmPrologueCursedObjectPickup pickup;
        if (pickups.Length == 0)
        {
            GameObject parent = FindGameObject(
                scene,
                "Generated Alpha Shared Farm Systems");
            if (parent == null)
            {
                throw new InvalidOperationException(
                    "Farm prologue repair cannot find its shared systems root.");
            }

            GameObject createdProxy = new GameObject(PrologueCursedObjectName);
            if (createdProxy.scene != scene)
            {
                SceneManager.MoveGameObjectToScene(createdProxy, scene);
            }
            createdProxy.transform.SetParent(parent.transform, false);
            createdProxy.AddComponent<BoxCollider>();
            pickup = createdProxy.AddComponent<FarmPrologueCursedObjectPickup>();

            FarmObjectivePresenter presenter =
                FindSingleComponent<FarmObjectivePresenter>(scene);
            UnityEventTools.AddPersistentListener(
                pickup.PickupRejectedEvent,
                presenter.ShowRejectedStatus);
            changes++;
        }
        else
        {
            pickup = pickups[0];
        }

        changes += RemoveStandaloneSafetyCursedItemPickups(
            scene,
            safetyPresentation);

        GameObject proxy = pickup.gameObject;
        if (!string.Equals(proxy.name, PrologueCursedObjectName,
                StringComparison.Ordinal))
        {
            proxy.name = PrologueCursedObjectName;
            changes++;
        }

        if (!proxy.activeSelf)
        {
            proxy.SetActive(true);
            changes++;
        }

        if (proxy.layer != 10)
        {
            proxy.layer = 10;
            changes++;
        }

        if (!proxy.CompareTag("Interact"))
        {
            proxy.tag = "Interact";
            changes++;
        }

        if ((pickup.transform.position - PrologueCursedObjectPosition)
            .sqrMagnitude > 0.000001f)
        {
            pickup.transform.position = PrologueCursedObjectPosition;
            changes++;
        }

        BoxCollider interactionCollider = proxy.GetComponent<BoxCollider>();
        if (interactionCollider == null)
        {
            throw new InvalidOperationException(
                "The Farm campaign cursed-object proxy is missing its BoxCollider.");
        }

        if (interactionCollider.isTrigger)
        {
            interactionCollider.isTrigger = false;
            changes++;
        }

        changes += SetVector3(
            interactionCollider.center,
            new Vector3(0f, 0.75f, 0f),
            value => interactionCollider.center = value);
        changes += SetVector3(
            interactionCollider.size,
            new Vector3(1.25f, 1.5f, 1.25f),
            value => interactionCollider.size = value);

        GameObject presentation = pickup.PresentationRoot;
        if (!IsExactPrefabInstance(presentation, safetyPresentation))
        {
            if (presentation != null)
            {
                if (!presentation.transform.IsChildOf(pickup.transform))
                {
                    throw new InvalidOperationException(
                        "The Farm cursed-object proxy references presentation " +
                        "outside its owned hierarchy.");
                }

                UnityEngine.Object.DestroyImmediate(presentation);
            }

            presentation = PrefabUtility.InstantiatePrefab(
                safetyPresentation,
                scene) as GameObject;
            if (presentation == null)
            {
                throw new InvalidOperationException(
                    "Unity could not instantiate Safety's cursed-item presentation.");
            }

            presentation.transform.SetParent(pickup.transform, false);
            presentation.transform.localPosition =
                PrologueCursedObjectPresentationPosition;
            presentation.transform.localRotation = Quaternion.identity;
            presentation.transform.localScale =
                PrologueCursedObjectPresentationScale;
            changes++;
        }

        if (!string.Equals(
                presentation.name,
                PrologueCursedObjectPresentationName,
                StringComparison.Ordinal))
        {
            presentation.name = PrologueCursedObjectPresentationName;
            changes++;
        }

        if (presentation.transform.parent != pickup.transform)
        {
            presentation.transform.SetParent(pickup.transform, false);
            changes++;
        }

        changes += SetVector3(
            presentation.transform.localPosition,
            PrologueCursedObjectPresentationPosition,
            value => presentation.transform.localPosition = value);
        if (Quaternion.Angle(
                presentation.transform.localRotation,
                Quaternion.identity) > 0.001f)
        {
            presentation.transform.localRotation = Quaternion.identity;
            changes++;
        }

        changes += SetVector3(
            presentation.transform.localScale,
            PrologueCursedObjectPresentationScale,
            value => presentation.transform.localScale = value);

        foreach (Collider childCollider in
                 presentation.GetComponentsInChildren<Collider>(true))
        {
            if (childCollider != null && childCollider.enabled)
            {
                childCollider.enabled = false;
                changes++;
            }
        }

        foreach (MonoBehaviour behaviour in
                 presentation.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour != null && behaviour.enabled &&
                (behaviour is global::IInteract ||
                 behaviour is global::Dissolver))
            {
                behaviour.enabled = false;
                changes++;
            }
        }

        if (presentation.activeSelf)
        {
            presentation.SetActive(false);
            changes++;
        }

        SerializedObject serialized = new SerializedObject(pickup);
        bool requiresConfigure =
            serialized.FindProperty("campaignState")?.objectReferenceValue != state ||
            serialized.FindProperty("inventoryCarryover")?.objectReferenceValue != carryover ||
            serialized.FindProperty("cursedItemTemplate")?.objectReferenceValue != token ||
            serialized.FindProperty("playerInventory")?.objectReferenceValue != playerInventory ||
            serialized.FindProperty("prologueDirector")?.objectReferenceValue != director ||
            serialized.FindProperty("presentationRoot")?.objectReferenceValue != presentation ||
            serialized.FindProperty("interactionCollider")?.objectReferenceValue !=
            interactionCollider;
        if (requiresConfigure)
        {
            pickup.Configure(
                state,
                carryover,
                token,
                playerInventory,
                director,
                presentation,
                interactionCollider);
            changes++;
        }

        changes += EnsureFarmRootTreeOfferingInventory(
            scene,
            state,
            carryover,
            token,
            playerInventory);

        if (interactionCollider.enabled)
        {
            interactionCollider.enabled = false;
            changes++;
        }

        if (changes > 0)
        {
            EditorUtility.SetDirty(pickup);
            EditorUtility.SetDirty(interactionCollider);
            EditorUtility.SetDirty(presentation);
        }

        return changes;
    }

    private static int EnsureFarmRootTreeOfferingInventory(
        Scene scene,
        CampaignStateService state,
        CampaignInventoryCarryover carryover,
        GameObject cursedItemToken,
        global::Inventory playerInventory)
    {
        CampaignRootTreeOffering offering =
            FindSingleComponent<CampaignRootTreeOffering>(scene);
        SerializedObject serialized = new SerializedObject(offering);
        bool requiresConfigure =
            serialized.FindProperty("stateService")?.objectReferenceValue != state ||
            serialized.FindProperty("inventoryCarryover")?.objectReferenceValue != carryover ||
            serialized.FindProperty("cursedItemPickupObject")?.objectReferenceValue !=
            cursedItemToken ||
            serialized.FindProperty("playerInventory")?.objectReferenceValue !=
            playerInventory;
        if (!requiresConfigure)
        {
            return 0;
        }

        GameObject offeredVisual =
            offering.OfferedObjectVisuals != null &&
            offering.OfferedObjectVisuals.Count > 0
                ? offering.OfferedObjectVisuals[0]
                : null;
        offering.Configure(
            state,
            carryover,
            cursedItemToken,
            playerInventory,
            new[] { offeredVisual });
        EditorUtility.SetDirty(offering);
        return 1;
    }

    private static int RemoveStandaloneSafetyCursedItemPickups(
        Scene scene,
        GameObject safetyPresentationPrefab)
    {
        var roots = new HashSet<GameObject>();
        foreach (global::Item item in FindComponents<global::Item>(scene))
        {
            if (item == null || item.transform.parent != null ||
                !string.Equals(
                    item.item?.itemName?.Trim(),
                    "Cursed Item",
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (!IsKnownLegacyCursedItemPickup(
                    item.gameObject,
                    safetyPresentationPrefab))
            {
                throw new InvalidOperationException(
                    "Farm cursed-object repair found an unknown standalone " +
                    "Safety cursed-item pickup named '" + item.name +
                    "' at " + item.transform.position + ".");
            }

            roots.Add(item.gameObject);
        }

        foreach (GameObject root in roots)
        {
            UnityEngine.Object.DestroyImmediate(root);
        }

        return roots.Count;
    }

    private static bool IsKnownLegacyCursedItemPickup(
        GameObject candidate,
        GameObject safetyPresentationPrefab)
    {
        if (!IsExactPrefabInstance(candidate, safetyPresentationPrefab))
        {
            return false;
        }

        string[] expectedNames =
        {
            "Item_CursedItem",
            "Item_CursedItem (1)",
            "Item_CursedItem (2)",
            "Item_CursedItem (3)",
            "Item_CursedItem (4)"
        };
        if (Array.IndexOf(expectedNames, candidate.name) < 0)
        {
            return false;
        }

        Vector3 position = candidate.transform.position;
        return position.x >= 66.5f && position.x <= 68.5f &&
               position.y >= -0.25f && position.y <= 0.5f &&
               position.z >= -64.5f && position.z <= -62f;
    }

    private static void ValidateFarmPrologueCursedObjectPickup(Scene scene)
    {
        FarmPrologueCursedObjectPickup pickup =
            FindSingleComponent<FarmPrologueCursedObjectPickup>(scene);
        GameObject expectedToken = RequirePrefab(PrologueCursedObjectPickupPath);
        GameObject expectedPresentation = RequirePrefab(SafetyCursedItemPickupPath);
        CampaignStateService expectedState =
            FindSingleComponent<CampaignStateService>(scene);
        CampaignInventoryCarryover expectedCarryover =
            FindSingleComponent<CampaignInventoryCarryover>(scene);
        global::Inventory expectedInventory =
            FindSingleComponent<global::Inventory>(scene);
        FarmPrologueDirector expectedDirector =
            FindSingleComponent<FarmPrologueDirector>(scene);
        global::ItemStats tokenItem =
            CampaignInventoryTokenUtility.GetItemStats(expectedToken);
        SerializedObject pickupData = new SerializedObject(pickup);
        SerializedObject directorData = new SerializedObject(expectedDirector);

        if (pickup == null || !pickup.isActiveAndEnabled ||
            !pickup.gameObject.activeInHierarchy ||
            !pickup.HasExclusiveInteractionAuthority ||
            pickup.CursedItemTemplate != expectedToken ||
            pickupData.FindProperty("campaignState")?.objectReferenceValue !=
            expectedState ||
            pickupData.FindProperty("inventoryCarryover")?.objectReferenceValue !=
            expectedCarryover ||
            pickupData.FindProperty("playerInventory")?.objectReferenceValue !=
            expectedInventory ||
            pickupData.FindProperty("prologueDirector")?.objectReferenceValue !=
            expectedDirector ||
            directorData.FindProperty("playerInventory")?.objectReferenceValue !=
            expectedInventory ||
            tokenItem == null ||
            !string.Equals(tokenItem.itemName, "CursedItem", StringComparison.Ordinal) ||
            tokenItem.quantity != 1 || tokenItem.stackSize != 1 ||
            pickup.InteractionCollider == null ||
            pickup.InteractionCollider.enabled ||
            pickup.InteractionCollider.isTrigger ||
            pickup.PresentationRoot == null ||
            pickup.PresentationRoot.activeSelf ||
            !IsExactPrefabInstance(pickup.PresentationRoot, expectedPresentation))
        {
            throw new InvalidOperationException(
                "The Farm campaign cursed-object proxy is not authored safely.");
        }

        CampaignRootTreeOffering offering =
            FindSingleComponent<CampaignRootTreeOffering>(scene);
        SerializedObject offeringData = new SerializedObject(offering);
        if (offeringData.FindProperty("stateService")?.objectReferenceValue !=
                expectedState ||
            offeringData.FindProperty("inventoryCarryover")?.objectReferenceValue !=
                expectedCarryover ||
            offeringData.FindProperty("cursedItemPickupObject")?.objectReferenceValue !=
                expectedToken ||
            offeringData.FindProperty("playerInventory")?.objectReferenceValue !=
                expectedInventory)
        {
            throw new InvalidOperationException(
                "The Farm Root Tree offering is not wired to the live player inventory.");
        }

        if (pickup.PickupRejectedEvent == null ||
            pickup.PickupRejectedEvent.GetPersistentEventCount() == 0)
        {
            throw new InvalidOperationException(
                "The Farm campaign cursed-object proxy lost its rejection feedback wiring.");
        }

        global::Dissolver[] dissolvers = pickup.PresentationRoot
            .GetComponentsInChildren<global::Dissolver>(true);
        if (dissolvers.Length != 1 || dissolvers[0] == null ||
            dissolvers[0].enabled)
        {
            throw new InvalidOperationException(
                "The Farm cursed-object presentation requires exactly one " +
                "authored, initially disabled Dissolver.");
        }

        foreach (Collider childCollider in pickup.PresentationRoot
                     .GetComponentsInChildren<Collider>(true))
        {
            if (childCollider != null && childCollider.enabled)
            {
                throw new InvalidOperationException(
                    "The protected cursed-object presentation has an enabled collider.");
            }
        }

        foreach (MonoBehaviour behaviour in pickup.PresentationRoot
                     .GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour != null && behaviour.enabled &&
                behaviour is global::IInteract)
            {
                throw new InvalidOperationException(
                    "The protected cursed-object presentation can bypass its " +
                    "campaign interaction proxy.");
            }
        }

        foreach (global::Item item in FindComponents<global::Item>(scene))
        {
            if (item != null && item.transform.parent == null &&
                string.Equals(
                    item.item?.itemName?.Trim(),
                    "Cursed Item",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "A standalone Safety cursed-item pickup remains in the Farm scene.");
            }
        }
    }

    private static bool IsExactPrefabInstance(
        GameObject instance,
        GameObject expectedPrefab)
    {
        if (instance == null || expectedPrefab == null ||
            !PrefabUtility.IsPartOfPrefabInstance(instance) ||
            PrefabUtility.GetNearestPrefabInstanceRoot(instance) != instance)
        {
            return false;
        }

        string expectedPath = AssetDatabase.GetAssetPath(expectedPrefab);
        string instancePath =
            PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance);
        return string.Equals(
            instancePath,
            expectedPath,
            StringComparison.Ordinal);
    }

    private static int SetVector3(
        Vector3 current,
        Vector3 expected,
        Action<Vector3> apply)
    {
        if ((current - expected).sqrMagnitude <= 0.000001f)
        {
            return 0;
        }

        apply(expected);
        return 1;
    }

    private static void RetireFarmHubPresentation(Scene scene)
    {
        // Keep the save-backed arrival acknowledgement, but make it entirely
        // non-visual.  This preserves the campaign state transition without
        // restoring the old beacon or "stations are available" world text.
        foreach (HubArrivalDirector arrival in
                 FindComponents<HubArrivalDirector>(scene))
        {
            SerializedObject serialized = new SerializedObject(arrival);
            SerializedProperty presentationRoot = serialized.FindProperty(
                "firstArrivalPresentationRoot");
            SerializedProperty autoCompleteSeconds = serialized.FindProperty(
                "autoCompleteSeconds");

            if (presentationRoot != null)
            {
                presentationRoot.objectReferenceValue = null;
            }

            if (autoCompleteSeconds != null)
            {
                // HubArrivalDirector intentionally clamps the actual wait to
                // one second, so this remains a brief silent state handoff.
                autoCompleteSeconds.floatValue = 0.1f;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(arrival);
        }

        var presentationRoots = new List<GameObject>();
        foreach (Transform candidate in FindTransforms(scene))
        {
            if (candidate == null || candidate.gameObject == null ||
                IsDescendantOfAny(candidate, presentationRoots) ||
                !IsRetiredFarmHubPresentation(candidate.gameObject.name))
            {
                continue;
            }

            presentationRoots.Add(candidate.gameObject);
        }

        foreach (GameObject root in presentationRoots)
        {
            UnityEngine.Object.DestroyImmediate(root);
        }

        // Station functionality was prototype-only.  Remove the authored
        // components as well as their meshes/colliders so no invisible
        // interaction volume can survive after its presentation is gone.
        DestroyComponents<HubStationProgression>(scene);
        DestroyComponents<HubStationInteractable>(scene);
        DestroyComponents<HubStationStatusPresenter>(scene);
        DestroyComponents<HubLoadoutStation>(scene);
        DestroyComponents<HubLoadoutFeedbackPresenter>(scene);
        DestroyComponents<HubInvestigationBoard>(scene);

        // TextMeshPro (not TextMeshProUGUI) is the authored world-space text
        // type.  The Farm HUD remains intact; every floating label is retired.
        TextMeshPro[] floatingTexts = FindComponents<TextMeshPro>(scene);
        foreach (TextMeshPro floatingText in floatingTexts)
        {
            if (floatingText != null)
            {
                UnityEngine.Object.DestroyImmediate(floatingText.gameObject);
            }
        }
    }

    private static bool IsRetiredFarmHubPresentation(string objectName)
    {
        foreach (string retiredName in RetiredFarmHubRootNames)
        {
            if (string.Equals(
                    objectName,
                    retiredName,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void DestroyComponents<T>(Scene scene) where T : Component
    {
        T[] components = FindComponents<T>(scene);
        foreach (T component in components)
        {
            if (component != null)
            {
                UnityEngine.Object.DestroyImmediate(component);
            }
        }
    }

    private static void ValidateFarmHubCleanup(Scene scene)
    {
        if (FindComponents<TextMeshPro>(scene).Length != 0)
        {
            throw new InvalidOperationException(
                "Farm Hub cleanup left floating TextMeshPro presentation behind.");
        }

        if (FindComponents<HubStationProgression>(scene).Length != 0 ||
            FindComponents<HubStationInteractable>(scene).Length != 0 ||
            FindComponents<HubStationStatusPresenter>(scene).Length != 0 ||
            FindComponents<HubLoadoutStation>(scene).Length != 0 ||
            FindComponents<HubLoadoutFeedbackPresenter>(scene).Length != 0 ||
            FindComponents<HubInvestigationBoard>(scene).Length != 0)
        {
            throw new InvalidOperationException(
                "Farm Hub cleanup left a retired station component behind.");
        }

        foreach (Transform candidate in FindTransforms(scene))
        {
            if (candidate != null &&
                IsRetiredFarmHubPresentation(candidate.gameObject.name))
            {
                throw new InvalidOperationException(
                    "Farm Hub cleanup left retired presentation root '" +
                    candidate.gameObject.name + "' behind.");
            }
        }

        if (FindComponents<CampaignSceneTravel>(scene).Length == 0 ||
            FindComponents<CampaignSpawnPoint>(scene).Length == 0 ||
            FindComponents<CampaignRootTreeOffering>(scene).Length == 0)
        {
            throw new InvalidOperationException(
                "Farm Hub cleanup removed a required travel, spawn, or cursed-object interaction.");
        }

        ValidateFarmPrologueCursedObjectPickup(scene);
    }

    private static void ApplyOpenWorld()
    {
        Scene scene = EditorSceneManager.OpenScene(
            OpenWorldScenePath,
            OpenSceneMode.Single);
        CampaignInventoryCarryover[] carryovers =
            FindComponents<CampaignInventoryCarryover>(scene);
        if (carryovers.Length == 0)
        {
            throw new InvalidOperationException(
                "Campaign inventory carryover is missing from the Open World.");
        }

        CampaignInventoryItemBinding[] catalog = CreateCampaignCatalog();
        CampaignInventoryItemBinding[] supplemental = CreateSafetyCatalog();
        GameObject truckKeyPickup = RequirePrefab(TruckKeyPickupPath);
        foreach (CampaignInventoryCarryover carryover in carryovers)
        {
            carryover.Configure(catalog, supplemental, truckKeyPickup);
            if (!carryover.ValidateStableInventoryCatalog(out string error))
            {
                throw new InvalidOperationException(
                    "Campaign carryover catalog is invalid in the Open World: " +
                    error);
            }
            EditorUtility.SetDirty(carryover);
        }

        var prefabs = new Dictionary<string, GameObject>(StringComparer.Ordinal)
        {
            ["Boar"] = RequirePrefab(BoarPath),
            ["RootBoar"] = RequirePrefab(RootBoarPath),
            ["Screecher"] = RequirePrefab(ScreecherPath),
            ["Juggernaut"] = RequirePrefab(JuggernautPath),
            ["Wereboar"] = RequirePrefab(WereboarPath)
        };

        foreach (AmbientAreaSpec area in AmbientAreas)
        {
            GameObject missionRoot = FindGameObject(scene, area.MissionRootName);
            if (missionRoot == null)
            {
                throw new InvalidOperationException(
                    "Open World mission root is missing: " + area.MissionRootName + ".");
            }

            BuildAmbientArea(missionRoot, area, prefabs);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, OpenWorldScenePath))
        {
            throw new InvalidOperationException("Unity could not save the Open World scene.");
        }
    }

    private static void BuildAmbientArea(
        GameObject missionRoot,
        AmbientAreaSpec spec,
        IReadOnlyDictionary<string, GameObject> prefabs)
    {
        Transform authority = FindDirectChild(missionRoot.transform, AmbientRootName);
        if (authority == null)
        {
            authority = new GameObject(AmbientRootName).transform;
            authority.SetParent(missionRoot.transform, false);
        }

        Transform markers = FindDirectChild(authority, AmbientMarkersName);
        if (markers == null)
        {
            markers = new GameObject(AmbientMarkersName).transform;
            markers.SetParent(authority, false);
        }

        for (int index = markers.childCount - 1; index >= 0; index--)
        {
            UnityEngine.Object.DestroyImmediate(markers.GetChild(index).gameObject);
        }

        var definitions = new OpenWorldAmbientEnemySpawnDefinition[spec.Spawns.Length];
        for (int index = 0; index < spec.Spawns.Length; index++)
        {
            AmbientSpawnSpec spawn = spec.Spawns[index];
            if (!prefabs.TryGetValue(spawn.PrefabId, out GameObject prefab))
            {
                throw new InvalidOperationException(
                    "Unsupported ambient enemy: " + spawn.PrefabId + ".");
            }

            if (!CampaignSafetyEnemyRuntimeAdapter.TryValidatePrefab(
                    prefab,
                    out _,
                    out _,
                    out _,
                    out string prefabError))
            {
                throw new InvalidOperationException(
                    "Ambient enemy prefab '" + spawn.PrefabId +
                    "' is not campaign-safe: " + prefabError);
            }

            GameObject marker = new GameObject(
                "Ambient_" + (index + 1).ToString("00") + "_" + spawn.PrefabId);
            marker.transform.SetParent(markers, false);
            marker.transform.position = spawn.Position;
            ValidateNavMeshAnchor(marker.transform, prefab);
            definitions[index] = new OpenWorldAmbientEnemySpawnDefinition(
                prefab,
                marker.transform);
        }

        OpenWorldAmbientThreatSpawner spawner = authority.GetComponent<
            OpenWorldAmbientThreatSpawner>();
        if (spawner == null)
        {
            spawner = authority.gameObject.AddComponent<OpenWorldAmbientThreatSpawner>();
        }

        WitchEncounterDirector witchEncounter =
            spec.SuppressDuringWitchCombat
                ? missionRoot.GetComponentInChildren<WitchEncounterDirector>(true)
                : null;
        if (spec.SuppressDuringWitchCombat && witchEncounter == null)
        {
            throw new InvalidOperationException(
                "The Hollow ambient spawner requires its witch encounter director.");
        }

        spawner.Configure(
            authority,
            definitions,
            spec.Difficulty,
            spec.MaximumAlive,
            3f,
            1.5f,
            22f,
            15f,
            spec.SuppressDuringWitchCombat,
            witchEncounter);
        EditorUtility.SetDirty(spawner);
    }

    private static CampaignInventoryItemBinding[] CreateCampaignCatalog()
    {
        return new[]
        {
            CreateBinding("m1_garand", RiflePickupPath),
            CreateBinding("m1_garand_ammo", AmmoPickupPath),
            CreateBinding("radar", RadarPickupPath),
            CreateBinding("cursed_root_shard", CursedShardPickupPath),
            CreateBinding("car_key", TruckKeyPickupPath),
            CreateBinding("exposed_heartroot", HeartrootPickupPath)
        };
    }

    private static CampaignInventoryItemBinding[] CreateSafetyCatalog()
    {
        return new[]
        {
            CreateBinding("leather", LeatherPickupPath),
            CreateBinding("box", BoxPickupPath),
            CreateBinding("stick", StickPickupPath),
            CreateBinding("iron_ore", IronOrePickupPath),
            CreateBinding("scrap_metal", ScrapMetalPickupPath),
            CreateBinding("stone", StonePickupPath),
            CreateBinding("cursed_item", SafetyCursedItemPickupPath)
        };
    }

    private static CampaignInventoryItemBinding CreateBinding(
        string id,
        string prefabPath)
    {
        var binding = new CampaignInventoryItemBinding();
        binding.Configure(id, RequirePrefab(prefabPath));
        return binding;
    }

    private static GameObject RequirePrefab(string assetPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null)
        {
            throw new InvalidOperationException("Required prefab is missing: " + assetPath + ".");
        }

        return prefab;
    }

    private static void ValidateNavMeshAnchor(Transform marker, GameObject prefab)
    {
        NavMeshAgent agent = prefab.GetComponentInChildren<NavMeshAgent>(true);
        int areaMask = agent != null ? agent.areaMask & 1 : 0;
        if (areaMask == 0 || !NavMesh.SamplePosition(
                marker.position,
                out NavMeshHit sample,
                15f,
                areaMask))
        {
            throw new InvalidOperationException(
                "Ambient marker is not near the baked Walkable NavMesh: " +
                marker.name + ".");
        }

        // The marker is runtime spawn authority, not a visual guide. Keep it
        // on the actual polygon so a later terrain or collider edit cannot
        // leave the ambient population suspended above or below the world.
        marker.position = sample.position;
    }

    private static bool IsRetiredFarmVisual(string objectName)
    {
        string name = objectName ?? string.Empty;
        return name.IndexOf("name stone", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("namestone", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("name_stone", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("offered_stone", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsDescendantOfAny(
        Transform candidate,
        IEnumerable<GameObject> possibleAncestors)
    {
        for (Transform parent = candidate.parent;
             parent != null;
             parent = parent.parent)
        {
            foreach (GameObject possibleAncestor in possibleAncestors)
            {
                if (parent.gameObject == possibleAncestor)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static Transform FindDirectChild(Transform parent, string name)
    {
        for (int index = 0; index < parent.childCount; index++)
        {
            Transform child = parent.GetChild(index);
            if (string.Equals(child.name, name, StringComparison.Ordinal))
            {
                return child;
            }
        }

        return null;
    }

    private static GameObject FindGameObject(Scene scene, string name)
    {
        foreach (Transform candidate in FindTransforms(scene))
        {
            if (string.Equals(candidate.name, name, StringComparison.Ordinal))
            {
                return candidate.gameObject;
            }
        }

        return null;
    }

    private static T FindSingleComponent<T>(Scene scene) where T : Component
    {
        T[] components = FindComponents<T>(scene);
        if (components.Length != 1)
        {
            throw new InvalidOperationException(
                "Expected exactly one " + typeof(T).Name + " in " +
                scene.path + ", found " + components.Length + ".");
        }

        return components[0];
    }

    private static T[] FindComponents<T>(Scene scene) where T : Component
    {
        var components = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            components.AddRange(root.GetComponentsInChildren<T>(true));
        }

        return components.ToArray();
    }

    private static IEnumerable<Transform> FindTransforms(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                yield return transform;
            }
        }
    }
}
#endif
