#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Repairs the checked-in Stillwater FBX wrapper transforms without replacing
/// source meshes, materials, LODs, prefab identities, or gameplay components.
/// The source generator fitted mesh vertices to the catalog pivot but retained
/// primitive/join object translations. Those translations must not be applied
/// again by the Unity render wrapper.
/// </summary>
public static class BloodrootStillwaterModuleRepairSetup
{
    private const string TraversalRoot = "Assets/Generated/PlayableArchitecture/SiltwaterTraversal";
    private const string InvestigationRoot = "Assets/Generated/PlayableArchitecture/SiltwaterInvestigation";
    private const float PositionTolerance = .00001f;
    private const float BoundsTolerance = .001f;
    private static readonly Vector3 FbxXReflection = new Vector3(-1f, 1f, 1f);
    private static readonly Quaternion FbxYUpRotation = Quaternion.Euler(-90f, 0f, 0f);
    private static readonly float[] SiloRingLodHeights = { .08f, .025f, .008f };
    // Original exact dimension/pivot contracts for both Stillwater families.
    // Embedded from the verified Stillwater catalog so a fresh clone needs no
    // external staging directory or full world-prompt CSV. All face +Z.
    private static readonly Dictionary<string, ModuleSpec> catalog =
        new Dictionary<string, ModuleSpec>(StringComparer.Ordinal)
        {
            { "GE_CAGE_3M", new ModuleSpec(new Vector3(0.9f, 3f, 0.8f), "BC", true) },
            { "GE_CATWALK_4M", new ModuleSpec(new Vector3(4f, 0.2f, 1.2f), "LLF", true) },
            { "GE_CATWALK_RAIL", new ModuleSpec(new Vector3(4f, 1.1f, 0.1f), "LLF", true) },
            { "GE_FLOOR_5", new ModuleSpec(new Vector3(5f, 0.3f, 5f), "LLF", true) },
            { "GE_FLOOR_STAIROPEN", new ModuleSpec(new Vector3(5f, 0.3f, 5f), "LLF", true) },
            { "GE_LADDER_3M", new ModuleSpec(new Vector3(0.6f, 3f, 0.3f), "BC", true) },
            { "GE_LANDING", new ModuleSpec(new Vector3(1.5f, 0.2f, 1.5f), "LLF", true) },
            { "GE_PURGE_VENT", new ModuleSpec(new Vector3(1.2f, 1.5f, 0.7f), "BC", true) },
            { "GE_ROOF_HATCH", new ModuleSpec(new Vector3(0.9f, 0.9f, 0.1f), "HL", true) },
            { "GE_SILO_BASECONE", new ModuleSpec(new Vector3(7f, 3f, 7f), "BC", true) },
            { "GE_SILO_RING", new ModuleSpec(new Vector3(8f, 4f, 8f), "BC", true) },
            { "GE_SILO_ROOF", new ModuleSpec(new Vector3(8.3f, 2.3f, 8.3f), "BC", true) },
            { "GE_SILO_SUPPORT", new ModuleSpec(new Vector3(0.4f, 5f, 0.4f), "BC", true) },
            { "GE_STAIR", new ModuleSpec(new Vector3(1.2f, 2.5f, 4.2f), "LLF", true) },
            { "GE_TRANSFER_PIPE", new ModuleSpec(new Vector3(0.5f, 0.5f, 4f), "C", true) },
            { "GE_WALL_5", new ModuleSpec(new Vector3(5f, 5f, 0.3f), "LLF", true) },
            { "GE_WALL_DOOR", new ModuleSpec(new Vector3(5f, 5f, 0.3f), "LLF", true) },
            { "GE_WALL_WINDOW", new ModuleSpec(new Vector3(5f, 5f, 0.3f), "LLF", true) },
            { "MC_DECK_4", new ModuleSpec(new Vector3(4f, 0.2f, 1.2f), "LLF", true) },
            { "MC_DECK_CORNER", new ModuleSpec(new Vector3(1.5f, 0.2f, 1.5f), "LLF", true) },
            { "MC_RAIL_4", new ModuleSpec(new Vector3(4f, 1.1f, 0.1f), "LLF", true) },
            { "MC_STAIR", new ModuleSpec(new Vector3(1.2f, 2f, 3.4f), "LLF", true) },
            { "MC_SUPPORT", new ModuleSpec(new Vector3(0.2f, 4f, 0.2f), "BC", true) },
            { "MILL_BEAM_4", new ModuleSpec(new Vector3(4f, 0.45f, 0.3f), "LLF", false) },
            { "MILL_CLERESTORY", new ModuleSpec(new Vector3(4f, 2f, 2f), "LLF", false) },
            { "MILL_COLUMN", new ModuleSpec(new Vector3(0.45f, 8f, 0.45f), "BC", false) },
            { "MILL_MAN_DOOR", new ModuleSpec(new Vector3(1.24f, 2.24f, 0.06f), "HL", false) },
            { "MILL_OFFICE_DOORWALL", new ModuleSpec(new Vector3(2f, 3.2f, 0.15f), "LLF", false) },
            { "MILL_OFFICE_FLOOR", new ModuleSpec(new Vector3(2f, 0.22f, 2f), "LLF", false) },
            { "MILL_OFFICE_WALL", new ModuleSpec(new Vector3(2f, 3.2f, 0.15f), "LLF", false) },
            { "MILL_OFFICE_WINDOWWALL", new ModuleSpec(new Vector3(2f, 3.2f, 0.15f), "LLF", false) },
            { "MILL_PARAPET_4", new ModuleSpec(new Vector3(4f, 0.8f, 0.2f), "LLF", false) },
            { "MILL_RAIL_2M", new ModuleSpec(new Vector3(2f, 1.1f, 0.1f), "LLF", false) },
            { "MILL_ROLLUP_DOOR", new ModuleSpec(new Vector3(3.46f, 3.66f, 0.1f), "top-center", false) },
            { "MILL_ROOF_4", new ModuleSpec(new Vector3(4f, 0.25f, 4f), "LLF", false) },
            { "MILL_SLAB_4", new ModuleSpec(new Vector3(4f, 0.3f, 4f), "LLF", false) },
            { "MILL_STAIR", new ModuleSpec(new Vector3(1.2f, 2f, 3.4f), "LLF", false) },
            { "MILL_STAIR_LANDING", new ModuleSpec(new Vector3(1.5f, 0.2f, 1.5f), "LLF", false) },
            { "MILL_WALL_4", new ModuleSpec(new Vector3(4f, 8f, 0.25f), "LLF", false) },
            { "MILL_WALL_COLLAPSED", new ModuleSpec(new Vector3(4f, 8f, 1.2f), "LLF", false) },
            { "MILL_WALL_DOOR", new ModuleSpec(new Vector3(4f, 8f, 0.25f), "LLF", false) },
            { "MILL_WALL_LOADING", new ModuleSpec(new Vector3(4f, 8f, 0.25f), "LLF", false) },
            { "MILL_WALL_WINDOW", new ModuleSpec(new Vector3(4f, 8f, 0.25f), "LLF", false) },
            { "MILL_WINDOWUNIT", new ModuleSpec(new Vector3(2.44f, 1.54f, 0.1f), "C", false) },
            { "MW_BEAM_4", new ModuleSpec(new Vector3(4f, 0.4f, 0.3f), "LLF", true) },
            { "MW_BOLLARD", new ModuleSpec(new Vector3(0.2f, 1.1f, 0.2f), "BC", true) },
            { "MW_CAGE_GATE", new ModuleSpec(new Vector3(1f, 2.1f, 0.08f), "HL", true) },
            { "MW_CAGE_PANEL", new ModuleSpec(new Vector3(2f, 2.6f, 0.08f), "LLF", true) },
            { "MW_COLUMN", new ModuleSpec(new Vector3(0.35f, 6.5f, 0.35f), "BC", true) },
            { "MW_DOCK_CANOPY", new ModuleSpec(new Vector3(4f, 0.18f, 3f), "LLF", true) },
            { "MW_DOCK_LEVELER", new ModuleSpec(new Vector3(3f, 0.15f, 2f), "hinge-rear-center", true) },
            { "MW_DOCK_PLATFORM", new ModuleSpec(new Vector3(4f, 0.9f, 3f), "LLF", true) },
            { "MW_DOCK_RAMP", new ModuleSpec(new Vector3(4f, 0.9f, 8f), "LLF", true) },
            { "MW_ROLLUP", new ModuleSpec(new Vector3(3.46f, 3.56f, 0.1f), "top-center", true) },
            { "MW_ROOF_4", new ModuleSpec(new Vector3(4f, 0.25f, 4f), "LLF", true) },
            { "MW_SLAB_4", new ModuleSpec(new Vector3(4f, 0.3f, 4f), "LLF", true) },
            { "MW_WALL_4", new ModuleSpec(new Vector3(4f, 6.5f, 0.2f), "LLF", true) },
            { "MW_WALL_BROKEN", new ModuleSpec(new Vector3(4f, 6.5f, 0.8f), "LLF", true) },
            { "MW_WALL_DOCK", new ModuleSpec(new Vector3(4f, 6.5f, 0.2f), "LLF", true) },
            { "MW_WALL_DOOR", new ModuleSpec(new Vector3(4f, 6.5f, 0.2f), "LLF", true) },
            { "QL_CEILING_2", new ModuleSpec(new Vector3(2f, 0.15f, 2f), "LLF", false) },
            { "QL_DOOR", new ModuleSpec(new Vector3(1.04f, 2.14f, 0.05f), "HL", false) },
            { "QL_FLOOR_2", new ModuleSpec(new Vector3(2f, 0.2f, 2f), "LLF", false) },
            { "QL_PASSTHRU_SHUTTER", new ModuleSpec(new Vector3(1.24f, 0.74f, 0.05f), "top-hinge-center", false) },
            { "QL_WALL_DOOR", new ModuleSpec(new Vector3(2f, 3.2f, 0.15f), "LLF", false) },
            { "QL_WALL_OBS", new ModuleSpec(new Vector3(2f, 3.2f, 0.15f), "LLF", false) },
            { "QL_WALL_PASSTHRU", new ModuleSpec(new Vector3(2f, 3.2f, 0.15f), "LLF", false) },
            { "QL_WALL_SOLID", new ModuleSpec(new Vector3(2f, 3.2f, 0.15f), "LLF", false) },
            { "QL_WINDOWUNIT", new ModuleSpec(new Vector3(1.44f, 1.04f, 0.08f), "C", false) },
            { "QV_CEILING", new ModuleSpec(new Vector3(2f, 0.2f, 2f), "LLF", false) },
            { "QV_DOOR", new ModuleSpec(new Vector3(1.26f, 2.26f, 0.16f), "HL", false) },
            { "QV_DOORFRAME_WALL", new ModuleSpec(new Vector3(2f, 3.2f, 0.25f), "LLF", false) },
            { "QV_FLOOR", new ModuleSpec(new Vector3(2f, 0.25f, 2f), "LLF", false) },
            { "QV_WALL", new ModuleSpec(new Vector3(2f, 3.2f, 0.25f), "LLF", false) },
        };

