using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bloodroot.Campaign;
using Bloodroot.Features.AlphaEnemies;
using Bloodroot.Features.FarmPrologue;
using Bloodroot.Features.WorldMissions;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Narrow campaign migration for six gradual environment looks and the exact
/// regular Boar, Root Boar, Juggernaut ambient roster. The migration never
/// rebuilds source scenes or source enemy prefabs; it edits only the two
/// production scenes, six owned materials/profiles, and the three explicitly
/// named campaign prefabs below.
/// </summary>
public static class BloodrootEnvironmentAndRosterSetup
{
    private const string FarmScenePath =
        "Assets/Scenes/Campaign/Farm_PrologueHub.unity";
    private const string OpenWorldScenePath =
        "Assets/Scenes/OpenWorld/Bloodroot_OpenWorld.unity";

    private const string MaterialFolder =
        "Assets/Materials/Features/CampaignEnvironment";
    private const string ProfileFolder =
        "Assets/VFX/CampaignEnvironment";

    private const string BoarPath = "Assets/PreFabs/Enemies/Boar.prefab";
    private const string BoarGuid = "c8ad7f826e5d0ab4492f0a5a73e86d54";
    private const string BoarRootPath =
        "Assets/PreFabs/Enemies/BoarRoot.prefab";
    private const string BoarRootGuid =
        "e81ccd077fe525944bd95804a17261ee";
    private const string RegularHogPath =
        "Assets/PreFabs/Enemies/RegularHog.prefab";
    private const string RegularHogGuid =
        "9bd9d91575d646d4abaf79a6ec9d8f31";

    private const string JuggernautSourcePath =
        "Assets/PreFabs/Enemies/Juggernaut.prefab";
    private const string JuggernautSourceGuid =
        "fbd7a0d02c2da08428098efcf05b9a5e";
    private const string JuggernautVariantPath =
        "Assets/PreFabs/AlphaPlaceholders/" +
        "Juggernaut_CampaignCompatible.prefab";
    private const string JuggernautVariantGuid =
        "309e4ba5d962b7a4a9d2a8630fd81b80";
    private const string JuggernautControllerPath =
        "Assets/PreFabs/Enemies/ABP_Juggernaut.controller";
    private const string JuggernautControllerGuid =
        "96f7b682ddc9b274ca09f6de173f1c0f";
    private const string RetiredCompatibilityControllerPath =
        "Assets/PreFabs/AlphaPlaceholders/" +
        "SafetyEnemyCompatibility.controller";

    private const string WitchSummonerPath =
        "Assets/PreFabs/AlphaPlaceholders/" +
        "Witch_Summoner_PLACEHOLDER.prefab";
    private const string WitchSummonerGuid =
        "3c37c90bb83af7c49b07509204981041";
    private const string WitchMatriarchPath =
        "Assets/PreFabs/AlphaPlaceholders/" +
        "Witch_Matriarch_PLACEHOLDER.prefab";
    private const string WitchMatriarchGuid =
        "d5b4bdf5bd573014d9267807274b9895";
    private const string RetiredWitchHogPath =
        "Assets/PreFabs/AlphaPlaceholders/" +
        "WitchSummonedHog_PLACEHOLDER.prefab";
    private const string RetiredWitchHogGuid =
        "c1d8d1b510b9e824bb48d2c5fe5823d2";

    private const string EnvironmentRootName =
        "Campaign Environment Transitions";
    private const string FarmEnvironmentParentPath =
        "__CAMPAIGN_STRUCTURE/_CORE";
    private const string OpenWorldEnvironmentParentPath =
        "Bloodroot_OpenWorld/_LIGHTING";

    private const float TransitionSeconds = 8f;
    private const float AreaPollSeconds = 0.25f;
    private const float AreaSwitchHysteresis = 25f;
    private const float VolumePriority = 10f;
    private const float FloatTolerance = 0.0001f;

    private static readonly CampaignAreaId[] AreaOrder =
    {
        CampaignAreaId.BlackPines,
        CampaignAreaId.StillwaterFeedMill,
        CampaignAreaId.HarrowEstate,
        CampaignAreaId.BloodrootHollow
    };

    private static readonly string[] ArrivalEncounterNames =
    {
        "ARRIVAL_ENCOUNTER_Black_Pines",
        "ARRIVAL_ENCOUNTER_Stillwater",
        "ARRIVAL_ENCOUNTER_Harrow_Estate"
    };

    private sealed class EnvironmentSpec
    {
        public EnvironmentSpec(
            string id,
            string assetName,
            Color skyTint,
            Color groundColor,
            float atmosphere,
            float exposure,
            float sunSize,
            Color fogColor,
            float fogDensity,
            Color ambientSky,
            Color ambientEquator,
            Color ambientGround,
            float ambientIntensity,
            float reflectionIntensity,
            Color sunColor,
            float sunIntensity,
            Vector3 sunEuler,
            float postExposure,
            float contrast,
            float saturation,
            Color colorFilter,
            float temperature,
            float tint,
            Color vignetteColor,
            float vignetteIntensity,
            float vignetteSmoothness,
            float bloomThreshold,
            float bloomIntensity,
            float bloomScatter)
        {
            Id = id;
            AssetName = assetName;
            SkyTint = skyTint;
            GroundColor = groundColor;
            Atmosphere = atmosphere;
            Exposure = exposure;
            SunSize = sunSize;
            FogColor = fogColor;
            FogDensity = fogDensity;
            AmbientSky = ambientSky;
            AmbientEquator = ambientEquator;
            AmbientGround = ambientGround;
            AmbientIntensity = ambientIntensity;
            ReflectionIntensity = reflectionIntensity;
            SunColor = sunColor;
            SunIntensity = sunIntensity;
            SunEuler = sunEuler;
            PostExposure = postExposure;
            Contrast = contrast;
            Saturation = saturation;
            ColorFilter = colorFilter;
            Temperature = temperature;
            Tint = tint;
            VignetteColor = vignetteColor;
            VignetteIntensity = vignetteIntensity;
            VignetteSmoothness = vignetteSmoothness;
            BloomThreshold = bloomThreshold;
            BloomIntensity = bloomIntensity;
            BloomScatter = bloomScatter;
        }

        public string Id { get; }
        public string AssetName { get; }
        public Color SkyTint { get; }
        public Color GroundColor { get; }
        public float Atmosphere { get; }
        public float Exposure { get; }
        public float SunSize { get; }
        public Color FogColor { get; }
        public float FogDensity { get; }
        public Color AmbientSky { get; }
        public Color AmbientEquator { get; }
        public Color AmbientGround { get; }
        public float AmbientIntensity { get; }
        public float ReflectionIntensity { get; }
        public Color SunColor { get; }
        public float SunIntensity { get; }
        public Vector3 SunEuler { get; }
        public float PostExposure { get; }
        public float Contrast { get; }
        public float Saturation { get; }
        public Color ColorFilter { get; }
        public float Temperature { get; }
        public float Tint { get; }
        public Color VignetteColor { get; }
        public float VignetteIntensity { get; }
        public float VignetteSmoothness { get; }
        public float BloomThreshold { get; }
        public float BloomIntensity { get; }
        public float BloomScatter { get; }

        public string MaterialPath =>
            MaterialFolder + "/" + AssetName + "_Skybox.mat";
        public string ProfilePath =>
            ProfileFolder + "/" + AssetName + "_Profile.asset";
        public string VolumeName => AssetName + " Volume";
    }

    private static readonly EnvironmentSpec FarmPrologue = new(
        "farm_prologue_dawn",
        "Farm_Prologue_Dawn",
        new Color(0.46f, 0.58f, 0.72f, 1f),
        new Color(0.24f, 0.19f, 0.16f, 1f),
        1.35f, 0.85f, 0.045f,
        new Color(0.34f, 0.39f, 0.43f, 1f), 0.012f,
        new Color(0.42f, 0.52f, 0.65f, 1f),
        new Color(0.31f, 0.30f, 0.29f, 1f),
        new Color(0.12f, 0.10f, 0.09f, 1f),
        0.78f, 0.72f,
        new Color(1f, 0.72f, 0.48f, 1f), 0.82f,
        new Vector3(18f, -42f, 0f),
        -0.25f, 8f, -12f,
        new Color(0.92f, 0.90f, 0.86f, 1f),
        -6f, 3f,
        new Color(0.04f, 0.045f, 0.055f, 1f), 0.12f, 0.38f,
        1.15f, 0.18f, 0.52f);

