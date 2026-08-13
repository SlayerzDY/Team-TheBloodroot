#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class BloodrootOpenWorldTerrainProduction
{
    private const string OpenWorldScene =
        "Assets/Scenes/OpenWorld/Bloodroot_OpenWorld.unity";

    private const string SourceTerrainPath =
        "Assets/Scenes/OpenWorld/Data/Bloodroot_OpenWorld_Terrain.asset";

    private const string ProductionTerrainPath =
        "Assets/Scenes/OpenWorld/Data/Bloodroot_OpenWorld_Terrain_Production.asset";

    private const string ProductionTerrainRepairBackupPath =
        "Assets/Scenes/OpenWorld/Data/Bloodroot_OpenWorld_Terrain_RepairTemp.asset";

    private const string SceneBackupFolder =
        "Assets/Scenes/OpenWorld/Backups";

    private const string SceneBackupPath =
        SceneBackupFolder + "/Bloodroot_OpenWorld_PreTerrainProduction.unity";

    private const string GeneratedMaterialFolder =
        "Assets/Materials/OpenWorld/TerrainProduction";

    private const string ExistingGroundLayerPath =
        "Assets/Materials/Features/NewGround/TerrainGround.terrainlayer";

    private const string ProductionGroundLayerPath =
        GeneratedMaterialFolder + "/ForestLoam.terrainlayer";

    private static readonly string[] ProductionTerrainLayerPaths =
    {
        ProductionGroundLayerPath,
        GeneratedMaterialFolder + "/WetMud.terrainlayer",
        GeneratedMaterialFolder + "/GravelRoad.terrainlayer",
        GeneratedMaterialFolder + "/ExposedClayRock.terrainlayer",
        GeneratedMaterialFolder + "/BloodrootCorruption.terrainlayer"
    };

    private static readonly string[] GeneratedTerrainOutputPaths =
    {
        ProductionGroundLayerPath,
        GeneratedMaterialFolder + "/WetMud.terrainlayer",
        GeneratedMaterialFolder + "/WetMud_Albedo.asset",
        GeneratedMaterialFolder + "/GravelRoad.terrainlayer",
        GeneratedMaterialFolder + "/GravelRoad_Albedo.asset",
        GeneratedMaterialFolder + "/ExposedClayRock.terrainlayer",
        GeneratedMaterialFolder + "/ExposedClayRock_Albedo.asset",
        GeneratedMaterialFolder + "/BloodrootCorruption.terrainlayer",
        GeneratedMaterialFolder + "/BloodrootCorruption_Albedo.asset"
    };

    private const int HeightmapResolution = 1025;
    private const int AlphamapResolution = 512;
    private const float TerrainWidth = 1400f;
    private const float TerrainHeight = 120f;
    private const float TerrainLength = 1400f;
    private const float TerrainWorldY = -10f;

    private static readonly float[] ProductionLayerSmoothness =
    {
        0.02f,
        0.065f,
        0.015f,
        0.025f,
        0.04f
    };

    private static readonly RouteNode[] ProgressionRoad =
    {
        new RouteNode(-350f, -150f, 4f),
        new RouteNode(-120f, -118f, 6f),
        new RouteNode(85f, -78f, 7f),
        new RouteNode(235f, -40f, 7.5f),
        new RouteNode(320f, -40f, 8f),
        new RouteNode(250f, 100f, 13f),
        new RouteNode(155f, 195f, 20f),
        new RouteNode(70f, 240f, 26f),
        new RouteNode(40f, 260f, 30f),
        new RouteNode(-25f, 315f, 47f),
        new RouteNode(40f, 360f, 64f),
        new RouteNode(110f, 420f, 50f),
        new RouteNode(100f, 470f, 39f),
        new RouteNode(65f, 515f, 27f),
        new RouteNode(50f, 540f, 21f),
        new RouteNode(50f, 600f, 9f)
    };

    private static readonly RouteNode[] BlackPinesCreek =
    {
        new RouteNode(-660f, -360f, 3.2f),
        new RouteNode(-605f, -322f, 2.95f),
        new RouteNode(-545f, -306f, 2.7f),
        new RouteNode(-475f, -325f, 2.45f),
        new RouteNode(-425f, -370f, 2.2f),
        new RouteNode(-365f, -414f, 1.95f),
        new RouteNode(-295f, -423f, 1.7f),
        new RouteNode(-230f, -398f, 1.45f),
        new RouteNode(-176f, -350f, 1.2f),
        new RouteNode(-112f, -311f, 0.95f),
        new RouteNode(-38f, -300f, 0.7f),
        new RouteNode(25f, -322f, 0.45f),
        new RouteNode(72f, -370f, 0.2f),
        new RouteNode(132f, -414f, -0.05f),
        new RouteNode(205f, -430f, -0.3f),
        new RouteNode(285f, -402f, -0.6f),
        new RouteNode(352f, -350f, -0.95f),
        new RouteNode(430f, -326f, -1.3f)
    };

    [MenuItem(
        "Bloodroot/Open World/Build Production Terrain Pass",
        false,
        30)]
    public static void BuildProductionTerrainPass()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Terrain Build Unavailable",
                "Exit Play Mode before building the production terrain.",
                "OK");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(OpenWorldScene) == null ||
            AssetDatabase.LoadAssetAtPath<TerrainData>(SourceTerrainPath) == null)
        {
            EditorUtility.DisplayDialog(
                "Terrain Foundation Missing",
                "The Bloodroot open-world scene or its baseline TerrainData is missing.",
                "OK");
            return;
        }

        if (!string.IsNullOrEmpty(
                AssetDatabase.AssetPathToGUID(ProductionTerrainPath)))
        {
            EditorUtility.DisplayDialog(
                "Production Terrain Already Exists",
                "The one-shot production terrain already exists. No asset was overwritten. " +
                "Use the validation command to inspect it.",
                "OK");
            return;
        }

        if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(SceneBackupPath)) ||
            GeneratedTerrainOutputPaths.Any(
                path => !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path))))
        {
            EditorUtility.DisplayDialog(
                "Incomplete Terrain Build Detected",
                "A terrain backup or generated terrain output already exists without " +
                "the production TerrainData. Inspect that recovery state before retrying; " +
                "the builder will not overwrite it.",
                "OK");
            return;
        }

        Scene openWorldScene = SceneManager.GetSceneByPath(OpenWorldScene);
        bool openedHere = !openWorldScene.IsValid() || !openWorldScene.isLoaded;
        Scene previousActiveScene = SceneManager.GetActiveScene();
        bool previousActiveWasOpenWorld =
            previousActiveScene.IsValid() &&
            previousActiveScene.path == OpenWorldScene;
        bool buildCommitted = false;
        bool sceneMutationStarted = false;
        int undoGroup = -1;

        if (!openedHere && openWorldScene.isDirty)
        {
            EditorUtility.DisplayDialog(
                "Unsaved Open-World Scene",
                "Save or discard the open Bloodroot_OpenWorld scene changes before running " +
                "the terrain build. Nothing was changed.",
                "OK");
            return;
        }

        try
        {
            if (openedHere)
            {
                openWorldScene =
                    EditorSceneManager.OpenScene(
                        OpenWorldScene,
                        OpenSceneMode.Additive);
            }

            SceneManager.SetActiveScene(openWorldScene);

            GameObject[] matchingWorldRoots =
                openWorldScene.GetRootGameObjects()
                    .Where(root => root.name == "Bloodroot_OpenWorld")
                    .ToArray();

            GameObject worldRoot = matchingWorldRoots.FirstOrDefault();

            if (matchingWorldRoots.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Expected exactly one Bloodroot_OpenWorld root, found " +
                    $"{matchingWorldRoots.Length}.");
            }

            Transform terrainTransform =
                worldRoot.transform.Find("_TERRAIN/Open World Terrain");

            if (terrainTransform == null)
            {
                throw new InvalidOperationException(
                    "The required _TERRAIN/Open World Terrain object was not found.");
            }

            Terrain terrain = terrainTransform.GetComponent<Terrain>();
            TerrainCollider terrainCollider =
                terrainTransform.GetComponent<TerrainCollider>();

            if (terrain == null || terrainCollider == null)
            {
                throw new InvalidOperationException(
                    "Open World Terrain requires both Terrain and TerrainCollider components.");
            }

            if (AssetDatabase.GetAssetPath(terrain.terrainData) != SourceTerrainPath ||
                AssetDatabase.GetAssetPath(terrainCollider.terrainData) != SourceTerrainPath)
            {
                throw new InvalidOperationException(
                    "The baseline scene Terrain and TerrainCollider must both reference " +
                    $"{SourceTerrainPath} before the one-shot build.");
            }

            if (AssetDatabase.LoadAssetAtPath<TerrainLayer>(ExistingGroundLayerPath) == null)
            {
                throw new InvalidOperationException(
                    $"The baseline ground layer is missing: {ExistingGroundLayerPath}.");
            }

            string[] requiredFoundationPaths =
            {
                "_TERRAIN/Roads",
                "_TERRAIN/Rivers",
                "_CORE/Open World NavMesh Surface (Bake Later)",
                "_CORE/Return Truck (Travel Wiring Pending)",
                "AREA_00_BLACK_PINES_FOREST/World Arrival Spawn",
                "AREA_00_BLACK_PINES_FOREST/Exit Road",
                "AREA_01_STILLWATER_FEED_MILL/Area Spawn",
                "AREA_01_STILLWATER_FEED_MILL/Locked Entrance",
                "AREA_02_HARROW_ESTATE/Area Spawn",
                "AREA_02_HARROW_ESTATE/Locked Entrance",
                "AREA_03_BLOODROOT_HOLLOW/Boss Arena Spawn",
                "AREA_03_BLOODROOT_HOLLOW/Locked Entrance"
            };

            string missingFoundationPath =
                requiredFoundationPaths.FirstOrDefault(
                    path => worldRoot.transform.Find(path) == null);

            if (!string.IsNullOrEmpty(missingFoundationPath))
            {
                throw new InvalidOperationException(
                    $"Required open-world hierarchy path is missing: " +
                    $"{missingFoundationPath}.");
            }

            if (worldRoot.transform.Find(
                    "_TERRAIN/Roads/Primary Progression Road") != null ||
                worldRoot.transform.Find(
                    "_TERRAIN/Rivers/Black Pines Creek") != null)
            {
                throw new InvalidOperationException(
                    "The baseline scene already contains production route guides.");
            }

            EnsureAssetFolder(SceneBackupFolder);
            EnsureAssetFolder(GeneratedMaterialFolder);

            if (!AssetDatabase.CopyAsset(OpenWorldScene, SceneBackupPath))
            {
                throw new InvalidOperationException(
                    $"Unity could not create the scene backup at {SceneBackupPath}.");
            }

            if (!AssetDatabase.CopyAsset(SourceTerrainPath, ProductionTerrainPath))
            {
                throw new InvalidOperationException(
                    $"Unity could not copy the baseline terrain to {ProductionTerrainPath}.");
            }

            AssetDatabase.ImportAsset(
                ProductionTerrainPath,
                ImportAssetOptions.ForceSynchronousImport);

            TerrainData productionTerrain =
                AssetDatabase.LoadAssetAtPath<TerrainData>(ProductionTerrainPath);

            if (productionTerrain == null)
            {
                throw new InvalidOperationException(
                    "Unity could not load the copied production TerrainData.");
            }

            ConfigureProductionTerrainData(productionTerrain);

            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build Bloodroot production terrain");
            sceneMutationStarted = true;

            Undo.RecordObject(terrainTransform, "Apply Bloodroot production terrain");
            Undo.RecordObject(terrain, "Apply Bloodroot production terrain");
            Undo.RecordObject(terrainCollider, "Apply Bloodroot production terrain");

            terrainTransform.position = new Vector3(-700f, TerrainWorldY, -700f);
            terrain.terrainData = productionTerrain;
            terrainCollider.terrainData = productionTerrain;
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 8f;
            terrain.basemapDistance = 1200f;

            CreateRouteGuides(worldRoot.transform, terrain);
            GroundCriticalTransforms(worldRoot.transform, terrain);
            ConfigureNavMeshSurface(worldRoot.transform);

            terrain.Flush();
            EditorUtility.SetDirty(terrain);
            EditorUtility.SetDirty(terrainCollider);
            EditorUtility.SetDirty(productionTerrain);
            EditorSceneManager.MarkSceneDirty(openWorldScene);
            Undo.FlushUndoRecordObjects();

            List<string> problems = ValidateLoadedScene(openWorldScene);

            if (problems.Count > 0)
            {
                throw new InvalidOperationException(
                    "Production terrain validation failed before save:\n\n- " +
                    string.Join("\n- ", problems));
            }

            AssetDatabase.SaveAssets();

            if (!EditorSceneManager.SaveScene(openWorldScene, OpenWorldScene))
            {
                throw new InvalidOperationException(
                    "Unity could not save Bloodroot_OpenWorld after the terrain pass.");
            }

            buildCommitted = true;
            Undo.CollapseUndoOperations(undoGroup);

            problems = ValidateLoadedScene(openWorldScene);

            if (problems.Count > 0)
            {
                throw new InvalidOperationException(
                    "Production terrain validation failed:\n\n- " +
                    string.Join("\n- ", problems));
            }

            Selection.activeGameObject = terrain.gameObject;
            SceneView.RepaintAll();

            EditorUtility.DisplayDialog(
                "Production Terrain Pass Complete",
                "Bloodroot_OpenWorld now uses a separate production TerrainData with " +
                "Black Pines, Stillwater, Harrow, and Bloodroot Hollow landforms, " +
                "progression roads, drainage, and five surface zones. Critical markers " +
                "were grounded and a NavMesh Surface was configured.\n\n" +
                "The baseline TerrainData and pre-pass scene backup remain untouched. " +
                "Bake the NavMesh after production props are placed.",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            string recoveryMessage;

            if (!buildCommitted)
            {
                bool rollbackSucceeded = false;

                try
                {
                    if (sceneMutationStarted && undoGroup >= 0)
                    {
                        Undo.FlushUndoRecordObjects();
                        Undo.RevertAllDownToGroup(undoGroup);
                    }

                    if (!openedHere &&
                        openWorldScene.IsValid() &&
                        openWorldScene.isLoaded &&
                        openWorldScene.isDirty)
                    {
                        if (!EditorSceneManager.CloseScene(openWorldScene, true))
                        {
                            throw new InvalidOperationException(
                                "Unity could not close the rolled-back open-world scene.");
                        }

                        openWorldScene =
                            EditorSceneManager.OpenScene(
                                OpenWorldScene,
                                OpenSceneMode.Additive);

                        if (previousActiveWasOpenWorld)
                        {
                            SceneManager.SetActiveScene(openWorldScene);
                        }
                    }

                    IEnumerable<string> cleanupPaths =
                        new[] { ProductionTerrainPath }
                            .Concat(ProductionTerrainLayerPaths)
                            .Concat(
                                GeneratedTerrainOutputPaths.Where(
                                    path => !path.EndsWith(
                                        ".terrainlayer",
                                        StringComparison.OrdinalIgnoreCase)))
                            .Concat(new[] { SceneBackupPath });

                    List<string> cleanupFailures = new List<string>();

                    foreach (string outputPath in cleanupPaths.Distinct())
                    {
                        if (!string.IsNullOrEmpty(
                                AssetDatabase.AssetPathToGUID(outputPath)) &&
                            !AssetDatabase.DeleteAsset(outputPath))
                        {
                            cleanupFailures.Add(outputPath);
                        }
                    }

                    if (cleanupFailures.Count > 0)
                    {
                        throw new InvalidOperationException(
                            "Unity could not remove these partial outputs: " +
                            string.Join(", ", cleanupFailures));
                    }

                    AssetDatabase.SaveAssets();
                    rollbackSucceeded = true;
                }
                catch (Exception rollbackException)
                {
                    Debug.LogException(rollbackException);
                }

                recoveryMessage = rollbackSucceeded
                    ? "The incomplete attempt was rolled back; the builder can be retried."
                    : "Automatic rollback did not finish. Keep the scene backup and inspect " +
                      "the generated assets before retrying.";
            }
            else
            {
                recoveryMessage =
                    "The validated scene was saved before the later editor error. Use the " +
                    "Repair command to regenerate and revalidate it safely.";
            }

            EditorUtility.DisplayDialog(
                "Production Terrain Build Failed",
                exception.Message +
                "\n\nThe baseline TerrainData was not modified. " + recoveryMessage,
                "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();

            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(previousActiveScene);
            }

            if (openedHere && openWorldScene.IsValid() && openWorldScene.isLoaded)
            {
                EditorSceneManager.CloseScene(openWorldScene, true);
            }
        }
    }

    [MenuItem(
        "Bloodroot/Open World/Repair Production Terrain Pass",
        false,
        31)]
    public static void RepairProductionTerrainPass()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Terrain Repair Unavailable",
                "Exit Play Mode before repairing the production terrain.",
                "OK");
            return;
        }

        TerrainData productionTerrain =
            AssetDatabase.LoadAssetAtPath<TerrainData>(ProductionTerrainPath);

        if (productionTerrain == null)
        {
            EditorUtility.DisplayDialog(
                "Production Terrain Missing",
                "Build the production terrain before running its repair command.",
                "OK");
            return;
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        string repairBackupAbsolutePath =
            Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                ProductionTerrainRepairBackupPath);
        bool repairBackupExistsOnDisk =
            File.Exists(repairBackupAbsolutePath) ||
            File.Exists(repairBackupAbsolutePath + ".meta");

        if (!repairBackupExistsOnDisk &&
            !string.IsNullOrEmpty(
                AssetDatabase.AssetPathToGUID(ProductionTerrainRepairBackupPath)))
        {
            AssetDatabase.DeleteAsset(ProductionTerrainRepairBackupPath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        if (repairBackupExistsOnDisk ||
            AssetDatabase.LoadAssetAtPath<TerrainData>(
                ProductionTerrainRepairBackupPath) != null)
        {
            EditorUtility.DisplayDialog(
                "Incomplete Terrain Repair Detected",
                "A temporary terrain repair backup already exists. Inspect that recovery " +
                "asset before retrying; the repair command will not overwrite it.",
                "OK");
            return;
        }

        Scene openWorldScene = SceneManager.GetSceneByPath(OpenWorldScene);
        bool openedHere = !openWorldScene.IsValid() || !openWorldScene.isLoaded;
        Scene previousActiveScene = SceneManager.GetActiveScene();
        bool previousActiveWasOpenWorld =
            previousActiveScene.IsValid() &&
            previousActiveScene.path == OpenWorldScene;
        bool repairCommitted = false;
        bool sceneMutationStarted = false;
        int undoGroup = -1;

        if (!openedHere && openWorldScene.isDirty)
        {
            EditorUtility.DisplayDialog(
                "Unsaved Open-World Scene",
                "Save or discard the open Bloodroot_OpenWorld scene changes before " +
                "repairing the production terrain. Nothing was changed.",
                "OK");
            return;
        }

        try
        {
            if (openedHere)
            {
                openWorldScene =
                    EditorSceneManager.OpenScene(
                        OpenWorldScene,
                        OpenSceneMode.Additive);
            }

            SceneManager.SetActiveScene(openWorldScene);

            GameObject[] matchingWorldRoots =
                openWorldScene.GetRootGameObjects()
                    .Where(root => root.name == "Bloodroot_OpenWorld")
                    .ToArray();

            GameObject worldRoot = matchingWorldRoots.FirstOrDefault();

            Transform terrainTransform =
                worldRoot != null
                    ? worldRoot.transform.Find("_TERRAIN/Open World Terrain")
                    : null;

            Terrain terrain =
                terrainTransform != null
                    ? terrainTransform.GetComponent<Terrain>()
                    : null;

            TerrainCollider terrainCollider =
                terrainTransform != null
                    ? terrainTransform.GetComponent<TerrainCollider>()
                    : null;

            if (matchingWorldRoots.Length != 1 ||
                terrain == null ||
                terrainCollider == null)
            {
                throw new InvalidOperationException(
                    "The production scene must contain exactly one Bloodroot_OpenWorld " +
                    "root and a Terrain with a matching TerrainCollider.");
            }

            if (AssetDatabase.GetAssetPath(terrain.terrainData) !=
                    ProductionTerrainPath ||
                AssetDatabase.GetAssetPath(terrainCollider.terrainData) !=
                    ProductionTerrainPath)
            {
                throw new InvalidOperationException(
                    "The live Terrain and TerrainCollider must both reference the " +
                    "production TerrainData before repair.");
            }

            string[] requiredRepairPaths =
            {
                "_TERRAIN/Roads",
                "_TERRAIN/Rivers",
                "_CORE/Open World NavMesh Surface (Bake Later)",
                "_CORE/Return Truck (Travel Wiring Pending)",
                "AREA_00_BLACK_PINES_FOREST/World Arrival Spawn",
                "AREA_00_BLACK_PINES_FOREST/Exit Road",
                "AREA_01_STILLWATER_FEED_MILL/Area Spawn",
                "AREA_01_STILLWATER_FEED_MILL/Locked Entrance",
                "AREA_02_HARROW_ESTATE/Area Spawn",
                "AREA_02_HARROW_ESTATE/Locked Entrance",
                "AREA_03_BLOODROOT_HOLLOW/Boss Arena Spawn",
                "AREA_03_BLOODROOT_HOLLOW/Locked Entrance"
            };

            string missingRepairPath =
                requiredRepairPaths.FirstOrDefault(
                    path => worldRoot.transform.Find(path) == null);

            if (!string.IsNullOrEmpty(missingRepairPath))
            {
                throw new InvalidOperationException(
                    $"Required repair hierarchy path is missing: {missingRepairPath}.");
            }

            if (!AssetDatabase.CopyAsset(
                    ProductionTerrainPath,
                    ProductionTerrainRepairBackupPath))
            {
                throw new InvalidOperationException(
                    "Unity could not create the temporary production-terrain backup.");
            }

            AssetDatabase.ImportAsset(
                ProductionTerrainRepairBackupPath,
                ImportAssetOptions.ForceSynchronousImport);

            TerrainData repairBackup =
                AssetDatabase.LoadAssetAtPath<TerrainData>(
                    ProductionTerrainRepairBackupPath);

            if (repairBackup == null)
            {
                throw new InvalidOperationException(
                    "Unity could not load the temporary production-terrain backup.");
            }

            ConfigureProductionTerrainData(productionTerrain);

            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Repair Bloodroot production terrain");
            sceneMutationStarted = true;

            Undo.RecordObject(terrainTransform, "Repair Bloodroot production terrain");
            Undo.RecordObject(terrain, "Repair Bloodroot production terrain");
            Undo.RecordObject(terrainCollider, "Repair Bloodroot production terrain");

            terrainTransform.position = new Vector3(-700f, TerrainWorldY, -700f);
            terrain.terrainData = productionTerrain;
            terrainCollider.terrainData = productionTerrain;

            CreateRouteGuides(worldRoot.transform, terrain);
            GroundCriticalTransforms(worldRoot.transform, terrain);
            ConfigureNavMeshSurface(worldRoot.transform);

            terrain.Flush();
            EditorUtility.SetDirty(terrain);
            EditorUtility.SetDirty(terrainCollider);
            EditorUtility.SetDirty(productionTerrain);
            EditorSceneManager.MarkSceneDirty(openWorldScene);

            Undo.FlushUndoRecordObjects();

            List<string> problems = ValidateLoadedScene(openWorldScene);

            if (problems.Count > 0)
            {
                throw new InvalidOperationException(
                    "Production terrain validation failed before repair save:\n\n- " +
                    string.Join("\n- ", problems));
            }

            AssetDatabase.SaveAssets();

            if (!EditorSceneManager.SaveScene(openWorldScene, OpenWorldScene))
            {
                throw new InvalidOperationException(
                    "Unity could not save the repaired Bloodroot_OpenWorld scene.");
            }

            repairCommitted = true;
            Undo.CollapseUndoOperations(undoGroup);

            if (!AssetDatabase.DeleteAsset(ProductionTerrainRepairBackupPath))
            {
                Debug.LogWarning(
                    $"Repair succeeded, but Unity could not remove " +
                    $"{ProductionTerrainRepairBackupPath}.");
            }

            Selection.activeGameObject = terrain.gameObject;
            SceneView.RepaintAll();

            EditorUtility.DisplayDialog(
                "Production Terrain Repair Complete",
                "The terrain is matte and naturally varied, Harrow Estate overlooks the " +
                "world from its hilltop, Black Pines Creek follows a winding drainage " +
                "course, Bloodroot Hollow has an irregular playable rim, and the " +
                "strengthened validation passed.",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            string recoveryMessage;

            if (!repairCommitted)
            {
                bool rollbackSucceeded = false;

                try
                {
                    if (sceneMutationStarted && undoGroup >= 0)
                    {
                        Undo.FlushUndoRecordObjects();
                        Undo.RevertAllDownToGroup(undoGroup);
                    }

                    TerrainData repairBackup =
                        AssetDatabase.LoadAssetAtPath<TerrainData>(
                            ProductionTerrainRepairBackupPath);

                    if (repairBackup != null)
                    {
                        EditorUtility.CopySerialized(repairBackup, productionTerrain);
                        productionTerrain.name =
                            Path.GetFileNameWithoutExtension(ProductionTerrainPath);
                        EditorUtility.SetDirty(productionTerrain);
                        AssetDatabase.SaveAssets();
                    }

                    if (!openedHere &&
                        openWorldScene.IsValid() &&
                        openWorldScene.isLoaded &&
                        openWorldScene.isDirty)
                    {
                        if (SceneManager.sceneCount == 1)
                        {
                            openWorldScene =
                                EditorSceneManager.OpenScene(
                                    OpenWorldScene,
                                    OpenSceneMode.Single);
                        }
                        else
                        {
                            if (!EditorSceneManager.CloseScene(openWorldScene, true))
                            {
                                throw new InvalidOperationException(
                                    "Unity could not close the rolled-back " +
                                    "open-world scene.");
                            }

                            openWorldScene =
                                EditorSceneManager.OpenScene(
                                    OpenWorldScene,
                                    OpenSceneMode.Additive);
                        }

                        if (previousActiveWasOpenWorld)
                        {
                            SceneManager.SetActiveScene(openWorldScene);
                        }
                    }

                    if (!string.IsNullOrEmpty(
                            AssetDatabase.AssetPathToGUID(
                                ProductionTerrainRepairBackupPath)) &&
                        !AssetDatabase.DeleteAsset(
                            ProductionTerrainRepairBackupPath))
                    {
                        throw new InvalidOperationException(
                            "Unity could not remove the temporary repair backup.");
                    }

                    rollbackSucceeded = true;
                }
                catch (Exception rollbackException)
                {
                    Debug.LogException(rollbackException);
                }

                recoveryMessage = rollbackSucceeded
                    ? "The production TerrainData and open scene were rolled back."
                    : "Automatic rollback did not finish. Keep the temporary terrain " +
                      "backup and inspect it before retrying.";
            }
            else
            {
                recoveryMessage =
                    "The validated repair was saved before the later editor error.";
            }

            EditorUtility.DisplayDialog(
                "Production Terrain Repair Failed",
                exception.Message + "\n\n" + recoveryMessage,
                "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();

            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(previousActiveScene);
            }

            if (openedHere && openWorldScene.IsValid() && openWorldScene.isLoaded)
            {
                EditorSceneManager.CloseScene(openWorldScene, true);
            }
        }
    }

    [MenuItem(
        "Bloodroot/Open World/Recover Incomplete Terrain Repair",
        false,
        33)]
    public static void RecoverIncompleteTerrainRepair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Terrain Recovery Unavailable",
                "Exit Play Mode before recovering the production terrain.",
                "OK");
            return;
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        TerrainData repairBackup =
            AssetDatabase.LoadAssetAtPath<TerrainData>(
                ProductionTerrainRepairBackupPath);
        TerrainData productionTerrain =
            AssetDatabase.LoadAssetAtPath<TerrainData>(ProductionTerrainPath);

        if (repairBackup == null || productionTerrain == null)
        {
            EditorUtility.DisplayDialog(
                "Terrain Recovery Not Available",
                "The temporary repair backup or production TerrainData is missing.",
                "OK");
            return;
        }

        try
        {
            EditorUtility.CopySerialized(repairBackup, productionTerrain);
            productionTerrain.name =
                Path.GetFileNameWithoutExtension(ProductionTerrainPath);
            EditorUtility.SetDirty(productionTerrain);
            AssetDatabase.SaveAssets();

            Scene openWorldScene = SceneManager.GetSceneByPath(OpenWorldScene);

            if (openWorldScene.IsValid() &&
                openWorldScene.isLoaded &&
                openWorldScene.isDirty)
            {
                bool wasActive = SceneManager.GetActiveScene() == openWorldScene;

                if (SceneManager.sceneCount == 1)
                {
                    openWorldScene =
                        EditorSceneManager.OpenScene(
                            OpenWorldScene,
                            OpenSceneMode.Single);
                }
                else
                {
                    if (!EditorSceneManager.CloseScene(openWorldScene, true))
                    {
                        throw new InvalidOperationException(
                            "Unity could not close the incomplete open-world scene.");
                    }

                    openWorldScene =
                        EditorSceneManager.OpenScene(
                            OpenWorldScene,
                            OpenSceneMode.Additive);
                }

                if (wasActive)
                {
                    SceneManager.SetActiveScene(openWorldScene);
                }
            }

            if (!AssetDatabase.DeleteAsset(ProductionTerrainRepairBackupPath))
            {
                throw new InvalidOperationException(
                    "Unity restored the production terrain but could not delete " +
                    "the temporary recovery asset.");
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            EditorUtility.DisplayDialog(
                "Terrain Recovery Complete",
                "The production TerrainData and open-world scene were restored " +
                "to their pre-repair state. The temporary recovery asset was removed.",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Terrain Recovery Failed",
                exception.Message +
                "\n\nKeep the temporary recovery asset for manual inspection.",
                "OK");
        }
    }

    [MenuItem(
        "Bloodroot/Open World/Validate Production Terrain Pass",
        false,
        32)]
    public static void ValidateProductionTerrainPass()
    {
        Scene scene = SceneManager.GetSceneByPath(OpenWorldScene);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        Scene previousActiveScene = SceneManager.GetActiveScene();

        try
        {
            if (openedHere)
            {
                scene =
                    EditorSceneManager.OpenScene(
                        OpenWorldScene,
                        OpenSceneMode.Additive);
            }

            List<string> problems = ValidateLoadedScene(scene);

            if (problems.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Production Terrain Validation Passed",
                    "The production TerrainData, relief, five surface layers, scene backup, " +
                    "grounded route markers, and NavMesh Surface all passed validation.",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "Production Terrain Validation Failed",
                    "- " + string.Join("\n- ", problems),
                    "OK");
            }
        }
        finally
        {
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(previousActiveScene);
            }

            if (openedHere && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static void ConfigureProductionTerrainData(TerrainData terrainData)
    {
        EditorUtility.DisplayProgressBar(
            "Bloodroot Production Terrain",
            "Configuring terrain resolution...",
            0.05f);

        if (terrainData.heightmapResolution != HeightmapResolution)
        {
            terrainData.heightmapResolution = HeightmapResolution;
        }

        terrainData.size = new Vector3(TerrainWidth, TerrainHeight, TerrainLength);

        // Reassigning an unchanged alphamap resolution releases the current
        // control textures before SetAlphamaps can repaint them. Avoid that
        // native "splatdatabase alphamap is null" error on deterministic repairs.
        if (terrainData.alphamapResolution != AlphamapResolution)
        {
            terrainData.alphamapResolution = AlphamapResolution;
        }

        if (terrainData.baseMapResolution != 1024)
        {
            terrainData.baseMapResolution = 1024;
        }

        float[,] heights = GenerateHeightmap();
        terrainData.SetHeights(0, 0, heights);

        TerrainLayer[] layers = GetOrCreateTerrainLayers();
        TerrainLayer[] currentLayers = terrainData.terrainLayers;
        bool layersMatch =
            currentLayers != null && currentLayers.Length == layers.Length;

        for (int index = 0; layersMatch && index < layers.Length; index++)
        {
            layersMatch = currentLayers[index] == layers[index];
        }

        int expectedControlTextureCount = (layers.Length + 3) / 4;
        Texture2D[] currentControlTextures = terrainData.alphamapTextures;
        bool controlTexturesComplete =
            terrainData.alphamapTextureCount == expectedControlTextureCount &&
            currentControlTextures != null &&
            currentControlTextures.Length == expectedControlTextureCount &&
            currentControlTextures.All(texture => texture != null);

        // Reassigning the identical layer array rebuilds Unity's internal splat
        // database and briefly leaves its control textures null. Layer assets are
        // updated in place, so only replace this array when its membership changed.
        // A legacy broken asset can already contain null control textures; clearing
        // and restoring its layers forces Unity to allocate fresh control textures.
        if (!layersMatch || !controlTexturesComplete)
        {
            terrainData.terrainLayers = Array.Empty<TerrainLayer>();
            terrainData.terrainLayers = layers;
        }

        Texture2D[] rebuiltControlTextures = terrainData.alphamapTextures;

        if (terrainData.alphamapTextureCount != expectedControlTextureCount ||
            rebuiltControlTextures == null ||
            rebuiltControlTextures.Length != expectedControlTextureCount ||
            rebuiltControlTextures.Any(texture => texture == null))
        {
            throw new InvalidOperationException(
                "Unity could not allocate the production terrain's alphamap " +
                "control textures.");
        }

        float[,,] alphamaps = GenerateAlphamaps(terrainData, layers.Length);
        terrainData.SetAlphamaps(0, 0, alphamaps);

        EditorUtility.SetDirty(terrainData);
    }

    private static float[,] GenerateHeightmap()
    {
        float[,] heights =
            new float[HeightmapResolution, HeightmapResolution];

        for (int z = 0; z < HeightmapResolution; z++)
        {
            if ((z & 31) == 0)
            {
                EditorUtility.DisplayProgressBar(
                    "Bloodroot Production Terrain",
                    "Sculpting region-scale landforms...",
                    0.08f + 0.47f * z / (HeightmapResolution - 1f));
            }

            float normalizedZ = z / (HeightmapResolution - 1f);
            float worldZ = -700f + normalizedZ * TerrainLength;

            for (int x = 0; x < HeightmapResolution; x++)
            {
                float normalizedX = x / (HeightmapResolution - 1f);
                float worldX = -700f + normalizedX * TerrainWidth;

                float warpX =
                    (Mathf.PerlinNoise(
                        worldX * 0.0017f + 14.2f,
                        worldZ * 0.0017f + 83.6f) - 0.5f) * 75f;

                float warpZ =
                    (Mathf.PerlinNoise(
                        worldX * 0.0017f + 61.8f,
                        worldZ * 0.0017f + 27.4f) - 0.5f) * 75f;

                float warpedX = worldX + warpX;
                float warpedZ = worldZ + warpZ;

                float broadNoise =
                    (Mathf.PerlinNoise(
                        warpedX * 0.0026f + 19.7f,
                        warpedZ * 0.0026f + 41.3f) - 0.5f) * 7f;

                float rollingNoise =
                    (Mathf.PerlinNoise(
                        warpedX * 0.0068f + 93.4f,
                        warpedZ * 0.0068f + 6.8f) - 0.5f) * 4.5f;

                float detailNoise =
                    (Mathf.PerlinNoise(
                        warpedX * 0.018f + 73.1f,
                        warpedZ * 0.018f + 12.8f) - 0.5f) * 2f;

                float microNoise =
                    (Mathf.PerlinNoise(
                        warpedX * 0.052f + 5.7f,
                        warpedZ * 0.052f + 96.2f) - 0.5f) * 0.8f;

                float ridgeSample =
                    Mathf.PerlinNoise(
                        warpedX * 0.0045f + 47.5f,
                        warpedZ * 0.0045f + 31.9f);

                float ridgedNoise =
                    (Mathf.Pow(
                        1f - Mathf.Abs(ridgeSample * 2f - 1f),
                        2f) - 0.33f) * 2.2f;

                float worldHeight =
                    7.5f +
                    broadNoise +
                    rollingNoise +
                    detailNoise +
                    microNoise +
                    ridgedNoise;

                worldHeight -=
                    5.5f * EllipseMask(worldX, worldZ, -350f, -150f, 230f, 190f);

                worldHeight +=
                    19f * EllipseMask(worldX, worldZ, -575f, -160f, 170f, 390f);

                worldHeight +=
                    13f * EllipseMask(worldX, worldZ, -300f, -415f, 390f, 145f);

                worldHeight +=
                    10f * EllipseMask(worldX, worldZ, -315f, 70f, 310f, 125f);

                worldHeight -=
                    4.5f * EllipseMask(worldX, worldZ, 320f, -40f, 285f, 235f);

                float estateMass =
                    EllipseMask(worldX, worldZ, 35f, 365f, 300f, 250f);

                float estateRoughness =
                    0.82f +
                    Mathf.PerlinNoise(
                        worldX * 0.005f + 32.1f,
                        worldZ * 0.005f + 78.6f) * 0.18f;

                float estateNorthTaper =
                    1f -
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.InverseLerp(450f, 525f, worldZ));

                float estateContribution =
                    38f * estateMass * estateRoughness +
                    7f * EllipseMask(
                        worldX,
                        worldZ,
                        -105f,
                        390f,
                        240f,
                        120f) +
                    5f * EllipseMask(
                        worldX,
                        worldZ,
                        150f,
                        320f,
                        210f,
                        95f);

                worldHeight += estateContribution * estateNorthTaper;

                float hollowDx = (worldX - 50f) / 1.04f;
                float hollowDz = worldZ - 600f;
                float hollowDistance =
                    Mathf.Sqrt(hollowDx * hollowDx + hollowDz * hollowDz);
                float hollowAngle = Mathf.Atan2(hollowDz, hollowDx);
                float hollowShapeNoise =
                    Mathf.PerlinNoise(
                        worldX * 0.0065f + 54.8f,
                        worldZ * 0.0065f + 11.3f);
                float hollowWidthNoise =
                    Mathf.PerlinNoise(
                        worldX * 0.008f + 8.9f,
                        worldZ * 0.008f + 69.7f);
                float northCompression =
                    Mathf.Clamp01(Mathf.Sin(hollowAngle));
                northCompression *= northCompression;

                float hollowRimRadius =
                    143f +
                    Mathf.Sin(hollowAngle * 3f + 0.4f) * 13f +
                    Mathf.Sin(hollowAngle * 5f - 1.2f) * 7f +
                    (hollowShapeNoise - 0.5f) * 28f -
                    northCompression * 45f;

                float hollowRimWidth =
                    42f + (hollowWidthNoise - 0.5f) * 18f;

                float hollowRing =
                    Mathf.Exp(
                        -Mathf.Pow(
                            (hollowDistance - hollowRimRadius) /
                            Mathf.Max(24f, hollowRimWidth),
                            2f));

                float hollowEntrance =
                    EllipseMask(worldX, worldZ, 100f, 455f, 75f, 90f);
                float hollowRimStrength = 22f + hollowShapeNoise * 12f;
                worldHeight +=
                    hollowRing *
                    hollowRimStrength *
                    (1f - hollowEntrance * 0.78f);

                float edgeX =
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.InverseLerp(575f, 700f, Mathf.Abs(worldX)));

                float southEdge =
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.InverseLerp(560f, 700f, -worldZ));

                worldHeight += edgeX * 16f + southEdge * 12f;

                float blackPinesPad =
                    4f +
                    (Mathf.PerlinNoise(
                        worldX * 0.045f + 24.1f,
                        worldZ * 0.045f + 5.6f) - 0.5f) * 0.36f;

                worldHeight =
                    BlendEllipseToHeight(
                        worldHeight,
                        worldX,
                        worldZ,
                        -350f,
                        -150f,
                        22f,
                        66f,
                        blackPinesPad);

                float stillwaterPad =
                    8f +
                    (Mathf.PerlinNoise(
                        worldX * 0.038f + 79.4f,
                        worldZ * 0.038f + 37.2f) - 0.5f) * 0.44f;

                worldHeight =
                    BlendEllipseToHeight(
                        worldHeight,
                        worldX,
                        worldZ,
                        335f,
                        -30f,
                        38f,
                        102f,
                        stillwaterPad,
                        0.8f);

                float estateBench =
                    64f +
                    (Mathf.PerlinNoise(
                        worldX * 0.041f + 17.7f,
                        worldZ * 0.041f + 88.2f) - 0.5f) * 0.7f;

                worldHeight =
                    BlendEllipseToHeight(
                        worldHeight,
                        worldX,
                        worldZ,
                        40f,
                        360f,
                        22f,
                        76f,
                        estateBench,
                        0.82f);

                float estateHousePad =
                    68f +
                    (Mathf.PerlinNoise(
                        worldX * 0.038f + 82.7f,
                        worldZ * 0.038f + 16.4f) - 0.5f) * 0.45f;

                worldHeight =
                    BlendEllipseToHeight(
                        worldHeight,
                        worldX,
                        worldZ,
                        -10f,
                        395f,
                        24f,
                        55f,
                        estateHousePad,
                        0.78f);

                float hollowFloorWarp =
                    (Mathf.PerlinNoise(
                        worldX * 0.008f + 35.9f,
                        worldZ * 0.008f + 3.4f) - 0.5f) * 12f;
                float hollowFloorDx = worldX - 50f + hollowFloorWarp;
                float hollowFloorDz = worldZ - 600f - hollowFloorWarp * 0.55f;
                float hollowFloorDistance =
                    Mathf.Sqrt(
                        hollowFloorDx * hollowFloorDx +
                        hollowFloorDz * hollowFloorDz);

                float hollowFloorNoise =
                    (Mathf.PerlinNoise(
                        worldX * 0.022f + 67.2f,
                        worldZ * 0.022f + 42.8f) - 0.5f) * 1.15f +
                    (Mathf.PerlinNoise(
                        worldX * 0.061f + 6.3f,
                        worldZ * 0.061f + 91.5f) - 0.5f) * 0.35f;

                float hollowFloorTarget =
                    9f +
                    hollowFloorNoise +
                    (worldX - 50f) * 0.002f +
                    (worldZ - 600f) * 0.001f;

                float hollowFloorInfluence =
                    DistanceMask(hollowFloorDistance, 28f, 88f) * 0.94f;

                worldHeight =
                    Mathf.Lerp(
                        worldHeight,
                        hollowFloorTarget,
                        hollowFloorInfluence);

                float creekTarget;
                float creekDistance =
                    DistanceToRoute(
                        worldX,
                        worldZ,
                        BlackPinesCreek,
                        out creekTarget);

                float creekWidthNoise =
                    Mathf.PerlinNoise(
                        worldX * 0.009f + 45.8f,
                        worldZ * 0.009f + 22.6f);

                float adjustedCreekDistance =
                    creekDistance * (0.78f + creekWidthNoise * 0.44f);

                float floodplainInfluence =
                    DistanceMask(adjustedCreekDistance, 7f, 34f);
                float floodplainTarget =
                    creekTarget +
                    Mathf.Lerp(
                        0.25f,
                        3.8f,
                        Mathf.Clamp01(adjustedCreekDistance / 34f));
                float loweredFloodplain =
                    Mathf.Min(worldHeight, floodplainTarget);

                worldHeight =
                    Mathf.Lerp(
                        worldHeight,
                        loweredFloodplain,
                        floodplainInfluence * 0.62f);

                float creekBankOuter =
                    DistanceMask(adjustedCreekDistance, 8f, 30f);
                float creekBankInner =
                    DistanceMask(adjustedCreekDistance, 3f, 10f);
                float creekBankBand =
                    Mathf.Max(0f, creekBankOuter - creekBankInner);
                worldHeight +=
                    creekBankBand * (0.35f + creekWidthNoise * 0.35f);

                float channelOuter = 15f + creekWidthNoise * 8f;
                float creekInfluence =
                    DistanceMask(
                        adjustedCreekDistance,
                        2.25f,
                        channelOuter);
                worldHeight =
                    Mathf.Lerp(
                        worldHeight,
                        creekTarget - 0.2f,
                        creekInfluence * 0.98f);

                float roadTarget;
                float roadDistance =
                    DistanceToRoute(
                        worldX,
                        worldZ,
                        ProgressionRoad,
                        out roadTarget);

                float roadShoulder =
                    DistanceMask(roadDistance, 6f, 25f) * 0.72f;
                float roadCore = DistanceMask(roadDistance, 3f, 8f);
                float roadInfluence =
                    Mathf.Lerp(roadShoulder, 0.985f, roadCore);
                worldHeight =
                    Mathf.Lerp(worldHeight, roadTarget, roadInfluence);

                worldHeight = Mathf.Clamp(worldHeight, -2f, 92f);
                heights[z, x] =
                    Mathf.Clamp01((worldHeight - TerrainWorldY) / TerrainHeight);
            }
        }

        return heights;
    }

    private static TerrainLayer[] GetOrCreateTerrainLayers()
    {
        TerrainLayer sourceGround =
            AssetDatabase.LoadAssetAtPath<TerrainLayer>(ExistingGroundLayerPath);

        if (sourceGround == null)
        {
            throw new InvalidOperationException(
                $"Missing baseline ground layer: {ExistingGroundLayerPath}");
        }

        TerrainLayer forestLayer =
            AssetDatabase.LoadAssetAtPath<TerrainLayer>(ProductionGroundLayerPath);

        if (forestLayer == null)
        {
            if (!AssetDatabase.CopyAsset(
                    ExistingGroundLayerPath,
                    ProductionGroundLayerPath))
            {
                throw new InvalidOperationException(
                    "Unity could not copy the baseline ground layer.");
            }

            forestLayer =
                AssetDatabase.LoadAssetAtPath<TerrainLayer>(ProductionGroundLayerPath);
        }

        forestLayer.name = "ForestLoam";
        forestLayer.tileSize = new Vector2(6f, 6f);
        forestLayer.metallic = 0f;
        forestLayer.specular = Color.black;
        forestLayer.smoothness = ProductionLayerSmoothness[0];
        forestLayer.smoothnessSource =
            TerrainLayerSmoothnessSource.ConstantOnly;
        forestLayer.maskMapTexture = null;
        forestLayer.normalScale = 0.65f;
        EditorUtility.SetDirty(forestLayer);

        TerrainLayer wetMud =
            GetOrCreateProceduralLayer(
                "WetMud",
                new Color(0.055f, 0.043f, 0.032f),
                new Color(0.19f, 0.145f, 0.09f),
                new Vector2(4f, 4f),
                ProductionLayerSmoothness[1]);

        TerrainLayer gravel =
            GetOrCreateProceduralLayer(
                "GravelRoad",
                new Color(0.16f, 0.15f, 0.13f),
                new Color(0.43f, 0.40f, 0.34f),
                new Vector2(3.5f, 3.5f),
                ProductionLayerSmoothness[2],
                true);

        TerrainLayer clayRock =
            GetOrCreateProceduralLayer(
                "ExposedClayRock",
                new Color(0.105f, 0.07f, 0.045f),
                new Color(0.39f, 0.235f, 0.14f),
                new Vector2(7f, 7f),
                ProductionLayerSmoothness[3]);

        TerrainLayer corrupted =
            GetOrCreateProceduralLayer(
                "BloodrootCorruption",
                new Color(0.035f, 0.008f, 0.012f),
                new Color(0.24f, 0.018f, 0.025f),
                new Vector2(5f, 5f),
                ProductionLayerSmoothness[4]);

        return new[]
        {
            forestLayer,
            wetMud,
            gravel,
            clayRock,
            corrupted
        };
    }

    private static TerrainLayer GetOrCreateProceduralLayer(
        string assetName,
        Color darkColor,
        Color lightColor,
        Vector2 tileSize,
        float smoothness,
        bool addFlecks = false)
    {
        string texturePath =
            $"{GeneratedMaterialFolder}/{assetName}_Albedo.asset";

        Texture2D texture =
            AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);

        const int textureSize = 128;
        string textureObjectName = Path.GetFileNameWithoutExtension(texturePath);

        if (texture == null)
        {
            texture =
                new Texture2D(
                    textureSize,
                    textureSize,
                    TextureFormat.RGBA32,
                    true,
                    false)
                {
                    name = textureObjectName,
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Trilinear,
                    anisoLevel = 4
                };
            AssetDatabase.CreateAsset(texture, texturePath);
        }

        texture.name = textureObjectName;
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Trilinear;
        texture.anisoLevel = 4;

        Color[] pixels = new Color[textureSize * textureSize];

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float macro =
                    Mathf.PerlinNoise(x * 0.028f + 13.4f, y * 0.028f + 76.2f);

                float broad =
                    Mathf.PerlinNoise(x * 0.085f + 7.2f, y * 0.085f + 18.4f);

                float detail =
                    Mathf.PerlinNoise(x * 0.31f + 51.7f, y * 0.31f + 2.9f);

                float grain =
                    Mathf.PerlinNoise(x * 0.67f + 91.3f, y * 0.67f + 44.6f);

                float blend =
                    Mathf.Clamp01(
                        macro * 0.32f +
                        broad * 0.42f +
                        detail * 0.2f +
                        grain * 0.06f);

                Color color = Color.Lerp(darkColor, lightColor, blend);

                if (addFlecks && detail > 0.7f && grain > 0.54f)
                {
                    color = Color.Lerp(color, lightColor * 1.12f, 0.48f);
                }

                color.a = 1f;
                pixels[y * textureSize + x] = color;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(true, false);
        EditorUtility.SetDirty(texture);

        string layerPath =
            $"{GeneratedMaterialFolder}/{assetName}.terrainlayer";

        TerrainLayer layer =
            AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);

        if (layer == null)
        {
            layer = new TerrainLayer
            {
                name = assetName
            };

            AssetDatabase.CreateAsset(layer, layerPath);
        }

        layer.name = assetName;
        layer.diffuseTexture = texture;
        layer.tileSize = tileSize;
        layer.metallic = 0f;
        layer.specular = Color.black;
        layer.smoothness = smoothness;
        layer.smoothnessSource =
            TerrainLayerSmoothnessSource.ConstantOnly;
        layer.maskMapTexture = null;
        layer.normalScale = 1f;
        EditorUtility.SetDirty(layer);

        return layer;
    }

    private static float[,,] GenerateAlphamaps(
        TerrainData terrainData,
        int layerCount)
    {
        int resolution = terrainData.alphamapResolution;
        float[,,] maps = new float[resolution, resolution, layerCount];

        for (int z = 0; z < resolution; z++)
        {
            if ((z & 15) == 0)
            {
                EditorUtility.DisplayProgressBar(
                    "Bloodroot Production Terrain",
                    "Painting regional terrain surfaces...",
                    0.58f + 0.34f * z / (resolution - 1f));
            }

            float normalizedZ = z / (resolution - 1f);
            float worldZ = -700f + normalizedZ * TerrainLength;

            for (int x = 0; x < resolution; x++)
            {
                float normalizedX = x / (resolution - 1f);
                float worldX = -700f + normalizedX * TerrainWidth;

                float worldHeight =
                    TerrainWorldY +
                    terrainData.GetInterpolatedHeight(normalizedX, normalizedZ);

                float slope =
                    terrainData.GetSteepness(normalizedX, normalizedZ);

                float roadTarget;
                float roadDistance =
                    DistanceToRoute(
                        worldX,
                        worldZ,
                        ProgressionRoad,
                        out roadTarget);

                float creekTarget;
                float creekDistance =
                    DistanceToRoute(
                        worldX,
                        worldZ,
                        BlackPinesCreek,
                        out creekTarget);

                float blackPines =
                    EllipseMask(worldX, worldZ, -350f, -150f, 360f, 310f);

                float stillwater =
                    EllipseMask(worldX, worldZ, 320f, -40f, 300f, 250f);

                float hollowDistance =
                    Vector2.Distance(
                        new Vector2(worldX, worldZ),
                        new Vector2(50f, 600f));

                float hollowAngle =
                    Mathf.Atan2(worldZ - 600f, worldX - 50f);
                float hollowNorth =
                    Mathf.Clamp01(Mathf.Sin(hollowAngle));
                hollowNorth *= hollowNorth;
                float hollowBoundary =
                    225f -
                    hollowNorth * 48f +
                    Mathf.Sin(hollowAngle * 3f + 0.4f) * 14f +
                    (Mathf.PerlinNoise(
                        worldX * 0.007f + 54.8f,
                        worldZ * 0.007f + 11.3f) - 0.5f) * 32f;
                float hollow =
                    DistanceMask(
                        hollowDistance,
                        82f,
                        Mathf.Max(145f, hollowBoundary));
                float road = DistanceMask(roadDistance, 7f, 21f);
                float creek = DistanceMask(creekDistance, 3f, 18f);
                float lowGround = 1f - Mathf.InverseLerp(1f, 8f, worldHeight);
                float steep = Mathf.InverseLerp(22f, 42f, slope);
                float highGround = Mathf.InverseLerp(30f, 62f, worldHeight);

                float surfaceNoise =
                    Mathf.PerlinNoise(
                        worldX * 0.021f + 8.4f,
                        worldZ * 0.021f + 61.2f);

                float forestWeight =
                    0.42f + blackPines * 0.9f + surfaceNoise * 0.18f;

                float wetMudWeight =
                    0.05f + creek * 2.4f + lowGround * 0.8f + stillwater * 0.2f;

                float gravelWeight =
                    0.025f + road * 3.1f * (1f - hollow * 0.9f) +
                    EllipseMask(worldX, worldZ, 335f, -30f, 95f, 72f) * 1.1f;

                float clayRockWeight =
                    0.03f + steep * 2.4f + highGround * 0.45f;

                float corruptionWeight =
                    0.01f + hollow * (0.9f + surfaceNoise * 0.75f);

                float total =
                    forestWeight +
                    wetMudWeight +
                    gravelWeight +
                    clayRockWeight +
                    corruptionWeight;

                maps[z, x, 0] = forestWeight / total;
                maps[z, x, 1] = wetMudWeight / total;
                maps[z, x, 2] = gravelWeight / total;
                maps[z, x, 3] = clayRockWeight / total;
                maps[z, x, 4] = corruptionWeight / total;
            }
        }

        return maps;
    }

    private static void CreateRouteGuides(Transform worldRoot, Terrain terrain)
    {
        Transform roads = worldRoot.Find("_TERRAIN/Roads");
        Transform rivers = worldRoot.Find("_TERRAIN/Rivers");

        if (roads == null || rivers == null)
        {
            throw new InvalidOperationException(
                "The required _TERRAIN/Roads or _TERRAIN/Rivers root is missing.");
        }

        CreateRouteGuide(
            roads,
            "Primary Progression Road",
            ProgressionRoad,
            terrain);

        CreateRouteGuide(
            rivers,
            "Black Pines Creek",
            BlackPinesCreek,
            terrain);
    }

    private static void CreateRouteGuide(
        Transform parent,
        string routeName,
        RouteNode[] nodes,
        Terrain terrain)
    {
        Transform existing = parent.Find(routeName);

        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        GameObject route = new GameObject(routeName);
        Undo.RegisterCreatedObjectUndo(route, "Create terrain route guide");
        route.transform.SetParent(parent, false);

        for (int index = 0; index < nodes.Length; index++)
        {
            GameObject waypoint =
                new GameObject($"Waypoint {index:00}");

            Undo.RegisterCreatedObjectUndo(
                waypoint,
                "Create terrain route waypoint");

            waypoint.transform.SetParent(route.transform, false);
            waypoint.transform.position =
                new Vector3(
                    nodes[index].position.x,
                    GetTerrainWorldHeight(
                        terrain,
                        nodes[index].position.x,
                        nodes[index].position.y) + 0.15f,
                    nodes[index].position.y);
        }
    }

    private static void GroundCriticalTransforms(
        Transform worldRoot,
        Terrain terrain)
    {
        GroundPath(worldRoot, "_CORE/PlayerSpawnPos", terrain, 0.25f);
        GroundPath(worldRoot, "_CORE/Player", terrain, 1f);
        GroundPath(
            worldRoot,
            "_CORE/Return Truck (Travel Wiring Pending)",
            terrain,
            0.1f);

        GroundPath(
            worldRoot,
            "AREA_00_BLACK_PINES_FOREST/World Arrival Spawn",
            terrain,
            0.25f);

        GroundPath(
            worldRoot,
            "AREA_00_BLACK_PINES_FOREST/Exit Road",
            terrain,
            0f);

        GroundPath(
            worldRoot,
            "AREA_01_STILLWATER_FEED_MILL/Area Spawn",
            terrain,
            0.25f);

        GroundPath(
            worldRoot,
            "AREA_01_STILLWATER_FEED_MILL/Locked Entrance",
            terrain,
            0f);

        GroundPath(
            worldRoot,
            "AREA_02_HARROW_ESTATE/Area Spawn",
            terrain,
            0.25f);

        GroundPath(
            worldRoot,
            "AREA_02_HARROW_ESTATE/Locked Entrance",
            terrain,
            0f);

        GroundPathAtWorldPosition(
            worldRoot,
            "AREA_03_BLOODROOT_HOLLOW/Boss Arena Spawn",
            terrain,
            50f,
            600f,
            0.25f);

        GroundPath(
            worldRoot,
            "AREA_03_BLOODROOT_HOLLOW/Locked Entrance",
            terrain,
            0f);
    }

    private static void GroundPath(
        Transform worldRoot,
        string path,
        Terrain terrain,
        float clearance)
    {
        Transform target = worldRoot.Find(path);

        if (target == null)
        {
            Debug.LogWarning($"Terrain grounding skipped missing path: {path}");
            return;
        }

        Undo.RecordObject(target, "Ground terrain marker");
        Vector3 position = target.position;
        position.y =
            GetTerrainWorldHeight(terrain, position.x, position.z) + clearance;
        target.position = position;
    }

    private static void GroundPathAtWorldPosition(
        Transform worldRoot,
        string path,
        Terrain terrain,
        float worldX,
        float worldZ,
        float clearance)
    {
        Transform target = worldRoot.Find(path);

        if (target == null)
        {
            Debug.LogWarning($"Terrain grounding skipped missing path: {path}");
            return;
        }

        Undo.RecordObject(target, "Place terrain marker");
        target.position =
            new Vector3(
                worldX,
                GetTerrainWorldHeight(terrain, worldX, worldZ) + clearance,
                worldZ);
    }

    private static void ConfigureNavMeshSurface(Transform worldRoot)
    {
        Transform navRoot =
            worldRoot.Find("_CORE/Open World NavMesh Surface (Bake Later)");

        if (navRoot == null)
        {
            throw new InvalidOperationException(
                "The open-world NavMesh Surface placeholder is missing.");
        }

        NavMeshSurface surface = navRoot.GetComponent<NavMeshSurface>();

        if (surface == null)
        {
            surface = Undo.AddComponent<NavMeshSurface>(navRoot.gameObject);
        }

        Undo.RecordObject(surface, "Configure open-world NavMesh Surface");
        surface.collectObjects = CollectObjects.All;
        surface.layerMask = ~0;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.ignoreNavMeshAgent = true;
        surface.ignoreNavMeshObstacle = true;
        surface.buildHeightMesh = false;
        EditorUtility.SetDirty(surface);
    }

    private static List<string> ValidateLoadedScene(Scene scene)
    {
        List<string> problems = new List<string>();

        if (!scene.IsValid() || !scene.isLoaded)
        {
            problems.Add("Bloodroot_OpenWorld is not loaded.");
            return problems;
        }

        GameObject[] matchingWorldRoots =
            scene.GetRootGameObjects()
                .Where(root => root.name == "Bloodroot_OpenWorld")
                .ToArray();

        GameObject worldRoot = matchingWorldRoots.FirstOrDefault();

        if (matchingWorldRoots.Length != 1)
        {
            problems.Add(
                $"Expected exactly one Bloodroot_OpenWorld root, found " +
                $"{matchingWorldRoots.Length}.");
            return problems;
        }

        Transform terrainTransform =
            worldRoot.transform.Find("_TERRAIN/Open World Terrain");

        Terrain terrain =
            terrainTransform != null
                ? terrainTransform.GetComponent<Terrain>()
                : null;

        if (terrain == null || terrain.terrainData == null)
        {
            problems.Add("Missing production Terrain component or TerrainData.");
            return problems;
        }

        string terrainPath = AssetDatabase.GetAssetPath(terrain.terrainData);

        if (terrainPath != ProductionTerrainPath)
        {
            problems.Add(
                $"Terrain uses {terrainPath} instead of {ProductionTerrainPath}.");
        }

        Vector3 expectedTerrainPosition =
            new Vector3(-700f, TerrainWorldY, -700f);

        if (Vector3.Distance(
                terrainTransform.position,
                expectedTerrainPosition) > 0.01f)
        {
            problems.Add("Terrain Transform position is not (-700, -10, -700).");
        }

        if (terrain.terrainData.heightmapResolution != HeightmapResolution)
        {
            problems.Add(
                $"Heightmap resolution is {terrain.terrainData.heightmapResolution}, " +
                $"expected {HeightmapResolution}.");
        }

        if (terrain.terrainData.alphamapResolution != AlphamapResolution)
        {
            problems.Add(
                $"Alphamap resolution is {terrain.terrainData.alphamapResolution}, " +
                $"expected {AlphamapResolution}.");
        }

        if (terrain.terrainData.baseMapResolution != 1024)
        {
            problems.Add(
                $"Basemap resolution is {terrain.terrainData.baseMapResolution}, " +
                "expected 1024.");
        }

        Vector3 terrainSize = terrain.terrainData.size;

        if (Vector3.Distance(
                terrainSize,
                new Vector3(TerrainWidth, TerrainHeight, TerrainLength)) > 0.01f)
        {
            problems.Add(
                $"Terrain size is {terrainSize}; expected " +
                $"({TerrainWidth}, {TerrainHeight}, {TerrainLength}).");
        }

        TerrainLayer[] terrainLayers = terrain.terrainData.terrainLayers;

        if (terrainLayers == null ||
            terrainLayers.Length != ProductionTerrainLayerPaths.Length)
        {
            problems.Add("Production terrain requires exactly five terrain layers.");
        }
        else
        {
            for (int index = 0; index < ProductionTerrainLayerPaths.Length; index++)
            {
                string layerPath = AssetDatabase.GetAssetPath(terrainLayers[index]);

                if (!string.Equals(
                        layerPath,
                        ProductionTerrainLayerPaths[index],
                        StringComparison.OrdinalIgnoreCase))
                {
                    problems.Add(
                        $"Terrain layer {index} uses {layerPath} instead of " +
                        $"{ProductionTerrainLayerPaths[index]}.");
                }

                TerrainLayer layer = terrainLayers[index];

                if (layer == null)
                {
                    continue;
                }

                if (layer.smoothnessSource !=
                    TerrainLayerSmoothnessSource.ConstantOnly)
                {
                    problems.Add(
                        $"Terrain layer {index} reads smoothness from its texture " +
                        "instead of the authored matte constant.");
                }

                if (Mathf.Abs(
                        layer.smoothness -
                        ProductionLayerSmoothness[index]) > 0.001f)
                {
                    problems.Add(
                        $"Terrain layer {index} smoothness is {layer.smoothness:F3}; " +
                        $"expected {ProductionLayerSmoothness[index]:F3}.");
                }

                if (Mathf.Abs(layer.metallic) > 0.001f)
                {
                    problems.Add($"Terrain layer {index} must be non-metallic.");
                }

                if (Mathf.Max(
                        layer.specular.r,
                        Mathf.Max(layer.specular.g, layer.specular.b)) > 0.01f)
                {
                    problems.Add($"Terrain layer {index} has a non-black specular tint.");
                }
            }
        }

        int expectedAlphamapTextureCount =
            (ProductionTerrainLayerPaths.Length + 3) / 4;

        Texture2D[] alphamapTextures = terrain.terrainData.alphamapTextures;

        if (terrain.terrainData.alphamapTextureCount != expectedAlphamapTextureCount ||
            alphamapTextures == null ||
            alphamapTextures.Length != expectedAlphamapTextureCount ||
            alphamapTextures.Any(texture => texture == null))
        {
            problems.Add(
                $"Terrain alphamap control textures are incomplete; expected " +
                $"{expectedAlphamapTextureCount} non-null textures for five layers.");
        }
        else
        {
            try
            {
                float[,,] paintedWeights =
                    terrain.terrainData.GetAlphamaps(
                        0,
                        0,
                        terrain.terrainData.alphamapWidth,
                        terrain.terrainData.alphamapHeight);

                float[] maximumLayerWeights =
                    new float[ProductionTerrainLayerPaths.Length];
                bool normalizedWeights = true;

                for (int z = 0; z < paintedWeights.GetLength(0); z += 32)
                {
                    for (int x = 0; x < paintedWeights.GetLength(1); x += 32)
                    {
                        float totalWeight = 0f;

                        for (int layerIndex = 0;
                             layerIndex < maximumLayerWeights.Length;
                             layerIndex++)
                        {
                            float weight = paintedWeights[z, x, layerIndex];

                            if (float.IsNaN(weight) ||
                                float.IsInfinity(weight) ||
                                weight < -0.001f ||
                                weight > 1.001f)
                            {
                                normalizedWeights = false;
                            }

                            totalWeight += weight;
                            maximumLayerWeights[layerIndex] =
                                Mathf.Max(maximumLayerWeights[layerIndex], weight);
                        }

                        if (float.IsNaN(totalWeight) ||
                            Mathf.Abs(totalWeight - 1f) > 0.02f)
                        {
                            normalizedWeights = false;
                        }
                    }
                }

                if (!normalizedWeights)
                {
                    problems.Add(
                        "Terrain alphamap weights are missing or not normalized.");
                }

                for (int layerIndex = 0;
                     layerIndex < maximumLayerWeights.Length;
                     layerIndex++)
                {
                    if (maximumLayerWeights[layerIndex] < 0.10f)
                    {
                        problems.Add(
                            $"Terrain layer {layerIndex} has no meaningful painted " +
                            "coverage in the production alphamap.");
                    }
                }
            }
            catch (Exception exception)
            {
                problems.Add(
                    $"Terrain alphamap data could not be read: {exception.Message}");
            }
        }

        float[,] heights =
            terrain.terrainData.GetHeights(
                0,
                0,
                terrain.terrainData.heightmapResolution,
                terrain.terrainData.heightmapResolution);

        float minimum = 1f;
        float maximum = 0f;

        foreach (float height in heights)
        {
            minimum = Mathf.Min(minimum, height);
            maximum = Mathf.Max(maximum, height);
        }

        float relief = (maximum - minimum) * terrain.terrainData.size.y;

        if (relief < 40f)
        {
            problems.Add(
                $"Terrain relief is only {relief:F1} m; expected at least 40 m.");
        }

        if (AssetDatabase.LoadAssetAtPath<TerrainData>(SourceTerrainPath) == null)
        {
            problems.Add("Baseline TerrainData is missing.");
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SceneBackupPath) == null)
        {
            problems.Add("Pre-terrain scene backup is missing.");
        }
        else
        {
            string[] backupDependencies =
                AssetDatabase.GetDependencies(SceneBackupPath, true);

            if (!backupDependencies.Contains(SourceTerrainPath))
            {
                problems.Add("Pre-terrain backup no longer references the baseline TerrainData.");
            }

            if (backupDependencies.Contains(ProductionTerrainPath))
            {
                problems.Add("Pre-terrain backup incorrectly references production TerrainData.");
            }
        }

        bool openWorldEnabledInBuild = false;

        foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
        {
            if (!buildScene.enabled)
            {
                continue;
            }

            if (string.Equals(
                    buildScene.path,
                    OpenWorldScene,
                    StringComparison.OrdinalIgnoreCase))
            {
                openWorldEnabledInBuild = true;
            }

            if (string.Equals(
                    buildScene.path,
                    SceneBackupPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                problems.Add("The pre-terrain backup scene must stay out of Build Settings.");
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(buildScene.path) == null)
            {
                problems.Add(
                    $"Enabled Build Settings scene is missing: {buildScene.path}.");
            }
        }

        if (!openWorldEnabledInBuild)
        {
            problems.Add("Bloodroot_OpenWorld is not enabled in Build Settings.");
        }

        Transform roads =
            worldRoot.transform.Find("_TERRAIN/Roads/Primary Progression Road");

        Transform creek =
            worldRoot.transform.Find("_TERRAIN/Rivers/Black Pines Creek");

        if (roads == null || roads.childCount != ProgressionRoad.Length)
        {
            problems.Add("Primary progression road guide is missing or incomplete.");
        }
        else
        {
            ValidateRouteGuide(
                problems,
                terrain,
                roads,
                ProgressionRoad,
                "Primary progression road");
        }

        if (creek == null || creek.childCount != BlackPinesCreek.Length)
        {
            problems.Add("Black Pines creek guide is missing or incomplete.");
        }
        else
        {
            ValidateRouteGuide(
                problems,
                terrain,
                creek,
                BlackPinesCreek,
                "Black Pines creek");

            float firstCreekHeight = creek.GetChild(0).position.y;
            float lastCreekHeight = creek.GetChild(creek.childCount - 1).position.y;

            if (firstCreekHeight - lastCreekHeight < 3.8f)
            {
                problems.Add("Black Pines creek does not descend at least 3.8 m overall.");
            }

            for (int index = 1; index < creek.childCount; index++)
            {
                float uphillStep =
                    creek.GetChild(index).position.y -
                    creek.GetChild(index - 1).position.y;

                if (uphillStep > 0.25f)
                {
                    problems.Add(
                        $"Black Pines creek rises {uphillStep:F1} m between " +
                        $"waypoints {index - 1:00} and {index:00}.");
                }
            }

            ValidateCreekLandform(problems, terrain);
        }

        float minimumRouteSeparation =
            GetMinimumRouteSeparation(ProgressionRoad, BlackPinesCreek);

        if (minimumRouteSeparation < 150f)
        {
            problems.Add(
                $"Black Pines Creek comes within {minimumRouteSeparation:F1} m of " +
                "the truck road; expected at least 150 m before bridge planning.");
        }

        ValidateRoadSurface(problems, terrain);

        ValidateTerrainSample(
            problems,
            terrain,
            "Black Pines arrival",
            -350f,
            -150f,
            2.5f,
            5.5f);

        ValidateTerrainSample(
            problems,
            terrain,
            "Stillwater yard",
            320f,
            -40f,
            6.5f,
            9.5f);

        ValidateTerrainSample(
            problems,
            terrain,
            "Harrow Estate overlook",
            -10f,
            395f,
            65f,
            71f);

        ValidateTerrainSample(
            problems,
            terrain,
            "Bloodroot Hollow gate",
            50f,
            540f,
            18f,
            24f);

        ValidateTerrainSample(
            problems,
            terrain,
            "Bloodroot Hollow arena",
            50f,
            600f,
            7f,
            11f);

        ValidateEstateOverlook(problems, terrain);
        ValidateHollowLandform(problems, terrain);
        ValidateWildernessVariation(problems, terrain);

        ValidateGroundedPath(
            problems,
            worldRoot.transform,
            terrain,
            "AREA_00_BLACK_PINES_FOREST/World Arrival Spawn",
            0.25f);

        ValidateGroundedPath(
            problems,
            worldRoot.transform,
            terrain,
            "AREA_00_BLACK_PINES_FOREST/Exit Road",
            0f);

        ValidateGroundedPath(
            problems,
            worldRoot.transform,
            terrain,
            "AREA_01_STILLWATER_FEED_MILL/Area Spawn",
            0.25f);

        ValidateGroundedPath(
            problems,
            worldRoot.transform,
            terrain,
            "AREA_01_STILLWATER_FEED_MILL/Locked Entrance",
            0f);

        ValidateGroundedPath(
            problems,
            worldRoot.transform,
            terrain,
            "AREA_02_HARROW_ESTATE/Area Spawn",
            0.25f);

        ValidateGroundedPath(
            problems,
            worldRoot.transform,
            terrain,
            "AREA_02_HARROW_ESTATE/Locked Entrance",
            0f);

        ValidateGroundedPath(
            problems,
            worldRoot.transform,
            terrain,
            "AREA_03_BLOODROOT_HOLLOW/Boss Arena Spawn",
            0.25f,
            new Vector2(50f, 600f));

        ValidateGroundedPath(
            problems,
            worldRoot.transform,
            terrain,
            "AREA_03_BLOODROOT_HOLLOW/Locked Entrance",
            0f);

        ValidateGroundedPath(
            problems,
            worldRoot.transform,
            terrain,
            "_CORE/Return Truck (Travel Wiring Pending)",
            0.1f);

        Transform navRoot =
            worldRoot.transform.Find("_CORE/Open World NavMesh Surface (Bake Later)");

        NavMeshSurface navMeshSurface =
            navRoot != null
                ? navRoot.GetComponent<NavMeshSurface>()
                : null;

        if (navMeshSurface == null)
        {
            problems.Add("Configured NavMesh Surface component is missing.");
        }
        else
        {
            if (navMeshSurface.collectObjects != CollectObjects.All)
            {
                problems.Add("NavMesh Surface must collect all open-world objects.");
            }

            if (navMeshSurface.useGeometry !=
                NavMeshCollectGeometry.PhysicsColliders)
            {
                problems.Add("NavMesh Surface must use physics collider geometry.");
            }

            if (navMeshSurface.buildHeightMesh)
            {
                problems.Add("NavMesh Surface height mesh must remain disabled.");
            }

            if (navMeshSurface.layerMask.value != ~0)
            {
                problems.Add("NavMesh Surface layer mask must include all layers.");
            }

            if (!navMeshSurface.ignoreNavMeshAgent ||
                !navMeshSurface.ignoreNavMeshObstacle)
            {
                problems.Add(
                    "NavMesh Surface must ignore NavMeshAgent and NavMeshObstacle components.");
            }

            if (!navMeshSurface.enabled || !navRoot.gameObject.activeInHierarchy)
            {
                problems.Add("NavMesh Surface placeholder must be active and enabled.");
            }
        }

        TerrainCollider terrainCollider = terrain.GetComponent<TerrainCollider>();

        if (terrainCollider == null ||
            terrainCollider.terrainData != terrain.terrainData)
        {
            problems.Add("TerrainCollider does not reference the production TerrainData.");
        }

        return problems;
    }

    private static void ValidateRouteGuide(
        List<string> problems,
        Terrain terrain,
        Transform guide,
        RouteNode[] expectedNodes,
        string label)
    {
        for (int index = 0; index < expectedNodes.Length; index++)
        {
            Transform waypoint = guide.GetChild(index);
            string expectedName = $"Waypoint {index:00}";

            if (waypoint.name != expectedName)
            {
                problems.Add(
                    $"{label} child {index} is named {waypoint.name} instead of " +
                    $"{expectedName}.");
            }

            Vector3 position = waypoint.position;
            float horizontalError =
                Vector2.Distance(
                    new Vector2(position.x, position.z),
                    expectedNodes[index].position);

            if (horizontalError > 0.1f)
            {
                problems.Add(
                    $"{label} {expectedName} is {horizontalError:F2} m from its " +
                    "designed XZ position.");
            }

            float expectedHeight =
                GetTerrainWorldHeight(terrain, position.x, position.z) + 0.15f;

            if (Mathf.Abs(position.y - expectedHeight) > 0.1f)
            {
                problems.Add(
                    $"{label} {expectedName} is not attached to the terrain surface.");
            }
        }
    }

    private static void ValidateGroundedPath(
        List<string> problems,
        Transform worldRoot,
        Terrain terrain,
        string path,
        float clearance,
        Vector2? expectedWorldPosition = null)
    {
        Transform target = worldRoot.Find(path);

        if (target == null)
        {
            problems.Add($"Required grounded marker is missing: {path}.");
            return;
        }

        Vector3 position = target.position;

        if (expectedWorldPosition.HasValue &&
            Vector2.Distance(
                new Vector2(position.x, position.z),
                expectedWorldPosition.Value) > 0.5f)
        {
            problems.Add(
                $"{path} is at ({position.x:F1}, {position.z:F1}) instead of " +
                $"({expectedWorldPosition.Value.x:F1}, " +
                $"{expectedWorldPosition.Value.y:F1}).");
        }

        float expectedHeight =
            GetTerrainWorldHeight(terrain, position.x, position.z) + clearance;

        if (Mathf.Abs(position.y - expectedHeight) > 0.1f)
        {
            problems.Add(
                $"{path} is {Mathf.Abs(position.y - expectedHeight):F2} m off " +
                "the terrain surface.");
        }
    }

    private static void ValidateTerrainSample(
        List<string> problems,
        Terrain terrain,
        string label,
        float worldX,
        float worldZ,
        float minimumHeight,
        float maximumHeight)
    {
        float height = GetTerrainWorldHeight(terrain, worldX, worldZ);

        if (height < minimumHeight || height > maximumHeight)
        {
            problems.Add(
                $"{label} terrain height is {height:F1} m; expected " +
                $"{minimumHeight:F1}-{maximumHeight:F1} m.");
        }
    }

    private static void ValidateCreekLandform(
        List<string> problems,
        Terrain terrain)
    {
        float routeLength = 0f;
        int directionChanges = 0;
        int previousTurn = 0;
        int healthyBankSections = 0;

        for (int index = 0; index < BlackPinesCreek.Length - 1; index++)
        {
            RouteNode start = BlackPinesCreek[index];
            RouteNode end = BlackPinesCreek[index + 1];
            Vector2 segment = end.position - start.position;
            routeLength += segment.magnitude;

            Vector2 midpoint = (start.position + end.position) * 0.5f;
            Vector2 normal =
                new Vector2(-segment.y, segment.x) /
                Mathf.Max(0.001f, segment.magnitude);

            float centerHeight =
                GetTerrainWorldHeight(terrain, midpoint.x, midpoint.y);
            Vector2 firstBank = midpoint + normal * 16f;
            Vector2 secondBank = midpoint - normal * 16f;
            float firstBankHeight =
                GetTerrainWorldHeight(terrain, firstBank.x, firstBank.y);
            float secondBankHeight =
                GetTerrainWorldHeight(terrain, secondBank.x, secondBank.y);

            if (firstBankHeight >= centerHeight + 0.2f &&
                secondBankHeight >= centerHeight + 0.2f)
            {
                healthyBankSections++;
            }

            if (index < BlackPinesCreek.Length - 2)
            {
                Vector2 nextSegment =
                    BlackPinesCreek[index + 2].position - end.position;
                float turn = Cross(segment, nextSegment);
                int turnDirection =
                    Mathf.Abs(turn) < 0.001f ? 0 : (turn > 0f ? 1 : -1);

                if (turnDirection != 0)
                {
                    if (previousTurn != 0 && turnDirection != previousTurn)
                    {
                        directionChanges++;
                    }

                    previousTurn = turnDirection;
                }
            }
        }

        float chord =
            Vector2.Distance(
                BlackPinesCreek[0].position,
                BlackPinesCreek[BlackPinesCreek.Length - 1].position);
        float sinuosity = routeLength / Mathf.Max(0.001f, chord);

        if (sinuosity < 1.1f || sinuosity > 1.25f)
        {
            problems.Add(
                $"Black Pines creek sinuosity is {sinuosity:F3}; expected " +
                "1.10-1.25 for broad natural meanders.");
        }

        if (directionChanges < 3)
        {
            problems.Add(
                $"Black Pines creek changes bend direction only {directionChanges} " +
                "times; expected at least three alternating meanders.");
        }

        int requiredHealthyBanks =
            Mathf.CeilToInt((BlackPinesCreek.Length - 1) * 0.65f);

        if (healthyBankSections < requiredHealthyBanks)
        {
            problems.Add(
                $"Black Pines creek has raised banks on only " +
                $"{healthyBankSections}/{BlackPinesCreek.Length - 1} sampled " +
                "sections.");
        }

        for (int index = 0; index < BlackPinesCreek.Length; index++)
        {
            RouteNode node = BlackPinesCreek[index];
            float actualHeight =
                GetTerrainWorldHeight(
                    terrain,
                    node.position.x,
                    node.position.y);
            float expectedHeight = node.height - 0.2f;

            if (Mathf.Abs(actualHeight - expectedHeight) > 0.75f)
            {
                problems.Add(
                    $"Black Pines creek bed waypoint {index:00} is " +
                    $"{Mathf.Abs(actualHeight - expectedHeight):F2} m from its " +
                    "designed bed height.");
            }
        }
    }

    private static void ValidateEstateOverlook(
        List<string> problems,
        Terrain terrain)
    {
        float estateHeight = GetTerrainWorldHeight(terrain, -10f, 395f);
        float blackPinesHeight = GetTerrainWorldHeight(terrain, -350f, -150f);
        float stillwaterHeight = GetTerrainWorldHeight(terrain, 320f, -40f);
        float hollowHeight = GetTerrainWorldHeight(terrain, 50f, 600f);
        float maximumHollowRim = float.MinValue;

        for (int index = 0; index < 16; index++)
        {
            float angle = index / 16f * Mathf.PI * 2f;

            if (Mathf.Sin(angle) < -0.6f && Mathf.Cos(angle) > -0.3f)
            {
                continue;
            }

            float maximumRadius = 190f;

            if (Mathf.Sin(angle) > 0.001f)
            {
                maximumRadius =
                    Mathf.Min(maximumRadius, 99f / Mathf.Sin(angle));
            }

            for (float radius = 80f; radius <= maximumRadius; radius += 5f)
            {
                maximumHollowRim =
                    Mathf.Max(
                        maximumHollowRim,
                        GetTerrainWorldHeight(
                            terrain,
                            50f + Mathf.Cos(angle) * radius * 1.04f,
                            600f + Mathf.Sin(angle) * radius));
            }
        }

        if (estateHeight - blackPinesHeight < 45f ||
            estateHeight - stillwaterHeight < 42f ||
            estateHeight - hollowHeight < 43f)
        {
            problems.Add(
                "Harrow Estate is not high enough to remain the open-world " +
                "overlook above Black Pines, Stillwater, and Bloodroot Hollow.");
        }

        if (estateHeight < maximumHollowRim + 4f)
        {
            problems.Add(
                $"Harrow Estate is {estateHeight:F1} m high but the highest " +
                $"sampled Bloodroot Hollow rim is {maximumHollowRim:F1} m; " +
                "the estate must remain the map's dominant overlook.");
        }

        float minimumPadHeight = float.MaxValue;
        float maximumPadHeight = float.MinValue;
        float maximumPadSlope = 0f;
        int padSampleCount = 0;

        for (int z = -1; z <= 1; z++)
        {
            for (int x = -1; x <= 1; x++)
            {
                float worldX = -10f + x * 12f;
                float worldZ = 395f + z * 12f;

                float ignoredRoadHeight;

                if (DistanceToRoute(
                        worldX,
                        worldZ,
                        ProgressionRoad,
                        out ignoredRoadHeight) < 26f)
                {
                    continue;
                }

                float height = GetTerrainWorldHeight(terrain, worldX, worldZ);
                padSampleCount++;
                minimumPadHeight = Mathf.Min(minimumPadHeight, height);
                maximumPadHeight = Mathf.Max(maximumPadHeight, height);
                maximumPadSlope =
                    Mathf.Max(
                        maximumPadSlope,
                        GetTerrainWorldSteepness(terrain, worldX, worldZ));
            }
        }

        if (padSampleCount < 4 ||
            maximumPadHeight - minimumPadHeight > 2.5f ||
            maximumPadSlope > 12f)
        {
            problems.Add(
                $"Harrow Estate build bench varies " +
                $"{maximumPadHeight - minimumPadHeight:F1} m and reaches " +
                $"{maximumPadSlope:F1} degrees; expected a buildable hilltop.");
        }
    }

    private static void ValidateHollowLandform(
        List<string> problems,
        Terrain terrain)
    {
        List<float> crestHeights = new List<float>();
        List<float> crestRadii = new List<float>();
        int strongCrests = 0;

        for (int index = 0; index < 16; index++)
        {
            float angle = index / 16f * Mathf.PI * 2f;

            if (Mathf.Sin(angle) < -0.6f && Mathf.Cos(angle) > -0.3f)
            {
                continue;
            }

            float maximumRadius = 190f;

            if (Mathf.Sin(angle) > 0.001f)
            {
                maximumRadius =
                    Mathf.Min(
                        maximumRadius,
                        99f / Mathf.Sin(angle));
            }

            float crestHeight = float.MinValue;
            float crestRadius = 80f;

            for (float radius = 80f; radius <= maximumRadius; radius += 5f)
            {
                float worldX = 50f + Mathf.Cos(angle) * radius * 1.04f;
                float worldZ = 600f + Mathf.Sin(angle) * radius;
                float height =
                    GetTerrainWorldHeight(terrain, worldX, worldZ);

                if (height > crestHeight)
                {
                    crestHeight = height;
                    crestRadius = radius;
                }
            }

            crestHeights.Add(crestHeight);
            crestRadii.Add(crestRadius);

            if (crestHeight >= 30f)
            {
                strongCrests++;
            }
        }

        float crestHeightSpread =
            crestHeights.Count > 0
                ? crestHeights.Max() - crestHeights.Min()
                : 0f;
        float crestRadiusSpread =
            crestRadii.Count > 0
                ? crestRadii.Max() - crestRadii.Min()
                : 0f;

        if (strongCrests < 10)
        {
            problems.Add(
                $"Bloodroot Hollow has only {strongCrests} strong sampled rim " +
                "crests; the arena is not sufficiently enclosed.");
        }

        if (crestHeightSpread < 5f || crestRadiusSpread < 20f)
        {
            problems.Add(
                $"Bloodroot Hollow rim variation is height " +
                $"{crestHeightSpread:F1} m / radius {crestRadiusSpread:F1} m; " +
                "expected an irregular natural enclosure.");
        }

        float minimumFloorHeight = float.MaxValue;
        float maximumFloorHeight = float.MinValue;
        float maximumFloorSlope = 0f;
        int floorSampleCount = 0;

        for (int z = -1; z <= 1; z++)
        {
            for (int x = -1; x <= 1; x++)
            {
                float worldX = 50f + x * 18f;
                float worldZ = 600f + z * 18f;

                float ignoredRoadHeight;

                if (worldZ < 598f ||
                    DistanceToRoute(
                        worldX,
                        worldZ,
                        ProgressionRoad,
                        out ignoredRoadHeight) < 12f)
                {
                    continue;
                }

                float height = GetTerrainWorldHeight(terrain, worldX, worldZ);
                floorSampleCount++;
                minimumFloorHeight = Mathf.Min(minimumFloorHeight, height);
                maximumFloorHeight = Mathf.Max(maximumFloorHeight, height);
                maximumFloorSlope =
                    Mathf.Max(
                        maximumFloorSlope,
                        GetTerrainWorldSteepness(terrain, worldX, worldZ));
            }
        }

        float floorRange = maximumFloorHeight - minimumFloorHeight;

        if (floorSampleCount < 4 ||
            floorRange < 0.12f || floorRange > 2.5f ||
            maximumFloorSlope > 12f)
        {
            problems.Add(
                $"Bloodroot Hollow arena floor varies {floorRange:F2} m and " +
                $"reaches {maximumFloorSlope:F1} degrees; expected subtle natural " +
                "relief on a playable floor.");
        }
    }

    private static void ValidateWildernessVariation(
        List<string> problems,
        Terrain terrain)
    {
        Vector2[] patchCenters =
        {
            new Vector2(-520f, 80f),
            new Vector2(-120f, 80f),
            new Vector2(430f, 230f)
        };

        float totalRange = 0f;
        float maximumRange = 0f;

        foreach (Vector2 center in patchCenters)
        {
            float minimum = float.MaxValue;
            float maximum = float.MinValue;

            for (int z = -2; z <= 2; z++)
            {
                for (int x = -2; x <= 2; x++)
                {
                    float height =
                        GetTerrainWorldHeight(
                            terrain,
                            center.x + x * 12f,
                            center.y + z * 12f);
                    minimum = Mathf.Min(minimum, height);
                    maximum = Mathf.Max(maximum, height);
                }
            }

            float range = maximum - minimum;
            totalRange += range;
            maximumRange = Mathf.Max(maximumRange, range);
        }

        float averageRange = totalRange / patchCenters.Length;

        if (averageRange < 0.35f || maximumRange > 8f)
        {
            problems.Add(
                $"Wilderness ground variation averages {averageRange:F2} m " +
                $"with a {maximumRange:F2} m maximum; expected lived-in rolling " +
                "micro-relief without impassable noise.");
        }
    }

    private static void ValidateRoadSurface(
        List<string> problems,
        Terrain terrain)
    {
        float maximumGrade = 0f;
        float maximumTargetError = 0f;
        float maximumCrossfall = 0f;

        for (int segmentIndex = 0;
             segmentIndex < ProgressionRoad.Length - 1;
             segmentIndex++)
        {
            RouteNode start = ProgressionRoad[segmentIndex];
            RouteNode end = ProgressionRoad[segmentIndex + 1];
            float segmentLength = Vector2.Distance(start.position, end.position);
            int steps = Mathf.Max(1, Mathf.CeilToInt(segmentLength / 10f));
            float previousHeight =
                GetTerrainWorldHeight(
                    terrain,
                    start.position.x,
                    start.position.y);

            for (int step = 0; step <= steps; step++)
            {
                float interpolation = step / (float)steps;
                Vector2 position =
                    Vector2.Lerp(start.position, end.position, interpolation);

                float height =
                    GetTerrainWorldHeight(terrain, position.x, position.y);

                float targetHeight =
                    Mathf.Lerp(start.height, end.height, interpolation);

                maximumTargetError =
                    Mathf.Max(
                        maximumTargetError,
                        Mathf.Abs(height - targetHeight));

                Vector2 direction =
                    (end.position - start.position).normalized;
                Vector2 normal = new Vector2(-direction.y, direction.x);
                Vector2 firstEdge = position + normal * 5f;
                Vector2 secondEdge = position - normal * 5f;
                float firstEdgeHeight =
                    GetTerrainWorldHeight(
                        terrain,
                        firstEdge.x,
                        firstEdge.y);
                float secondEdgeHeight =
                    GetTerrainWorldHeight(
                        terrain,
                        secondEdge.x,
                        secondEdge.y);
                maximumCrossfall =
                    Mathf.Max(
                        maximumCrossfall,
                        Mathf.Abs(firstEdgeHeight - secondEdgeHeight));

                if (step > 0)
                {
                    float run = segmentLength / steps;
                    maximumGrade =
                        Mathf.Max(
                            maximumGrade,
                            Mathf.Abs(height - previousHeight) / run);
                }

                previousHeight = height;
            }
        }

        if (maximumTargetError > 2f)
        {
            problems.Add(
                $"Truck road departs from its graded route by up to " +
                $"{maximumTargetError:F1} m; expected no more than 2 m.");
        }

        if (maximumGrade > 0.25f)
        {
            problems.Add(
                $"Truck road reaches a {maximumGrade * 100f:F1}% grade; " +
                "expected no more than 25% for this blockout pass.");
        }

        if (maximumCrossfall > 2.5f)
        {
            problems.Add(
                $"Truck road crossfall reaches {maximumCrossfall:F1} m across " +
                "10 m; expected no more than 2.5 m.");
        }

        Vector2[] requiredGateCrossings =
        {
            new Vector2(235f, -40f),
            new Vector2(40f, 260f),
            new Vector2(50f, 540f)
        };

        foreach (Vector2 gate in requiredGateCrossings)
        {
            float gateTarget;
            float gateDistance =
                DistanceToRoute(
                    gate.x,
                    gate.y,
                    ProgressionRoad,
                    out gateTarget);

            if (gateDistance > 8f)
            {
                problems.Add(
                    $"Truck road misses progression gate ({gate.x:F0}, " +
                    $"{gate.y:F0}) by {gateDistance:F1} m.");
            }
        }
    }

    private static float GetMinimumRouteSeparation(
        RouteNode[] first,
        RouteNode[] second)
    {
        float minimum = float.MaxValue;

        for (int firstIndex = 0;
             firstIndex < first.Length - 1;
             firstIndex++)
        {
            Vector2 firstStart = first[firstIndex].position;
            Vector2 firstEnd = first[firstIndex + 1].position;

            for (int secondIndex = 0;
                 secondIndex < second.Length - 1;
                 secondIndex++)
            {
                Vector2 secondStart = second[secondIndex].position;
                Vector2 secondEnd = second[secondIndex + 1].position;

                if (SegmentsIntersect(
                        firstStart,
                        firstEnd,
                        secondStart,
                        secondEnd))
                {
                    return 0f;
                }

                minimum =
                    Mathf.Min(
                        minimum,
                        DistancePointToSegment(firstStart, secondStart, secondEnd),
                        DistancePointToSegment(firstEnd, secondStart, secondEnd),
                        DistancePointToSegment(secondStart, firstStart, firstEnd),
                        DistancePointToSegment(secondEnd, firstStart, firstEnd));
            }
        }

        return minimum;
    }

    private static float DistancePointToSegment(
        Vector2 point,
        Vector2 start,
        Vector2 end)
    {
        Vector2 segment = end - start;
        float denominator = Mathf.Max(segment.sqrMagnitude, 0.0001f);
        float interpolation =
            Mathf.Clamp01(Vector2.Dot(point - start, segment) / denominator);

        return Vector2.Distance(point, start + segment * interpolation);
    }

    private static bool SegmentsIntersect(
        Vector2 firstStart,
        Vector2 firstEnd,
        Vector2 secondStart,
        Vector2 secondEnd)
    {
        float firstSide = Cross(firstEnd - firstStart, secondStart - firstStart);
        float secondSide = Cross(firstEnd - firstStart, secondEnd - firstStart);
        float thirdSide = Cross(secondEnd - secondStart, firstStart - secondStart);
        float fourthSide = Cross(secondEnd - secondStart, firstEnd - secondStart);

        if (((firstSide > 0f && secondSide < 0f) ||
             (firstSide < 0f && secondSide > 0f)) &&
            ((thirdSide > 0f && fourthSide < 0f) ||
             (thirdSide < 0f && fourthSide > 0f)))
        {
            return true;
        }

        const float epsilon = 0.0001f;

        return (Mathf.Abs(firstSide) <= epsilon &&
                PointOnSegment(secondStart, firstStart, firstEnd, epsilon)) ||
               (Mathf.Abs(secondSide) <= epsilon &&
                PointOnSegment(secondEnd, firstStart, firstEnd, epsilon)) ||
               (Mathf.Abs(thirdSide) <= epsilon &&
                PointOnSegment(firstStart, secondStart, secondEnd, epsilon)) ||
               (Mathf.Abs(fourthSide) <= epsilon &&
                PointOnSegment(firstEnd, secondStart, secondEnd, epsilon));
    }

    private static bool PointOnSegment(
        Vector2 point,
        Vector2 start,
        Vector2 end,
        float epsilon)
    {
        return point.x >= Mathf.Min(start.x, end.x) - epsilon &&
               point.x <= Mathf.Max(start.x, end.x) + epsilon &&
               point.y >= Mathf.Min(start.y, end.y) - epsilon &&
               point.y <= Mathf.Max(start.y, end.y) + epsilon;
    }

    private static float Cross(Vector2 first, Vector2 second)
    {
        return first.x * second.y - first.y * second.x;
    }

    private static float GetTerrainWorldHeight(
        Terrain terrain,
        float worldX,
        float worldZ)
    {
        Vector3 terrainPosition = terrain.transform.position;
        Vector3 terrainSize = terrain.terrainData.size;
        float normalizedX =
            Mathf.InverseLerp(
                terrainPosition.x,
                terrainPosition.x + terrainSize.x,
                worldX);

        float normalizedZ =
            Mathf.InverseLerp(
                terrainPosition.z,
                terrainPosition.z + terrainSize.z,
                worldZ);

        return terrainPosition.y +
               terrain.terrainData.GetInterpolatedHeight(normalizedX, normalizedZ);
    }

    private static float GetTerrainWorldSteepness(
        Terrain terrain,
        float worldX,
        float worldZ)
    {
        Vector3 terrainPosition = terrain.transform.position;
        Vector3 terrainSize = terrain.terrainData.size;
        float normalizedX =
            Mathf.InverseLerp(
                terrainPosition.x,
                terrainPosition.x + terrainSize.x,
                worldX);
        float normalizedZ =
            Mathf.InverseLerp(
                terrainPosition.z,
                terrainPosition.z + terrainSize.z,
                worldZ);

        return terrain.terrainData.GetSteepness(normalizedX, normalizedZ);
    }

    private static float EllipseMask(
        float worldX,
        float worldZ,
        float centerX,
        float centerZ,
        float radiusX,
        float radiusZ)
    {
        float normalizedDistance =
            Mathf.Sqrt(
                Mathf.Pow((worldX - centerX) / radiusX, 2f) +
                Mathf.Pow((worldZ - centerZ) / radiusZ, 2f));

        return 1f -
               Mathf.SmoothStep(
                   0f,
                   1f,
                   Mathf.Clamp01(normalizedDistance));
    }

    private static float BlendEllipseToHeight(
        float currentHeight,
        float worldX,
        float worldZ,
        float centerX,
        float centerZ,
        float innerRadius,
        float outerRadius,
        float targetHeight,
        float zScale = 1f)
    {
        float dx = worldX - centerX;
        float dz = (worldZ - centerZ) / Mathf.Max(0.01f, zScale);
        float distance = Mathf.Sqrt(dx * dx + dz * dz);
        float influence = DistanceMask(distance, innerRadius, outerRadius);
        return Mathf.Lerp(currentHeight, targetHeight, influence);
    }

    private static float DistanceMask(
        float distance,
        float innerDistance,
        float outerDistance)
    {
        float interpolation =
            Mathf.InverseLerp(innerDistance, outerDistance, distance);

        return 1f - Mathf.SmoothStep(0f, 1f, interpolation);
    }

    private static float DistanceToRoute(
        float worldX,
        float worldZ,
        RouteNode[] route,
        out float targetHeight)
    {
        Vector2 point = new Vector2(worldX, worldZ);
        float bestDistance = float.MaxValue;
        targetHeight = route[0].height;

        for (int index = 0; index < route.Length - 1; index++)
        {
            Vector2 start = route[index].position;
            Vector2 end = route[index + 1].position;
            Vector2 segment = end - start;
            float denominator = Mathf.Max(segment.sqrMagnitude, 0.0001f);
            float interpolation =
                Mathf.Clamp01(Vector2.Dot(point - start, segment) / denominator);

            Vector2 nearest = start + segment * interpolation;
            float distance = Vector2.Distance(point, nearest);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                targetHeight =
                    Mathf.Lerp(
                        route[index].height,
                        route[index + 1].height,
                        interpolation);
            }
        }

        return bestDistance;
    }

    private static void EnsureAssetFolder(string assetPath)
    {
        string normalized = assetPath.Replace('\\', '/');
        string[] parts = normalized.Split('/');
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

    private readonly struct RouteNode
    {
        public RouteNode(float x, float z, float targetHeight)
        {
            position = new Vector2(x, z);
            height = targetHeight;
        }

        public readonly Vector2 position;
        public readonly float height;
    }
}
#endif
