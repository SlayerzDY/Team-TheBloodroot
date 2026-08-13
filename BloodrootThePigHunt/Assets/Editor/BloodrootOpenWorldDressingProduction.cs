#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Deterministically dresses the production open world without changing imported assets.
/// High-volume trees and grass live in TerrainData; authored rock instances live in the scene.
/// </summary>
public static class BloodrootOpenWorldDressingProduction
{
    private const string OpenWorldScene =
        "Assets/Scenes/OpenWorld/Bloodroot_OpenWorld.unity";
    private const string ProductionTerrainPath =
        "Assets/Scenes/OpenWorld/Data/Bloodroot_OpenWorld_Terrain_Production.asset";
    private const string TerrainRepairTempPath =
        "Assets/Scenes/OpenWorld/Data/Bloodroot_OpenWorld_Terrain_DressingRepairTemp.asset";
    private const string BackupFolder = "Assets/Scenes/OpenWorld/Backups";
    private const string SceneBackupPath =
        BackupFolder + "/Bloodroot_OpenWorld_PreNaturalDressing.unity";
    private const string TerrainBackupPath =
        BackupFolder + "/Bloodroot_OpenWorld_Terrain_PreNaturalDressing.asset";
    private const string GeneratedPrefabFolder =
        "Assets/PreFabs/OpenWorld/NaturalDressing";
    private const string GeneratedMaterialFolder =
        "Assets/Materials/OpenWorld/NaturalDressing";

    private const string ForestPack =
        "Assets/Imports/BluBlu Games/Low Poly Forest Mini Pack/Prefabs/";

    private static readonly string[] SourceTreePrefabPaths =
    {
        ForestPack + "Fir.prefab",
        ForestPack + "SM_Tree_01.prefab",
        ForestPack + "SM_Tree_02.prefab",
        ForestPack + "SM_Tree_Green.prefab"
    };

    private static readonly string[] ProductionTreePrefabPaths =
    {
        GeneratedPrefabFolder + "/Fir_Terrain.prefab",
        GeneratedPrefabFolder + "/SM_Tree_01_Terrain.prefab",
        GeneratedPrefabFolder + "/SM_Tree_02_Terrain.prefab",
        GeneratedPrefabFolder + "/SM_Tree_Green_Terrain.prefab"
    };

    private static readonly string[] SourceGrassPrefabPaths =
    {
        ForestPack + "SM_Grass_01.prefab",
        ForestPack + "SM_Grass_02.prefab"
    };

    private static readonly string[] ProductionGrassPrefabPaths =
    {
        GeneratedPrefabFolder + "/SM_Grass_01_Terrain.prefab",
        GeneratedPrefabFolder + "/SM_Grass_02_Terrain.prefab"
    };

    private static readonly string[] RockPrefabPaths =
    {
        ForestPack + "SM_Rock_01.prefab",
        ForestPack + "SM_Rock_02.prefab",
        ForestPack + "SM_Rock_03.prefab",
        ForestPack + "SM_Rock_04.prefab",
        ForestPack + "SM_Rock_05.prefab",
        ForestPack + "SM_Rock_06.prefab",
        ForestPack + "SM_Rock_07.prefab"
    };

    private static readonly RouteNode[] ProgressionRoad =
    {
        new RouteNode(-350f, -150f), new RouteNode(-120f, -118f),
        new RouteNode(85f, -78f), new RouteNode(235f, -40f),
        new RouteNode(320f, -40f), new RouteNode(250f, 100f),
        new RouteNode(155f, 195f), new RouteNode(70f, 240f),
        new RouteNode(40f, 260f), new RouteNode(-25f, 315f),
        new RouteNode(40f, 360f), new RouteNode(110f, 420f),
        new RouteNode(100f, 470f), new RouteNode(65f, 515f),
        new RouteNode(50f, 540f), new RouteNode(50f, 600f)
    };

    private static readonly RouteNode[] BlackPinesCreek =
    {
        new RouteNode(-660f, -360f), new RouteNode(-605f, -322f),
        new RouteNode(-545f, -306f), new RouteNode(-475f, -325f),
        new RouteNode(-425f, -370f), new RouteNode(-365f, -414f),
        new RouteNode(-295f, -423f), new RouteNode(-230f, -398f),
        new RouteNode(-176f, -350f), new RouteNode(-112f, -311f),
        new RouteNode(-38f, -300f), new RouteNode(25f, -322f),
        new RouteNode(72f, -370f), new RouteNode(132f, -414f),
        new RouteNode(205f, -430f), new RouteNode(285f, -402f),
        new RouteNode(352f, -350f), new RouteNode(430f, -326f)
    };

    private static readonly Vector2[] ProgressionGates =
    {
        new Vector2(235f, -40f),
        new Vector2(40f, 260f),
        new Vector2(50f, 540f)
    };

    private static readonly int[] TreeTargets = { 1900, 280, 600, 225, 500 };
    private static readonly int[] RockTargets = { 170, 60, 105, 120, 45 };

    private const int DetailResolution = 512;
    private const int DetailResolutionPerPatch = 16;
    private const float TreeGrid = 7.5f;
    private const float RockGrid = 13f;
    private const float MinimumTreeSpacing = 5.6f;
    private const float MinimumRockSpacing = 7.5f;
    private const string DressingRootName = "_DRESSING";
    private const string GeneratedRootPrefix = "Generated Natural Dressing";
    private const string SignaturePrefix = "_PLACEMENT_SIGNATURE_";
    private const string GeneratedAssetOwner = "BloodrootOpenWorldDressingProduction|v1";

    private enum Biome
    {
        BlackPines = 0,
        Stillwater = 1,
        HarrowEstate = 2,
        BloodrootHollow = 3,
        Transition = 4
    }

    private readonly struct RouteNode
    {
        public readonly Vector2 Point;

        public RouteNode(float x, float z)
        {
            Point = new Vector2(x, z);
        }
    }

    private readonly struct ScatterCandidate
    {
        public readonly int GridX;
        public readonly int GridZ;
        public readonly Vector2 Point;
        public readonly Biome Biome;
        public readonly float Slope;
        public readonly float Score;
        public readonly uint Hash;

        public ScatterCandidate(
            int gridX,
            int gridZ,
            Vector2 point,
            Biome biome,
            float slope,
            float score,
            uint hash)
        {
            GridX = gridX;
            GridZ = gridZ;
            Point = point;
            Biome = biome;
            Slope = slope;
            Score = score;
            Hash = hash;
        }
    }

    private readonly struct RockPlacement
    {
        public readonly string Name;
        public readonly string PrefabPath;
        public readonly Biome Biome;
        public readonly Vector2 Point;
        public readonly float Scale;
        public readonly float Yaw;
        public readonly float SinkFraction;
        public readonly bool ColliderEnabled;

        public RockPlacement(
            string name,
            string prefabPath,
            Biome biome,
            Vector2 point,
            float scale,
            float yaw,
            float sinkFraction,
            bool colliderEnabled)
        {
            Name = name;
            PrefabPath = prefabPath;
            Biome = biome;
            Point = point;
            Scale = scale;
            Yaw = yaw;
            SinkFraction = sinkFraction;
            ColliderEnabled = colliderEnabled;
        }
    }

    private sealed class GeneratedAssetSnapshot
    {
        public string AssetPath;
        public byte[] AssetBytes;
        public byte[] MetaBytes;
    }

    private sealed class TerrainSampler
    {
        private readonly Terrain terrain;
        private readonly TerrainData data;
        private readonly float[,] heights;
        private readonly float[,,] alphamaps;
        private readonly int resolution;
        private readonly float sampleX;
        private readonly float sampleZ;

        public TerrainSampler(Terrain sourceTerrain)
        {
            terrain = sourceTerrain;
            data = terrain.terrainData;
            resolution = data.heightmapResolution;
            heights = data.GetHeights(0, 0, resolution, resolution);
            alphamaps = data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight);
            sampleX = data.size.x / (resolution - 1f);
            sampleZ = data.size.z / (resolution - 1f);
        }

        public float WorldHeight(Vector2 point)
        {
            float nx = Mathf.InverseLerp(
                terrain.transform.position.x,
                terrain.transform.position.x + data.size.x,
                point.x);
            float nz = Mathf.InverseLerp(
                terrain.transform.position.z,
                terrain.transform.position.z + data.size.z,
                point.y);
            return terrain.transform.position.y + data.GetInterpolatedHeight(nx, nz);
        }

        public Vector3 Normal(Vector2 point)
        {
            WorldToHeightIndex(point, out int x, out int z);
            int x0 = Mathf.Max(0, x - 1);
            int x1 = Mathf.Min(resolution - 1, x + 1);
            int z0 = Mathf.Max(0, z - 1);
            int z1 = Mathf.Min(resolution - 1, z + 1);
            float dx = (heights[z, x1] - heights[z, x0]) * data.size.y /
                       Mathf.Max(0.001f, (x1 - x0) * sampleX);
            float dz = (heights[z1, x] - heights[z0, x]) * data.size.y /
                       Mathf.Max(0.001f, (z1 - z0) * sampleZ);
            return new Vector3(-dx, 1f, -dz).normalized;
        }

        public float Slope(Vector2 point)
        {
            return Vector3.Angle(Vector3.up, Normal(point));
        }

        public float LayerWeight(Vector2 point, int layer)
        {
            if (layer < 0 || layer >= data.alphamapLayers)
            {
                return 0f;
            }

            float nx = Mathf.InverseLerp(
                terrain.transform.position.x,
                terrain.transform.position.x + data.size.x,
                point.x);
            float nz = Mathf.InverseLerp(
                terrain.transform.position.z,
                terrain.transform.position.z + data.size.z,
                point.y);
            int x = Mathf.Clamp(Mathf.FloorToInt(nx * data.alphamapWidth), 0, data.alphamapWidth - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(nz * data.alphamapHeight), 0, data.alphamapHeight - 1);
            return alphamaps[z, x, layer];
        }