    // Safety deliberately removed these unused Investigation prefabs. Keep
    // their source contracts available to future builders, but never recreate
    // them as part of repairing the currently imported 69-module campaign set.
    private static readonly HashSet<string> PrunedInvestigationModules = new HashSet<string>(StringComparer.Ordinal)
    {
        "MILL_PARAPET_4", "MILL_WALL_COLLAPSED", "MILL_WINDOWUNIT",
        "QL_WALL_DOOR", "QL_WINDOWUNIT"
    };

    public static void ApplyBatch()
    {
        bool changed = ConfigureModulePrefabs();
        Debug.Log("STILLWATER_MODULE_REPAIR=PASS modules=69 movers=9 no_changes=" +
                  (changed ? "0" : "1"));
    }

    /// <summary>Updates only the 69 imported source-module and nine mover prefabs.</summary>
    public static bool ConfigureModulePrefabs()
    {
        // Validate both exact rosters before mutating either family. A missing
        // or unexpected module must not cause a partially repaired asset set.
        string[] traversalModules = ModulePaths(TraversalRoot, 39);
        string[] investigationModules = ModulePaths(InvestigationRoot, 30);
        string[] traversalMovers = MoverPaths(TraversalRoot, 4);
        string[] investigationMovers = MoverPaths(InvestigationRoot, 5);
        bool changed = ConfigureFamily(traversalModules, traversalMovers);
        changed |= ConfigureFamily(investigationModules, investigationMovers);
        return changed;
    }

