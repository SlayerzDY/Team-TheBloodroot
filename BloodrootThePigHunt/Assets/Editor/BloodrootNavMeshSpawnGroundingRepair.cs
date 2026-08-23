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

        string canonicalFullPath = ToProjectFilePath(canonicalPath);
        byte[] originalNavMeshBytes = File.ReadAllBytes(canonicalFullPath);
        string temporaryPath = string.Empty;
        Dictionary<GameObject, bool> activeStates = null;
        Dictionary<Collider, bool> gateColliderStates = null;
        bool canonicalReplaced = false;

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

            rebuiltData.name = Path.GetFileNameWithoutExtension(canonicalPath);

            temporaryPath = AssetDatabase.GenerateUniqueAssetPath(
                "Assets/Scenes/OpenWorld/Bloodroot_OpenWorld/" +
                "NavMesh-SpawnGrounding.pending.asset");
            AssetDatabase.CreateAsset(rebuiltData, temporaryPath);
            AssetDatabase.SaveAssets();

            surface.RemoveData();
            surface.navMeshData = null;
            File.Copy(
                ToProjectFilePath(temporaryPath),
                canonicalFullPath,
                overwrite: true);
            canonicalReplaced = true;
            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);

            NavMeshData reboundData = AssetDatabase.LoadAssetAtPath<NavMeshData>(
                canonicalPath);
            if (reboundData == null ||
                !string.Equals(
                    AssetDatabase.AssetPathToGUID(canonicalPath),
                    canonicalGuid,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Focused Open World bake did not preserve the canonical NavMesh asset GUID.");
            }

            surface.navMeshData = reboundData;
            surface.AddData();
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

            AssetDatabase.DeleteAsset(temporaryPath);
            temporaryPath = string.Empty;
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

            if (!string.IsNullOrWhiteSpace(temporaryPath))
            {
                AssetDatabase.DeleteAsset(temporaryPath);
            }

            if (canonicalReplaced)
            {
                File.WriteAllBytes(canonicalFullPath, originalNavMeshBytes);
                AssetDatabase.Refresh(
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);
            }

            throw;
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

    private static string ToProjectFilePath(string assetPath)
    {
        string projectPath = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(
            projectPath,
            assetPath.Replace('/', Path.DirectorySeparatorChar));
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