        private void WorldToHeightIndex(Vector2 point, out int x, out int z)
        {
            float nx = Mathf.InverseLerp(
                terrain.transform.position.x,
                terrain.transform.position.x + data.size.x,
                point.x);
            float nz = Mathf.InverseLerp(
                terrain.transform.position.z,
                terrain.transform.position.z + data.size.z,
                point.y);
            x = Mathf.Clamp(Mathf.RoundToInt(nx * (resolution - 1)), 0, resolution - 1);
            z = Mathf.Clamp(Mathf.RoundToInt(nz * (resolution - 1)), 0, resolution - 1);
        }
    }

    [MenuItem("Bloodroot/Open World/Build Natural World Dressing", false, 40)]
    public static void BuildNaturalWorldDressing()
    {
        RunDressingBuild(false);
    }

    [MenuItem("Bloodroot/Open World/Rebuild Natural World Dressing", false, 41)]
    public static void RebuildNaturalWorldDressing()
    {
        RunDressingBuild(true);
    }

    [MenuItem("Bloodroot/Open World/Validate Natural World Dressing", false, 42)]
    public static void ValidateNaturalWorldDressing()
    {
        if (!TryOpenTargetScene(out Scene scene, out bool openedHere))
        {
            return;
        }

        Scene previous = SceneManager.GetActiveScene();
        try
        {
            SceneManager.SetActiveScene(scene);
            Terrain terrain = FindProductionTerrain(scene);
            ValidateDressing(scene, terrain, false);
            EditorUtility.DisplayDialog(
                "Natural Dressing Valid",
                BuildValidationSummary(terrain),
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Natural Dressing Validation Failed",
                exception.Message,
                "OK");
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded)
            {
                SceneManager.SetActiveScene(previous);
            }

            if (openedHere && scene.IsValid() && scene.isLoaded && !scene.isDirty)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static void RunDressingBuild(bool allowExisting)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Natural Dressing Unavailable",
                "Exit Play Mode before dressing the open world.",
                "OK");
            return;
        }

        if (!TryOpenTargetScene(out Scene scene, out bool openedHere))
        {
            return;
        }

        Scene previous = SceneManager.GetActiveScene();
        TerrainData repairBackup = null;
        byte[] sceneBytes = null;
        List<GeneratedAssetSnapshot> generatedAssetSnapshots = null;
        bool generatedAssetMutationStarted = false;
        bool mutationStarted = false;
        bool committed = false;
        bool rollbackCompleted = false;

        try
        {
            SceneManager.SetActiveScene(scene);
            if (scene.isDirty)
            {
                throw new InvalidOperationException(
                    "Bloodroot_OpenWorld has unsaved changes. Save or revert them before running the deterministic dressing pass.");
            }

            Terrain terrain = FindProductionTerrain(scene);
            TerrainData data = terrain.terrainData;
            Transform generatedRoot = FindGeneratedRoot(scene);
            bool hasTerrainVegetation = data.treeInstances.Length > 0 ||
                                        data.detailPrototypes.Length > 0;
            Transform[] existingSignatures = generatedRoot == null
                ? Array.Empty<Transform>()
                : generatedRoot.Cast<Transform>()
                    .Where(child => child.name.StartsWith(SignaturePrefix, StringComparison.Ordinal))
                    .ToArray();
            string expectedExistingSignature = generatedRoot == null
                ? string.Empty
                : SignaturePrefix + CalculatePlacementHash(generatedRoot, data).ToString("X16");
            bool hasRecognizedSignature = existingSignatures.Length == 1 &&
                                          existingSignatures[0].name == expectedExistingSignature;
            if (!allowExisting && (generatedRoot != null || hasTerrainVegetation))
            {
                throw new InvalidOperationException(
                    "The scene already contains terrain vegetation or a dressing hierarchy. " +
                    (hasRecognizedSignature
                        ? "Use Rebuild Natural World Dressing to regenerate the recognized production pass safely."
                        : "It is not recognized as generated output, so the tool will not overwrite it."));
            }

            if (allowExisting && (!hasTerrainVegetation || !hasRecognizedSignature))
            {
                throw new InvalidOperationException(
                    "Rebuild requires both the generated hierarchy signature and its TerrainData vegetation. " +
                    "Unrecognized or partial vegetation was left untouched.");
            }

            if (allowExisting)
            {
                ValidateDressing(scene, terrain, false);
            }

            EnsurePersistentBackups();
            generatedAssetSnapshots = CaptureGeneratedAssetSnapshots();
            generatedAssetMutationStarted = true;
            EnsureProductionNatureAssets();
            ValidateRequiredSourceAssets();

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (AssetOrMetaExists(TerrainRepairTempPath))
            {
                throw new InvalidOperationException(
                    "A prior dressing repair temp asset still exists. Inspect that recovery state before retrying.");
            }

            if (!AssetDatabase.CopyAsset(ProductionTerrainPath, TerrainRepairTempPath))
            {
                throw new InvalidOperationException("Could not create the temporary TerrainData rollback copy.");
            }

            AssetDatabase.ImportAsset(TerrainRepairTempPath, ImportAssetOptions.ForceSynchronousImport);
            repairBackup = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainRepairTempPath);
            if (repairBackup == null)
            {
                throw new InvalidOperationException("The temporary TerrainData rollback copy could not be loaded.");
            }

            sceneBytes = File.ReadAllBytes(ProjectAbsolutePath(OpenWorldScene));
            mutationStarted = true;

            ApplyTerrainSettings(terrain);
            TerrainSampler sampler = new TerrainSampler(terrain);
            BuildTrees(data, terrain, sampler);
            BuildGrass(data, terrain, sampler);
            Transform root = EnsureGeneratedHierarchy(scene);
            ReconcileRocks(scene, root, terrain, sampler);
            UpdatePlacementSignature(root, data);

            EditorSceneManager.MarkSceneDirty(scene);
            ValidateDressing(scene, terrain, true);

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene, OpenWorldScene))
            {
                throw new InvalidOperationException("Unity could not save the dressed open-world scene.");
            }

            if (!AssetDatabase.DeleteAsset(TerrainRepairTempPath))
            {
                throw new InvalidOperationException(
                    "The dressing was saved, but Unity could not delete the temporary rollback TerrainData. " +
                    "The run will be restored rather than left in an ambiguous state.");
            }
            AssetDatabase.SaveAssets();
            committed = true;
            generatedAssetMutationStarted = false;

            Debug.Log("Bloodroot natural world dressing built and validated. " + BuildValidationSummary(terrain));
            EditorUtility.DisplayDialog(
                "Natural World Dressing Complete",
                BuildValidationSummary(terrain) +
                "\n\nThe progression road, creek channel, gates, arrival pads, Estate overlook, and Hollow combat floor remain clear.",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            rollbackCompleted = !mutationStarted;
            if (mutationStarted)
            {
                try
                {
                    if (repairBackup != null)
                    {
                        TerrainData live = AssetDatabase.LoadAssetAtPath<TerrainData>(ProductionTerrainPath);
                        if (live != null)
                        {
                            EditorUtility.CopySerialized(repairBackup, live);
                            live.name = Path.GetFileNameWithoutExtension(ProductionTerrainPath);
                            EditorUtility.SetDirty(live);
                            AssetDatabase.SaveAssets();
                        }
                    }

                    if (sceneBytes != null)
                    {
                        AssetDatabase.ReleaseCachedFileHandles();
                        File.WriteAllBytes(ProjectAbsolutePath(OpenWorldScene), sceneBytes);
                        AssetDatabase.ImportAsset(OpenWorldScene, ImportAssetOptions.ForceSynchronousImport);
                        ReloadTargetScenePreservingOthers(scene);
                    }

                    rollbackCompleted = true;
                }
                catch (Exception rollbackException)
                {
                    Debug.LogException(rollbackException);
                }
            }

            bool generatedAssetsRestored = !generatedAssetMutationStarted;
            if (generatedAssetMutationStarted)
            {
                try
                {
                    RestoreGeneratedAssetSnapshots(generatedAssetSnapshots);
                    generatedAssetsRestored = true;
                }
                catch (Exception generatedAssetRollbackException)
                {
                    Debug.LogException(generatedAssetRollbackException);
                }
            }

            rollbackCompleted = rollbackCompleted && generatedAssetsRestored;

            if (rollbackCompleted && AssetOrMetaExists(TerrainRepairTempPath))
            {
                AssetDatabase.DeleteAsset(TerrainRepairTempPath);
            }

            EditorUtility.DisplayDialog(
                "Natural Dressing Failed",
                exception.Message +
                (rollbackCompleted
                    ? "\n\nThe saved scene and production TerrainData were restored."
                    : "\n\nAutomatic rollback also failed. The DressingRepairTemp TerrainData was retained for manual recovery; inspect the Console before changing the scene."),
                "OK");
        }
        finally
        {
            AssetDatabase.SaveAssets();
            Scene restored = SceneManager.GetSceneByPath(OpenWorldScene);
            if (previous.IsValid() && previous.isLoaded)
            {
                SceneManager.SetActiveScene(previous);
            }
            else if (restored.IsValid() && restored.isLoaded)
            {
                SceneManager.SetActiveScene(restored);
            }

            if (openedHere && committed)
            {
                Scene loaded = SceneManager.GetSceneByPath(OpenWorldScene);
                if (loaded.IsValid() && loaded.isLoaded)
                {
                    SceneManager.SetActiveScene(loaded);
                }
            }
        }
    }

    private static bool TryOpenTargetScene(out Scene scene, out bool openedHere)
    {
        scene = default;
        openedHere = false;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(OpenWorldScene) == null)
        {
            EditorUtility.DisplayDialog(
                "Open World Missing",
                "Assets/Scenes/OpenWorld/Bloodroot_OpenWorld.unity is missing.",
                "OK");
            return false;
        }

        scene = SceneManager.GetSceneByPath(OpenWorldScene);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(OpenWorldScene, OpenSceneMode.Additive);
            openedHere = true;
        }

        return scene.IsValid() && scene.isLoaded;
    }

    private static void ReloadTargetScenePreservingOthers(Scene target)
    {
        bool targetWasActive = SceneManager.GetActiveScene().path == OpenWorldScene;
        Scene temporary = default;
        if (target.IsValid() && target.isLoaded && SceneManager.sceneCount == 1)
        {
            temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        }

        if (target.IsValid() && target.isLoaded && !EditorSceneManager.CloseScene(target, true))
        {
            throw new InvalidOperationException("Could not close the mutated open-world scene during rollback.");
        }

        Scene restored = EditorSceneManager.OpenScene(OpenWorldScene, OpenSceneMode.Additive);
        if (!restored.IsValid() || !restored.isLoaded)
        {
            throw new InvalidOperationException("Could not reopen the restored open-world scene during rollback.");
        }

        if (targetWasActive)
        {
            SceneManager.SetActiveScene(restored);
        }

        if (temporary.IsValid() && temporary.isLoaded)
        {
            EditorSceneManager.CloseScene(temporary, true);
        }
    }

    private static Terrain FindProductionTerrain(Scene scene)
    {
        Terrain[] terrains = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Terrain>(true))
            .ToArray();
        if (terrains.Length != 1)
        {
            throw new InvalidOperationException(
                $"Expected one Terrain in Bloodroot_OpenWorld, found {terrains.Length}.");
        }

        Terrain terrain = terrains[0];
        string path = AssetDatabase.GetAssetPath(terrain.terrainData);
        if (path != ProductionTerrainPath)
        {
            throw new InvalidOperationException(
                "The scene Terrain is not using Bloodroot_OpenWorld_Terrain_Production.asset.");
        }

        TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
        if (collider == null || collider.terrainData != terrain.terrainData)
        {
            throw new InvalidOperationException("Terrain and TerrainCollider must share the production TerrainData.");
        }

        return terrain;
    }

    private static void ValidateRequiredSourceAssets()
    {
        foreach (string path in SourceTreePrefabPaths.Concat(RockPrefabPaths))
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                throw new InvalidOperationException("Required nature prefab is missing: " + path);
            }
        }

        foreach (string path in ProductionTreePrefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || prefab.GetComponent<LODGroup>() == null)
            {
                throw new InvalidOperationException(
                    "Terrain tree production prefab is missing or lacks a root LODGroup: " + path);
            }
        }

        foreach (string path in ProductionGrassPrefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null ||
                prefab.GetComponent<MeshFilter>() == null ||
                prefab.GetComponent<MeshRenderer>() == null)
            {
                throw new InvalidOperationException(
                    "Terrain grass production prefab is missing or invalid: " + path);
            }
        }
    }

    private static void EnsurePersistentBackups()
    {
        EnsureFolder(BackupFolder);
        bool terrainBackupExists = AssetOrMetaExists(TerrainBackupPath);
        bool sceneBackupExists = AssetOrMetaExists(SceneBackupPath);
        if (terrainBackupExists != sceneBackupExists)
        {
            throw new InvalidOperationException(
                "The pre-dressing backup pair is incomplete. Restore both matching backup files or remove the remaining half manually; " +
                "the tool will not synthesize a mixed-era recovery pair from the dressed live scene.");
        }

        bool createdPair = false;
        if (!terrainBackupExists)
        {
            bool createdTerrain = false;
            bool createdScene = false;
            try
            {
                if (!AssetDatabase.CopyAsset(ProductionTerrainPath, TerrainBackupPath))
                {
                    throw new InvalidOperationException("Could not create the pre-dressing TerrainData backup.");
                }

                createdTerrain = true;
                if (!AssetDatabase.CopyAsset(OpenWorldScene, SceneBackupPath))
                {
                    throw new InvalidOperationException("Could not create the pre-dressing scene backup.");
                }

                createdScene = true;
                createdPair = true;
                AssetDatabase.SaveAssets();
            }
            catch
            {
                if (createdScene)
                {
                    AssetDatabase.DeleteAsset(SceneBackupPath);
                }

                if (createdTerrain)
                {
                    AssetDatabase.DeleteAsset(TerrainBackupPath);
                }

                throw;
            }
        }

        Scene alreadyLoaded = SceneManager.GetSceneByPath(SceneBackupPath);
        if (alreadyLoaded.IsValid() && alreadyLoaded.isLoaded)
        {
            throw new InvalidOperationException(
                "Close the pre-dressing backup scene before running the dressing tool so it can be verified without touching open work.");
        }

        TerrainData backupData = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainBackupPath);
        Scene activeBeforeBackupCheck = SceneManager.GetActiveScene();
        Scene backupScene = default(Scene);
        bool backupVerified = false;
        try
        {
            backupScene = EditorSceneManager.OpenScene(SceneBackupPath, OpenSceneMode.Additive);
            Terrain[] terrains = backupScene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Terrain>(true))
                .ToArray();
            if (terrains.Length != 1 || backupData == null)
            {
                throw new InvalidOperationException("The pre-dressing scene/TerrainData backup pair is invalid.");
            }

            TerrainCollider collider = terrains[0].GetComponent<TerrainCollider>();
            if (collider == null)
            {
                throw new InvalidOperationException("The pre-dressing scene backup lost its TerrainCollider.");
            }

            if (createdPair)
            {
                terrains[0].terrainData = backupData;
                collider.terrainData = backupData;
                EditorSceneManager.MarkSceneDirty(backupScene);
                if (!EditorSceneManager.SaveScene(backupScene, SceneBackupPath))
                {
                    throw new InvalidOperationException("Could not save the self-contained pre-dressing scene backup.");
                }
            }
            else if (terrains[0].terrainData != backupData || collider.terrainData != backupData)
            {
                throw new InvalidOperationException(
                    "The persistent pre-dressing scene and TerrainData no longer form their original self-contained pair.");
            }

            if (FindGeneratedRoot(backupScene) != null ||
                backupData.treeInstances.Length != 0 ||
                backupData.detailPrototypes.Length != 0)
            {
                throw new InvalidOperationException(
                    "The persistent pre-dressing backup already contains generated natural dressing and will not be replaced from live state.");
            }

            backupVerified = true;
        }
        finally
        {
            if (backupScene.IsValid() && backupScene.isLoaded)
            {
                EditorSceneManager.CloseScene(backupScene, true);
            }

            if (createdPair && !backupVerified)
            {
                AssetDatabase.DeleteAsset(SceneBackupPath);
                AssetDatabase.DeleteAsset(TerrainBackupPath);
            }

            if (activeBeforeBackupCheck.IsValid() && activeBeforeBackupCheck.isLoaded)
            {
                SceneManager.SetActiveScene(activeBeforeBackupCheck);
            }
        }
    }

    private static void ValidatePersistentBackupPair()
    {
        bool terrainBackupExists = AssetOrMetaExists(TerrainBackupPath);
        bool sceneBackupExists = AssetOrMetaExists(SceneBackupPath);
        if (!terrainBackupExists || !sceneBackupExists)
        {
            throw new InvalidOperationException(
                terrainBackupExists == sceneBackupExists
                    ? "Persistent pre-dressing backups are missing."
                    : "The persistent pre-dressing backup pair is incomplete.");
        }

        TerrainData backupData = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainBackupPath);
        Scene activeBeforeBackupCheck = SceneManager.GetActiveScene();
        Scene backupScene = SceneManager.GetSceneByPath(SceneBackupPath);
        bool openedHere = !backupScene.IsValid() || !backupScene.isLoaded;
        if (openedHere)
        {
            backupScene = EditorSceneManager.OpenScene(SceneBackupPath, OpenSceneMode.Additive);
        }

        try
        {
            if (backupScene.isDirty)
            {
                throw new InvalidOperationException(
                    "The pre-dressing backup scene has unsaved changes and cannot be verified safely.");
            }

            Terrain[] terrains = backupScene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Terrain>(true))
                .ToArray();
            TerrainCollider collider = terrains.Length == 1
                ? terrains[0].GetComponent<TerrainCollider>()
                : null;
            if (backupData == null || terrains.Length != 1 || collider == null ||
                terrains[0].terrainData != backupData || collider.terrainData != backupData)
            {
                throw new InvalidOperationException(
                    "The persistent pre-dressing scene/TerrainData pair is invalid or no longer self-contained.");
            }

            if (FindGeneratedRoot(backupScene) != null ||
                backupData.treeInstances.Length != 0 ||
                backupData.detailPrototypes.Length != 0)
            {
                throw new InvalidOperationException(
                    "The persistent pre-dressing backup contains generated natural dressing.");
            }

            string[] dependencies = AssetDatabase.GetDependencies(SceneBackupPath, true);
            if (!dependencies.Contains(TerrainBackupPath) || dependencies.Contains(ProductionTerrainPath))
            {
                throw new InvalidOperationException(
                    "The pre-dressing scene backup is not isolated from the live production TerrainData.");
            }
        }
        finally
        {
            if (openedHere && backupScene.IsValid() && backupScene.isLoaded)
            {
                EditorSceneManager.CloseScene(backupScene, true);
            }

            if (activeBeforeBackupCheck.IsValid() && activeBeforeBackupCheck.isLoaded)
            {
                SceneManager.SetActiveScene(activeBeforeBackupCheck);
            }
        }
    }

    private static void EnsureProductionNatureAssets()
    {
        EnsureFolder(GeneratedPrefabFolder);
        EnsureFolder(GeneratedMaterialFolder);

        for (int i = 0; i < SourceTreePrefabPaths.Length; i++)
        {
            PrepareProductionNaturePrefab(
                SourceTreePrefabPaths[i],
                ProductionTreePrefabPaths[i],
                "Tree",
                0.075f,
                true,
                false);
        }

        for (int i = 0; i < SourceGrassPrefabPaths.Length; i++)
        {
            PrepareProductionNaturePrefab(
                SourceGrassPrefabPaths[i],
                ProductionGrassPrefabPaths[i],
                "Grass" + (i + 1),
                i == 0 ? 0.06f : 0.045f,
                false,
                true);
        }

        AssetDatabase.SaveAssets();
    }

    private static List<GeneratedAssetSnapshot> CaptureGeneratedAssetSnapshots()
    {
        List<string> paths = ProductionTreePrefabPaths
            .Concat(ProductionGrassPrefabPaths)
            .Concat(ExpectedProductionMaterialPaths())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
        List<GeneratedAssetSnapshot> snapshots = new List<GeneratedAssetSnapshot>(paths.Count);
        foreach (string path in paths)
        {
            string absoluteAssetPath = ProjectAbsolutePath(path);
            string absoluteMetaPath = ProjectAbsolutePath(path + ".meta");
            bool assetExists = File.Exists(absoluteAssetPath);
            bool metaExists = File.Exists(absoluteMetaPath);
            if (assetExists != metaExists)
            {
                throw new InvalidOperationException(
                    "A generated nature asset has an incomplete asset/meta pair and will not be touched: " + path);
            }

            snapshots.Add(new GeneratedAssetSnapshot
            {
                AssetPath = path,
                AssetBytes = assetExists ? File.ReadAllBytes(absoluteAssetPath) : null,
                MetaBytes = metaExists ? File.ReadAllBytes(absoluteMetaPath) : null
            });
        }

        return snapshots;
    }

    private static IEnumerable<string> ExpectedProductionMaterialPaths()
    {
        foreach (string sourcePath in SourceTreePrefabPaths)
        {
            foreach (string path in ExpectedProductionMaterialPaths(sourcePath, "Tree"))
            {
                yield return path;
            }
        }

        for (int i = 0; i < SourceGrassPrefabPaths.Length; i++)
        {
            foreach (string path in ExpectedProductionMaterialPaths(
                         SourceGrassPrefabPaths[i],
                         "Grass" + (i + 1)))
            {
                yield return path;
            }
        }
    }

    private static IEnumerable<string> ExpectedProductionMaterialPaths(
        string sourcePrefabPath,
        string category)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePrefabPath);
        if (source == null)
        {
            throw new InvalidOperationException("Missing nature source prefab: " + sourcePrefabPath);
        }

        foreach (Material material in source.GetComponentsInChildren<Renderer>(true)
                     .SelectMany(renderer => renderer.sharedMaterials)
                     .Where(item => item != null)
                     .Distinct())
        {
            yield return ProductionMaterialPath(material, category, out string ignoredGuid);
        }
    }

    private static void RestoreGeneratedAssetSnapshots(List<GeneratedAssetSnapshot> snapshots)
    {
        if (snapshots == null)
        {
            throw new InvalidOperationException("Generated-asset rollback was requested without a snapshot.");
        }

        AssetDatabase.ReleaseCachedFileHandles();
        foreach (GeneratedAssetSnapshot snapshot in snapshots)
        {
            string absoluteAssetPath = ProjectAbsolutePath(snapshot.AssetPath);
            string absoluteMetaPath = ProjectAbsolutePath(snapshot.AssetPath + ".meta");
            if (snapshot.AssetBytes == null)
            {
                if (File.Exists(absoluteAssetPath))
                {
                    File.Delete(absoluteAssetPath);
                }

                if (File.Exists(absoluteMetaPath))
                {
                    File.Delete(absoluteMetaPath);
                }

                continue;
            }

            File.WriteAllBytes(absoluteAssetPath, snapshot.AssetBytes);
            File.WriteAllBytes(absoluteMetaPath, snapshot.MetaBytes);
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    private static void PrepareProductionNaturePrefab(
        string sourcePath,
        string targetPath,
        string materialCategory,
        float smoothness,
        bool requireRootLodGroup,
        bool disableShadows)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        if (source == null)
        {
            throw new InvalidOperationException("Missing nature source prefab: " + sourcePath);
        }

        bool targetExists = AssetOrMetaExists(targetPath);
        bool createdTarget = false;
        if (!targetExists)
        {
            if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
            {
                throw new InvalidOperationException("Could not create nature production prefab: " + targetPath);
            }

            createdTarget = true;
            AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceSynchronousImport);
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(targetPath) == null)
        {
            throw new InvalidOperationException("The generated prefab path is occupied by a different asset type: " + targetPath);
        }

        ValidateGeneratedAssetOwnership(
            targetPath,
            GeneratedAssetOwner + "|prefab|" + RequireAssetGuid(sourcePath),
            createdTarget);

        Dictionary<string, Material[]> sourceMaterials = source
            .GetComponentsInChildren<Renderer>(true)
            .ToDictionary(
                renderer => RelativeTransformPath(source.transform, renderer.transform),
                renderer => renderer.sharedMaterials);
        GameObject contents = PrefabUtility.LoadPrefabContents(targetPath);
        try
        {
            contents.name = Path.GetFileNameWithoutExtension(targetPath);
            Renderer[] renderers = contents.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                throw new InvalidOperationException("Nature prefab has no renderer: " + targetPath);
            }

            foreach (Renderer renderer in renderers)
            {
                string relativePath = RelativeTransformPath(contents.transform, renderer.transform);
                if (!sourceMaterials.TryGetValue(relativePath, out Material[] originalMaterials))
                {
                    throw new InvalidOperationException(
                        "Production nature prefab renderer no longer matches its source: " + targetPath + " / " + relativePath);
                }

                Material[] productionMaterials = new Material[originalMaterials.Length];
                for (int materialIndex = 0; materialIndex < originalMaterials.Length; materialIndex++)
                {
                    productionMaterials[materialIndex] = GetOrCreateProductionMaterial(
                        originalMaterials[materialIndex],
                        materialCategory,
                        smoothness);
                }

                renderer.sharedMaterials = productionMaterials;
                if (disableShadows)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = true;
                }
            }

            if (requireRootLodGroup && contents.GetComponent<LODGroup>() == null)
            {
                LODGroup lodGroup = contents.AddComponent<LODGroup>();
                lodGroup.SetLODs(new[] { new LOD(0.0015f, renderers) });
                lodGroup.RecalculateBounds();
            }

            else if (requireRootLodGroup)
            {
                LODGroup lodGroup = contents.GetComponent<LODGroup>();
                LOD[] lods = lodGroup.GetLODs();
                if (lods.Length > 0)
                {
                    lods[lods.Length - 1].screenRelativeTransitionHeight = 0.0015f;
                    lodGroup.SetLODs(lods);
                    lodGroup.RecalculateBounds();
                }
            }

            if (!requireRootLodGroup &&
                (contents.GetComponent<MeshFilter>() == null ||
                 contents.GetComponent<MeshRenderer>() == null))
            {
                throw new InvalidOperationException(
                    "Terrain grass production prefab must have a root MeshFilter and MeshRenderer: " + targetPath);
            }

            PrefabUtility.SaveAsPrefabAsset(contents, targetPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static Material GetOrCreateProductionMaterial(
        Material sourceMaterial,
        string category,
        float smoothness)
    {
        if (sourceMaterial == null)
        {
            return null;
        }

        string sourceMaterialPath = AssetDatabase.GetAssetPath(sourceMaterial);
        string materialPath = ProductionMaterialPath(sourceMaterial, category, out string guid);
        Material productionMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        bool materialExists = AssetOrMetaExists(materialPath);
        bool createdMaterial = false;
        if (!materialExists)
        {
            if (!AssetDatabase.CopyAsset(sourceMaterialPath, materialPath))
            {
                throw new InvalidOperationException("Could not create nature material copy: " + materialPath);
            }

            createdMaterial = true;
            AssetDatabase.ImportAsset(materialPath, ImportAssetOptions.ForceSynchronousImport);
            productionMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        }

        if (productionMaterial == null)
        {
            throw new InvalidOperationException("The generated material path is occupied by a different asset type: " + materialPath);
        }

        ValidateGeneratedAssetOwnership(
            materialPath,
            GeneratedAssetOwner + "|material|" + category + "|" + guid,
            createdMaterial);
        productionMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (productionMaterial == null)
        {
            throw new InvalidOperationException("Generated material could not be reloaded after ownership validation: " + materialPath);
        }

        productionMaterial.enableInstancing = true;
        productionMaterial.name = Path.GetFileNameWithoutExtension(materialPath);
        if (productionMaterial.HasProperty("_Metallic"))
        {
            productionMaterial.SetFloat("_Metallic", 0f);
        }

        if (productionMaterial.HasProperty("_Smoothness"))
        {
            productionMaterial.SetFloat("_Smoothness", smoothness);
        }

        if (productionMaterial.HasProperty("_Glossiness"))
        {
            productionMaterial.SetFloat("_Glossiness", smoothness);
        }

        EditorUtility.SetDirty(productionMaterial);
        return productionMaterial;
    }

    private static string ProductionMaterialPath(
        Material sourceMaterial,
        string category,
        out string sourceGuid)
    {
        string sourceMaterialPath = AssetDatabase.GetAssetPath(sourceMaterial);
        sourceGuid = AssetDatabase.AssetPathToGUID(sourceMaterialPath);
        if (string.IsNullOrEmpty(sourceMaterialPath) || string.IsNullOrEmpty(sourceGuid))
        {
            throw new InvalidOperationException("A nature source material is not a persistent asset: " + sourceMaterial.name);
        }

        return GeneratedMaterialFolder + "/" + category + "_" +
               SanitizeFileName(sourceMaterial.name) + "_" + sourceGuid.Substring(0, 8) + ".mat";
    }

    private static string RelativeTransformPath(Transform root, Transform target)
    {
        if (root == target)
        {
            return string.Empty;
        }

        Stack<string> segments = new Stack<string>();
        Transform current = target;
        while (current != null && current != root)
        {
            segments.Push(current.name);
            current = current.parent;
        }

        if (current != root)
        {
            throw new InvalidOperationException("Renderer is not a descendant of its prefab root.");
        }

        return string.Join("/", segments);
    }

    private static void ApplyTerrainSettings(Terrain terrain)
    {
        terrain.drawTreesAndFoliage = true;
        terrain.treeDistance = 775f;
        terrain.treeBillboardDistance = 90f;
        terrain.treeCrossFadeLength = 20f;
        terrain.treeMaximumFullLODCount = 100;
        terrain.detailObjectDistance = 82f;
        terrain.detailObjectDensity = 0.88f;
        TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
        if (collider != null)
        {
            SerializedObject serializedCollider = new SerializedObject(collider);
            SerializedProperty treeColliders = serializedCollider.FindProperty("m_EnableTreeColliders");
            if (treeColliders != null)
            {
                treeColliders.boolValue = true;
                serializedCollider.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        EditorUtility.SetDirty(terrain);
    }

    private static void BuildTrees(TerrainData data, Terrain terrain, TerrainSampler sampler)
    {
        GameObject[] prefabs = ProductionTreePrefabPaths
            .Select(path => AssetDatabase.LoadAssetAtPath<GameObject>(path))
            .ToArray();
        TreePrototype[] prototypes = prefabs
            .Select(prefab => new TreePrototype { prefab = prefab, navMeshLod = 0 })
            .ToArray();
        data.treePrototypes = prototypes;
        data.RefreshPrototypes();

        List<ScatterCandidate>[] candidates = CreateCandidateBuckets(
            terrain,
            sampler,
            TreeGrid,
            0x7410A5E1u,
            IsTreeEligible,
            point => TreeTerrainAffinity(sampler, point));

        List<TreeInstance> instances = new List<TreeInstance>(TreeTargets.Sum());
        SpatialPointSet accepted = new SpatialPointSet(MinimumTreeSpacing);
        for (int biomeIndex = 0; biomeIndex < TreeTargets.Length; biomeIndex++)
        {
            int added = 0;
            foreach (ScatterCandidate candidate in candidates[biomeIndex]
                         .OrderByDescending(item => item.Score)
                         .ThenBy(item => item.Hash))
            {
                if (!accepted.TryAdd(candidate.Point))
                {
                    continue;
                }

                int prototypeIndex = ChooseTreePrototype(candidate.Biome, candidate.Hash);
                float heightScale = TreeScale(prototypeIndex, Hash01(candidate.Hash ^ 0xA53C91E5u));
                float widthScale = heightScale * Mathf.Lerp(
                    0.91f,
                    1.04f,
                    Hash01(candidate.Hash ^ 0xC12F92A7u));
                Vector3 normalized = WorldToNormalized(terrain, candidate.Point);
                instances.Add(new TreeInstance
                {
                    position = normalized,
                    prototypeIndex = prototypeIndex,
                    widthScale = widthScale,
                    heightScale = heightScale,
                    rotation = Hash01(candidate.Hash ^ 0x9E3779B9u) * Mathf.PI * 2f,
                    color = Color.white,
                    lightmapColor = Color.white
                });
                added++;
                if (added >= TreeTargets[biomeIndex])
                {
                    break;
                }
            }

            if (added != TreeTargets[biomeIndex])
            {
                throw new InvalidOperationException(
                    $"Could only place {added} of {TreeTargets[biomeIndex]} requested {((Biome)biomeIndex)} trees.");
            }
        }

        TreeInstance[] ordered = instances
            .OrderBy(instance => instance.position.z)
            .ThenBy(instance => instance.position.x)
            .ThenBy(instance => instance.prototypeIndex)
            .ToArray();
        data.SetTreeInstances(ordered, true);
        data.RefreshPrototypes();
        EditorUtility.SetDirty(data);
    }

    private static void BuildGrass(TerrainData data, Terrain terrain, TerrainSampler sampler)
    {
        GameObject[] grassPrefabs = ProductionGrassPrefabPaths
            .Select(path => AssetDatabase.LoadAssetAtPath<GameObject>(path))
            .ToArray();
        DetailPrototype[] prototypes = new DetailPrototype[grassPrefabs.Length];
        for (int i = 0; i < prototypes.Length; i++)
        {
            prototypes[i] = new DetailPrototype
            {
                prototype = grassPrefabs[i],
                usePrototypeMesh = true,
                renderMode = DetailRenderMode.VertexLit,
                useInstancing = true,
                minWidth = 0.78f,
                maxWidth = 1.32f,
                minHeight = 0.78f,
                maxHeight = 1.34f,
                noiseSeed = 9107 + i * 271,
                noiseSpread = 0.18f,
                positionJitter = 0.86f,
                alignToGround = 0.78f,
                healthyColor = Color.white,
                dryColor = new Color(0.76f, 0.69f, 0.52f, 1f)
            };
            if (!prototypes[i].Validate(out string validationError))
            {
                throw new InvalidOperationException(
                    "Invalid grass detail prototype: " + validationError);
            }
        }

        bool prototypesMatch = data.detailPrototypes.Length == prototypes.Length;
        for (int i = 0; prototypesMatch && i < prototypes.Length; i++)
        {
            prototypesMatch = data.detailPrototypes[i].prototype == prototypes[i].prototype;
        }

        bool resolutionMatches = data.detailResolution == DetailResolution &&
                                 data.detailResolutionPerPatch == DetailResolutionPerPatch;
        if (!resolutionMatches)
        {
            data.SetDetailResolution(DetailResolution, DetailResolutionPerPatch);
            prototypesMatch = false;
        }

        if (!prototypesMatch)
        {
            data.detailPrototypes = prototypes;
        }
        else
        {
            // Update tunable prototype values without changing membership or order.
            data.detailPrototypes = prototypes;
        }

        data.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
        data.RefreshPrototypes();

        int[][,] maps =
        {
            new int[DetailResolution, DetailResolution],
            new int[DetailResolution, DetailResolution]
        };
        float[,,] alpha = data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight);
        Vector3 terrainPosition = terrain.transform.position;
        Vector3 size = data.size;
        for (int z = 0; z < DetailResolution; z++)
        {
            float nz = (z + 0.5f) / DetailResolution;
            float worldZ = terrainPosition.z + nz * size.z;
            int alphaZ = Mathf.Clamp(
                Mathf.FloorToInt(nz * data.alphamapHeight),
                0,
                data.alphamapHeight - 1);
            for (int x = 0; x < DetailResolution; x++)
            {
                float nx = (x + 0.5f) / DetailResolution;
                float worldX = terrainPosition.x + nx * size.x;
                Vector2 point = new Vector2(worldX, worldZ);
                float slope = sampler.Slope(point);
                if (!IsGrassEligible(point, slope, sampler.WorldHeight(point)))
                {
                    continue;
                }

                int alphaX = Mathf.Clamp(
                    Mathf.FloorToInt(nx * data.alphamapWidth),
                    0,
                    data.alphamapWidth - 1);
                float loam = alpha[alphaZ, alphaX, 0];
                float mud = alpha[alphaZ, alphaX, Mathf.Min(1, data.alphamapLayers - 1)];
                float gravel = alpha[alphaZ, alphaX, Mathf.Min(2, data.alphamapLayers - 1)];
                float clay = alpha[alphaZ, alphaX, Mathf.Min(3, data.alphamapLayers - 1)];
                float corruption = alpha[alphaZ, alphaX, Mathf.Min(4, data.alphamapLayers - 1)];
                Biome biome = ClassifyBiome(point);
                float chance = GrassChance(biome, point);
                chance *= Mathf.Clamp01(1f - Mathf.InverseLerp(22f, 35f, slope));
                chance *= Mathf.Clamp01(0.45f + loam * 0.62f + mud * 0.32f -
                                        gravel * 0.93f - clay * 0.36f - corruption * 0.40f);
                float patch = Mathf.PerlinNoise(
                    (worldX + 1260f) * 0.014f,
                    (worldZ - 730f) * 0.014f);
                chance *= Mathf.Lerp(0.45f, 1.25f, patch);

                uint hash = Hash2D(x, z, 0x6D2B79F5u);
                if (Hash01(hash) >= Mathf.Clamp01(chance))
                {
                    continue;
                }

                bool wet = mud > 0.18f || DistanceToPolyline(point, BlackPinesCreek) < 22f;
                int layer = wet ? 0 : 1;
                if (Hash01(hash ^ 0xB5297A4Du) < 0.19f)
                {
                    layer = 1 - layer;
                }

                int count = Hash01(hash ^ 0x68E31DA4u) < 0.11f ? 2 : 1;
                maps[layer][z, x] = count;
            }
        }

        data.SetDetailLayer(0, 0, 0, maps[0]);
        data.SetDetailLayer(0, 0, 1, maps[1]);
        EditorUtility.SetDirty(data);
    }

    private static List<ScatterCandidate>[] CreateCandidateBuckets(
        Terrain terrain,
        TerrainSampler sampler,
        float grid,
        uint salt,
        Func<Vector2, float, float, bool> eligibility,
        Func<Vector2, float> terrainAffinity)
    {
        List<ScatterCandidate>[] buckets = Enumerable.Range(0, 5)
            .Select(_ => new List<ScatterCandidate>())
            .ToArray();
        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        int countX = Mathf.FloorToInt(size.x / grid);
        int countZ = Mathf.FloorToInt(size.z / grid);
        for (int z = 0; z < countZ; z++)
        {
            for (int x = 0; x < countX; x++)
            {
                uint hash = Hash2D(x, z, salt);
                float jitterX = (Hash01(hash ^ 0xA511E9B3u) - 0.5f) * grid * 0.78f;
                float jitterZ = (Hash01(hash ^ 0x63D83595u) - 0.5f) * grid * 0.78f;
                Vector2 point = new Vector2(
                    origin.x + (x + 0.5f) * grid + jitterX,
                    origin.z + (z + 0.5f) * grid + jitterZ);
                float slope = sampler.Slope(point);
                float height = sampler.WorldHeight(point);
                if (!eligibility(point, slope, height))
                {
                    continue;
                }

                Biome biome = ClassifyBiome(point);
                float affinity = terrainAffinity(point);
                if (affinity < -1.25f)
                {
                    continue;
                }

                float broadNoise = Mathf.PerlinNoise(
                    (point.x + 1820f) * 0.0041f,
                    (point.y - 920f) * 0.0041f);
                float detailNoise = Mathf.PerlinNoise(
                    (point.x - 410f) * 0.013f,
                    (point.y + 1510f) * 0.013f);
                float score = broadNoise * 0.58f + detailNoise * 0.24f +
                              Hash01(hash ^ 0xC2B2AE35u) * 0.18f +
                              affinity * 0.22f;
                buckets[(int)biome].Add(new ScatterCandidate(
                    x,
                    z,
                    point,
                    biome,
                    slope,
                    score,
                    hash));
            }
        }

        return buckets;
    }

    private static bool IsTreeEligible(Vector2 point, float slope, float height)
    {
        if (height < -0.25f || slope > 32f || IsNearTerrainEdge(point, 25f))
        {
            return false;
        }

        if (DistanceToPolyline(point, ProgressionRoad) < 14f ||
            DistanceToPolyline(point, BlackPinesCreek) < 10f ||
            ProgressionGates.Any(gate => Vector2.Distance(point, gate) < 26f))
        {
            return false;
        }

        if (Vector2.Distance(point, new Vector2(-350f, -150f)) < 38f ||
            Vector2.Distance(point, new Vector2(-362f, -142f)) < 20f ||
            InsideEllipse(point, new Vector2(335f, -30f), 115f, 95f) ||
            InsideEllipse(point, new Vector2(-10f, 395f), 42f, 72f) ||
            Vector2.Distance(point, new Vector2(40f, 360f)) < 28f ||
            Vector2.Distance(point, new Vector2(50f, 600f)) < 95f ||
            InsideEstateViewFan(point, 165f, 44f))
        {
            return false;
        }

        return true;
    }

    private static bool IsRockEligible(Vector2 point, float slope, float height)
    {
        if (height < -0.4f || slope > 44f || IsNearTerrainEdge(point, 15f))
        {
            return false;
        }

        if (DistanceToPolyline(point, ProgressionRoad) < 11f ||
            DistanceToPolyline(point, BlackPinesCreek) < 5f ||
            ProgressionGates.Any(gate => Vector2.Distance(point, gate) < 24f))
        {
            return false;
        }

        if (Vector2.Distance(point, new Vector2(-350f, -150f)) < 34f ||
            Vector2.Distance(point, new Vector2(-362f, -142f)) < 18f ||
            InsideEllipse(point, new Vector2(335f, -30f), 104f, 86f) ||
            InsideEllipse(point, new Vector2(-10f, 395f), 38f, 64f) ||
            Vector2.Distance(point, new Vector2(40f, 360f)) < 24f ||
            Vector2.Distance(point, new Vector2(50f, 600f)) < 72f ||
            InsideEstateViewFan(point, 150f, 42f))
        {
            return false;
        }

        return true;
    }

    private static bool IsGrassEligible(Vector2 point, float slope, float height)
    {
        if (height < -0.25f || slope > 35f || IsNearTerrainEdge(point, 5f))
        {
            return false;
        }

        if (DistanceToPolyline(point, ProgressionRoad) < 6f ||
            DistanceToPolyline(point, BlackPinesCreek) < 3f ||
            ProgressionGates.Any(gate => Vector2.Distance(point, gate) < 12f))
        {
            return false;
        }

        if (Vector2.Distance(point, new Vector2(-350f, -150f)) < 18f ||
            Vector2.Distance(point, new Vector2(-362f, -142f)) < 12f ||
            InsideEllipse(point, new Vector2(335f, -30f), 76f, 62f) ||
            InsideEllipse(point, new Vector2(-10f, 395f), 30f, 52f) ||
            Vector2.Distance(point, new Vector2(40f, 360f)) < 15f ||
            Vector2.Distance(point, new Vector2(50f, 600f)) < 55f)
        {
            return false;
        }

        return true;
    }

    private static Biome ClassifyBiome(Vector2 point)
    {
        if (point.y >= 410f &&
            InsideEllipse(point, new Vector2(50f, 585f), 240f, 190f))
        {
            return Biome.BloodrootHollow;
        }

        if (InsideEllipse(point, new Vector2(15f, 340f), 320f, 250f))
        {
            return Biome.HarrowEstate;
        }

        if (InsideEllipse(point, new Vector2(320f, -40f), 330f, 250f))
        {
            return Biome.Stillwater;
        }

        if (InsideEllipse(point, new Vector2(-330f, -190f), 420f, 360f))
        {
            return Biome.BlackPines;
        }

        return Biome.Transition;
    }

    private static int ChooseTreePrototype(Biome biome, uint hash)
    {
        float value = Hash01(hash ^ 0x27D4EB2Fu);
        switch (biome)
        {
            case Biome.BlackPines:
                return value < 0.86f ? 0 : value < 0.90f ? 1 : value < 0.94f ? 2 : 3;
            case Biome.Stillwater:
                return value < 0.60f ? 0 : value < 0.67f ? 1 : value < 0.75f ? 2 : 3;
            case Biome.HarrowEstate:
                return value < 0.45f ? 0 : value < 0.50f ? 1 : value < 0.55f ? 2 : 3;
            case Biome.BloodrootHollow:
                return value < 0.65f ? 0 : value < 0.72f ? 1 : value < 0.80f ? 2 : 3;
            default:
                return value < 0.70f ? 0 : value < 0.75f ? 1 : value < 0.80f ? 2 : 3;
        }
    }

    private static float TreeTerrainAffinity(TerrainSampler sampler, Vector2 point)
    {
        float loam = sampler.LayerWeight(point, 0);
        float mud = sampler.LayerWeight(point, 1);
        float gravel = sampler.LayerWeight(point, 2);
        float clay = sampler.LayerWeight(point, 3);
        float corruption = sampler.LayerWeight(point, 4);
        float affinity = loam * 0.70f + mud * 0.32f - gravel * 0.95f - clay * 0.32f;
        if (gravel > 0.48f || clay > 0.76f ||
            (corruption > 0.76f && ClassifyBiome(point) != Biome.BloodrootHollow))
        {
            return -2f;
        }
        affinity += ClassifyBiome(point) == Biome.BloodrootHollow
            ? corruption * 0.08f
            : -corruption * 0.40f;
        float creekDistance = DistanceToPolyline(point, BlackPinesCreek);
        if (creekDistance >= 12f && creekDistance <= 30f)
        {
            affinity += 0.22f;
        }

        return Mathf.Clamp(affinity, -1f, 1f);
    }

    private static float RockTerrainAffinity(TerrainSampler sampler, Vector2 point)
    {
        float loam = sampler.LayerWeight(point, 0);
        float mud = sampler.LayerWeight(point, 1);
        float gravel = sampler.LayerWeight(point, 2);
        float clay = sampler.LayerWeight(point, 3);
        float corruption = sampler.LayerWeight(point, 4);
        float affinity = clay * 0.82f + gravel * 0.28f + corruption * 0.32f -
                         loam * 0.12f + mud * 0.08f;
        float creekDistance = DistanceToPolyline(point, BlackPinesCreek);
        if (creekDistance >= 6f && creekDistance <= 18f)
        {
            affinity += 0.24f;
        }

        return Mathf.Clamp(affinity, -1f, 1f);
    }

    private static float TreeScale(int prototypeIndex, float value)
    {
        switch (prototypeIndex)
        {
            case 0:
                return Mathf.Lerp(0.88f, 1.34f, value);
            case 3:
                return Mathf.Lerp(0.86f, 1.22f, value);
            default:
                return Mathf.Lerp(0.88f, 1.28f, value);
        }
    }

    private static float GrassChance(Biome biome, Vector2 point)
    {
        float chance;
        switch (biome)
        {
            case Biome.BlackPines:
                chance = 0.66f;
                break;
            case Biome.Stillwater:
                chance = 0.30f;
                break;
            case Biome.HarrowEstate:
                chance = 0.52f;
                break;
            case Biome.BloodrootHollow:
                chance = Vector2.Distance(point, new Vector2(50f, 600f)) < 72f ? 0.12f : 0.33f;
                break;
            default:
                chance = 0.42f;
                break;
        }

        float creekDistance = DistanceToPolyline(point, BlackPinesCreek);
        if (creekDistance >= 4f && creekDistance <= 20f)
        {
            chance = Mathf.Max(chance, 0.72f);
        }

        return chance;
    }

    private static void ReconcileRocks(
        Scene scene,
        Transform generatedRoot,
        Terrain terrain,
        TerrainSampler sampler)
    {
        Transform rocksRoot = EnsureChild(generatedRoot, "Rocks");
        List<ScatterCandidate>[] candidates = CreateCandidateBuckets(
            terrain,
            sampler,
            RockGrid,
            0xD1B54A35u,
            IsRockEligible,
            point => RockTerrainAffinity(sampler, point));
        SpatialPointSet accepted = new SpatialPointSet(MinimumRockSpacing);
        Vector2[] treePoints = terrain.terrainData.treeInstances
            .Select(instance => NormalizedToWorld(terrain, instance.position))
            .ToArray();
        List<RockPlacement> placements = new List<RockPlacement>(RockTargets.Sum());
        for (int biomeIndex = 0; biomeIndex < RockTargets.Length; biomeIndex++)
        {
            int added = 0;
            foreach (ScatterCandidate candidate in candidates[biomeIndex]
                         .OrderByDescending(item => item.Score + RockSlopeBonus(item.Slope))
                         .ThenBy(item => item.Hash))
            {
                if (treePoints.Any(tree => (tree - candidate.Point).sqrMagnitude < 12.25f) ||
                    !accepted.TryAdd(candidate.Point))
                {
                    continue;
                }

                int prefabIndex = ChooseRockPrefab(candidate.Biome, candidate.Hash);
                float scale = Mathf.Lerp(0.68f, 1.52f, Hash01(candidate.Hash ^ 0x94D049BBu));
                if (prefabIndex == 2 || prefabIndex == 3)
                {
                    scale *= Mathf.Lerp(0.72f, 1.08f, Hash01(candidate.Hash ^ 0x2545F491u));
                }

                string prefix = BiomeCode(candidate.Biome);
                string name = $"ROCK_{prefix}_{candidate.GridX:D3}_{candidate.GridZ:D3}";
                placements.Add(new RockPlacement(
                    name,
                    RockPrefabPaths[prefabIndex],
                    candidate.Biome,
                    candidate.Point,
                    scale,
                    Hash01(candidate.Hash ^ 0xDB4F0B91u) * 360f,
                    Mathf.Lerp(0.12f, 0.26f, Hash01(candidate.Hash ^ 0xBBE05633u)),
                    prefabIndex <= 3 || Hash01(candidate.Hash ^ 0xA0F2EC75u) < 0.08f));
                added++;
                if (added >= RockTargets[biomeIndex])
                {
                    break;
                }
            }

            if (added != RockTargets[biomeIndex])
            {
                throw new InvalidOperationException(
                    $"Could only place {added} of {RockTargets[biomeIndex]} requested {((Biome)biomeIndex)} rocks.");
            }
        }

        Dictionary<string, GameObject> existing = rocksRoot
            .GetComponentsInChildren<Transform>(true)
            .Where(item => item != rocksRoot && item.name.StartsWith("ROCK_", StringComparison.Ordinal))
            .ToDictionary(item => item.name, item => item.gameObject);
        HashSet<string> retained = new HashSet<string>();

        foreach (RockPlacement placement in placements.OrderBy(item => item.Name, StringComparer.Ordinal))
        {
            Transform biomeRoot = EnsureChild(rocksRoot, BiomeDisplayName(placement.Biome));
            int chunkX = Mathf.FloorToInt((placement.Point.x + 700f) / 200f);
            int chunkZ = Mathf.FloorToInt((placement.Point.y + 700f) / 200f);
            Transform chunkRoot = EnsureChild(biomeRoot, $"Chunk_{chunkX:D2}_{chunkZ:D2}");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(placement.PrefabPath);
            GameObject instance = null;
            if (existing.TryGetValue(placement.Name, out GameObject old))
            {
                GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(old);
                if (source != null && AssetDatabase.GetAssetPath(source) == placement.PrefabPath)
                {
                    instance = old;
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(old);
                }
            }

            if (instance == null)
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                if (instance == null)
                {
                    throw new InvalidOperationException("Could not instantiate rock prefab: " + placement.PrefabPath);
                }
            }

            retained.Add(placement.Name);
            instance.name = placement.Name;
            instance.transform.SetParent(chunkRoot, false);
            instance.transform.localScale = Vector3.one * placement.Scale;
            Vector3 normal = sampler.Normal(placement.Point);
            Vector3 up = Vector3.Slerp(Vector3.up, normal, 0.70f).normalized;
            Vector3 yawForward = new Vector3(
                Mathf.Sin(placement.Yaw * Mathf.Deg2Rad),
                0f,
                Mathf.Cos(placement.Yaw * Mathf.Deg2Rad));
            Vector3 forward = Vector3.ProjectOnPlane(yawForward, up).normalized;
            if (forward.sqrMagnitude < 0.1f)
            {
                forward = Vector3.ProjectOnPlane(Vector3.forward, up).normalized;
            }

            instance.transform.rotation = Quaternion.LookRotation(forward, up);
            instance.transform.position = new Vector3(
                placement.Point.x,
                sampler.WorldHeight(placement.Point),
                placement.Point.y);
            Bounds bounds = CalculateRendererBounds(instance);
            float sink = bounds.size.y * placement.SinkFraction;
            instance.transform.position += Vector3.up *
                                           (sampler.WorldHeight(placement.Point) - sink - bounds.min.y);
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = placement.ColliderEnabled;
            }

            EditorUtility.SetDirty(instance.transform);
        }

        foreach (KeyValuePair<string, GameObject> item in existing)
        {
            if (!retained.Contains(item.Key) && item.Value != null)
            {
                UnityEngine.Object.DestroyImmediate(item.Value);
            }
        }

        RemoveEmptyGeneratedContainers(rocksRoot);
    }

    private static int ChooseRockPrefab(Biome biome, uint hash)
    {
        float value = Hash01(hash ^ 0x8CB92BA7u);
        if (biome == Biome.BloodrootHollow)
        {
            return value < 0.24f ? 2 : value < 0.44f ? 3 : 4 + Mathf.Min(2, (int)(value * 9f) % 3);
        }

        if (biome == Biome.HarrowEstate)
        {
            return value < 0.13f ? 1 : value < 0.25f ? 2 : 4 + Mathf.Min(2, (int)(value * 11f) % 3);
        }

        if (biome == Biome.BlackPines)
        {
            return value < 0.08f ? 0 : 4 + Mathf.Min(2, (int)(value * 13f) % 3);
        }

        return value < 0.18f ? 0 : value < 0.32f ? 1 : 4 + Mathf.Min(2, (int)(value * 7f) % 3);
    }

    private static float RockSlopeBonus(float slope)
    {
        return Mathf.Clamp01(1f - Mathf.Abs(slope - 22f) / 22f) * 0.14f;
    }

    private static Transform EnsureGeneratedHierarchy(Scene scene)
    {
        GameObject worldRoot = scene.GetRootGameObjects()
            .FirstOrDefault(root => root.name == "Bloodroot_OpenWorld");
        if (worldRoot == null)
        {
            throw new InvalidOperationException("Bloodroot_OpenWorld root is missing.");
        }

        Transform dressingRoot = EnsureChild(worldRoot.transform, DressingRootName);
        Transform generated = dressingRoot.Cast<Transform>()
            .FirstOrDefault(child => child.name.StartsWith(GeneratedRootPrefix, StringComparison.Ordinal));
        if (generated == null)
        {
            generated = EnsureChild(dressingRoot, GeneratedRootPrefix);
        }

        return generated;
    }

    private static Transform FindGeneratedRoot(Scene scene)
    {
        GameObject worldRoot = scene.GetRootGameObjects()
            .FirstOrDefault(root => root.name == "Bloodroot_OpenWorld");
        if (worldRoot == null)
        {
            return null;
        }

        Transform dressing = worldRoot.transform.Cast<Transform>()
            .FirstOrDefault(child => child.name == DressingRootName);
        return dressing == null
            ? null
            : dressing.Cast<Transform>()
                .FirstOrDefault(child => child.name.StartsWith(GeneratedRootPrefix, StringComparison.Ordinal));
    }

    private static Transform EnsureChild(Transform parent, string name)
    {
        Transform child = parent.Cast<Transform>().FirstOrDefault(item => item.name == name);
        if (child != null)
        {
            return child;
        }

        GameObject gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        return gameObject.transform;
    }

    private static void UpdatePlacementSignature(Transform root, TerrainData data)
    {
        ulong hash = CalculatePlacementHash(root, data);
        Transform signature = root.Cast<Transform>()
            .FirstOrDefault(child => child.name.StartsWith(SignaturePrefix, StringComparison.Ordinal));
        if (signature == null)
        {
            signature = EnsureChild(root, SignaturePrefix + hash.ToString("X16"));
        }
        else
        {
            signature.name = SignaturePrefix + hash.ToString("X16");
        }
    }

    private static ulong CalculatePlacementHash(Transform root, TerrainData data)
    {
        ulong hash = 1469598103934665603UL;
        foreach (TreeInstance tree in data.treeInstances)
        {
            HashValue(ref hash, tree.prototypeIndex);
            HashValue(ref hash, Mathf.RoundToInt(tree.position.x * 1000000f));
            HashValue(ref hash, Mathf.RoundToInt(tree.position.y * 1000000f));
            HashValue(ref hash, Mathf.RoundToInt(tree.position.z * 1000000f));
            HashValue(ref hash, Mathf.RoundToInt(tree.widthScale * 10000f));
            HashValue(ref hash, Mathf.RoundToInt(tree.heightScale * 10000f));
            HashValue(ref hash, Mathf.RoundToInt(tree.rotation * 10000f));
        }

        for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
        {
            int[,] map = data.GetDetailLayer(0, 0, data.detailWidth, data.detailHeight, layer);
            for (int z = 0; z < data.detailHeight; z++)
            {
                for (int x = 0; x < data.detailWidth; x++)
                {
                    if (map[z, x] != 0)
                    {
                        HashValue(ref hash, layer);
                        HashValue(ref hash, x);
                        HashValue(ref hash, z);
                        HashValue(ref hash, map[z, x]);
                    }
                }
            }
        }

        Transform rocksRoot = root.Find("Rocks");
        if (rocksRoot != null)
        {
            foreach (Transform rock in rocksRoot.GetComponentsInChildren<Transform>(true)
                         .Where(item => item.name.StartsWith("ROCK_", StringComparison.Ordinal))
                         .OrderBy(item => item.name, StringComparer.Ordinal))
            {
                HashString(ref hash, rock.name);
                HashVector(ref hash, rock.position, 1000f);
                HashVector(ref hash, rock.eulerAngles, 100f);
                HashVector(ref hash, rock.lossyScale, 1000f);
                GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(rock.gameObject);
                HashString(ref hash, source == null ? string.Empty : AssetDatabase.GetAssetPath(source));
                foreach (Collider collider in rock.GetComponentsInChildren<Collider>(true))
                {
                    HashValue(ref hash, collider.enabled ? 1 : 0);
                }
            }
        }

        return hash;
    }

    private static void ValidateDressing(Scene scene, Terrain terrain, bool allowRepairTemp)
    {
        TerrainData data = terrain.terrainData;
        ValidateProductionNatureAssets();
        if (data.treePrototypes.Length != ProductionTreePrefabPaths.Length)
        {
            throw new InvalidOperationException("Terrain tree prototype count is incorrect.");
        }

        for (int i = 0; i < ProductionTreePrefabPaths.Length; i++)
        {
            if (AssetDatabase.GetAssetPath(data.treePrototypes[i].prefab) != ProductionTreePrefabPaths[i])
            {
                throw new InvalidOperationException("Terrain tree prototype order is incorrect.");
            }
        }

        int expectedTrees = TreeTargets.Sum();
        TreeInstance[] trees = data.treeInstances;
        if (trees.Length != expectedTrees)
        {
            throw new InvalidOperationException(
                $"Expected {expectedTrees} terrain trees, found {trees.Length}.");
        }

        TerrainSampler sampler = new TerrainSampler(terrain);
        SpatialPointSet treeSpacing = new SpatialPointSet(MinimumTreeSpacing - 0.02f);
        int[] biomeTreeCounts = new int[TreeTargets.Length];
        int[] prototypeTreeCounts = new int[ProductionTreePrefabPaths.Length];
        foreach (TreeInstance tree in trees)
        {
            if (tree.prototypeIndex < 0 || tree.prototypeIndex >= ProductionTreePrefabPaths.Length ||
                !IsFinite(tree.position) || tree.position.x < 0f || tree.position.x > 1f ||
                tree.position.z < 0f || tree.position.z > 1f)
            {
                throw new InvalidOperationException("A terrain tree has invalid normalized placement data.");
            }

            Vector2 point = NormalizedToWorld(terrain, tree.position);
            float slope = sampler.Slope(point);
            float height = sampler.WorldHeight(point);
            float snappedWorldY = terrain.transform.position.y + tree.position.y * data.size.y;
            if (!IsTreeEligible(point, slope, height) ||
                TreeTerrainAffinity(sampler, point) < -1.25f)
            {
                throw new InvalidOperationException("A generated tree violates a protected-space or terrain filter.");
            }

            if (!treeSpacing.TryAdd(point))
            {
                throw new InvalidOperationException("Generated trees violate the minimum spacing requirement.");
            }

            if (Mathf.Abs(snappedWorldY - height) > 0.18f ||
                tree.heightScale < 0.84f || tree.heightScale > 1.36f ||
                tree.widthScale < 0.75f || tree.widthScale > 1.42f ||
                tree.rotation < 0f || tree.rotation > Mathf.PI * 2f + 0.001f)
            {
                throw new InvalidOperationException("A generated tree has invalid height snapping, scale, or rotation.");
            }

            biomeTreeCounts[(int)ClassifyBiome(point)]++;
            prototypeTreeCounts[tree.prototypeIndex]++;
        }


        if (prototypeTreeCounts[0] < expectedTrees * 0.55f ||
            prototypeTreeCounts[1] + prototypeTreeCounts[2] > expectedTrees * 0.18f ||
            prototypeTreeCounts[3] < expectedTrees * 0.10f)
        {
            throw new InvalidOperationException(
                "Terrain tree prototype mix drifted outside the performance-safe biome palette.");
        }

        for (int i = 0; i < TreeTargets.Length; i++)
        {
            if (biomeTreeCounts[i] != TreeTargets[i])
            {
                throw new InvalidOperationException(
                    $"{((Biome)i)} tree count drifted from {TreeTargets[i]} to {biomeTreeCounts[i]}.");
            }
        }

        if (data.detailResolution != DetailResolution ||
            data.detailResolutionPerPatch != DetailResolutionPerPatch ||
            data.detailPrototypes.Length != ProductionGrassPrefabPaths.Length)
        {
            throw new InvalidOperationException("Terrain grass resolution or prototype count is incorrect.");
        }

        int grassCount = 0;
        int populatedCells = 0;
        for (int layer = 0; layer < ProductionGrassPrefabPaths.Length; layer++)
        {
            DetailPrototype prototype = data.detailPrototypes[layer];
            if (AssetDatabase.GetAssetPath(prototype.prototype) != ProductionGrassPrefabPaths[layer] ||
                !prototype.usePrototypeMesh || !prototype.useInstancing)
            {
                throw new InvalidOperationException("Terrain grass prototype order or instancing configuration is incorrect.");
            }

            int[,] map = data.GetDetailLayer(0, 0, data.detailWidth, data.detailHeight, layer);
            for (int z = 0; z < data.detailHeight; z++)
            {
                for (int x = 0; x < data.detailWidth; x++)
                {
                    int count = map[z, x];
                    if (count < 0 || count > 2)
                    {
                        throw new InvalidOperationException("Terrain grass density contains an out-of-range value.");
                    }

                    grassCount += count;
                    if (count > 0)
                    {
                        Vector2 point = new Vector2(
                            terrain.transform.position.x + (x + 0.5f) / data.detailWidth * data.size.x,
                            terrain.transform.position.z + (z + 0.5f) / data.detailHeight * data.size.z);
                        float height = sampler.WorldHeight(point);
                        float slope = sampler.Slope(point);
                        if (!IsGrassEligible(point, slope, height))
                        {
                            throw new InvalidOperationException(
                                "A populated grass cell violates a road, creek, gate, mission-space, or slope exclusion.");
                        }

                        populatedCells++;
                    }
                }
            }
        }

        if (grassCount < 50000 || grassCount > 160000 || populatedCells < 40000)
        {
            throw new InvalidOperationException(
                $"Terrain grass coverage is outside the production budget: {grassCount} instances in {populatedCells} populated cells.");
        }

        Transform generatedRoot = FindGeneratedRoot(scene);
        if (generatedRoot == null)
        {
            throw new InvalidOperationException("Generated Natural Dressing hierarchy is missing.");
        }

        Transform rocksRoot = generatedRoot.Find("Rocks");
        if (rocksRoot == null)
        {
            throw new InvalidOperationException("Generated rock hierarchy is missing.");
        }

        Transform[] rocks = rocksRoot.GetComponentsInChildren<Transform>(true)
            .Where(item => item.name.StartsWith("ROCK_", StringComparison.Ordinal))
            .ToArray();
        if (rocks.Length != RockTargets.Sum() || rocks.Select(item => item.name).Distinct().Count() != rocks.Length)
        {
            throw new InvalidOperationException(
                $"Expected {RockTargets.Sum()} uniquely named rocks, found {rocks.Length}.");
        }

        int enabledRockColliders = 0;
        int[] biomeRockCounts = new int[RockTargets.Length];
        SpatialPointSet rockSpacing = new SpatialPointSet(MinimumRockSpacing - 0.02f);
        Vector2[] treePoints = trees.Select(item => NormalizedToWorld(terrain, item.position)).ToArray();
        foreach (Transform rock in rocks)
        {
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(rock.gameObject);
            string sourcePath = source == null ? string.Empty : AssetDatabase.GetAssetPath(source);
            if (!RockPrefabPaths.Contains(sourcePath))
            {
                throw new InvalidOperationException("A generated rock lost its approved prefab connection: " + rock.name);
            }

            Vector2 point = new Vector2(rock.position.x, rock.position.z);
            if (!IsRockEligible(point, sampler.Slope(point), sampler.WorldHeight(point)))
            {
                throw new InvalidOperationException("A generated rock violates a protected-space or terrain filter: " + rock.name);
            }


            if (!rockSpacing.TryAdd(point) ||
                treePoints.Any(treePoint => (treePoint - point).sqrMagnitude < 12.20f))
            {
                throw new InvalidOperationException(
                    "Generated rocks violate rock spacing or intersect a terrain-tree trunk: " + rock.name);
            }

            Bounds bounds = CalculateRendererBounds(rock.gameObject);
            float ground = sampler.WorldHeight(point);
            float buriedFraction = bounds.size.y <= 0.001f
                ? 0f
                : (ground - bounds.min.y) / bounds.size.y;
            if (buriedFraction < 0.09f || buriedFraction > 0.30f)
            {
                throw new InvalidOperationException(
                    $"Generated rock {rock.name} is floating or over-buried ({buriedFraction:P0}).");
            }

            Collider[] colliders = rock.GetComponentsInChildren<Collider>(true);
            int enabledOnRock = colliders.Count(collider => collider.enabled);
            enabledRockColliders += enabledOnRock;
            int prefabIndex = Array.IndexOf(RockPrefabPaths, sourcePath);
            if (prefabIndex <= 3 && enabledOnRock == 0)
            {
                throw new InvalidOperationException("A large generated rock has no active collider: " + rock.name);
            }

            biomeRockCounts[(int)ClassifyBiome(point)]++;
        }

        for (int i = 0; i < RockTargets.Length; i++)
        {
            if (biomeRockCounts[i] != RockTargets[i])
            {
                throw new InvalidOperationException(
                    $"{((Biome)i)} rock count drifted from {RockTargets[i]} to {biomeRockCounts[i]}.");
            }
        }

        if (enabledRockColliders < 110 || enabledRockColliders > 210)
        {
            throw new InvalidOperationException(
                $"Enabled rock-collider count is outside the performance band: {enabledRockColliders}.");
        }

        Transform[] signatures = generatedRoot.Cast<Transform>()
            .Where(child => child.name.StartsWith(SignaturePrefix, StringComparison.Ordinal))
            .ToArray();
        string expectedSignature = SignaturePrefix + CalculatePlacementHash(generatedRoot, data).ToString("X16");
        if (signatures.Length != 1 || signatures[0].name != expectedSignature)
        {
            throw new InvalidOperationException("Natural dressing semantic signature is missing or stale.");
        }

        if (Mathf.Abs(terrain.treeDistance - 775f) > 0.01f ||
            Mathf.Abs(terrain.detailObjectDistance - 82f) > 0.01f ||
            terrain.treeMaximumFullLODCount != 100)
        {
            throw new InvalidOperationException("Terrain vegetation draw-distance settings drifted.");
        }

        TerrainCollider terrainCollider = terrain.GetComponent<TerrainCollider>();
        SerializedObject serializedTerrainCollider = new SerializedObject(terrainCollider);
        SerializedProperty treeColliders = serializedTerrainCollider.FindProperty("m_EnableTreeColliders");
        if (treeColliders == null || !treeColliders.boolValue)
        {
            throw new InvalidOperationException("Terrain tree colliders are disabled.");
        }

        ValidatePersistentBackupPair();

        if (!allowRepairTemp && AssetOrMetaExists(TerrainRepairTempPath))
        {
            throw new InvalidOperationException("A temporary dressing-repair TerrainData asset remains.");
        }

        EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
        if (buildScenes.Any(item => item.enabled &&
                                   (item.path == SceneBackupPath ||
                                    item.path.IndexOf("DressingRepair", StringComparison.OrdinalIgnoreCase) >= 0)))
        {
            throw new InvalidOperationException("A natural-dressing backup or repair scene is enabled in Build Settings.");
        }
    }

    private static void ValidateProductionNatureAssets()
    {
        HashSet<string> validatedMaterials = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < ProductionTreePrefabPaths.Length; i++)
        {
            string path = ProductionTreePrefabPaths[i];
            ValidateGeneratedAssetOwnership(
                path,
                GeneratedAssetOwner + "|prefab|" + RequireAssetGuid(SourceTreePrefabPaths[i]),
                false);
            ValidateProductionMaterialOwnershipForPrefab(
                SourceTreePrefabPaths[i],
                "Tree",
                validatedMaterials);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            LODGroup group = prefab == null ? null : prefab.GetComponent<LODGroup>();
            if (group == null)
            {
                throw new InvalidOperationException("A production tree prefab lost its root LODGroup: " + path);
            }

            LOD[] lods = group.GetLODs();
            if (lods.Length == 0 || lods[lods.Length - 1].screenRelativeTransitionHeight > 0.002f)
            {
                throw new InvalidOperationException("A production tree has an invalid distance-culling threshold: " + path);
            }

            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    ValidateMatteInstancedMaterial(material, "tree", path);
                }
            }
        }

        for (int i = 0; i < ProductionGrassPrefabPaths.Length; i++)
        {
            string path = ProductionGrassPrefabPaths[i];
            string category = "Grass" + (i + 1);
            ValidateGeneratedAssetOwnership(
                path,
                GeneratedAssetOwner + "|prefab|" + RequireAssetGuid(SourceGrassPrefabPaths[i]),
                false);
            ValidateProductionMaterialOwnershipForPrefab(
                SourceGrassPrefabPaths[i],
                category,
                validatedMaterials);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            MeshRenderer renderer = prefab == null ? null : prefab.GetComponent<MeshRenderer>();
            if (renderer == null || renderer.shadowCastingMode != ShadowCastingMode.Off)
            {
                throw new InvalidOperationException("A production grass prefab is invalid or casts shadows: " + path);
            }

            foreach (Material material in renderer.sharedMaterials)
            {
                ValidateMatteInstancedMaterial(material, "grass", path);
            }
        }
    }

    private static void ValidateProductionMaterialOwnershipForPrefab(
        string sourcePrefabPath,
        string category,
        HashSet<string> validatedMaterials)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePrefabPath);
        if (source == null)
        {
            throw new InvalidOperationException("Missing nature source prefab: " + sourcePrefabPath);
        }

        foreach (Material sourceMaterial in source.GetComponentsInChildren<Renderer>(true)
                     .SelectMany(renderer => renderer.sharedMaterials)
                     .Where(material => material != null))
        {
            string materialPath = ProductionMaterialPath(sourceMaterial, category, out string sourceGuid);
            if (!validatedMaterials.Add(materialPath))
            {
                continue;
            }

            ValidateGeneratedAssetOwnership(
                materialPath,
                GeneratedAssetOwner + "|material|" + category + "|" + sourceGuid,
                false);
        }
    }

    private static void ValidateMatteInstancedMaterial(Material material, string kind, string ownerPath)
    {
        if (material == null || material.shader == null || !material.enableInstancing)
        {
            throw new InvalidOperationException(
                $"A production {kind} material is missing, has no shader, or has instancing disabled: {ownerPath}");
        }

        if (material.HasProperty("_Metallic") && material.GetFloat("_Metallic") > 0.001f)
        {
            throw new InvalidOperationException("A production nature material is metallic: " + material.name);
        }

        float smoothness = material.HasProperty("_Smoothness")
            ? material.GetFloat("_Smoothness")
            : material.HasProperty("_Glossiness")
                ? material.GetFloat("_Glossiness")
                : 0f;
        if (smoothness > 0.10f)
        {
            throw new InvalidOperationException("A production nature material is too glossy: " + material.name);
        }
    }

    private static string BuildValidationSummary(Terrain terrain)
    {
        TerrainData data = terrain.terrainData;
        int grass = 0;
        for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
        {
            int[,] map = data.GetDetailLayer(0, 0, data.detailWidth, data.detailHeight, layer);
            foreach (int count in map)
            {
                grass += count;
            }
        }

        Scene scene = terrain.gameObject.scene;
        Transform root = FindGeneratedRoot(scene);
        int rocks = root == null || root.Find("Rocks") == null
            ? 0
            : root.Find("Rocks").GetComponentsInChildren<Transform>(true)
                .Count(item => item.name.StartsWith("ROCK_", StringComparison.Ordinal));
        return $"{data.treeInstances.Length:N0} terrain trees, {grass:N0} grass instances, and {rocks:N0} grounded rocks passed validation.";
    }

    private static Bounds CalculateRendererBounds(GameObject gameObject)
    {
        Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            throw new InvalidOperationException("Generated nature prefab has no Renderer: " + gameObject.name);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private static Vector3 WorldToNormalized(Terrain terrain, Vector2 point)
    {
        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        return new Vector3(
            Mathf.Clamp01((point.x - origin.x) / size.x),
            0f,
            Mathf.Clamp01((point.y - origin.z) / size.z));
    }

    private static Vector2 NormalizedToWorld(Terrain terrain, Vector3 point)
    {
        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        return new Vector2(origin.x + point.x * size.x, origin.z + point.z * size.z);
    }

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
               !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
               !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }

    private static float DistanceToPolyline(Vector2 point, RouteNode[] route)
    {
        float best = float.PositiveInfinity;
        for (int i = 1; i < route.Length; i++)
        {
            best = Mathf.Min(best, DistanceToSegment(point, route[i - 1].Point, route[i].Point));
        }

        return best;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        float denominator = segment.sqrMagnitude;
        if (denominator < 0.0001f)
        {
            return Vector2.Distance(point, start);
        }

        float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / denominator);
        return Vector2.Distance(point, start + segment * t);
    }

    private static bool IsNearTerrainEdge(Vector2 point, float distance)
    {
        return point.x < -700f + distance || point.x > 700f - distance ||
               point.y < -700f + distance || point.y > 700f - distance;
    }

    private static bool InsideEllipse(Vector2 point, Vector2 center, float radiusX, float radiusZ)
    {
        float x = (point.x - center.x) / radiusX;
        float z = (point.y - center.y) / radiusZ;
        return x * x + z * z <= 1f;
    }

    private static bool InsideEstateViewFan(Vector2 point, float distance, float halfAngle)
    {
        Vector2 origin = new Vector2(-10f, 395f);
        Vector2 delta = point - origin;
        if (delta.magnitude < 18f || delta.magnitude > distance)
        {
            return false;
        }

        return Vector2.Angle(Vector2.down, delta) <= halfAngle;
    }

    private static string BiomeCode(Biome biome)
    {
        switch (biome)
        {
            case Biome.BlackPines: return "BP";
            case Biome.Stillwater: return "SW";
            case Biome.HarrowEstate: return "HE";
            case Biome.BloodrootHollow: return "BH";
            default: return "TR";
        }
    }

    private static string BiomeDisplayName(Biome biome)
    {
        switch (biome)
        {
            case Biome.BlackPines: return "Black Pines";
            case Biome.Stillwater: return "Stillwater Feed Mill";
            case Biome.HarrowEstate: return "Harrow Estate";
            case Biome.BloodrootHollow: return "Bloodroot Hollow";
            default: return "World Transitions";
        }
    }

    private static void RemoveEmptyGeneratedContainers(Transform rocksRoot)
    {
        foreach (Transform biome in rocksRoot.Cast<Transform>().ToArray())
        {
            foreach (Transform chunk in biome.Cast<Transform>().ToArray())
            {
                if (chunk.childCount == 0)
                {
                    UnityEngine.Object.DestroyImmediate(chunk.gameObject);
                }
            }

            if (biome.childCount == 0)
            {
                UnityEngine.Object.DestroyImmediate(biome.gameObject);
            }
        }
    }

    private static void EnsureFolder(string path)
    {
        string[] segments = path.Split('/');
        string current = segments[0];
        for (int i = 1; i < segments.Length; i++)
        {
            string next = current + "/" + segments[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, segments[i]);
            }

            current = next;
        }
    }

    private static string SanitizeFileName(string value)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }

        return value.Replace(' ', '_');
    }

    private static string ProjectAbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string RequireAssetGuid(string assetPath)
    {
        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrEmpty(guid))
        {
            throw new InvalidOperationException("Asset has no persistent GUID: " + assetPath);
        }

        return guid;
    }

    private static void ValidateGeneratedAssetOwnership(
        string assetPath,
        string expectedMarker,
        bool createdByThisCall)
    {
        AssetImporter importer = AssetImporter.GetAtPath(assetPath);
        if (importer == null)
        {
            throw new InvalidOperationException("Generated asset has no importer: " + assetPath);
        }

        if (!createdByThisCall && importer.userData != expectedMarker)
        {
            throw new InvalidOperationException(
                "The deterministic generated path is occupied by an asset not owned by this tool: " + assetPath);
        }

        if (createdByThisCall)
        {
            importer.userData = expectedMarker;
            importer.SaveAndReimport();
        }
    }

    private static bool AssetOrMetaExists(string assetPath)
    {
        string absoluteAssetPath = ProjectAbsolutePath(assetPath);
        string absoluteMetaPath = ProjectAbsolutePath(assetPath + ".meta");
        if (File.Exists(absoluteAssetPath) || File.Exists(absoluteMetaPath))
        {
            return true;
        }

        // DeleteAsset can leave a just-deleted TerrainData path in Unity's GUID cache until
        // the next synchronous refresh. A cached GUID without either physical file is not a
        // recoverable rollback asset and must not block validation or the next safe rebuild.
        if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(assetPath)))
        {
            return false;
        }

        AssetDatabase.ReleaseCachedFileHandles();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        return File.Exists(absoluteAssetPath) || File.Exists(absoluteMetaPath);
    }

    private static uint Hash2D(int x, int z, uint salt)
    {
        uint value = unchecked((uint)x) * 0x8DA6B343u;
        value ^= unchecked((uint)z) * 0xD8163841u;
        value ^= salt;
        value ^= value >> 16;
        value *= 0x7FEB352Du;
        value ^= value >> 15;
        value *= 0x846CA68Bu;
        value ^= value >> 16;
        return value;
    }

    private static float Hash01(uint value)
    {
        value ^= value >> 16;
        value *= 0x7FEB352Du;
        value ^= value >> 15;
        value *= 0x846CA68Bu;
        value ^= value >> 16;
        return (value & 0x00FFFFFFu) / 16777216f;
    }

    private static void HashValue(ref ulong hash, int value)
    {
        unchecked
        {
            hash ^= (uint)value;
            hash *= 1099511628211UL;
        }
    }

    private static void HashString(ref ulong hash, string value)
    {
        foreach (char character in value)
        {
            HashValue(ref hash, character);
        }
    }

    private static void HashVector(ref ulong hash, Vector3 value, float scale)
    {
        HashValue(ref hash, Mathf.RoundToInt(value.x * scale));
        HashValue(ref hash, Mathf.RoundToInt(value.y * scale));
        HashValue(ref hash, Mathf.RoundToInt(value.z * scale));
    }

    private sealed class SpatialPointSet
    {
        private readonly float minimumDistance;
        private readonly float cellSize;
        private readonly Dictionary<Vector2Int, List<Vector2>> cells =
            new Dictionary<Vector2Int, List<Vector2>>();

        public SpatialPointSet(float minimumDistance)
        {
            this.minimumDistance = minimumDistance;
            cellSize = minimumDistance;
        }

        public bool TryAdd(Vector2 point)
        {
            Vector2Int cell = Cell(point);
            for (int z = -1; z <= 1; z++)
            {
                for (int x = -1; x <= 1; x++)
                {
                    Vector2Int neighbor = new Vector2Int(cell.x + x, cell.y + z);
                    if (!cells.TryGetValue(neighbor, out List<Vector2> points))
                    {
                        continue;
                    }

                    if (points.Any(existing => Vector2.Distance(existing, point) < minimumDistance))
                    {
                        return false;
                    }
                }
            }

            if (!cells.TryGetValue(cell, out List<Vector2> bucket))
            {
                bucket = new List<Vector2>();
                cells.Add(cell, bucket);
            }

            bucket.Add(point);
            return true;
        }

        private Vector2Int Cell(Vector2 point)
        {
            return new Vector2Int(
                Mathf.FloorToInt(point.x / cellSize),
                Mathf.FloorToInt(point.y / cellSize));
        }
    }
}
#endif