    /// <summary>
    /// Shared with the source builders immediately after their LODGroup setup.
    /// Catalog LLF is lower-left-front; HL is left-hinge at mid-height. A
    /// render-only X reflection restores the source's +X while preserving +Z
    /// stair ascent, unlike a 180-degree yaw which would reverse the stairs.
    /// </summary>
    public static bool ConfigureModule(GameObject wrapper, string moduleId)
    {
        if (wrapper == null) throw new ArgumentNullException(nameof(wrapper));
        ModuleSpec spec = GetSpec(moduleId);
        Transform renderRoot = wrapper.transform.Find(moduleId + "_FBX_RenderAuthority");
        if (renderRoot == null)
            throw new InvalidOperationException(moduleId + " is missing its FBX render authority.");
        MeshFilter[] filters = renderRoot.GetComponentsInChildren<MeshFilter>(true);
        if (filters.Length != 3 || filters.Any(filter => filter.sharedMesh == null ||
                filter.transform.parent != renderRoot || filter.GetComponent<MeshRenderer>() == null))
            throw new InvalidOperationException(moduleId + " must retain its three direct FBX LOD renderers.");

        LODGroup group = wrapper.GetComponent<LODGroup>();
        if (group == null || group.GetLODs().Length != 3)
            throw new InvalidOperationException(moduleId + " must retain its three authored LOD levels.");
        Mesh[] meshes = filters.Select(filter => filter.sharedMesh).ToArray();
        Material[][] materials = filters.Select(filter =>
            filter.GetComponent<MeshRenderer>().sharedMaterials).ToArray();
        LOD[] lods = group.GetLODs();

        bool changed = SetTransform(renderRoot, Vector3.zero, Quaternion.identity, FbxXReflection);
        bool siloRing = string.Equals(moduleId, "GE_SILO_RING", StringComparison.Ordinal);
        if (siloRing)
        {
            // The collapsed ring LODs lose the circular corrugation profile at
            // ordinary viewing distances. Retain the clean source LOD0 longer
            // for this module only; ownership, fades, and source meshes stay intact.
            LOD[] adjusted = (LOD[])lods.Clone();
            bool lodChanged = false;
            for (int index = 0; index < adjusted.Length; index++)
            {
                if (adjusted[index].screenRelativeTransitionHeight == SiloRingLodHeights[index]) continue;
                adjusted[index].screenRelativeTransitionHeight = SiloRingLodHeights[index];
                lodChanged = true;
            }
            if (lodChanged)
            {
                group.SetLODs(adjusted);
                changed = true;
            }
        }
        Bounds expected = spec.Bounds;
        Matrix4x4 sourceBasis = Matrix4x4.Scale(FbxXReflection) * Matrix4x4.Rotate(FbxYUpRotation);
        foreach (MeshFilter filter in filters)
        {
            string meshPath = AssetDatabase.GetAssetPath(filter.sharedMesh);
            string expectedRoot = spec.Traversal ? TraversalRoot : InvestigationRoot;
            if (!meshPath.StartsWith(expectedRoot + "/Source/Modules/" + moduleId + "/",
                    StringComparison.Ordinal) || !meshPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(moduleId + " refers to an unexpected source mesh: " + meshPath);
            var importer = AssetImporter.GetAtPath(meshPath) as ModelImporter;
            if (importer == null || importer.bakeAxisConversion)
                throw new InvalidOperationException(moduleId + " no longer has the checked-in FBX coordinate convention.");

            Bounds shape = TransformBounds(filter.sharedMesh.bounds, sourceBasis);
            RequireClose(shape.size, expected.size, moduleId + " source LOD dimensions");
            Vector3 offset = Vector3.Scale(expected.center - shape.center, FbxXReflection);
            changed |= SetTransform(filter.transform, offset, FbxYUpRotation, Vector3.one);
        }

        if (changed || !Close(group.localReferencePoint, expected.center) ||
            Mathf.Abs(group.size - Mathf.Max(expected.size.x, expected.size.y, expected.size.z)) > BoundsTolerance)
        {
            group.RecalculateBounds();
            changed = true;
        }

        for (int index = 0; index < filters.Length; index++)
        {
            if (filters[index].sharedMesh != meshes[index] ||
                !filters[index].GetComponent<MeshRenderer>().sharedMaterials.SequenceEqual(materials[index]))
                throw new InvalidOperationException(moduleId + " changed mesh/material authority during pivot repair.");
            RequireClose(LocalBounds(wrapper.transform, filters[index]).min, expected.min, moduleId + " LOD minimum");
            RequireClose(LocalBounds(wrapper.transform, filters[index]).max, expected.max, moduleId + " LOD maximum");
        }
        LOD[] after = group.GetLODs();
        for (int index = 0; index < lods.Length; index++)
        {
            float expectedHeight = siloRing
                ? SiloRingLodHeights[index]
                : lods[index].screenRelativeTransitionHeight;
            if (!after[index].renderers.SequenceEqual(lods[index].renderers) ||
                after[index].fadeTransitionWidth != lods[index].fadeTransitionWidth ||
                after[index].screenRelativeTransitionHeight != expectedHeight)
                throw new InvalidOperationException(moduleId + " changed LOD ownership, fades, or its required distance profile.");
        }
        return changed;
    }

    /// <summary>
    /// Corrects verified assembly placements after restoring the catalog pivot.
    /// Keeps assembly roots, mission markers, and NavMesh links in place; an open
    /// vault leaf moves as one unit with its existing collider and carving obstacle.
    /// </summary>
    public static bool ConfigureAssemblyModulePlacements(GameObject assembly, bool traversal)
    {
        Transform visuals = assembly.transform.Find(traversal
            ? "FBX_RenderAuthority_ModularAssembly_39Sources"
            : "FBX_RenderAuthority_ModularAssembly");
        if (visuals == null)
            throw new InvalidOperationException(assembly.name + " is missing its catalog module assembly.");
        bool changed = false;
        if (traversal)
        {
            changed |= SetModulePose(visuals, "WAREHOUSE_BROKEN_REVIEW_PANEL__MW_WALL_BROKEN",
                new Vector3(24f, 0f, 56f), 180f, Vector3.one);
            for (int interval = 0; interval < 5; interval++)
            {
                float y = interval * 5f;
                // Physical ramp A runs .5 -> 4.2; B runs 4.7 -> 1.0.
                Vector3 stairScale = new Vector3(1f, 1f, 3.7f / 4.2f);
                changed |= SetModulePose(visuals, "ELEVATOR_STAIR_" + interval + "_A__GE_STAIR",
                    new Vector3(1f, y, .5f), 0f, stairScale);
                changed |= SetModulePose(visuals, "ELEVATOR_STAIR_" + interval + "_B__GE_STAIR",
                    new Vector3(4.6f, y + 2.5f, 4.7f), 180f, stairScale);
                bool forward = interval % 2 == 0;
                float centerX = forward ? 9.8f : 13.4f;
                // Match the real 43..47 sloped profile, not its flat landing tongues.
                Vector3 catwalkScale = new Vector3(2.2f / 1.2f, 1f, 4f / 3.4f);
                changed |= SetModulePose(visuals, "WAREHOUSE_CATWALK_STAIR_" + interval + "__MC_STAIR",
                    new Vector3(centerX + (forward ? -1.1f : 1.1f), interval * 2f,
                        forward ? 43f : 47f), forward ? 0f : 180f, catwalkScale);
            }
        }
        else
        {
            for (int z = 0; z < 20; z += 4)
            {
                changed |= SetModulePose(visuals, "MILL_WEST_" + z,
                    new Vector3(0f, 0f, z + 4f), 90f, Vector3.one);
                if (z != 0)
                    changed |= SetModulePose(visuals, "MILL_EAST_" + z,
                        new Vector3(30f, 0f, z), -90f, Vector3.one);
            }
            foreach (string level in new[] { "GROUND", "UPPER" })
            {
                float y = level == "GROUND" ? 0f : 4f;
                changed |= SetModulePose(visuals, "ANNEX_" + level + "_WEST_2",
                    new Vector3(18f, y, 2f), 90f, Vector3.one);
                // Upper boundary ends at z=6; z=6..8 is the actual upper entry.
                changed |= SetModulePose(visuals, "ANNEX_" + level + "_WEST_8",
                    new Vector3(18f, y, level == "GROUND" ? 8f : 6f), 90f, Vector3.one);
            }
            foreach (int z in new[] { 2, 4 })
                changed |= SetModulePose(visuals, "LAB_EAST_OBS_" + z,
                    new Vector3(40f, 0f, z - 2f), -90f, Vector3.one);
            foreach (int z in new[] { 4, 6 })
            {
                changed |= SetModulePose(visuals, "LAB_WEST_" + z,
                    new Vector3(30f, 0f, z + 2f), 90f, Vector3.one);
                changed |= SetModulePose(visuals, "VAULT_EAST_" + z,
                    new Vector3(44f, 0f, z), -90f, Vector3.one);
            }

            Transform vaultDoor = assembly.transform.Find(
                "MovingParts_Separate_OpenReviewState/QV_DOOR_OpenReview_85deg");
            if (vaultDoor == null)
                throw new InvalidOperationException(assembly.name + " is missing its authored vault door mover.");
            // The corrected hinge-left leaf extends along +X. Park it against
            // the north jamb so its collider/carving clears the full z=4..6 portal.
            // Preserve the mover name, prefab, scale, and all component settings.
            changed |= SetTransform(vaultDoor, new Vector3(40.08f, 1.13f, 6.15f),
                Quaternion.Euler(0f, 180f, 0f), vaultDoor.localScale);
        }
        return changed;
    }

    public static void InspectBatch()
    {
        string[] paths = ModulePaths(TraversalRoot, 39)
            .Concat(ModulePaths(InvestigationRoot, 30)).ToArray();
        foreach (string path in paths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            string id = Path.GetFileNameWithoutExtension(path);
            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                Bounds actual = LocalBounds(prefab.transform, filter);
                Debug.Log("STILLWATER_MODULE_BOUNDS " + id + " " + filter.name +
                          " actual=" + Format(actual.min) + " -> " + Format(actual.max) +
                          " expected=" + Format(GetSpec(id).Bounds.min) + " -> " + Format(GetSpec(id).Bounds.max) +
                          " raw=" + Format(filter.sharedMesh.bounds.min) + " -> " + Format(filter.sharedMesh.bounds.max));
            }
        }
    }