    private static readonly EnvironmentSpec FarmHub = new(
        "farm_hub_clear_day",
        "Farm_Hub_Clear_Day",
        new Color(0.42f, 0.65f, 0.86f, 1f),
        new Color(0.31f, 0.28f, 0.20f, 1f),
        0.85f, 1.15f, 0.035f,
        new Color(0.55f, 0.65f, 0.68f, 1f), 0.0045f,
        new Color(0.53f, 0.68f, 0.85f, 1f),
        new Color(0.38f, 0.40f, 0.36f, 1f),
        new Color(0.17f, 0.15f, 0.10f, 1f),
        1.05f, 0.9f,
        new Color(1f, 0.93f, 0.74f, 1f), 1.15f,
        new Vector3(42f, -28f, 0f),
        0.15f, 12f, 5f,
        new Color(1f, 0.98f, 0.92f, 1f),
        5f, 1f,
        new Color(0.035f, 0.03f, 0.025f, 1f), 0.08f, 0.42f,
        1.05f, 0.24f, 0.58f);

    private static readonly EnvironmentSpec BlackPines = new(
        "black_pines_forest",
        "Black_Pines_Forest",
        new Color(0.18f, 0.30f, 0.30f, 1f),
        new Color(0.035f, 0.055f, 0.045f, 1f),
        2f, 0.92f, 0.06f,
        new Color(0.105f, 0.16f, 0.145f, 1f), 0.007f,
        new Color(0.16f, 0.27f, 0.27f, 1f),
        new Color(0.09f, 0.13f, 0.11f, 1f),
        new Color(0.025f, 0.04f, 0.03f, 1f),
        1.28f, 0.85f,
        new Color(0.70f, 0.80f, 0.72f, 1f), 1.32f,
        new Vector3(28f, 22f, 0f),
        0.40f, 10f, -26f,
        new Color(0.76f, 0.86f, 0.79f, 1f),
        -10f, -7f,
        new Color(0.008f, 0.018f, 0.014f, 1f), 0.08f, 0.40f,
        1.12f, 0.16f, 0.50f);

    private static readonly EnvironmentSpec Stillwater = new(
        "stillwater_industrial_haze",
        "Stillwater_Industrial_Haze",
        new Color(0.58f, 0.46f, 0.26f, 1f),
        new Color(0.14f, 0.105f, 0.055f, 1f),
        1.55f, 1.02f, 0.075f,
        new Color(0.30f, 0.25f, 0.15f, 1f), 0.0065f,
        new Color(0.48f, 0.38f, 0.22f, 1f),
        new Color(0.28f, 0.23f, 0.15f, 1f),
        new Color(0.09f, 0.065f, 0.035f, 1f),
        1.30f, 0.85f,
        new Color(1f, 0.62f, 0.31f, 1f), 1.40f,
        new Vector3(21f, -112f, 0f),
        0.38f, 14f, -18f,
        new Color(1f, 0.81f, 0.55f, 1f),
        17f, -8f,
        new Color(0.045f, 0.025f, 0.012f, 1f), 0.08f, 0.34f,
        0.92f, 0.34f, 0.62f);

    private static readonly EnvironmentSpec Harrow = new(
        "harrow_estate_dusk",
        "Harrow_Estate_Dusk",
        new Color(0.27f, 0.20f, 0.43f, 1f),
        new Color(0.055f, 0.04f, 0.075f, 1f),
        2.15f, 0.92f, 0.085f,
        new Color(0.17f, 0.12f, 0.23f, 1f), 0.006f,
        new Color(0.27f, 0.20f, 0.40f, 1f),
        new Color(0.14f, 0.11f, 0.20f, 1f),
        new Color(0.045f, 0.032f, 0.068f, 1f),
        1.25f, 0.78f,
        new Color(0.82f, 0.55f, 0.83f, 1f), 1.30f,
        new Vector3(12f, 146f, 0f),
        0.38f, 18f, -20f,
        new Color(0.84f, 0.73f, 0.94f, 1f),
        -14f, 10f,
        new Color(0.018f, 0.008f, 0.030f, 1f), 0.10f, 0.32f,
        0.82f, 0.28f, 0.66f);

    private static readonly EnvironmentSpec Hollow = new(
        "bloodroot_hollow_crimson",
        "Bloodroot_Hollow_Crimson",
        new Color(0.30f, 0.025f, 0.045f, 1f),
        new Color(0.012f, 0.006f, 0.008f, 1f),
        2.8f, 0.85f, 0.12f,
        new Color(0.17f, 0.018f, 0.025f, 1f), 0.008f,
        new Color(0.30f, 0.035f, 0.055f, 1f),
        new Color(0.13f, 0.015f, 0.025f, 1f),
        new Color(0.025f, 0.004f, 0.007f, 1f),
        1.18f, 0.72f,
        new Color(1f, 0.18f, 0.13f, 1f), 1.20f,
        new Vector3(8f, -168f, 0f),
        0.35f, 20f, -34f,
        new Color(0.86f, 0.42f, 0.43f, 1f),
        24f, 14f,
        new Color(0.025f, 0.001f, 0.004f, 1f), 0.10f, 0.29f,
        0.62f, 0.52f, 0.72f);

    private static readonly EnvironmentSpec[] AllSpecs =
    {
        FarmPrologue,
        FarmHub,
        BlackPines,
        Stillwater,
        Harrow,
        Hollow
    };

    private static readonly EnvironmentSpec[] OpenWorldSpecs =
    {
        BlackPines,
        Stillwater,
        Harrow,
        Hollow
    };

    [MenuItem(
        "Tools/Bloodroot/Campaign/Build Environment And Enemy Rosters")]
    public static void BuildFromMenu()
    {
        try
        {
            RunBuild();
            EditorUtility.DisplayDialog(
                "Bloodroot Campaign Environment",
                "The six campaign environments and exact enemy rosters " +
                "were authored and validated.",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Bloodroot Campaign Environment Failed",
                exception.Message,
                "OK");
        }
    }

    public static void BuildBatch()
    {
        RunBuild();
    }

    [MenuItem(
        "Tools/Bloodroot/Campaign/Validate Environment And Enemy Rosters")]
    public static void ValidateFromMenu()
    {
        try
        {
            RunReadOnlyValidation();
            EditorUtility.DisplayDialog(
                "Bloodroot Campaign Environment",
                "Saved assets, prefabs, scenes, and rosters passed " +
                "read-only validation.",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Bloodroot Campaign Environment Validation Failed",
                exception.Message,
                "OK");
        }
    }

    public static void ValidateBatch()
    {
        RunReadOnlyValidation();
    }

    private static void RunBuild()
    {
        RequireSafeEditorState();
        RequireSourceInputs();
        SceneSetup[] originalSetup =
            EditorSceneManager.GetSceneManagerSetup();

        try
        {
            EnsureFolder(MaterialFolder);
            EnsureFolder(ProfileFolder);
            foreach (EnvironmentSpec spec in AllSpecs)
            {
                EnsureSkybox(spec);
                EnsureProfile(spec);
            }

            PreflightJuggernautVariant();
            PreflightWitchPrefab(WitchSummonerPath, WitchSummonerGuid);
            PreflightWitchPrefab(WitchMatriarchPath, WitchMatriarchGuid);
            PatchJuggernautVariant();
            PatchWitchPrefab(WitchSummonerPath, WitchSummonerGuid);
            PatchWitchPrefab(WitchMatriarchPath, WitchMatriarchGuid);

            // Prefab contents use temporary preview scenes and can leave no
            // active scene in batch mode. Open the production scenes only
            // after all prefab work so their RenderSettings remain writable.
            OpenScenes(out Scene farm, out Scene openWorld);
            PreflightFarm(farm);
            PreflightOpenWorld(openWorld);
            AuthorFarm(farm);
            AuthorOpenWorld(openWorld);

            ValidateEnvironmentAssets();
            ValidateFarm(farm);
            ValidateOpenWorld(openWorld);
            ValidateCampaignPrefabs();

            var saved = new List<string>();
            if (farm.isDirty)
            {
                Require(
                    EditorSceneManager.SaveScene(farm, FarmScenePath),
                    "Unity could not save the Farm campaign scene.");
                saved.Add(FarmScenePath);
            }

            if (openWorld.isDirty)
            {
                Require(
                    EditorSceneManager.SaveScene(openWorld, OpenWorldScenePath),
                    "Unity could not save the Open World scene.");
                saved.Add(OpenWorldScenePath);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            OpenScenes(out farm, out openWorld);
            ValidateEnvironmentAssets();
            ValidateFarm(farm);
            ValidateOpenWorld(openWorld);
            ValidateCampaignPrefabs();

            Debug.Log(
                "BLOODROOT_ENVIRONMENT_ROSTER_BUILD: PASS. Saved scenes: " +
                (saved.Count == 0 ? "<none>" : string.Join(", ", saved)));
        }
        finally
        {
            RestoreSceneSetup(originalSetup);
        }
    }

    private static void RunReadOnlyValidation()
    {
        RequireSafeEditorState();
        RequireSourceInputs();
        SceneSetup[] originalSetup =
            EditorSceneManager.GetSceneManagerSetup();

        try
        {
            ValidateEnvironmentAssets();
            ValidateCampaignPrefabs();
            OpenScenes(out Scene farm, out Scene openWorld);
            ValidateFarm(farm);
            ValidateOpenWorld(openWorld);
            Require(!farm.isDirty && !openWorld.isDirty,
                "Read-only validation dirtied a production scene.");
            Debug.Log("BLOODROOT_ENVIRONMENT_ROSTER_VALIDATE: PASS");
        }
        finally
        {
            RestoreSceneSetup(originalSetup);
        }
    }

    private static void RequireSafeEditorState()
    {
        Require(
            !Application.isPlaying && !EditorApplication.isCompiling &&
            !EditorApplication.isUpdating,
            "Environment authoring requires an idle Edit Mode Editor.");

        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            Scene scene = SceneManager.GetSceneAt(index);
            Require(
                !scene.isDirty,
                $"Save or discard unsaved scene '{scene.name}' first.");
        }
    }

