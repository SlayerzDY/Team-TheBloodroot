#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bloodroot.Campaign;
using Bloodroot.Features.AlphaEnemies;
using Bloodroot.Features.FarmPrologue;
using Bloodroot.Features.WorldMissions;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps authored enemy spawn markers attached to a real Walkable NavMesh
/// polygon and to the physical ground that produced that polygon. This is an
/// editor-only repair pass: runtime spawners retain their own defensive
/// NavMesh checks, while authored markers can never remain in the air or
/// below terrain after a scene edit.
/// </summary>
public static class BloodrootNavMeshSpawnGroundingRepair
{
    private const string FarmScenePath =
        "Assets/Scenes/Campaign/Farm_PrologueHub.unity";
    private const string FarmNavMeshDataPath =
        "Assets/Scenes/Campaign/Farm_PrologueHub/NavMesh-Level NavMesh Surface.asset";
    private const string OpenWorldScenePath =
        "Assets/Scenes/OpenWorld/Bloodroot_OpenWorld.unity";
    private const int WalkableAreaMask = 1;
    private const float MarkerSampleDistance = 18f;
    private const float MaximumHorizontalCorrection = 8f;
    private const float MaximumVerticalCorrection = 12f;
    // Mirror the runtime grounded-spawn guard: an agent may not be suspended
    // more than 0.15m above a solid support, and terrain may not protrude more
    // than 0.05m above the NavMesh point.
    private const float GroundSupportTolerance = 0.15f;
    private const float BelowTerrainTolerance = 0.05f;
    private const float SpawnVolumeSampleDistance = 0.75f;
    private const float MinimumSafeSpawnVolumeSize = 0.5f;
    private const float GeometrySupportTolerance = 0.08f;
    private const float GeometryMaximumAuditDrop = 2.5f;
    private const float GeometryMaximumSupportPenetration = 0.20f;
    private const float GeometryRepairTolerance = 0.035f;
    private const float GeometryMaximumRepairDrop = 3f;

    private readonly struct FarmSpawnVolumeSpec
    {
        public FarmSpawnVolumeSpec(
            string markerName,
            Vector2 center,
            Vector2 localSize)
        {
            MarkerName = markerName;
            Center = center;
            LocalSize = localSize;
        }

        public string MarkerName { get; }
        public Vector2 Center { get; }
        public Vector2 LocalSize { get; }
    }

    // These three transforms are shared by the Farm's initial MobSpawner and
    // recurring emergence director. Their compact volumes sit well inside the
    // fence and away from the chore interaction cluster.
    private static readonly FarmSpawnVolumeSpec[] FarmSpawnVolumes =
    {
        new FarmSpawnVolumeSpec(
            "EMERGENCE_ZONE_01",
            new Vector2(45f, 27f),
            new Vector2(12f, 4f)),
        new FarmSpawnVolumeSpec(
            "EMERGENCE_ZONE_02",
            new Vector2(27.2f, -28.3f),
            new Vector2(24f, 4f)),
        new FarmSpawnVolumeSpec(
            "EMERGENCE_ZONE_03",
            new Vector2(41f, -31.3f),
            new Vector2(8f, 4f))
    };

    // Conservative interior polygon, inset from every physical fence edge.
    // Proving the complete spawn rectangle is inside this polygon also proves
    // random points selected by MobSpawner cannot land outside the Farm pen.
    private static readonly Vector2[] FarmFenceInterior =
    {
        new Vector2(22f, -67f),
        new Vector2(93f, -67f),
        new Vector2(93f, 30f),
        new Vector2(29f, 30f),
        new Vector2(22f, 22f)
    };

    [MenuItem("Tools/Bloodroot/Navigation/Repair Campaign Spawn Grounding")]
    public static void RepairAndValidateFromMenu()
    {
        RepairAndValidate();
    }

