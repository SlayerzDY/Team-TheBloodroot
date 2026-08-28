#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Bloodroot.Features.AlphaEnemies;
using Bloodroot.Features.WorldMissions;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Reconciles Stillwater to its production modular architecture. It removes
/// the obsolete primitive facility that overlapped the imported assemblies,
/// moves encounter/navigation sockets onto authored walkable surfaces, and
/// verifies collision, route linkage, and terrain contact in the real scene.
/// </summary>
public static class BloodrootStillwaterProfessionalIntegrationSetup
{
    private const string ScenePath =
        "Assets/Scenes/OpenWorld/Bloodroot_OpenWorld.unity";
    private const string StillwaterMissionRootName =
        "Stillwater Mission Systems";
    private const string AlphaWorldRootName =
        "__ALPHA_WORLD_MISSIONS_V3";
    private const string BlockoutRootName =
        "05 Replaceable Primitive Blockout";
    private const string NavigationRootName =
        "04 Navigation Validation Anchors";
    private const string LegacyBlockoutPrefix =
        "BLOCKOUT_REPLACE_Stillwater";

    private const string TraversalName =
        "SIL_002_003_004_SiltwaterTraversal_Playable";
    private const string TraversalPath =
        "Assets/Meshy Assets/Catalog/SiltwaterTraversal/Prefabs/" +
        "SIL_002_003_004_SiltwaterTraversal_Playable.prefab";
    private const string InvestigationName =
        "SIL_001_009_010_SiltwaterInvestigation_Playable";
    private const string InvestigationPath =
        "Assets/Meshy Assets/Catalog/SiltwaterInvestigation/Prefabs/" +
        "SIL_001_009_010_SiltwaterInvestigation_Playable.prefab";
    private const string TraversalSourcePath =
        "Assets/Generated/PlayableArchitecture/SiltwaterTraversal/Prefabs/" +
        "SIL_002_003_004_SiltwaterTraversal_Playable.prefab";
    private const string InvestigationSourcePath =
        "Assets/Generated/PlayableArchitecture/SiltwaterInvestigation/Prefabs/" +
        "SIL_001_009_010_SiltwaterInvestigation_Playable.prefab";
    private const string TraversalCollisionRoot =
        "CompoundColliderArchitecture_Agent0_DefaultLayer0_PreservesBlockedAreas";
    private const string InvestigationCollisionRoot =
        "ColliderArchitecture_Agent0_DefaultLayer0_PreservesOpenings";

    private static readonly Vector3 TraversalPosition =
        new Vector3(426.4552f, 5.790293f, -528.14655f);
    private static readonly Vector3 InvestigationPosition =
        new Vector3(484.651f, 7.341051f, -536.7732f);
    private static readonly Vector3 DefensePoint =
        TraversalPosition + new Vector3(11.65f, 10.10f, 48.25f);

    private static readonly Vector3[] DefenseOffsets =
    {
        new Vector3(-2.65f, 0f, -.55f),
        new Vector3(-1.35f, 0f, .35f),
        new Vector3(-.05f, 0f, -.55f),
        new Vector3(1.25f, 0f, .35f),
        new Vector3(2.55f, 0f, -.55f)
    };

    [MenuItem("Tools/Bloodroot/Open World/Rebuild Stillwater Professional Integration")]
    public static void ApplyMenu()
    {
        ApplyBatch();
    }

