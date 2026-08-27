#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Bloodroot.Campaign;
using Bloodroot.Features.FarmPrologue;
using Bloodroot.Features.WorldMissions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Additive authoring pass for the player-facing campaign compass and its one
/// active world waypoint. It updates only its owned hierarchy, the eight Farm
/// objective strings, and the additive compass subtree in Safety's UI prefab.
/// </summary>
public static class BloodrootCampaignGuidanceSetup
{
    private const string FarmScenePath =
        "Assets/Scenes/Campaign/Farm_PrologueHub.unity";
    private const string OpenWorldScenePath =
        "Assets/Scenes/OpenWorld/Bloodroot_OpenWorld.unity";
    private const string UiPrefabPath = "Assets/PreFabs/UI/UI.prefab";
    private const string GuidanceRootName = "__BR_OBJECTIVE_GUIDANCE_V1";
    private const string AnchorRootName = "Waypoint Anchors";
    private const string CompassRootName = "Campaign Compass";
    private const string DiamondMeshPath =
        "Assets/Features/CampaignGuidance/ObjectiveDiamond.asset";
    private const string DiamondMaterialPath =
        "Assets/Features/CampaignGuidance/ObjectiveDiamond_Glow.mat";
    private const string TmpFontGuid =
        "8f586378b4e144a9851e7b34d9b748ee";
    private static readonly Vector2 CompassPanelSize =
        new(456f, 122f);
    private static readonly Vector2 CompassDialBackgroundSize =
        new(82f, 82f);
    private static readonly Vector2 CompassDialSize =
        new(74f, 74f);
    private static readonly Vector2 CompassObjectiveDiamondSize =
        new(6f, 6f);
    private const float CompassObjectiveIndicatorRadius = 38f;
    private static readonly Vector3 ObjectiveDiamondWorldScale =
        new(0.54f, 0.76f, 0.54f);
    private const float ObjectiveDiamondHoverAmplitude = 0.28f;
    private const float ObjectiveDiamondMaximumDistanceScale = 4.5f;
    private const float FirstWaveShakeAmplitude = 0.025f;
    private const float FirstWaveShakeFrequency = 10f;
    private const float FirstWaveShakeRampInSeconds = 0.35f;

    private readonly struct FarmChoreSpec
    {
        public FarmChoreSpec(
            string objectName,
            string choreId,
            string displayName,
            string expectedDirection,
            string instruction)
        {
            ObjectName = objectName;
            ChoreId = choreId;
            DisplayName = displayName;
            ExpectedDirection = expectedDirection;
            Instruction = instruction;
        }

        public string ObjectName { get; }
        public string ChoreId { get; }
        public string DisplayName { get; }
        public string ExpectedDirection { get; }
        public string Instruction { get; }
    }

    private static readonly FarmChoreSpec[] FarmChores =
    {
        new(
            "STEP_01_Collect_Feed_Scoop",
            "feed_collect_scoop",
            "Feed scoop",
            "N",
            "Feed the pigs (1/3): Go north (N) to the feed bin beside the pig yard. Stand under the glowing diamond, face the bin, and press E to take the feed scoop."),
        new(
            "STEP_02_Fill_South_Trough",
            "feed_fill_south_trough",
            "First feed trough",
            "SE",
            "Feed the pigs (2/3): From the feed bin, go southeast (SE) to the first feed trough. Stand beside it under the glowing diamond and press E to fill it."),
        new(
            "STEP_03_Fill_North_Trough",
            "feed_fill_north_trough",
            "Second feed trough",
            "E",
            "Feed the pigs (3/3): Continue east (E) to the second feed trough. Stand beside it under the glowing diamond and press E to fill it."),
        new(
            "STEP_04_Clear_East_Stall",
            "muck_clear_east_stall",
            "East pig stall",
            "N",
            "Muck the stalls (1/3): From the second trough, go north (N) to the eastern pig stall. Enter the stall, approach the manure under the glowing diamond, and press E to clear it."),
        new(
            "STEP_05_Clear_West_Stall",
            "muck_clear_west_stall",
            "West pig stall",
            "W",
            "Muck the stalls (2/3): Go west (W) across the barn to the western pig stall. Enter the stall, approach the manure under the glowing diamond, and press E to clear it."),
        new(
            "STEP_06_Dump_Muck_Wheelbarrow",
            "muck_dump_waste",
            "Muck heap wheelbarrow",
            "SE",
            "Muck the stalls (3/3): Go southeast (SE) to the loaded wheelbarrow beside the muck heap. Stand under the glowing diamond and press E to dump it."),
        new(
            "STEP_07_Prime_Livestock_Pump",
            "water_prime_pump",
            "Livestock pump",
            "NW",
            "Check the water (1/2): Go northwest (NW) to the livestock pump. Stand beside the handle under the glowing diamond and press E to prime it."),
        new(
            "STEP_08_Open_Trough_Valve",
            "water_open_trough_valve",
            "Trough valve",
            "SE",
            "Check the water (2/2): Go southeast (SE) to the trough valve. Stand beside the valve under the glowing diamond and press E to open it and refill the trough.")
    };

