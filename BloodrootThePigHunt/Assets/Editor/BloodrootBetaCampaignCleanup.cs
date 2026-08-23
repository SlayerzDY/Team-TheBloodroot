#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Bloodroot.Campaign;
using Bloodroot.Features.AlphaEnemies;
using Bloodroot.Features.Hub;
using Bloodroot.Features.WorldMissions;
using TMPro;
using UnityEditor;
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
        CampaignRootTreeOffering[] offerings =
            FindComponents<CampaignRootTreeOffering>(scene);
        foreach (CampaignRootTreeOffering offering in offerings)
        {
            offering.gameObject.name = "Cursed Object Offering";
            offering.Configure(
                state,
                carryover,
                RequirePrefab(PrologueCursedObjectPickupPath),
                null,
                Array.Empty<GameObject>());
            EditorUtility.SetDirty(offering);
        }

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