    public static void ApplyBatch()
    {
        if (Application.isPlaying)
            throw new InvalidOperationException(
                "Stillwater integration cannot be authored during Play Mode.");

        bool prefabChanges =
            BloodrootStillwaterModuleRepairSetup.ConfigureModulePrefabs();
        prefabChanges |= ConfigureRuntimeCollisionPrefab(TraversalSourcePath, true);
        prefabChanges |= ConfigureRuntimeCollisionPrefab(InvestigationSourcePath, false);
        AssetDatabase.SaveAssets();

        Scene scene = EditorSceneManager.OpenScene(
            ScenePath,
            OpenSceneMode.Single);

        if (TryValidateScene(scene, out string currentError))
        {
            Debug.Log(
                "BLOODROOT_STILLWATER_PROFESSIONAL_INTEGRATION=PASS " +
                "no_changes=" + (prefabChanges ? "0" : "1") +
                " blockout_renderers=0 core_solid_colliders=148 physical_links=21");
            return;
        }

        Debug.Log(
            "Stillwater professional integration requires reconciliation: " +
            currentError);

        Transform missionRoot = RequireUniqueTransform(
            scene,
            StillwaterMissionRootName);
        Transform alphaRoot = RequireDirectChild(
            missionRoot,
            AlphaWorldRootName);
        Transform blockoutRoot = RequireDirectChild(
            alphaRoot,
            BlockoutRootName);

        foreach (Transform child in blockoutRoot.Cast<Transform>().ToArray())
        {
            if (!child.name.StartsWith(
                    LegacyBlockoutPrefix,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Refusing to remove unexpected content from Stillwater's " +
                    "owned blockout root: " + child.name);
            }

            UnityEngine.Object.DestroyImmediate(child.gameObject);
        }

        RepositionNavigationAnchors(alphaRoot, scene);
        RepositionDefenseEnemies(missionRoot);
        RepositionBlockedSpawnMarkers(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new InvalidOperationException(
                "Could not save the rebuilt Stillwater integration.");

        scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!TryValidateScene(scene, out string error))
        {
            throw new InvalidOperationException(
                "Stillwater professional integration failed validation: " +
                error);
        }

        Debug.Log(
            "BLOODROOT_STILLWATER_PROFESSIONAL_INTEGRATION=PASS " +
            "no_changes=0 blockout_renderers=0 core_solid_colliders=148 physical_links=21");
    }

    private static bool ConfigureRuntimeCollisionPrefab(string path, bool traversal)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            bool changed = ConfigureAssemblyForCampaign(root, traversal);
            if (changed && PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                throw new InvalidOperationException("Could not save " + path + ".");
            return changed;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    public static void ValidateNavigationBatch()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!TryValidateScene(scene, out string error))
            throw new InvalidOperationException(error);
        NavMeshSurface surface = FindSceneComponents<NavMeshSurface>(scene).Single();
        if (surface.navMeshData == null)
            throw new InvalidOperationException("Stillwater requires the saved Open World NavMesh.");
        surface.RemoveData();
        surface.AddData();
        foreach (NavMeshLink link in FindSceneComponents<NavMeshLink>(scene)
                     .Where(item => item.isActiveAndEnabled))
            link.UpdateLink();
        Physics.SyncTransforms();

