using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bloodroot.Features.Infection;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class BloodrootBlackPinesAtmosphereProduction
{
    private const string OpenWorldScene =
        "Assets/Scenes/OpenWorld/Bloodroot_OpenWorld.unity";
    private const string ProductionTerrainPath =
        "Assets/Scenes/OpenWorld/Data/Bloodroot_OpenWorld_Terrain_Production.asset";
    private const string BackupScenePath =
        "Assets/Scenes/OpenWorld/Backups/Bloodroot_OpenWorld_PreBlackPinesAtmosphere.unity";
    private const string BackupTerrainPath =
        "Assets/Scenes/OpenWorld/Backups/Bloodroot_OpenWorld_Terrain_PreBlackPinesAtmosphere.asset";
    private const string TerrainRepairTempPath =
        "Assets/Scenes/OpenWorld/Data/Bloodroot_OpenWorld_Terrain_BlackPinesAtmosphereRepairTemp.asset";

    private const string MaterialFolder =
        "Assets/Materials/OpenWorld/BlackPinesAtmosphere";
    private const string PrefabFolder =
        "Assets/PreFabs/OpenWorld/BlackPinesAtmosphere";
    private const string VfxFolder =
        "Assets/VFX/OpenWorld/BlackPinesAtmosphere";

    private const string ProfilePath =
        VfxFolder + "/BlackPinesAtmosphereProfile.asset";
    private const string FogTexturePath =
        VfxFolder + "/BlackPinesFogSoftNoise.asset";
    private const string FogMaterialPath =
        MaterialFolder + "/BlackPinesLowFog.mat";
    private const string PatchPrefabPath =
        PrefabFolder + "/BlackPinesInfectedPatch.prefab";

    private const string CorruptedGroundSourcePath =
        "Assets/Materials/Features/Infection/BloodrootCorruptedGround.mat";
    private const string RootsSourcePath =
        "Assets/Materials/Features/Infection/BloodrootRoots.mat";
    private const string CoreSourcePath =
        "Assets/Materials/Features/Infection/BloodrootCore.mat";
    private const string ParticlesSourcePath =
        "Assets/Materials/Features/Infection/BloodrootParticles.mat";

    private const string CorruptedGroundMaterialPath =
        MaterialFolder + "/BlackPinesCorruptedGround.mat";
    private const string RootsMaterialPath =
        MaterialFolder + "/BlackPinesInfectedRoots.mat";
    private const string CoreMaterialPath =
        MaterialFolder + "/BlackPinesInfectedCore.mat";
    private const string InfectionParticlesMaterialPath =
        MaterialFolder + "/BlackPinesInfectionParticles.mat";

    private const string AreaRootName = "AREA_00_BLACK_PINES_FOREST";
    private const string EnvironmentRootName = "Environment";
    private const string AtmosphereRootName = "Generated Black Pines Atmosphere";
    private const string VolumeObjectName = "Local Haunted Grade";
    private const string FogRootName = "Low-Hanging Fog";
    private const string PatchRootName = "Infected Patches";
    private const string TreeRootName = "Infected Trees";
    private const string SignaturePrefix = "_BLACK_PINES_ATMOSPHERE_SIGNATURE_";
    private const string LegacyAtmosphereSignature =
        "_BLACK_PINES_ATMOSPHERE_SIGNATURE_8497FB7928DBAB79";
    private const string NaturalDressingSignature = "_PLACEMENT_SIGNATURE_76E974E673BC46E5";
    private const string OwnershipPrefix = "BloodrootBlackPinesAtmosphereProduction|v1|";
    private const int FogParticleBudgetCeiling = 270;
    private const float PatchApronDiameter = 17f;
    private const float PatchApronThicknessScale = 0.035f;
    private const float PatchRootSizeMultiplier = 1.10f;
    private const float PatchMistRadius = 4f;
    private const float PatchMistRate = 3.5f;
    private const int PatchMistMaximum = 20;

    private static readonly Vector3 VolumeCenter = new Vector3(-240f, 25f, -175f);
    private static readonly Vector3 VolumeSize = new Vector3(950f, 110f, 720f);
    private static readonly Vector2 BlackPinesFogCenter = new Vector2(-330f, -190f);
    private static readonly Vector2 BlackPinesFogRadii = new Vector2(420f, 360f);
    private static readonly Vector3 PatchTriggerCenter = new Vector3(0f, 2f, 0f);
    private static readonly Vector3 PatchTriggerSize = new Vector3(12f, 4f, 12f);
    private static readonly string[] LegacyFogFieldNames =
    {
        "Fog Creek West",
        "Fog Creek Middle",
        "Fog Creek East",
        "Fog Deep Grove",
        "Fog East Grove"
    };

    private static readonly string[] SourceTreePrefabPaths =
    {
        "Assets/PreFabs/OpenWorld/NaturalDressing/Fir_Terrain.prefab",
        "Assets/PreFabs/OpenWorld/NaturalDressing/SM_Tree_01_Terrain.prefab",
        "Assets/PreFabs/OpenWorld/NaturalDressing/SM_Tree_02_Terrain.prefab"
    };

    private static readonly string[] InfectedTreePrefabPaths =
    {
        PrefabFolder + "/Infected_Fir_Terrain.prefab",
        PrefabFolder + "/Infected_SM_Tree_01_Terrain.prefab",
        PrefabFolder + "/Infected_SM_Tree_02_Terrain.prefab"
    };

    private readonly struct FogFieldSpec
    {
        public readonly string Name;
        public readonly Vector2 Point;
        public readonly Vector3 Size;
        public readonly uint Seed;
        public readonly float Rate;
        public readonly int Maximum;
        public readonly bool BaseCoverage;

        public FogFieldSpec(
            string name,
            Vector2 point,
            Vector3 size,
            uint seed,
            float rate,
            int maximum,
            bool baseCoverage)
        {
            Name = name;
            Point = point;
            Size = size;
            Seed = seed;
            Rate = rate;
            Maximum = maximum;
            BaseCoverage = baseCoverage;
        }
    }

    private readonly struct PatchSpec
    {
        public readonly string Name;
        public readonly Vector2 Point;
        public readonly bool Mechanical;
        public readonly float Yaw;

        public PatchSpec(string name, Vector2 point, bool mechanical, float yaw)
        {
            Name = name;
            Point = point;
            Mechanical = mechanical;
            Yaw = yaw;
        }
    }

    private readonly struct TreePlacementSpec
    {
        public readonly string Name;
        public readonly int PrefabIndex;
        public readonly Vector2 Point;
        public readonly float Scale;
        public readonly float Yaw;

        public TreePlacementSpec(
            string name,
            int prefabIndex,
            Vector2 point,
            float scale,
            float yaw)
        {
            Name = name;
            PrefabIndex = prefabIndex;
            Point = point;
            Scale = scale;
            Yaw = yaw;
        }
    }

    private sealed class GeneratedAssetSnapshot
    {
        public string AssetPath;
        public byte[] AssetBytes;
        public byte[] MetaBytes;
    }

    private static readonly FogFieldSpec[] FogFields =
    {
        // These five overlap the quieter base veil to create authored dense pockets.
        new FogFieldSpec("Fog Creek West", new Vector2(-500f, -335f), new Vector3(300f, 5f, 120f), 17011u, 0.86f, 32, false),
        new FogFieldSpec("Fog Creek Middle", new Vector2(-250f, -385f), new Vector3(280f, 5f, 110f), 17027u, 0.90f, 32, false),
        new FogFieldSpec("Fog Creek East", new Vector2(20f, -330f), new Vector3(260f, 4f, 110f), 17041u, 0.81f, 30, false),
        new FogFieldSpec("Fog Deep Grove", new Vector2(-250f, -245f), new Vector3(180f, 8f, 110f), 17053u, 0.84f, 30, false),
        new FogFieldSpec("Fog East Grove", new Vector2(60f, -175f), new Vector3(180f, 10f, 90f), 17077u, 0.77f, 28, false),

        // Nine low-density tiles overlap at their edges and cover the complete Black Pines biome.
        new FogFieldSpec("Fog Base South West", new Vector2(-560f, -430f), new Vector3(300f, 5f, 260f), 17101u, 0.24f, 10, true),
        new FogFieldSpec("Fog Base South Center", new Vector2(-300f, -430f), new Vector3(300f, 5f, 260f), 17117u, 0.36f, 16, true),
        new FogFieldSpec("Fog Base South East", new Vector2(-40f, -430f), new Vector3(280f, 5f, 260f), 17131u, 0.24f, 10, true),
        new FogFieldSpec("Fog Base Middle West", new Vector2(-560f, -190f), new Vector3(300f, 6f, 260f), 17147u, 0.32f, 14, true),
        new FogFieldSpec("Fog Base Middle Center", new Vector2(-300f, -190f), new Vector3(300f, 7f, 260f), 17159u, 0.42f, 18, true),
        new FogFieldSpec("Fog Base Middle East", new Vector2(-40f, -190f), new Vector3(280f, 6f, 260f), 17173u, 0.32f, 14, true),
        new FogFieldSpec("Fog Base North West", new Vector2(-560f, 50f), new Vector3(300f, 6f, 240f), 17189u, 0.24f, 10, true),
        new FogFieldSpec("Fog Base North Center", new Vector2(-300f, 50f), new Vector3(300f, 6f, 240f), 17203u, 0.36f, 16, true),
        new FogFieldSpec("Fog Base North East", new Vector2(-40f, 50f), new Vector3(280f, 6f, 240f), 17221u, 0.24f, 10, true)
    };

    private static readonly PatchSpec[] Patches =
    {
        new PatchSpec("INFECTED_PATCH_BP_00_FORESHADOW_VISUAL", new Vector2(-300f, -215f), false, 18f),
        new PatchSpec("INFECTED_PATCH_BP_01_WEST_CREEK", new Vector2(-425f, -335f), true, 74f),
        new PatchSpec("INFECTED_PATCH_BP_02_CREEK_OBJECTIVE", new Vector2(-175f, -315f), true, 137f),
        new PatchSpec("INFECTED_PATCH_BP_03_MID_ROUTE", new Vector2(-15f, -165f), true, 211f),
        new PatchSpec("INFECTED_PATCH_BP_04_PRE_GATE", new Vector2(145f, -125f), true, 286f)
    };

    private static readonly TreePlacementSpec[] InfectedTrees =
    {
        new TreePlacementSpec("INFECTED_TREE_BP_000", 0, new Vector2(-313f, -224f), 1.10f, 24f),
        new TreePlacementSpec("INFECTED_TREE_BP_001", 1, new Vector2(-290f, -228f), 0.96f, 181f),
        new TreePlacementSpec("INFECTED_TREE_BP_002", 2, new Vector2(-286f, -205f), 1.04f, 302f),
        new TreePlacementSpec("INFECTED_TREE_BP_003", 1, new Vector2(-440f, -343f), 1.08f, 67f),
        new TreePlacementSpec("INFECTED_TREE_BP_004", 0, new Vector2(-413f, -350f), 1.16f, 203f),
        new TreePlacementSpec("INFECTED_TREE_BP_005", 2, new Vector2(-408f, -326f), 0.93f, 318f),
        new TreePlacementSpec("INFECTED_TREE_BP_006", 0, new Vector2(-190f, -325f), 1.12f, 15f),
        new TreePlacementSpec("INFECTED_TREE_BP_007", 2, new Vector2(-164f, -330f), 0.98f, 146f),
        new TreePlacementSpec("INFECTED_TREE_BP_008", 1, new Vector2(-159f, -301f), 1.05f, 277f),
        new TreePlacementSpec("INFECTED_TREE_BP_009", 2, new Vector2(-31f, -175f), 1.02f, 38f),
        new TreePlacementSpec("INFECTED_TREE_BP_010", 0, new Vector2(-2f, -180f), 1.18f, 169f),
        new TreePlacementSpec("INFECTED_TREE_BP_011", 1, new Vector2(1f, -152f), 0.95f, 290f),
        new TreePlacementSpec("INFECTED_TREE_BP_012", 0, new Vector2(130f, -136f), 1.14f, 52f),
        new TreePlacementSpec("INFECTED_TREE_BP_013", 1, new Vector2(158f, -141f), 1.00f, 194f),
        new TreePlacementSpec("INFECTED_TREE_BP_014", 2, new Vector2(160f, -113f), 1.07f, 325f)
    };

    private static readonly Vector2[] ProtectedRoad =
    {
        new Vector2(-350f, -150f),
        new Vector2(-120f, -118f),
        new Vector2(85f, -78f),
        new Vector2(235f, -40f)
    };

    [MenuItem("Bloodroot/Open World/Build Black Pines Atmosphere", false, 50)]
    public static void BuildBlackPinesAtmosphere()
    {
        RunAtmosphereBuild(false);
    }

    [MenuItem("Bloodroot/Open World/Rebuild Black Pines Atmosphere", false, 51)]
    public static void RebuildBlackPinesAtmosphere()
    {
        RunAtmosphereBuild(true);
    }

    [MenuItem("Bloodroot/Open World/Validate Black Pines Atmosphere", false, 52)]
    public static void ValidateBlackPinesAtmosphere()
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
            ValidateAtmosphere(scene, terrain, true);
            EditorUtility.DisplayDialog(
                "Black Pines Atmosphere Valid",
                BuildValidationSummary(),
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Black Pines Atmosphere Validation Failed",
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

    private static void ValidateAtmosphere(Scene scene, Terrain terrain, bool requireNoRepairTemp)
    {
        if (scene.path != OpenWorldScene || !scene.IsValid() || !scene.isLoaded)
        {
            throw new InvalidOperationException("Black Pines atmosphere validation requires the live open-world scene.");
        }

        TerrainData data = terrain.terrainData;
        ValidateNaturalDressingBoundary(scene, data);
        if (EditorUtility.IsDirty(data))
        {
            throw new InvalidOperationException(
                "Production TerrainData is dirty during atmosphere validation. Atmosphere must never write TerrainData.");
        }

        Transform area = FindSceneTransform(scene, AreaRootName);
        Transform environment = area == null ? null : area.Find(EnvironmentRootName);
        Transform root = FindAtmosphereRoot(scene);
        if (area == null || environment == null || root == null || root.parent != environment)
        {
            throw new InvalidOperationException(
                "Generated Black Pines atmosphere is missing from AREA_00_BLACK_PINES_FOREST/Environment.");
        }

        ValidateIdentityTransform(root, "Black Pines atmosphere root");
        if (!root.gameObject.activeSelf)
        {
            throw new InvalidOperationException("Black Pines atmosphere root is disabled.");
        }
        string expectedSignature = ExpectedSignatureName();
        Transform[] signatures = root.Cast<Transform>()
            .Where(child => child.name.StartsWith(SignaturePrefix, StringComparison.Ordinal))
            .ToArray();
        if (signatures.Length != 1 || signatures[0].name != expectedSignature)
        {
            throw new InvalidOperationException(
                "Black Pines atmosphere semantic signature is missing, stale, or duplicated.");
        }

        string[] requiredRootChildren =
        {
            VolumeObjectName,
            FogRootName,
            PatchRootName,
            TreeRootName,
            expectedSignature
        };
        string[] actualRootChildren = root.Cast<Transform>().Select(child => child.name).ToArray();
        if (actualRootChildren.Length != requiredRootChildren.Length ||
            !actualRootChildren.SequenceEqual(requiredRootChildren))
        {
            throw new InvalidOperationException(
                "Black Pines atmosphere root children are missing, reordered, or contain unrecognized content.");
        }

        if (root.Cast<Transform>().Any(child => !child.gameObject.activeSelf))
        {
            throw new InvalidOperationException("One or more Black Pines atmosphere sections are disabled.");
        }

        ValidateLocalVolume(root.Find(VolumeObjectName));
        ValidateFogFields(root.Find(FogRootName), terrain);
        ValidatePatches(root.Find(PatchRootName), terrain);
        ValidateInfectedTrees(root.Find(TreeRootName), terrain);
        ValidateGeneratedAssets();
        ValidatePersistentBackupPair();

        if (requireNoRepairTemp && AssetOrMetaExists(TerrainRepairTempPath))
        {
            throw new InvalidOperationException(
                "A Black Pines atmosphere TerrainData repair temp remains after the operation.");
        }

        foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
        {
            if (entry.enabled && string.IsNullOrEmpty(entry.path))
            {
                throw new InvalidOperationException("Build Settings contains an enabled scene with an empty path.");
            }

            if (entry.enabled && AssetDatabase.LoadAssetAtPath<SceneAsset>(entry.path) == null)
            {
                throw new InvalidOperationException(
                    "Build Settings contains an enabled missing scene: " + entry.path);
            }
        }
    }

    private static void ValidateRecognizedAtmosphereForRebuild(
        Scene scene,
        Transform root,
        IReadOnlyDictionary<string, string> expectedAssets)
    {
        Transform area = FindSceneTransform(scene, AreaRootName);
        Transform environment = area == null ? null : area.Find(EnvironmentRootName);
        if (environment == null || root == null || root.parent != environment)
        {
            throw new InvalidOperationException(
                "Rebuild requires the owned Black Pines atmosphere under AREA_00_BLACK_PINES_FOREST/Environment.");
        }

        ValidateIdentityTransform(root, "Black Pines atmosphere root");
        string expectedSignature = ExpectedSignatureName();
        string[] expectedRootChildren =
        {
            VolumeObjectName,
            FogRootName,
            PatchRootName,
            TreeRootName,
            expectedSignature
        };
        string[] legacyRootChildren =
        {
            VolumeObjectName,
            FogRootName,
            PatchRootName,
            TreeRootName,
            LegacyAtmosphereSignature
        };
        string[] actualRootChildren = root.Cast<Transform>().Select(child => child.name).ToArray();
        bool currentRootLayout = actualRootChildren.SequenceEqual(expectedRootChildren);
        bool legacyRootLayout = actualRootChildren.SequenceEqual(legacyRootChildren);
        if (!currentRootLayout && !legacyRootLayout)
        {
            throw new InvalidOperationException(
                "Rebuild refused because the existing atmosphere hierarchy is partial, stale, reordered, or contains unrecognized content.");
        }

        Transform volume = root.Find(VolumeObjectName);
        Transform fogRoot = root.Find(FogRootName);
        Transform patchRoot = root.Find(PatchRootName);
        Transform treeRoot = root.Find(TreeRootName);
        if (volume == null || fogRoot == null || patchRoot == null || treeRoot == null)
        {
            throw new InvalidOperationException("Rebuild refused because an owned atmosphere section is missing.");
        }

        ValidateIdentityTransform(fogRoot, "Black Pines fog root");
        ValidateIdentityTransform(patchRoot, "Black Pines infected-patch root");
        ValidateIdentityTransform(treeRoot, "Black Pines infected-tree root");
        string[] actualFogNames = fogRoot.Cast<Transform>().Select(child => child.name).ToArray();
        bool currentFogLayout = actualFogNames.SequenceEqual(FogFields.Select(spec => spec.Name));
        bool legacyFogLayout = actualFogNames.SequenceEqual(LegacyFogFieldNames);
        bool recognizedFogSchema =
            (currentRootLayout && currentFogLayout) ||
            (legacyRootLayout && legacyFogLayout);
        if (!recognizedFogSchema ||
            !patchRoot.Cast<Transform>().Select(child => child.name)
                .SequenceEqual(Patches.Select(spec => spec.Name)) ||
            !treeRoot.Cast<Transform>().Select(child => child.name)
                .SequenceEqual(InfectedTrees.Select(spec => spec.Name)))
        {
            throw new InvalidOperationException(
                "Rebuild refused because generated fog, patch, or infected-tree membership is unrecognized.");
        }

        for (int index = 0; index < Patches.Length; index++)
        {
            GameObject patch = patchRoot.GetChild(index).gameObject;
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(patch) != PatchPrefabPath)
            {
                throw new InvalidOperationException(
                    "Rebuild refused because an infected patch points at an unrecognized prefab: " + patch.name);
            }
        }

        for (int index = 0; index < InfectedTrees.Length; index++)
        {
            GameObject tree = treeRoot.GetChild(index).gameObject;
            string expectedPath = InfectedTreePrefabPaths[InfectedTrees[index].PrefabIndex];
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(tree) != expectedPath)
            {
                throw new InvalidOperationException(
                    "Rebuild refused because an infected tree points at an unrecognized prefab: " + tree.name);
            }
        }

        foreach (KeyValuePair<string, string> pair in expectedAssets)
        {
            string assetFile = ProjectAbsolutePath(pair.Key);
            string metaFile = ProjectAbsolutePath(pair.Key + ".meta");
            AssetImporter importer = AssetImporter.GetAtPath(pair.Key);
            if (!File.Exists(assetFile) || !File.Exists(metaFile) ||
                AssetDatabase.LoadMainAssetAtPath(pair.Key) == null ||
                importer == null || importer.userData != pair.Value)
            {
                throw new InvalidOperationException(
                    "Rebuild refused because generated output is missing or not owned by this tool: " + pair.Key);
            }
        }

        ValidatePersistentBackupPair();
        if (AssetOrMetaExists(TerrainRepairTempPath))
        {
            throw new InvalidOperationException(
                "A Black Pines atmosphere TerrainData repair temp remains from a prior operation.");
        }
    }

    private static void ValidateLocalVolume(Transform transform)
    {
        if (transform == null)
        {
            throw new InvalidOperationException("Black Pines local haunted-grade object is missing.");
        }

        if (!transform.gameObject.activeSelf || transform.gameObject.layer != 0 ||
            Vector3.Distance(transform.position, VolumeCenter) > 0.01f ||
            Quaternion.Angle(transform.rotation, Quaternion.identity) > 0.01f ||
            Vector3.Distance(transform.localScale, Vector3.one) > 0.001f)
        {
            throw new InvalidOperationException(
                "Black Pines local haunted-grade transform or layer has drifted.");
        }

        BoxCollider[] colliders = transform.GetComponents<BoxCollider>();
        Volume[] volumes = transform.GetComponents<Volume>();
        NavMeshModifier[] modifiers = transform.GetComponents<NavMeshModifier>();
        if (colliders.Length != 1 || volumes.Length != 1 || modifiers.Length != 1)
        {
            throw new InvalidOperationException(
                "Black Pines local grade must have exactly one BoxCollider, Volume, and NavMeshModifier.");
        }

        BoxCollider collider = colliders[0];
        if (!collider.enabled || !collider.isTrigger ||
            Vector3.Distance(collider.center, Vector3.zero) > 0.001f ||
            Vector3.Distance(collider.size, VolumeSize) > 0.01f)
        {
            throw new InvalidOperationException("Black Pines local Volume collider settings have drifted.");
        }

        Volume volume = volumes[0];
        if (!volume.enabled || volume.isGlobal ||
            !Nearly(volume.priority, 20f) ||
            !Nearly(volume.blendDistance, 60f) ||
            !Nearly(volume.weight, 1f) ||
            AssetDatabase.GetAssetPath(volume.sharedProfile) != ProfilePath)
        {
            throw new InvalidOperationException("Black Pines local Volume settings or profile reference have drifted.");
        }

        NavMeshModifier modifier = modifiers[0];
        if (!modifier.enabled || !modifier.ignoreFromBuild || modifier.applyToChildren)
        {
            throw new InvalidOperationException(
                "Black Pines local Volume collider is not explicitly excluded from NavMesh baking.");
        }

        ValidateAtmosphereProfile(volume.sharedProfile);
    }

    private static void ValidateAtmosphereProfile(VolumeProfile profile)
    {
        if (profile == null || AssetDatabase.GetAssetPath(profile) != ProfilePath || profile.components.Count != 5)
        {
            throw new InvalidOperationException(
                "Black Pines atmosphere profile must contain exactly five owned overrides.");
        }

        Type[] types = profile.components.Select(component => component.GetType()).ToArray();
        Type[] expected =
        {
            typeof(ColorAdjustments),
            typeof(WhiteBalance),
            typeof(Vignette),
            typeof(FilmGrain),
            typeof(Bloom)
        };
        if (!types.SequenceEqual(expected))
        {
            throw new InvalidOperationException(
                "Black Pines atmosphere profile override order or membership has drifted.");
        }

        if (!profile.TryGet(out ColorAdjustments color) || !color.active ||
            !ParameterEquals(color.postExposure, -0.45f) ||
            !ParameterEquals(color.contrast, 16f) ||
            !ParameterEquals(color.saturation, -24f) ||
            !ParameterEquals(color.hueShift, -3f) ||
            !color.colorFilter.overrideState ||
            ColorDistance(color.colorFilter.value, new Color(0.78f, 0.84f, 0.79f, 1f)) > 0.002f)
        {
            throw new InvalidOperationException("Black Pines haunted color grade values have drifted.");
        }

        if (!profile.TryGet(out WhiteBalance white) || !white.active ||
            !ParameterEquals(white.temperature, -8f) || !ParameterEquals(white.tint, -6f))
        {
            throw new InvalidOperationException("Black Pines White Balance values have drifted.");
        }

        if (!profile.TryGet(out Vignette vignette) || !vignette.active ||
            !ParameterEquals(vignette.intensity, 0.18f) ||
            !ParameterEquals(vignette.smoothness, 0.38f) ||
            !vignette.rounded.overrideState || vignette.rounded.value)
        {
            throw new InvalidOperationException("Black Pines Vignette values have drifted.");
        }

        if (!profile.TryGet(out FilmGrain grain) || !grain.active ||
            !grain.type.overrideState || grain.type.value != FilmGrainLookup.Thin1 ||
            !ParameterEquals(grain.intensity, 0.08f) || !ParameterEquals(grain.response, 0.8f))
        {
            throw new InvalidOperationException("Black Pines Film Grain values have drifted.");
        }

        if (!profile.TryGet(out Bloom bloom) || !bloom.active ||
            !ParameterEquals(bloom.threshold, 1.1f) ||
            !ParameterEquals(bloom.intensity, 0.18f) ||
            !ParameterEquals(bloom.scatter, 0.55f))
        {
            throw new InvalidOperationException("Black Pines Bloom values have drifted.");
        }
    }

    private static bool ParameterEquals(VolumeParameter<float> parameter, float expected)
    {
        return parameter.overrideState && Nearly(parameter.value, expected);
    }

    private static void ValidateFogFields(Transform fogRoot, Terrain terrain)
    {
        if (fogRoot == null)
        {
            throw new InvalidOperationException("Black Pines low-hanging fog root is missing.");
        }
        ValidateIdentityTransform(fogRoot, "Black Pines fog root");
        if (fogRoot.childCount != FogFields.Length)
        {
            throw new InvalidOperationException(
                "Black Pines fog field count drifted; expected " + FogFields.Length + ".");
        }
        if (FogFields.Select(field => field.Name).Distinct(StringComparer.Ordinal).Count() != FogFields.Length ||
            FogFields.Any(field => field.Seed == 0u) ||
            FogFields.Select(field => field.Seed).Distinct().Count() != FogFields.Length)
        {
            throw new InvalidOperationException(
                "Black Pines fog fields must have unique names and unique nonzero deterministic seeds.");
        }

        int aggregateMaximum = 0;
        for (int index = 0; index < FogFields.Length; index++)
        {
            FogFieldSpec spec = FogFields[index];
            Transform child = fogRoot.GetChild(index);
            if (!child.gameObject.activeSelf || child.name != spec.Name)
            {
                throw new InvalidOperationException("Black Pines fog field order or name drifted at index " + index + ".");
            }

            Vector3 expectedPosition = new Vector3(
                spec.Point.x,
                WorldHeight(terrain, spec.Point) + 0.18f,
                spec.Point.y);
            if (Vector3.Distance(child.position, expectedPosition) > 0.08f ||
                Quaternion.Angle(child.rotation, Quaternion.identity) > 0.01f ||
                Vector3.Distance(child.localScale, Vector3.one) > 0.001f)
            {
                throw new InvalidOperationException("Black Pines fog field is not grounded or has transform drift: " + spec.Name);
            }

            ParticleSystem[] systems = child.GetComponents<ParticleSystem>();
            ParticleSystemRenderer[] renderers = child.GetComponents<ParticleSystemRenderer>();
            if (systems.Length != 1 || renderers.Length != 1)
            {
                throw new InvalidOperationException("Black Pines fog field component count drifted: " + spec.Name);
            }

            ParticleSystem particleSystem = systems[0];
            ParticleSystem.MainModule main = particleSystem.main;
            ParticleSystem.ShapeModule shape = particleSystem.shape;
            ParticleSystem.EmissionModule emission = particleSystem.emission;
            if (particleSystem.useAutoRandomSeed || particleSystem.randomSeed != spec.Seed ||
                !main.loop || !main.prewarm || main.simulationSpace != ParticleSystemSimulationSpace.World ||
                main.maxParticles != spec.Maximum || main.cullingMode == ParticleSystemCullingMode.AlwaysSimulate ||
                !shape.enabled || shape.shapeType != ParticleSystemShapeType.Box ||
                Mathf.Abs(shape.scale.x - spec.Size.x) > 0.01f ||
                Mathf.Abs(shape.scale.z - spec.Size.z) > 0.01f ||
                !emission.enabled || Mathf.Abs(emission.rateOverTime.constant - spec.Rate) > 0.01f)
            {
                throw new InvalidOperationException("Black Pines fog field particle settings drifted: " + spec.Name);
            }

            ParticleSystemRenderer renderer = renderers[0];
            if (renderer.renderMode != ParticleSystemRenderMode.HorizontalBillboard ||
                renderer.shadowCastingMode != ShadowCastingMode.Off || renderer.receiveShadows ||
                AssetDatabase.GetAssetPath(renderer.sharedMaterial) != FogMaterialPath)
            {
                throw new InvalidOperationException("Black Pines fog renderer settings drifted: " + spec.Name);
            }

            aggregateMaximum += main.maxParticles;
        }

        if (aggregateMaximum != FogFields.Sum(field => field.Maximum) ||
            aggregateMaximum > FogParticleBudgetCeiling)
        {
            throw new InvalidOperationException(
                "Black Pines low-fog particle budget is invalid: " + aggregateMaximum + ".");
        }
        int baseMaximum = FogFields.Where(field => field.BaseCoverage).Sum(field => field.Maximum);
        int pocketMaximum = FogFields.Where(field => !field.BaseCoverage).Sum(field => field.Maximum);
        if (baseMaximum != 118 || pocketMaximum != 152)
        {
            throw new InvalidOperationException(
                "Black Pines base-veil or dense-pocket fog budget drifted.");
        }

        ValidateFullAreaFogCoverage();
    }

    private static void ValidateFullAreaFogCoverage()
    {
        FogFieldSpec[] baseFields = FogFields.Where(field => field.BaseCoverage).ToArray();
        if (baseFields.Length != 9)
        {
            throw new InvalidOperationException(
                "Black Pines full-area fog veil must contain exactly nine base fields.");
        }

        // Sample the complete wider Black Pines dressing ellipse. The terrain clips its west edge
        // at x=-700, so that is the first valid world-space sample.
        for (int x = -700; x <= 90; x += 20)
        {
            for (int z = -550; z <= 170; z += 20)
            {
                Vector2 point = new Vector2(x, z);
                Vector2 normalized = new Vector2(
                    (point.x - BlackPinesFogCenter.x) / BlackPinesFogRadii.x,
                    (point.y - BlackPinesFogCenter.y) / BlackPinesFogRadii.y);
                if (normalized.sqrMagnitude > 1f)
                {
                    continue;
                }

                bool covered = baseFields.Any(field =>
                    Mathf.Abs(point.x - field.Point.x) <= field.Size.x * 0.5f &&
                    Mathf.Abs(point.y - field.Point.y) <= field.Size.z * 0.5f);
                if (!covered)
                {
                    throw new InvalidOperationException(
                        "Black Pines base fog veil leaves an uncovered biome sample at " + point + ".");
                }
            }
        }
    }

    private static void ValidatePatches(Transform patchRoot, Terrain terrain)
    {
        if (patchRoot == null)
        {
            throw new InvalidOperationException("Black Pines infected-patch root is missing.");
        }
        ValidateIdentityTransform(patchRoot, "Black Pines infected-patch root");
        if (patchRoot.childCount != Patches.Length)
        {
            throw new InvalidOperationException(
                "Black Pines infected-patch count drifted; expected " + Patches.Length + ".");
        }

        for (int index = 0; index < Patches.Length; index++)
        {
            PatchSpec spec = Patches[index];
            GameObject instance = patchRoot.GetChild(index).gameObject;
            if (!instance.activeSelf || instance.name != spec.Name ||
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance) != PatchPrefabPath)
            {
                throw new InvalidOperationException("Black Pines infected-patch name, order, or prefab drifted: " + spec.Name);
            }

            Vector3 expectedPosition = new Vector3(
                spec.Point.x,
                WorldHeight(terrain, spec.Point),
                spec.Point.y);
            if (Vector3.Distance(instance.transform.position, expectedPosition) > 0.08f ||
                Mathf.Abs(Mathf.DeltaAngle(instance.transform.eulerAngles.y, spec.Yaw)) > 0.05f ||
                Vector3.Distance(instance.transform.localScale, Vector3.one) > 0.001f)
            {
                throw new InvalidOperationException("Black Pines infected patch is not grounded or has transform drift: " + spec.Name);
            }

            if (DistanceToPolyline(spec.Point, ProtectedRoad) < 55f ||
                Vector2.Distance(spec.Point, new Vector2(-350f, -150f)) < 50f ||
                Vector2.Distance(spec.Point, new Vector2(235f, -40f)) < 40f)
            {
                throw new InvalidOperationException("Black Pines infected patch violates a protected gameplay clearance: " + spec.Name);
            }

            BloodrootInfectionZone zone = instance.GetComponent<BloodrootInfectionZone>();
            BoxCollider trigger = instance.GetComponent<BoxCollider>();
            Rigidbody body = instance.GetComponent<Rigidbody>();
            NavMeshModifier modifier = instance.GetComponent<NavMeshModifier>();
            if (zone == null || trigger == null || body == null ||
                modifier == null || !modifier.enabled || !modifier.ignoreFromBuild ||
                !modifier.applyToChildren ||
                zone.enabled != spec.Mechanical || trigger.enabled != spec.Mechanical ||
                !trigger.isTrigger || !body.isKinematic || body.useGravity ||
                body.collisionDetectionMode != CollisionDetectionMode.Discrete ||
                Vector3.Distance(trigger.center, PatchTriggerCenter) > 0.001f ||
                Vector3.Distance(trigger.size, PatchTriggerSize) > 0.001f)
            {
                throw new InvalidOperationException("Black Pines infected patch mechanics drifted: " + spec.Name);
            }

            SerializedObject serializedZone = new SerializedObject(zone);
            SerializedProperty infectionRate = serializedZone.FindProperty("infectionPerSecond");
            if (infectionRate == null || !Nearly(infectionRate.floatValue, 20f))
            {
                throw new InvalidOperationException("Black Pines infected patch rate drifted: " + spec.Name);
            }

            if (instance.GetComponentsInChildren<Light>(true).Length != 1 ||
                instance.GetComponentsInChildren<ParticleSystem>(true).Length != 1 ||
                instance.GetComponentsInChildren<Renderer>(true).Length > 12)
            {
                throw new InvalidOperationException("Black Pines infected patch exceeded its visual budget: " + spec.Name);
            }

            ParticleSystem patchMist = instance.GetComponentInChildren<ParticleSystem>(true);
            Transform apron = instance.transform.Find("Corrupted Ground Apron");
            ParticleSystem.MainModule patchMain = patchMist.main;
            ParticleSystem.EmissionModule patchEmission = patchMist.emission;
            ParticleSystem.ShapeModule patchShape = patchMist.shape;
            ParticleSystem.VelocityOverLifetimeModule patchVelocity = patchMist.velocityOverLifetime;
            if (apron == null ||
                Vector3.Distance(
                    apron.localScale,
                    new Vector3(PatchApronDiameter, PatchApronThicknessScale, PatchApronDiameter)) > 0.001f ||
                patchMain.maxParticles != PatchMistMaximum ||
                !patchEmission.enabled || !Nearly(patchEmission.rateOverTime.constant, PatchMistRate) ||
                !patchShape.enabled || patchShape.shapeType != ParticleSystemShapeType.Circle ||
                !Nearly(patchShape.radius, PatchMistRadius) ||
                !patchVelocity.enabled ||
                patchVelocity.x.mode != ParticleSystemCurveMode.Constant ||
                patchVelocity.y.mode != ParticleSystemCurveMode.Constant ||
                patchVelocity.z.mode != ParticleSystemCurveMode.Constant ||
                !Nearly(patchVelocity.x.constant, 0f) ||
                !Nearly(patchVelocity.y.constant, 0.11f) ||
                !Nearly(patchVelocity.z.constant, 0f))
            {
                throw new InvalidOperationException(
                    "Black Pines infected patch mist velocity drifted: " + spec.Name);
            }
        }

        if (Patches.Count(patch => patch.Mechanical) != 4)
        {
            throw new InvalidOperationException("Black Pines must have exactly four mechanical infection patches.");
        }
    }

    private static void ValidateInfectedTrees(Transform treeRoot, Terrain terrain)
    {
        if (treeRoot == null)
        {
            throw new InvalidOperationException("Black Pines infected-tree root is missing.");
        }
        ValidateIdentityTransform(treeRoot, "Black Pines infected-tree root");
        if (treeRoot.childCount != InfectedTrees.Length)
        {
            throw new InvalidOperationException(
                "Black Pines infected-tree count drifted; expected " + InfectedTrees.Length + ".");
        }

        for (int index = 0; index < InfectedTrees.Length; index++)
        {
            TreePlacementSpec spec = InfectedTrees[index];
            GameObject instance = treeRoot.GetChild(index).gameObject;
            if (!instance.activeSelf || instance.name != spec.Name ||
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance) != InfectedTreePrefabPaths[spec.PrefabIndex])
            {
                throw new InvalidOperationException("Black Pines infected-tree name, order, or prefab drifted: " + spec.Name);
            }

            Vector3 expectedPosition = new Vector3(
                spec.Point.x,
                WorldHeight(terrain, spec.Point),
                spec.Point.y);
            if (Vector3.Distance(instance.transform.position, expectedPosition) > 0.08f ||
                Mathf.Abs(Mathf.DeltaAngle(instance.transform.eulerAngles.y, spec.Yaw)) > 0.05f ||
                Vector3.Distance(instance.transform.localScale, Vector3.one * spec.Scale) > 0.002f)
            {
                throw new InvalidOperationException("Black Pines infected tree is not grounded or has transform drift: " + spec.Name);
            }

            if (instance.GetComponent<LODGroup>() == null ||
                instance.GetComponentsInChildren<Collider>(true).Length == 0 ||
                instance.transform.Find("Authored Infection Growth") == null)
            {
                throw new InvalidOperationException("Black Pines infected tree lost LOD, collision, or authored growth: " + spec.Name);
            }
        }
    }

    private static void ValidateGeneratedAssets()
    {
        IReadOnlyDictionary<string, string> expectedAssets = BuildExpectedAssetMarkers();
        foreach (KeyValuePair<string, string> pair in expectedAssets)
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(pair.Key);
            AssetImporter importer = AssetImporter.GetAtPath(pair.Key);
            if (asset == null || importer == null || importer.userData != pair.Value)
            {
                throw new InvalidOperationException(
                    "Generated Black Pines asset is missing or unowned: " + pair.Key);
            }
        }

        Texture2D fogTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(FogTexturePath);
        Material fogMaterial = AssetDatabase.LoadAssetAtPath<Material>(FogMaterialPath);
        if (fogTexture == null || fogTexture.width != 128 || fogTexture.height != 128 ||
            fogTexture.wrapMode != TextureWrapMode.Clamp || fogMaterial == null ||
            fogMaterial.shader == null || fogMaterial.shader.name != "Universal Render Pipeline/Particles/Unlit" ||
            fogMaterial.renderQueue < 3000 ||
            (fogMaterial.HasProperty("_ZWrite") && !Nearly(fogMaterial.GetFloat("_ZWrite"), 0f)) ||
            (fogMaterial.HasProperty("_BaseMap") && fogMaterial.GetTexture("_BaseMap") != fogTexture))
        {
            throw new InvalidOperationException("Owned Black Pines fog texture/material settings have drifted.");
        }

        foreach (string materialPath in expectedAssets
                     .Where(pair => pair.Value.Contains("material|infected-tree-"))
                     .Select(pair => pair.Key))
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null || !material.enableInstancing ||
                (material.HasProperty("_Value") && material.GetFloat("_Value") > 0.35f) ||
                (material.HasProperty("_Saturation") && material.GetFloat("_Saturation") > 0.36f) ||
                (material.HasProperty("_Smoothness") && material.GetFloat("_Smoothness") > 0.08f))
            {
                throw new InvalidOperationException("Owned haunted-tree material is no longer dark/matte: " + materialPath);
            }

            if (material.HasProperty("Color_369F793F"))
            {
                Color tint = material.GetColor("Color_369F793F");
                if (Mathf.Max(tint.r, Mathf.Max(tint.g, tint.b)) > 0.15f)
                {
                    throw new InvalidOperationException("Owned haunted foliage tint is too bright: " + materialPath);
                }
            }
        }

        foreach (string materialPath in new[]
                 {
                     CorruptedGroundMaterialPath,
                     RootsMaterialPath,
                     CoreMaterialPath
                 })
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null || !material.enableInstancing || !material.IsKeywordEnabled("_EMISSION") ||
                (material.HasProperty("_Metallic") && material.GetFloat("_Metallic") > 0.001f) ||
                (material.HasProperty("_Smoothness") && material.GetFloat("_Smoothness") > 0.06f))
            {
                throw new InvalidOperationException("Owned infection material is not matte/emissive/instanced: " + materialPath);
            }
        }

        for (int index = 0; index < InfectedTreePrefabPaths.Length; index++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(InfectedTreePrefabPaths[index]);
            LODGroup lodGroup = prefab == null ? null : prefab.GetComponent<LODGroup>();
            Transform growth = prefab == null ? null : prefab.transform.Find("Authored Infection Growth");
            if (prefab == null || lodGroup == null ||
                prefab.GetComponentsInChildren<Collider>(true).Length == 0 ||
                growth == null)
            {
                throw new InvalidOperationException(
                    "Owned infected-tree prefab lost required production structure: " + InfectedTreePrefabPaths[index]);
            }

            Renderer[] growthRenderers = growth.GetComponentsInChildren<Renderer>(true);
            LOD[] lods = lodGroup.GetLODs();
            if (growthRenderers.Length != 4 || lods.Length == 0 ||
                growthRenderers.Any(renderer => !lods[0].renderers.Contains(renderer)) ||
                lods.Skip(1).Any(lod => lod.renderers.Any(growthRenderers.Contains)))
            {
                throw new InvalidOperationException(
                    "Owned infected-tree growth is not confined to the nearest LOD: " + InfectedTreePrefabPaths[index]);
            }
        }
    }

    private static void ValidateIdentityTransform(Transform transform, string label)
    {
        if (Vector3.Distance(transform.localPosition, Vector3.zero) > 0.001f ||
            Quaternion.Angle(transform.localRotation, Quaternion.identity) > 0.001f ||
            Vector3.Distance(transform.localScale, Vector3.one) > 0.001f)
        {
            throw new InvalidOperationException(label + " must retain an identity local transform.");
        }
    }

    private static float WorldHeight(Terrain terrain, Vector2 point)
    {
        TerrainData data = terrain.terrainData;
        Vector3 origin = terrain.transform.position;
        float normalizedX = Mathf.InverseLerp(origin.x, origin.x + data.size.x, point.x);
        float normalizedZ = Mathf.InverseLerp(origin.z, origin.z + data.size.z, point.y);
        if (normalizedX < 0f || normalizedX > 1f || normalizedZ < 0f || normalizedZ > 1f)
        {
            throw new InvalidOperationException("Atmosphere placement falls outside production Terrain bounds: " + point);
        }
        return origin.y + data.GetInterpolatedHeight(normalizedX, normalizedZ);
    }

    private static float DistanceToPolyline(Vector2 point, IReadOnlyList<Vector2> line)
    {
        float minimum = float.PositiveInfinity;
        for (int index = 0; index < line.Count - 1; index++)
        {
            Vector2 a = line[index];
            Vector2 b = line[index + 1];
            Vector2 segment = b - a;
            float lengthSquared = segment.sqrMagnitude;
            float amount = lengthSquared <= 0.0001f
                ? 0f
                : Mathf.Clamp01(Vector2.Dot(point - a, segment) / lengthSquared);
            minimum = Mathf.Min(minimum, Vector2.Distance(point, a + segment * amount));
        }
        return minimum;
    }

    private static float ColorDistance(Color a, Color b)
    {
        Vector4 delta = new Vector4(a.r - b.r, a.g - b.g, a.b - b.b, a.a - b.a);
        return delta.magnitude;
    }

    private static bool Nearly(float a, float b, float tolerance = 0.001f)
    {
        return Mathf.Abs(a - b) <= tolerance;
    }

    private static bool AssetOrMetaExists(string assetPath)
    {
        return File.Exists(ProjectAbsolutePath(assetPath)) ||
               File.Exists(ProjectAbsolutePath(assetPath + ".meta"));
    }

    private static string ProjectAbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrEmpty(projectRoot))
        {
            throw new InvalidOperationException("Could not resolve the Unity project root.");
        }

        return Path.GetFullPath(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string BuildValidationSummary()
    {
        int mechanical = Patches.Count(patch => patch.Mechanical);
        int fogBudget = FogFields.Sum(field => field.Maximum);
        return FogFields.Length + " low-hanging fog fields, " +
               Patches.Length + " infected patches (" + mechanical + " mechanical), " +
               InfectedTrees.Length + " authored infected trees, and a " + fogBudget +
               "-particle fog ceiling passed validation. Terrain vegetation and TerrainData remain untouched.";
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
            throw new InvalidOperationException(
                "Could not close the mutated open-world scene during atmosphere rollback.");
        }

        Scene restored = EditorSceneManager.OpenScene(OpenWorldScene, OpenSceneMode.Additive);
        if (!restored.IsValid() || !restored.isLoaded)
        {
            throw new InvalidOperationException(
                "Could not reopen the restored open-world scene during atmosphere rollback.");
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
        Terrain[] terrains = GetSceneComponents<Terrain>(scene);
        if (terrains.Length != 1)
        {
            throw new InvalidOperationException(
                "Black Pines atmosphere requires exactly one Terrain in Bloodroot_OpenWorld; found " + terrains.Length + ".");
        }

        Terrain terrain = terrains[0];
        string dataPath = AssetDatabase.GetAssetPath(terrain.terrainData);
        if (dataPath != ProductionTerrainPath)
        {
            throw new InvalidOperationException(
                "The open-world Terrain does not reference the production TerrainData. Found: " + dataPath);
        }

        TerrainCollider[] colliders = GetSceneComponents<TerrainCollider>(scene);
        if (colliders.Length != 1 || colliders[0].terrainData != terrain.terrainData)
        {
            throw new InvalidOperationException(
                "The open-world TerrainCollider must reference the same production TerrainData as the Terrain.");
        }

        return terrain;
    }

    private static T[] GetSceneComponents<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true))
            .Where(component => component.gameObject.scene == scene)
            .ToArray();
    }

    private static Transform FindSceneTransform(Scene scene, string name)
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(transform => transform.name == name);
    }

    private static Transform FindAtmosphereRoot(Scene scene)
    {
        Transform area = FindSceneTransform(scene, AreaRootName);
        if (area == null)
        {
            return null;
        }

        Transform environment = area.Find(EnvironmentRootName);
        return environment == null ? null : environment.Find(AtmosphereRootName);
    }

    private static void ValidateNaturalDressingBoundary(Scene scene, TerrainData data)
    {
        Transform[] signatures = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Where(transform => transform.name.StartsWith("_PLACEMENT_SIGNATURE_", StringComparison.Ordinal))
            .ToArray();
        if (signatures.Length != 1 || signatures[0].name != NaturalDressingSignature)
        {
            throw new InvalidOperationException(
                "The validated natural-dressing signature is missing or changed. Run its validator before adding atmosphere; the atmosphere tool will not guess around changed Terrain vegetation.");
        }

        if (data.treePrototypes.Length != 4 || data.treeInstances.Length != 3505 ||
            data.detailPrototypes.Length != 2)
        {
            throw new InvalidOperationException(
                "Terrain vegetation no longer matches the validated natural-dressing boundary (4 prototypes, 3,505 trees, 2 detail prototypes).");
        }
    }

    private static void EnsurePersistentBackupPair()
    {
        bool sceneAsset = File.Exists(ProjectAbsolutePath(BackupScenePath));
        bool sceneMeta = File.Exists(ProjectAbsolutePath(BackupScenePath + ".meta"));
        bool terrainAsset = File.Exists(ProjectAbsolutePath(BackupTerrainPath));
        bool terrainMeta = File.Exists(ProjectAbsolutePath(BackupTerrainPath + ".meta"));
        bool any = sceneAsset || sceneMeta || terrainAsset || terrainMeta;
        bool all = sceneAsset && sceneMeta && terrainAsset && terrainMeta;

        if (any && !all)
        {
            throw new InvalidOperationException(
                "The pre-atmosphere backup pair is incomplete. Restore both matching scene/TerrainData files or remove the partial pair manually before retrying.");
        }

        if (all)
        {
            ValidatePersistentBackupPair();
            return;
        }

        bool createdScene = false;
        bool createdTerrain = false;
        try
        {
            if (!AssetDatabase.CopyAsset(ProductionTerrainPath, BackupTerrainPath))
            {
                throw new InvalidOperationException(
                    "Could not create the immutable pre-atmosphere TerrainData backup.");
            }
            createdTerrain = true;

            if (!AssetDatabase.CopyAsset(OpenWorldScene, BackupScenePath))
            {
                throw new InvalidOperationException(
                    "Could not create the immutable pre-atmosphere scene backup.");
            }
            createdScene = true;

            AssetDatabase.ImportAsset(BackupTerrainPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(BackupScenePath, ImportAssetOptions.ForceSynchronousImport);
            TerrainData backupData = AssetDatabase.LoadAssetAtPath<TerrainData>(BackupTerrainPath);
            if (backupData == null)
            {
                throw new InvalidOperationException(
                    "The copied pre-atmosphere TerrainData could not be loaded.");
            }

            Scene loadedBackup = SceneManager.GetSceneByPath(BackupScenePath);
            if (loadedBackup.IsValid() && loadedBackup.isLoaded)
            {
                throw new InvalidOperationException(
                    "The pre-atmosphere backup scene is already loaded. Close it before creating the immutable backup pair.");
            }

            Scene backupScene = EditorSceneManager.OpenScene(BackupScenePath, OpenSceneMode.Additive);
            try
            {
                Terrain[] terrains = GetSceneComponents<Terrain>(backupScene);
                TerrainCollider[] colliders = GetSceneComponents<TerrainCollider>(backupScene);
                if (terrains.Length != 1 || colliders.Length != 1)
                {
                    throw new InvalidOperationException(
                        "The copied backup scene does not contain exactly one Terrain and one TerrainCollider.");
                }

                terrains[0].terrainData = backupData;
                colliders[0].terrainData = backupData;
                EditorSceneManager.MarkSceneDirty(backupScene);
                if (!EditorSceneManager.SaveScene(backupScene, BackupScenePath))
                {
                    throw new InvalidOperationException(
                        "Unity could not save the isolated pre-atmosphere backup scene.");
                }
            }
            finally
            {
                if (backupScene.IsValid() && backupScene.isLoaded && !backupScene.isDirty)
                {
                    EditorSceneManager.CloseScene(backupScene, true);
                }
            }

            ValidatePersistentBackupPair();
        }
        catch
        {
            if (createdScene)
            {
                AssetDatabase.DeleteAsset(BackupScenePath);
            }

            if (createdTerrain)
            {
                AssetDatabase.DeleteAsset(BackupTerrainPath);
            }

            throw;
        }
    }

    private static void ValidatePersistentBackupPair()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BackupScenePath) == null ||
            AssetDatabase.LoadAssetAtPath<TerrainData>(BackupTerrainPath) == null)
        {
            throw new InvalidOperationException(
                "The immutable pre-atmosphere backup pair could not be loaded.");
        }

        string[] dependencies = AssetDatabase.GetDependencies(BackupScenePath, true);
        if (!dependencies.Contains(BackupTerrainPath) || dependencies.Contains(ProductionTerrainPath))
        {
            throw new InvalidOperationException(
                "The pre-atmosphere backup scene is not isolated from the live production TerrainData.");
        }

        foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
        {
            if (entry.enabled &&
                (entry.path == BackupScenePath ||
                 entry.path.IndexOf("BlackPinesAtmosphereRepair", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                throw new InvalidOperationException(
                    "A Black Pines atmosphere backup or repair scene is enabled in Build Settings: " + entry.path);
            }
        }

        Scene loaded = SceneManager.GetSceneByPath(BackupScenePath);
        bool openedHere = !loaded.IsValid() || !loaded.isLoaded;
        if (openedHere)
        {
            loaded = EditorSceneManager.OpenScene(BackupScenePath, OpenSceneMode.Additive);
        }

        try
        {
            if (loaded.isDirty)
            {
                throw new InvalidOperationException(
                    "The immutable pre-atmosphere backup scene is dirty. Save or close it without changes before validation.");
            }

            TerrainData backupData = AssetDatabase.LoadAssetAtPath<TerrainData>(BackupTerrainPath);
            Terrain[] terrains = GetSceneComponents<Terrain>(loaded);
            TerrainCollider[] colliders = GetSceneComponents<TerrainCollider>(loaded);
            if (terrains.Length != 1 || colliders.Length != 1 ||
                terrains[0].terrainData != backupData || colliders[0].terrainData != backupData)
            {
                throw new InvalidOperationException(
                    "The immutable pre-atmosphere backup pair has mismatched Terrain references.");
            }

            if (FindAtmosphereRoot(loaded) != null)
            {
                throw new InvalidOperationException(
                    "The immutable pre-atmosphere backup unexpectedly contains generated atmosphere.");
            }
        }
        finally
        {
            if (openedHere && loaded.IsValid() && loaded.isLoaded && !loaded.isDirty)
            {
                EditorSceneManager.CloseScene(loaded, true);
            }
        }
    }

    private static IReadOnlyDictionary<string, string> BuildExpectedAssetMarkers()
    {
        Dictionary<string, string> markers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ProfilePath] = OwnershipPrefix + "profile|black-pines",
            [FogTexturePath] = OwnershipPrefix + "texture|soft-noise",
            [FogMaterialPath] = OwnershipPrefix + "material|low-fog",
            [PatchPrefabPath] = OwnershipPrefix + "prefab|infected-patch",
            [CorruptedGroundMaterialPath] = OwnershipPrefix + "material|infection-ground|" + SourceGuid(CorruptedGroundSourcePath),
            [RootsMaterialPath] = OwnershipPrefix + "material|infection-roots|" + SourceGuid(RootsSourcePath),
            [CoreMaterialPath] = OwnershipPrefix + "material|infection-core|" + SourceGuid(CoreSourcePath),
            [InfectionParticlesMaterialPath] = OwnershipPrefix + "material|infection-particles|" + SourceGuid(ParticlesSourcePath)
        };

        for (int i = 0; i < SourceTreePrefabPaths.Length; i++)
        {
            string sourcePrefabPath = SourceTreePrefabPaths[i];
            string targetPrefabPath = InfectedTreePrefabPaths[i];
            markers[targetPrefabPath] = OwnershipPrefix + "prefab|infected-tree|" + SourceGuid(sourcePrefabPath);
            foreach (string sourceMaterialPath in CollectSourceMaterialPaths(sourcePrefabPath))
            {
                string targetMaterialPath = TreeMaterialTargetPath(i, sourceMaterialPath);
                markers[targetMaterialPath] = OwnershipPrefix + "material|infected-tree-" + i + "|" + SourceGuid(sourceMaterialPath);
            }
        }

        return markers;
    }

    private static IEnumerable<string> CollectSourceMaterialPaths(string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            throw new InvalidOperationException("Required production tree prefab is missing: " + prefabPath);
        }

        string[] paths = prefab.GetComponentsInChildren<Renderer>(true)
            .SelectMany(renderer => renderer.sharedMaterials)
            .Where(material => material != null)
            .Select(AssetDatabase.GetAssetPath)
            .Where(path => !string.IsNullOrEmpty(path))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        if (paths.Length == 0)
        {
            throw new InvalidOperationException(
                "Required production tree prefab has no project material assets: " + prefabPath);
        }

        return paths;
    }

    private static string TreeMaterialTargetPath(int treeIndex, string sourceMaterialPath)
    {
        string prefabStem = Path.GetFileNameWithoutExtension(InfectedTreePrefabPaths[treeIndex]);
        string materialStem = SanitizeAssetStem(Path.GetFileNameWithoutExtension(sourceMaterialPath));
        string guid = SourceGuid(sourceMaterialPath);
        return MaterialFolder + "/" + prefabStem + "_" + materialStem + "_" + guid.Substring(0, 8) + ".mat";
    }

    private static string SourceGuid(string assetPath)
    {
        if (AssetDatabase.LoadMainAssetAtPath(assetPath) == null)
        {
            throw new InvalidOperationException("Required source asset is missing: " + assetPath);
        }

        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrEmpty(guid))
        {
            throw new InvalidOperationException("Required source asset has no GUID: " + assetPath);
        }

        return guid;
    }

    private static string SanitizeAssetStem(string value)
    {
        return new string(value.Select(character =>
                char.IsLetterOrDigit(character) ? character : '_')
            .ToArray());
    }

    private static List<GeneratedAssetSnapshot> CaptureGeneratedAssetSnapshots(IEnumerable<string> assetPaths)
    {
        List<GeneratedAssetSnapshot> snapshots = new List<GeneratedAssetSnapshot>();
        foreach (string assetPath in assetPaths.OrderBy(path => path, StringComparer.Ordinal))
        {
            string absoluteAsset = ProjectAbsolutePath(assetPath);
            string absoluteMeta = ProjectAbsolutePath(assetPath + ".meta");
            bool assetExists = File.Exists(absoluteAsset);
            bool metaExists = File.Exists(absoluteMeta);
            if (assetExists != metaExists)
            {
                throw new InvalidOperationException(
                    "Generated atmosphere asset/meta pair is incomplete: " + assetPath);
            }

            snapshots.Add(new GeneratedAssetSnapshot
            {
                AssetPath = assetPath,
                AssetBytes = assetExists ? File.ReadAllBytes(absoluteAsset) : null,
                MetaBytes = metaExists ? File.ReadAllBytes(absoluteMeta) : null
            });
        }

        return snapshots;
    }

    private static void RestoreGeneratedAssetSnapshots(List<GeneratedAssetSnapshot> snapshots)
    {
        if (snapshots == null)
        {
            return;
        }

        AssetDatabase.ReleaseCachedFileHandles();
        foreach (GeneratedAssetSnapshot snapshot in snapshots)
        {
            if (snapshot.AssetBytes == null)
            {
                if (AssetOrMetaExists(snapshot.AssetPath))
                {
                    AssetDatabase.DeleteAsset(snapshot.AssetPath);
                }

                continue;
            }

            File.WriteAllBytes(ProjectAbsolutePath(snapshot.AssetPath), snapshot.AssetBytes);
            File.WriteAllBytes(ProjectAbsolutePath(snapshot.AssetPath + ".meta"), snapshot.MetaBytes);
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    private static HashSet<string> CaptureAndCreateOutputFolders()
    {
        HashSet<string> created = new HashSet<string>(StringComparer.Ordinal);
        EnsureFolder(MaterialFolder, created);
        EnsureFolder(PrefabFolder, created);
        EnsureFolder(VfxFolder, created);
        return created;
    }

    private static void EnsureFolder(string assetFolder, HashSet<string> created)
    {
        if (AssetDatabase.IsValidFolder(assetFolder))
        {
            return;
        }

        string parent = Path.GetDirectoryName(assetFolder)?.Replace('\\', '/');
        string name = Path.GetFileName(assetFolder);
        if (string.IsNullOrEmpty(parent) || !AssetDatabase.IsValidFolder(parent))
        {
            throw new InvalidOperationException(
                "Cannot create atmosphere output folder because its parent is missing: " + assetFolder);
        }

        string guid = AssetDatabase.CreateFolder(parent, name);
        if (string.IsNullOrEmpty(guid))
        {
            throw new InvalidOperationException("Unity could not create atmosphere output folder: " + assetFolder);
        }

        created.Add(assetFolder);
    }

    private static void RemoveCreatedFoldersIfEmpty(HashSet<string> created)
    {
        if (created == null)
        {
            return;
        }

        foreach (string folder in created.OrderByDescending(path => path.Length))
        {
            string absolute = ProjectAbsolutePath(folder);
            if (Directory.Exists(absolute) && !Directory.EnumerateFileSystemEntries(absolute).Any())
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }

    private static void RestoreTerrainFromRepairCopy(TerrainData repairBackup, byte[] exactBytes)
    {
        if (repairBackup != null)
        {
            TerrainData live = AssetDatabase.LoadAssetAtPath<TerrainData>(ProductionTerrainPath);
            if (live != null)
            {
                EditorUtility.CopySerialized(repairBackup, live);
                live.name = Path.GetFileNameWithoutExtension(ProductionTerrainPath);
                EditorUtility.SetDirty(live);
                AssetDatabase.SaveAssetIfDirty(live);
            }
        }

        if (exactBytes != null)
        {
            AssetDatabase.ReleaseCachedFileHandles();
            File.WriteAllBytes(ProjectAbsolutePath(ProductionTerrainPath), exactBytes);
            AssetDatabase.ImportAsset(ProductionTerrainPath, ImportAssetOptions.ForceSynchronousImport);
        }
    }

    private static void EnsureGeneratedAssets(IReadOnlyDictionary<string, string> expectedAssets)
    {
        foreach (KeyValuePair<string, string> pair in expectedAssets)
        {
            ValidateExistingOwnership(pair.Key, pair.Value);
        }

        EnsureAtmosphereProfile(expectedAssets[ProfilePath]);
        Texture2D fogTexture = EnsureFogTexture(expectedAssets[FogTexturePath]);
        EnsureFogMaterial(fogTexture, expectedAssets[FogMaterialPath]);
        EnsureInfectionMaterials(fogTexture, expectedAssets);
        EnsureInfectedTreeAssets(expectedAssets);
        EnsureInfectedPatchPrefab(expectedAssets[PatchPrefabPath]);

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (KeyValuePair<string, string> pair in expectedAssets)
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(pair.Key);
            AssetImporter importer = AssetImporter.GetAtPath(pair.Key);
            if (asset == null || importer == null || importer.userData != pair.Value)
            {
                throw new InvalidOperationException(
                    "Generated atmosphere asset is missing or has invalid ownership after generation: " + pair.Key);
            }
        }
    }

    private static void ValidateExistingOwnership(string assetPath, string expectedMarker)
    {
        string absoluteAsset = ProjectAbsolutePath(assetPath);
        string absoluteMeta = ProjectAbsolutePath(assetPath + ".meta");
        bool assetExists = File.Exists(absoluteAsset);
        bool metaExists = File.Exists(absoluteMeta);
        if (assetExists != metaExists)
        {
            throw new InvalidOperationException(
                "Generated atmosphere asset/meta pair is incomplete and will not be overwritten: " + assetPath);
        }

        if (!assetExists)
        {
            return;
        }

        AssetImporter importer = AssetImporter.GetAtPath(assetPath);
        if (importer == null || importer.userData != expectedMarker)
        {
            throw new InvalidOperationException(
                "Existing output is not owned by the Black Pines atmosphere tool and will not be overwritten: " + assetPath);
        }
    }

    private static void SetOwnershipMarker(string assetPath, string expectedMarker)
    {
        AssetImporter importer = AssetImporter.GetAtPath(assetPath);
        if (importer == null)
        {
            throw new InvalidOperationException(
                "Could not attach ownership metadata to generated asset: " + assetPath);
        }

        if (importer.userData != expectedMarker)
        {
            importer.userData = expectedMarker;
            importer.SaveAndReimport();
        }
    }

    private static void EnsureAtmosphereProfile(string marker)
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = Path.GetFileNameWithoutExtension(ProfilePath);
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }
        VolumeComponent[] existing = profile.components
            .Where(component => component != null)
            .ToArray();
        ColorAdjustments color = FindOrCreateProfileComponent<ColorAdjustments>(profile, existing);
        color.active = true;
        SetParameter(color.postExposure, -0.45f);
        SetParameter(color.contrast, 16f);
        SetParameter(color.saturation, -24f);
        SetParameter(color.colorFilter, new Color(0.78f, 0.84f, 0.79f, 1f));
        SetParameter(color.hueShift, -3f);

        WhiteBalance whiteBalance = FindOrCreateProfileComponent<WhiteBalance>(profile, existing);
        whiteBalance.active = true;
        SetParameter(whiteBalance.temperature, -8f);
        SetParameter(whiteBalance.tint, -6f);

        Vignette vignette = FindOrCreateProfileComponent<Vignette>(profile, existing);
        vignette.active = true;
        SetParameter(vignette.color, new Color(0.01f, 0.018f, 0.014f, 1f));
        SetParameter(vignette.center, new Vector2(0.5f, 0.5f));
        SetParameter(vignette.intensity, 0.18f);
        SetParameter(vignette.smoothness, 0.38f);
        SetParameter(vignette.rounded, false);

        FilmGrain grain = FindOrCreateProfileComponent<FilmGrain>(profile, existing);
        grain.active = true;
        SetParameter(grain.type, FilmGrainLookup.Thin1);
        SetParameter(grain.intensity, 0.08f);
        SetParameter(grain.response, 0.8f);

        Bloom bloom = FindOrCreateProfileComponent<Bloom>(profile, existing);
        bloom.active = true;
        SetParameter(bloom.threshold, 1.1f);
        SetParameter(bloom.intensity, 0.18f);
        SetParameter(bloom.scatter, 0.55f);

        VolumeComponent[] desired = { color, whiteBalance, vignette, grain, bloom };
        HashSet<VolumeComponent> desiredSet = new HashSet<VolumeComponent>(desired);
        foreach (VolumeComponent component in existing)
        {
            if (desiredSet.Contains(component))
            {
                continue;
            }

            profile.components.Remove(component);
            UnityEngine.Object.DestroyImmediate(component, true);
        }
        profile.components.Clear();
        profile.components.AddRange(desired);

        profile.name = Path.GetFileNameWithoutExtension(ProfilePath);
        profile.Reset();
        foreach (VolumeComponent component in profile.components)
        {
            EditorUtility.SetDirty(component);
        }
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssetIfDirty(profile);
        SetOwnershipMarker(ProfilePath, marker);
    }

    private static T FindOrCreateProfileComponent<T>(
        VolumeProfile profile,
        IReadOnlyList<VolumeComponent> existing)
        where T : VolumeComponent
    {
        T component = existing
            .FirstOrDefault(candidate => candidate.GetType() == typeof(T)) as T;
        return component != null
            ? component
            : VolumeProfileFactory.CreateVolumeComponent<T>(profile, true, false);
    }

    private static void SetParameter<T>(VolumeParameter<T> parameter, T value)
    {
        parameter.overrideState = true;
        parameter.value = value;
    }

    private static Texture2D EnsureFogTexture(string marker)
    {
        const int size = 128;
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(FogTexturePath);
        if (texture == null)
        {
            texture = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
            texture.name = Path.GetFileNameWithoutExtension(FogTexturePath);
            AssetDatabase.CreateAsset(texture, FogTexturePath);
        }
        else
        {
            if (!texture.Reinitialize(size, size, TextureFormat.RGBA32, true))
            {
                throw new InvalidOperationException("Unity could not resize the owned Black Pines fog texture.");
            }
            texture.name = Path.GetFileNameWithoutExtension(FogTexturePath);
        }

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = ((x + 0.5f) / size) * 2f - 1f;
                float ny = ((y + 0.5f) / size) * 2f - 1f;
                float radius = Mathf.Sqrt(nx * nx + ny * ny);
                float edge = 1f - Mathf.SmoothStep(0.38f, 1f, radius);
                float warpX = Mathf.PerlinNoise(x * 0.037f + 13.2f, y * 0.037f + 4.7f);
                float warpY = Mathf.PerlinNoise(x * 0.041f + 31.4f, y * 0.041f + 17.8f);
                float noiseA = Mathf.PerlinNoise(x * 0.065f + warpX * 2.4f, y * 0.065f + warpY * 2.4f);
                float noiseB = Mathf.PerlinNoise(x * 0.145f + 53.1f, y * 0.145f + 81.3f);
                float alpha = edge * Mathf.Lerp(0.26f, 0.78f, noiseA * 0.72f + noiseB * 0.28f);
                pixels[y * size + x] = new Color(0.68f, 0.76f, 0.72f, Mathf.Clamp01(alpha));
            }
        }

        texture.SetPixels(pixels);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.anisoLevel = 0;
        texture.Apply(true, false);
        EditorUtility.SetDirty(texture);
        AssetDatabase.SaveAssetIfDirty(texture);
        SetOwnershipMarker(FogTexturePath, marker);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(FogTexturePath);
    }

    private static void EnsureFogMaterial(Texture2D fogTexture, string marker)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            throw new InvalidOperationException(
                "URP Particles/Unlit shader is unavailable; Black Pines fog cannot be built safely.");
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(FogMaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, FogMaterialPath);
        }
        material.shader = shader;
        material.name = Path.GetFileNameWithoutExtension(FogMaterialPath);
        ConfigureTransparentParticleMaterial(
            material,
            fogTexture,
            new Color(0.18f, 0.23f, 0.21f, 0.18f),
            true,
            0.25f,
            3.5f);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
        SetOwnershipMarker(FogMaterialPath, marker);
    }

    private static void EnsureInfectionMaterials(
        Texture2D fogTexture,
        IReadOnlyDictionary<string, string> expectedAssets)
    {
        Material ground = CloneMaterial(
            CorruptedGroundSourcePath,
            CorruptedGroundMaterialPath,
            expectedAssets[CorruptedGroundMaterialPath]);
        ConfigureInfectionLitMaterial(
            ground,
            new Color(0.10f, 0.018f, 0.022f, 1f),
            new Color(0.22f, 0.008f, 0.012f, 1f),
            0.04f);

        Material roots = CloneMaterial(
            RootsSourcePath,
            RootsMaterialPath,
            expectedAssets[RootsMaterialPath]);
        ConfigureInfectionLitMaterial(
            roots,
            new Color(0.065f, 0.018f, 0.016f, 1f),
            new Color(0.16f, 0.006f, 0.008f, 1f),
            0.03f);

        Material core = CloneMaterial(
            CoreSourcePath,
            CoreMaterialPath,
            expectedAssets[CoreMaterialPath]);
        ConfigureInfectionLitMaterial(
            core,
            new Color(0.19f, 0.012f, 0.018f, 1f),
            new Color(0.85f, 0.018f, 0.025f, 1f),
            0.05f);

        Material particles = CloneMaterial(
            ParticlesSourcePath,
            InfectionParticlesMaterialPath,
            expectedAssets[InfectionParticlesMaterialPath]);
        ConfigureTransparentParticleMaterial(
            particles,
            fogTexture,
            new Color(0.38f, 0.025f, 0.035f, 0.34f),
            true,
            0.15f,
            2.4f);

        SaveOwnedMaterial(ground, CorruptedGroundMaterialPath, expectedAssets[CorruptedGroundMaterialPath]);
        SaveOwnedMaterial(roots, RootsMaterialPath, expectedAssets[RootsMaterialPath]);
        SaveOwnedMaterial(core, CoreMaterialPath, expectedAssets[CoreMaterialPath]);
        SaveOwnedMaterial(particles, InfectionParticlesMaterialPath, expectedAssets[InfectionParticlesMaterialPath]);
    }

    private static Material CloneMaterial(string sourcePath, string targetPath, string marker)
    {
        Material source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
        if (source == null)
        {
            throw new InvalidOperationException("Required source material is missing: " + sourcePath);
        }

        Material target = AssetDatabase.LoadAssetAtPath<Material>(targetPath);
        if (target == null)
        {
            target = new Material(source);
            target.name = Path.GetFileNameWithoutExtension(targetPath);
            AssetDatabase.CreateAsset(target, targetPath);
        }
        else
        {
            EditorUtility.CopySerialized(source, target);
            target.name = Path.GetFileNameWithoutExtension(targetPath);
        }

        EditorUtility.SetDirty(target);
        AssetDatabase.SaveAssetIfDirty(target);
        SetOwnershipMarker(targetPath, marker);
        return AssetDatabase.LoadAssetAtPath<Material>(targetPath);
    }

    private static void ConfigureInfectionLitMaterial(
        Material material,
        Color baseColor,
        Color emission,
        float smoothness)
    {
        SetColorIfPresent(material, "_BaseColor", baseColor);
        SetColorIfPresent(material, "_Color", baseColor);
        SetColorIfPresent(material, "_EmissionColor", emission);
        SetFloatIfPresent(material, "_Metallic", 0f);
        SetFloatIfPresent(material, "_Smoothness", smoothness);
        material.enableInstancing = true;
        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
    }

    private static void ConfigureTransparentParticleMaterial(
        Material material,
        Texture texture,
        Color color,
        bool softParticles,
        float softNear,
        float softFar)
    {
        SetTextureIfPresent(material, "_BaseMap", texture);
        SetTextureIfPresent(material, "_MainTex", texture);
        SetColorIfPresent(material, "_BaseColor", color);
        SetColorIfPresent(material, "_Color", color);
        SetFloatIfPresent(material, "_Surface", 1f);
        SetFloatIfPresent(material, "_Blend", 0f);
        SetFloatIfPresent(material, "_ZWrite", 0f);
        SetFloatIfPresent(material, "_Cull", 2f);
        SetFloatIfPresent(material, "_SoftParticlesEnabled", softParticles ? 1f : 0f);
        SetFloatIfPresent(material, "_SoftParticlesNearFadeDistance", softNear);
        SetFloatIfPresent(material, "_SoftParticlesFarFadeDistance", softFar);
        SetFloatIfPresent(material, "_CameraFadingEnabled", 1f);
        SetFloatIfPresent(material, "_CameraNearFadeDistance", 0f);
        SetFloatIfPresent(material, "_CameraFarFadeDistance", 125f);
        UnityEditor.BaseShaderGUI.SetupMaterialBlendMode(material);
        UnityEditor.BaseShaderGUI.SetMaterialKeywords(
            material,
            null,
            UnityEditor.Rendering.Universal.ShaderGUI.ParticleGUI.SetMaterialKeywords);
        material.enableInstancing = true;
    }

    private static void SaveOwnedMaterial(Material material, string assetPath, string marker)
    {
        material.name = Path.GetFileNameWithoutExtension(assetPath);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
        SetOwnershipMarker(assetPath, marker);
    }

    private static void EnsureInfectedTreeAssets(IReadOnlyDictionary<string, string> expectedAssets)
    {
        Material growthMaterial = AssetDatabase.LoadAssetAtPath<Material>(RootsMaterialPath);
        if (growthMaterial == null)
        {
            throw new InvalidOperationException("Owned infected-root material is missing before tree generation.");
        }

        for (int treeIndex = 0; treeIndex < SourceTreePrefabPaths.Length; treeIndex++)
        {
            string sourcePrefabPath = SourceTreePrefabPaths[treeIndex];
            string targetPrefabPath = InfectedTreePrefabPaths[treeIndex];
            Dictionary<string, Material> materialMap = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (string sourceMaterialPath in CollectSourceMaterialPaths(sourcePrefabPath))
            {
                string targetMaterialPath = TreeMaterialTargetPath(treeIndex, sourceMaterialPath);
                Material material = CloneMaterial(
                    sourceMaterialPath,
                    targetMaterialPath,
                    expectedAssets[targetMaterialPath]);
                ConfigureHauntedTreeMaterial(material, treeIndex);
                SaveOwnedMaterial(material, targetMaterialPath, expectedAssets[targetMaterialPath]);
                materialMap[sourceMaterialPath] = AssetDatabase.LoadAssetAtPath<Material>(targetMaterialPath);
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(sourcePrefabPath);
            try
            {
                contents.name = Path.GetFileNameWithoutExtension(targetPrefabPath);
                foreach (Renderer renderer in contents.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int slot = 0; slot < materials.Length; slot++)
                    {
                        Material sourceMaterial = materials[slot];
                        if (sourceMaterial == null)
                        {
                            continue;
                        }

                        string sourceMaterialPath = AssetDatabase.GetAssetPath(sourceMaterial);
                        if (!materialMap.TryGetValue(sourceMaterialPath, out Material hauntedMaterial))
                        {
                            throw new InvalidOperationException(
                                "Could not map source tree material while creating infected variant: " + sourceMaterialPath);
                        }

                        materials[slot] = hauntedMaterial;
                    }
                    renderer.sharedMaterials = materials;
                }

                AddInfectedGrowth(contents.transform, growthMaterial, treeIndex);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(contents, targetPrefabPath);
                if (saved == null)
                {
                    throw new InvalidOperationException(
                        "Unity could not save infected tree prefab: " + targetPrefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            SetOwnershipMarker(targetPrefabPath, expectedAssets[targetPrefabPath]);
            GameObject generated = AssetDatabase.LoadAssetAtPath<GameObject>(targetPrefabPath);
            if (generated == null || generated.GetComponent<LODGroup>() == null ||
                generated.GetComponentsInChildren<Collider>(true).Length == 0)
            {
                throw new InvalidOperationException(
                    "Generated infected tree lost its production LODGroup or collider: " + targetPrefabPath);
            }
        }
    }

    private static void ConfigureHauntedTreeMaterial(Material material, int treeIndex)
    {
        Color barkTarget = treeIndex == 0
            ? new Color(0.075f, 0.065f, 0.055f, 1f)
            : new Color(0.09f, 0.075f, 0.06f, 1f);
        if (material.HasProperty("_BaseColor"))
        {
            Color original = material.GetColor("_BaseColor");
            barkTarget.a = original.a;
            material.SetColor("_BaseColor", Color.Lerp(original, barkTarget, 0.82f));
        }
        if (material.HasProperty("_Color"))
        {
            Color original = material.GetColor("_Color");
            Color dark = barkTarget;
            dark.a = original.a;
            material.SetColor("_Color", Color.Lerp(original, dark, 0.82f));
        }

        SetFloatIfPresent(material, "_Hue", -0.035f);
        SetFloatIfPresent(material, "_Saturation", 0.32f);
        SetFloatIfPresent(material, "_Value", 0.31f);
        SetFloatIfPresent(material, "_Metallic", 0f);
        SetFloatIfPresent(material, "_Smoothness", 0.025f);
        SetColorIfPresent(material, "Color_369F793F", new Color(0.075f, 0.11f, 0.075f, 1f));
        SetColorIfPresent(material, "Color_FA85148A", new Color(0.32f, 0.018f, 0.024f, 1f));
        SetColorIfPresent(material, "_EmissionColor", new Color(0.12f, 0.008f, 0.012f, 1f));
        SetFloatIfPresent(material, "_EmissionStrength", 0.28f);
        material.enableInstancing = true;
    }

    private static void AddInfectedGrowth(Transform root, Material growthMaterial, int variant)
    {
        Transform old = root.Find("Authored Infection Growth");
        if (old != null)
        {
            UnityEngine.Object.DestroyImmediate(old.gameObject);
        }

        GameObject growth = new GameObject("Authored Infection Growth");
        growth.transform.SetParent(root, false);
        float treeHeight = variant == 0 ? 5.5f : 7.2f;
        for (int index = 0; index < 3; index++)
        {
            GameObject tendril = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tendril.name = "Infected Tendril " + (index + 1).ToString("00");
            RemovePrimitiveCollider(tendril);
            tendril.transform.SetParent(growth.transform, false);
            float angle = 35f + index * 119f + variant * 21f;
            float radians = angle * Mathf.Deg2Rad;
            tendril.transform.localPosition = new Vector3(
                Mathf.Cos(radians) * 0.19f,
                treeHeight * (0.16f + index * 0.08f),
                Mathf.Sin(radians) * 0.19f);
            tendril.transform.localRotation = Quaternion.Euler(8f + index * 7f, angle, 9f - index * 8f);
            tendril.transform.localScale = new Vector3(0.055f, treeHeight * (0.15f + index * 0.025f), 0.055f);
            tendril.GetComponent<Renderer>().sharedMaterial = growthMaterial;
        }

        GameObject knot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        knot.name = "Infected Trunk Knot";
        RemovePrimitiveCollider(knot);
        knot.transform.SetParent(growth.transform, false);
        knot.transform.localPosition = new Vector3(0.15f, treeHeight * 0.34f, -0.12f);
        knot.transform.localScale = new Vector3(0.42f, 0.30f, 0.22f);
        knot.GetComponent<Renderer>().sharedMaterial = growthMaterial;

        LODGroup lodGroup = root.GetComponent<LODGroup>();
        if (lodGroup == null)
        {
            throw new InvalidOperationException("Infected tree source lost its required root LODGroup.");
        }

        LOD[] lods = lodGroup.GetLODs();
        if (lods.Length == 0)
        {
            throw new InvalidOperationException("Infected tree source has no authored LOD levels.");
        }

        Renderer[] growthRenderers = growth.GetComponentsInChildren<Renderer>(true);
        lods[0].renderers = lods[0].renderers
            .Concat(growthRenderers)
            .Where(renderer => renderer != null)
            .Distinct()
            .ToArray();
        lodGroup.SetLODs(lods);
        lodGroup.RecalculateBounds();
    }

    private static void EnsureInfectedPatchPrefab(string marker)
    {
        Material ground = AssetDatabase.LoadAssetAtPath<Material>(CorruptedGroundMaterialPath);
        Material roots = AssetDatabase.LoadAssetAtPath<Material>(RootsMaterialPath);
        Material core = AssetDatabase.LoadAssetAtPath<Material>(CoreMaterialPath);
        Material particles = AssetDatabase.LoadAssetAtPath<Material>(InfectionParticlesMaterialPath);
        if (ground == null || roots == null || core == null || particles == null)
        {
            throw new InvalidOperationException(
                "Owned infection materials are incomplete before infected-patch generation.");
        }

        GameObject root = new GameObject(Path.GetFileNameWithoutExtension(PatchPrefabPath));
        try
        {
            BoxCollider trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = PatchTriggerCenter;
            trigger.size = PatchTriggerSize;
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            NavMeshModifier modifier = root.AddComponent<NavMeshModifier>();
            modifier.enabled = true;
            modifier.ignoreFromBuild = true;
            modifier.applyToChildren = true;
            BloodrootInfectionZone zone = root.AddComponent<BloodrootInfectionZone>();
            SerializedObject serializedZone = new SerializedObject(zone);
            SerializedProperty infectionRate = serializedZone.FindProperty("infectionPerSecond");
            if (infectionRate == null)
            {
                throw new InvalidOperationException(
                    "BloodrootInfectionZone no longer exposes the expected infectionPerSecond field.");
            }
            infectionRate.floatValue = 20f;
            serializedZone.ApplyModifiedPropertiesWithoutUndo();

            GameObject apron = CreatePrimitiveChild(
                root.transform,
                PrimitiveType.Cylinder,
                "Corrupted Ground Apron",
                new Vector3(0f, 0.055f, 0f),
                Quaternion.identity,
                new Vector3(PatchApronDiameter, PatchApronThicknessScale, PatchApronDiameter),
                ground);
            apron.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            for (int index = 0; index < 8; index++)
            {
                float angle = index * 45f + (index % 2) * 8f;
                float radians = angle * Mathf.Deg2Rad;
                float length = (4.6f + (index % 3) * 0.9f) * PatchRootSizeMultiplier;
                CreatePrimitiveChild(
                    root.transform,
                    PrimitiveType.Cube,
                    "Root Spur " + (index + 1).ToString("00"),
                    new Vector3(Mathf.Cos(radians) * length * 0.42f, 0.13f, Mathf.Sin(radians) * length * 0.42f),
                    Quaternion.Euler(0f, 90f - angle, (index % 2 == 0 ? 3f : -4f)),
                    new Vector3(
                        length,
                        (0.16f + (index % 2) * 0.05f) * PatchRootSizeMultiplier,
                        (0.18f + (index % 3) * 0.04f) * PatchRootSizeMultiplier),
                    roots);
            }

            CreatePrimitiveChild(
                root.transform,
                PrimitiveType.Sphere,
                "Infection Core",
                new Vector3(0f, 0.78f, 0f),
                Quaternion.identity,
                new Vector3(2f, 1.38f, 2f),
                core);

            GameObject lightObject = new GameObject("Infection Practical Light");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.56f, 0.035f, 0.025f, 1f);
            light.intensity = 2.1f;
            light.range = 10f;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;

            GameObject particleObject = new GameObject("Infection Low Mist");
            particleObject.layer = LayerMask.NameToLayer("TransparentFX");
            particleObject.transform.SetParent(root.transform, false);
            particleObject.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            ParticleSystem particleSystem = particleObject.AddComponent<ParticleSystem>();
            ConfigureInfectionParticleSystem(particleSystem, particles);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PatchPrefabPath);
            if (saved == null)
            {
                throw new InvalidOperationException(
                    "Unity could not save the lightweight Black Pines infected-patch prefab.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }

        SetOwnershipMarker(PatchPrefabPath, marker);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PatchPrefabPath);
        BoxCollider savedTrigger = prefab == null ? null : prefab.GetComponent<BoxCollider>();
        ParticleSystem savedMist = prefab == null ? null : prefab.GetComponentInChildren<ParticleSystem>(true);
        Transform savedApron = prefab == null ? null : prefab.transform.Find("Corrupted Ground Apron");
        if (prefab == null || savedTrigger == null || savedMist == null || savedApron == null ||
            Vector3.Distance(savedTrigger.center, PatchTriggerCenter) > 0.001f ||
            Vector3.Distance(savedTrigger.size, PatchTriggerSize) > 0.001f ||
            Vector3.Distance(
                savedApron.localScale,
                new Vector3(PatchApronDiameter, PatchApronThicknessScale, PatchApronDiameter)) > 0.001f ||
            prefab.GetComponent<Rigidbody>() == null ||
            prefab.GetComponent<BloodrootInfectionZone>() == null ||
            prefab.GetComponent<NavMeshModifier>() == null ||
            !prefab.GetComponent<NavMeshModifier>().enabled ||
            !prefab.GetComponent<NavMeshModifier>().ignoreFromBuild ||
            !prefab.GetComponent<NavMeshModifier>().applyToChildren ||
            prefab.GetComponentsInChildren<Renderer>(true).Length > 12 ||
            prefab.GetComponentsInChildren<Light>(true).Length != 1 ||
            prefab.GetComponentsInChildren<ParticleSystem>(true).Length != 1)
        {
            throw new InvalidOperationException(
                "Generated Black Pines infected-patch prefab failed its lightweight component budget.");
        }
    }

    private static GameObject CreatePrimitiveChild(
        Transform parent,
        PrimitiveType type,
        string name,
        Vector3 localPosition,
        Quaternion localRotation,
        Vector3 localScale,
        Material material)
    {
        GameObject child = GameObject.CreatePrimitive(type);
        child.name = name;
        RemovePrimitiveCollider(child);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = localPosition;
        child.transform.localRotation = localRotation;
        child.transform.localScale = localScale;
        Renderer renderer = child.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.receiveShadows = true;
        return child;
    }

    private static void RemovePrimitiveCollider(GameObject gameObject)
    {
        Collider collider = gameObject.GetComponent<Collider>();
        if (collider != null)
        {
            UnityEngine.Object.DestroyImmediate(collider);
        }
    }

    private static void ConfigureInfectionParticleSystem(ParticleSystem particleSystem, Material material)
    {
        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particleSystem.useAutoRandomSeed = false;
        particleSystem.randomSeed = 98173u;

        ParticleSystem.MainModule main = particleSystem.main;
        main.loop = true;
        main.prewarm = true;
        main.duration = 5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3.8f, 6.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.12f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.30f, 0.018f, 0.025f, 0.18f),
            new Color(0.55f, 0.035f, 0.045f, 0.32f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = PatchMistMaximum;
        main.cullingMode = ParticleSystemCullingMode.PauseAndCatchup;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = PatchMistRate;

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = PatchMistRadius;
        shape.radiusThickness = 1f;

        ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.11f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f);

        ParticleSystem.NoiseModule noise = particleSystem.noise;
        noise.enabled = true;
        noise.strength = 0.18f;
        noise.frequency = 0.22f;
        noise.scrollSpeed = 0.08f;
        noise.damping = true;

        ParticleSystem.ColorOverLifetimeModule color = particleSystem.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(CreateMistGradient(0.22f, 0.16f));

        ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.alignment = ParticleSystemRenderSpace.View;
        particleSystem.Play();
    }

    private static void SetColorIfPresent(Material material, string property, Color value)
    {
        if (material.HasProperty(property))
        {
            material.SetColor(property, value);
        }
    }

    private static void SetFloatIfPresent(Material material, string property, float value)
    {
        if (material.HasProperty(property))
        {
            material.SetFloat(property, value);
        }
    }

    private static void SetTextureIfPresent(Material material, string property, Texture value)
    {
        if (material.HasProperty(property))
        {
            material.SetTexture(property, value);
        }
    }

    private static void ReconcileAtmosphere(Scene scene, Terrain terrain)
    {
        Transform area = FindSceneTransform(scene, AreaRootName);
        if (area == null)
        {
            throw new InvalidOperationException("Black Pines area root is missing: " + AreaRootName);
        }

        Transform environment = area.Find(EnvironmentRootName);
        if (environment == null)
        {
            throw new InvalidOperationException(
                "Black Pines Environment root is missing. The atmosphere tool will not invent a parallel hierarchy.");
        }

        Transform root = EnsureDirectChild(environment, AtmosphereRootName);
        root.gameObject.SetActive(true);
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;
        root.localScale = Vector3.one;

        Transform volumeRoot = EnsureDirectChild(root, VolumeObjectName);
        Transform fogRoot = EnsureDirectChild(root, FogRootName);
        Transform patchRoot = EnsureDirectChild(root, PatchRootName);
        Transform treeRoot = EnsureDirectChild(root, TreeRootName);
        volumeRoot.gameObject.SetActive(true);
        fogRoot.gameObject.SetActive(true);
        patchRoot.gameObject.SetActive(true);
        treeRoot.gameObject.SetActive(true);
        fogRoot.localPosition = Vector3.zero;
        fogRoot.localRotation = Quaternion.identity;
        fogRoot.localScale = Vector3.one;
        patchRoot.localPosition = Vector3.zero;
        patchRoot.localRotation = Quaternion.identity;
        patchRoot.localScale = Vector3.one;
        treeRoot.localPosition = Vector3.zero;
        treeRoot.localRotation = Quaternion.identity;
        treeRoot.localScale = Vector3.one;

        ConfigureLocalVolume(volumeRoot);
        ReconcileFogFields(fogRoot, terrain);
        ReconcilePatches(patchRoot, terrain, scene);
        ReconcileInfectedTrees(treeRoot, terrain, scene);
        UpdateAtmosphereSignature(root);

        string[] allowedDirectChildren =
        {
            VolumeObjectName,
            FogRootName,
            PatchRootName,
            TreeRootName,
            ExpectedSignatureName()
        };
        foreach (Transform child in root.Cast<Transform>().ToArray())
        {
            if (!allowedDirectChildren.Contains(child.name))
            {
                throw new InvalidOperationException(
                    "Unrecognized child exists under the generated Black Pines atmosphere root: " + child.name);
            }
        }

        SetSiblingIndex(volumeRoot, 0);
        SetSiblingIndex(fogRoot, 1);
        SetSiblingIndex(patchRoot, 2);
        SetSiblingIndex(treeRoot, 3);
        Transform signature = root.Find(ExpectedSignatureName());
        SetSiblingIndex(signature, 4);
    }

    private static Transform EnsureDirectChild(Transform parent, string name)
    {
        Transform[] matches = parent.Cast<Transform>()
            .Where(child => child.name == name)
            .ToArray();
        if (matches.Length > 1)
        {
            throw new InvalidOperationException(
                "Generated hierarchy contains duplicate children named: " + name);
        }

        if (matches.Length == 1)
        {
            return matches[0];
        }

        GameObject created = new GameObject(name);
        created.transform.SetParent(parent, false);
        return created.transform;
    }

    private static void SetSiblingIndex(Transform transform, int index)
    {
        if (transform != null)
        {
            transform.SetSiblingIndex(index);
        }
    }

    private static void ConfigureLocalVolume(Transform volumeRoot)
    {
        volumeRoot.gameObject.layer = 0;
        volumeRoot.position = VolumeCenter;
        volumeRoot.rotation = Quaternion.identity;
        volumeRoot.localScale = Vector3.one;

        BoxCollider collider = GetOrAddComponent<BoxCollider>(volumeRoot.gameObject);
        collider.enabled = true;
        collider.isTrigger = true;
        collider.center = Vector3.zero;
        collider.size = VolumeSize;

        Volume volume = GetOrAddComponent<Volume>(volumeRoot.gameObject);
        volume.enabled = true;
        volume.isGlobal = false;
        volume.priority = 20f;
        volume.blendDistance = 60f;
        volume.weight = 1f;
        volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (volume.sharedProfile == null)
        {
            throw new InvalidOperationException("Owned Black Pines atmosphere profile is missing.");
        }

        NavMeshModifier modifier = GetOrAddComponent<NavMeshModifier>(volumeRoot.gameObject);
        modifier.enabled = true;
        modifier.ignoreFromBuild = true;
        modifier.applyToChildren = false;
    }

    private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component == null ? gameObject.AddComponent<T>() : component;
    }

    private static void ReconcileFogFields(Transform fogRoot, Terrain terrain)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(FogMaterialPath);
        if (material == null)
        {
            throw new InvalidOperationException("Owned Black Pines low-fog material is missing.");
        }

        HashSet<string> expectedNames = new HashSet<string>(FogFields.Select(field => field.Name));
        foreach (Transform child in fogRoot.Cast<Transform>().ToArray())
        {
            if (!expectedNames.Contains(child.name))
            {
                throw new InvalidOperationException(
                    "Unrecognized child exists under the generated Black Pines fog root: " + child.name);
            }
        }

        int transparentLayer = LayerMask.NameToLayer("TransparentFX");
        if (transparentLayer < 0)
        {
            transparentLayer = 0;
        }

        for (int index = 0; index < FogFields.Length; index++)
        {
            FogFieldSpec spec = FogFields[index];
            Transform child = EnsureDirectChild(fogRoot, spec.Name);
            child.gameObject.SetActive(true);
            child.gameObject.layer = transparentLayer;
            child.position = new Vector3(
                spec.Point.x,
                WorldHeight(terrain, spec.Point) + 0.18f,
                spec.Point.y);
            child.rotation = Quaternion.identity;
            child.localScale = Vector3.one;
            ParticleSystem particleSystem = GetOrAddComponent<ParticleSystem>(child.gameObject);
            ConfigureFogParticleSystem(particleSystem, material, spec);
            child.SetSiblingIndex(index);
        }
    }

    private static void ConfigureFogParticleSystem(
        ParticleSystem particleSystem,
        Material material,
        FogFieldSpec spec)
    {
        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particleSystem.useAutoRandomSeed = false;
        particleSystem.randomSeed = spec.Seed;

        ParticleSystem.MainModule main = particleSystem.main;
        main.loop = true;
        main.prewarm = true;
        main.duration = 42f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(31f, 41f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.015f, 0.055f);
        main.startSize = new ParticleSystem.MinMaxCurve(24f, 46f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Color.white;
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = spec.Maximum;
        main.cullingMode = ParticleSystemCullingMode.PauseAndCatchup;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = spec.Rate;

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        float vertical = Mathf.Min(3.5f, spec.Size.y);
        shape.scale = new Vector3(spec.Size.x, vertical, spec.Size.z);
        shape.position = new Vector3(0f, vertical * 0.52f, 0f);

        ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.045f, 0.055f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.005f, 0.025f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.04f, 0.05f);

        ParticleSystem.NoiseModule noise = particleSystem.noise;
        noise.enabled = true;
        noise.separateAxes = true;
        noise.strengthX = new ParticleSystem.MinMaxCurve(0.16f, 0.30f);
        noise.strengthY = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
        noise.strengthZ = new ParticleSystem.MinMaxCurve(0.14f, 0.28f);
        noise.frequency = 0.075f;
        noise.scrollSpeed = 0.025f;
        noise.damping = true;
        noise.quality = ParticleSystemNoiseQuality.Medium;

        ParticleSystem.ColorOverLifetimeModule color = particleSystem.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(CreateMistGradient(0.27f, 0.20f));

        ParticleSystem.SizeOverLifetimeModule size = particleSystem.sizeOverLifetime;
        size.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.58f),
            new Keyframe(0.18f, 0.92f),
            new Keyframe(0.72f, 1f),
            new Keyframe(1f, 0.75f));
        size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ParticleSystem.CollisionModule collision = particleSystem.collision;
        collision.enabled = false;
        ParticleSystem.TriggerModule trigger = particleSystem.trigger;
        trigger.enabled = false;
        ParticleSystem.LightsModule lights = particleSystem.lights;
        lights.enabled = false;
        ParticleSystem.TrailModule trails = particleSystem.trails;
        trails.enabled = false;

        ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.allowRoll = true;
        particleSystem.Play();
    }

    private static Gradient CreateMistGradient(float peakAlpha, float lateAlpha)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.56f, 0.64f, 0.60f), 0f),
                new GradientColorKey(new Color(0.42f, 0.51f, 0.47f), 0.62f),
                new GradientColorKey(new Color(0.34f, 0.41f, 0.38f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(peakAlpha, 0.14f),
                new GradientAlphaKey(lateAlpha, 0.72f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    private static void ReconcilePatches(Transform patchRoot, Terrain terrain, Scene scene)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PatchPrefabPath);
        if (prefab == null)
        {
            throw new InvalidOperationException("Owned Black Pines infected-patch prefab is missing.");
        }

        HashSet<string> expectedNames = new HashSet<string>(Patches.Select(spec => spec.Name));
        foreach (Transform child in patchRoot.Cast<Transform>().ToArray())
        {
            if (!expectedNames.Contains(child.name))
            {
                throw new InvalidOperationException(
                    "Unrecognized child exists under the generated Black Pines patch root: " + child.name);
            }
        }

        for (int index = 0; index < Patches.Length; index++)
        {
            PatchSpec spec = Patches[index];
            GameObject instance = EnsurePrefabInstance(
                patchRoot,
                spec.Name,
                prefab,
                PatchPrefabPath,
                scene);
            instance.transform.position = new Vector3(
                spec.Point.x,
                WorldHeight(terrain, spec.Point),
                spec.Point.y);
            instance.transform.rotation = Quaternion.Euler(0f, spec.Yaw, 0f);
            instance.transform.localScale = Vector3.one;
            instance.SetActive(true);

            BloodrootInfectionZone zone = instance.GetComponent<BloodrootInfectionZone>();
            BoxCollider trigger = instance.GetComponent<BoxCollider>();
            Rigidbody body = instance.GetComponent<Rigidbody>();
            if (zone == null || trigger == null || body == null)
            {
                throw new InvalidOperationException(
                    "Generated infected patch instance is missing its mechanical root components: " + spec.Name);
            }
            zone.enabled = spec.Mechanical;
            trigger.enabled = spec.Mechanical;
            trigger.isTrigger = true;
            trigger.center = PatchTriggerCenter;
            trigger.size = PatchTriggerSize;
            body.isKinematic = true;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            instance.transform.SetSiblingIndex(index);
        }
    }

    private static void ReconcileInfectedTrees(Transform treeRoot, Terrain terrain, Scene scene)
    {
        GameObject[] prefabs = InfectedTreePrefabPaths
            .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
            .ToArray();
        if (prefabs.Any(prefab => prefab == null))
        {
            throw new InvalidOperationException("One or more owned infected-tree prefabs are missing.");
        }

        HashSet<string> expectedNames = new HashSet<string>(InfectedTrees.Select(spec => spec.Name));
        foreach (Transform child in treeRoot.Cast<Transform>().ToArray())
        {
            if (!expectedNames.Contains(child.name))
            {
                throw new InvalidOperationException(
                    "Unrecognized child exists under the generated Black Pines infected-tree root: " + child.name);
            }
        }

        for (int index = 0; index < InfectedTrees.Length; index++)
        {
            TreePlacementSpec spec = InfectedTrees[index];
            GameObject instance = EnsurePrefabInstance(
                treeRoot,
                spec.Name,
                prefabs[spec.PrefabIndex],
                InfectedTreePrefabPaths[spec.PrefabIndex],
                scene);
            instance.transform.position = new Vector3(
                spec.Point.x,
                WorldHeight(terrain, spec.Point),
                spec.Point.y);
            instance.transform.rotation = Quaternion.Euler(0f, spec.Yaw, 0f);
            instance.transform.localScale = Vector3.one * spec.Scale;
            instance.SetActive(true);
            instance.transform.SetSiblingIndex(index);
        }
    }

    private static GameObject EnsurePrefabInstance(
        Transform parent,
        string instanceName,
        GameObject prefab,
        string prefabPath,
        Scene scene)
    {
        Transform existing = parent.Cast<Transform>()
            .SingleOrDefault(child => child.name == instanceName);
        GameObject instance = existing == null ? null : existing.gameObject;
        if (instance != null)
        {
            string existingPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance);
            if (existingPath != prefabPath)
            {
                throw new InvalidOperationException(
                    "Recognized atmosphere instance name points at the wrong prefab: " + instanceName);
            }

            PrefabUtility.RevertPrefabInstance(instance, InteractionMode.AutomatedAction);
        }
        else
        {
            instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException("Unity could not instantiate atmosphere prefab: " + prefabPath);
            }
            instance.transform.SetParent(parent, true);
        }

        instance.name = instanceName;
        return instance;
    }

    private static void UpdateAtmosphereSignature(Transform root)
    {
        string expected = ExpectedSignatureName();
        Transform[] signatures = root.Cast<Transform>()
            .Where(child => child.name.StartsWith(SignaturePrefix, StringComparison.Ordinal))
            .ToArray();
        if (signatures.Length > 1)
        {
            throw new InvalidOperationException(
                "Generated Black Pines atmosphere contains duplicate semantic signatures.");
        }

        Transform signature;
        if (signatures.Length == 0)
        {
            signature = new GameObject(expected).transform;
            signature.SetParent(root, false);
        }
        else
        {
            signature = signatures[0];
            signature.name = expected;
        }

        signature.gameObject.SetActive(true);
        signature.localPosition = Vector3.zero;
        signature.localRotation = Quaternion.identity;
        signature.localScale = Vector3.one;
    }

    private static string ExpectedSignatureName()
    {
        ulong hash = 1469598103934665603UL;
        HashString(ref hash, "BloodrootBlackPinesAtmosphere|v2|full-area-fog|dense-pockets|expanded-patches|fifteen-trees");
        HashVector3(ref hash, VolumeCenter);
        HashVector3(ref hash, VolumeSize);
        HashFloat(ref hash, BlackPinesFogCenter.x);
        HashFloat(ref hash, BlackPinesFogCenter.y);
        HashFloat(ref hash, BlackPinesFogRadii.x);
        HashFloat(ref hash, BlackPinesFogRadii.y);
        HashUInt(ref hash, (uint)FogParticleBudgetCeiling);
        foreach (FogFieldSpec field in FogFields)
        {
            HashString(ref hash, field.Name);
            HashFloat(ref hash, field.Point.x);
            HashFloat(ref hash, field.Point.y);
            HashVector3(ref hash, field.Size);
            HashUInt(ref hash, field.Seed);
            HashFloat(ref hash, field.Rate);
            HashUInt(ref hash, (uint)field.Maximum);
            HashUInt(ref hash, field.BaseCoverage ? 1u : 0u);
        }
        HashFloat(ref hash, PatchApronDiameter);
        HashFloat(ref hash, PatchApronThicknessScale);
        HashFloat(ref hash, PatchRootSizeMultiplier);
        HashVector3(ref hash, PatchTriggerCenter);
        HashVector3(ref hash, PatchTriggerSize);
        HashFloat(ref hash, PatchMistRadius);
        HashFloat(ref hash, PatchMistRate);
        HashUInt(ref hash, (uint)PatchMistMaximum);
        foreach (PatchSpec patch in Patches)
        {
            HashString(ref hash, patch.Name);
            HashFloat(ref hash, patch.Point.x);
            HashFloat(ref hash, patch.Point.y);
            HashUInt(ref hash, patch.Mechanical ? 1u : 0u);
            HashFloat(ref hash, patch.Yaw);
        }
        foreach (TreePlacementSpec tree in InfectedTrees)
        {
            HashString(ref hash, tree.Name);
            HashUInt(ref hash, (uint)tree.PrefabIndex);
            HashFloat(ref hash, tree.Point.x);
            HashFloat(ref hash, tree.Point.y);
            HashFloat(ref hash, tree.Scale);
            HashFloat(ref hash, tree.Yaw);
        }
        return SignaturePrefix + hash.ToString("X16");
    }

    private static void HashString(ref ulong hash, string value)
    {
        foreach (char character in value)
        {
            hash ^= character;
            hash *= 1099511628211UL;
        }
    }

    private static void HashFloat(ref ulong hash, float value)
    {
        foreach (byte item in BitConverter.GetBytes(value))
        {
            hash ^= item;
            hash *= 1099511628211UL;
        }
    }

    private static void HashUInt(ref ulong hash, uint value)
    {
        foreach (byte item in BitConverter.GetBytes(value))
        {
            hash ^= item;
            hash *= 1099511628211UL;
        }
    }

    private static void HashVector3(ref ulong hash, Vector3 value)
    {
        HashFloat(ref hash, value.x);
        HashFloat(ref hash, value.y);
        HashFloat(ref hash, value.z);
    }

    private static void RunAtmosphereBuild(bool allowExisting)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Black Pines Atmosphere Unavailable",
                "Exit Play Mode before building the Black Pines atmosphere.",
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
        byte[] terrainBytesBefore = null;
        List<GeneratedAssetSnapshot> assetSnapshots = null;
        HashSet<string> foldersCreated = null;
        bool mutationStarted = false;
        bool committed = false;
        bool rollbackCompleted = false;

        try
        {
            SceneManager.SetActiveScene(scene);
            if (scene.isDirty)
            {
                throw new InvalidOperationException(
                    "Bloodroot_OpenWorld has unsaved changes. Save or revert them before running the deterministic atmosphere pass.");
            }

            Terrain terrain = FindProductionTerrain(scene);
            TerrainData data = terrain.terrainData;
            if (EditorUtility.IsDirty(data))
            {
                throw new InvalidOperationException(
                    "The production TerrainData has unsaved changes. Save or revert them before building atmosphere.");
            }

            ValidateNaturalDressingBoundary(scene, data);
            Transform existingRoot = FindAtmosphereRoot(scene);
            IReadOnlyDictionary<string, string> expectedAssets = BuildExpectedAssetMarkers();
            if (!allowExisting)
            {
                if (existingRoot != null || expectedAssets.Keys.Any(AssetOrMetaExists))
                {
                    throw new InvalidOperationException(
                        "Black Pines atmosphere output already exists. Use Rebuild only when the existing root and every generated asset validate as recognized production output.");
                }
            }
            else
            {
                if (existingRoot == null)
                {
                    throw new InvalidOperationException(
                        "Rebuild requires the recognized Black Pines atmosphere hierarchy. No owned root was found.");
                }

                ValidateRecognizedAtmosphereForRebuild(scene, existingRoot, expectedAssets);
            }

            EnsurePersistentBackupPair();
            assetSnapshots = CaptureGeneratedAssetSnapshots(expectedAssets.Keys);
            foldersCreated = CaptureAndCreateOutputFolders();

            if (AssetOrMetaExists(TerrainRepairTempPath))
            {
                throw new InvalidOperationException(
                    "A prior Black Pines atmosphere TerrainData repair temp exists. Inspect that recovery state before retrying.");
            }

            if (!AssetDatabase.CopyAsset(ProductionTerrainPath, TerrainRepairTempPath))
            {
                throw new InvalidOperationException(
                    "Could not create the temporary TerrainData rollback copy.");
            }

            AssetDatabase.ImportAsset(TerrainRepairTempPath, ImportAssetOptions.ForceSynchronousImport);
            repairBackup = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainRepairTempPath);
            if (repairBackup == null)
            {
                throw new InvalidOperationException(
                    "The temporary TerrainData rollback copy could not be loaded.");
            }

            sceneBytes = File.ReadAllBytes(ProjectAbsolutePath(OpenWorldScene));
            terrainBytesBefore = File.ReadAllBytes(ProjectAbsolutePath(ProductionTerrainPath));
            mutationStarted = true;

            EnsureGeneratedAssets(expectedAssets);
            ReconcileAtmosphere(scene, terrain);
            EditorSceneManager.MarkSceneDirty(scene);
            ValidateAtmosphere(scene, terrain, false);

            if (!EditorSceneManager.SaveScene(scene, OpenWorldScene))
            {
                throw new InvalidOperationException(
                    "Unity could not save the Black Pines atmosphere into the open-world scene.");
            }

            byte[] terrainBytesAfter = File.ReadAllBytes(ProjectAbsolutePath(ProductionTerrainPath));
            if (!terrainBytesBefore.SequenceEqual(terrainBytesAfter))
            {
                throw new InvalidOperationException(
                    "The atmosphere pass changed production TerrainData bytes. The run will be rolled back.");
            }

            if (EditorUtility.IsDirty(data))
            {
                throw new InvalidOperationException(
                    "The atmosphere pass dirtied production TerrainData. The run will be rolled back.");
            }

            if (!AssetDatabase.DeleteAsset(TerrainRepairTempPath))
            {
                throw new InvalidOperationException(
                    "The atmosphere was saved, but Unity could not remove the temporary TerrainData backup. The run will be restored.");
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ValidateAtmosphere(scene, terrain, true);
            committed = true;
            Transform root = FindAtmosphereRoot(scene);
            if (root != null)
            {
                Selection.activeGameObject = root.gameObject;
                if (SceneView.lastActiveSceneView != null)
                {
                    SceneView.lastActiveSceneView.LookAt(
                        new Vector3(-285f, 7f, -235f),
                        Quaternion.Euler(18f, 32f, 0f),
                        150f);
                    SceneView.lastActiveSceneView.Repaint();
                }
            }

            Debug.Log("Bloodroot Black Pines atmosphere built and validated. " + BuildValidationSummary());
            EditorUtility.DisplayDialog(
                "Black Pines Atmosphere Complete",
                BuildValidationSummary() +
                "\n\nThe local grade darkens and desaturates every tree seen inside Black Pines; fifteen authored tree variants carry visible infection around five patches.",
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
                    RestoreTerrainFromRepairCopy(repairBackup, terrainBytesBefore);
                    RestoreGeneratedAssetSnapshots(assetSnapshots);

                    if (sceneBytes != null)
                    {
                        AssetDatabase.ReleaseCachedFileHandles();
                        File.WriteAllBytes(ProjectAbsolutePath(OpenWorldScene), sceneBytes);
                        AssetDatabase.ImportAsset(OpenWorldScene, ImportAssetOptions.ForceSynchronousImport);
                        ReloadTargetScenePreservingOthers(scene);
                    }

                    RemoveCreatedFoldersIfEmpty(foldersCreated);
                    rollbackCompleted = true;
                }
                catch (Exception rollbackException)
                {
                    Debug.LogException(rollbackException);
                }
            }

            if (rollbackCompleted && AssetOrMetaExists(TerrainRepairTempPath))
            {
                AssetDatabase.DeleteAsset(TerrainRepairTempPath);
            }

            EditorUtility.DisplayDialog(
                "Black Pines Atmosphere Failed",
                exception.Message +
                (rollbackCompleted
                    ? "\n\nThe saved scene, generated assets, and production TerrainData were restored."
                    : "\n\nAutomatic rollback also failed. The atmosphere TerrainData repair copy was retained for manual recovery; inspect the Console before changing the scene."),
                "OK");
        }
        finally
        {
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
}
