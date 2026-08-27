#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Campaign-owned terrain and imported-architecture support overlay for the
/// continuous Open World scene. The safety-owned production TerrainData is a
/// read-only baseline; all authored height, hole, tree, and detail changes are written
/// to a derivative TerrainData with its own stable GUID.
///
/// This authorer intentionally owns only:
/// - the derivative TerrainData at <see cref="CampaignTerrainPath"/>;
/// - the scene hierarchy named <see cref="SupportRootName"/>;
/// - terrain references on the scene Terrain and TerrainCollider;
/// - the HAR_016 translation;
/// - removal of the three audited Harrow dressing rocks.
///
/// It does not edit mission, evidence, runtime, imported-prefab, production
/// terrain, or safety-owned Editor source files.
/// </summary>
public static class BloodrootOpenWorldAssetSupportSetup
{
    private const string ScenePath =
        "Assets/Scenes/OpenWorld/Bloodroot_OpenWorld.unity";

    private const string ProductionTerrainPath =
        "Assets/Scenes/OpenWorld/Data/Bloodroot_OpenWorld_Terrain_Production.asset";

    private const string CampaignTerrainPath =
        "Assets/Scenes/OpenWorld/Data/Bloodroot_OpenWorld_Terrain_CampaignSupport.asset";

    private const string SupportRootName =
        "__CAMPAIGN_OPEN_WORLD_ASSET_SUPPORT_V1";

    private const string MetadataName = "Metadata";
    private const string UndergroundSupportName = "MausoleumUndergroundSupport";
    private const string SchemaMarkerName = "SCHEMA__OPEN_WORLD_ASSET_SUPPORT__V1";
    private const string TransactionFolder =
        "Library/BloodrootOpenWorldAssetSupportTransactions";

    private const string MausoleumName = "HAR_015_MausoleumEntry_Playable";
    private const string CryptName = "HAR_016_CryptInterior_Playable";
    private const string MausoleumApproachName = "MausoleumGroundApproach_NAV";
    private const string MausoleumThresholdName = "MausoleumThresholdLanding_NAV";
    private const string MausoleumRampName =
        "MausoleumToCryptStairPhysicalRamp_1P40__PHYSICAL_RAMP__CATALOG_ROUTE";
    private const string MausoleumSocketName = "CryptEntrySocketLanding_NAV";
    private const string CryptSocketName = "CryptEntryConnector_NAV";
    private const string CryptMainFloorName = "CryptMainFloor_NAV";

    private const float HeightComparisonTolerance = 0.00004f;
    private const float WorldSupportTolerance = 0.08f;
    private const float ManorSupportTolerance = 0.16f;
    private const float TransformTolerance = 0.01f;
    private const float SocketTolerance = 0.05f;
    private const float RouteDatumTolerance = 0.03f;
    private const float ReachabilityClearanceTolerance = 0.03f;

    private static readonly Vector3 MausoleumExteriorDatumLocal =
        new Vector3(0f, 0.2f, -3f);

    private static readonly Vector3 MausoleumThresholdDatumLocal =
        new Vector3(0f, 0.2f, -1f);

    private static readonly Vector3 MausoleumSocketDatumLocal =
        new Vector3(0f, -2.25f, 4.5f);

    private static readonly Vector3 CryptEntryDatumLocal =
        new Vector3(0f, -2.25f, -4.5f);

    private static readonly Vector3 CryptFloorDatumLocal =
        new Vector3(0f, -2.25f, -2f);

    private static readonly SupportPadSpec[] SupportPads =
    {
        new SupportPadSpec(
            "Farmhouse",
            "FRM_001_Farmhouse_Playable",
            null,
            new Vector2(-286.5337f, -243.59091f),
            new Vector2(5f, 4f),
            new Vector2(4f, 4f),
            0f,
            WorldSupportTolerance,
            true),

        new SupportPadSpec(
            "FireTower",
            "BLK_001_FireTower_Playable",
            null,
            new Vector2(-287.19116f, 46.63011f),
            new Vector2(2.75f, 2.75f),
            new Vector2(3f, 3f),
            0f,
            WorldSupportTolerance,
            true),

        new SupportPadSpec(
            "RangerOutpost",
            "BLK_002_RangerOutpost_Playable",
            null,
            new Vector2(-131.70734f, -204.71668f),
            new Vector2(6f, 3.8f),
            new Vector2(4f, 4f),
            0f,
            WorldSupportTolerance,
            true),

        new SupportPadSpec(
            "SiltwaterTraversal",
            "SIL_002_003_004_SiltwaterTraversal_Playable",
            null,
            new Vector2(438.4552f, -486.14655f),
            new Vector2(12f, 14f),
            new Vector2(6f, 6f),
            0f,
            WorldSupportTolerance,
            true),

        // The original Stillwater pad supported only the warehouse half of
        // the traversal prefab. This second same-datum pad seats the grain
        // elevator, silo supports, and their connecting approach on terrain.
        new SupportPadSpec(
            "SiltwaterTraversalGrainComplex",
            "SIL_002_003_004_SiltwaterTraversal_Playable",
            null,
            new Vector2(443.9552f, -514.14655f),
            new Vector2(18.5f, 15f),
            new Vector2(6f, 6f),
            0f,
            WorldSupportTolerance,
            true),

        new SupportPadSpec(
            "SiltwaterInvestigation",
            "SIL_001_009_010_SiltwaterInvestigation_Playable",
            null,
            new Vector2(506.651f, -529.2732f),
            new Vector2(22f, 12.5f),
            new Vector2(8f, 8f),
            0f,
            WorldSupportTolerance,
            true),

        new SupportPadSpec(
            "HarrowGate",
            "HAR_003_EstateGate_Playable",
            null,
            new Vector2(15.697048f, 300.0433f),
            new Vector2(16f, 1.5f),
            new Vector2(8f, 8f),
            -45.591f,
            WorldSupportTolerance,
            true),

        new SupportPadSpec(
            "HarrowBarn",
            "HAR_006_PrizeHogBarn_Playable",
            null,
            new Vector2(9.202525f, 330.65866f),
            new Vector2(9f, 5.5f),
            new Vector2(8f, 8f),
            174.293f,
            WorldSupportTolerance,
            true),

        new SupportPadSpec(
            "HarrowManorUnion",
            "HAR_001_HarrowManorShell_Playable",
            null,
            new Vector2(34.16965f, 372.83224f),
            new Vector2(20f, 12f),
            new Vector2(12f, 12f),
            169.314f,
            ManorSupportTolerance,
            true),

        new SupportPadSpec(
            "HarrowCarriageHouse",
            "HAR005_CarriageHouse_Playable",
            null,
            new Vector2(128.46716f, 351.26535f),
            new Vector2(7f, 5.65f),
            new Vector2(7f, 7f),
            0f,
            WorldSupportTolerance,
            true),

        new SupportPadSpec(
            "MausoleumApproach",
            MausoleumName,
            null,
            new Vector2(77f, 379.10f),
            new Vector2(2.25f, 4f),
            new Vector2(5f, 6f),
            91.314f,
            WorldSupportTolerance,
            false),

        // Preserve rendered terrain above the aligned underground crypt. The
        // narrow entry corridor below remains a terrain hole and uses the
        // imported mausoleum ramp as its walk surface.
        new SupportPadSpec(
            "CryptRoof",
            CryptName,
            null,
            new Vector2(90.4163f, 378.8127f),
            new Vector2(7.25f, 5.25f),
            new Vector2(3f, 3f),
            91.883f,
            WorldSupportTolerance,
            false),

        new SupportPadSpec(
            "ThornVeilCorridor",
            "TV_ARCH",
            null,
            new Vector2(50.2624f, 547.1882f),
            new Vector2(3f, 1f),
            new Vector2(5f, 9f),
            0f,
            WorldSupportTolerance,
            true)
    };

    private static readonly string[] RemovedRockNames =
    {
        "ROCK_HE_058_084",
        "ROCK_HE_056_084",
        "ROCK_HE_061_082"
    };

    [MenuItem("Tools/Bloodroot/Open World/Build Campaign Asset Support")]
    public static void BuildMenu()
    {
        BuildOrRebuildBatch();
        EditorUtility.DisplayDialog(
            "Open World Asset Support",
            "Campaign terrain and imported-asset support validation passed.",
            "OK");
    }

    [MenuItem("Tools/Bloodroot/Open World/Validate Campaign Asset Support")]
    public static void ValidateMenu()
    {
        ValidateBatch();
        EditorUtility.DisplayDialog(
            "Open World Asset Support",
            "Campaign terrain and imported-asset support validation passed.",
            "OK");
    }