        Transform mission = RequireUniqueTransform(scene, StillwaterMissionRootName);
        Transform anchors = RequireDirectChild(
            RequireDirectChild(mission, AlphaWorldRootName), NavigationRootName);
        var samples = new List<NavMeshHit>();
        var navigationErrors = new List<string>();
        foreach (Transform anchor in anchors.Cast<Transform>().OrderBy(
                     item => item.name, StringComparer.Ordinal))
        {
            if (!NavMesh.SamplePosition(anchor.position, out NavMeshHit hit, .75f, 1) ||
                Mathf.Abs(hit.position.y - anchor.position.y) > .35f)
                throw new InvalidOperationException(anchor.name +
                    " is not on its intended supported navigation level.");
            samples.Add(hit);
        }
        foreach (Vector2Int route in new[]
                 { new Vector2Int(3, 4), new Vector2Int(4, 5), new Vector2Int(5, 6) })
        {
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(samples[route.x].position,
                    samples[route.y].position, 1, path) ||
                path.status != NavMeshPathStatus.PathComplete)
                navigationErrors.Add("Stillwater route NAV_ANCHOR_" +
                    (route.x + 1).ToString("00") + " to NAV_ANCHOR_" +
                    (route.y + 1).ToString("00") + " is " + path.status +
                    " from " + samples[route.x].position + " to " +
                    samples[route.y].position + "; corners: " +
                    string.Join(" -> ", path.corners.Select(point => point.ToString())) + ".");
        }

        Transform investigationRoutes = RequireDirectChild(
            RequireUniqueTransform(scene, InvestigationName), "PersistentHardRouteMarkers");
        Vector3 recordsPosition = RequireDirectChild(investigationRoutes, "RecordsOffice").position;
        Vector3 vaultPosition = RequireDirectChild(investigationRoutes, "QualityVault").position;
        var vaultPath = new NavMeshPath();
        if (!NavMesh.SamplePosition(recordsPosition, out NavMeshHit recordsHit, .75f, 1) ||
            !NavMesh.SamplePosition(vaultPosition, out NavMeshHit vaultHit, .75f, 1) ||
            !NavMesh.CalculatePath(recordsHit.position, vaultHit.position, 1, vaultPath) ||
            vaultPath.status != NavMeshPathStatus.PathComplete)
            navigationErrors.Add("Stillwater's records office must connect through the open vault doorway.");

        foreach (Vector2 center in new[] { new Vector2(18f, 6f), new Vector2(30f, 6f),
                     new Vector2(18f, 18f), new Vector2(30f, 18f) })
        {
            foreach (float y in new[] { .1f, 21f, 31.6f })
            {
                Vector3 point = TraversalPosition + new Vector3(center.x, y, center.y);
                if (NavMesh.SamplePosition(point, out _, .3f, 1))
                    throw new InvalidOperationException(
                        "A closed silo interior or roof received walkable navigation at " + point + ".");
            }
        }

        var spawnDefinitions = new List<(Transform marker, GameObject prefab)>();
        foreach (WorldArrivalEnemySpawner spawner in FindSceneComponents<WorldArrivalEnemySpawner>(scene))
            spawnDefinitions.AddRange(spawner.Spawns.Select(item => (item.SpawnPoint, item.EnemyPrefab)));
        foreach (OpenWorldAmbientThreatSpawner spawner in FindSceneComponents<OpenWorldAmbientThreatSpawner>(scene))
            spawnDefinitions.AddRange(spawner.Spawns.Select(item => (item.SpawnPoint, item.EnemyPrefab)));
        foreach (WorldLandmarkEnemySpawner spawner in FindSceneComponents<WorldLandmarkEnemySpawner>(scene))
            spawnDefinitions.AddRange(spawner.Spawns.Select(item => (item.SpawnPoint, item.EnemyPrefab)));
        int checkedSpawns = 0;
        foreach ((Transform marker, GameObject prefab) in spawnDefinitions)
        {
            if (marker == null || marker.position.x < 390f || marker.position.x > 530f ||
                marker.position.z < -560f || marker.position.z > -460f)
                continue;
            if (!CampaignSafetyEnemyRuntimeAdapter.TryResolveGroundedSpawnPosition(
                    prefab, marker.position, .75f, out NavMeshHit hit, out error))
            {
                navigationErrors.Add(marker.name + ": " + error);
                continue;
            }
            NavMeshAgent agent = prefab.GetComponent<NavMeshAgent>();
            float radius = Mathf.Max(.1f, agent.radius);
            float height = Mathf.Max(radius * 2f, agent.height);
            Vector3 bottom = hit.position + Vector3.up * (.06f + radius);
            Vector3 top = hit.position + Vector3.up * (.06f + height - radius);
            Collider obstruction = Physics.OverlapCapsule(bottom, top, radius,
                    Physics.AllLayers, QueryTriggerInteraction.Ignore)
                .FirstOrDefault(item => item != null && !(item is TerrainCollider));
            if (obstruction != null)
                navigationErrors.Add(marker.name + " at " + hit.position +
                    " lacks enemy capsule clearance from " + obstruction.name + ".");
            checkedSpawns++;
        }
        if (navigationErrors.Count != 0)
            throw new InvalidOperationException(string.Join("\n", navigationErrors));
        Debug.Log("BLOODROOT_STILLWATER_NAVIGATION=PASS anchors=" + samples.Count +
                  " connected_routes=4 closed_silos=4 clear_grounded_spawns=" + checkedSpawns);
    }

    /// <summary>Shared by the focused repair and the original source builders.</summary>
    public static bool ConfigureAssemblyForCampaign(GameObject root, bool traversal)
    {
        bool changed = BloodrootStillwaterModuleRepairSetup.ConfigureAssemblyModulePlacements(root, traversal);
        changed |= BloodrootStillwaterArtPolishSetup.ConfigureAssembly(root, traversal);
        string expectedName = traversal ? TraversalCollisionRoot : InvestigationCollisionRoot;
        Transform collisionRoot = root.transform.Cast<Transform>().Single(item =>
            item.name.StartsWith(traversal ? "CompoundColliderArchitecture_" :
                "ColliderArchitecture_", StringComparison.Ordinal));
        if (collisionRoot.name != expectedName)
        {
            collisionRoot.name = expectedName;
            changed = true;
        }
        if (collisionRoot.gameObject.layer != 0)
        {
            collisionRoot.gameObject.layer = 0;
            changed = true;
        }

        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
        {
            int desiredLayer = collider.isTrigger ? 2 : 0;
            if (collider.gameObject.layer != desiredLayer)
            {
                collider.gameObject.layer = desiredLayer;
                changed = true;
            }
            // These are serialized route measurements, not gameplay triggers.
            if (collider.isTrigger && collider.enabled)
            {
                collider.enabled = false;
                changed = true;
            }
            if (collider.isTrigger || collider.name.IndexOf(
                    "NO_NAV_CREDIT", StringComparison.Ordinal) < 0)
                continue;

            NavMeshModifier modifier = collider.GetComponent<NavMeshModifier>();
            if (modifier == null)
            {
                modifier = collider.gameObject.AddComponent<NavMeshModifier>();
                changed = true;
            }
            if (!modifier.enabled || modifier.ignoreFromBuild ||
                !modifier.overrideArea || modifier.area != 1)
            {
                modifier.enabled = true;
                modifier.ignoreFromBuild = false;
                modifier.overrideArea = true;
                modifier.area = 1; // Unity's built-in Not Walkable area.
                changed = true;
            }
        }
        return changed;
    }

    private static void RepositionNavigationAnchors(
        Transform alphaRoot,
        Scene scene)
    {
        Transform navigationRoot = RequireDirectChild(
            alphaRoot,
            NavigationRootName);
        Terrain terrain = FindSceneComponents<Terrain>(scene).Single();
        Vector3[] anchorInputs = BuildNavigationAnchorInputs(terrain);
        Transform[] anchors = navigationRoot.Cast<Transform>()
            .Where(child => child.name.StartsWith(
                "NAV_ANCHOR_",
                StringComparison.Ordinal))
            .OrderBy(child => child.name, StringComparer.Ordinal)
            .ToArray();
        if (anchors.Length != anchorInputs.Length)
            throw new InvalidOperationException(
                "Stillwater requires exactly ten navigation anchors.");

        for (int index = 0; index < anchors.Length; index++)
        {
            anchors[index].position = anchorInputs[index] + Vector3.up * 0.10f;
            EditorUtility.SetDirty(anchors[index]);
        }
    }

    private static void RepositionBlockedSpawnMarkers(Scene scene)
    {
        Terrain terrain = FindSceneComponents<Terrain>(scene).Single();
        foreach (Transform marker in FindSceneComponents<Transform>(scene))
        {
            if (!marker.name.StartsWith("Spawn_", StringComparison.Ordinal) &&
                !marker.name.StartsWith("SPAWN_POINT_", StringComparison.Ordinal))
                continue;
            Vector3 position = marker.position;
            if (Mathf.Abs(position.x - 438f) > .25f ||
                Mathf.Abs(position.z + 514f) > .25f)
                continue;
            // The old marker sat inside the newly restored catwalk support.
            marker.position = GroundPoint(terrain, new Vector2(440.5f, -513.5f)) +
                              Vector3.up * .10f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(marker);
            EditorUtility.SetDirty(marker);
        }
    }

    private static void RepositionDefenseEnemies(Transform missionRoot)
    {
        Transform[] enemies = missionRoot
            .GetComponentsInChildren<Transform>(true)
            .Where(item => item.name.StartsWith(
                "SummonedHogMinion_",
                StringComparison.Ordinal))
            .OrderBy(item => item.name, StringComparer.Ordinal)
            .ToArray();
        // The current tower campaign has no legacy generator-defense minions.
        // Repair older authored instances only; never recreate retired gameplay.
        if (enemies.Length == 0)
            return;
        if (enemies.Length != DefenseOffsets.Length)
            throw new InvalidOperationException(
                "Stillwater generator defense requires five authored minions.");

        for (int index = 0; index < enemies.Length; index++)
        {
            enemies[index].position = DefensePoint + DefenseOffsets[index];
            PrefabUtility.RecordPrefabInstancePropertyModifications(enemies[index]);
            EditorUtility.SetDirty(enemies[index]);
        }
    }

    private static bool TryValidateScene(Scene scene, out string error)
    {
        try
        {
            Transform missionRoot = RequireUniqueTransform(
                scene,
                StillwaterMissionRootName);
            Transform alphaRoot = RequireDirectChild(
                missionRoot,
                AlphaWorldRootName);
            Transform blockoutRoot = RequireDirectChild(
                alphaRoot,
                BlockoutRootName);
            if (blockoutRoot.childCount != 0 ||
                blockoutRoot.GetComponentsInChildren<Renderer>(true).Length != 0 ||
                blockoutRoot.GetComponentsInChildren<Collider>(true).Length != 0)
            {
                throw new InvalidOperationException(
                    "The obsolete Stillwater primitive blockout is still present.");
            }

            if (FindSceneComponents<Transform>(scene).Any(item =>
                    item.name.StartsWith(
                        LegacyBlockoutPrefix,
                        StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "A legacy Stillwater blockout object remains in the scene.");
            }

            Transform traversal = ValidateConnectedAssembly(
                scene,
                TraversalName,
                TraversalPath,
                TraversalPosition,
                TraversalCollisionRoot,
                76,
                18,
                18,
                4);
            Transform investigation = ValidateConnectedAssembly(
                scene,
                InvestigationName,
                InvestigationPath,
                InvestigationPosition,
                InvestigationCollisionRoot,
                51,
                3,
                3,
                5);

            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer < 0 ||
                Physics.GetIgnoreLayerCollision(playerLayer, 0))
            {
                throw new InvalidOperationException(
                    "Player collision with Stillwater's Default-layer " +
                    "architecture is disabled in Physics settings.");
            }

            Terrain terrain = FindSceneComponents<Terrain>(scene).Single();
            ValidateTerrainContact(
                traversal,
                "COLLIDER_WarehouseFloor_24x16",
                terrain);
            ValidateTerrainContact(
                investigation,
                "COLLIDER_MillGround_30x20",
                terrain);
            ValidateColliderComponentGrounding(traversal, terrain);
            ValidateColliderComponentGrounding(investigation, terrain);
            BloodrootStillwaterArtPolishSetup.ValidateAssembly(traversal.gameObject, true);
            BloodrootStillwaterArtPolishSetup.ValidateAssembly(investigation.gameObject, false);
            Transform vaultDoor = RequireDirectChild(
                RequireDirectChild(investigation, "MovingParts_Separate_OpenReviewState"),
                "QV_DOOR_OpenReview_85deg");
            BoxCollider vaultLeaf = vaultDoor.GetComponent<BoxCollider>();
            NavMeshObstacle vaultObstacle = vaultDoor.GetComponent<NavMeshObstacle>();
            if (!Approximately(vaultDoor.localPosition,
                    new Vector3(40.08f, 1.13f, 6.15f), .001f) ||
                Quaternion.Angle(vaultDoor.localRotation,
                    Quaternion.Euler(0f, 180f, 0f)) > .001f ||
                vaultLeaf == null || !vaultLeaf.enabled || vaultLeaf.isTrigger ||
                vaultObstacle == null || !vaultObstacle.enabled || !vaultObstacle.carving ||
                vaultLeaf.bounds.min.z - investigation.position.z < 6.06f)
                throw new InvalidOperationException(
                    "The open vault door must remain parked at its jamb, outside the two-metre portal.");
            ValidateNavigationAnchors(alphaRoot, terrain);
            ValidateDefenseEnemies(missionRoot);

            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static Transform ValidateConnectedAssembly(
        Scene scene,
        string name,
        string expectedPath,
        Vector3 expectedPosition,
        string collisionRootName,
        int expectedBoxColliders,
        int expectedMeshColliders,
        int expectedLinks,
        int expectedProbes)
    {
        Transform root = RequireUniqueTransform(scene, name);
        GameObject owner = root.gameObject;
        if (root.parent != null ||
            PrefabUtility.GetOutermostPrefabInstanceRoot(owner) != owner ||
            PrefabUtility.GetPrefabInstanceStatus(owner) !=
                PrefabInstanceStatus.Connected ||
            !string.Equals(
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(owner),
                expectedPath,
                StringComparison.Ordinal) ||
            !Approximately(root.position, expectedPosition, 0.001f) ||
            Quaternion.Angle(root.rotation, Quaternion.identity) > 0.001f ||
            !Approximately(root.lossyScale, Vector3.one, 0.0001f))
        {
            throw new InvalidOperationException(
                name + " is not the exact connected production prefab at its " +
                "authored natural-scale scene pose.");
        }

        Transform collisionRoot = RequireDirectChild(root, collisionRootName);
        BoxCollider[] boxes = collisionRoot.GetComponentsInChildren<BoxCollider>(true);
        MeshCollider[] meshes = collisionRoot.GetComponentsInChildren<MeshCollider>(true);
        NavMeshLink[] links = owner.GetComponentsInChildren<NavMeshLink>(true);
        if (boxes.Length != expectedBoxColliders ||
            meshes.Length != expectedMeshColliders ||
            links.Length != expectedLinks)
        {
            throw new InvalidOperationException(
                name + " collision/link inventory drifted: boxes=" +
                boxes.Length + ", meshes=" + meshes.Length + ", links=" +
                links.Length + ".");
        }

        foreach (BoxCollider box in boxes)
        {
            if (!box.enabled || box.isTrigger ||
                box.size.x <= 0f || box.size.y <= 0f || box.size.z <= 0f)
            {
                throw new InvalidOperationException(
                    name + " contains an invalid BoxCollider: " + box.name);
            }

            bool noNavCredit = box.name.IndexOf(
                "NO_NAV_CREDIT",
                StringComparison.Ordinal) >= 0;
            if (box.gameObject.layer != 0)
            {
                throw new InvalidOperationException(
                    name + " collider '" + box.name + "' is not on its " +
                    "authored runtime/navigation layer.");
            }
            NavMeshModifier[] modifiers = box.GetComponents<NavMeshModifier>();
            if (noNavCredit && (modifiers.Length != 1 || !modifiers[0].enabled ||
                    modifiers[0].ignoreFromBuild || !modifiers[0].overrideArea ||
                    modifiers[0].area != 1))
                throw new InvalidOperationException(box.name +
                    " must block navigation without exposing terrain beneath the silo.");
        }

        foreach (MeshCollider mesh in meshes)
        {
            if (!mesh.enabled || mesh.isTrigger || mesh.convex ||
                mesh.sharedMesh == null || mesh.gameObject.layer != 0)
            {
                throw new InvalidOperationException(
                    name + " contains an invalid physical ramp collider: " +
                    mesh.name);
            }
        }

        Collider[] probes = owner.GetComponentsInChildren<Collider>(true)
            .Where(item => item.isTrigger).ToArray();
        if (probes.Length != expectedProbes || probes.Any(item =>
                item.enabled || item.gameObject.layer != 2))
            throw new InvalidOperationException(name +
                " route measurement volumes must remain disabled and nonphysical.");

        foreach (NavMeshLink link in links)
        {
            MeshCollider backing = link.GetComponent<MeshCollider>();
            if (!link.enabled || !link.bidirectional ||
                link.agentTypeID != 0 || backing == null ||
                backing.sharedMesh == null || link.gameObject.layer != 0)
            {
                throw new InvalidOperationException(
                    name + " contains an unbacked or invalid route link: " +
                    link.name);
            }
        }

        Renderer[] renderers = owner.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0 || renderers.Any(renderer =>
                renderer is MeshRenderer &&
                (renderer.GetComponent<MeshFilter>() == null ||
                 renderer.GetComponent<MeshFilter>().sharedMesh == null)))
        {
            throw new InvalidOperationException(
                name + " contains missing professional render geometry.");
        }

        return root;
    }

    private static void ValidateTerrainContact(
        Transform assembly,
        string floorColliderName,
        Terrain terrain)
    {
        BoxCollider floor = assembly.GetComponentsInChildren<BoxCollider>(true)
            .Single(item => string.Equals(
                item.name,
                floorColliderName,
                StringComparison.Ordinal));
        Vector3 center = floor.bounds.center;
        float terrainY = SampleWorldHeight(
            terrain,
            new Vector2(center.x, center.z));
        float contactY = floor.bounds.max.y;
        if (Mathf.Abs(contactY - terrainY) > 0.08f)
        {
            throw new InvalidOperationException(
                assembly.name + " floor is not seated on terrain. gap=" +
                Mathf.Abs(contactY - terrainY).ToString("0.000") + "m.");
        }
    }

    private static void ValidateColliderComponentGrounding(
        Transform assembly,
        Terrain terrain)
    {
        Collider[] colliders = assembly.GetComponentsInChildren<Collider>(true)
            .Where(item => item.enabled && !item.isTrigger &&
                item.name.IndexOf("NO_NAV_CREDIT", StringComparison.Ordinal) < 0 &&
                !HasAncestorNamed(item.transform, "MovingParts_Separate_OpenReviewState"))
            .ToArray();
        var adjacency = new List<int>[colliders.Length];
        var grounded = new bool[colliders.Length];

        for (int index = 0; index < colliders.Length; index++)
        {
            adjacency[index] = new List<int>();
            Bounds bounds = colliders[index].bounds;
            float terrainY = SampleWorldHeight(
                terrain,
                new Vector2(bounds.center.x, bounds.center.z));
            grounded[index] = bounds.min.y <= terrainY + 0.30f &&
                              bounds.max.y >= terrainY - 0.30f;
        }

        for (int left = 0; left < colliders.Length; left++)
        {
            Bounds expanded = colliders[left].bounds;
            expanded.Expand(0.30f);
            for (int right = left + 1; right < colliders.Length; right++)
            {
                if (!expanded.Intersects(colliders[right].bounds))
                    continue;
                adjacency[left].Add(right);
                adjacency[right].Add(left);
            }
        }

        var visited = new bool[colliders.Length];
        for (int seed = 0; seed < colliders.Length; seed++)
        {
            if (visited[seed])
                continue;

            var queue = new Queue<int>();
            var component = new List<int>();
            bool componentGrounded = false;
            queue.Enqueue(seed);
            visited[seed] = true;
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                component.Add(current);
                componentGrounded |= grounded[current];
                foreach (int next in adjacency[current])
                {
                    if (visited[next])
                        continue;
                    visited[next] = true;
                    queue.Enqueue(next);
                }
            }

            if (!componentGrounded)
            {
                throw new InvalidOperationException(
                    assembly.name + " contains an ungrounded collision " +
                    "component beginning at " + colliders[component[0]].name +
                    ".");
            }
        }
    }

    private static void ValidateNavigationAnchors(
        Transform alphaRoot,
        Terrain terrain)
    {
        Transform navigationRoot = RequireDirectChild(
            alphaRoot,
            NavigationRootName);
        Transform[] anchors = navigationRoot.Cast<Transform>()
            .Where(child => child.name.StartsWith(
                "NAV_ANCHOR_",
                StringComparison.Ordinal))
            .OrderBy(child => child.name, StringComparer.Ordinal)
            .ToArray();
        Vector3[] expected = BuildNavigationAnchorInputs(terrain);
        if (anchors.Length != expected.Length)
            throw new InvalidOperationException(
                "Stillwater navigation-anchor inventory drifted.");

        for (int index = 0; index < anchors.Length; index++)
        {
            if (!Approximately(
                    anchors[index].position,
                    expected[index] + Vector3.up * 0.10f,
                    0.01f))
            {
                throw new InvalidOperationException(
                    anchors[index].name +
                    " is not on its rebuilt Stillwater route surface.");
            }
        }
    }

    private static void ValidateDefenseEnemies(Transform missionRoot)
    {
        Transform[] enemies = missionRoot
            .GetComponentsInChildren<Transform>(true)
            .Where(item => item.name.StartsWith(
                "SummonedHogMinion_",
                StringComparison.Ordinal))
            .OrderBy(item => item.name, StringComparer.Ordinal)
            .ToArray();
        if (enemies.Length == 0)
            return;
        if (enemies.Length != DefenseOffsets.Length)
            throw new InvalidOperationException(
                "Stillwater generator-defense enemy inventory drifted.");

        for (int index = 0; index < enemies.Length; index++)
        {
            if (!Approximately(
                    enemies[index].position,
                    DefensePoint + DefenseOffsets[index],
                    0.01f))
            {
                throw new InvalidOperationException(
                    enemies[index].name +
                    " is not seated on the authored catwalk defense deck.");
            }
        }
    }

    private static Vector3[] BuildNavigationAnchorInputs(Terrain terrain)
    {
        Vector3 warehouseExterior =
            TraversalPosition + new Vector3(18.6f, 0.10f, 35f);
        Vector3 warehouseInterior =
            TraversalPosition + new Vector3(18.6f, 0.10f, 44f);
        Vector3 siloCatwalk =
            TraversalPosition + new Vector3(20f, 22.10f, 2f);
        return new[]
        {
            GroundPoint(terrain, new Vector2(397f, -548f)),
            GroundPoint(terrain, new Vector2(426f, -537f)),
            GroundPoint(terrain, new Vector2(433f, -519f)),
            warehouseExterior,
            warehouseInterior,
            DefensePoint,
            siloCatwalk,
            GroundPoint(terrain, new Vector2(443f, -479f)),
            GroundPoint(terrain, new Vector2(462f, -480f)),
            GroundPoint(terrain, new Vector2(505f, -470f))
        };
    }

    private static Vector3 GroundPoint(Terrain terrain, Vector2 point)
    {
        return new Vector3(
            point.x,
            SampleWorldHeight(terrain, point),
            point.y);
    }

    private static bool HasAncestorNamed(Transform item, string name)
    {
        while (item != null)
        {
            if (string.Equals(item.name, name, StringComparison.Ordinal))
                return true;
            item = item.parent;
        }
        return false;
    }

    private static float SampleWorldHeight(Terrain terrain, Vector2 point)
    {
        return terrain.SampleHeight(new Vector3(point.x, 0f, point.y)) +
               terrain.transform.position.y;
    }

    private static Transform RequireUniqueTransform(Scene scene, string name)
    {
        Transform[] matches = FindSceneComponents<Transform>(scene)
            .Where(item => string.Equals(item.name, name, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException(
                "Expected one scene object named '" + name + "'; found " +
                matches.Length + ".");
        return matches[0];
    }

    private static Transform RequireDirectChild(Transform parent, string name)
    {
        Transform[] matches = parent.Cast<Transform>()
            .Where(child => string.Equals(child.name, name, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException(
                "Expected one direct child '" + name + "' beneath '" +
                parent.name + "'; found " + matches.Length + ".");
        return matches[0];
    }

    private static IEnumerable<T> FindSceneComponents<T>(Scene scene)
        where T : Component
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true))
            .Where(component => component != null);
    }

    private static bool Approximately(Vector3 left, Vector3 right, float tolerance)
    {
        return Mathf.Abs(left.x - right.x) <= tolerance &&
               Mathf.Abs(left.y - right.y) <= tolerance &&
               Mathf.Abs(left.z - right.z) <= tolerance;
    }
}
#endif