    private static bool ConfigureFamily(string[] modulePaths, string[] moverPaths)
    {
        bool changed = false;
        foreach (string path in modulePaths)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (ConfigureModule(contents, Path.GetFileNameWithoutExtension(path)))
                {
                    if (PrefabUtility.SaveAsPrefabAsset(contents, path) == null)
                        throw new InvalidOperationException("Could not save repaired Stillwater module " + path);
                    changed = true;
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
        foreach (string path in moverPaths)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                string id = Path.GetFileNameWithoutExtension(path);
                id = id.Substring(0, id.Length - "_Mover".Length);
                Bounds expected = GetSpec(id).Bounds;
                BoxCollider collider = contents.GetComponent<BoxCollider>();
                NavMeshObstacle obstacle = contents.GetComponent<NavMeshObstacle>();
                if (collider == null || obstacle == null || obstacle.shape != NavMeshObstacleShape.Box)
                    throw new InvalidOperationException(id + " must retain its authored collider and box obstacle.");
                foreach (MeshFilter filter in contents.GetComponentsInChildren<MeshFilter>(true))
                {
                    Bounds actual = LocalBounds(contents.transform, filter);
                    RequireClose(actual.min, expected.min, id + " mover visual minimum");
                    RequireClose(actual.max, expected.max, id + " mover visual maximum");
                }
                bool moverChanged = !Close(collider.center, expected.center) || !Close(collider.size, expected.size) ||
                    !Close(obstacle.center, expected.center) || !Close(obstacle.size, expected.size);
                if (moverChanged)
                {
                    collider.center = expected.center;
                    collider.size = expected.size;
                    obstacle.center = expected.center;
                    obstacle.size = expected.size;
                    if (PrefabUtility.SaveAsPrefabAsset(contents, path) == null)
                        throw new InvalidOperationException("Could not save repaired Stillwater mover " + path);
                    changed = true;
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
        return changed;
    }

    private static string[] ModulePaths(string root, int count)
    {
        string moduleDirectory = root + "/Prefabs/Modules";
        string[] paths = Directory.GetFiles(Absolute(moduleDirectory), "*.prefab")
            .Select(path => moduleDirectory + "/" + Path.GetFileName(path))
            .OrderBy(path => path, StringComparer.Ordinal).ToArray();
        string[] expected = catalog.Where(entry => entry.Value.Traversal == (root == TraversalRoot) &&
                !PrunedInvestigationModules.Contains(entry.Key))
            .Select(entry => entry.Key).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        RequireRoster(root, paths, expected, count);
        return paths;
    }

    private static string[] MoverPaths(string root, int count)
    {
        string directory = root + "/Prefabs/Movers";
        string[] paths = Directory.GetFiles(Absolute(directory), "*_Mover.prefab")
            .Select(path => directory + "/" + Path.GetFileName(path))
            .OrderBy(path => path, StringComparer.Ordinal).ToArray();
        string[] expected = root == TraversalRoot
            ? new[] { "GE_ROOF_HATCH_Mover", "MW_CAGE_GATE_Mover", "MW_DOCK_LEVELER_Mover", "MW_ROLLUP_Mover" }
            : new[] { "MILL_MAN_DOOR_Mover", "MILL_ROLLUP_DOOR_Mover", "QL_DOOR_Mover", "QL_PASSTHRU_SHUTTER_Mover", "QV_DOOR_Mover" };
        RequireRoster(directory, paths, expected, count);
        return paths;
    }

    private static void RequireRoster(string label, string[] paths, string[] expected, int count)
    {
        string[] actual = paths.Select(Path.GetFileNameWithoutExtension)
            .OrderBy(id => id, StringComparer.Ordinal).ToArray();
        if (expected.Length != count || !actual.SequenceEqual(expected, StringComparer.Ordinal))
            throw new InvalidOperationException(label + " does not match its " + count + " imported assets. Missing: " +
                string.Join(", ", expected.Except(actual, StringComparer.Ordinal)) + "; unexpected: " +
                string.Join(", ", actual.Except(expected, StringComparer.Ordinal)));
    }

    private static bool SetModulePose(Transform parent, string name, Vector3 position, float yaw, Vector3 scale)
    {
        Transform module = parent.Find(name);
        if (module == null)
            throw new InvalidOperationException("Missing authored Stillwater module " + name);
        return SetTransform(module, position, Quaternion.Euler(0f, yaw, 0f), scale);
    }

    private static bool SetTransform(Transform transform, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        bool changed = !Close(transform.localPosition, position) ||
            Quaternion.Angle(transform.localRotation, rotation) > .001f || !Close(transform.localScale, scale);
        if (changed)
        {
            transform.localPosition = position;
            transform.localRotation = rotation;
            transform.localScale = scale;
        }
        return changed;
    }

    private static bool Close(Vector3 first, Vector3 second) =>
        (first - second).sqrMagnitude <= PositionTolerance * PositionTolerance;

    private static void RequireClose(Vector3 actual, Vector3 expected, string label)
    {
        if ((actual - expected).sqrMagnitude > BoundsTolerance * BoundsTolerance)
            throw new InvalidOperationException(label + " differs from the catalog: " +
                Format(actual) + " instead of " + Format(expected));
    }

    private static Bounds LocalBounds(Transform root, MeshFilter filter) =>
        TransformBounds(filter.sharedMesh.bounds, root.worldToLocalMatrix * filter.transform.localToWorldMatrix);

    private static Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
    {
        Vector3 center = matrix.MultiplyPoint3x4(bounds.center);
        Vector3 x = matrix.MultiplyVector(new Vector3(bounds.extents.x, 0f, 0f));
        Vector3 y = matrix.MultiplyVector(new Vector3(0f, bounds.extents.y, 0f));
        Vector3 z = matrix.MultiplyVector(new Vector3(0f, 0f, bounds.extents.z));
        return new Bounds(center, new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
            Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
            Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z)) * 2f);
    }