    public static void BuildOrRebuildBatch()
    {
        if (Application.isPlaying)
        {
            throw new InvalidOperationException(
                "Open World asset support cannot be authored during Play Mode.");
        }

        RequireAssetFile(ScenePath);
        RequireAssetFile(ProductionTerrainPath);
        RequireTargetSceneClosedForBuild();

        ImmutableAssetGuard productionGuard =
            new ImmutableAssetGuard(ProductionTerrainPath);

        List<string> currentProblems = ValidateFromDisk();
        if (currentProblems.Count == 0)
        {
            productionGuard.AssertUnchanged();
            Debug.Log("BLOODROOT_OPEN_WORLD_ASSET_SUPPORT_BUILD=NOOP_VALIDATED");
            return;
        }

        FileTransaction transaction = null;
        Scene openedScene = default;
        bool sceneIsOpen = false;

        try
        {
            transaction = new FileTransaction(
                ScenePath,
                ScenePath + ".meta",
                CampaignTerrainPath,
                CampaignTerrainPath + ".meta");

            TerrainData production =
                RequireTerrainData(ProductionTerrainPath, "production");

            TerrainData campaign = CreateOrLoadCampaignTerrain(production);
            SynchronizeCampaignTerrainBaseline(production, campaign);

            openedScene = EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Additive);
            sceneIsOpen = true;

            Terrain terrain = RequireSingleTerrain(openedScene);
            TerrainCollider terrainCollider =
                RequireTerrainCollider(terrain);

            RepointTerrain(terrain, terrainCollider, campaign);

            // Socket-align the underground crypt before resolving terrain
            // support datums. CryptRoof derives its target height from the
            // aligned crypt root, so resolving pads first would author one
            // heightmap and validate against a different one later in this
            // same transaction.
            AlignCrypt(openedScene);

            ResolvedPad[] resolvedPads =
                ResolvePads(openedScene);
            HoleSpec[] resolvedHoles = ResolveHoleSpecs(openedScene);

            float[,] expectedHeights = BuildExpectedHeights(
                production,
                terrain.transform.position,
                resolvedPads);

            bool[,] expectedHoles = BuildExpectedHoles(
                production,
                terrain.transform.position,
                resolvedHoles);

            TreeInstance[] expectedTrees = BuildExpectedTrees(
                production,
                terrain.transform.position,
                resolvedPads,
                resolvedHoles);

            campaign.SetHeights(0, 0, expectedHeights);
            campaign.SetHoles(0, 0, expectedHoles);
            campaign.SetTreeInstances(expectedTrees, false);
            bool[,] detailClearMask = BuildStillwaterDetailClearMask(
                production, terrain.transform.position, openedScene);
            if (production.detailWidth > 0 && production.detailHeight > 0)
            {
                for (int layer = 0; layer < production.detailPrototypes.Length; layer++)
                {
                    campaign.SetDetailLayer(0, 0, layer,
                        BuildExpectedDetailLayer(production, layer, detailClearMask));
                }
            }
            EditorUtility.SetDirty(campaign);
            AssetDatabase.SaveAssetIfDirty(campaign);

            RemoveIntersectingRocks(openedScene);
            ReconcileSupportHierarchy(
                openedScene,
                terrain.gameObject.layer,
                production,
                campaign);

            EditorSceneManager.MarkSceneDirty(openedScene);
            if (!EditorSceneManager.SaveScene(openedScene, ScenePath))
            {
                throw new IOException(
                    "Unity could not save the Open World scene after asset support authoring.");
            }

            productionGuard.AssertUnchanged();

            List<string> builtProblems = ValidateSceneAndAssets(
                openedScene,
                production,
                campaign);

            if (builtProblems.Count > 0)
            {
                throw new InvalidOperationException(
                    "Authored Open World asset support failed semantic validation:\n\n- " +
                    string.Join("\n- ", builtProblems));
            }

            transaction.Commit();
            Debug.Log("BLOODROOT_OPEN_WORLD_ASSET_SUPPORT_BUILD=PASS");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw new InvalidOperationException(
                "Open World asset support authoring failed. The scene and campaign " +
                "TerrainData transaction was rolled back.",
                exception);
        }
        finally
        {
            if (sceneIsOpen && openedScene.IsValid() && openedScene.isLoaded)
            {
                EditorSceneManager.CloseScene(openedScene, true);
            }

            if (transaction != null)
            {
                transaction.Dispose();
            }

            productionGuard.AssertUnchanged();
        }
    }

    public static void ValidateBatch()
    {
        if (Application.isPlaying)
        {
            throw new InvalidOperationException(
                "Open World asset support validation cannot run during Play Mode.");
        }

        List<string> problems = ValidateFromDisk();
        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                "Open World asset support validation failed:\n\n- " +
                string.Join("\n- ", problems));
        }

        Debug.Log("BLOODROOT_OPEN_WORLD_ASSET_SUPPORT_VALIDATION=PASS");
    }

    private static List<string> ValidateFromDisk()
    {
        List<string> problems = new List<string>();

        if (!AssetFileExists(ScenePath))
        {
            problems.Add("The Open World scene asset is missing: " + ScenePath);
            return problems;
        }

        TerrainData production =
            AssetDatabase.LoadAssetAtPath<TerrainData>(ProductionTerrainPath);
        TerrainData campaign =
            AssetDatabase.LoadAssetAtPath<TerrainData>(CampaignTerrainPath);

        if (production == null)
        {
            problems.Add("The production TerrainData is missing: " + ProductionTerrainPath);
        }

        if (campaign == null)
        {
            problems.Add("The campaign TerrainData is missing: " + CampaignTerrainPath);
        }

        Scene loaded = SceneManager.GetSceneByPath(ScenePath);
        bool openedForValidation = !loaded.IsValid() || !loaded.isLoaded;
        Scene scene = loaded;

        try
        {
            if (openedForValidation)
            {
                scene = EditorSceneManager.OpenScene(
                    ScenePath,
                    OpenSceneMode.Additive);
            }

            if (production != null && campaign != null)
            {
                problems.AddRange(
                    ValidateSceneAndAssets(scene, production, campaign));
            }
            else
            {
                ValidateBasicSceneState(scene, campaign, problems);
            }
        }
        catch (Exception exception)
        {
            problems.Add("Validation could not inspect the Open World scene: " +
                         exception.Message);
        }
        finally
        {
            if (openedForValidation && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        return problems;
    }

    private static List<string> ValidateSceneAndAssets(
        Scene scene,
        TerrainData production,
        TerrainData campaign)
    {
        List<string> problems = new List<string>();

        if (production == campaign)
        {
            problems.Add(
                "Production and campaign terrain references resolve to the same object.");
            return problems;
        }

        if (AssetDatabase.GetAssetPath(production) != ProductionTerrainPath)
        {
            problems.Add("Production terrain resolved from an unexpected path.");
        }

        if (AssetDatabase.GetAssetPath(campaign) != CampaignTerrainPath)
        {
            problems.Add("Campaign terrain resolved from an unexpected path.");
        }

        Terrain terrain = TryGetSingleTerrain(scene, problems);
        if (terrain == null)
        {
            return problems;
        }

        TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
        if (collider == null)
        {
            problems.Add("The Open World Terrain has no TerrainCollider.");
        }

        if (terrain.terrainData != campaign)
        {
            problems.Add("The scene Terrain is not using the campaign TerrainData.");
        }

        if (collider != null && collider.terrainData != campaign)
        {
            problems.Add(
                "The scene TerrainCollider is not using the campaign TerrainData.");
        }

        ValidateTerrainShape(production, campaign, problems);

        ResolvedPad[] resolvedPads;
        try
        {
            resolvedPads = ResolvePads(scene);
        }
        catch (Exception exception)
        {
            problems.Add("Support-pad targets could not be resolved: " + exception.Message);
            return problems;
        }

        HoleSpec[] resolvedHoles;
        try
        {
            resolvedHoles = ResolveHoleSpecs(scene);
        }
        catch (Exception exception)
        {
            problems.Add(
                "Terrain-hole targets could not be resolved: " +
                exception.Message);
            return problems;
        }

        float[,] expectedHeights = BuildExpectedHeights(
            production,
            terrain.transform.position,
            resolvedPads);

        bool[,] expectedHoles = BuildExpectedHoles(
            production,
            terrain.transform.position,
            resolvedHoles);

        TreeInstance[] expectedTrees = BuildExpectedTrees(
            production,
            terrain.transform.position,
            resolvedPads,
            resolvedHoles);

        CompareHeights(campaign, expectedHeights, problems);
        CompareHoles(campaign, expectedHoles, problems);
        CompareTrees(campaign.treeInstances, expectedTrees, problems);
        CompareStillwaterDetails(
            scene, production, campaign, terrain.transform.position, problems);
        ValidateSupportDatums(campaign, terrain.transform.position, resolvedPads, problems);
        ValidateMausoleumHoleReachability(
            scene,
            production,
            terrain.transform.position,
            expectedHoles,
            resolvedHoles,
            problems);
        ValidateCrypt(scene, problems);
        ValidateRemovedRocks(scene, problems);
        ValidateSupportHierarchy(
            scene,
            terrain.gameObject.layer,
            production,
            campaign,
            problems);

        return problems;
    }

    private static void ValidateBasicSceneState(
        Scene scene,
        TerrainData campaign,
        List<string> problems)
    {
        Terrain terrain = TryGetSingleTerrain(scene, problems);
        if (terrain == null)
        {
            return;
        }

        if (campaign != null && terrain.terrainData != campaign)
        {
            problems.Add("The scene Terrain is not using the campaign TerrainData.");
        }

        ValidateCrypt(scene, problems);
        ValidateRemovedRocks(scene, problems);
    }

    private static TerrainData CreateOrLoadCampaignTerrain(
        TerrainData production)
    {
        TerrainData campaign =
            AssetDatabase.LoadAssetAtPath<TerrainData>(CampaignTerrainPath);

        if (campaign == null)
        {
            if (AssetFileExists(CampaignTerrainPath))
            {
                throw new InvalidOperationException(
                    "A non-TerrainData asset already exists at " + CampaignTerrainPath);
            }

            if (!AssetDatabase.CopyAsset(
                    ProductionTerrainPath,
                    CampaignTerrainPath))
            {
                throw new IOException(
                    "Unity could not clone the production TerrainData to " +
                    CampaignTerrainPath);
            }

            AssetDatabase.ImportAsset(
                CampaignTerrainPath,
                ImportAssetOptions.ForceSynchronousImport);

            campaign =
                AssetDatabase.LoadAssetAtPath<TerrainData>(CampaignTerrainPath);
        }

        if (campaign == null)
        {
            throw new InvalidOperationException(
                "The campaign TerrainData could not be loaded after creation.");
        }

        if (campaign == production)
        {
            throw new InvalidOperationException(
                "Refusing to author because the derivative resolves to the production TerrainData.");
        }

        return campaign;
    }

    private static void SynchronizeCampaignTerrainBaseline(
        TerrainData production,
        TerrainData campaign)
    {
        string expectedPath = AssetDatabase.GetAssetPath(campaign);
        if (expectedPath != CampaignTerrainPath)
        {
            throw new InvalidOperationException(
                "Campaign TerrainData path changed before baseline synchronization.");
        }

        // CopySerialized updates the existing derivative object and preserves
        // its asset/meta identity. The production object is never dirtied.
        EditorUtility.CopySerialized(production, campaign);
        campaign.name = "Bloodroot_OpenWorld_Terrain_CampaignSupport";
        EditorUtility.SetDirty(campaign);
        AssetDatabase.SaveAssetIfDirty(campaign);
    }

    private static void RepointTerrain(
        Terrain terrain,
        TerrainCollider collider,
        TerrainData campaign)
    {
        terrain.terrainData = campaign;
        collider.terrainData = campaign;
        EditorUtility.SetDirty(terrain);
        EditorUtility.SetDirty(collider);
    }

    private static float[,] BuildExpectedHeights(
        TerrainData production,
        Vector3 terrainPosition,
        ResolvedPad[] pads)
    {
        int resolution = production.heightmapResolution;
        float[,] heights = production.GetHeights(0, 0, resolution, resolution);

        for (int z = 0; z < resolution; z++)
        {
            float normalizedZ = z / (resolution - 1f);
            float worldZ = terrainPosition.z + normalizedZ * production.size.z;

            for (int x = 0; x < resolution; x++)
            {
                float normalizedX = x / (resolution - 1f);
                float worldX = terrainPosition.x + normalizedX * production.size.x;
                Vector2 world = new Vector2(worldX, worldZ);

                for (int p = 0; p < pads.Length; p++)
                {
                    float weight = pads[p].spec.Weight(world);
                    if (weight <= 0f)
                    {
                        continue;
                    }

                    float normalizedTarget = Mathf.Clamp01(
                        (pads[p].targetWorldY - terrainPosition.y) /
                        production.size.y);

                    heights[z, x] = Mathf.Lerp(
                        heights[z, x],
                        normalizedTarget,
                        weight);
                }
            }
        }

        return heights;
    }

    private static bool[,] BuildExpectedHoles(
        TerrainData production,
        Vector3 terrainPosition,
        HoleSpec[] holeSpecs)
    {
        int resolution = production.holesResolution;
        bool[,] holes = production.GetHoles(0, 0, resolution, resolution);
        Vector2 halfCellSize = new Vector2(
            production.size.x / resolution * 0.5f,
            production.size.z / resolution * 0.5f);

        for (int z = 0; z < resolution; z++)
        {
            float normalizedZ = (z + 0.5f) / resolution;
            float worldZ = terrainPosition.z + normalizedZ * production.size.z;

            for (int x = 0; x < resolution; x++)
            {
                float normalizedX = (x + 0.5f) / resolution;
                float worldX = terrainPosition.x + normalizedX * production.size.x;
                Vector2 world = new Vector2(worldX, worldZ);

                for (int h = 0; h < holeSpecs.Length; h++)
                {
                    if (holeSpecs[h].ContainsCell(world, halfCellSize))
                    {
                        // Unity hole masks use false for removed terrain.
                        holes[z, x] = false;
                        break;
                    }
                }
            }
        }

        return holes;
    }

    private static TreeInstance[] BuildExpectedTrees(
        TerrainData production,
        Vector3 terrainPosition,
        ResolvedPad[] pads,
        HoleSpec[] holeSpecs)
    {
        TreeInstance[] sourceTrees = production.treeInstances;
        List<TreeInstance> retained =
            new List<TreeInstance>(sourceTrees.Length);

        for (int i = 0; i < sourceTrees.Length; i++)
        {
            TreeInstance tree = sourceTrees[i];
            Vector2 world = new Vector2(
                terrainPosition.x + tree.position.x * production.size.x,
                terrainPosition.z + tree.position.z * production.size.z);

            if (IsInsideAnyModifiedRegion(world, pads, holeSpecs))
            {
                continue;
            }

            retained.Add(tree);
        }

        return retained.ToArray();
    }

    private static bool IsInsideAnyModifiedRegion(
        Vector2 world,
        ResolvedPad[] pads,
        HoleSpec[] holeSpecs)
    {
        for (int i = 0; i < pads.Length; i++)
        {
            if (pads[i].spec.Weight(world) > 0f)
            {
                return true;
            }
        }

        for (int i = 0; i < holeSpecs.Length; i++)
        {
            if (holeSpecs[i].Contains(world))
            {
                return true;
            }
        }

        return false;
    }

    private static bool[,] BuildStillwaterDetailClearMask(
        TerrainData production,
        Vector3 terrainPosition,
        Scene scene)
    {
        Transform traversal = RequireUniqueTransform(
            scene, "SIL_002_003_004_SiltwaterTraversal_Playable");
        Transform investigation = RequireUniqueTransform(
            scene, "SIL_001_009_010_SiltwaterInvestigation_Playable");
        HoleSpec[] footprints =
        {
            ResolveFloorFootprint(traversal, "COLLIDER_WarehouseFloor_24x16"),
            ResolveFloorFootprint(traversal,
                "COLLIDER_ElevatorFloorEast_0", "COLLIDER_ElevatorFloorRear_0"),
            ResolveFloorFootprint(investigation, "COLLIDER_MillGround_30x20"),
            ResolveFloorFootprint(investigation, "COLLIDER_LabGround_10x8"),
            ResolveFloorFootprint(investigation, "COLLIDER_VaultGround_4x4")
        };
        int width = production.detailWidth;
        int height = production.detailHeight;
        bool[,] mask = new bool[height, width];
        if (width == 0 || height == 0)
            return mask;

        Vector2 cellSize = new Vector2(
            production.size.x / width, production.size.z / height);
        for (int z = 0; z < height; z++)
        for (int x = 0; x < width; x++)
        {
            Vector2 center = new Vector2(
                terrainPosition.x + (x + 0.5f) * cellSize.x,
                terrainPosition.z + (z + 0.5f) * cellSize.y);
            // Details scatter anywhere inside their density cell. Clear cells
            // intersecting actual ground-floor footprints, never whole support
            // pads or feather regions; all non-overlapping cells stay intact.
            foreach (HoleSpec footprint in footprints)
            {
                if (!footprint.OverlapsCell(center, cellSize * 0.5f))
                    continue;
                mask[z, x] = true;
                break;
            }
        }
        return mask;
    }

    private static HoleSpec ResolveFloorFootprint(
        Transform assembly,
        params string[] floorNames)
    {
        if (Vector3.Dot(assembly.up, Vector3.up) < 0.9999f)
            throw new InvalidOperationException(
                "Stillwater detail clearance requires upright architecture: " + assembly.name);

        Bounds localBounds = new Bounds();
        bool hasBounds = false;
        foreach (string floorName in floorNames)
        {
            Transform floor = RequireUniqueDescendant(assembly, floorName);
            BoxCollider box = floor.GetComponent<BoxCollider>();
            if (box == null || !box.enabled || box.isTrigger ||
                !floor.gameObject.activeInHierarchy)
                throw new InvalidOperationException(
                    "Stillwater ground-floor collider is unavailable: " + floorName);

            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 corner = assembly.InverseTransformPoint(floor.TransformPoint(
                    box.center + Vector3.Scale(box.size * 0.5f, new Vector3(x, y, z))));
                if (hasBounds)
                    localBounds.Encapsulate(corner);
                else
                    localBounds = new Bounds(corner, Vector3.zero);
                hasBounds = true;
            }
        }

        Vector3 center = assembly.TransformPoint(localBounds.center);
        Vector3 scale = assembly.lossyScale;
        return new HoleSpec(assembly.name + "_GroundFloorDetails",
            new Vector2(center.x, center.z),
            new Vector2(localBounds.extents.x * Mathf.Abs(scale.x),
                localBounds.extents.z * Mathf.Abs(scale.z)),
            -assembly.eulerAngles.y);
    }

    private static int[,] BuildExpectedDetailLayer(
        TerrainData production,
        int layer,
        bool[,] clearMask)
    {
        int[,] density = production.GetDetailLayer(
            0, 0, production.detailWidth, production.detailHeight, layer);
        for (int z = 0; z < production.detailHeight; z++)
        for (int x = 0; x < production.detailWidth; x++)
            if (clearMask[z, x])
                density[z, x] = 0;
        return density;
    }

    private static void CompareStillwaterDetails(
        Scene scene,
        TerrainData production,
        TerrainData campaign,
        Vector3 terrainPosition,
        List<string> problems)
    {
        if (campaign.detailWidth != production.detailWidth ||
            campaign.detailHeight != production.detailHeight ||
            campaign.detailResolutionPerPatch != production.detailResolutionPerPatch ||
            !campaign.detailPrototypes.SequenceEqual(production.detailPrototypes))
        {
            problems.Add("Campaign terrain detail resolution or prototypes differ from production.");
            return;
        }
        if (production.detailWidth == 0 || production.detailHeight == 0)
            return;

        bool[,] clearMask = BuildStillwaterDetailClearMask(production, terrainPosition, scene);
        for (int layer = 0; layer < production.detailPrototypes.Length; layer++)
        {
            int[,] expected = BuildExpectedDetailLayer(production, layer, clearMask);
            int[,] actual = campaign.GetDetailLayer(
                0, 0, campaign.detailWidth, campaign.detailHeight, layer);
            int mismatches = 0;
            for (int z = 0; z < production.detailHeight; z++)
            for (int x = 0; x < production.detailWidth; x++)
                if (actual[z, x] != expected[z, x])
                    mismatches++;
            if (mismatches > 0)
                problems.Add("Campaign detail layer " + layer + " differs at " + mismatches +
                    " cells; Stillwater floors must be clear and all outside densities preserved.");
        }
    }

    private static ResolvedPad[] ResolvePads(Scene scene)
    {
        ResolvedPad[] resolved = new ResolvedPad[SupportPads.Length];

        for (int i = 0; i < SupportPads.Length; i++)
        {
            SupportPadSpec spec = SupportPads[i];
            Transform primary = RequireUniqueTransform(scene, spec.primaryTargetName);
            float targetY = primary.position.y;

            if (spec.id == "HarrowManorUnion")
            {
                spec = new SupportPadSpec(
                    spec.id,
                    spec.primaryTargetName,
                    spec.secondaryTargetName,
                    new Vector2(primary.position.x, primary.position.z),
                    spec.halfExtents,
                    spec.featherExtents,
                    primary.eulerAngles.y,
                    spec.semanticTolerance,
                    spec.validateDatum);
            }

            if (spec.id == "MausoleumApproach")
            {
                Vector3 center = primary.TransformPoint(
                    new Vector3(0f, 0f, -4f));
                spec = new SupportPadSpec(
                    spec.id,
                    spec.primaryTargetName,
                    spec.secondaryTargetName,
                    new Vector2(center.x, center.z),
                    spec.halfExtents,
                    spec.featherExtents,
                    -primary.eulerAngles.y,
                    spec.semanticTolerance,
                    spec.validateDatum);
            }

            if (spec.id == "CryptRoof")
            {
                spec = new SupportPadSpec(
                    spec.id,
                    spec.primaryTargetName,
                    spec.secondaryTargetName,
                    new Vector2(primary.position.x, primary.position.z),
                    spec.halfExtents,
                    spec.featherExtents,
                    -primary.eulerAngles.y,
                    spec.semanticTolerance,
                    spec.validateDatum);
            }

            if (!string.IsNullOrEmpty(spec.secondaryTargetName))
            {
                Transform secondary =
                    RequireUniqueTransform(scene, spec.secondaryTargetName);
                targetY = (targetY + secondary.position.y) * 0.5f;
            }

            if (spec.id == "CryptRoof")
            {
                targetY += 1f;
            }

            if (spec.id == "SiltwaterTraversal" ||
                spec.id == "SiltwaterTraversalGrainComplex" ||
                spec.id == "SiltwaterInvestigation")
            {
                // The modular slabs finish at the assembly's zero datum.
                // Recess soil slightly so it cannot render through the floors;
                // the slabs and footings remain embedded in supported terrain.
                targetY -= 0.04f;
            }

            resolved[i] = new ResolvedPad(spec, targetY);
        }

        return resolved;
    }

    private static HoleSpec[] ResolveHoleSpecs(Scene scene)
    {
        Transform mausoleum = RequireUniqueTransform(scene, MausoleumName);
        Vector3 center = mausoleum.TransformPoint(new Vector3(0f, 0f, 1f));
        return new[]
        {
            // Narrow descending corridor from the mausoleum threshold to its
            // authored crypt socket. The selection envelope adds only the
            // millimetre-scale lateral and decimetre-scale longitudinal margin
            // needed to select complete Terrain cells at this placement; the
            // exterior approach cell remains solid.
            new HoleSpec(
                "MausoleumRampCorridor",
                new Vector2(center.x, center.z),
                new Vector2(1.21f, 4.8f),
                -mausoleum.eulerAngles.y)
        };
    }

    private static void AlignCrypt(Scene scene)
    {
        Transform mausoleum = RequireUniqueTransform(scene, MausoleumName);
        Transform crypt = RequireUniqueTransform(scene, CryptName);
        Transform mausoleumSocket =
            RequireUniqueDescendant(mausoleum, MausoleumSocketName);
        Transform cryptSocket =
            RequireUniqueDescendant(crypt, CryptSocketName);

        Vector3 delta = mausoleumSocket.position - cryptSocket.position;
        crypt.position += delta;
    }

    private static void RemoveIntersectingRocks(Scene scene)
    {
        for (int i = 0; i < RemovedRockNames.Length; i++)
        {
            List<Transform> matches =
                FindSceneTransforms(scene, RemovedRockNames[i]);

            for (int m = matches.Count - 1; m >= 0; m--)
            {
                UnityEngine.Object.DestroyImmediate(matches[m].gameObject);
            }
        }
    }

    private static void ReconcileSupportHierarchy(
        Scene scene,
        int terrainLayer,
        TerrainData production,
        TerrainData campaign)
    {
        GameObject root = GetOrCreateSceneRoot(scene, SupportRootName);
        ConfigureTransform(root.transform, null, Vector3.zero, Quaternion.identity, Vector3.one);
        RemoveUnexpectedChildren(
            root.transform,
            new HashSet<string>(StringComparer.Ordinal)
            {
                MetadataName,
                UndergroundSupportName
            });

        Transform metadata = GetOrCreateChild(root.transform, MetadataName);
        ConfigureLocalTransform(metadata, Vector3.zero, Quaternion.identity, Vector3.one);

        string sourceGuid = AssetDatabase.AssetPathToGUID(ProductionTerrainPath);
        string campaignGuid = AssetDatabase.AssetPathToGUID(CampaignTerrainPath);
        string sourceHash = HashFile(ToAbsolutePath(ProductionTerrainPath));

        ReconcileMarkerChildren(
            metadata,
            new[]
            {
                SchemaMarkerName,
                "SOURCE_TERRAIN_GUID__" + sourceGuid,
                "SOURCE_TERRAIN_SHA256__" + sourceHash,
                "CAMPAIGN_TERRAIN_GUID__" + campaignGuid,
                "SOURCE_HEIGHT_RESOLUTION__" + production.heightmapResolution,
                "SOURCE_HOLES_RESOLUTION__" + production.holesResolution
            });

        Transform underground =
            GetOrCreateChild(root.transform, UndergroundSupportName);
        ConfigureLocalTransform(underground, Vector3.zero, Quaternion.identity, Vector3.one);

        Transform mausoleum = RequireUniqueTransform(scene, MausoleumName);
        Transform crypt = RequireUniqueTransform(scene, CryptName);

        BoxSupportSpec[] boxes = BuildBoxSupportSpecs(mausoleum, crypt);
        HashSet<string> expectedNames =
            new HashSet<string>(boxes.Select(box => box.name), StringComparer.Ordinal);

        RemoveUnexpectedChildren(underground, expectedNames);

        for (int i = 0; i < boxes.Length; i++)
        {
            ReconcileBoxSupport(underground, boxes[i], terrainLayer);
        }

    }

    private static BoxSupportSpec[] BuildBoxSupportSpecs(
        Transform mausoleum,
        Transform crypt)
    {
        return new[]
        {
            new BoxSupportSpec(
                "RampRetaining_Left",
                mausoleum.TransformPoint(new Vector3(-3.25f, -1f, 1.5f)),
                mausoleum.rotation,
                new Vector3(0.35f, 3.2f, 6f)),

            new BoxSupportSpec(
                "RampRetaining_Right",
                mausoleum.TransformPoint(new Vector3(3.25f, -1f, 1.5f)),
                mausoleum.rotation,
                new Vector3(0.35f, 3.2f, 6f))
        };
    }

    private static void ReconcileBoxSupport(
        Transform parent,
        BoxSupportSpec spec,
        int layer)
    {
        Transform child = GetOrCreateChild(parent, spec.name);
        ConfigureTransform(
            child,
            parent,
            spec.worldPosition,
            spec.worldRotation,
            Vector3.one);

        child.gameObject.layer = layer;
        child.gameObject.isStatic = true;

        Component[] components = child.GetComponents<Component>();
        BoxCollider box = child.GetComponent<BoxCollider>();
        if (box == null)
        {
            box = child.gameObject.AddComponent<BoxCollider>();
        }

        for (int i = components.Length - 1; i >= 0; i--)
        {
            Component component = components[i];
            if (component is Transform || component == box)
            {
                continue;
            }

            UnityEngine.Object.DestroyImmediate(component);
        }

        box.center = Vector3.zero;
        box.size = spec.size;
        box.isTrigger = false;
        box.enabled = true;
    }

    private static void ValidateTerrainShape(
        TerrainData production,
        TerrainData campaign,
        List<string> problems)
    {
        if (campaign.heightmapResolution != production.heightmapResolution)
        {
            problems.Add(
                "Campaign heightmap resolution differs from production.");
        }

        if (campaign.holesResolution != production.holesResolution)
        {
            problems.Add("Campaign holes resolution differs from production.");
        }

        if ((campaign.size - production.size).sqrMagnitude > 0.0001f)
        {
            problems.Add("Campaign terrain size differs from production.");
        }

        if (campaign.terrainLayers.Length != production.terrainLayers.Length)
        {
            problems.Add("Campaign terrain-layer count differs from production.");
        }

        if (campaign.treePrototypes.Length != production.treePrototypes.Length)
        {
            problems.Add("Campaign tree-prototype count differs from production.");
        }
    }

    private static void CompareHeights(
        TerrainData actualData,
        float[,] expected,
        List<string> problems)
    {
        int resolution = actualData.heightmapResolution;
        if (expected.GetLength(0) != resolution ||
            expected.GetLength(1) != resolution)
        {
            problems.Add("Expected height array does not match campaign resolution.");
            return;
        }

        float[,] actual = actualData.GetHeights(0, 0, resolution, resolution);
        float maximumDelta = 0f;
        int mismatchCount = 0;

        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float delta = Mathf.Abs(actual[z, x] - expected[z, x]);
                maximumDelta = Mathf.Max(maximumDelta, delta);
                if (delta > HeightComparisonTolerance)
                {
                    mismatchCount++;
                }
            }
        }

        if (mismatchCount > 0)
        {
            problems.Add(
                "Campaign heightmap differs from the deterministic support result at " +
                mismatchCount + " samples; maximum normalized delta " +
                maximumDelta.ToString("F7") + ".");
        }
    }

    private static void CompareHoles(
        TerrainData actualData,
        bool[,] expected,
        List<string> problems)
    {
        int resolution = actualData.holesResolution;
        if (expected.GetLength(0) != resolution ||
            expected.GetLength(1) != resolution)
        {
            problems.Add("Expected hole array does not match campaign resolution.");
            return;
        }

        bool[,] actual = actualData.GetHoles(0, 0, resolution, resolution);
        int mismatchCount = 0;

        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                if (actual[z, x] != expected[z, x])
                {
                    mismatchCount++;
                }
            }
        }

        if (mismatchCount > 0)
        {
            problems.Add(
                "Campaign terrain hole mask differs from the mausoleum/crypt " +
                "allowlist at " + mismatchCount + " samples.");
        }
    }

    private static void CompareTrees(
        TreeInstance[] actual,
        TreeInstance[] expected,
        List<string> problems)
    {
        if (actual.Length != expected.Length)
        {
            problems.Add(
                "Campaign tree count is " + actual.Length +
                ", expected " + expected.Length +
                " after deterministic support-region filtering.");
            return;
        }

        for (int i = 0; i < actual.Length; i++)
        {
            if (!TreeInstancesEqual(actual[i], expected[i]))
            {
                problems.Add(
                    "Campaign tree instance " + i +
                    " differs from the deterministic filtered production tree.");
                return;
            }
        }
    }

    private static bool TreeInstancesEqual(TreeInstance a, TreeInstance b)
    {
        return (a.position - b.position).sqrMagnitude <= 0.0000000001f &&
               Mathf.Abs(a.widthScale - b.widthScale) <= 0.00001f &&
               Mathf.Abs(a.heightScale - b.heightScale) <= 0.00001f &&
               Mathf.Abs(a.rotation - b.rotation) <= 0.00001f &&
               a.prototypeIndex == b.prototypeIndex &&
               a.color.Equals(b.color) &&
               a.lightmapColor.Equals(b.lightmapColor);
    }

    private static void ValidateSupportDatums(
        TerrainData campaign,
        Vector3 terrainPosition,
        ResolvedPad[] pads,
        List<string> problems)
    {
        for (int i = 0; i < pads.Length; i++)
        {
            ResolvedPad pad = pads[i];
            if (!pad.spec.validateDatum)
            {
                continue;
            }

            Vector2[] samples = pad.spec.DenseInnerSamples();
            float maximumError = 0f;

            for (int s = 0; s < samples.Length; s++)
            {
                float normalizedX =
                    (samples[s].x - terrainPosition.x) / campaign.size.x;
                float normalizedZ =
                    (samples[s].y - terrainPosition.z) / campaign.size.z;

                float worldHeight = terrainPosition.y +
                    campaign.GetInterpolatedHeight(normalizedX, normalizedZ);

                maximumError = Mathf.Max(
                    maximumError,
                    Mathf.Abs(worldHeight - pad.targetWorldY));
            }

            if (maximumError > pad.spec.semanticTolerance)
            {
                problems.Add(
                    pad.spec.id + " support surface differs from its authored datum by " +
                    maximumError.ToString("F3") + " m; tolerance is " +
                    pad.spec.semanticTolerance.ToString("F3") + " m.");
            }
        }
    }

    private static void ValidateMausoleumHoleReachability(
        Scene scene,
        TerrainData production,
        Vector3 terrainPosition,
        bool[,] expectedHoles,
        HoleSpec[] holeSpecs,
        List<string> problems)
    {
        Transform mausoleum;
        try
        {
            mausoleum = RequireUniqueTransform(scene, MausoleumName);
        }
        catch (Exception exception)
        {
            problems.Add(
                "The mausoleum terrain-hole contract could not resolve HAR_015: " +
                exception.Message);
            return;
        }

        if (holeSpecs.Length != 1)
        {
            problems.Add(
                "The mausoleum terrain-hole allowlist must contain exactly one corridor; " +
                "found " + holeSpecs.Length + ".");
            return;
        }

        HoleSpec corridor = holeSpecs[0];
        Vector3 expectedCenter3 =
            mausoleum.TransformPoint(new Vector3(0f, 0f, 1f));
        Vector2 expectedCenter = new Vector2(expectedCenter3.x, expectedCenter3.z);

        if (!string.Equals(
                corridor.id,
                "MausoleumRampCorridor",
                StringComparison.Ordinal) ||
            Vector2.Distance(corridor.center, expectedCenter) > TransformTolerance ||
            Vector2.Distance(corridor.halfExtents, new Vector2(1.21f, 4.8f)) >
                TransformTolerance ||
            Mathf.Abs(Mathf.DeltaAngle(
                corridor.yawDegrees,
                -mausoleum.eulerAngles.y)) > TransformTolerance)
        {
            problems.Add(
                "The mausoleum terrain-hole corridor is not the canonical 2.42 m by " +
                "9.6 m grid-selection rectangle centered at HAR_015 local (0, 1).");
        }

        int resolution = production.holesResolution;
        if (expectedHoles.GetLength(0) != resolution ||
            expectedHoles.GetLength(1) != resolution)
        {
            problems.Add(
                "The mausoleum terrain-hole reachability check received a mask with " +
                "an unexpected resolution.");
            return;
        }

        Vector2 halfCellSize = new Vector2(
            production.size.x / resolution * 0.5f,
            production.size.z / resolution * 0.5f);
        int authoredCellCount = 0;

        for (int z = 0; z < resolution; z++)
        {
            float worldZ = terrainPosition.z +
                (z + 0.5f) / resolution * production.size.z;

            for (int x = 0; x < resolution; x++)
            {
                float worldX = terrainPosition.x +
                    (x + 0.5f) / resolution * production.size.x;

                if (corridor.ContainsCell(
                        new Vector2(worldX, worldZ),
                        halfCellSize))
                {
                    authoredCellCount++;
                }
            }
        }

        if (authoredCellCount == 0)
        {
            problems.Add(
                "The production terrain resolution cannot represent the fully backed " +
                "mausoleum corridor without overhanging its 2.4 m ramp.");
            return;
        }

        Vector2Int exteriorCell;
        Vector2Int thresholdCell;
        Vector2Int socketCell;
        bool exteriorResolved = ValidateHoleDatum(
            "HAR015_EstateGrounds",
            mausoleum.TransformPoint(MausoleumExteriorDatumLocal),
            false,
            production,
            terrainPosition,
            expectedHoles,
            problems,
            out exteriorCell);
        bool thresholdResolved = ValidateHoleDatum(
            "HAR015_MausoleumThreshold",
            mausoleum.TransformPoint(MausoleumThresholdDatumLocal),
            true,
            production,
            terrainPosition,
            expectedHoles,
            problems,
            out thresholdCell);
        bool socketResolved = ValidateHoleDatum(
            "HAR015_CryptEntrySocket",
            mausoleum.TransformPoint(MausoleumSocketDatumLocal),
            true,
            production,
            terrainPosition,
            expectedHoles,
            problems,
            out socketCell);

        // The exterior must remain supported terrain while the threshold and
        // socket cells form one continuous, fully contained cut over the
        // authored 2.4 m ramp and its 3.0 m landings.
        if (exteriorResolved && thresholdResolved && socketResolved &&
            !AreAuthoredHoleCellsConnected(
                production,
                terrainPosition,
                corridor,
                thresholdCell,
                socketCell))
        {
            problems.Add(
                "The fully backed mausoleum terrain-hole cells do not form a continuous " +
                "route from the threshold to the crypt socket.");
        }
    }

    private static bool ValidateHoleDatum(
        string datumName,
        Vector3 worldDatum,
        bool shouldBeHole,
        TerrainData terrain,
        Vector3 terrainPosition,
        bool[,] holes,
        List<string> problems,
        out Vector2Int cell)
    {
        if (!TryGetHoleCell(
                terrain,
                terrainPosition,
                worldDatum,
                out cell))
        {
            problems.Add(
                datumName + " is outside the production terrain hole-mask bounds.");
            return false;
        }

        bool isHole = !holes[cell.y, cell.x];
        if (isHole != shouldBeHole)
        {
            problems.Add(
                datumName + (shouldBeHole
                    ? " is still covered by terrain."
                    : " no longer has solid exterior terrain support."));
        }

        return true;
    }

    private static bool TryGetHoleCell(
        TerrainData terrain,
        Vector3 terrainPosition,
        Vector3 world,
        out Vector2Int cell)
    {
        float normalizedX = (world.x - terrainPosition.x) / terrain.size.x;
        float normalizedZ = (world.z - terrainPosition.z) / terrain.size.z;
        if (normalizedX < 0f || normalizedX >= 1f ||
            normalizedZ < 0f || normalizedZ >= 1f)
        {
            cell = default;
            return false;
        }

        int resolution = terrain.holesResolution;
        cell = new Vector2Int(
            Mathf.FloorToInt(normalizedX * resolution),
            Mathf.FloorToInt(normalizedZ * resolution));
        return true;
    }

    private static bool AreAuthoredHoleCellsConnected(
        TerrainData terrain,
        Vector3 terrainPosition,
        HoleSpec corridor,
        Vector2Int start,
        Vector2Int end)
    {
        int resolution = terrain.holesResolution;
        Vector2 halfCellSize = new Vector2(
            terrain.size.x / resolution * 0.5f,
            terrain.size.z / resolution * 0.5f);

        if (!IsAuthoredHoleCell(
                terrain,
                terrainPosition,
                corridor,
                halfCellSize,
                start) ||
            !IsAuthoredHoleCell(
                terrain,
                terrainPosition,
                corridor,
                halfCellSize,
                end))
        {
            return false;
        }

        Queue<Vector2Int> open = new Queue<Vector2Int>();
        HashSet<int> visited = new HashSet<int>();
        open.Enqueue(start);
        visited.Add(start.y * resolution + start.x);

        Vector2Int[] directions =
        {
            Vector2Int.left,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.up
        };

        while (open.Count > 0)
        {
            Vector2Int current = open.Dequeue();
            if (current == end)
            {
                return true;
            }

            for (int i = 0; i < directions.Length; i++)
            {
                Vector2Int next = current + directions[i];
                if (next.x < 0 || next.x >= resolution ||
                    next.y < 0 || next.y >= resolution)
                {
                    continue;
                }

                int key = next.y * resolution + next.x;
                if (visited.Contains(key) ||
                    !IsAuthoredHoleCell(
                        terrain,
                        terrainPosition,
                        corridor,
                        halfCellSize,
                        next))
                {
                    continue;
                }

                visited.Add(key);
                open.Enqueue(next);
            }
        }

        return false;
    }

    private static bool IsAuthoredHoleCell(
        TerrainData terrain,
        Vector3 terrainPosition,
        HoleSpec corridor,
        Vector2 halfCellSize,
        Vector2Int cell)
    {
        int resolution = terrain.holesResolution;
        Vector2 center = new Vector2(
            terrainPosition.x +
                (cell.x + 0.5f) / resolution * terrain.size.x,
            terrainPosition.z +
                (cell.y + 0.5f) / resolution * terrain.size.z);
        return corridor.ContainsCell(center, halfCellSize);
    }

    private static void ValidateCrypt(
        Scene scene,
        List<string> problems)
    {
        Transform mausoleum;
        Transform crypt;

        try
        {
            mausoleum = RequireUniqueTransform(scene, MausoleumName);
            crypt = RequireUniqueTransform(scene, CryptName);
        }
        catch (Exception exception)
        {
            problems.Add("Crypt roots could not be resolved: " + exception.Message);
            return;
        }

        Transform approach;
        Transform threshold;
        Transform ramp;
        Transform mausoleumSocket;
        Transform cryptSocket;
        Transform cryptMainFloor;
        try
        {
            approach = RequireUniqueDescendant(
                mausoleum,
                MausoleumApproachName);
            threshold = RequireUniqueDescendant(
                mausoleum,
                MausoleumThresholdName);
            ramp = RequireUniqueDescendant(mausoleum, MausoleumRampName);
            mausoleumSocket = RequireUniqueDescendant(
                mausoleum,
                MausoleumSocketName);
            cryptSocket = RequireUniqueDescendant(crypt, CryptSocketName);
            cryptMainFloor = RequireUniqueDescendant(
                crypt,
                CryptMainFloorName);
        }
        catch (Exception exception)
        {
            problems.Add(
                "The mausoleum/crypt walk route could not be resolved: " +
                exception.Message);
            return;
        }

        BoxCollider approachBox = ValidateCanonicalRouteBox(
            approach,
            new Vector3(0f, -0.1f, -2.3f),
            Quaternion.identity,
            new Vector3(3f, 0.2f, 2.5f),
            problems);
        BoxCollider thresholdBox = ValidateCanonicalRouteBox(
            threshold,
            new Vector3(0f, -0.1f, -1f),
            Quaternion.identity,
            new Vector3(3f, 0.2f, 1.4f),
            problems);
        BoxCollider rampBox = ValidateCanonicalRouteBox(
            ramp,
            new Vector3(0f, -1.2f, 1.55f),
            new Quaternion(0.24253564f, 0f, 0f, 0.97014254f),
            new Vector3(2.4f, 0.2f, 5.6f),
            problems);
        BoxCollider mausoleumSocketBox = ValidateCanonicalRouteBox(
            mausoleumSocket,
            new Vector3(0f, -2.5f, 4.5f),
            Quaternion.identity,
            new Vector3(3f, 0.2f, 2f),
            problems);
        BoxCollider cryptSocketBox = ValidateCanonicalRouteBox(
            cryptSocket,
            new Vector3(0f, -2.5f, -5f),
            Quaternion.identity,
            new Vector3(2.4f, 0.2f, 2f),
            problems);
        BoxCollider cryptMainFloorBox = ValidateCanonicalRouteBox(
            cryptMainFloor,
            new Vector3(0f, -2.5f, 0f),
            Quaternion.identity,
            new Vector3(13.5f, 0.2f, 9.5f),
            problems);

        float socketGap = Vector3.Distance(
            mausoleumSocket.position,
            cryptSocket.position);
        if (socketGap > SocketTolerance)
        {
            problems.Add(
                "HAR_016 entry is " + socketGap.ToString("F3") +
                " m from the HAR_015 socket; maximum is " +
                SocketTolerance.ToString("F2") + " m.");
        }

        ValidateReachabilityDatum(
            "HAR015_EstateGrounds exterior",
            mausoleum.TransformPoint(MausoleumExteriorDatumLocal),
            approachBox,
            0.2f,
            problems);
        ValidateReachabilityDatum(
            "HAR015_MausoleumThreshold",
            mausoleum.TransformPoint(MausoleumThresholdDatumLocal),
            thresholdBox,
            0.2f,
            problems);

        Vector3 alignedSocketDatum =
            mausoleum.TransformPoint(MausoleumSocketDatumLocal);
        ValidateReachabilityDatum(
            "HAR015_CryptEntrySocket landing",
            alignedSocketDatum,
            mausoleumSocketBox,
            0.15f,
            problems);
        ValidateReachabilityDatum(
            "HAR015_CryptEntrySocket aligned crypt connector",
            alignedSocketDatum,
            cryptSocketBox,
            0.15f,
            problems);

        Vector3 cryptEntryDatum = crypt.TransformPoint(CryptEntryDatumLocal);
        ValidateReachabilityDatum(
            "HAR016_MausoleumEntrySocket connector",
            cryptEntryDatum,
            cryptSocketBox,
            0.15f,
            problems);
        ValidateReachabilityDatum(
            "HAR016_MausoleumEntrySocket main-floor overlap",
            cryptEntryDatum,
            cryptMainFloorBox,
            0.15f,
            problems);
        ValidateReachabilityDatum(
            "HAR016_MainCrypt floor",
            crypt.TransformPoint(CryptFloorDatumLocal),
            cryptMainFloorBox,
            0.15f,
            problems);

        ValidateSharedColliderDatum(
            "mausoleum approach-to-threshold overlap",
            mausoleum.TransformPoint(new Vector3(0f, -0.1f, -1.3f)),
            approachBox,
            thresholdBox,
            problems);
        ValidateSharedColliderDatum(
            "mausoleum threshold-to-ramp transition",
            mausoleum.TransformPoint(new Vector3(0f, 0f, -0.7f)),
            thresholdBox,
            rampBox,
            problems);
        ValidateSharedColliderDatum(
            "mausoleum ramp-to-socket-landing transition",
            mausoleum.TransformPoint(new Vector3(0f, -2.4f, 3.8f)),
            rampBox,
            mausoleumSocketBox,
            problems);
        ValidateSharedColliderDatum(
            "aligned mausoleum-to-crypt socket",
            mausoleumSocket.position,
            mausoleumSocketBox,
            cryptSocketBox,
            problems);
        ValidateSharedColliderDatum(
            "crypt entry-connector-to-main-floor overlap",
            crypt.TransformPoint(new Vector3(0f, -2.5f, -4.5f)),
            cryptSocketBox,
            cryptMainFloorBox,
            problems);

    }

    private static BoxCollider ValidateCanonicalRouteBox(
        Transform route,
        Vector3 expectedLocalPosition,
        Quaternion expectedLocalRotation,
        Vector3 expectedSize,
        List<string> problems)
    {
        BoxCollider[] boxes = route.GetComponents<BoxCollider>();
        if (boxes.Length != 1)
        {
            problems.Add(
                route.name + " must contain exactly one BoxCollider; found " +
                boxes.Length + ".");
            return boxes.Length > 0 ? boxes[0] : null;
        }

        BoxCollider box = boxes[0];
        if (Vector3.Distance(route.localPosition, expectedLocalPosition) >
                TransformTolerance ||
            Quaternion.Angle(route.localRotation, expectedLocalRotation) >
                TransformTolerance ||
            Vector3.Distance(route.localScale, Vector3.one) > TransformTolerance ||
            Vector3.Distance(box.center, Vector3.zero) > TransformTolerance ||
            Vector3.Distance(box.size, expectedSize) > TransformTolerance ||
            !route.gameObject.activeInHierarchy ||
            !box.enabled ||
            box.isTrigger)
        {
            problems.Add(
                route.name +
                " does not match its exact enabled, non-trigger walk-surface contract.");
        }

        return box;
    }

    private static void ValidateReachabilityDatum(
        string label,
        Vector3 worldDatum,
        BoxCollider support,
        float expectedClearance,
        List<string> problems)
    {
        if (support == null)
        {
            return;
        }

        Vector3 local = support.transform.InverseTransformPoint(worldDatum) -
            support.center;
        Vector3 half = support.size * 0.5f;
        float outsideX = Mathf.Max(Mathf.Abs(local.x) - half.x, 0f);
        float outsideZ = Mathf.Max(Mathf.Abs(local.z) - half.z, 0f);
        float horizontalResidual =
            Mathf.Sqrt(outsideX * outsideX + outsideZ * outsideZ);

        Vector3 surfaceLocal = support.center + new Vector3(
            Mathf.Clamp(local.x, -half.x, half.x),
            half.y,
            Mathf.Clamp(local.z, -half.z, half.z));
        float clearance = Vector3.Distance(
            worldDatum,
            support.transform.TransformPoint(surfaceLocal));

        if (horizontalResidual > RouteDatumTolerance ||
            Mathf.Abs(clearance - expectedClearance) >
                ReachabilityClearanceTolerance)
        {
            problems.Add(
                label + " is not on its exact walk-surface datum (horizontal residual " +
                horizontalResidual.ToString("F3") + " m, clearance " +
                clearance.ToString("F3") + " m).");
        }
    }

    private static void ValidateSharedColliderDatum(
        string label,
        Vector3 worldDatum,
        BoxCollider first,
        BoxCollider second,
        List<string> problems)
    {
        if (first == null || second == null)
        {
            return;
        }

        float firstGap = DistanceToBox(first, worldDatum);
        float secondGap = DistanceToBox(second, worldDatum);
        if (firstGap > RouteDatumTolerance || secondGap > RouteDatumTolerance)
        {
            problems.Add(
                label + " is not physically shared by both authored colliders " +
                "(gaps " + firstGap.ToString("F3") + " m and " +
                secondGap.ToString("F3") + " m).");
        }
    }

    private static float DistanceToBox(
        BoxCollider box,
        Vector3 worldPoint)
    {
        Vector3 local = box.transform.InverseTransformPoint(worldPoint) - box.center;
        Vector3 half = box.size * 0.5f;
        Vector3 closest = new Vector3(
            Mathf.Clamp(local.x, -half.x, half.x),
            Mathf.Clamp(local.y, -half.y, half.y),
            Mathf.Clamp(local.z, -half.z, half.z));
        return Vector3.Distance(
            worldPoint,
            box.transform.TransformPoint(box.center + closest));
    }

    private static void ValidateRemovedRocks(
        Scene scene,
        List<string> problems)
    {
        for (int i = 0; i < RemovedRockNames.Length; i++)
        {
            int count = FindSceneTransforms(scene, RemovedRockNames[i]).Count;
            if (count > 0)
            {
                problems.Add(
                    RemovedRockNames[i] +
                    " still intersects an authored terrain support region.");
            }
        }
    }

    private static void ValidateSupportHierarchy(
        Scene scene,
        int terrainLayer,
        TerrainData production,
        TerrainData campaign,
        List<string> problems)
    {
        List<Transform> roots = FindSceneTransforms(scene, SupportRootName);

        if (roots.Count != 1 || roots[0].parent != null)
        {
            problems.Add(
                "Expected exactly one top-level " + SupportRootName +
                ", found " + roots.Count + ".");
            return;
        }

        Transform root = roots[0];
        if (root.position.sqrMagnitude > 0.0001f ||
            Quaternion.Angle(root.rotation, Quaternion.identity) > 0.01f ||
            (root.localScale - Vector3.one).sqrMagnitude > 0.0001f)
        {
            problems.Add("The asset-support root transform is not canonical.");
        }

        Transform metadata = FindDirectChild(root, MetadataName);
        Transform underground = FindDirectChild(root, UndergroundSupportName);
        HashSet<string> expectedRootChildren = new HashSet<string>(
            new[]
            {
                MetadataName,
                UndergroundSupportName
            },
            StringComparer.Ordinal);
        if (!new HashSet<string>(
                DirectChildren(root).Select(child => child.name),
                StringComparer.Ordinal).SetEquals(expectedRootChildren))
        {
            problems.Add(
                "The asset-support root has missing or unexpected direct children.");
        }
        if (metadata == null)
        {
            problems.Add("The asset-support metadata hierarchy is missing.");
        }
        else
        {
            string sourceGuid = AssetDatabase.AssetPathToGUID(ProductionTerrainPath);
            string campaignGuid = AssetDatabase.AssetPathToGUID(CampaignTerrainPath);
            string sourceHash = HashFile(ToAbsolutePath(ProductionTerrainPath));
            string[] expectedMarkers =
            {
                SchemaMarkerName,
                "SOURCE_TERRAIN_GUID__" + sourceGuid,
                "SOURCE_TERRAIN_SHA256__" + sourceHash,
                "CAMPAIGN_TERRAIN_GUID__" + campaignGuid,
                "SOURCE_HEIGHT_RESOLUTION__" + production.heightmapResolution,
                "SOURCE_HOLES_RESOLUTION__" + production.holesResolution
            };

            HashSet<string> actualMarkers = new HashSet<string>(
                DirectChildren(metadata).Select(child => child.name),
                StringComparer.Ordinal);

            if (!actualMarkers.SetEquals(expectedMarkers))
            {
                problems.Add("The asset-support metadata markers are stale or incomplete.");
            }
        }

        if (underground == null)
        {
            problems.Add("The mausoleum underground support hierarchy is missing.");
        }
        else
        {
            Transform mausoleum;
            Transform crypt;
            try
            {
                mausoleum = RequireUniqueTransform(scene, MausoleumName);
                crypt = RequireUniqueTransform(scene, CryptName);
            }
            catch (Exception exception)
            {
                problems.Add("Underground support targets are missing: " + exception.Message);
                mausoleum = null;
                crypt = null;
            }

            if (mausoleum != null && crypt != null)
            {
                BoxSupportSpec[] expectedBoxes = BuildBoxSupportSpecs(mausoleum, crypt);
                HashSet<string> expectedNames = new HashSet<string>(
                    expectedBoxes.Select(box => box.name),
                    StringComparer.Ordinal);

                HashSet<string> actualNames = new HashSet<string>(
                    DirectChildren(underground).Select(child => child.name),
                    StringComparer.Ordinal);

                if (!actualNames.SetEquals(expectedNames))
                {
                    problems.Add(
                        "The mausoleum support collider set is stale or incomplete.");
                }
                else
                {
                    for (int i = 0; i < expectedBoxes.Length; i++)
                    {
                        BoxSupportSpec expected = expectedBoxes[i];
                        Transform child = FindDirectChild(underground, expected.name);
                        BoxCollider box = child != null
                            ? child.GetComponent<BoxCollider>()
                            : null;

                        if (child == null || box == null)
                        {
                            problems.Add(
                                expected.name + " is missing its BoxCollider.");
                            continue;
                        }

                        if (child.gameObject.layer != terrainLayer ||
                            !child.gameObject.isStatic ||
                            Vector3.Distance(
                                child.position,
                                expected.worldPosition) > TransformTolerance ||
                            Quaternion.Angle(
                                child.rotation,
                                expected.worldRotation) > 0.05f ||
                            (child.localScale - Vector3.one).sqrMagnitude > 0.0001f ||
                            (box.size - expected.size).sqrMagnitude > 0.0001f ||
                            box.center.sqrMagnitude > 0.0001f ||
                            box.isTrigger ||
                            !box.enabled)
                        {
                            problems.Add(
                                expected.name +
                                " collider configuration is not canonical.");
                        }

                        Component[] components = child.GetComponents<Component>();
                        if (components.Any(component =>
                                !(component is Transform) &&
                                !(component is BoxCollider)))
                        {
                            problems.Add(
                                expected.name + " contains an unowned component.");
                        }
                    }
                }
            }
        }

        if (AssetDatabase.GetAssetPath(campaign) != CampaignTerrainPath)
        {
            problems.Add("Support metadata references a noncanonical campaign terrain.");
        }
    }

    private static void ReconcileMarkerChildren(
        Transform parent,
        IEnumerable<string> markerNames)
    {
        HashSet<string> expected =
            new HashSet<string>(markerNames, StringComparer.Ordinal);
        RemoveUnexpectedChildren(parent, expected);

        foreach (string markerName in expected.OrderBy(value => value, StringComparer.Ordinal))
        {
            Transform marker = GetOrCreateChild(parent, markerName);
            ConfigureLocalTransform(marker, Vector3.zero, Quaternion.identity, Vector3.one);

            Component[] components = marker.GetComponents<Component>();
            for (int i = components.Length - 1; i >= 0; i--)
            {
                if (!(components[i] is Transform))
                {
                    UnityEngine.Object.DestroyImmediate(components[i]);
                }
            }
        }
    }

    private static void RemoveUnexpectedChildren(
        Transform parent,
        HashSet<string> expectedNames)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (!expectedNames.Contains(child.name))
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    private static GameObject GetOrCreateSceneRoot(Scene scene, string name)
    {
        List<Transform> matches = FindSceneTransforms(scene, name);

        if (matches.Count > 1)
        {
            throw new InvalidOperationException(
                "Multiple top-level scene objects are named " + name + ".");
        }

        if (matches.Count == 1)
        {
            matches[0].SetParent(null, true);
            return matches[0].gameObject;
        }

        GameObject created = new GameObject(name);
        SceneManager.MoveGameObjectToScene(created, scene);
        return created;
    }

    private static Transform GetOrCreateChild(Transform parent, string name)
    {
        List<Transform> matches = DirectChildren(parent)
            .Where(child => child.name == name)
            .ToList();

        if (matches.Count > 1)
        {
            throw new InvalidOperationException(
                "Multiple direct children named " + name +
                " exist under " + parent.name + ".");
        }

        if (matches.Count == 1)
        {
            return matches[0];
        }

        GameObject created = new GameObject(name);
        created.transform.SetParent(parent, false);
        return created.transform;
    }

    private static void ConfigureTransform(
        Transform transform,
        Transform parent,
        Vector3 worldPosition,
        Quaternion worldRotation,
        Vector3 localScale)
    {
        transform.SetParent(parent, true);
        transform.position = worldPosition;
        transform.rotation = worldRotation;
        transform.localScale = localScale;
    }

    private static void ConfigureLocalTransform(
        Transform transform,
        Vector3 localPosition,
        Quaternion localRotation,
        Vector3 localScale)
    {
        transform.localPosition = localPosition;
        transform.localRotation = localRotation;
        transform.localScale = localScale;
    }

    private static Transform RequireUniqueTransform(Scene scene, string name)
    {
        List<Transform> matches = FindSceneTransforms(scene, name);
        if (matches.Count != 1)
        {
            throw new InvalidOperationException(
                "Expected exactly one scene transform named " + name +
                ", found " + matches.Count + ".");
        }

        return matches[0];
    }

    private static Transform RequireUniqueDescendant(
        Transform parent,
        string name)
    {
        Transform[] transforms = parent.GetComponentsInChildren<Transform>(true);
        List<Transform> matches = transforms
            .Where(transform => transform != parent && transform.name == name)
            .ToList();

        if (matches.Count != 1)
        {
            throw new InvalidOperationException(
                "Expected exactly one descendant named " + name +
                " under " + parent.name + ", found " + matches.Count + ".");
        }

        return matches[0];
    }

    private static List<Transform> FindSceneTransforms(Scene scene, string name)
    {
        List<Transform> matches = new List<Transform>();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return matches;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int r = 0; r < roots.Length; r++)
        {
            Transform[] transforms =
                roots[r].GetComponentsInChildren<Transform>(true);

            for (int t = 0; t < transforms.Length; t++)
            {
                if (transforms[t].name == name)
                {
                    matches.Add(transforms[t]);
                }
            }
        }

        return matches;
    }

    private static IEnumerable<Transform> DirectChildren(Transform parent)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            yield return parent.GetChild(i);
        }
    }

    private static Transform FindDirectChild(Transform parent, string name)
    {
        return DirectChildren(parent).FirstOrDefault(child => child.name == name);
    }

    private static Terrain RequireSingleTerrain(Scene scene)
    {
        List<string> problems = new List<string>();
        Terrain terrain = TryGetSingleTerrain(scene, problems);
        if (terrain == null)
        {
            throw new InvalidOperationException(string.Join(" ", problems));
        }

        return terrain;
    }

    private static Terrain TryGetSingleTerrain(
        Scene scene,
        List<string> problems)
    {
        List<Terrain> terrains = new List<Terrain>();
        GameObject[] roots = scene.GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            terrains.AddRange(roots[i].GetComponentsInChildren<Terrain>(true));
        }

        if (terrains.Count != 1)
        {
            problems.Add(
                "Expected exactly one Terrain in the Open World scene, found " +
                terrains.Count + ".");
            return null;
        }

        return terrains[0];
    }

    private static TerrainCollider RequireTerrainCollider(Terrain terrain)
    {
        TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
        if (collider == null)
        {
            throw new InvalidOperationException(
                "The Open World Terrain has no TerrainCollider.");
        }

        return collider;
    }

    private static TerrainData RequireTerrainData(
        string path,
        string role)
    {
        TerrainData terrain = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
        if (terrain == null)
        {
            throw new InvalidOperationException(
                "The " + role + " TerrainData is missing: " + path);
        }

        return terrain;
    }

    private static void RequireTargetSceneClosedForBuild()
    {
        Scene loaded = SceneManager.GetSceneByPath(ScenePath);
        if (loaded.IsValid() && loaded.isLoaded)
        {
            throw new InvalidOperationException(
                "Close Bloodroot_OpenWorld before running this transactional authorer. " +
                "This prevents rollback from overwriting an open or unsaved scene.");
        }
    }

    private static void RequireAssetFile(string assetPath)
    {
        if (!AssetFileExists(assetPath))
        {
            throw new FileNotFoundException(
                "Required asset is missing.", assetPath);
        }
    }

    private static bool AssetFileExists(string assetPath)
    {
        return File.Exists(ToAbsolutePath(assetPath));
    }

    private static string ToAbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(
            Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string HashFile(string absolutePath)
    {
        if (!File.Exists(absolutePath))
        {
            return "<missing>";
        }

        using (FileStream stream = File.OpenRead(absolutePath))
        using (SHA256 sha = SHA256.Create())
        {
            return BitConverter.ToString(sha.ComputeHash(stream))
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }
    }

    private sealed class ImmutableAssetGuard
    {
        private readonly string assetPath;
        private readonly string assetHash;
        private readonly string metaHash;

        public ImmutableAssetGuard(string assetPath)
        {
            this.assetPath = assetPath;
            assetHash = HashFile(ToAbsolutePath(assetPath));
            metaHash = HashFile(ToAbsolutePath(assetPath + ".meta"));
        }

        public void AssertUnchanged()
        {
            string currentAssetHash = HashFile(ToAbsolutePath(assetPath));
            string currentMetaHash = HashFile(ToAbsolutePath(assetPath + ".meta"));

            if (currentAssetHash != assetHash || currentMetaHash != metaHash)
            {
                throw new InvalidOperationException(
                    "The immutable production TerrainData or its meta changed during " +
                    "campaign asset-support authoring.");
            }
        }
    }

    private sealed class FileTransaction : IDisposable
    {
        private readonly string snapshotFolder;
        private readonly List<FileSnapshot> snapshots =
            new List<FileSnapshot>();
        private bool committed;
        private bool disposed;

        public FileTransaction(params string[] assetAndMetaPaths)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            snapshotFolder = Path.Combine(
                projectRoot,
                TransactionFolder.Replace('/', Path.DirectorySeparatorChar),
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(snapshotFolder);

            for (int i = 0; i < assetAndMetaPaths.Length; i++)
            {
                string absolute = ToAbsolutePath(assetAndMetaPaths[i]);
                string snapshot = Path.Combine(
                    snapshotFolder,
                    "snapshot_" + i.ToString("D2") + ".bin");

                snapshots.Add(new FileSnapshot(absolute, snapshot));
            }
        }

        public void Commit()
        {
            committed = true;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            try
            {
                if (!committed)
                {
                    for (int i = 0; i < snapshots.Count; i++)
                    {
                        snapshots[i].Restore();
                    }

                    AssetDatabase.Refresh(
                        ImportAssetOptions.ForceSynchronousImport);
                }
            }
            finally
            {
                try
                {
                    if (Directory.Exists(snapshotFolder))
                    {
                        Directory.Delete(snapshotFolder, true);
                    }
                }
                catch (Exception cleanupException)
                {
                    Debug.LogWarning(
                        "Could not remove Open World asset-support transaction files: " +
                        cleanupException.Message);
                }
            }
        }
    }

    private sealed class FileSnapshot
    {
        private readonly string originalPath;
        private readonly string snapshotPath;
        private readonly bool existed;

        public FileSnapshot(string originalPath, string snapshotPath)
        {
            this.originalPath = originalPath;
            this.snapshotPath = snapshotPath;
            existed = File.Exists(originalPath);

            if (existed)
            {
                File.Copy(originalPath, snapshotPath, true);
            }
        }

        public void Restore()
        {
            if (existed)
            {
                string directory = Path.GetDirectoryName(originalPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.Copy(snapshotPath, originalPath, true);
            }
            else if (File.Exists(originalPath))
            {
                File.Delete(originalPath);
            }
        }
    }

    private sealed class SupportPadSpec
    {
        public readonly string id;
        public readonly string primaryTargetName;
        public readonly string secondaryTargetName;
        public readonly Vector2 center;
        public readonly Vector2 halfExtents;
        public readonly Vector2 featherExtents;
        public readonly float yawDegrees;
        public readonly float semanticTolerance;
        public readonly bool validateDatum;

        public SupportPadSpec(
            string id,
            string primaryTargetName,
            string secondaryTargetName,
            Vector2 center,
            Vector2 halfExtents,
            Vector2 featherExtents,
            float yawDegrees,
            float semanticTolerance,
            bool validateDatum)
        {
            this.id = id;
            this.primaryTargetName = primaryTargetName;
            this.secondaryTargetName = secondaryTargetName;
            this.center = center;
            this.halfExtents = halfExtents;
            this.featherExtents = featherExtents;
            this.yawDegrees = yawDegrees;
            this.semanticTolerance = semanticTolerance;
            this.validateDatum = validateDatum;
        }

        public float Weight(Vector2 world)
        {
            Vector2 local = Rotate(world - center, -yawDegrees);
            float outsideX = Mathf.Max(Mathf.Abs(local.x) - halfExtents.x, 0f);
            float outsideZ = Mathf.Max(Mathf.Abs(local.y) - halfExtents.y, 0f);

            if (outsideX <= 0f && outsideZ <= 0f)
            {
                return 1f;
            }

            float normalizedX = featherExtents.x > 0f
                ? outsideX / featherExtents.x
                : float.PositiveInfinity;
            float normalizedZ = featherExtents.y > 0f
                ? outsideZ / featherExtents.y
                : float.PositiveInfinity;

            float normalizedDistance = Mathf.Sqrt(
                normalizedX * normalizedX + normalizedZ * normalizedZ);

            if (normalizedDistance >= 1f)
            {
                return 0f;
            }

            float t = 1f - normalizedDistance;
            return t * t * (3f - 2f * t);
        }

        public Vector2[] DenseInnerSamples()
        {
            List<Vector2> samples = new List<Vector2>();
            float[] fractions = { -0.75f, -0.375f, 0f, 0.375f, 0.75f };

            for (int z = 0; z < fractions.Length; z++)
            {
                for (int x = 0; x < fractions.Length; x++)
                {
                    Vector2 local = new Vector2(
                        halfExtents.x * fractions[x],
                        halfExtents.y * fractions[z]);
                    samples.Add(center + Rotate(local, yawDegrees));
                }
            }

            return samples.ToArray();
        }
    }

    private sealed class ResolvedPad
    {
        public readonly SupportPadSpec spec;
        public readonly float targetWorldY;

        public ResolvedPad(SupportPadSpec spec, float targetWorldY)
        {
            this.spec = spec;
            this.targetWorldY = targetWorldY;
        }
    }

    private sealed class HoleSpec
    {
        public readonly string id;
        public readonly Vector2 center;
        public readonly Vector2 halfExtents;
        public readonly float yawDegrees;
        private readonly float inverseCosine;
        private readonly float inverseSine;

        public HoleSpec(
            string id,
            Vector2 center,
            Vector2 halfExtents,
            float yawDegrees)
        {
            this.id = id;
            this.center = center;
            this.halfExtents = halfExtents;
            this.yawDegrees = yawDegrees;

            float inverseRadians = -yawDegrees * Mathf.Deg2Rad;
            inverseCosine = Mathf.Cos(inverseRadians);
            inverseSine = Mathf.Sin(inverseRadians);
        }

        public bool Contains(Vector2 world)
        {
            Vector2 local = ToLocal(world);
            return Mathf.Abs(local.x) <= halfExtents.x &&
                   Mathf.Abs(local.y) <= halfExtents.y;
        }

        public bool ContainsCell(
            Vector2 worldCenter,
            Vector2 worldHalfSize)
        {
            // Terrain holes remove complete, axis-aligned cells. Requiring all
            // four corners to fit prevents the cell edge from extending beyond
            // the authored collider-backed corridor after rotation. Projecting
            // the axis-aligned half-size is equivalent to testing each corner
            // and avoids four repeated rotations per terrain cell.
            Vector2 localCenter = ToLocal(worldCenter);
            float localHalfX =
                Mathf.Abs(inverseCosine) * worldHalfSize.x +
                Mathf.Abs(inverseSine) * worldHalfSize.y;
            float localHalfZ =
                Mathf.Abs(inverseSine) * worldHalfSize.x +
                Mathf.Abs(inverseCosine) * worldHalfSize.y;

            return Mathf.Abs(localCenter.x) + localHalfX <= halfExtents.x &&
                   Mathf.Abs(localCenter.y) + localHalfZ <= halfExtents.y;
        }

        public bool OverlapsCell(Vector2 worldCenter, Vector2 worldHalfSize)
        {
            Vector2 localCenter = ToLocal(worldCenter);
            float cosine = Mathf.Abs(inverseCosine);
            float sine = Mathf.Abs(inverseSine);
            Vector2 delta = worldCenter - center;
            // Separating-axis test: the floor's two axes and the detail cell's
            // two axes. This also keeps rotated floor clearances tightly scoped.
            return Mathf.Abs(localCenter.x) < halfExtents.x +
                       cosine * worldHalfSize.x + sine * worldHalfSize.y &&
                   Mathf.Abs(localCenter.y) < halfExtents.y +
                       sine * worldHalfSize.x + cosine * worldHalfSize.y &&
                   Mathf.Abs(delta.x) < worldHalfSize.x +
                       cosine * halfExtents.x + sine * halfExtents.y &&
                   Mathf.Abs(delta.y) < worldHalfSize.y +
                       sine * halfExtents.x + cosine * halfExtents.y;
        }

        private Vector2 ToLocal(Vector2 world)
        {
            Vector2 delta = world - center;
            return new Vector2(
                delta.x * inverseCosine - delta.y * inverseSine,
                delta.x * inverseSine + delta.y * inverseCosine);
        }
    }

    private sealed class BoxSupportSpec
    {
        public readonly string name;
        public readonly Vector3 worldPosition;
        public readonly Quaternion worldRotation;
        public readonly Vector3 size;

        public BoxSupportSpec(
            string name,
            Vector3 worldPosition,
            Quaternion worldRotation,
            Vector3 size)
        {
            this.name = name;
            this.worldPosition = worldPosition;
            this.worldRotation = worldRotation;
            this.size = size;
        }
    }

    private static Vector2 Rotate(Vector2 value, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cosine = Mathf.Cos(radians);
        float sine = Mathf.Sin(radians);
        return new Vector2(
            value.x * cosine - value.y * sine,
            value.x * sine + value.y * cosine);
    }
}
#endif
