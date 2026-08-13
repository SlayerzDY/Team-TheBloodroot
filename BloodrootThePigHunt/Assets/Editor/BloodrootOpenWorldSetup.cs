#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class BloodrootOpenWorldSetup
{
    private const string SourceFarmScene =
        "Assets/Scenes/NewLevel_BaseNoTouch.unity";

    private const string FarmScene =
        "Assets/Scenes/Campaign/Farm_PrologueHub.unity";

    private const string FarmRebuildScene =
        "Assets/Scenes/Campaign/Farm_PrologueHub_Rebuild.unity";

    private const string FarmBackupScene =
        "Assets/Scenes/Campaign/Farm_PrologueHub_Previous.unity";

    private const string OpenWorldScene =
        "Assets/Scenes/OpenWorld/Bloodroot_OpenWorld.unity";

    private const string TerrainDataPath =
        "Assets/Scenes/OpenWorld/Data/Bloodroot_OpenWorld_Terrain.asset";

    private const string LockedMaterialPath =
        "Assets/Materials/OpenWorld/LockedBarrier.mat";

    private const string UnlockedMaterialPath =
        "Assets/Materials/OpenWorld/UnlockedBeacon.mat";

    private const string TravelMaterialPath =
        "Assets/Materials/OpenWorld/TravelMarker.mat";

    private static readonly Vector3 BlackPinesCenter =
        new Vector3(-350f, 0f, -150f);

    [MenuItem(
        "Bloodroot/Open World/Create Farm + Open World Scene Structure",
        false,
        10)]
    public static void CreateSceneStructure()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(FarmScene) != null ||
            AssetDatabase.LoadAssetAtPath<SceneAsset>(OpenWorldScene) != null)
        {
            EditorUtility.DisplayDialog(
                "Open World Setup Already Exists",
                "Farm_PrologueHub or Bloodroot_OpenWorld already exists. " +
                "No scene was overwritten. Use the validation command to inspect the setup.",
                "OK");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceFarmScene) == null)
        {
            EditorUtility.DisplayDialog(
                "Original Farm Scene Not Found",
                $"Expected the original farm at:\n{SourceFarmScene}",
                "OK");
            return;
        }

        Scene originalActiveScene = SceneManager.GetActiveScene();

        try
        {
            EnsureProjectFolders();
            CreateFarmSceneCopy();
            CreateOpenWorldScene();
            ConfigureBuildScenes();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (originalActiveScene.IsValid() && originalActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(originalActiveScene);
            }

            Debug.Log(
                "Bloodroot open-world foundation created successfully. " +
                $"Farm: {FarmScene} | Open world: {OpenWorldScene}");

            EditorUtility.DisplayDialog(
                "Bloodroot Open World Created",
                "Created the Farm prologue/hub scene, the continuous open-world scene, " +
                "starter terrain, region hierarchy, locked entrance placeholders, " +
                "and Build Profile scene order.\n\n" +
                "The scene that was already open was not saved or replaced.",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Open World Setup Failed",
                "Unity could not finish the setup. Check the Console for the exact error. " +
                "Existing scenes were not overwritten.",
                "OK");
        }
    }

    [MenuItem(
        "Bloodroot/Open World/Recreate Farm Hub From NewLevel BaseNoTouch",
        false,
        11)]
    public static void RecreateFarmHubFromNewLevelBaseNoTouch()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceFarmScene) == null)
        {
            EditorUtility.DisplayDialog(
                "NewLevel Base Scene Not Found",
                $"Expected the Farm source scene at:\n{SourceFarmScene}",
                "OK");
            return;
        }

        Scene loadedSourceScene =
            SceneManager.GetSceneByPath(SourceFarmScene);

        if (loadedSourceScene.IsValid() &&
            loadedSourceScene.isLoaded &&
            loadedSourceScene.isDirty)
        {
            EditorUtility.DisplayDialog(
                "Save NewLevel BaseNoTouch First",
                "NewLevel_BaseNoTouch has unsaved changes. Save it before recreating " +
                "Farm_PrologueHub so the copied scene includes those changes.",
                "OK");
            return;
        }

        Scene loadedFarmScene =
            SceneManager.GetSceneByPath(FarmScene);

        if (loadedFarmScene.IsValid() && loadedFarmScene.isLoaded)
        {
            EditorUtility.DisplayDialog(
                "Close Farm Prologue Hub First",
                "Farm_PrologueHub is currently open. Close it before recreating the scene. " +
                "NewLevel_BaseNoTouch can remain open.",
                "OK");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(FarmRebuildScene) != null ||
            AssetDatabase.LoadAssetAtPath<SceneAsset>(FarmBackupScene) != null)
        {
            EditorUtility.DisplayDialog(
                "Farm Rebuild Temporary Scene Exists",
                "A prior Farm rebuild temporary scene still exists. No scene was changed. " +
                "Inspect the Campaign scene folder before retrying.",
                "OK");
            return;
        }

        Scene originalActiveScene = SceneManager.GetActiveScene();
        EditorBuildSettingsScene[] originalBuildScenes =
            EditorBuildSettings.scenes;

        byte[] previousFarmContents = null;
        bool farmContentsReplaced = false;
        bool rebuiltFarmPromoted = false;

        try
        {
            EnsureProjectFolders();
            CreateFarmSceneCopy(FarmRebuildScene);

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(FarmScene) != null)
            {
                string farmFilePath = GetAbsoluteAssetPath(FarmScene);
                string rebuildFilePath = GetAbsoluteAssetPath(FarmRebuildScene);

                previousFarmContents =
                    System.IO.File.ReadAllBytes(farmFilePath);

                farmContentsReplaced = true;
                System.IO.File.Copy(rebuildFilePath, farmFilePath, true);

                if (!AssetDatabase.DeleteAsset(FarmRebuildScene))
                {
                    throw new InvalidOperationException(
                        $"Unity could not remove the temporary scene {FarmRebuildScene}.");
                }

                AssetDatabase.ImportAsset(
                    FarmScene,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);
            }
            else
            {
                MoveSceneAsset(FarmRebuildScene, FarmScene);
                rebuiltFarmPromoted = true;
            }

            ConfigureBuildScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (originalActiveScene.IsValid() && originalActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(originalActiveScene);
            }

            Debug.Log(
                "Farm prologue/hub recreated from NewLevel_BaseNoTouch successfully. " +
                $"Farm: {FarmScene} | Open world unchanged: {OpenWorldScene}");

            EditorUtility.DisplayDialog(
                "Farm Hub Recreated",
                "Recreated Farm_PrologueHub from NewLevel_BaseNoTouch and restored the " +
                "prologue and hub hierarchy. Bloodroot_OpenWorld was not changed.\n\n" +
                "The open NewLevel_BaseNoTouch scene was not saved, closed, or replaced.\n\n" +
                "Re-bake Level NavMesh Surface in Farm_PrologueHub before testing enemy AI.",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            if (farmContentsReplaced && previousFarmContents != null)
            {
                System.IO.File.WriteAllBytes(
                    GetAbsoluteAssetPath(FarmScene),
                    previousFarmContents);

                AssetDatabase.ImportAsset(
                    FarmScene,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);
            }
            else if (rebuiltFarmPromoted)
            {
                AssetDatabase.DeleteAsset(FarmScene);
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(FarmRebuildScene) != null)
            {
                AssetDatabase.DeleteAsset(FarmRebuildScene);
            }

            EditorBuildSettings.scenes = originalBuildScenes;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (originalActiveScene.IsValid() && originalActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(originalActiveScene);
            }

            EditorUtility.DisplayDialog(
                "Farm Hub Recreation Failed",
                "Unity could not recreate Farm_PrologueHub. The previous generated Farm " +
                "scene was restored when available. Check the Console for the exact error.",
                "OK");
        }
    }

    [MenuItem(
        "Bloodroot/Open World/Validate Farm + Open World Scene Structure",
        false,
        12)]
    public static void ValidateSceneStructure()
    {
        List<string> problems = new List<string>();

        ValidateScene(
            FarmScene,
            new[]
            {
                "__CAMPAIGN_STRUCTURE",
                "_CORE",
                "_PROLOGUE_STATE",
                "_HUB_STATE",
                "SOURCE__NewLevel_BaseNoTouch"
            },
            problems);

        ValidateScene(
            OpenWorldScene,
            new[]
            {
                "Bloodroot_OpenWorld",
                "_CORE",
                "_TERRAIN",
                "AREA_00_BLACK_PINES_FOREST",
                "AREA_01_STILLWATER_FEED_MILL",
                "AREA_02_HARROW_ESTATE",
                "AREA_03_BLOODROOT_HOLLOW"
            },
            problems);

        if (AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath) == null)
        {
            problems.Add($"Missing terrain data: {TerrainDataPath}");
        }

        EditorBuildSettingsScene[] buildScenes =
            EditorBuildSettings.scenes;

        if (buildScenes.Length < 2 ||
            buildScenes[0].path != FarmScene ||
            !buildScenes[0].enabled ||
            buildScenes[1].path != OpenWorldScene ||
            !buildScenes[1].enabled)
        {
            problems.Add(
                "Build Profiles must begin with enabled Farm_PrologueHub and Bloodroot_OpenWorld scenes.");
        }

        foreach (EditorBuildSettingsScene buildScene in buildScenes)
        {
            if (buildScene.path == FarmRebuildScene ||
                buildScene.path == FarmBackupScene)
            {
                problems.Add(
                    $"Build Profiles contains a temporary Farm scene entry: {buildScene.path}");
            }

            if (buildScene.enabled &&
                AssetDatabase.LoadAssetAtPath<SceneAsset>(buildScene.path) == null)
            {
                problems.Add(
                    $"Build Profiles contains an enabled missing scene: {buildScene.path}");
            }
        }

        if (problems.Count == 0)
        {
            Debug.Log(
                "Bloodroot open-world scene validation passed: " +
                "both scenes, terrain data, campaign roots, area roots, and Build Profiles are present.");

            EditorUtility.DisplayDialog(
                "Open World Validation Passed",
                "Both scene structures are present and saved. The terrain data, four open-world " +
                "area roots, Farm state roots, and Build Profile order all passed validation.",
                "OK");
            return;
        }

        string report = string.Join("\n", problems.Select(problem => $"- {problem}"));
        Debug.LogError($"Bloodroot open-world scene validation failed:\n{report}");

        EditorUtility.DisplayDialog(
            "Open World Validation Failed",
            report,
            "OK");
    }

    private static void EnsureProjectFolders()
    {
        EnsureFolder("Assets/Scenes/Campaign");
        EnsureFolder("Assets/Scenes/OpenWorld");
        EnsureFolder("Assets/Scenes/OpenWorld/Data");
        EnsureFolder("Assets/Scripts/Features/OpenWorld");
        EnsureFolder("Assets/PreFabs/OpenWorld");
        EnsureFolder("Assets/PreFabs/OpenWorld/Farm");
        EnsureFolder("Assets/PreFabs/OpenWorld/Travel");
        EnsureFolder("Assets/PreFabs/OpenWorld/Gates");
        EnsureFolder("Assets/Materials/OpenWorld");
        EnsureFolder("Assets/Audio/OpenWorld");
        EnsureFolder("Assets/VFX/OpenWorld");
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string[] segments = folderPath.Split('/');
        string currentPath = segments[0];

        for (int index = 1; index < segments.Length; index++)
        {
            string nextPath = $"{currentPath}/{segments[index]}";

            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(currentPath, segments[index]);
            }

            currentPath = nextPath;
        }
    }

    private static void CreateFarmSceneCopy(
        string destinationScene = FarmScene)
    {
        if (!AssetDatabase.CopyAsset(SourceFarmScene, destinationScene))
        {
            throw new InvalidOperationException(
                $"Unity could not copy {SourceFarmScene} to {destinationScene}.");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Scene farmScene =
            EditorSceneManager.OpenScene(destinationScene, OpenSceneMode.Additive);

        try
        {
            SceneManager.SetActiveScene(farmScene);

            GameObject campaignRoot =
                CreateSceneRoot("__CAMPAIGN_STRUCTURE", farmScene);

            GameObject core =
                CreateEmpty("_CORE", campaignRoot.transform);

            CreateEmpty(
                "FarmHubStateController (Component Pending)",
                core.transform);

            CreateEmpty(
                "SETUP_NOTES__PLACE_MARKERS_BEFORE_PLAY",
                core.transform);

            CreateEmpty(
                "SOURCE__NewLevel_BaseNoTouch",
                core.transform);

            GameObject prologueState =
                CreateEmpty("_PROLOGUE_STATE", campaignRoot.transform);

            CreateEmpty("Prologue Objectives", prologueState.transform);
            CreateEmpty("Prologue Enemies", prologueState.transform);
            CreateEmpty("Prologue Dialogue", prologueState.transform);
            CreateEmpty("Prologue Spawn", prologueState.transform);

            GameObject completionTrigger =
                CreateEmpty(
                    "Complete Prologue Trigger",
                    prologueState.transform);

            BoxCollider completionCollider =
                completionTrigger.AddComponent<BoxCollider>();

            completionCollider.isTrigger = true;
            completionCollider.size = new Vector3(6f, 3f, 6f);

            GameObject hubState =
                CreateEmpty("_HUB_STATE", campaignRoot.transform);

            CreateEmpty("Hub Spawn", hubState.transform);
            CreateEmpty("Mission Board", hubState.transform);
            CreateEmpty("Upgrade Area", hubState.transform);
            CreateEmpty("Storage Area", hubState.transform);
            CreateEmpty("Hub Decorations", hubState.transform);

            GameObject truckTravelPoint =
                CreateEmpty("Truck Travel Point", hubState.transform);

            BoxCollider truckCollider =
                truckTravelPoint.AddComponent<BoxCollider>();

            truckCollider.size = new Vector3(4f, 3f, 7f);

            hubState.SetActive(false);

            EditorSceneManager.MarkSceneDirty(farmScene);

            if (!EditorSceneManager.SaveScene(farmScene, destinationScene))
            {
                throw new InvalidOperationException(
                    $"Unity could not save the Farm scene at {destinationScene}.");
            }
        }
        finally
        {
            if (farmScene.IsValid() && farmScene.isLoaded)
            {
                EditorSceneManager.CloseScene(farmScene, true);
            }
        }
    }

    private static void CreateOpenWorldScene()
    {
        Scene openWorldScene =
            EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);

        try
        {
            SceneManager.SetActiveScene(openWorldScene);

            Material lockedMaterial =
                GetOrCreateMaterial(
                    LockedMaterialPath,
                    new Color(0.42f, 0.025f, 0.02f),
                    new Color(1.25f, 0.04f, 0.02f));

            Material unlockedMaterial =
                GetOrCreateMaterial(
                    UnlockedMaterialPath,
                    new Color(0.03f, 0.35f, 0.12f),
                    new Color(0.05f, 1.2f, 0.32f));

            Material travelMaterial =
                GetOrCreateMaterial(
                    TravelMaterialPath,
                    new Color(0.28f, 0.13f, 0.025f),
                    new Color(0.8f, 0.3f, 0.04f));

            GameObject worldRoot =
                CreateSceneRoot("Bloodroot_OpenWorld", openWorldScene);

            GameObject core =
                CreateEmpty("_CORE", worldRoot.transform);

            CreateEmpty(
                "OpenWorldProgressionManager (Component Pending)",
                core.transform);

            CreateEmpty(
                "Open World NavMesh Surface (Bake Later)",
                core.transform);

            Vector3 arrivalPosition =
                BlackPinesCenter + new Vector3(0f, 8f, 0f);

            InstantiatePrefabOrPlaceholder(
                "Assets/PreFabs/Player/Player ForLevel.prefab",
                "Player",
                core.transform,
                openWorldScene,
                arrivalPosition);

            InstantiatePrefabOrPlaceholder(
                "Assets/PreFabs/PlayerSpawnPos.prefab",
                "PlayerSpawnPos",
                core.transform,
                openWorldScene,
                arrivalPosition);

            InstantiatePrefabOrPlaceholder(
                "Assets/PreFabs/UI/UI.prefab",
                "UI",
                core.transform,
                openWorldScene,
                Vector3.zero);

            InstantiatePrefabOrPlaceholder(
                "Assets/MiniMap/MiniMapCam.prefab",
                "MiniMapCam",
                core.transform,
                openWorldScene,
                arrivalPosition + Vector3.up * 25f);

            CreateTravelTruckPlaceholder(
                core.transform,
                arrivalPosition + new Vector3(-12f, -5f, 8f),
                travelMaterial);

            GameObject terrainRoot =
                CreateEmpty("_TERRAIN", worldRoot.transform);

            CreateTerrain(openWorldScene, terrainRoot.transform);
            CreateEmpty("Roads", terrainRoot.transform);
            CreateEmpty("Rivers", terrainRoot.transform);
            CreateEmpty("World Boundaries", terrainRoot.transform);

            CreateLighting(openWorldScene, worldRoot.transform);

            CreateWorldArea(
                worldRoot.transform,
                "AREA_00_BLACK_PINES_FOREST",
                BlackPinesCenter,
                "Black Pines Mission Systems",
                true,
                null,
                Vector3.zero,
                string.Empty,
                lockedMaterial,
                unlockedMaterial);

            CreateWorldArea(
                worldRoot.transform,
                "AREA_01_STILLWATER_FEED_MILL",
                new Vector3(320f, 0f, -40f),
                "Stillwater Mission Systems",
                false,
                "Locked Entrance",
                new Vector3(235f, 0f, -40f),
                "REQUIRES_PROGRESS_1__CLEAR_BLACK_PINES_FOREST",
                lockedMaterial,
                unlockedMaterial);

            CreateWorldArea(
                worldRoot.transform,
                "AREA_02_HARROW_ESTATE",
                new Vector3(40f, 0f, 360f),
                "Harrow Estate Mission Systems",
                false,
                "Locked Entrance",
                new Vector3(40f, 0f, 260f),
                "REQUIRES_PROGRESS_2__CLEAR_STILLWATER_FEED_MILL",
                lockedMaterial,
                unlockedMaterial);

            CreateWorldArea(
                worldRoot.transform,
                "AREA_03_BLOODROOT_HOLLOW",
                new Vector3(50f, 0f, 650f),
                "Bloodroot Hollow Boss Systems",
                false,
                "Locked Entrance",
                new Vector3(50f, 0f, 540f),
                "REQUIRES_PROGRESS_3__CLEAR_HARROW_ESTATE",
                lockedMaterial,
                unlockedMaterial);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0025f;
            RenderSettings.fogColor = new Color(0.095f, 0.12f, 0.13f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.24f, 0.29f, 0.31f);
            RenderSettings.ambientEquatorColor = new Color(0.12f, 0.14f, 0.13f);
            RenderSettings.ambientGroundColor = new Color(0.045f, 0.05f, 0.04f);

            EditorSceneManager.MarkSceneDirty(openWorldScene);

            if (!EditorSceneManager.SaveScene(openWorldScene, OpenWorldScene))
            {
                throw new InvalidOperationException(
                    $"Unity could not save the open-world scene at {OpenWorldScene}.");
            }
        }
        finally
        {
            if (openWorldScene.IsValid() && openWorldScene.isLoaded)
            {
                EditorSceneManager.CloseScene(openWorldScene, true);
            }
        }
    }

    private static void CreateTerrain(Scene scene, Transform parent)
    {
        TerrainData terrainData =
            AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);

        if (terrainData == null)
        {
            terrainData = new TerrainData
            {
                name = "Bloodroot_OpenWorld_Terrain",
                heightmapResolution = 513,
                size = new Vector3(1400f, 120f, 1400f)
            };

            float[,] heights =
                new float[terrainData.heightmapResolution, terrainData.heightmapResolution];

            for (int z = 0; z < terrainData.heightmapResolution; z++)
            {
                for (int x = 0; x < terrainData.heightmapResolution; x++)
                {
                    float broadNoise =
                        Mathf.PerlinNoise(x * 0.0125f + 17f, z * 0.0125f + 31f);

                    float detailNoise =
                        Mathf.PerlinNoise(x * 0.034f + 71f, z * 0.034f + 11f);

                    heights[z, x] =
                        0.012f + broadNoise * 0.016f + detailNoise * 0.004f;
                }
            }

            terrainData.SetHeights(0, 0, heights);

            TerrainLayer existingGroundLayer =
                AssetDatabase.LoadAssetAtPath<TerrainLayer>(
                    "Assets/Materials/Features/NewGround/TerrainGround.terrainlayer");

            if (existingGroundLayer != null)
            {
                terrainData.terrainLayers = new[] { existingGroundLayer };
            }

            AssetDatabase.CreateAsset(terrainData, TerrainDataPath);
        }

        GameObject terrainObject =
            Terrain.CreateTerrainGameObject(terrainData);

        terrainObject.name = "Open World Terrain";
        terrainObject.transform.position = new Vector3(-700f, 0f, -700f);
        SceneManager.MoveGameObjectToScene(terrainObject, scene);
        terrainObject.transform.SetParent(parent, true);

        Terrain terrain = terrainObject.GetComponent<Terrain>();

        if (terrain != null)
        {
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 8f;
            terrain.basemapDistance = 1200f;
        }
    }

    private static void CreateLighting(Scene scene, Transform worldRoot)
    {
        GameObject lightingRoot =
            CreateEmpty("_LIGHTING", worldRoot);

        GameObject lightObject = new GameObject("Directional Light");
        SceneManager.MoveGameObjectToScene(lightObject, scene);
        lightObject.transform.SetParent(lightingRoot.transform, true);
        lightObject.transform.rotation = Quaternion.Euler(52f, -28f, 0f);

        Light directionalLight = lightObject.AddComponent<Light>();
        directionalLight.type = LightType.Directional;
        directionalLight.intensity = 1.1f;
        directionalLight.color = new Color(0.78f, 0.84f, 0.88f);
        directionalLight.shadows = LightShadows.Soft;

        CreateEmpty("Global Volume (Configure Later)", lightingRoot.transform);
        CreateEmpty("Audio Ambience (Configure Later)", lightingRoot.transform);
    }

    private static void CreateWorldArea(
        Transform worldRoot,
        string areaName,
        Vector3 areaCenter,
        string systemsName,
        bool systemsActive,
        string gateName,
        Vector3 gatePosition,
        string requirementName,
        Material lockedMaterial,
        Material unlockedMaterial)
    {
        GameObject areaRoot = CreateEmpty(areaName, worldRoot);
        areaRoot.transform.position = areaCenter;

        CreateEmpty("Environment", areaRoot.transform);

        GameObject systems = CreateEmpty(systemsName, areaRoot.transform);
        systems.SetActive(systemsActive);

        GameObject areaSpawn =
            CreateEmpty(
                areaName.Contains("BLACK_PINES")
                    ? "World Arrival Spawn"
                    : areaName.Contains("BLOODROOT_HOLLOW")
                        ? "Boss Arena Spawn"
                        : "Area Spawn",
                areaRoot.transform);

        areaSpawn.transform.localPosition = new Vector3(0f, 5f, 0f);

        CreateEmpty("Area Audio", areaRoot.transform);
        CreateEmpty("Area Landmarks", areaRoot.transform);

        if (string.IsNullOrEmpty(gateName))
        {
            CreateEmpty("Exit Road", areaRoot.transform);
            return;
        }

        CreateGatePlaceholder(
            areaRoot.transform,
            gateName,
            gatePosition,
            requirementName,
            lockedMaterial,
            unlockedMaterial);
    }

    private static void CreateGatePlaceholder(
        Transform areaRoot,
        string gateName,
        Vector3 worldPosition,
        string requirementName,
        Material lockedMaterial,
        Material unlockedMaterial)
    {
        GameObject gateRoot = CreateEmpty(gateName, areaRoot);
        gateRoot.transform.position = worldPosition;

        GameObject barrier =
            GameObject.CreatePrimitive(PrimitiveType.Cube);

        barrier.name = "Barrier";
        barrier.transform.SetParent(gateRoot.transform, false);
        barrier.transform.localPosition = new Vector3(0f, 3f, 0f);
        barrier.transform.localScale = new Vector3(18f, 6f, 2f);

        Renderer barrierRenderer = barrier.GetComponent<Renderer>();

        if (barrierRenderer != null)
        {
            barrierRenderer.sharedMaterial = lockedMaterial;
        }

        GameObject feedbackTrigger =
            CreateEmpty("Locked Feedback Trigger", gateRoot.transform);

        feedbackTrigger.transform.localPosition = new Vector3(0f, 3f, -4f);

        BoxCollider feedbackCollider =
            feedbackTrigger.AddComponent<BoxCollider>();

        feedbackCollider.isTrigger = true;
        feedbackCollider.size = new Vector3(22f, 6f, 5f);

        GameObject beacon =
            GameObject.CreatePrimitive(PrimitiveType.Cylinder);

        beacon.name = "Status Beacon";
        beacon.transform.SetParent(gateRoot.transform, false);
        beacon.transform.localPosition = new Vector3(8f, 3f, 0f);
        beacon.transform.localScale = new Vector3(1.2f, 3f, 1.2f);

        Renderer beaconRenderer = beacon.GetComponent<Renderer>();

        if (beaconRenderer != null)
        {
            beaconRenderer.sharedMaterial = unlockedMaterial;
        }

        GameObject sign =
            CreateEmpty("World Space Sign (Add TextMeshPro)", gateRoot.transform);

        sign.transform.localPosition = new Vector3(0f, 7.5f, 0f);

        CreateEmpty(requirementName, gateRoot.transform);
    }

    private static void CreateTravelTruckPlaceholder(
        Transform parent,
        Vector3 worldPosition,
        Material material)
    {
        GameObject truckRoot =
            CreateEmpty("Return Truck (Travel Wiring Pending)", parent);

        truckRoot.transform.position = worldPosition;

        GameObject truckBody =
            GameObject.CreatePrimitive(PrimitiveType.Cube);

        truckBody.name = "Truck Placeholder";
        truckBody.transform.SetParent(truckRoot.transform, false);
        truckBody.transform.localPosition = new Vector3(0f, 1.4f, 0f);
        truckBody.transform.localScale = new Vector3(3f, 2.4f, 6f);

        Renderer renderer = truckBody.GetComponent<Renderer>();

        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
    }

    private static GameObject InstantiatePrefabOrPlaceholder(
        string prefabPath,
        string objectName,
        Transform parent,
        Scene scene,
        Vector3 worldPosition)
    {
        GameObject prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        GameObject instance;

        if (prefab != null)
        {
            instance =
                PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        }
        else
        {
            instance = new GameObject($"{objectName} (Prefab Missing)");
            SceneManager.MoveGameObjectToScene(instance, scene);
        }

        if (instance == null)
        {
            throw new InvalidOperationException(
                $"Unity could not instantiate {prefabPath}.");
        }

        instance.name = objectName;
        instance.transform.SetParent(parent, true);
        instance.transform.position = worldPosition;
        return instance;
    }

    private static Material GetOrCreateMaterial(
        string path,
        Color baseColor,
        Color emissionColor)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material != null)
        {
            return material;
        }

        Shader shader =
            Shader.Find("Universal Render Pipeline/Lit") ??
            Shader.Find("Standard");

        if (shader == null)
        {
            throw new InvalidOperationException(
                "Unity could not find a compatible material shader.");
        }

        material = new Material(shader)
        {
            name = System.IO.Path.GetFileNameWithoutExtension(path),
            color = baseColor
        };

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", baseColor);
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", emissionColor);
            material.EnableKeyword("_EMISSION");
        }

        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static GameObject CreateSceneRoot(string name, Scene scene)
    {
        GameObject root = new GameObject(name);
        SceneManager.MoveGameObjectToScene(root, scene);
        return root;
    }

    private static GameObject CreateEmpty(string name, Transform parent)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child;
    }

    private static void ConfigureBuildScenes()
    {
        List<EditorBuildSettingsScene> remainingScenes =
            EditorBuildSettings.scenes
                .Where(scene =>
                    scene.path != FarmScene &&
                    scene.path != OpenWorldScene &&
                    scene.path != FarmRebuildScene &&
                    scene.path != FarmBackupScene)
                .ToList();

        List<EditorBuildSettingsScene> buildScenes =
            new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(FarmScene, true),
                new EditorBuildSettingsScene(OpenWorldScene, true)
            };

        buildScenes.AddRange(remainingScenes);
        EditorBuildSettings.scenes = buildScenes.ToArray();
    }

    private static void MoveSceneAsset(
        string sourcePath,
        string destinationPath)
    {
        string error = AssetDatabase.MoveAsset(sourcePath, destinationPath);

        if (!string.IsNullOrEmpty(error))
        {
            throw new InvalidOperationException(
                $"Unity could not move {sourcePath} to {destinationPath}: {error}");
        }
    }

    private static string GetAbsoluteAssetPath(string assetPath)
    {
        string projectRoot =
            System.IO.Directory.GetParent(Application.dataPath).FullName;

        return System.IO.Path.Combine(
            projectRoot,
            assetPath.Replace('/', System.IO.Path.DirectorySeparatorChar));
    }

    private static void ValidateScene(
        string scenePath,
        IEnumerable<string> expectedNames,
        ICollection<string> problems)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
        {
            problems.Add($"Missing scene: {scenePath}");
            return;
        }

        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool openedForValidation = !scene.IsValid() || !scene.isLoaded;

        if (openedForValidation)
        {
            scene =
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        }

        try
        {
            HashSet<string> hierarchyNames = new HashSet<string>();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                CollectHierarchyNames(root.transform, hierarchyNames);
            }

            foreach (string expectedName in expectedNames)
            {
                if (!hierarchyNames.Contains(expectedName))
                {
                    problems.Add(
                        $"{scenePath} is missing hierarchy object '{expectedName}'.");
                }
            }
        }
        finally
        {
            if (openedForValidation && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static void CollectHierarchyNames(
        Transform current,
        ISet<string> names)
    {
        names.Add(current.name);

        foreach (Transform child in current)
        {
            CollectHierarchyNames(child, names);
        }
    }
}
#endif