    private static string Format(Vector3 value) =>
        value.ToString("F4", CultureInfo.InvariantCulture);

    private static string Absolute(string assetPath) =>
        Path.GetFullPath(Path.Combine(Application.dataPath, "../" + assetPath));

    private static ModuleSpec GetSpec(string id)
    {
        if (!catalog.TryGetValue(id, out ModuleSpec spec))
            throw new InvalidOperationException("No exact Stillwater source catalog contract for " + id);
        return spec;
    }

    private readonly struct ModuleSpec
    {
        public readonly bool Traversal;
        public readonly Bounds Bounds;

        public ModuleSpec(Vector3 size, string pivot, bool traversal)
        {
            Traversal = traversal;
            Vector3 center;
            switch (pivot)
            {
                case "LLF": center = size * .5f; break;
                case "BC": center = new Vector3(0f, size.y * .5f, 0f); break;
                case "C": center = Vector3.zero; break;
                case "HL": center = new Vector3(size.x * .5f, 0f, 0f); break;
                case "top-center":
                case "top-hinge-center": center = new Vector3(0f, -size.y * .5f, 0f); break;
                case "hinge-rear-center": center = new Vector3(0f, 0f, size.z * .5f); break;
                default: throw new InvalidOperationException("Unsupported Stillwater catalog pivot " + pivot);
            }
            Bounds = new Bounds(center, size);
        }
    }
}
#endif