    private static void RequireSourceInputs()
    {
        Require(File.Exists(ToAbsolutePath(FarmScenePath)),
            "Missing Farm production scene.");
        Require(File.Exists(ToAbsolutePath(OpenWorldScenePath)),
            "Missing Open World production scene.");
        RequireExactAsset<GameObject>(BoarPath, BoarGuid, "regular Boar");
        RequireExactAsset<GameObject>(BoarRootPath, BoarRootGuid, "Root Boar");
        RequireExactAsset<GameObject>(RegularHogPath, RegularHogGuid,
            "retired regular hog");
        RequireExactAsset<GameObject>(JuggernautSourcePath,
            JuggernautSourceGuid, "updated Safety Juggernaut");
        RequireExactAsset<GameObject>(JuggernautVariantPath,
            JuggernautVariantGuid, "campaign Juggernaut variant");
        RequireExactAsset<RuntimeAnimatorController>(JuggernautControllerPath,
            JuggernautControllerGuid, "Juggernaut animator controller");
        RequireExactAsset<GameObject>(WitchSummonerPath, WitchSummonerGuid,
            "Summoner witch");
        RequireExactAsset<GameObject>(WitchMatriarchPath, WitchMatriarchGuid,
            "Matriarch witch");
        RequireExactAsset<GameObject>(RetiredWitchHogPath,
            RetiredWitchHogGuid, "retired witch hog");
    }