    [MenuItem("Tools/Bloodroot/Campaign/Apply Compass and Objective Guidance")]
    public static void ApplyFromMenu()
    {
        ApplyBatch();
        EditorUtility.DisplayDialog(
            "Bloodroot Campaign Guidance",
            "The compass, chore guidance, and Open World waypoints are authored and validated.",
            "OK");
    }

    public static void ApplyBatch()
    {
        if (Application.isPlaying || EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            throw new InvalidOperationException(
                "Campaign guidance authoring requires an idle Unity Editor in Edit Mode.");
        }

        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
        bool restoreSetup = !Application.isBatchMode &&
                            originalSetup != null &&
                            originalSetup.Length > 0;
        try
        {
            Mesh currentMesh = AssetDatabase.LoadAssetAtPath<Mesh>(
                DiamondMeshPath);
            Material currentMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                DiamondMaterialPath);
            if (currentMesh != null && currentMaterial != null &&
                TryValidateCurrentProject(currentMesh, currentMaterial))
            {
                Console.WriteLine(
                    "BLOODROOT_CAMPAIGN_GUIDANCE: PASS no_changes=1 compass=1 farmTargets=8 openWorldTargets=5");
                return;
            }

            EnsureAssetFolder("Assets/Features/CampaignGuidance");
            Mesh diamondMesh = EnsureDiamondMesh();
            Material diamondMaterial = EnsureDiamondMaterial();
            ApplyCompassPrefab();
            ApplyFarmScene(diamondMesh, diamondMaterial);
            ApplyOpenWorldScene(diamondMesh, diamondMaterial);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ValidateCompassPrefab();
            Console.WriteLine(
                "BLOODROOT_CAMPAIGN_GUIDANCE: PASS compass=1 farmTargets=8 openWorldTargets=5");
        }
        finally
        {
            if (restoreSetup)
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
        }
    }

    private static bool TryValidateCurrentProject(
        Mesh expectedMesh,
        Material expectedMaterial)
    {
        try
        {
            ValidateCompassPrefab();

            Scene farm = EditorSceneManager.OpenScene(
                FarmScenePath,
                OpenSceneMode.Single);
            FarmPrologueDirector director =
                RequireSingleComponent<FarmPrologueDirector>(farm);
            var chores = new FarmChoreInteractable[FarmChores.Length];
            for (int index = 0; index < FarmChores.Length; index++)
            {
                FarmChoreSpec spec = FarmChores[index];
                FarmChoreInteractable chore =
                    RequireNamedComponent<FarmChoreInteractable>(
                        farm,
                        spec.ObjectName);
                if (!string.Equals(chore.ChoreId, spec.ChoreId,
                        StringComparison.Ordinal) ||
                    !string.Equals(chore.ObjectiveText, spec.Instruction,
                        StringComparison.Ordinal))
                {
                    return false;
                }

                chores[index] = chore;
            }

            ValidateFarmDirections(director, chores);
            CampaignObjectiveGuidance farmGuidance =
                RequireSingleComponent<CampaignObjectiveGuidance>(farm);
            if (!farmGuidance.IsFarmGuidance)
                return false;
            ValidateSceneGuidance(farm, farmGuidance, FarmChores.Length);
            ValidateMarkerAssets(farm, expectedMesh, expectedMaterial);
            ValidateFarmRumble(farm);

            Scene openWorld = EditorSceneManager.OpenScene(
                OpenWorldScenePath,
                OpenSceneMode.Single);
            CampaignObjectiveGuidance openWorldGuidance =
                RequireSingleComponent<CampaignObjectiveGuidance>(openWorld);
            if (!openWorldGuidance.IsOpenWorldGuidance)
                return false;
            ValidateSceneGuidance(openWorld, openWorldGuidance, 5);
            ValidateMarkerAssets(openWorld, expectedMesh, expectedMaterial);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void ApplyFarmScene(Mesh mesh, Material material)
    {
        Scene scene = EditorSceneManager.OpenScene(
            FarmScenePath,
            OpenSceneMode.Single);
        FarmPrologueDirector director =
            RequireSingleComponent<FarmPrologueDirector>(scene);
        FarmRumblePresenter rumblePresenter =
            RequireSingleComponent<FarmRumblePresenter>(scene);
        if (director.PlayerSpawnFallback == null)
        {
            throw new InvalidOperationException(
                "Farm guidance requires the authored player spawn fallback.");
        }

        rumblePresenter.ConfigureShake(
            FirstWaveShakeAmplitude,
            FirstWaveShakeFrequency,
            FirstWaveShakeRampInSeconds);
        EditorUtility.SetDirty(rumblePresenter);

        GameObject root = RebuildOwnedSceneRoot(scene);
        Transform anchors = CreateChild(root.transform, AnchorRootName).transform;
        var targets = new FarmChoreGuidanceTarget[FarmChores.Length];
        var chores = new FarmChoreInteractable[FarmChores.Length];

        for (int index = 0; index < FarmChores.Length; index++)
        {
            FarmChoreSpec spec = FarmChores[index];
            FarmChoreInteractable chore =
                RequireNamedComponent<FarmChoreInteractable>(
                    scene,
                    spec.ObjectName);
            if (!string.Equals(chore.ChoreId, spec.ChoreId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Farm chore '{spec.ObjectName}' has unexpected ID " +
                    $"'{chore.ChoreId}'.");
            }

            chore.Configure(
                director,
                spec.ChoreId,
                spec.Instruction,
                chore.RequiredInteractions);
            EditorUtility.SetDirty(chore);
            chores[index] = chore;

            Vector3 markerPosition = CalculateMarkerPosition(
                chore.gameObject,
                chore.GetComponent<Collider>(),
                2.7f);
            Transform anchor = CreateAnchor(
                anchors,
                $"Farm {index + 1:00} - {spec.DisplayName}",
                markerPosition);
            var target = new FarmChoreGuidanceTarget();
            target.Configure(chore, anchor, spec.DisplayName);
            targets[index] = target;
        }

        ValidateFarmDirections(director, chores);

        CampaignObjectiveGuidance guidance =
            root.AddComponent<CampaignObjectiveGuidance>();
        guidance.ConfigureFarm(director, targets);
        EditorUtility.SetDirty(guidance);
        BuildWorldMarker(root.transform, guidance, mesh, material);
        ValidateSceneGuidance(scene, guidance, FarmChores.Length);
        SaveScene(scene, FarmScenePath);
    }

    private static void ApplyOpenWorldScene(Mesh mesh, Material material)
    {
        Scene scene = EditorSceneManager.OpenScene(
            OpenWorldScenePath,
            OpenSceneMode.Single);
        CampaignStateService state =
            RequireSingleComponent<CampaignStateService>(scene);
        HollowWitchSpawner hollowSpawner =
            RequireSingleComponent<HollowWitchSpawner>(scene);
        CampaignThornVeilGate thornVeil =
            RequireSingleComponent<CampaignThornVeilGate>(scene);

        CampaignProgressionTower[] towers =
            FindComponents<CampaignProgressionTower>(scene)
                .OrderBy(tower => (int)tower.Area)
                .ToArray();
        if (towers.Length != 4 ||
            towers.Select(tower => tower.Area).Distinct().Count() != 4)
        {
            throw new InvalidOperationException(
                "Open World guidance requires exactly one progression tower for each campaign area.");
        }

        GameObject root = RebuildOwnedSceneRoot(scene);
        Transform anchors = CreateChild(root.transform, AnchorRootName).transform;
        var targets = new ProgressionTowerGuidanceTarget[towers.Length];
        for (int index = 0; index < towers.Length; index++)
        {
            CampaignProgressionTower tower = towers[index];
            if (tower.Objective == null)
            {
                throw new InvalidOperationException(
                    $"The {tower.Area} progression tower has no objective.");
            }

            Transform beacon = RequireUniqueDescendant(
                tower.transform,
                "BEACON_Top_Point_Light");
            string displayName = GetTowerDisplayName(tower.Area);
            Transform anchor = CreateAnchor(
                anchors,
                $"Tower {index + 1:00} - {displayName}",
                beacon.position + Vector3.up * 3.2f);
            var target = new ProgressionTowerGuidanceTarget();
            target.Configure(tower, anchor, displayName);
            targets[index] = target;
        }

        Vector3 veilMarkerPosition = CalculateMarkerPosition(
            thornVeil.BlockedRoot != null
                ? thornVeil.BlockedRoot
                : thornVeil.gameObject,
            thornVeil.GetComponentInChildren<Collider>(true),
            3.4f);
        Transform veilAnchor = CreateAnchor(
            anchors,
            "Thorn Veil",
            veilMarkerPosition);

        CampaignObjectiveGuidance guidance =
            root.AddComponent<CampaignObjectiveGuidance>();
        guidance.ConfigureOpenWorld(
            state,
            targets,
            hollowSpawner,
            veilAnchor,
            "Bloodroot Hollow thorn veil");
        EditorUtility.SetDirty(guidance);
        BuildWorldMarker(root.transform, guidance, mesh, material);
        ValidateSceneGuidance(scene, guidance, 5);
        SaveScene(scene, OpenWorldScenePath);
    }

    private static void ApplyCompassPrefab()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(UiPrefabPath);
        try
        {
            Canvas[] canvases = prefabRoot.GetComponentsInChildren<Canvas>(true);
            if (canvases.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Safety UI requires exactly one Canvas; found {canvases.Length}.");
            }

            Canvas canvas = canvases[0];
            Transform[] previousRoots = canvas.GetComponentsInChildren<Transform>(true)
                .Where(candidate => candidate != canvas.transform &&
                    string.Equals(candidate.name, CompassRootName,
                        StringComparison.Ordinal))
                .ToArray();
            foreach (Transform previousRoot in previousRoots)
            {
                if (previousRoot != null)
                    UnityEngine.Object.DestroyImmediate(previousRoot.gameObject);
            }

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                AssetDatabase.GUIDToAssetPath(TmpFontGuid));
            if (font == null)
            {
                throw new InvalidOperationException(
                    "The Safety UI's LiberationSans TMP font is missing.");
            }

            GameObject compassRoot = CreateUiImage(
                canvas.transform,
                CompassRootName,
                new Color(0.015f, 0.025f, 0.035f, 0.9f));
            RectTransform rootRect = compassRoot.GetComponent<RectTransform>();
            SetRect(
                rootRect,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -16f),
                CompassPanelSize);
            Outline outline = compassRoot.AddComponent<Outline>();
            outline.effectColor = new Color(0.1f, 0.95f, 0.85f, 0.42f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;

            TMP_Text heading = CreateUiText(
                compassRoot.transform,
                "Current Heading",
                "N  000\u00b0",
                font,
                19f,
                Color.white,
                TextAlignmentOptions.Center);
            SetRect(
                heading.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(68f, -8f),
                new Vector2(124f, 24f));

            TMP_Text trueNorth = CreateUiText(
                compassRoot.transform,
                "True North Label",
                "TRUE NORTH",
                font,
                10f,
                new Color(1f, 0.55f, 0.32f, 1f),
                TextAlignmentOptions.Center);
            SetRect(
                trueNorth.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(68f, -30f),
                new Vector2(124f, 16f));

            GameObject dialBackground = CreateUiImage(
                compassRoot.transform,
                "Compass Dial Background",
                new Color(0.07f, 0.1f, 0.12f, 0.95f));
            SetRect(
                dialBackground.GetComponent<RectTransform>(),
                new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(68f, -16f),
                CompassDialBackgroundSize);

            GameObject dialObject = CreateUiTransform(
                dialBackground.transform,
                "Compass Dial");
            RectTransform dial = dialObject.GetComponent<RectTransform>();
            SetRect(
                dial,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                CompassDialSize);

            CreateDialLine(dial, "North South Axis", new Vector2(2f, 54f));
            CreateDialLine(dial, "East West Axis", new Vector2(54f, 2f));

            RectTransform north = CreateCardinal(
                dial,
                "North",
                "N",
                new Vector2(0f, 28f),
                new Color(1f, 0.42f, 0.28f, 1f),
                font);
            RectTransform east = CreateCardinal(
                dial,
                "East",
                "E",
                new Vector2(28f, 0f),
                Color.white,
                font);
            RectTransform south = CreateCardinal(
                dial,
                "South",
                "S",
                new Vector2(0f, -28f),
                Color.white,
                font);
            RectTransform west = CreateCardinal(
                dial,
                "West",
                "W",
                new Vector2(-28f, 0f),
                Color.white,
                font);

            GameObject objectiveDiamondObject = CreateUiImage(
                dial,
                "Objective Diamond",
                new Color(0.32f, 1.35f, 1.1f, 1f));
            RectTransform objectiveDiamond =
                objectiveDiamondObject.GetComponent<RectTransform>();
            SetRect(
                objectiveDiamond,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, CompassObjectiveIndicatorRadius),
                CompassObjectiveDiamondSize);
            objectiveDiamond.localRotation = Quaternion.Euler(0f, 0f, 45f);

            TMP_Text facingMarker = CreateUiText(
                dialBackground.transform,
                "Player Heading Marker",
                "\u25bc",
                font,
                15f,
                new Color(1f, 0.86f, 0.32f, 1f),
                TextAlignmentOptions.Center);
            SetRect(
                facingMarker.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 45f),
                new Vector2(24f, 20f));

            GameObject divider = CreateUiImage(
                compassRoot.transform,
                "Divider",
                new Color(0.28f, 0.85f, 0.8f, 0.45f));
            SetRect(
                divider.GetComponent<RectTransform>(),
                new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(139f, -3f),
                new Vector2(2f, 86f));

            TMP_Text waypointTitle = CreateUiText(
                compassRoot.transform,
                "Waypoint Title",
                "ACTIVE WAYPOINT",
                font,
                12f,
                new Color(0.45f, 1f, 0.92f, 1f),
                TextAlignmentOptions.Left);
            SetRect(
                waypointTitle.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(157f, -12f),
                new Vector2(282f, 18f));

            TMP_Text objectiveLabel = CreateUiText(
                compassRoot.transform,
                "Active Waypoint",
                "NO ACTIVE WAYPOINT",
                font,
                17f,
                Color.white,
                TextAlignmentOptions.Left);
            objectiveLabel.enableAutoSizing = true;
            objectiveLabel.fontSizeMin = 11f;
            objectiveLabel.fontSizeMax = 17f;
            SetRect(
                objectiveLabel.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(157f, 3f),
                new Vector2(282f, 46f));

            TMP_Text hint = CreateUiText(
                compassRoot.transform,
                "Waypoint Hint",
                "Follow the glowing marker and compass bearing",
                font,
                11f,
                new Color(0.72f, 0.82f, 0.84f, 1f),
                TextAlignmentOptions.Left);
            SetRect(
                hint.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(157f, 10f),
                new Vector2(282f, 18f));

            CampaignCompassHUD compass =
                compassRoot.AddComponent<CampaignCompassHUD>();
            compass.Configure(
                null,
                null,
                dial,
                heading,
                new[] { north, east, south, west },
                objectiveDiamond,
                objectiveLabel,
                0.08f,
                0.5f,
                CompassObjectiveIndicatorRadius);
            if (!compass.ValidateConfiguration(out string compassError))
            {
                throw new InvalidOperationException(compassError);
            }

            // Keep guidance below Safety's pause, inventory, loading and fade
            // overlays without changing any of their existing sibling order.
            compassRoot.transform.SetAsFirstSibling();
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, UiPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static CampaignObjectiveWorldMarker BuildWorldMarker(
        Transform parent,
        CampaignObjectiveGuidance guidance,
        Mesh mesh,
        Material material)
    {
        GameObject controller = CreateChild(parent, "World Objective Marker");
        CampaignObjectiveWorldMarker marker =
            controller.AddComponent<CampaignObjectiveWorldMarker>();
        GameObject presentation = CreateChild(
            controller.transform,
            "Glowing Diamond Presentation");
        GameObject diamond = CreateChild(
            presentation.transform,
            "Spinning Objective Diamond");
        MeshFilter filter = diamond.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        MeshRenderer renderer = diamond.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        GameObject lightObject = CreateChild(
            presentation.transform,
            "Objective Glow Light");
        Light glow = lightObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(0.25f, 1f, 0.84f, 1f);
        glow.intensity = 7f;
        glow.range = 20f;
        glow.shadows = LightShadows.None;
        glow.renderMode = LightRenderMode.ForcePixel;

        marker.Configure(guidance, presentation, diamond.transform, glow);
        marker.ConfigurePresentation(
            ObjectiveDiamondWorldScale,
            ObjectiveDiamondHoverAmplitude,
            ObjectiveDiamondMaximumDistanceScale);
        EditorUtility.SetDirty(marker);
        if (!marker.ValidateConfiguration(out string markerError))
            throw new InvalidOperationException(markerError);

        return marker;
    }

    private static void ValidateFarmDirections(
        FarmPrologueDirector director,
        IReadOnlyList<FarmChoreInteractable> chores)
    {
        Transform previous = director.PlayerSpawnFallback;
        for (int index = 0; index < FarmChores.Length; index++)
        {
            FarmChoreInteractable chore = chores[index];
            Vector3 route = chore.transform.position - previous.position;
            route.y = 0f;
            string actual = CampaignCompassHUD.GetCardinalHeading(
                CampaignCompassHUD.CalculateTrueHeading(route));
            string expected = FarmChores[index].ExpectedDirection;
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Farm instruction {index + 1} says {expected}, but its " +
                    $"authored compass route is {actual}.");
            }

            previous = chore.transform;
        }
    }

    private static void ValidateSceneGuidance(
        Scene scene,
        CampaignObjectiveGuidance guidance,
        int expectedWaypointCount)
    {
        CampaignObjectiveGuidance[] guidanceComponents =
            FindComponents<CampaignObjectiveGuidance>(scene);
        CampaignObjectiveWorldMarker[] markers =
            FindComponents<CampaignObjectiveWorldMarker>(scene);
        if (guidanceComponents.Length != 1 ||
            guidanceComponents[0] != guidance ||
            markers.Length != 1)
        {
            throw new InvalidOperationException(
                $"{scene.name} requires exactly one guidance authority and one world marker.");
        }

        if (!guidance.ValidateConfiguration(out string guidanceError))
            throw new InvalidOperationException(guidanceError);
        if (!markers[0].ValidateConfiguration(out string markerError))
            throw new InvalidOperationException(markerError);

        Transform anchorRoot = RequireUniqueDescendant(
            guidance.transform,
            AnchorRootName);
        if (anchorRoot.childCount != expectedWaypointCount)
        {
            throw new InvalidOperationException(
                $"{scene.name} requires {expectedWaypointCount} authored waypoint anchors; " +
                $"found {anchorRoot.childCount}.");
        }

        if (guidance.GetComponentInChildren<Collider>(true) != null)
        {
            throw new InvalidOperationException(
                $"{scene.name} guidance presentation cannot add gameplay collision.");
        }
    }

    private static void ValidateMarkerAssets(
        Scene scene,
        Mesh expectedMesh,
        Material expectedMaterial)
    {
        CampaignObjectiveWorldMarker marker =
            RequireSingleComponent<CampaignObjectiveWorldMarker>(scene);
        MeshFilter filter = marker.SpinningDiamond != null
            ? marker.SpinningDiamond.GetComponent<MeshFilter>()
            : null;
        MeshRenderer renderer = marker.SpinningDiamond != null
            ? marker.SpinningDiamond.GetComponent<MeshRenderer>()
            : null;
        if (filter == null || filter.sharedMesh != expectedMesh ||
            renderer == null || renderer.sharedMaterial != expectedMaterial)
        {
            throw new InvalidOperationException(
                $"{scene.name} does not reference the exact shared objective diamond assets.");
        }

        if (!Approximately(marker.BaseWorldScale, ObjectiveDiamondWorldScale) ||
            !Mathf.Approximately(
                marker.HoverAmplitude,
                ObjectiveDiamondHoverAmplitude) ||
            !Mathf.Approximately(
                marker.MaximumDistanceScale,
                ObjectiveDiamondMaximumDistanceScale))
        {
            throw new InvalidOperationException(
                $"{scene.name} objective diamond presentation is not using the compact tuning.");
        }
    }

    private static void ValidateFarmRumble(Scene scene)
    {
        FarmRumblePresenter presenter =
            RequireSingleComponent<FarmRumblePresenter>(scene);
        if (!Mathf.Approximately(
                presenter.ShakeAmplitude,
                FirstWaveShakeAmplitude) ||
            !Mathf.Approximately(
                presenter.ShakeFrequency,
                FirstWaveShakeFrequency) ||
            !Mathf.Approximately(
                presenter.RampInSeconds,
                FirstWaveShakeRampInSeconds))
        {
            throw new InvalidOperationException(
                "Farm first-wave rumble is not using the reduced shake tuning.");
        }
    }

    private static void ValidateCompassPrefab()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(UiPrefabPath);
        try
        {
            CampaignCompassHUD[] compasses =
                prefabRoot.GetComponentsInChildren<CampaignCompassHUD>(true);
            string error = string.Empty;
            bool valid = compasses.Length == 1 &&
                         compasses[0].ValidateConfiguration(out error);
            if (!valid)
            {
                throw new InvalidOperationException(
                    "Safety UI compass validation failed: " +
                    (compasses.Length == 1 ? error :
                        $"expected one compass, found {compasses.Length}."));
            }

            CampaignCompassHUD compass = compasses[0];
            if (compass.transform.GetSiblingIndex() != 0)
                throw new InvalidOperationException(
                    "The compass must remain below Safety's modal and loading overlays.");
            RectTransform compassRect =
                compass.GetComponent<RectTransform>();
            RectTransform dialBackground =
                RequireUniqueDescendant(
                    compass.transform,
                    "Compass Dial Background") as RectTransform;
            Transform objectiveDiamondTransform =
                RequireUniqueDescendant(
                    compass.transform,
                    "Objective Diamond");
            RectTransform objectiveDiamond =
                objectiveDiamondTransform as RectTransform;
            Image objectiveDiamondImage =
                objectiveDiamondTransform.GetComponent<Image>();

            if (compassRect == null ||
                !Approximately(compassRect.sizeDelta, CompassPanelSize) ||
                dialBackground == null ||
                !Approximately(
                    dialBackground.sizeDelta,
                    CompassDialBackgroundSize) ||
                !Approximately(
                    compass.CompassDial.sizeDelta,
                    CompassDialSize) ||
                objectiveDiamond == null ||
                !Approximately(
                    objectiveDiamond.sizeDelta,
                    CompassObjectiveDiamondSize) ||
                objectiveDiamondImage == null ||
                Quaternion.Angle(objectiveDiamond.localRotation,
                    Quaternion.Euler(0f, 0f, 45f)) > .01f ||
                !Mathf.Approximately(
                    compass.ObjectiveIndicatorRadius,
                    CompassObjectiveIndicatorRadius))
            {
                throw new InvalidOperationException(
                    "Safety UI compass is not using the compact presentation tuning.");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static GameObject RebuildOwnedSceneRoot(Scene scene)
    {
        GameObject[] previousRoots = scene.GetRootGameObjects()
            .Where(candidate => string.Equals(
                candidate.name,
                GuidanceRootName,
                StringComparison.Ordinal))
            .ToArray();
        foreach (GameObject previousRoot in previousRoots)
            UnityEngine.Object.DestroyImmediate(previousRoot);

        GameObject root = new GameObject(GuidanceRootName);
        SceneManager.MoveGameObjectToScene(root, scene);
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;
        return root;
    }

    private static Transform CreateAnchor(
        Transform parent,
        string name,
        Vector3 worldPosition)
    {
        GameObject anchor = CreateChild(parent, name);
        anchor.transform.position = worldPosition;
        anchor.transform.rotation = Quaternion.identity;
        anchor.transform.localScale = Vector3.one;
        return anchor.transform;
    }

    private static Vector3 CalculateMarkerPosition(
        GameObject target,
        Collider interactionCollider,
        float heightOffset)
    {
        Bounds bounds;
        bool hasBounds = false;
        if (interactionCollider != null)
        {
            bounds = interactionCollider.bounds;
            hasBounds = true;
        }
        else
        {
            bounds = new Bounds(target.transform.position, Vector3.zero);
        }

        foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null)
                continue;

            Bounds rendererBounds = renderer.bounds;
            if (rendererBounds.size.sqrMagnitude <= 0.000001f)
                continue;

            if (hasBounds)
                bounds.Encapsulate(rendererBounds);
            else
            {
                bounds = rendererBounds;
                hasBounds = true;
            }
        }

        Vector3 horizontalCenter = interactionCollider != null
            ? interactionCollider.bounds.center
            : bounds.center;
        return new Vector3(
            horizontalCenter.x,
            bounds.max.y + heightOffset,
            horizontalCenter.z);
    }

    private static Mesh EnsureDiamondMesh()
    {
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(DiamondMeshPath);
        if (mesh == null)
        {
            mesh = new Mesh();
            AssetDatabase.CreateAsset(mesh, DiamondMeshPath);
        }

        mesh.Clear();
        mesh.name = "ObjectiveDiamond";
        mesh.vertices = new[]
        {
            new Vector3(0f, 1f, 0f),
            new Vector3(0f, -1f, 0f),
            new Vector3(-1f, 0f, 0f),
            new Vector3(1f, 0f, 0f),
            new Vector3(0f, 0f, 1f),
            new Vector3(0f, 0f, -1f)
        };
        mesh.triangles = new[]
        {
            0, 4, 3,
            0, 3, 5,
            0, 5, 2,
            0, 2, 4,
            1, 3, 4,
            1, 5, 3,
            1, 2, 5,
            1, 4, 2
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static Material EnsureDiamondMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            throw new InvalidOperationException(
                "The URP Lit shader is unavailable for the objective marker.");
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(
            DiamondMaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, DiamondMaterialPath);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        material.name = "ObjectiveDiamond_Glow";
        Color baseColor = new Color(0.08f, 1.15f, 0.88f, 1f);
        Color emission = new Color(0.18f, 8f, 5.5f, 1f);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", baseColor);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", baseColor);
        if (material.HasProperty("_EmissionColor"))
            material.SetColor("_EmissionColor", emission);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.72f);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0.18f);
        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags =
            MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureAssetFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int index = 1; index < parts.Length; index++)
        {
            string next = current + "/" + parts[index];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[index]);
            current = next;
        }
    }

    private static string GetTowerDisplayName(CampaignAreaId area)
    {
        return area switch
        {
            CampaignAreaId.BlackPines => "Black Pines progression tower",
            CampaignAreaId.StillwaterFeedMill =>
                "Stillwater Feed Mill progression tower",
            CampaignAreaId.HarrowEstate =>
                "Harrow Estate progression tower",
            CampaignAreaId.BloodrootHollow =>
                "Bloodroot Hollow progression tower",
            _ => throw new ArgumentOutOfRangeException(nameof(area), area, null)
        };
    }

    private static GameObject CreateChild(Transform parent, string name)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;
        return child;
    }

    private static GameObject CreateUiTransform(Transform parent, string name)
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child;
    }

    private static GameObject CreateUiImage(
        Transform parent,
        string name,
        Color color)
    {
        GameObject child = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        child.transform.SetParent(parent, false);
        Image image = child.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return child;
    }

    private static TMP_Text CreateUiText(
        Transform parent,
        string name,
        string value,
        TMP_FontAsset font,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment)
    {
        GameObject child = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        child.transform.SetParent(parent, false);
        TextMeshProUGUI text = child.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.richText = true;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static void CreateDialLine(
        RectTransform dial,
        string name,
        Vector2 size)
    {
        GameObject line = CreateUiImage(
            dial,
            name,
            new Color(0.48f, 0.72f, 0.73f, 0.32f));
        SetRect(
            line.GetComponent<RectTransform>(),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            size);
    }

    private static RectTransform CreateCardinal(
        RectTransform dial,
        string name,
        string value,
        Vector2 position,
        Color color,
        TMP_FontAsset font)
    {
        TMP_Text text = CreateUiText(
            dial,
            name,
            value,
            font,
            17f,
            color,
            TextAlignmentOptions.Center);
        text.fontStyle = FontStyles.Bold;
        SetRect(
            text.rectTransform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            position,
            new Vector2(24f, 20f));
        return text.rectTransform;
    }

    private static bool Approximately(Vector2 left, Vector2 right)
    {
        return (left - right).sqrMagnitude <= 0.0001f;
    }

    private static bool Approximately(Vector3 left, Vector3 right)
    {
        return (left - right).sqrMagnitude <= 0.0001f;
    }

    private static void SetRect(
        RectTransform rect,
        Vector2 anchor,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
    }

    private static Transform RequireUniqueDescendant(
        Transform root,
        string name)
    {
        Transform[] matches = root.GetComponentsInChildren<Transform>(true)
            .Where(candidate => string.Equals(
                candidate.name,
                name,
                StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"'{root.name}' requires exactly one descendant named " +
                $"'{name}'; found {matches.Length}.");
        }

        return matches[0];
    }

    private static T RequireNamedComponent<T>(Scene scene, string objectName)
        where T : Component
    {
        T[] matches = FindComponents<T>(scene)
            .Where(component => string.Equals(
                component.gameObject.name,
                objectName,
                StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"{scene.name} requires exactly one {typeof(T).Name} on " +
                $"'{objectName}'; found {matches.Length}.");
        }

        return matches[0];
    }

    private static T RequireSingleComponent<T>(Scene scene)
        where T : Component
    {
        T[] components = FindComponents<T>(scene);
        if (components.Length != 1)
        {
            throw new InvalidOperationException(
                $"{scene.name} requires exactly one {typeof(T).Name}; " +
                $"found {components.Length}.");
        }

        return components[0];
    }

    private static T[] FindComponents<T>(Scene scene) where T : Component
    {
        var components = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
            components.AddRange(root.GetComponentsInChildren<T>(true));
        return components.ToArray();
    }

    private static void SaveScene(Scene scene, string path)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, path))
        {
            throw new InvalidOperationException(
                $"Unity could not save {path}.");
        }
    }
}
#endif