    /// <summary>
    /// Batch-safe entry point. It validates the Farm navigation that remains
    /// after hub cleanup, then repairs only marker transforms/volumes in the
    /// Farm and Open World scenes. It never rebuilds hub content.
    /// </summary>
    public static void RepairAndValidate()
    {
        EnsureIdleEditor();

        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
        bool restoreSetup = !Application.isBatchMode &&
                            originalSetup != null &&
                            originalSetup.Length > 0;
        try
        {
            int farmChanges;
            int farmMarkers = RepairFarmSpawnMarkers(out farmChanges);
            int worldChanges;
            int worldMarkers = RepairOpenWorldSpawnMarkers(out worldChanges);

            AssetDatabase.SaveAssets();
            Console.WriteLine(
                "BLOODROOT_NAVMESH_SPAWN_GROUNDING: PASS " +
                "farmMarkers=" + farmMarkers +
                " farmChanges=" + farmChanges +
                " openWorldMarkers=" + worldMarkers +
                " openWorldChanges=" + worldChanges +
                " walkableArea=" + WalkableAreaMask + ".");
        }
        finally
        {
            if (restoreSetup)
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }
        }
    }

    /// <summary>
    /// Read-only live-scene diagnostic for static visual placements. Detailed
    /// architecture keeps its authored compound colliders and exact deployment
    /// poses; this audit identifies loose unsupported visuals, missing solid
    /// collision, and stair meshes without local physical collision.
    /// </summary>
    [MenuItem("Tools/Bloodroot/Navigation/Audit Campaign Geometry Integrity")]
    public static void AuditCampaignGeometryIntegrity()
    {
        EnsureIdleEditor();

        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
        bool restoreSetup = !Application.isBatchMode &&
                            originalSetup != null &&
                            originalSetup.Length > 0;
        try
        {
            AuditSceneGeometry(FarmScenePath, "Farm Prologue/Hub");
            AuditSceneGeometry(OpenWorldScenePath, "Open World");
            Console.WriteLine("BLOODROOT_CAMPAIGN_GEOMETRY_AUDIT: PASS_COMPLETED");
        }
        finally
        {
            if (restoreSetup)
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }
        }
    }

    /// <summary>
    /// Read-only route proof against the current Safety campaign topology.
    /// It intentionally does not use the retired six-objective Alpha mission
    /// validator: the current campaign has one progression tower per area.
    /// </summary>
    [MenuItem("Tools/Bloodroot/Navigation/Validate Campaign Geometry Traversal")]
    public static void ValidateCampaignGeometryTraversal()
    {
        EnsureIdleEditor();

        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
        bool restoreSetup = !Application.isBatchMode &&
                            originalSetup != null &&
                            originalSetup.Length > 0;
        try
        {
            Scene farm = EditorSceneManager.OpenScene(
                FarmScenePath,
                OpenSceneMode.Single);
            NavMeshSurface farmSurface = RequireSingleSurface(
                farm,
                "Farm Prologue/Hub");
            EnsureSurfaceDataRegistered(farmSurface, "Farm Prologue/Hub");
            int farmRoutes = ValidateFarmObjectiveTraversal(farm);

            Scene world = EditorSceneManager.OpenScene(
                OpenWorldScenePath,
                OpenSceneMode.Single);
            NavMeshSurface worldSurface = RequireSingleSurface(
                world,
                "Open World");
            EnsureSurfaceDataRegistered(worldSurface, "Open World");
            int activeLinks = RefreshActiveNavMeshLinks(world);
            int markerRoutes = ValidateRouteMarkerContainers(world);
            int namedRoutes = ValidateOpenWorldNamedTraversal(world);

            Console.WriteLine(
                "BLOODROOT_CAMPAIGN_GEOMETRY_TRAVERSAL: PASS " +
                "farmObjectiveRoutes=" + farmRoutes + " " +
                "activeOpenWorldLinks=" + activeLinks + " " +
                "openWorldMarkerRoutes=" + markerRoutes + " " +
                "openWorldNamedRoutes=" + namedRoutes + ".");
        }
        finally
        {
            if (restoreSetup)
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }
        }
    }

    /// <summary>
    /// Grounds only known ground-contact scenery, restores the authored
    /// low-poly forest collision that scene dressing disabled, and fits
    /// collision to the small number of Farm meshes that have no authored
    /// collision. Enterable architecture retains its source-prefab compound
    /// walls, doorway gaps, floors, and physical stair ramps.
    /// </summary>
    [MenuItem("Tools/Bloodroot/Navigation/Repair Campaign Geometry Integrity")]
    public static void RepairCampaignGeometryIntegrity()
    {
        EnsureIdleEditor();

        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
        bool restoreSetup = !Application.isBatchMode &&
                            originalSetup != null &&
                            originalSetup.Length > 0;
        try
        {
            int farmGrounded;
            int farmColliders;
            RepairSceneGeometry(
                FarmScenePath,
                "Farm Prologue/Hub",
                true,
                out farmGrounded,
                out farmColliders);

            int worldGrounded;
            int worldColliders;
            RepairSceneGeometry(
                OpenWorldScenePath,
                "Open World",
                false,
                out worldGrounded,
                out worldColliders);

            AssetDatabase.SaveAssets();
            Console.WriteLine(
                "BLOODROOT_CAMPAIGN_GEOMETRY_REPAIR: PASS " +
                "farmGrounded=" + farmGrounded + " " +
                "farmColliders=" + farmColliders + " " +
                "openWorldGrounded=" + worldGrounded + " " +
                "openWorldColliders=" + worldColliders + ".");
        }
        finally
        {
            if (restoreSetup)
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }
        }
    }

    /// <summary>
    /// Rebuilds the shared Farm Prologue/Hub and Open World NavMeshSurfaces in
    /// isolation, then validates every current campaign enemy spawn marker
    /// against the new navigation data. Both canonical asset GUIDs remain
    /// stable so authored scene references are preserved.
    /// </summary>
    [MenuItem("Tools/Bloodroot/Navigation/Bake and Repair Campaign Navigation")]
    public static void BakeAndRepairCampaignNavigation()
    {
        EnsureIdleEditor();

        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
        bool restoreSetup = !Application.isBatchMode &&
                            originalSetup != null &&
                            originalSetup.Length > 0;
        try
        {
            // Prologue and Hub are two states of this one Farm scene, so the
            // single focused Farm bake updates navigation for both states.
            RebuildFarmNavMeshOnly();
            RebuildOpenWorldNavMeshOnly();
            RepairAndValidate();
            Console.WriteLine(
                "BLOODROOT_CAMPAIGN_NAVMESH_BAKE: PASS " +
                "farm=Farm_PrologueHub openWorld=Bloodroot_OpenWorld.");
        }
        finally
        {
            if (restoreSetup)
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }
        }
    }

    /// <summary>
    /// Rebuilds the shared Farm Prologue/Hub NavMeshSurface in isolation and
    /// replaces only the canonical NavMesh asset contents. The existing meta
    /// file and GUID remain unchanged so every authored reference stays valid.
    /// </summary>
    private static void RebuildFarmNavMeshOnly()
    {
        Scene scene = EditorSceneManager.OpenScene(
            FarmScenePath,
            OpenSceneMode.Single);
        NavMeshSurface surface = RequireSingleSurface(
            scene,
            "Farm Prologue/Hub");
        NavMeshData canonicalData = surface.navMeshData;
        string canonicalPath = AssetDatabase.GetAssetPath(canonicalData);
        string canonicalGuid = AssetDatabase.AssetPathToGUID(canonicalPath);
        if (!string.Equals(
                canonicalPath,
                FarmNavMeshDataPath,
                StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(canonicalGuid))
        {
            throw new InvalidOperationException(
                "Farm Prologue/Hub NavMeshSurface must reference the canonical " +
                "saved NavMesh asset at '" + FarmNavMeshDataPath + "'.");
        }

        Physics.SyncTransforms();
        surface.BuildNavMesh();
        NavMeshData rebuiltData = surface.navMeshData;
        if (rebuiltData == null || rebuiltData == canonicalData)
        {
            throw new InvalidOperationException(
                "Farm Prologue/Hub NavMeshSurface did not produce new " +
                "navigation data.");
        }

        CommitRebuiltNavMeshData(
            surface,
            rebuiltData,
            canonicalData,
            canonicalPath,
            canonicalGuid,
            "Farm Prologue/Hub");
        EnsureSurfaceDataRegistered(surface, "Farm Prologue/Hub");
        EditorUtility.SetDirty(surface);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, FarmScenePath))
        {
            throw new InvalidOperationException(
                "Unity could not save the focused Farm Prologue/Hub " +
                "NavMesh bake.");
        }

        AssetDatabase.SaveAssets();
        Console.WriteLine(
            "BLOODROOT_FARM_NAVMESH_BAKE: PASS guid=" +
            canonicalGuid + ".");
    }

    /// <summary>
    /// Rebuilds only the currently authored Open World NavMeshSurface. It does
    /// not invoke the retired alpha-mission builder because that builder owns
    /// a superseded multi-objective campaign contract. The replacement keeps
    /// the canonical NavMesh asset GUID stable, restores temporary scene
    /// activation/collider state before saving, then validates every current
    /// enemy marker against the new mesh.
    /// </summary>
    [MenuItem("Tools/Bloodroot/Navigation/Bake and Repair Open World Spawn Grounding")]
    public static void BakeAndRepairOpenWorld()
    {
        EnsureIdleEditor();
        RebuildOpenWorldNavMeshOnly();
        RepairAndValidate();
    }

    private static void RebuildOpenWorldNavMeshOnly()
    {
        Scene scene = EditorSceneManager.OpenScene(
            OpenWorldScenePath,
            OpenSceneMode.Single);
        NavMeshSurface surface = RequireSingleSurface(scene, "Open World");
        NavMeshData canonicalData = surface.navMeshData;
        string canonicalPath = AssetDatabase.GetAssetPath(canonicalData);
        string canonicalGuid = AssetDatabase.AssetPathToGUID(canonicalPath);
        if (string.IsNullOrWhiteSpace(canonicalPath) ||
            string.IsNullOrWhiteSpace(canonicalGuid))
        {
            throw new InvalidOperationException(
                "Open World NavMeshSurface must reference a canonical saved NavMesh asset.");
        }

        Dictionary<GameObject, bool> activeStates = null;
        Dictionary<Collider, bool> gateColliderStates = null;

        try
        {
            activeStates = ActivateOpenWorldMissionRoots(scene);
            gateColliderStates = DisableThornVeilBakeColliders(scene);
            Physics.SyncTransforms();

            surface.BuildNavMesh();
            NavMeshData rebuiltData = surface.navMeshData;
            if (rebuiltData == null || rebuiltData == canonicalData)
            {
                throw new InvalidOperationException(
                    "Open World NavMeshSurface did not produce a new NavMeshData instance.");
            }

            CommitRebuiltNavMeshData(
                surface,
                rebuiltData,
                canonicalData,
                canonicalPath,
                canonicalGuid,
                "Open World");
            RestoreThornVeilBakeColliders(gateColliderStates);
            gateColliderStates = null;
            RestoreActiveStates(activeStates);
            activeStates = null;
            Physics.SyncTransforms();

            // Validate the rebaked asset before committing the scene's
            // existing reference. Marker validation below performs the wider
            // current-campaign audit after this focused bake returns.
            EnsureSurfaceDataRegistered(surface, "Open World");
            EditorUtility.SetDirty(surface);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, OpenWorldScenePath))
            {
                throw new InvalidOperationException(
                    "Unity could not save the focused Open World NavMesh bake.");
            }

            AssetDatabase.SaveAssets();
            Console.WriteLine(
                "BLOODROOT_OPEN_WORLD_NAVMESH_BAKE: PASS guid=" +
                canonicalGuid + ".");
        }
        catch
        {
            try
            {
                RestoreThornVeilBakeColliders(gateColliderStates);
                RestoreActiveStates(activeStates);
            }
            catch
            {
                // Preserve the original bake failure. The scene has not been
                // saved until the success path, so reopening remains safe.
            }

            throw;
        }
    }

    private static void CommitRebuiltNavMeshData(
        NavMeshSurface surface,
        NavMeshData rebuiltData,
        NavMeshData canonicalData,
        string canonicalPath,
        string canonicalGuid,
        string label)
    {
        NavMeshData rollbackData = UnityEngine.Object.Instantiate(canonicalData);
        bool canonicalUpdated = false;

        try
        {
            surface.RemoveData();
            canonicalUpdated = true;
            EditorUtility.CopySerialized(rebuiltData, canonicalData);
            canonicalData.name = Path.GetFileNameWithoutExtension(canonicalPath);
            EditorUtility.SetDirty(canonicalData);
            AssetDatabase.SaveAssetIfDirty(canonicalData);

            if (!string.Equals(
                    AssetDatabase.GetAssetPath(canonicalData),
                    canonicalPath,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    AssetDatabase.AssetPathToGUID(canonicalPath),
                    canonicalGuid,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Focused " + label + " bake did not preserve the " +
                    "canonical NavMesh asset path and GUID.");
            }

            surface.navMeshData = canonicalData;
            surface.AddData();
        }
        catch (Exception commitException)
        {
            try
            {
                surface.RemoveData();
                if (canonicalUpdated)
                {
                    EditorUtility.CopySerialized(rollbackData, canonicalData);
                    EditorUtility.SetDirty(canonicalData);
                    AssetDatabase.SaveAssetIfDirty(canonicalData);
                }

                surface.navMeshData = canonicalData;
                if (surface.isActiveAndEnabled)
                {
                    surface.AddData();
                }
            }
            catch (Exception rollbackException)
            {
                throw new InvalidOperationException(
                    label + " NavMesh commit failed and its in-memory " +
                    "rollback also failed.",
                    new AggregateException(commitException, rollbackException));
            }

            throw;
        }
        finally
        {
            if (rebuiltData != null && !EditorUtility.IsPersistent(rebuiltData))
            {
                UnityEngine.Object.DestroyImmediate(rebuiltData);
            }

            if (rollbackData != null)
            {
                UnityEngine.Object.DestroyImmediate(rollbackData);
            }
        }
    }

    private static Dictionary<GameObject, bool> ActivateOpenWorldMissionRoots(
        Scene scene)
    {
        string[] rootNames =
        {
            "Black Pines Mission Systems",
            "Stillwater Mission Systems",
            "Harrow Estate Mission Systems",
            "Bloodroot Hollow Boss Systems"
        };
        var states = new Dictionary<GameObject, bool>();
        foreach (string rootName in rootNames)
        {
            GameObject root = FindComponents<Transform>(scene)
                .Select(candidate => candidate != null
                    ? candidate.gameObject
                    : null)
                .FirstOrDefault(candidate => candidate != null &&
                    string.Equals(candidate.name, rootName,
                        StringComparison.Ordinal));
            if (root == null)
            {
                throw new InvalidOperationException(
                    "Focused Open World bake is missing mission root '" +
                    rootName + "'.");
            }

            states[root] = root.activeSelf;
            if (!root.activeSelf)
            {
                root.SetActive(true);
            }
        }

        return states;
    }

    private static Dictionary<Collider, bool> DisableThornVeilBakeColliders(
        Scene scene)
    {
        var states = new Dictionary<Collider, bool>();
        foreach (CampaignThornVeilGate gate in
                 FindComponents<CampaignThornVeilGate>(scene))
        {
            SerializedProperty colliders = new SerializedObject(gate)
                .FindProperty("blockingColliders");
            if (colliders == null || !colliders.isArray)
            {
                continue;
            }

            for (int index = 0; index < colliders.arraySize; index++)
            {
                Collider collider = colliders.GetArrayElementAtIndex(index)
                    .objectReferenceValue as Collider;
                if (collider == null || states.ContainsKey(collider))
                {
                    continue;
                }

                states.Add(collider, collider.enabled);
                collider.enabled = false;
            }
        }

        return states;
    }

    private static void RestoreThornVeilBakeColliders(
        IDictionary<Collider, bool> states)
    {
        if (states == null)
        {
            return;
        }

        foreach (KeyValuePair<Collider, bool> state in states)
        {
            if (state.Key != null)
            {
                state.Key.enabled = state.Value;
            }
        }
    }

    private static void RestoreActiveStates(
        IDictionary<GameObject, bool> states)
    {
        if (states == null)
        {
            return;
        }

        foreach (KeyValuePair<GameObject, bool> state in states)
        {
            if (state.Key != null && state.Key.activeSelf != state.Value)
            {
                state.Key.SetActive(state.Value);
            }
        }
    }

    private static int RepairFarmSpawnMarkers(out int changes)
    {
        Scene scene = EditorSceneManager.OpenScene(
            FarmScenePath,
            OpenSceneMode.Single);
        NavMeshSurface surface = RequireSingleSurface(scene, "Farm Hub");
        EnsureSurfaceDataRegistered(surface, "Farm Hub");
        Terrain terrain = FindTerrain(scene, required: false, "Farm Hub");

        changes = 0;
        int markerCount = 0;
        Dictionary<string, Transform> authoredMarkers =
            FindAndAuthorFarmSpawnVolumes(scene, ref changes);
        Physics.SyncTransforms();
        ValidateFarmSpawnerMarkerOwnership(scene, authoredMarkers);
        ValidateFarmFenceContainment(authoredMarkers);

        foreach (FarmRecurringEmergenceDirector director in
                 FindComponents<FarmRecurringEmergenceDirector>(scene))
        {
            foreach (Transform marker in director.SpawnPoints)
            {
                NavMeshAgent agent = FindRequiredAgent(
                    director.EnemyPrefabs,
                    "Farm recurring emergence");
                changes += SnapAndValidateMarker(
                    marker,
                    agent,
                    terrain,
                    "Farm recurring emergence/" + MarkerName(marker));
                markerCount++;
            }
        }

        foreach (global::MobSpawner spawner in
                 FindComponents<global::MobSpawner>(scene))
        {
            NavMeshAgent agent = ResolveMobSpawnerAgent(spawner);
            SerializedProperty points = new SerializedObject(spawner)
                .FindProperty("spawnPoint");
            if (points == null || !points.isArray)
            {
                throw new InvalidOperationException(
                    "Farm MobSpawner is missing its serialized spawnPoint array.");
            }

            for (int index = 0; index < points.arraySize; index++)
            {
                Transform marker = points.GetArrayElementAtIndex(index)
                    .objectReferenceValue as Transform;
                string label = "Farm MobSpawner/" + MarkerName(marker);
                changes += SnapAndValidateMarker(
                    marker,
                    agent,
                    terrain,
                    label);
                changes += EnsureMobSpawnerVolumeIsSafe(
                    marker,
                    agent,
                    terrain,
                    label);
                markerCount++;
            }
        }

        Physics.SyncTransforms();
        ValidateFarmFenceContainment(authoredMarkers);

        if (changes > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, FarmScenePath))
            {
                throw new InvalidOperationException(
                    "Unity could not save the repaired Farm Hub spawn markers.");
            }
        }

        return markerCount;
    }

    private static Dictionary<string, Transform> FindAndAuthorFarmSpawnVolumes(
        Scene scene,
        ref int changes)
    {
        var markers = new Dictionary<string, Transform>(StringComparer.Ordinal);
        foreach (FarmSpawnVolumeSpec spec in FarmSpawnVolumes)
        {
            Transform marker = RequireSingleSceneTransform(scene, spec.MarkerName);
            BoxCollider box = marker.GetComponent<BoxCollider>();
            if (box == null)
            {
                throw new InvalidOperationException(
                    "Farm spawn marker '" + spec.MarkerName +
                    "' requires its authored BoxCollider volume.");
            }

            Vector3 position = marker.position;
            Vector3 expectedPosition = new Vector3(
                spec.Center.x,
                position.y,
                spec.Center.y);
            if ((position - expectedPosition).sqrMagnitude > 0.000001f)
            {
                marker.position = expectedPosition;
                changes++;
            }

            Vector3 size = box.size;
            Vector3 expectedSize = new Vector3(
                spec.LocalSize.x,
                size.y,
                spec.LocalSize.y);
            if ((size - expectedSize).sqrMagnitude > 0.000001f)
            {
                box.size = expectedSize;
                changes++;
            }

            markers.Add(spec.MarkerName, marker);
        }

        return markers;
    }

    private static Transform RequireSingleSceneTransform(
        Scene scene,
        string objectName)
    {
        var matches = new List<Transform>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform candidate in
                     root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate != null && string.Equals(
                        candidate.name,
                        objectName,
                        StringComparison.Ordinal))
                {
                    matches.Add(candidate);
                }
            }
        }

        if (matches.Count != 1)
        {
            throw new InvalidOperationException(
                "Expected exactly one Farm spawn marker named '" + objectName +
                "'; found " + matches.Count + ".");
        }

        return matches[0];
    }

    private static void ValidateFarmSpawnerMarkerOwnership(
        Scene scene,
        IReadOnlyDictionary<string, Transform> authoredMarkers)
    {
        Transform[] expected = FarmSpawnVolumes
            .Select(spec => authoredMarkers[spec.MarkerName])
            .ToArray();
        FarmRecurringEmergenceDirector[] directors =
            FindComponents<FarmRecurringEmergenceDirector>(scene);
        global::MobSpawner[] spawners = FindComponents<global::MobSpawner>(scene);
        if (directors.Length == 0 || spawners.Length == 0)
        {
            throw new InvalidOperationException(
                "Farm enemy spawning requires both its recurring director and MobSpawner.");
        }

        foreach (FarmRecurringEmergenceDirector director in directors)
        {
            Transform[] actual = director.SpawnPoints.ToArray();
            if (actual.Length != expected.Length ||
                actual.Any(marker => marker == null) ||
                actual.Distinct().Count() != actual.Length ||
                !actual.SequenceEqual(expected))
            {
                throw new InvalidOperationException(
                    "Farm recurring emergence does not use exactly the three " +
                    "fence-contained spawn markers in canonical order.");
            }
        }

        foreach (global::MobSpawner spawner in spawners)
        {
            SerializedProperty points = new SerializedObject(spawner)
                .FindProperty("spawnPoint");
            var actual = new List<Transform>();
            if (points != null && points.isArray)
            {
                for (int index = 0; index < points.arraySize; index++)
                {
                    Transform marker = points.GetArrayElementAtIndex(index)
                        .objectReferenceValue as Transform;
                    if (marker != null)
                    {
                        actual.Add(marker);
                    }
                }
            }

            if (points == null || !points.isArray ||
                points.arraySize != expected.Length ||
                actual.Count != expected.Length ||
                actual.Distinct().Count() != actual.Count ||
                !actual.SequenceEqual(expected))
            {
                throw new InvalidOperationException(
                    "Farm MobSpawner does not use exactly the three " +
                    "fence-contained spawn markers in canonical order.");
            }
        }
    }

    private static void ValidateFarmFenceContainment(
        IReadOnlyDictionary<string, Transform> authoredMarkers)
    {
        foreach (KeyValuePair<string, Transform> pair in authoredMarkers)
        {
            BoxCollider box = pair.Value != null
                ? pair.Value.GetComponent<BoxCollider>()
                : null;
            if (box == null)
            {
                throw new InvalidOperationException(
                    "Farm spawn marker '" + pair.Key +
                    "' has no volume to validate against the fence.");
            }

            Vector3 center = box.center;
            Vector3 extents = box.size * 0.5f;
            Vector3[] localCorners =
            {
                new Vector3(center.x - extents.x, center.y, center.z - extents.z),
                new Vector3(center.x - extents.x, center.y, center.z + extents.z),
                new Vector3(center.x + extents.x, center.y, center.z - extents.z),
                new Vector3(center.x + extents.x, center.y, center.z + extents.z)
            };

            foreach (Vector3 localCorner in localCorners)
            {
                Vector3 worldCorner = box.transform.TransformPoint(localCorner);
                if (!IsInsidePolygon(
                        new Vector2(worldCorner.x, worldCorner.z),
                        FarmFenceInterior))
                {
                    throw new InvalidOperationException(
                        "Farm spawn volume '" + pair.Key +
                        "' crosses the fenced-in gameplay area at " +
                        worldCorner + ".");
                }
            }
        }
    }

    private static bool IsInsidePolygon(
        Vector2 point,
        IReadOnlyList<Vector2> polygon)
    {
        bool inside = false;
        for (int index = 0, previous = polygon.Count - 1;
             index < polygon.Count;
             previous = index++)
        {
            Vector2 start = polygon[previous];
            Vector2 end = polygon[index];
            if (DistanceToSegment(point, start, end) <= 0.001f)
            {
                return true;
            }

            bool crosses = (end.y > point.y) != (start.y > point.y);
            if (crosses && point.x <
                (start.x - end.x) * (point.y - end.y) /
                (start.y - end.y) + end.x)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static float DistanceToSegment(
        Vector2 point,
        Vector2 start,
        Vector2 end)
    {
        Vector2 segment = end - start;
        float denominator = segment.sqrMagnitude;
        if (denominator <= 0.000001f)
        {
            return Vector2.Distance(point, start);
        }

        float amount = Mathf.Clamp01(
            Vector2.Dot(point - start, segment) / denominator);
        return Vector2.Distance(point, start + segment * amount);
    }

    private static int RepairOpenWorldSpawnMarkers(out int changes)
    {
        Scene scene = EditorSceneManager.OpenScene(
            OpenWorldScenePath,
            OpenSceneMode.Single);
        NavMeshSurface surface = RequireSingleSurface(scene, "Open World");
        EnsureSurfaceDataRegistered(surface, "Open World");
        Terrain terrain = FindTerrain(scene, required: true, "Open World");

        changes = 0;
        int markerCount = 0;

        foreach (WorldArrivalEnemySpawner spawner in
                 FindComponents<WorldArrivalEnemySpawner>(scene))
        {
            var resolvedSpawns = new List<NavMeshHit>();
            foreach (WorldArrivalEnemySpawnDefinition spawn in spawner.Spawns)
            {
                if (spawn == null)
                {
                    throw new InvalidOperationException(
                        "Open World arrival spawner has a null spawn definition.");
                }

                NavMeshAgent agent = FindRequiredAgent(
                    spawn.EnemyPrefab,
                    "Arrival enemy '" + PrefabName(spawn.EnemyPrefab) + "'");
                string label = "Arrival/" + spawner.name + "/" +
                               MarkerName(spawn.SpawnPoint);
                changes += SnapAndValidateMarker(
                    spawn.SpawnPoint,
                    agent,
                    terrain,
                    label,
                    out NavMeshHit hit);
                resolvedSpawns.Add(hit);
                markerCount++;
            }

            ValidateArrivalPaths(spawner, resolvedSpawns, terrain);
        }

        foreach (OpenWorldAmbientThreatSpawner spawner in
                 FindComponents<OpenWorldAmbientThreatSpawner>(scene))
        {
            foreach (OpenWorldAmbientEnemySpawnDefinition spawn in
                     spawner.Spawns)
            {
                if (spawn == null)
                {
                    throw new InvalidOperationException(
                        "Open World ambient spawner has a null spawn definition.");
                }

                NavMeshAgent agent = FindRequiredAgent(
                    spawn.EnemyPrefab,
                    "Ambient enemy '" + PrefabName(spawn.EnemyPrefab) + "'");
                changes += SnapAndValidateMarker(
                    spawn.SpawnPoint,
                    agent,
                    terrain,
                    "Ambient/" + spawner.name + "/" +
                    MarkerName(spawn.SpawnPoint));
                markerCount++;
            }
        }

        foreach (WorldLandmarkEnemySpawner spawner in
                 FindComponents<WorldLandmarkEnemySpawner>(scene))
        {
            var resolvedSpawns = new List<NavMeshHit>();
            foreach (WorldLandmarkEnemySpawnDefinition spawn in spawner.Spawns)
            {
                if (spawn == null)
                {
                    throw new InvalidOperationException(
                        "Open World landmark spawner has a null spawn definition.");
                }

                NavMeshAgent agent = FindRequiredAgent(
                    spawn.EnemyPrefab,
                    "Landmark enemy '" + PrefabName(spawn.EnemyPrefab) + "'");
                string label = "Landmark/" + spawner.name + "/" +
                               MarkerName(spawn.SpawnPoint);
                changes += SnapAndValidateMarker(
                    spawn.SpawnPoint,
                    agent,
                    terrain,
                    label,
                    out NavMeshHit hit);
                resolvedSpawns.Add(hit);
                markerCount++;
            }

            ValidateLandmarkPaths(spawner, resolvedSpawns, terrain);
        }

        // Witches fly by design, so their root transforms are intentionally
        // not forced to terrain. Their summoned boars use NavMeshAgents, and
        // these are the only Witch-authored locations that need grounding.
        foreach (WitchController witch in FindComponents<WitchController>(scene))
        {
            markerCount += RepairWitchMinionMarkers(
                witch,
                terrain,
                ref changes);
        }

        if (changes > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, OpenWorldScenePath))
            {
                throw new InvalidOperationException(
                    "Unity could not save the repaired Open World spawn markers.");
            }
        }

        return markerCount;
    }

    private static int RepairWitchMinionMarkers(
        WitchController witch,
        Terrain terrain,
        ref int changes)
    {
        SerializedObject serialized = new SerializedObject(witch);
        SerializedProperty points = serialized.FindProperty("minionSpawnPoints");
        if (points == null || !points.isArray || points.arraySize == 0)
        {
            return 0;
        }

        SerializedProperty prefabs = serialized.FindProperty("minionPrefabs");
        if (prefabs == null || !prefabs.isArray)
        {
            throw new InvalidOperationException(
                "Witch '" + witch.name +
                "' has minion spawn points but no minion prefab roster.");
        }

        var candidates = new List<GameObject>();
        for (int index = 0; index < prefabs.arraySize; index++)
        {
            GameObject prefab = prefabs.GetArrayElementAtIndex(index)
                .objectReferenceValue as GameObject;
            if (prefab != null)
            {
                candidates.Add(prefab);
            }
        }

        NavMeshAgent agent = FindRequiredAgent(
            candidates,
            "Witch minions for '" + witch.name + "'");
        int count = 0;
        for (int index = 0; index < points.arraySize; index++)
        {
            Transform marker = points.GetArrayElementAtIndex(index)
                .objectReferenceValue as Transform;
            changes += SnapAndValidateMarker(
                marker,
                agent,
                terrain,
                "Witch minion/" + witch.name + "/" + MarkerName(marker));
            count++;
        }

        return count;
    }

    private static int SnapAndValidateMarker(
        Transform marker,
        NavMeshAgent agent,
        Terrain terrain,
        string label)
    {
        return SnapAndValidateMarker(
            marker,
            agent,
            terrain,
            label,
            out _);
    }

    private static int SnapAndValidateMarker(
        Transform marker,
        NavMeshAgent agent,
        Terrain terrain,
        string label,
        out NavMeshHit sample)
    {
        if (marker == null)
        {
            throw new InvalidOperationException(label + " has no spawn marker.");
        }

        int areaMask = agent != null
            ? agent.areaMask & WalkableAreaMask
            : 0;
        if (areaMask == 0)
        {
            throw new InvalidOperationException(
                label + " requires an enemy NavMeshAgent that supports the " +
                "Walkable area.");
        }

        if (!TryFindGroundedNavMeshSample(
                marker.position,
                areaMask,
                terrain,
                out sample))
        {
            throw new InvalidOperationException(
                label + " has no nearby Walkable NavMesh point with solid " +
                "physical ground support.");
        }

        Vector3 correction = sample.position - marker.position;
        float horizontalCorrection = new Vector2(
            correction.x,
            correction.z).magnitude;
        if (horizontalCorrection > MaximumHorizontalCorrection ||
            Mathf.Abs(correction.y) > MaximumVerticalCorrection)
        {
            throw new InvalidOperationException(
                label + " would require an unsafe NavMesh correction (horizontal=" +
                horizontalCorrection.ToString("F2") + "m, vertical=" +
                Mathf.Abs(correction.y).ToString("F2") + "m).");
        }

        if (correction.sqrMagnitude <= 0.000001f)
        {
            return 0;
        }

        marker.position = sample.position;
        return 1;
    }

    private static int EnsureMobSpawnerVolumeIsSafe(
        Transform marker,
        NavMeshAgent agent,
        Terrain terrain,
        string label)
    {
        if (marker == null)
        {
            return 0;
        }

        BoxCollider box = marker.GetComponent<BoxCollider>();
        if (box == null)
        {
            return 0;
        }

        if (IsMobSpawnerVolumeSafe(box, agent, terrain, label))
        {
            return 0;
        }

        Vector3 scale = marker.lossyScale;
        float localX = MinimumSafeSpawnVolumeSize /
                       Mathf.Max(0.0001f, Mathf.Abs(scale.x));
        float localZ = MinimumSafeSpawnVolumeSize /
                       Mathf.Max(0.0001f, Mathf.Abs(scale.z));
        Vector3 size = box.size;
        size.x = Mathf.Min(size.x, localX);
        size.z = Mathf.Min(size.z, localZ);
        box.size = size;

        if (!IsMobSpawnerVolumeSafe(box, agent, terrain, label))
        {
            throw new InvalidOperationException(
                label + " cannot keep its random spawn volume on Walkable NavMesh.");
        }

        return 1;
    }

    private static bool IsMobSpawnerVolumeSafe(
        BoxCollider box,
        NavMeshAgent agent,
        Terrain terrain,
        string label)
    {
        Bounds bounds = box.bounds;
        Vector3[] probes =
        {
            new Vector3(bounds.center.x, box.transform.position.y, bounds.center.z),
            new Vector3(bounds.min.x, box.transform.position.y, bounds.min.z),
            new Vector3(bounds.min.x, box.transform.position.y, bounds.max.z),
            new Vector3(bounds.max.x, box.transform.position.y, bounds.min.z),
            new Vector3(bounds.max.x, box.transform.position.y, bounds.max.z)
        };

        int areaMask = agent.areaMask & WalkableAreaMask;
        foreach (Vector3 probe in probes)
        {
            if (!TryFindGroundedNavMeshSample(
                    probe,
                    areaMask,
                    terrain,
                    out NavMeshHit sample,
                    SpawnVolumeSampleDistance,
                    includeSearchRing: false))
            {
                return false;
            }

            float horizontal = new Vector2(
                sample.position.x - probe.x,
                sample.position.z - probe.z).magnitude;
            if (horizontal > SpawnVolumeSampleDistance)
            {
                return false;
            }

        }

        return true;
    }

    private static bool TryFindGroundedNavMeshSample(
        Vector3 requestedPosition,
        int areaMask,
        Terrain terrain,
        out NavMeshHit result,
        float sampleDistance = MarkerSampleDistance,
        bool includeSearchRing = true)
    {
        result = default;
        if (areaMask == 0)
        {
            return false;
        }

        var queries = new List<Vector3> { requestedPosition };
        AddTerrainHeightQuery(queries, terrain, requestedPosition);
        if (includeSearchRing)
        {
            float[] radii = { 2f, 4f, 6f, 8f };
            Vector2[] directions =
            {
                Vector2.right,
                Vector2.left,
                Vector2.up,
                Vector2.down,
                new Vector2(0.70710678f, 0.70710678f),
                new Vector2(-0.70710678f, 0.70710678f),
                new Vector2(0.70710678f, -0.70710678f),
                new Vector2(-0.70710678f, -0.70710678f)
            };
            foreach (float radius in radii)
            {
                foreach (Vector2 direction in directions)
                {
                    Vector3 query = requestedPosition + new Vector3(
                        direction.x * radius,
                        0f,
                        direction.y * radius);
                    queries.Add(query);
                    AddTerrainHeightQuery(queries, terrain, query);
                }
            }
        }

        bool found = false;
        float bestScore = float.PositiveInfinity;
        float queryDistance = includeSearchRing
            ? Mathf.Min(sampleDistance, 2f)
            : sampleDistance;
        foreach (Vector3 query in queries)
        {
            if (!NavMesh.SamplePosition(
                    query,
                    out NavMeshHit candidate,
                    queryDistance,
                    areaMask) ||
                !HasPhysicalGround(candidate.position, terrain))
            {
                continue;
            }

            Vector3 correction = candidate.position - requestedPosition;
            float horizontal = new Vector2(correction.x, correction.z).magnitude;
            if (horizontal > MaximumHorizontalCorrection ||
                Mathf.Abs(correction.y) > MaximumVerticalCorrection)
            {
                continue;
            }

            float score = horizontal + Mathf.Abs(correction.y) * 0.05f;
            if (!found || score < bestScore)
            {
                result = candidate;
                bestScore = score;
                found = true;
            }
        }

        return found;
    }

    private static void AddTerrainHeightQuery(
        ICollection<Vector3> queries,
        Terrain terrain,
        Vector3 query)
    {
        if (terrain == null || !IsWithinTerrain(terrain, query))
        {
            return;
        }

        query.y = terrain.SampleHeight(query) + terrain.transform.position.y +
                  0.05f;
        queries.Add(query);
    }

    private static void ValidateArrivalPaths(
        WorldArrivalEnemySpawner spawner,
        IReadOnlyList<NavMeshHit> spawnSamples,
        Terrain terrain)
    {
        if (spawner.ArrivalTrigger == null)
        {
            throw new InvalidOperationException(
                "Arrival spawner '" + spawner.name + "' has no trigger.");
        }

        Vector3 triggerPosition = spawner.ArrivalTrigger.bounds.center;
        if (!NavMesh.SamplePosition(
                triggerPosition,
                out NavMeshHit triggerSample,
                MarkerSampleDistance,
                WalkableAreaMask))
        {
            throw new InvalidOperationException(
                "Arrival trigger '" + spawner.name +
                "' is not near the active baked Walkable NavMesh.");
        }

        ValidatePhysicalGround(
            triggerSample.position,
            terrain,
            "Arrival trigger/" + spawner.name);

        foreach (NavMeshHit spawnSample in spawnSamples)
        {
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(
                    spawnSample.position,
                    triggerSample.position,
                    WalkableAreaMask,
                    path) ||
                path.status != NavMeshPathStatus.PathComplete)
            {
                throw new InvalidOperationException(
                    "Arrival spawn in '" + spawner.name +
                    "' has no complete Walkable NavMesh path to its player trigger.");
            }
        }
    }

    private static void ValidateLandmarkPaths(
        WorldLandmarkEnemySpawner spawner,
        IReadOnlyList<NavMeshHit> spawnSamples,
        Terrain terrain)
    {
        if (spawner.ProximityTrigger == null)
        {
            throw new InvalidOperationException(
                "Landmark spawner '" + spawner.name + "' has no trigger.");
        }

        Vector3 triggerPosition = spawner.ProximityTrigger.bounds.center;
        if (!TryFindGroundedNavMeshSample(
                triggerPosition,
                WalkableAreaMask,
                terrain,
                out NavMeshHit triggerSample))
        {
            throw new InvalidOperationException(
                "Landmark trigger '" + spawner.name +
                "' is not near a solid, Walkable NavMesh point.");
        }

        foreach (NavMeshHit spawnSample in spawnSamples)
        {
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(
                    spawnSample.position,
                    triggerSample.position,
                    WalkableAreaMask,
                    path) ||
                path.status != NavMeshPathStatus.PathComplete)
            {
                throw new InvalidOperationException(
                    "Landmark spawn in '" + spawner.name +
                    "' has no complete Walkable NavMesh path to its trigger.");
            }
        }
    }

    private static void ValidatePhysicalGround(
        Vector3 navMeshPosition,
        Terrain terrain,
        string label)
    {
        if (HasPhysicalGround(navMeshPosition, terrain))
        {
            return;
        }

        string terrainDetail = string.Empty;
        if (terrain != null && IsWithinTerrain(terrain, navMeshPosition))
        {
            float terrainHeight = terrain.SampleHeight(navMeshPosition) +
                                  terrain.transform.position.y;
            if (navMeshPosition.y < terrainHeight - BelowTerrainTolerance)
            {
                terrainDetail = " NavMesh=" + navMeshPosition.y.ToString("F2") +
                                ", terrain=" + terrainHeight.ToString("F2") +
                                " (below terrain).";
            }
            else
            {
                terrainDetail = " NavMesh=" + navMeshPosition.y.ToString("F2") +
                                ", terrain=" + terrainHeight.ToString("F2") +
                                ".";
            }
        }

        throw new InvalidOperationException(
            label + " has no solid collider directly supporting its Walkable " +
            "NavMesh sample at " + navMeshPosition + "." + terrainDetail);
    }

    private static bool HasPhysicalGround(
        Vector3 navMeshPosition,
        Terrain terrain)
    {
        if (terrain != null && IsWithinTerrain(terrain, navMeshPosition))
        {
            float terrainHeight = terrain.SampleHeight(navMeshPosition) +
                                  terrain.transform.position.y;
            if (navMeshPosition.y < terrainHeight - BelowTerrainTolerance)
            {
                return false;
            }
        }

        RaycastHit[] hits = Physics.RaycastAll(
            navMeshPosition + Vector3.up * 3f,
            Vector3.down,
            6f,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);
        return hits.Any(hit => hit.collider != null &&
                               !hit.collider.isTrigger &&
                               Mathf.Abs(
                                   hit.point.y - navMeshPosition.y) <=
                               GroundSupportTolerance);
    }

    private static int ValidateFarmObjectiveTraversal(Scene scene)
    {
        string[] objectiveSteps =
        {
            "STEP_01_Collect_Feed_Scoop",
            "STEP_02_Fill_South_Trough",
            "STEP_03_Fill_North_Trough",
            "STEP_04_Clear_East_Stall",
            "STEP_05_Clear_West_Stall",
            "STEP_06_Dump_Muck_Wheelbarrow",
            "STEP_07_Prime_Livestock_Pump",
            "STEP_08_Open_Trough_Valve"
        };

        int routes = 0;
        foreach (string stepName in objectiveSteps)
        {
            Transform target = RequireSingleSceneTransform(scene, stepName);
            ValidateLocalTraversalApproach(
                target,
                "Farm objective approach -> " + stepName);
            routes++;
        }

        return routes;
    }

    private static void ValidateLocalTraversalApproach(
        Transform target,
        string label)
    {
        Vector3 requested = ResolveTraversalPoint(
            target,
            useVisualBottom: true,
            useColliderTop: false);
        if (!NavMesh.SamplePosition(
                requested,
                out NavMeshHit targetHit,
                3f,
                NavMesh.AllAreas))
        {
            throw new InvalidOperationException(
                label + " has no nearby baked NavMesh approach.");
        }

        const float approachRadius = 2.25f;
        for (int index = 0; index < 8; index++)
        {
            float angle = index * Mathf.PI * 0.25f;
            Vector3 requestedApproach = requested + new Vector3(
                Mathf.Cos(angle) * approachRadius,
                0f,
                Mathf.Sin(angle) * approachRadius);
            if (!NavMesh.SamplePosition(
                    requestedApproach,
                    out NavMeshHit approachHit,
                    1.5f,
                    NavMesh.AllAreas) ||
                Vector3.Distance(
                    approachHit.position,
                    targetHit.position) < 0.75f)
            {
                continue;
            }

            var path = new NavMeshPath();
            if (NavMesh.CalculatePath(
                    approachHit.position,
                    targetHit.position,
                    NavMesh.AllAreas,
                    path) &&
                path.status == NavMeshPathStatus.PathComplete)
            {
                return;
            }
        }

        throw new InvalidOperationException(
            label + " has no complete local player approach route.");
    }

    private static int ValidateRouteMarkerContainers(Scene scene)
    {
        int routeCount = 0;
        routeCount += ValidateRouteMarkerContainer(
            scene,
            "RouteMarkers",
            "Farmhouse",
            new[]
            {
                "ExteriorStart", "CellarDestination",
                "GroundStart", "AtticDestination"
            });
        routeCount += ValidateRouteMarkerContainer(
            scene,
            "PersistentHardRouteMarkers_18Gates",
            "Siltwater Traversal",
            new[]
            {
                "WarehouseExterior", "WarehouseInterior",
                "DockExterior", "DockPlatform",
                "DockPlatform", "WarehouseDockInterior",
                "WarehouseInterior", "WarehouseCage",
                "WarehouseInterior", "Catwalk2",
                "Catwalk2", "Catwalk4",
                "Catwalk4", "Catwalk6",
                "Catwalk6", "Catwalk8",
                "Catwalk8", "Catwalk10",
                "Catwalk10", "Elevator10",
                "Elevator0", "Elevator5",
                "Elevator5", "Elevator10",
                "Elevator10", "Elevator15",
                "Elevator15", "Elevator20",
                "Elevator20", "Elevator25",
                "Elevator20", "SiloCatwalk22",
                "WarehouseExterior", "Elevator25",
                "WarehouseExterior", "SiloCatwalk22"
            },
            requireCompleteNavMeshPaths: false,
            expectedPhysicalRampLinks: 18);
        routeCount += ValidateRouteMarkerContainer(
            scene,
            "PersistentHardRouteMarkers",
            "Siltwater Investigation",
            new[]
            {
                "ExteriorStart", "MainHallInvestigation",
                "MainHallStairBase", "MezzanineLanding",
                "MezzanineLanding", "AnnexUpperOffice",
                "AnnexUpperOffice", "AnnexGroundOffice",
                "AnnexGroundOffice", "QualityLab",
                "QualityLab", "RecordsOffice",
                "RecordsOffice", "QualityVault"
            });

        if (routeCount == 0)
        {
            throw new InvalidOperationException(
                "Open World has no active deployed architecture route markers.");
        }

        return routeCount;
    }

    private static int RefreshActiveNavMeshLinks(Scene scene)
    {
        NavMeshLink[] links = FindComponents<NavMeshLink>(scene)
            .Where(link => link != null && link.isActiveAndEnabled)
            .ToArray();
        foreach (NavMeshLink link in links)
        {
            link.UpdateLink();
        }

        return links.Length;
    }

    private static int ValidateRouteMarkerContainer(
        Scene scene,
        string containerName,
        string label,
        string[] routeEndpoints,
        bool requireCompleteNavMeshPaths = true,
        int expectedPhysicalRampLinks = 0)
    {
        if (routeEndpoints == null ||
            routeEndpoints.Length == 0 ||
            routeEndpoints.Length % 2 != 0)
        {
            throw new ArgumentException(
                "Route endpoints must contain complete start/destination pairs.",
                nameof(routeEndpoints));
        }

        string[] requiredMarkerNames = routeEndpoints.Distinct().ToArray();
        Transform[] containers = FindComponents<Transform>(scene)
            .Where(candidate => candidate != null &&
                                candidate.gameObject.activeInHierarchy &&
                                string.Equals(
                                    candidate.name,
                                    containerName,
                                    StringComparison.Ordinal) &&
                                requiredMarkerNames.All(
                                    markerName => FindDirectActiveChild(
                                        candidate,
                                        markerName) != null))
            .ToArray();
        if (containers.Length != 1)
        {
            throw new InvalidOperationException(
                label + " requires exactly one active '" + containerName +
                "' container with its authored route markers; found " +
                containers.Length + ".");
        }

        Transform container = containers[0];
        if (requireCompleteNavMeshPaths)
        {
            for (int index = 0; index < routeEndpoints.Length; index += 2)
            {
                Transform start = RequireDirectActiveChild(
                    container,
                    routeEndpoints[index]);
                Transform destination = RequireDirectActiveChild(
                    container,
                    routeEndpoints[index + 1]);
                ValidateCompleteTraversalRoute(
                    start,
                    destination,
                    label + "/" + start.name + " -> " + destination.name,
                    useVisualBottom: false,
                    sampleDistance: 2.5f);
            }
        }
        else
        {
            foreach (string markerName in requiredMarkerNames)
            {
                ValidateLocalTraversalApproach(
                    RequireDirectActiveChild(container, markerName),
                    label + "/" + markerName);
            }

            ValidatePhysicalRampBackedLinks(
                container.parent,
                label,
                expectedPhysicalRampLinks);
        }

        return routeEndpoints.Length / 2;
    }

    private static void ValidatePhysicalRampBackedLinks(
        Transform architectureRoot,
        string label,
        int expectedCount)
    {
        if (architectureRoot == null || expectedCount <= 0)
        {
            throw new InvalidOperationException(
                label + " requires an authored physical ramp-link contract.");
        }

        NavMeshLink[] links = architectureRoot
            .GetComponentsInChildren<NavMeshLink>(true)
            .Where(link => link != null)
            .ToArray();
        if (links.Length != expectedCount)
        {
            throw new InvalidOperationException(
                label + " requires exactly " + expectedCount +
                " physical ramp links; found " + links.Length + ".");
        }

        foreach (NavMeshLink link in links)
        {
            MeshCollider physicalRamp = link.GetComponent<MeshCollider>();
            if (!link.isActiveAndEnabled ||
                !link.activated ||
                !link.bidirectional ||
                link.agentTypeID != 0 ||
                physicalRamp == null ||
                !physicalRamp.enabled ||
                physicalRamp.isTrigger ||
                physicalRamp.sharedMesh == null)
            {
                throw new InvalidOperationException(
                    label + "/" + link.name +
                    " must remain an active bidirectional agent-0 link on an " +
                    "enabled solid physical MeshCollider ramp.");
            }

            Vector3 start = link.transform.TransformPoint(link.startPoint);
            Vector3 end = link.transform.TransformPoint(link.endPoint);
            if (!NavMesh.SamplePosition(
                    start,
                    out _,
                    1.25f,
                    NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(
                    end,
                    out _,
                    1.25f,
                    NavMesh.AllAreas))
            {
                throw new InvalidOperationException(
                    label + "/" + link.name +
                    " is not attached to baked walkable landing geometry at " +
                    "both ends.");
            }
        }
    }

    private static Transform RequireDirectActiveChild(
        Transform parent,
        string childName)
    {
        Transform child = FindDirectActiveChild(parent, childName);
        if (child == null)
        {
            throw new InvalidOperationException(
                GeometryHierarchyPath(parent) + " is missing active route marker '" +
                childName + "'.");
        }

        return child;
    }

    private static Transform FindDirectActiveChild(
        Transform parent,
        string childName)
    {
        if (parent == null)
        {
            return null;
        }

        return parent
            .Cast<Transform>()
            .FirstOrDefault(candidate => candidate != null &&
                                         candidate.gameObject.activeInHierarchy &&
                                         string.Equals(
                                             candidate.name,
                                             childName,
                                             StringComparison.Ordinal));
    }

    private static int ValidateOpenWorldNamedTraversal(Scene scene)
    {
        int routes = 0;
        routes += ValidateNamedRouteChain(
            scene,
            "Black Pines Fire Tower",
            "TowerGroundApproach_NAV",
            "TowerSwitchbackLanding_Bay0",
            "TowerSwitchbackLanding_Bay1",
            "TowerSwitchbackLanding_Bay2",
            "TowerSwitchbackLanding_Bay3",
            "TowerLevelLanding_4P5",
            "TowerLevelLanding_9",
            "TowerLevelLanding_13P5",
            "TowerLevelLanding_18",
            "TowerCabinRobustConnector_NAV",
            "TowerCabinInteriorFloor_NAV");
        routes += ValidateNamedRouteChain(
            scene,
            "Black Pines Ranger Outpost",
            "OutpostPorch_NAV",
            "OutpostMainFloor_NAV",
            "OutpostStorageToShedConnector_NAV",
            "OutpostPowerShedFloor_NAV");
        routes += ValidateNamedRouteChain(
            scene,
            "Harrow Mausoleum to Crypt",
            "MausoleumGroundApproach_NAV",
            "MausoleumThresholdLanding_NAV",
            "CryptEntrySocketLanding_NAV",
            "CryptEntryConnector_NAV",
            "CryptMainFloor_NAV",
            "CryptVaultConnector_NAV");
        return routes;
    }

    private static int ValidateNamedRouteChain(
        Scene scene,
        string label,
        params string[] pointNames)
    {
        if (pointNames == null || pointNames.Length < 2)
        {
            throw new ArgumentException(
                "A named traversal chain needs at least two points.",
                nameof(pointNames));
        }

        Transform[] points = pointNames
            .Select(name => RequireSingleSceneTransform(scene, name))
            .ToArray();
        for (int index = 1; index < points.Length; index++)
        {
            ValidateCompleteTraversalRoute(
                points[index - 1],
                points[index],
                label + "/" + pointNames[index - 1] + " -> " +
                pointNames[index],
                useVisualBottom: false,
                sampleDistance: 2.5f,
                useColliderTop: true);
        }

        return points.Length - 1;
    }

    private static void ValidateCompleteTraversalRoute(
        Transform requestedStart,
        Transform requestedEnd,
        string label,
        bool useVisualBottom,
        float sampleDistance,
        bool useColliderTop = false)
    {
        Vector3 start = ResolveTraversalPoint(
            requestedStart,
            useVisualBottom,
            useColliderTop);
        Vector3 end = ResolveTraversalPoint(
            requestedEnd,
            useVisualBottom,
            useColliderTop);
        if (!NavMesh.SamplePosition(
                start,
                out NavMeshHit startHit,
                sampleDistance,
                NavMesh.AllAreas))
        {
            throw new InvalidOperationException(
                label + " start has no nearby baked NavMesh at " + start + ".");
        }

        if (!NavMesh.SamplePosition(
                end,
                out NavMeshHit endHit,
                sampleDistance,
                NavMesh.AllAreas))
        {
            throw new InvalidOperationException(
                label + " destination has no nearby baked NavMesh at " +
                end + ".");
        }

        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(
                startHit.position,
                endHit.position,
                NavMesh.AllAreas,
                path) ||
            path.status != NavMeshPathStatus.PathComplete)
        {
            throw new InvalidOperationException(
                label + " is not a complete player NavMesh route (" +
                path.status + ").");
        }
    }

    private static Vector3 ResolveTraversalPoint(
        Transform target,
        bool useVisualBottom,
        bool useColliderTop)
    {
        if (target == null)
        {
            throw new InvalidOperationException(
                "Traversal point is missing.");
        }

        if (useColliderTop)
        {
            Collider collider = target
                .GetComponentsInChildren<Collider>(true)
                .FirstOrDefault(item => item != null &&
                                        item.enabled &&
                                        !item.isTrigger);
            if (collider != null)
            {
                return new Vector3(
                    collider.bounds.center.x,
                    collider.bounds.max.y + 0.05f,
                    collider.bounds.center.z);
            }
        }

        if (useVisualBottom &&
            TryGetCombinedVisualBounds(target.gameObject, out Bounds bounds))
        {
            return new Vector3(
                bounds.center.x,
                bounds.min.y + 0.05f,
                bounds.center.z);
        }

        return target.position;
    }

    private static void RepairSceneGeometry(
        string scenePath,
        string label,
        bool isFarm,
        out int groundedCount,
        out int colliderCount)
    {
        Scene scene = EditorSceneManager.OpenScene(
            scenePath,
            OpenSceneMode.Single);
        Terrain[] terrains = FindComponents<Terrain>(scene)
            .Where(item => item != null && item.terrainData != null)
            .ToArray();
        GameObject[] placements = CollectGeometryPlacements(scene);
        groundedCount = 0;
        colliderCount = 0;

        if (isFarm)
        {
            RemoveEmergencePresentationCollision(scene);
        }

        // Restore source-authored collision before support raycasts so every
        // placed world rock participates in the repaired NavMesh bake. Farm
        // emergence roots/spikes remain nonblocking presentation so enemies
        // cannot spawn inside a newly introduced obstacle.
        foreach (GameObject placement in placements)
        {
            string assetPath =
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                    placement);
            colliderCount += RestoreForestAssetCollision(
                placement,
                assetPath);
        }

        Physics.SyncTransforms();
        foreach (GameObject placement in placements)
        {
            string assetPath =
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                    placement);
            if (!IsGroundContactRepairCandidate(placement, assetPath) ||
                !TryGetCombinedVisualBounds(placement, out Bounds bounds) ||
                !TryFindHighestExternalSupport(
                    placement,
                    bounds,
                    terrains,
                    out float supportY))
            {
                continue;
            }

            float gap = bounds.min.y - supportY;
            if (gap <= GeometryRepairTolerance ||
                gap > GeometryMaximumRepairDrop)
            {
                continue;
            }

            Transform transform = placement.transform;
            transform.position += Vector3.down * gap;
            EditorUtility.SetDirty(transform);
            if (PrefabUtility.IsPartOfPrefabInstance(transform))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(
                    transform);
            }

            groundedCount++;
            Physics.SyncTransforms();
        }

        if (isFarm)
        {
            // These scene-authored Farm props are intentionally not converted
            // into giant renderer boxes. Exact static mesh collision preserves
            // their openings/profile; simple bed primitives get fitted boxes.
            colliderCount += EnsureExactMeshCollision(
                RequireSingleSceneTransform(
                    scene,
                    "Meshy_AI_i_need_a_low_poly_cou_0822004032_texture")
                    .gameObject);
            colliderCount += EnsureExactMeshCollision(
                RequireSingleSceneTransform(scene, "Feed Bin").gameObject);
            colliderCount += EnsureFittedBoxCollision(
                RequireSingleSceneTransform(
                    scene,
                    "SLOT_Replaceable_Bed_Frame").gameObject);
            colliderCount += EnsureFittedBoxCollision(
                RequireSingleSceneTransform(
                    scene,
                    "SLOT_Replaceable_Bed_Mattress").gameObject);
            colliderCount += EnsureFittedBoxCollision(
                RequireSingleSceneTransform(
                    scene,
                    "SLOT_Replaceable_Bed_Pillow").gameObject);
        }

        colliderCount += ConfigureReliableLargeMeshCollision(scene, isFarm);

        Physics.SyncTransforms();
        if (groundedCount > 0 || colliderCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new InvalidOperationException(
                    "Unity could not save repaired " + label +
                    " geometry.");
            }
        }

        Console.WriteLine(
            "BLOODROOT_GEOMETRY_SCENE_REPAIR " +
            "scene=\"" + EscapeAuditValue(label) + "\" " +
            "grounded=" + groundedCount + " " +
            "colliders=" + colliderCount + ".");
    }

    private static int RestoreForestAssetCollision(
        GameObject placement,
        string assetPath)
    {
        if (!ContainsAnyIgnoreCase(assetPath, new[] { "/SM_Rock_" }))
        {
            return 0;
        }

        int changes = 0;
        foreach (Collider collider in
                 placement.GetComponentsInChildren<Collider>(true))
        {
            if (collider == null || collider.isTrigger || collider.enabled)
            {
                continue;
            }

            SerializedObject serialized = new SerializedObject(collider);
            SerializedProperty enabled = serialized.FindProperty("m_Enabled");
            if (enabled != null &&
                enabled.prefabOverride &&
                PrefabUtility.IsPartOfPrefabInstance(collider))
            {
                PrefabUtility.RevertPropertyOverride(
                    enabled,
                    InteractionMode.AutomatedAction);
                serialized.Update();
            }

            if (!collider.enabled)
            {
                collider.enabled = true;
                EditorUtility.SetDirty(collider);
                if (PrefabUtility.IsPartOfPrefabInstance(collider))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(
                        collider);
                }
            }

            if (collider.enabled)
            {
                changes++;
            }
        }

        bool hasEnabledSolid = placement
            .GetComponentsInChildren<Collider>(true)
            .Any(collider => collider != null &&
                             collider.enabled &&
                             !collider.isTrigger);
        if (!hasEnabledSolid)
        {
            changes += EnsureExactMeshCollision(placement);
        }

        return changes;
    }

    private static void RemoveEmergencePresentationCollision(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(
                true);
            foreach (Transform transform in transforms)
            {
                if (transform == null ||
                    !ContainsAnyIgnoreCase(
                        GeometryHierarchyPath(transform),
                        new[] { "Generated Alpha Infected Ground" }))
                {
                    continue;
                }

                foreach (Collider collider in
                         transform.GetComponents<Collider>())
                {
                    if (collider == null || collider.isTrigger)
                    {
                        continue;
                    }

                    if (PrefabUtility.IsAddedComponentOverride(collider))
                    {
                        UnityEngine.Object.DestroyImmediate(collider, true);
                    }
                    else if (collider.enabled)
                    {
                        collider.enabled = false;
                        EditorUtility.SetDirty(collider);
                        if (PrefabUtility.IsPartOfPrefabInstance(collider))
                        {
                            PrefabUtility.RecordPrefabInstancePropertyModifications(
                                collider);
                        }
                    }
                }
            }
        }
    }

    private static int EnsureExactMeshCollision(GameObject root)
    {
        int changes = 0;
        MeshColliderCookingOptions reliableCooking =
            MeshColliderCookingOptions.CookForFasterSimulation |
            MeshColliderCookingOptions.EnableMeshCleaning |
            MeshColliderCookingOptions.WeldColocatedVertices;
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(
                     true))
        {
            if (filter == null || filter.sharedMesh == null)
            {
                continue;
            }

            MeshCollider existingMesh = filter.GetComponent<MeshCollider>();
            if (existingMesh != null &&
                existingMesh.enabled &&
                !existingMesh.isTrigger &&
                existingMesh.sharedMesh == filter.sharedMesh)
            {
                if (existingMesh.cookingOptions != reliableCooking)
                {
                    existingMesh.cookingOptions = reliableCooking;
                    EditorUtility.SetDirty(existingMesh);
                    changes++;
                }

                continue;
            }

            if (filter.GetComponents<Collider>().Any(collider =>
                    collider != null &&
                    collider.enabled &&
                    !collider.isTrigger))
            {
                continue;
            }

            Rigidbody body = filter.GetComponentInParent<Rigidbody>();
            if (body != null && !body.isKinematic)
            {
                throw new InvalidOperationException(
                    "Static exact mesh collision cannot be added beneath " +
                    "non-kinematic Rigidbody '" + body.name + "'.");
            }

            MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            collider.convex = false;
            collider.isTrigger = false;
            collider.enabled = true;
            collider.cookingOptions = reliableCooking;
            EditorUtility.SetDirty(collider);
            if (PrefabUtility.IsPartOfPrefabInstance(collider))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(
                    collider);
            }

            changes++;
        }

        return changes;
    }

    private static int EnsureFittedBoxCollision(GameObject target)
    {
        if (target.GetComponents<Collider>().Any(collider =>
                collider != null &&
                collider.enabled &&
                !collider.isTrigger))
        {
            return 0;
        }

        if (!TryGetLocalMeshBounds(target.transform, out Bounds bounds))
        {
            throw new InvalidOperationException(
                "Cannot fit collision to '" + target.name +
                "' because it has no mesh bounds.");
        }

        BoxCollider collider = target.AddComponent<BoxCollider>();
        collider.center = bounds.center;
        collider.size = bounds.size;
        collider.isTrigger = false;
        collider.enabled = true;
        EditorUtility.SetDirty(collider);
        return 1;
    }

    private static int ConfigureReliableLargeMeshCollision(
        Scene scene,
        bool configureAllSceneMeshColliders)
    {
        int changes = 0;
        foreach (MeshCollider collider in FindComponents<MeshCollider>(scene))
        {
            Mesh mesh = collider != null ? collider.sharedMesh : null;
            if (mesh == null ||
                (collider.cookingOptions &
                 MeshColliderCookingOptions.UseFastMidphase) == 0)
            {
                continue;
            }

            ulong triangleCount = 0;
            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                triangleCount += mesh.GetIndexCount(subMesh) / 3UL;
            }

            if (!configureAllSceneMeshColliders &&
                triangleCount <= 2097152UL)
            {
                continue;
            }

            collider.cookingOptions &=
                ~MeshColliderCookingOptions.UseFastMidphase;
            EditorUtility.SetDirty(collider);
            if (PrefabUtility.IsPartOfPrefabInstance(collider))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(
                    collider);
            }

            changes++;
            Console.WriteLine(
                "BLOODROOT_GEOMETRY_LARGE_MESH_RELIABLE_COOKING " +
                "collider=\"" + EscapeAuditValue(
                    GeometryHierarchyPath(collider.transform)) + "\" " +
                "triangles=" + triangleCount + ".");
        }

        return changes;
    }

    private static bool TryGetLocalMeshBounds(
        Transform root,
        out Bounds bounds)
    {
        bool initialized = false;
        bounds = default;
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(
                     true))
        {
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null)
            {
                continue;
            }

            Bounds meshBounds = mesh.bounds;
            Vector3 center = meshBounds.center;
            Vector3 extents = meshBounds.extents;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 localCorner = center + Vector3.Scale(
                            extents,
                            new Vector3(x, y, z));
                        Vector3 rootLocal = root.InverseTransformPoint(
                            filter.transform.TransformPoint(localCorner));
                        if (!initialized)
                        {
                            bounds = new Bounds(rootLocal, Vector3.zero);
                            initialized = true;
                        }
                        else
                        {
                            bounds.Encapsulate(rootLocal);
                        }
                    }
                }
            }
        }

        return initialized && bounds.size.sqrMagnitude > 0.0001f;
    }

    private static bool IsGroundContactRepairCandidate(
        GameObject placement,
        string assetPath)
    {
        string identity = placement.name + " " + assetPath + " " +
                          GeometryHierarchyPath(placement.transform);
        return ContainsAnyIgnoreCase(identity, new[]
        {
            "/SM_Rock_",
            "BlackPinesInfectedPatch.prefab",
            "Item_Radar",
            "Pickup - Flashlight",
            "Pickup - Rifle",
            "Windmill",
            "/Water Well and Pump",
            "/Water tank",
            "/Feed trough",
            "/Pig poo",
            "/Wheelbarrow",
            "worktable_01a_fbx"
        });
    }

    private static void AuditSceneGeometry(string scenePath, string label)
    {
        Scene scene = EditorSceneManager.OpenScene(
            scenePath,
            OpenSceneMode.Single);
        Physics.SyncTransforms();

        Terrain[] terrains = FindComponents<Terrain>(scene)
            .Where(item => item != null && item.terrainData != null)
            .ToArray();
        GameObject[] placements = CollectGeometryPlacements(scene);
        int supported = 0;
        int floating = 0;
        int noSupport = 0;
        int missingSolidCollider = 0;
        int stairMeshes = 0;
        int stairsWithoutLocalCollision = 0;

        foreach (GameObject placement in placements)
        {
            if (!TryGetCombinedVisualBounds(placement, out Bounds bounds))
            {
                continue;
            }

            string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                placement);
            string hierarchyPath = GeometryHierarchyPath(placement.transform);
            Collider[] solidColliders = placement
                .GetComponentsInChildren<Collider>(true)
                .Where(collider => collider != null &&
                                   collider.enabled &&
                                   !collider.isTrigger)
                .ToArray();

            if (solidColliders.Length == 0 &&
                RequiresStaticSolidCollision(placement, bounds, assetPath))
            {
                missingSolidCollider++;
                Console.WriteLine(
                    "BLOODROOT_GEOMETRY_MISSING_SOLID_COLLIDER " +
                    "scene=\"" + EscapeAuditValue(label) + "\" " +
                    "root=\"" + EscapeAuditValue(hierarchyPath) + "\" " +
                    "asset=\"" + EscapeAuditValue(assetPath) + "\" " +
                    "size=" + bounds.size.ToString("F3") + ".");
            }

            if (TryFindHighestExternalSupport(
                    placement,
                    bounds,
                    terrains,
                    out float supportY))
            {
                float gap = bounds.min.y - supportY;
                bool mustTouchSupport = IsGroundContactRepairCandidate(
                    placement,
                    assetPath);
                if (gap > GeometrySupportTolerance &&
                    (mustTouchSupport || gap <= GeometryMaximumAuditDrop))
                {
                    floating++;
                    Console.WriteLine(
                        "BLOODROOT_GEOMETRY_FLOATING " +
                        "scene=\"" + EscapeAuditValue(label) + "\" " +
                        "root=\"" + EscapeAuditValue(hierarchyPath) + "\" " +
                        "asset=\"" + EscapeAuditValue(assetPath) + "\" " +
                        "gap=" + gap.ToString("F3") + " " +
                        "visualBottom=" + bounds.min.y.ToString("F3") + " " +
                        "support=" + supportY.ToString("F3") + ".");
                }
                else
                {
                    supported++;
                }
            }
            else
            {
                noSupport++;
                Console.WriteLine(
                    "BLOODROOT_GEOMETRY_NO_NEAR_SUPPORT " +
                    "scene=\"" + EscapeAuditValue(label) + "\" " +
                    "root=\"" + EscapeAuditValue(hierarchyPath) + "\" " +
                    "asset=\"" + EscapeAuditValue(assetPath) + "\" " +
                    "visualBottom=" + bounds.min.y.ToString("F3") + ".");
            }

            Transform[] stairCandidates = placement
                .GetComponentsInChildren<Transform>(true)
                .Where(IsStairOrRampVisual)
                .ToArray();
            stairCandidates = stairCandidates
                .Where(candidate => !stairCandidates.Any(ancestor =>
                    ancestor != candidate &&
                    candidate.IsChildOf(ancestor)))
                .ToArray();
            foreach (Transform stair in stairCandidates)
            {
                stairMeshes++;
                bool hasLocalSolidCollider =
                    TryGetCombinedVisualBounds(
                        stair.gameObject,
                        out Bounds stairBounds) &&
                    (HasOverlappingSolidCollider(
                         placement,
                         stairBounds) ||
                     HasNamedStairCollisionAuthority(
                         placement,
                         stair));
                if (hasLocalSolidCollider)
                {
                    continue;
                }

                stairsWithoutLocalCollision++;
                Console.WriteLine(
                    "BLOODROOT_GEOMETRY_STAIR_WITHOUT_LOCAL_COLLIDER " +
                    "scene=\"" + EscapeAuditValue(label) + "\" " +
                    "stair=\"" + EscapeAuditValue(
                        GeometryHierarchyPath(stair)) + "\" " +
                    "asset=\"" + EscapeAuditValue(assetPath) + "\".");
            }
        }

        Console.WriteLine(
            "BLOODROOT_GEOMETRY_SCENE_AUDIT " +
            "scene=\"" + EscapeAuditValue(label) + "\" " +
            "placements=" + placements.Length + " " +
            "supported=" + supported + " " +
            "floating=" + floating + " " +
            "noSupport=" + noSupport + " " +
            "missingSolidCollider=" + missingSolidCollider + " " +
            "stairMeshes=" + stairMeshes + " " +
            "stairsWithoutLocalCollision=" +
            stairsWithoutLocalCollision + ".");
    }

    private static GameObject[] CollectGeometryPlacements(Scene scene)
    {
        var placements = new HashSet<GameObject>();
        foreach (Renderer renderer in FindComponents<Renderer>(scene))
        {
            if (!IsInspectableStaticRenderer(renderer))
            {
                continue;
            }

            GameObject prefabRoot =
                PrefabUtility.GetOutermostPrefabInstanceRoot(
                    renderer.gameObject);
            GameObject placement = prefabRoot != null &&
                                   prefabRoot.scene == scene
                ? prefabRoot
                : renderer.gameObject;
            if (placement == null ||
                IsGeometryAuditExcluded(placement, renderer))
            {
                continue;
            }

            placements.Add(placement);
        }

        return placements
            .OrderBy(item => GeometryHierarchyPath(item.transform),
                StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsInspectableStaticRenderer(Renderer renderer)
    {
        return renderer != null &&
               renderer.enabled &&
               renderer.gameObject.activeInHierarchy &&
               !(renderer is ParticleSystemRenderer) &&
               !(renderer is TrailRenderer) &&
               !(renderer is LineRenderer) &&
               !(renderer is SkinnedMeshRenderer);
    }

    private static bool IsGeometryAuditExcluded(
        GameObject placement,
        Renderer sourceRenderer)
    {
        if (placement.GetComponentInChildren<Terrain>(true) != null ||
            placement.GetComponentInChildren<Canvas>(true) != null ||
            placement.GetComponentInChildren<Camera>(true) != null ||
            placement.GetComponentInChildren<NavMeshAgent>(true) != null ||
            placement.GetComponentInChildren<CharacterController>(true) != null)
        {
            return true;
        }

        Rigidbody[] rigidbodies =
            placement.GetComponentsInChildren<Rigidbody>(true);
        if (rigidbodies.Any(body => body != null && !body.isKinematic))
        {
            return true;
        }

        string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
            placement);
        string identity = placement.name + " " +
                          sourceRenderer.gameObject.name + " " + assetPath;
        string[] excludedTokens =
        {
            "/UI/", "/Player/", "/Enemy", "/Enemies/", "/Witch",
            " marker", "marker_", "trigger", " volume", "_volume",
            "spawn", "socket", "nav_anchor", "route_marker", "loop_marker",
            "particle", "vfx", "fx_", "decal", "skybox", "cloud",
            "water plane", "waterplane", "reflection probe", "light probe"
        };
        if (ContainsAnyIgnoreCase(identity, excludedTokens))
        {
            return true;
        }

        Bounds bounds = sourceRenderer.bounds;
        return bounds.size.x > 250f ||
               bounds.size.y > 250f ||
               bounds.size.z > 250f;
    }

    private static bool TryGetCombinedVisualBounds(
        GameObject placement,
        out Bounds bounds)
    {
        Renderer[] renderers = placement
            .GetComponentsInChildren<Renderer>(true)
            .Where(IsInspectableStaticRenderer)
            .ToArray();
        if (renderers.Length == 0)
        {
            bounds = default;
            return false;
        }

        bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
        {
            bounds.Encapsulate(renderers[index].bounds);
        }

        return bounds.size.sqrMagnitude > 0.0001f;
    }

    private static bool TryFindHighestExternalSupport(
        GameObject placement,
        Bounds bounds,
        IReadOnlyList<Terrain> terrains,
        out float supportY)
    {
        supportY = float.NegativeInfinity;
        bool found = false;
        float sampleX = Mathf.Min(bounds.extents.x * 0.45f, 2f);
        float sampleZ = Mathf.Min(bounds.extents.z * 0.45f, 2f);
        Vector2[] footprint =
        {
            Vector2.zero,
            new Vector2(sampleX, sampleZ),
            new Vector2(sampleX, -sampleZ),
            new Vector2(-sampleX, sampleZ),
            new Vector2(-sampleX, -sampleZ)
        };

        foreach (Vector2 offset in footprint)
        {
            Vector3 point = new Vector3(
                bounds.center.x + offset.x,
                bounds.min.y,
                bounds.center.z + offset.y);
            foreach (Terrain terrain in terrains)
            {
                if (!IsWithinTerrain(terrain, point))
                {
                    continue;
                }

                float terrainY = terrain.SampleHeight(point) +
                                 terrain.transform.position.y;
                if (terrainY <= bounds.min.y +
                    GeometryMaximumSupportPenetration)
                {
                    supportY = Mathf.Max(supportY, terrainY);
                    found = true;
                }
            }

            Vector3 origin = point + Vector3.up * 0.5f;
            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                Vector3.down,
                GeometryMaximumAuditDrop + 0.75f,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                Collider collider = hit.collider;
                if (collider == null ||
                    !collider.enabled ||
                    collider.isTrigger ||
                    hit.point.y > bounds.min.y +
                        GeometryMaximumSupportPenetration ||
                    collider.transform == placement.transform ||
                    collider.transform.IsChildOf(placement.transform))
                {
                    continue;
                }

                supportY = Mathf.Max(supportY, hit.point.y);
                found = true;
            }
        }

        return found;
    }

    private static bool RequiresStaticSolidCollision(
        GameObject placement,
        Bounds bounds,
        string assetPath)
    {
        if (bounds.size.x < 0.15f ||
            bounds.size.y < 0.15f ||
            bounds.size.z < 0.15f)
        {
            return false;
        }

        string identity = placement.name + " " + assetPath + " " +
                          GeometryHierarchyPath(placement.transform);
        string[] nonBlockingTokens =
        {
            "/Items/", "/ItemPickups/", "evidence_paper", "evidence_folder",
            "evidence_clipboard", "evidence_bag", "presentation", "preview",
            "highlight", "indicator", "beacon", "ghost", "visual only",
            "infectedpatch", "infected_patch", "infected mound",
            "infected_mound", "pig poo", "pig_poo",
            "Generated Alpha Infected Ground", "FarmChoreStepFeedback"
        };
        return !ContainsAnyIgnoreCase(identity, nonBlockingTokens);
    }

    private static bool HasOverlappingSolidCollider(
        GameObject placement,
        Bounds visualBounds)
    {
        visualBounds.Expand(new Vector3(0.4f, 0.75f, 0.4f));
        return placement
            .GetComponentsInChildren<Collider>(true)
            .Any(collider => collider != null &&
                             collider.enabled &&
                             !collider.isTrigger &&
                             collider.gameObject.activeInHierarchy &&
                             collider.bounds.size.sqrMagnitude > 0.0001f &&
                             collider.bounds.Intersects(visualBounds));
    }

    private static bool HasNamedStairCollisionAuthority(
        GameObject placement,
        Transform stair)
    {
        if (string.Equals(
                stair.name,
                "MILL_STAIR_LANDING_MID",
                StringComparison.Ordinal))
        {
            return HasNamedEnabledSolidCollider(
                       placement,
                       "COLLIDER_StairMidLanding_2P00x1P50") &&
                   HasNamedEnabledSolidCollider(
                       placement,
                       "OVERRIDE_RobustStairRamp_Flight01_1P50W") &&
                   HasNamedEnabledSolidCollider(
                       placement,
                       "OVERRIDE_RobustStairRamp_Flight02_1P50W");
        }

        if (string.Equals(
                stair.name,
                "ELEVATOR_FLOOR_STAIR_0__GE_FLOOR_5",
                StringComparison.Ordinal))
        {
            string[] authorities =
            {
                "COLLIDER_ElevatorABottomLanding_0",
                "COLLIDER_ElevatorBTopAndFloorLanding_0",
                "COLLIDER_ElevatorFloorEast_0",
                "COLLIDER_ElevatorFloorRear_0",
                "PHYSICAL_ElevatorRamp_0_A",
                "PHYSICAL_ElevatorRamp_0_B"
            };
            return authorities.All(name =>
                HasNamedEnabledSolidCollider(placement, name));
        }

        return false;
    }

    private static bool HasNamedEnabledSolidCollider(
        GameObject placement,
        string objectName)
    {
        return placement
            .GetComponentsInChildren<Transform>(true)
            .Where(candidate => candidate != null &&
                                (string.Equals(
                                     candidate.name,
                                     objectName,
                                     StringComparison.Ordinal) ||
                                 candidate.name.StartsWith(
                                     objectName + "_",
                                     StringComparison.Ordinal)))
            .Any(candidate => candidate
                .GetComponentsInChildren<Collider>(true)
                .Any(collider => collider != null &&
                                 collider.enabled &&
                                 !collider.isTrigger));
    }

    private static bool IsStairOrRampVisual(Transform candidate)
    {
        if (candidate == null ||
            !candidate.gameObject.activeInHierarchy ||
            !candidate.GetComponentsInChildren<Renderer>(true)
                .Any(IsInspectableStaticRenderer))
        {
            return false;
        }

        if (candidate.name.StartsWith(
                "OVERRIDE_WarehouseStairRoofOpening_",
                StringComparison.Ordinal))
        {
            // These are thin visual frame posts around a deliberately clear
            // 1.50m stair route, not walkable stair surfaces.
            return false;
        }

        return ContainsAnyIgnoreCase(candidate.name, new[]
        {
            "stair", "staircase", "steps", "physical_ramp", "walkable_ramp"
        });
    }

    private static bool ContainsAnyIgnoreCase(
        string value,
        IEnumerable<string> tokens)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        return tokens.Any(token =>
            !string.IsNullOrEmpty(token) &&
            value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static string GeometryHierarchyPath(Transform target)
    {
        if (target == null)
        {
            return "<missing>";
        }

        var names = new Stack<string>();
        for (Transform current = target;
             current != null;
             current = current.parent)
        {
            names.Push(current.name);
        }

        return string.Join("/", names);
    }

    private static string EscapeAuditValue(string value)
    {
        return (value ?? string.Empty)
            .Replace("\\", "/")
            .Replace("\"", "'");
    }

    private static NavMeshSurface RequireSingleSurface(
        Scene scene,
        string label)
    {
        NavMeshSurface[] surfaces = FindComponents<NavMeshSurface>(scene);
        if (surfaces.Length != 1 || surfaces[0] == null ||
            !surfaces[0].isActiveAndEnabled || surfaces[0].navMeshData == null)
        {
            throw new InvalidOperationException(
                label + " requires exactly one active NavMeshSurface with baked data.");
        }

        if (!EditorUtility.IsPersistent(surfaces[0].navMeshData))
        {
            throw new InvalidOperationException(
                label + " NavMeshSurface is not bound to a saved NavMesh asset.");
        }

        return surfaces[0];
    }

    private static void EnsureSurfaceDataRegistered(
        NavMeshSurface surface,
        string label)
    {
        surface.RemoveData();
        surface.AddData();

        if (!surface.isActiveAndEnabled || surface.navMeshData == null)
        {
            throw new InvalidOperationException(
                label + " NavMeshSurface could not register its baked NavMesh data.");
        }
    }

    private static Terrain FindTerrain(
        Scene scene,
        bool required,
        string label)
    {
        Terrain[] terrains = FindComponents<Terrain>(scene)
            .Where(candidate => candidate != null &&
                                candidate.terrainData != null)
            .ToArray();
        if (terrains.Length == 1)
        {
            return terrains[0];
        }

        if (required)
        {
            throw new InvalidOperationException(
                label + " requires exactly one authored Terrain; found " +
                terrains.Length + ".");
        }

        return null;
    }

    private static bool IsWithinTerrain(Terrain terrain, Vector3 position)
    {
        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        return position.x >= origin.x && position.x <= origin.x + size.x &&
               position.z >= origin.z && position.z <= origin.z + size.z;
    }

    private static NavMeshAgent ResolveMobSpawnerAgent(global::MobSpawner spawner)
    {
        SerializedObject serialized = new SerializedObject(spawner);
        var candidates = new List<GameObject>();
        AddObjectReference(serialized.FindProperty("Enemy"), candidates);
        AddObjectReference(serialized.FindProperty("regularPig"), candidates);

        SerializedProperty roster = serialized.FindProperty("enemies");
        if (roster != null && roster.isArray)
        {
            for (int index = 0; index < roster.arraySize; index++)
            {
                AddObjectReference(roster.GetArrayElementAtIndex(index), candidates);
            }
        }

        return FindRequiredAgent(candidates, "Farm MobSpawner");
    }

    private static void AddObjectReference(
        SerializedProperty property,
        ICollection<GameObject> candidates)
    {
        GameObject prefab = property != null
            ? property.objectReferenceValue as GameObject
            : null;
        if (prefab != null)
        {
            candidates.Add(prefab);
        }
    }

    private static NavMeshAgent FindRequiredAgent(
        IReadOnlyList<GameObject> prefabs,
        string label)
    {
        if (prefabs != null)
        {
            foreach (GameObject prefab in prefabs)
            {
                NavMeshAgent agent = FindAgent(prefab);
                if (agent != null && (agent.areaMask & WalkableAreaMask) != 0)
                {
                    return agent;
                }
            }
        }

        throw new InvalidOperationException(
            label + " has no prefab with a NavMeshAgent that can use the " +
            "Walkable area.");
    }

    private static NavMeshAgent FindRequiredAgent(
        GameObject prefab,
        string label)
    {
        NavMeshAgent agent = FindAgent(prefab);
        if (agent == null || (agent.areaMask & WalkableAreaMask) == 0)
        {
            throw new InvalidOperationException(
                label + " has no prefab NavMeshAgent that can use the Walkable area.");
        }

        return agent;
    }

    private static NavMeshAgent FindAgent(GameObject prefab)
    {
        return prefab != null
            ? prefab.GetComponentInChildren<NavMeshAgent>(true)
            : null;
    }

    private static T[] FindComponents<T>(Scene scene) where T : Component
    {
        var results = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            results.AddRange(root.GetComponentsInChildren<T>(true));
        }

        return results.ToArray();
    }

    private static string MarkerName(Transform marker)
    {
        return marker != null ? marker.name : "<missing>";
    }

    private static string PrefabName(GameObject prefab)
    {
        return prefab != null ? prefab.name : "<missing>";
    }

    private static void EnsureIdleEditor()
    {
        if (Application.isPlaying || EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            throw new InvalidOperationException(
                "Campaign spawn grounding requires an idle Unity Editor in Edit Mode.");
        }
    }
}
#endif