    private static void OpenScenes(out Scene farm, out Scene openWorld)
    {
        farm = EditorSceneManager.OpenScene(
            FarmScenePath,
            OpenSceneMode.Single);
        openWorld = EditorSceneManager.OpenScene(
            OpenWorldScenePath,
            OpenSceneMode.Additive);
        Require(
            farm.IsValid() && farm.isLoaded &&
            openWorld.IsValid() && openWorld.isLoaded,
            "Unity did not load both production scenes.");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);
        Require(!string.IsNullOrWhiteSpace(parent) &&
                AssetDatabase.IsValidFolder(parent),
            "Owned asset parent folder is missing: " + parent);
        string guid = AssetDatabase.CreateFolder(parent, name);
        Require(!string.IsNullOrEmpty(guid),
            "Could not create owned asset folder: " + path);
    }

    private static Material EnsureSkybox(EnvironmentSpec spec)
    {
        Shader shader = Shader.Find("Skybox/Procedural");
        Require(shader != null, "Skybox/Procedural shader is unavailable.");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(
            spec.MaterialPath);
        if (material == null)
        {
            Require(string.IsNullOrEmpty(
                    AssetDatabase.AssetPathToGUID(spec.MaterialPath)),
                "Owned skybox path contains a non-Material asset: " +
                spec.MaterialPath);
            material = new Material(shader)
            {
                name = spec.AssetName + " Skybox"
            };
            AssetDatabase.CreateAsset(material, spec.MaterialPath);
        }

        Require(material.shader == shader,
            "Owned skybox shader drifted: " + spec.MaterialPath);
        bool changed = false;
        changed |= SetFloat(material, "_SunDisk", 2f);
        changed |= SetFloat(material, "_SunSize", spec.SunSize);
        changed |= SetFloat(material, "_SunSizeConvergence", 5f);
        changed |= SetFloat(material, "_AtmosphereThickness", spec.Atmosphere);
        changed |= SetColor(material, "_SkyTint", spec.SkyTint);
        changed |= SetColor(material, "_GroundColor", spec.GroundColor);
        changed |= SetFloat(material, "_Exposure", spec.Exposure);
        if (material.name != spec.AssetName + " Skybox")
        {
            material.name = spec.AssetName + " Skybox";
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
        }

        return material;
    }

    private static VolumeProfile EnsureProfile(EnvironmentSpec spec)
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(
            spec.ProfilePath);
        if (profile == null)
        {
            Require(string.IsNullOrEmpty(
                    AssetDatabase.AssetPathToGUID(spec.ProfilePath)),
                "Owned profile path contains a different asset: " +
                spec.ProfilePath);
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = spec.AssetName + " Profile";
            AssetDatabase.CreateAsset(profile, spec.ProfilePath);
        }

        VolumeComponent[] existing = profile.components
            .Where(component => component != null)
            .ToArray();
        ColorAdjustments color = FindOrCreate<ColorAdjustments>(
            profile, existing);
        WhiteBalance white = FindOrCreate<WhiteBalance>(profile, existing);
        Vignette vignette = FindOrCreate<Vignette>(profile, existing);
        Bloom bloom = FindOrCreate<Bloom>(profile, existing);

        var desired = new VolumeComponent[]
        {
            color, white, vignette, bloom
        };
        var desiredSet = new HashSet<VolumeComponent>(desired);
        foreach (VolumeComponent component in existing)
        {
            if (!desiredSet.Contains(component))
            {
                profile.components.Remove(component);
                UnityEngine.Object.DestroyImmediate(component, true);
            }
        }

        profile.components.Clear();
        profile.components.AddRange(desired);
        profile.name = spec.AssetName + " Profile";

        color.active = true;
        SetParameter(color.postExposure, spec.PostExposure);
        SetParameter(color.contrast, spec.Contrast);
        SetParameter(color.saturation, spec.Saturation);
        SetParameter(color.hueShift, 0f);
        SetParameter(color.colorFilter, spec.ColorFilter);

        white.active = true;
        SetParameter(white.temperature, spec.Temperature);
        SetParameter(white.tint, spec.Tint);

        vignette.active = true;
        SetParameter(vignette.color, spec.VignetteColor);
        SetParameter(vignette.center, new Vector2(0.5f, 0.5f));
        SetParameter(vignette.intensity, spec.VignetteIntensity);
        SetParameter(vignette.smoothness, spec.VignetteSmoothness);
        SetParameter(vignette.rounded, false);

        bloom.active = true;
        SetParameter(bloom.threshold, spec.BloomThreshold);
        SetParameter(bloom.intensity, spec.BloomIntensity);
        SetParameter(bloom.scatter, spec.BloomScatter);

        foreach (VolumeComponent component in desired)
            EditorUtility.SetDirty(component);
        EditorUtility.SetDirty(profile);
        profile.Reset();
        AssetDatabase.SaveAssetIfDirty(profile);
        return profile;
    }

    private static T FindOrCreate<T>(
        VolumeProfile profile,
        IReadOnlyList<VolumeComponent> existing)
        where T : VolumeComponent
    {
        T found = existing.FirstOrDefault(
            component => component.GetType() == typeof(T)) as T;
        return found != null
            ? found
            : VolumeProfileFactory.CreateVolumeComponent<T>(
                profile, true, false);
    }

    private static void SetParameter<T>(VolumeParameter<T> parameter, T value)
    {
        parameter.overrideState = true;
        parameter.value = value;
    }

    private static void PreflightJuggernautVariant()
    {
        GameObject asset = RequireExactAsset<GameObject>(
            JuggernautVariantPath,
            JuggernautVariantGuid,
            "campaign Juggernaut variant");
        Require(PrefabUtility.GetPrefabAssetType(asset) == PrefabAssetType.Variant,
            "Campaign Juggernaut must remain a prefab variant.");
        UnityEngine.Object source =
            PrefabUtility.GetCorrespondingObjectFromSource(asset);
        Require(AssetDatabase.GetAssetPath(source) == JuggernautSourcePath,
            "Campaign Juggernaut source prefab has drifted.");

        GameObject root = PrefabUtility.LoadPrefabContents(
            JuggernautVariantPath);
        try
        {
            juggernautEnemyAI[] ais =
                root.GetComponentsInChildren<juggernautEnemyAI>(true);
            Animator[] animators =
                root.GetComponentsInChildren<Animator>(true);
            Require(ais.Length == 1 && animators.Length >= 1 &&
                    animators.Length <= 3,
                "Campaign Juggernaut must contain one AI and one known " +
                "legacy-or-current Animator topology.");
            Require(PrefabUtility.GetCorrespondingObjectFromSource(ais[0]) != null,
                "Campaign Juggernaut AI must remain inherited from Safety.");

            Animator[] production = animators.Where(animator =>
                    animator.transform != root.transform &&
                    AssetDatabase.GetAssetPath(
                        animator.runtimeAnimatorController) ==
                    JuggernautControllerPath)
                .ToArray();
            Animator[] removableRoot = animators.Where(animator =>
                    animator.transform == root.transform &&
                    (animator.runtimeAnimatorController == null ||
                     AssetDatabase.GetAssetPath(
                         animator.runtimeAnimatorController) ==
                     RetiredCompatibilityControllerPath))
                .ToArray();
            Require(production.Length == 1 &&
                    production.Length + removableRoot.Length ==
                    animators.Length,
                "Campaign Juggernaut Animator inheritance is unrecognized " +
                $"(total={animators.Length}, production={production.Length}, " +
                $"removableRoot={removableRoot.Length}). " +
                string.Join(" | ", animators.Select(animator =>
                    $"{AnimationUtility.CalculateTransformPath(animator.transform, root.transform)};" +
                    $"enabled={animator.enabled};" +
                    $"controller={AssetDatabase.GetAssetPath(animator.runtimeAnimatorController)}")));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void PatchJuggernautVariant()
    {
        RuntimeAnimatorController controller =
            RequireExactAsset<RuntimeAnimatorController>(
                JuggernautControllerPath,
                JuggernautControllerGuid,
                "Juggernaut animator controller");
        GameObject root = PrefabUtility.LoadPrefabContents(
            JuggernautVariantPath);
        bool changed = false;
        try
        {
            juggernautEnemyAI ai = RequireSingle(
                root.GetComponentsInChildren<juggernautEnemyAI>(true),
                "campaign Juggernaut AI");
            Animator[] animators =
                root.GetComponentsInChildren<Animator>(true);
            Animator productionAnimator = animators.Single(animator =>
                animator.transform != root.transform &&
                AssetDatabase.GetAssetPath(
                    animator.runtimeAnimatorController) ==
                JuggernautControllerPath);
            Animator[] redundantRootAnimators = animators.Where(animator =>
                    animator.transform == root.transform)
                .ToArray();
            if (redundantRootAnimators.Length > 0)
            {
                Component rigBuilder = root.GetComponent("RigBuilder");
                if (rigBuilder != null)
                {
                    // Safety's RigBuilder has no authored rig layers, but its
                    // RequireComponent contract prevents Unity from removing
                    // the obsolete compatibility Animator. Remove that empty
                    // inherited helper on the campaign variant first so the
                    // exact Safety Animator can remain the sole authority.
                    UnityEngine.Object.DestroyImmediate(rigBuilder);
                    changed = true;
                }
            }

            foreach (Animator redundant in redundantRootAnimators)
            {
                UnityEngine.Object.DestroyImmediate(redundant);
                changed = true;
            }

            if (!ai.enabled)
            {
                ai.enabled = true;
                changed = true;
            }

            if (productionAnimator.runtimeAnimatorController != controller)
            {
                productionAnimator.runtimeAnimatorController = controller;
                changed = true;
            }

            SerializedObject serialized = new(ai);
            SerializedProperty animatorProperty =
                serialized.FindProperty("animator");
            Require(animatorProperty != null,
                "Juggernaut AI animator field is unavailable.");
            if (animatorProperty.objectReferenceValue != productionAnimator)
            {
                animatorProperty.objectReferenceValue = productionAnimator;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }

            if (changed)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(ai);
                PrefabUtility.RecordPrefabInstancePropertyModifications(
                    productionAnimator);
                Require(PrefabUtility.SaveAsPrefabAsset(
                            root, JuggernautVariantPath) != null,
                    "Could not save the campaign Juggernaut variant.");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void PreflightWitchPrefab(string path, string guid)
    {
        RequireExactAsset<GameObject>(path, guid, "campaign witch");
        GameObject boar = RequireExactAsset<GameObject>(
            BoarPath, BoarGuid, "regular Boar");
        GameObject retired = RequireExactAsset<GameObject>(
            RetiredWitchHogPath, RetiredWitchHogGuid, "retired witch hog");
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            WitchController witch = RequireSingle(
                root.GetComponentsInChildren<WitchController>(true),
                "witch controller in " + path);
            SerializedProperty minions =
                new SerializedObject(witch).FindProperty("minionPrefabs");
            Require(minions != null && minions.isArray &&
                    minions.arraySize == 1,
                "Witch minion topology is unrecognized: " + path);
            UnityEngine.Object value =
                minions.GetArrayElementAtIndex(0).objectReferenceValue;
            Require(value == boar || value == retired,
                "Witch minion is neither the retired hog nor exact Boar: " +
                path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void PatchWitchPrefab(string path, string guid)
    {
        RequireExactAsset<GameObject>(path, guid, "campaign witch");
        GameObject boar = RequireExactAsset<GameObject>(
            BoarPath, BoarGuid, "regular Boar");
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            WitchController witch = RequireSingle(
                root.GetComponentsInChildren<WitchController>(true),
                "witch controller in " + path);
            SerializedObject serialized = new(witch);
            SerializedProperty minions = serialized.FindProperty("minionPrefabs");
            if (minions.arraySize == 1 &&
                minions.GetArrayElementAtIndex(0).objectReferenceValue == boar)
            {
                return;
            }

            minions.arraySize = 1;
            minions.GetArrayElementAtIndex(0).objectReferenceValue = boar;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Require(PrefabUtility.SaveAsPrefabAsset(root, path) != null,
                "Could not save witch prefab: " + path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void PreflightFarm(Scene farm)
    {
        RequirePath(farm, FarmEnvironmentParentPath);
        RequireSingleSceneComponent<CampaignStateService>(farm);
        RequireSingleSceneComponent<FarmPrologueDirector>(farm);
        RequireSingleSceneComponent<FarmRecurringEmergenceDirector>(farm);
        RequireSingleSceneComponent<waveManager>(farm);
        RequireSingleSceneComponent<MobSpawner>(farm);
        RequireDirectionalLight(farm);
        CampaignEnvironmentTransitionController[] controllers =
            FindSceneComponents<CampaignEnvironmentTransitionController>(farm);
        Require(controllers.Length <= 1 &&
                (controllers.Length == 0 ||
                 controllers[0].gameObject.name == EnvironmentRootName),
            "Farm contains an unknown campaign environment controller.");
        ValidateFarmRoster(farm, allowPrevious: true);
    }

    private static void PreflightOpenWorld(Scene openWorld)
    {
        RequirePath(openWorld, OpenWorldEnvironmentParentPath);
        RequireSingleSceneComponent<CampaignStateService>(openWorld);
        CampaignRegionalRespawn respawn =
            RequireSingleSceneComponent<CampaignRegionalRespawn>(openWorld);
        Require(respawn.TryValidateAuthoredConfiguration(out string problem),
            "Regional respawn topology is invalid: " + problem);
        RequireDirectionalLight(openWorld);
        CampaignEnvironmentTransitionController[] controllers =
            FindSceneComponents<CampaignEnvironmentTransitionController>(
                openWorld);
        Require(controllers.Length <= 1 &&
                (controllers.Length == 0 ||
                 controllers[0].gameObject.name == EnvironmentRootName),
            "Open World contains an unknown campaign environment controller.");
        ValidateArrivalRosters(openWorld, allowPrevious: true);
    }

    private static void AuthorFarm(Scene farm)
    {
        GameObject boar = RequireExactAsset<GameObject>(
            BoarPath, BoarGuid, "regular Boar");
        GameObject boarRoot = RequireExactAsset<GameObject>(
            BoarRootPath, BoarRootGuid, "Root Boar");
        GameObject juggernaut = RequireExactAsset<GameObject>(
            JuggernautVariantPath, JuggernautVariantGuid,
            "campaign Juggernaut");

        MobSpawner spawner = RequireSingleSceneComponent<MobSpawner>(farm);
        SerializedObject spawnerData = new(spawner);
        SerializedProperty fallback = RequireProperty(spawnerData, "Enemy");
        SerializedProperty roster = RequireProperty(spawnerData, "enemies");
        SerializedProperty regularPig = RequireProperty(
            spawnerData, "regularPig");
        bool spawnerChanged =
            fallback.objectReferenceValue != boar ||
            !ArrayMatches(roster, boar, boarRoot, juggernaut) ||
            regularPig.objectReferenceValue != null;
        if (spawnerChanged)
        {
            fallback.objectReferenceValue = boar;
            SetArray(roster, boar, boarRoot, juggernaut);
            regularPig.objectReferenceValue = null;
            spawnerData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(spawner);
            EditorSceneManager.MarkSceneDirty(farm);
        }

        waveManager waves = RequireSingleSceneComponent<waveManager>(farm);
        SerializedObject waveData = new(waves);
        SerializedProperty hogIntro = RequireProperty(
            waveData, "useHogHuntIntro");
        if (hogIntro.boolValue)
        {
            hogIntro.boolValue = false;
            waveData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(waves);
            EditorSceneManager.MarkSceneDirty(farm);
        }

        FarmRecurringEmergenceDirector recurring =
            RequireSingleSceneComponent<FarmRecurringEmergenceDirector>(farm);
        SerializedObject recurringData = new(recurring);
        SerializedProperty recurringRoster = RequireProperty(
            recurringData, "enemyPrefabs");
        if (!ArrayMatches(recurringRoster, boar, boarRoot, juggernaut))
        {
            SetArray(recurringRoster, boar, boarRoot, juggernaut);
            recurringData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(recurring);
            EditorSceneManager.MarkSceneDirty(farm);
        }

        Transform parent = RequirePath(farm, FarmEnvironmentParentPath);
        Transform environmentRoot = EnsureEnvironmentRoot(parent, farm);
        Volume prologueVolume = EnsureVolume(
            environmentRoot, FarmPrologue, farm, 1f);
        Volume hubVolume = EnsureVolume(
            environmentRoot, FarmHub, farm, 0f);
        CampaignEnvironmentTransitionController controller =
            environmentRoot.GetComponent<CampaignEnvironmentTransitionController>();
        if (controller == null)
        {
            controller = environmentRoot.gameObject.AddComponent<
                CampaignEnvironmentTransitionController>();
            EditorSceneManager.MarkSceneDirty(farm);
        }

        CampaignStateService state =
            RequireSingleSceneComponent<CampaignStateService>(farm);
        FarmPrologueDirector director =
            RequireSingleSceneComponent<FarmPrologueDirector>(farm);
        Light sun = RequireDirectionalLight(farm);
        CampaignEnvironmentPreset prologuePreset =
            CreatePreset(FarmPrologue, prologueVolume);
        CampaignEnvironmentPreset hubPreset =
            CreatePreset(FarmHub, hubVolume);
        if (!FarmControllerMatches(controller, state, director, sun))
        {
            controller.ConfigureFarm(
                state, director, sun, prologuePreset, hubPreset,
                TransitionSeconds);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(farm);
        }

        ApplyPreview(farm, FarmPrologue, sun);
    }

    private static void AuthorOpenWorld(Scene openWorld)
    {
        GameObject boar = RequireExactAsset<GameObject>(
            BoarPath, BoarGuid, "regular Boar");
        GameObject boarRoot = RequireExactAsset<GameObject>(
            BoarRootPath, BoarRootGuid, "Root Boar");
        GameObject juggernaut = RequireExactAsset<GameObject>(
            JuggernautVariantPath, JuggernautVariantGuid,
            "campaign Juggernaut");
        GameObject[][] desiredRosters =
        {
            new[] { boar, boarRoot },
            new[] { boar, juggernaut },
            new[] { juggernaut }
        };

        WorldArrivalEnemySpawner[] spawners =
            ResolveArrivalSpawners(openWorld);
        Require(spawners.Length == desiredRosters.Length,
            "Open World arrival spawner count drifted before roster authoring.");
        for (int index = 0; index < spawners.Length; index++)
        {
            WorldArrivalEnemySpawner spawner = spawners[index];
            if (SpawnerRosterMatches(spawner, desiredRosters[index]))
                continue;

            var definitions = new WorldArrivalEnemySpawnDefinition[
                spawner.Spawns.Count];
            for (int spawnIndex = 0;
                 spawnIndex < definitions.Length;
                 spawnIndex++)
            {
                definitions[spawnIndex] = new WorldArrivalEnemySpawnDefinition(
                    desiredRosters[index][spawnIndex],
                    spawner.Spawns[spawnIndex].SpawnPoint);
            }

            spawner.Configure(
                spawner.ArrivalTrigger,
                spawner.RuntimeContainer,
                definitions,
                spawner.DifficultyLevel,
                spawner.NavMeshSampleRadius,
                spawner.PlayerTag);
            EditorUtility.SetDirty(spawner);
            EditorSceneManager.MarkSceneDirty(openWorld);
        }

        Transform parent = RequirePath(
            openWorld, OpenWorldEnvironmentParentPath);
        Transform environmentRoot = EnsureEnvironmentRoot(parent, openWorld);
        var volumes = new Volume[OpenWorldSpecs.Length];
        for (int index = 0; index < OpenWorldSpecs.Length; index++)
        {
            volumes[index] = EnsureVolume(
                environmentRoot,
                OpenWorldSpecs[index],
                openWorld,
                index == 0 ? 1f : 0f);
        }

        CampaignEnvironmentTransitionController controller =
            environmentRoot.GetComponent<CampaignEnvironmentTransitionController>();
        if (controller == null)
        {
            controller = environmentRoot.gameObject.AddComponent<
                CampaignEnvironmentTransitionController>();
            EditorSceneManager.MarkSceneDirty(openWorld);
        }

        CampaignStateService state =
            RequireSingleSceneComponent<CampaignStateService>(openWorld);
        CampaignRegionalRespawn respawn =
            RequireSingleSceneComponent<CampaignRegionalRespawn>(openWorld);
        Light sun = RequireDirectionalLight(openWorld);
        var bindings = new CampaignAreaEnvironmentBinding[AreaOrder.Length];
        for (int index = 0; index < bindings.Length; index++)
        {
            CampaignRegionalRespawnMapping mapping =
                respawn.RegionMappings[index];
            Require(mapping.Area == AreaOrder[index] &&
                    mapping.RespawnSocket != null,
                "Regional respawn anchor order drifted.");
            bindings[index] = new CampaignAreaEnvironmentBinding(
                AreaOrder[index],
                mapping.RespawnSocket,
                CreatePreset(OpenWorldSpecs[index], volumes[index]));
        }

        if (!OpenWorldControllerMatches(controller, state, respawn, sun))
        {
            controller.ConfigureOpenWorld(
                state, respawn, sun, bindings, TransitionSeconds,
                AreaPollSeconds, AreaSwitchHysteresis);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(openWorld);
        }

        ApplyPreview(openWorld, BlackPines, sun);
    }

    private static Transform EnsureEnvironmentRoot(
        Transform parent,
        Scene scene)
    {
        Transform[] matches = DirectChildren(parent, EnvironmentRootName);
        Require(matches.Length <= 1,
            "Duplicate campaign environment roots exist in " + scene.name);
        Transform root;
        if (matches.Length == 0)
        {
            root = new GameObject(EnvironmentRootName).transform;
            root.SetParent(parent, false);
            EditorSceneManager.MarkSceneDirty(scene);
        }
        else
        {
            root = matches[0];
        }

        Require(root.gameObject.activeSelf && parent.gameObject.activeInHierarchy,
            "Campaign environment root must be always active.");
        Require(root.GetComponents<Component>().All(component =>
                component is Transform ||
                component is CampaignEnvironmentTransitionController),
            "Campaign environment root contains an unowned component.");
        if (!VectorApproximately(root.localPosition, Vector3.zero) ||
            !QuaternionApproximately(root.localRotation, Quaternion.identity) ||
            !VectorApproximately(root.localScale, Vector3.one))
        {
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;
            EditorSceneManager.MarkSceneDirty(scene);
        }

        return root;
    }

    private static Volume EnsureVolume(
        Transform root,
        EnvironmentSpec spec,
        Scene scene,
        float weight)
    {
        Transform[] matches = DirectChildren(root, spec.VolumeName);
        Require(matches.Length <= 1,
            "Duplicate owned Volume: " + spec.VolumeName);
        Transform child;
        if (matches.Length == 0)
        {
            child = new GameObject(spec.VolumeName).transform;
            child.SetParent(root, false);
            EditorSceneManager.MarkSceneDirty(scene);
        }
        else
        {
            child = matches[0];
        }

        Require(child.gameObject.activeSelf &&
                child.GetComponents<Component>().All(component =>
                    component is Transform || component is Volume),
            "Owned Volume contains an unrecognized component: " +
            spec.VolumeName);
        Volume[] volumes = child.GetComponents<Volume>();
        Require(volumes.Length <= 1,
            "Owned environment object contains duplicate Volumes.");
        Volume volume = volumes.Length == 1
            ? volumes[0]
            : child.gameObject.AddComponent<Volume>();
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(
            spec.ProfilePath);
        bool changed = volumes.Length == 0 || !volume.isGlobal ||
                       !Nearly(volume.priority, VolumePriority) ||
                       !Nearly(volume.weight, weight) ||
                       !Nearly(volume.blendDistance, 0f) ||
                       volume.sharedProfile != profile;
        if (changed)
        {
            volume.isGlobal = true;
            volume.priority = VolumePriority;
            volume.weight = weight;
            volume.blendDistance = 0f;
            volume.sharedProfile = profile;
            EditorUtility.SetDirty(volume);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        return volume;
    }

    private static CampaignEnvironmentPreset CreatePreset(
        EnvironmentSpec spec,
        Volume volume)
    {
        return new CampaignEnvironmentPreset(
            spec.Id,
            AssetDatabase.LoadAssetAtPath<Material>(spec.MaterialPath),
            volume,
            spec.FogColor,
            spec.FogDensity,
            spec.AmbientSky,
            spec.AmbientEquator,
            spec.AmbientGround,
            spec.AmbientIntensity,
            spec.ReflectionIntensity,
            spec.SunColor,
            spec.SunIntensity,
            spec.SunEuler);
    }

    private static bool FarmControllerMatches(
        CampaignEnvironmentTransitionController controller,
        CampaignStateService state,
        FarmPrologueDirector director,
        Light sun)
    {
        return controller.Mode == CampaignEnvironmentMode.Farm &&
               controller.CampaignState == state &&
               controller.FarmPrologueDirector == director &&
               controller.RegionalRespawn == null &&
               controller.DirectionalLight == sun &&
               controller.FarmProloguePreset?.Id == FarmPrologue.Id &&
               controller.FarmHubPreset?.Id == FarmHub.Id &&
               controller.AreaBindings.Count == 0 &&
               Nearly(controller.TransitionSeconds, TransitionSeconds) &&
               controller.ValidateRuntimeContract(out _);
    }

    private static bool OpenWorldControllerMatches(
        CampaignEnvironmentTransitionController controller,
        CampaignStateService state,
        CampaignRegionalRespawn respawn,
        Light sun)
    {
        if (controller.Mode != CampaignEnvironmentMode.OpenWorld ||
            controller.CampaignState != state ||
            controller.FarmPrologueDirector != null ||
            controller.RegionalRespawn != respawn ||
            controller.DirectionalLight != sun ||
            controller.AreaBindings.Count != AreaOrder.Length ||
            !Nearly(controller.TransitionSeconds, TransitionSeconds) ||
            !Nearly(controller.AreaPollSeconds, AreaPollSeconds) ||
            !Nearly(controller.AreaSwitchHysteresis,
                AreaSwitchHysteresis) ||
            !controller.ValidateRuntimeContract(out _))
        {
            return false;
        }

        for (int index = 0; index < AreaOrder.Length; index++)
        {
            CampaignAreaEnvironmentBinding binding =
                controller.AreaBindings[index];
            if (binding.Area != AreaOrder[index] ||
                binding.Anchor != respawn.RegionMappings[index].RespawnSocket ||
                binding.Preset?.Id != OpenWorldSpecs[index].Id)
            {
                return false;
            }
        }

        return true;
    }

    private static void ApplyPreview(
        Scene scene,
        EnvironmentSpec spec,
        Light sun)
    {
        Scene authoredScene = SceneManager.GetSceneByPath(scene.path);
        Require(authoredScene.IsValid() && authoredScene.isLoaded,
            "RenderSettings scene is not loaded: " + scene.path);
        Scene previous = SceneManager.GetActiveScene();
        if (previous != authoredScene)
        {
            Require(SceneManager.SetActiveScene(authoredScene),
                "Could not activate scene for RenderSettings: " +
                authoredScene.name);
        }
        Material material = AssetDatabase.LoadAssetAtPath<Material>(
            spec.MaterialPath);
        bool changed = RenderSettings.skybox != material ||
                       !RenderSettings.fog ||
                       RenderSettings.fogMode != FogMode.ExponentialSquared ||
                       !ColorApproximately(RenderSettings.fogColor,
                           spec.FogColor) ||
                       !Nearly(RenderSettings.fogDensity, spec.FogDensity) ||
                       RenderSettings.ambientMode != AmbientMode.Trilight ||
                       !ColorApproximately(RenderSettings.ambientSkyColor,
                           spec.AmbientSky) ||
                       !ColorApproximately(RenderSettings.ambientEquatorColor,
                           spec.AmbientEquator) ||
                       !ColorApproximately(RenderSettings.ambientGroundColor,
                           spec.AmbientGround) ||
                       !Nearly(RenderSettings.ambientIntensity,
                           spec.AmbientIntensity) ||
                       !Nearly(RenderSettings.reflectionIntensity,
                           spec.ReflectionIntensity) ||
                       RenderSettings.sun != sun ||
                       !ColorApproximately(sun.color, spec.SunColor) ||
                       !Nearly(sun.intensity, spec.SunIntensity) ||
                       !QuaternionApproximately(sun.transform.rotation,
                           Quaternion.Euler(spec.SunEuler));
        if (changed)
        {
            RenderSettings.skybox = material;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = spec.FogColor;
            RenderSettings.fogDensity = spec.FogDensity;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = spec.AmbientSky;
            RenderSettings.ambientEquatorColor = spec.AmbientEquator;
            RenderSettings.ambientGroundColor = spec.AmbientGround;
            RenderSettings.ambientIntensity = spec.AmbientIntensity;
            RenderSettings.reflectionIntensity = spec.ReflectionIntensity;
            RenderSettings.sun = sun;
            sun.color = spec.SunColor;
            sun.intensity = spec.SunIntensity;
            sun.transform.rotation = Quaternion.Euler(spec.SunEuler);
            EditorUtility.SetDirty(sun);
            EditorSceneManager.MarkSceneDirty(authoredScene);
        }

        if (previous.IsValid() && previous.isLoaded &&
            previous != authoredScene)
            SceneManager.SetActiveScene(previous);
    }

    private static void ValidateEnvironmentAssets()
    {
        Require(AssetDatabase.IsValidFolder(MaterialFolder) &&
                AssetDatabase.IsValidFolder(ProfileFolder),
            "Campaign environment asset folders are missing.");
        var materials = new HashSet<Material>();
        var profiles = new HashSet<VolumeProfile>();
        foreach (EnvironmentSpec spec in AllSpecs)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                spec.MaterialPath);
            Require(material != null && materials.Add(material) &&
                    material.shader != null &&
                    material.shader.name == "Skybox/Procedural" &&
                    MaterialFloat(material, "_SunDisk", 2f) &&
                    MaterialFloat(material, "_SunSize", spec.SunSize) &&
                    MaterialFloat(material, "_SunSizeConvergence", 5f) &&
                    MaterialFloat(material, "_AtmosphereThickness",
                        spec.Atmosphere) &&
                    MaterialColor(material, "_SkyTint", spec.SkyTint) &&
                    MaterialColor(material, "_GroundColor",
                        spec.GroundColor) &&
                    MaterialFloat(material, "_Exposure", spec.Exposure),
                "Owned skybox is missing or drifted: " + spec.MaterialPath);

            VolumeProfile profile =
                AssetDatabase.LoadAssetAtPath<VolumeProfile>(spec.ProfilePath);
            Require(profile != null && profiles.Add(profile) &&
                    profile.components.Count == 4 &&
                    profile.components[0] is ColorAdjustments &&
                    profile.components[1] is WhiteBalance &&
                    profile.components[2] is Vignette &&
                    profile.components[3] is Bloom,
                "Owned VolumeProfile membership drifted: " + spec.ProfilePath);
            Require(profile.TryGet(out ColorAdjustments color) && color.active &&
                    Parameter(color.postExposure, spec.PostExposure) &&
                    Parameter(color.contrast, spec.Contrast) &&
                    Parameter(color.saturation, spec.Saturation) &&
                    Parameter(color.hueShift, 0f) &&
                    color.colorFilter.overrideState &&
                    ColorApproximately(color.colorFilter.value,
                        spec.ColorFilter),
                "Color Adjustments drifted: " + spec.ProfilePath);
            Require(profile.TryGet(out WhiteBalance white) && white.active &&
                    Parameter(white.temperature, spec.Temperature) &&
                    Parameter(white.tint, spec.Tint),
                "White Balance drifted: " + spec.ProfilePath);
            Require(profile.TryGet(out Vignette vignette) && vignette.active &&
                    vignette.color.overrideState &&
                    ColorApproximately(vignette.color.value,
                        spec.VignetteColor) &&
                    Parameter(vignette.intensity, spec.VignetteIntensity) &&
                    Parameter(vignette.smoothness, spec.VignetteSmoothness),
                "Vignette drifted: " + spec.ProfilePath);
            Require(profile.TryGet(out Bloom bloom) && bloom.active &&
                    Parameter(bloom.threshold, spec.BloomThreshold) &&
                    Parameter(bloom.intensity, spec.BloomIntensity) &&
                    Parameter(bloom.scatter, spec.BloomScatter),
                "Bloom drifted: " + spec.ProfilePath);
        }
    }

    private static void ValidateCampaignPrefabs()
    {
        GameObject boar = RequireExactAsset<GameObject>(
            BoarPath, BoarGuid, "regular Boar");
        RuntimeAnimatorController controller =
            RequireExactAsset<RuntimeAnimatorController>(
                JuggernautControllerPath,
                JuggernautControllerGuid,
                "Juggernaut controller");
        GameObject root = PrefabUtility.LoadPrefabContents(
            JuggernautVariantPath);
        try
        {
            juggernautEnemyAI ai = RequireSingle(
                root.GetComponentsInChildren<juggernautEnemyAI>(true),
                "campaign Juggernaut AI");
            Animator animator = RequireSingle(
                root.GetComponentsInChildren<Animator>(true),
                "campaign Juggernaut Animator");
            SerializedProperty reference =
                new SerializedObject(ai).FindProperty("animator");
            Require(ai.enabled &&
                    PrefabUtility.GetCorrespondingObjectFromSource(ai) != null &&
                    PrefabUtility.GetCorrespondingObjectFromSource(animator) != null &&
                    animator.runtimeAnimatorController == controller &&
                    reference?.objectReferenceValue == animator,
                "Campaign Juggernaut compatibility wiring drifted.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        foreach (string path in new[] { WitchSummonerPath, WitchMatriarchPath })
        {
            root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                WitchController witch = RequireSingle(
                    root.GetComponentsInChildren<WitchController>(true),
                    "witch controller");
                SerializedProperty minions =
                    new SerializedObject(witch).FindProperty("minionPrefabs");
                Require(ArrayMatches(minions, boar),
                    "Witch must summon the exact regular Boar: " + path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    private static void ValidateFarm(Scene farm)
    {
        ValidateFarmRoster(farm, allowPrevious: false);
        Transform root = RequireEnvironmentHierarchy(
            farm,
            FarmEnvironmentParentPath,
            new[] { FarmPrologue, FarmHub },
            new[] { 1f, 0f });
        CampaignEnvironmentTransitionController controller =
            RequireSingle(root.GetComponents<
                    CampaignEnvironmentTransitionController>(),
                "Farm environment controller");
        Require(FarmControllerMatches(
                controller,
                RequireSingleSceneComponent<CampaignStateService>(farm),
                RequireSingleSceneComponent<FarmPrologueDirector>(farm),
                RequireDirectionalLight(farm)),
            "Farm environment controller contract drifted.");
        ValidatePreview(farm, FarmPrologue, RequireDirectionalLight(farm));
    }

    private static void ValidateOpenWorld(Scene openWorld)
    {
        ValidateArrivalRosters(openWorld, allowPrevious: false);
        Transform root = RequireEnvironmentHierarchy(
            openWorld,
            OpenWorldEnvironmentParentPath,
            OpenWorldSpecs,
            new[] { 1f, 0f, 0f, 0f });
        CampaignEnvironmentTransitionController controller =
            RequireSingle(root.GetComponents<
                    CampaignEnvironmentTransitionController>(),
                "Open World environment controller");
        Require(OpenWorldControllerMatches(
                controller,
                RequireSingleSceneComponent<CampaignStateService>(openWorld),
                RequireSingleSceneComponent<CampaignRegionalRespawn>(openWorld),
                RequireDirectionalLight(openWorld)),
            "Open World environment controller contract drifted.");
        ValidatePreview(openWorld, BlackPines, RequireDirectionalLight(openWorld));
    }

    private static void ValidateFarmRoster(Scene farm, bool allowPrevious)
    {
        GameObject boar = RequireExactAsset<GameObject>(
            BoarPath, BoarGuid, "regular Boar");
        GameObject rootBoar = RequireExactAsset<GameObject>(
            BoarRootPath, BoarRootGuid, "Root Boar");
        GameObject jug = RequireExactAsset<GameObject>(
            JuggernautVariantPath, JuggernautVariantGuid, "Juggernaut");
        GameObject oldHog = RequireExactAsset<GameObject>(
            RegularHogPath, RegularHogGuid, "retired regular hog");
        MobSpawner spawner = RequireSingleSceneComponent<MobSpawner>(farm);
        SerializedObject data = new(spawner);
        SerializedProperty fallback = RequireProperty(data, "Enemy");
        SerializedProperty roster = RequireProperty(data, "enemies");
        SerializedProperty regularPig = RequireProperty(data, "regularPig");
        bool target = fallback.objectReferenceValue == boar &&
                      ArrayMatches(roster, boar, rootBoar, jug) &&
                      regularPig.objectReferenceValue == null;
        bool previous = fallback.objectReferenceValue == boar &&
                        ArrayMatches(roster, boar, boar, rootBoar) &&
                        (regularPig.objectReferenceValue == null ||
                         regularPig.objectReferenceValue == oldHog);
        Require(target || (allowPrevious && previous),
            "Farm MobSpawner roster is neither exact previous nor target.");

        SerializedProperty intro = RequireProperty(
            new SerializedObject(
                RequireSingleSceneComponent<waveManager>(farm)),
            "useHogHuntIntro");
        Require(allowPrevious || !intro.boolValue,
            "Farm hog intro must remain disabled.");
        SerializedProperty recurring = RequireProperty(
            new SerializedObject(RequireSingleSceneComponent<
                FarmRecurringEmergenceDirector>(farm)),
            "enemyPrefabs");
        Require(ArrayMatches(recurring, boar, rootBoar, jug) ||
                (allowPrevious && ArrayMatches(
                    recurring, boar, boar, rootBoar)),
            "Farm recurring enemy roster is unrecognized.");
    }

    private static void ValidateArrivalRosters(
        Scene scene,
        bool allowPrevious)
    {
        GameObject boar = RequireExactAsset<GameObject>(
            BoarPath, BoarGuid, "regular Boar");
        GameObject rootBoar = RequireExactAsset<GameObject>(
            BoarRootPath, BoarRootGuid, "Root Boar");
        GameObject jug = RequireExactAsset<GameObject>(
            JuggernautVariantPath, JuggernautVariantGuid, "Juggernaut");
        WorldArrivalEnemySpawner[] spawners = ResolveArrivalSpawners(scene);
        Require(SpawnerRosterMatches(spawners[0], new[] { boar, rootBoar }),
            "Black Pines arrival roster drifted.");
        Require(SpawnerRosterMatches(spawners[1], new[] { boar, jug }) ||
                (allowPrevious && SpawnerRosterMatches(
                    spawners[1], new[] { boar, boar })),
            "Stillwater arrival roster is unrecognized.");
        Require(SpawnerRosterMatches(spawners[2], new[] { jug }) ||
                (allowPrevious && SpawnerRosterMatches(
                    spawners[2], new[] { rootBoar })),
            "Harrow arrival roster is unrecognized.");
    }

    private static Transform RequireEnvironmentHierarchy(
        Scene scene,
        string parentPath,
        IReadOnlyList<EnvironmentSpec> specs,
        IReadOnlyList<float> weights)
    {
        Transform parent = RequirePath(scene, parentPath);
        Transform root = RequireSingle(
            DirectChildren(parent, EnvironmentRootName),
            scene.name + " environment root");
        Require(root.gameObject.activeSelf &&
                root.GetComponents<Component>().All(component =>
                    component is Transform ||
                    component is CampaignEnvironmentTransitionController) &&
                root.childCount == specs.Count,
            scene.name + " environment hierarchy drifted.");
        for (int index = 0; index < specs.Count; index++)
        {
            EnvironmentSpec spec = specs[index];
            Transform child = RequireSingle(
                DirectChildren(root, spec.VolumeName), spec.VolumeName);
            Volume volume = RequireSingle(
                child.GetComponents<Volume>(), spec.VolumeName + " component");
            Require(child.gameObject.activeSelf &&
                    child.GetComponents<Component>().Length == 2 &&
                    volume.isGlobal &&
                    Nearly(volume.priority, VolumePriority) &&
                    Nearly(volume.weight, weights[index]) &&
                    Nearly(volume.blendDistance, 0f) &&
                    AssetDatabase.GetAssetPath(volume.sharedProfile) ==
                    spec.ProfilePath,
                spec.VolumeName + " settings drifted.");
        }

        return root;
    }

    private static void ValidatePreview(
        Scene scene,
        EnvironmentSpec spec,
        Light sun)
    {
        Scene previous = SceneManager.GetActiveScene();
        if (previous != scene)
        {
            Require(SceneManager.SetActiveScene(scene),
                "Could not activate scene for preview validation.");
        }
        Require(RenderSettings.skybox ==
                    AssetDatabase.LoadAssetAtPath<Material>(spec.MaterialPath) &&
                RenderSettings.fog &&
                RenderSettings.fogMode == FogMode.ExponentialSquared &&
                ColorApproximately(RenderSettings.fogColor, spec.FogColor) &&
                Nearly(RenderSettings.fogDensity, spec.FogDensity) &&
                RenderSettings.ambientMode == AmbientMode.Trilight &&
                ColorApproximately(RenderSettings.ambientSkyColor,
                    spec.AmbientSky) &&
                ColorApproximately(RenderSettings.ambientEquatorColor,
                    spec.AmbientEquator) &&
                ColorApproximately(RenderSettings.ambientGroundColor,
                    spec.AmbientGround) &&
                Nearly(RenderSettings.ambientIntensity,
                    spec.AmbientIntensity) &&
                Nearly(RenderSettings.reflectionIntensity,
                    spec.ReflectionIntensity) &&
                RenderSettings.sun == sun &&
                ColorApproximately(sun.color, spec.SunColor) &&
                Nearly(sun.intensity, spec.SunIntensity) &&
                QuaternionApproximately(sun.transform.rotation,
                    Quaternion.Euler(spec.SunEuler)),
            scene.name + " authored environment preview drifted.");
        if (previous.IsValid() && previous.isLoaded && previous != scene)
            SceneManager.SetActiveScene(previous);
    }

    private static WorldArrivalEnemySpawner[] ResolveArrivalSpawners(
        Scene scene)
    {
        WorldArrivalEnemySpawner[] all =
            FindSceneComponents<WorldArrivalEnemySpawner>(scene);
        Require(all.Length == ArrivalEncounterNames.Length,
            "Open World must contain exactly three arrival spawners.");
        var ordered = new WorldArrivalEnemySpawner[ArrivalEncounterNames.Length];
        for (int index = 0; index < ArrivalEncounterNames.Length; index++)
        {
            ordered[index] = RequireSingle(
                all.Where(spawner => HasAncestor(
                        spawner.transform, ArrivalEncounterNames[index]))
                    .ToArray(),
                ArrivalEncounterNames[index] + " spawner");
        }

        return ordered;
    }

    private static bool SpawnerRosterMatches(
        WorldArrivalEnemySpawner spawner,
        IReadOnlyList<GameObject> roster)
    {
        if (spawner == null || spawner.Spawns.Count != roster.Count)
            return false;
        for (int index = 0; index < roster.Count; index++)
        {
            if (spawner.Spawns[index] == null ||
                spawner.Spawns[index].EnemyPrefab != roster[index] ||
                spawner.Spawns[index].SpawnPoint == null)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasAncestor(Transform transform, string name)
    {
        for (Transform current = transform;
             current != null;
             current = current.parent)
        {
            if (current.name == name)
                return true;
        }

        return false;
    }

    private static Light RequireDirectionalLight(Scene scene)
    {
        return RequireSingle(
            FindSceneComponents<Light>(scene)
                .Where(light => light.type == LightType.Directional)
                .ToArray(),
            scene.name + " directional light");
    }

    private static T RequireSingleSceneComponent<T>(Scene scene)
        where T : Component
    {
        return RequireSingle(
            FindSceneComponents<T>(scene),
            scene.name + " " + typeof(T).Name);
    }

    private static T[] FindSceneComponents<T>(Scene scene)
        where T : Component
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true))
            .Where(component => component != null &&
                                component.gameObject.scene == scene)
            .ToArray();
    }

    private static Transform RequirePath(Scene scene, string path)
    {
        string[] parts = path.Split('/');
        Transform[] roots = scene.GetRootGameObjects()
            .Where(root => root.name == parts[0])
            .Select(root => root.transform)
            .ToArray();
        Transform current = RequireSingle(roots, path + " root");
        for (int index = 1; index < parts.Length; index++)
        {
            current = RequireSingle(
                DirectChildren(current, parts[index]), path);
        }

        return current;
    }

    private static Transform[] DirectChildren(Transform parent, string name)
    {
        var results = new List<Transform>();
        for (int index = 0; index < parent.childCount; index++)
        {
            Transform child = parent.GetChild(index);
            if (child.name == name)
                results.Add(child);
        }

        return results.ToArray();
    }

    private static T RequireSingle<T>(T[] values, string label)
    {
        Require(values != null && values.Length == 1,
            $"Expected exactly one {label}; found {values?.Length ?? 0}.");
        return values[0];
    }

    private static T RequireExactAsset<T>(
        string path,
        string guid,
        string label)
        where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        Require(asset != null &&
                string.Equals(AssetDatabase.AssetPathToGUID(path), guid,
                    StringComparison.OrdinalIgnoreCase),
            $"Missing or replaced {label} at '{path}'.");
        return asset;
    }

    private static SerializedProperty RequireProperty(
        SerializedObject serialized,
        string name)
    {
        SerializedProperty property = serialized.FindProperty(name);
        Require(property != null,
            serialized.targetObject.name + " is missing serialized field " +
            name + ".");
        return property;
    }

    private static bool ArrayMatches(
        SerializedProperty array,
        params UnityEngine.Object[] expected)
    {
        if (array == null || !array.isArray || array.arraySize != expected.Length)
            return false;
        for (int index = 0; index < expected.Length; index++)
        {
            if (array.GetArrayElementAtIndex(index).objectReferenceValue !=
                expected[index])
            {
                return false;
            }
        }

        return true;
    }

    private static void SetArray(
        SerializedProperty array,
        params UnityEngine.Object[] values)
    {
        Require(array != null && array.isArray,
            "Expected an object-reference array.");
        array.arraySize = values.Length;
        for (int index = 0; index < values.Length; index++)
            array.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
    }

    private static bool SetFloat(Material material, string name, float value)
    {
        Require(material.HasProperty(name),
            material.name + " lacks shader property " + name);
        if (Nearly(material.GetFloat(name), value))
            return false;
        material.SetFloat(name, value);
        return true;
    }

    private static bool SetColor(Material material, string name, Color value)
    {
        Require(material.HasProperty(name),
            material.name + " lacks shader property " + name);
        if (ColorApproximately(material.GetColor(name), value))
            return false;
        material.SetColor(name, value);
        return true;
    }

    private static bool MaterialFloat(Material material, string name, float value)
    {
        return material.HasProperty(name) &&
               Nearly(material.GetFloat(name), value);
    }

    private static bool MaterialColor(Material material, string name, Color value)
    {
        return material.HasProperty(name) &&
               ColorApproximately(material.GetColor(name), value);
    }

    private static bool Parameter(
        VolumeParameter<float> parameter,
        float value)
    {
        return parameter.overrideState && Nearly(parameter.value, value);
    }

    private static bool Nearly(float left, float right)
    {
        return Mathf.Abs(left - right) <= FloatTolerance;
    }

    private static bool ColorApproximately(Color left, Color right)
    {
        return Nearly(left.r, right.r) && Nearly(left.g, right.g) &&
               Nearly(left.b, right.b) && Nearly(left.a, right.a);
    }

    private static bool VectorApproximately(Vector3 left, Vector3 right)
    {
        return (left - right).sqrMagnitude <=
               FloatTolerance * FloatTolerance;
    }

    private static bool QuaternionApproximately(
        Quaternion left,
        Quaternion right)
    {
        return Mathf.Abs(Quaternion.Dot(left, right)) >= 0.99999f;
    }

    private static string ToAbsolutePath(string assetPath)
    {
        return Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", assetPath));
    }

    private static void RestoreSceneSetup(SceneSetup[] setup)
    {
        try
        {
            EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "Could not restore the prior scene setup: " +
                exception.Message);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
