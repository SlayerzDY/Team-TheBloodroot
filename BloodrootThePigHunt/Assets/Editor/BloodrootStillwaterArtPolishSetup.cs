#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Prefab-local finishing pass for Stillwater's existing modular architecture.
/// The navigation/collision assembly remains the authority for every walking
/// surface. This pass supplies missing load paths, matching visible floor
/// surfaces, and an industrial material/trim vocabulary without new gameplay.
/// </summary>
public static class BloodrootStillwaterArtPolishSetup
{
    private const string AssetRoot = "Assets/Generated/PlayableArchitecture/StillwaterIntegration";
    private const string OwnedRoot = "__STILLWATER_PRODUCTION_ART_V1";
    private const float Tolerance = .0001f;

    private enum Shape { Box, HBeam, Footing, SiloRing }
    private enum Finish { Steel, Galvanized, Concrete, Ochre, Cladding }

    private sealed class Part
    {
        public string Name;
        public Shape Shape;
        public Finish Finish;
        public Vector3 Position;
        public Quaternion Rotation = Quaternion.identity;
        public Vector3 Size;
        public bool Solid;
    }

    private sealed class Assets
    {
        public readonly Dictionary<Shape, Mesh> Meshes = new Dictionary<Shape, Mesh>();
        public readonly Dictionary<Finish, Material> Materials = new Dictionary<Finish, Material>();
    }

    public static bool ConfigureAssembly(GameObject root, bool traversal)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        Transform modules = RequireModules(root, traversal);
        Transform overrides = RequireOverrides(root, traversal);
        bool changed = false;
        Assets assets = GetAssets(true, ref changed);
        List<Part> parts = BuildParts(root, traversal);

        Transform owned = root.transform.Find(OwnedRoot);
        if (owned == null)
        {
            owned = new GameObject(OwnedRoot).transform;
            owned.SetParent(root.transform, false);
            changed = true;
        }
        changed |= SetPose(owned, Vector3.zero, Quaternion.identity, Vector3.one);
        changed |= SetActive(owned.gameObject, true);
        if (owned.gameObject.layer != 0) { owned.gameObject.layer = 0; changed = true; }

        // These are structural members, not additional traversable decks. Their
        // exact colliders obstruct navigation; their top faces grant no route.
        NavMeshModifier modifier = owned.GetComponent<NavMeshModifier>();
        if (modifier == null) { modifier = owned.gameObject.AddComponent<NavMeshModifier>(); changed = true; }
        if (!modifier.enabled || modifier.ignoreFromBuild || !modifier.overrideArea || modifier.area != 1)
        {
            modifier.enabled = true;
            modifier.ignoreFromBuild = false;
            modifier.overrideArea = true;
            modifier.area = 1;
            changed = true;
        }

        var expected = new HashSet<string>(parts.Select(part => part.Name), StringComparer.Ordinal);
        if (expected.Count != parts.Count)
            throw new InvalidOperationException("Stillwater art specifications contain duplicate names.");
        foreach (Transform child in owned.Cast<Transform>().ToArray())
        {
            if (expected.Contains(child.name)) continue;
            // Only the explicitly owned finishing hierarchy is reconciled.
            UnityEngine.Object.DestroyImmediate(child.gameObject);
            changed = true;
        }
        foreach (Part part in parts) changed |= ConfigurePart(owned, part, assets);

        foreach (Transform module in modules)
            if (SupersededModule(module.name, traversal))
                changed |= SetActive(module.gameObject, false);

        foreach (Renderer renderer in overrides.GetComponentsInChildren<Renderer>(true))
        {
            if (SupersededOverride(renderer.name, traversal))
            {
                if (renderer.enabled) { renderer.enabled = false; changed = true; }
                continue;
            }
            Material finish = assets.Materials[OverrideFinish(renderer.name)];
            Material[] materials = renderer.sharedMaterials;
            if (materials.Length != 1 || materials[0] != finish)
            {
                renderer.sharedMaterials = new[] { finish };
                changed = true;
            }
        }

        ValidateAssembly(root, traversal);
        return changed;
    }

    public static void ValidateAssembly(GameObject root, bool traversal)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        bool ignored = false;
        Assets assets = GetAssets(false, ref ignored);
        Transform owned = root.transform.Find(OwnedRoot);
        if (owned == null || !owned.gameObject.activeSelf || owned.gameObject.layer != 0)
            throw new InvalidOperationException(root.name + " is missing its enabled Stillwater finishing hierarchy.");
        RequirePose(owned, Vector3.zero, Quaternion.identity, Vector3.one);
        NavMeshModifier modifier = owned.GetComponent<NavMeshModifier>();
        if (modifier == null || !modifier.enabled || modifier.ignoreFromBuild || !modifier.overrideArea || modifier.area != 1)
            throw new InvalidOperationException("Stillwater structural members must retain Not Walkable navigation classification.");

        List<Part> parts = BuildParts(root, traversal);
        if (owned.childCount != parts.Count)
            throw new InvalidOperationException("Stillwater structural/finish part count has drifted.");
        foreach (Part part in parts)
        {
            Transform item = owned.Find(part.Name);
            if (item == null || !item.gameObject.activeSelf || item.gameObject.layer != 0 || item.childCount != 0)
                throw new InvalidOperationException("Missing or altered Stillwater finish: " + part.Name);
            RequirePose(item, part.Position, part.Rotation, part.Size);
            MeshFilter filter = item.GetComponent<MeshFilter>();
            MeshRenderer renderer = item.GetComponent<MeshRenderer>();
            if (filter == null || filter.sharedMesh != assets.Meshes[part.Shape] || renderer == null || !renderer.enabled ||
                renderer.sharedMaterials.Length != 1 || renderer.sharedMaterial != assets.Materials[part.Finish])
                throw new InvalidOperationException("Stillwater finish mesh/material drifted: " + part.Name);
            ValidatePartCollision(item, part, assets);
        }

        foreach (Transform module in RequireModules(root, traversal))
            if (SupersededModule(module.name, traversal) && module.gameObject.activeSelf)
                throw new InvalidOperationException("An obsolete unsupported module is visible: " + module.name);
        foreach (Renderer renderer in RequireOverrides(root, traversal).GetComponentsInChildren<Renderer>(true))
        {
            if (SupersededOverride(renderer.name, traversal))
            {
                if (renderer.enabled) throw new InvalidOperationException("An obsolete review volume is visible: " + renderer.name);
            }
            else if (renderer.sharedMaterials.Length != 1 ||
                     renderer.sharedMaterial != assets.Materials[OverrideFinish(renderer.name)])
                throw new InvalidOperationException("Review-colored material remains in Stillwater: " + renderer.name);
        }

        if (traversal)
        {
            Renderer[] obsoleteRoofFrame = RequireOverrides(root, true)
                .GetComponentsInChildren<Renderer>(true).Where(renderer =>
                    renderer.name.StartsWith("OVERRIDE_WarehouseStairRoofOpening_", StringComparison.Ordinal)).ToArray();
            if (obsoleteRoofFrame.Length != 3 || obsoleteRoofFrame.Any(renderer => renderer.enabled))
                throw new InvalidOperationException("The three obsolete warehouse roof-opening review members must remain suppressed.");
            foreach (Vector2 point in CatwalkSupports())
            {
                ValidateLoadPath(owned, CatwalkId(point), .28f, 9.50f, 9.80f);
                ValidateBearingReceiver(root, owned.Find(CatwalkId(point) + "_Bearing"),
                    "OVERRIDE_COLLIDER_CatwalkSpine_1P50W_", .12f);
            }
            Vector2[] silos = SiloCenters();
            for (int silo = 0; silo < silos.Length; silo++)
            {
                for (int corner = 0; corner < 4; corner++)
                    ValidateLoadPath(owned, "Silo" + silo + "_Leg" + corner, .28f, 7.72f, 8f);
                Transform ring = owned.Find("Silo" + silo + "_LoadRing");
                RequireClose(ring.localPosition.y + ring.localScale.y * .5f, 8.02f, "silo ring-to-shell contact");
                RequireClose(ring.localPosition.y - ring.localScale.y * .5f, 7.72f, "silo ring-to-column contact");
                ValidateSiloShellContact(root, silo, ring);
            }
            ValidateFloorBackings(root, owned);
            ValidateFrontLandingClearance(owned);
            ValidateElevatorBandClearance(root, owned);
        }
        else
        {
            for (int index = 0; index < 3; index++)
                ValidateLoadPath(owned, "Mezzanine" + index, .28f, 3.50f, 3.78f);
            ValidateInvestigationVaultOpening(root);
        }
    }

    private static List<Part> BuildParts(GameObject root, bool traversal)
    {
        var parts = new List<Part>();
        if (traversal) BuildTraversal(parts, root);
        else BuildInvestigation(parts);
        return parts;
    }

    private static void BuildTraversal(List<Part> parts, GameObject root)
    {
        foreach (Vector2 point in CatwalkSupports())
        {
            string id = CatwalkId(point);
            AddSupport(parts, id, point, 9.50f, 9.80f, 1.50f, .26f);
            Beam(parts, id + "_KneeWest", new Vector3(point.x, 8.62f, point.y),
                new Vector3(point.x - .61f, 9.61f, point.y), .10f, .10f, Finish.Steel);
            Beam(parts, id + "_KneeEast", new Vector3(point.x, 8.62f, point.y),
                new Vector3(point.x + .61f, 9.61f, point.y), .10f, .10f, Finish.Steel);
        }

        Vector2[] silos = SiloCenters();
        Vector2[] corners = { new Vector2(-2.7f, -2.7f), new Vector2(2.7f, -2.7f),
            new Vector2(2.7f, 2.7f), new Vector2(-2.7f, 2.7f) };
        for (int silo = 0; silo < silos.Length; silo++)
        {
            Vector2 center = silos[silo];
            for (int corner = 0; corner < corners.Length; corner++)
            {
                Vector2 position = center + corners[corner];
                string id = "Silo" + silo + "_Leg" + corner;
                AddSupport(parts, id, position, 7.72f, 8f, .74f, .40f);
                Vector2 next = center + corners[(corner + 1) % corners.Length];
                Beam(parts, "Silo" + silo + "_Tie" + corner,
                    new Vector3(position.x, 4.70f, position.y), new Vector3(next.x, 4.70f, next.y),
                    .18f, .20f, Finish.Steel);
                Beam(parts, "Silo" + silo + "_BraceA" + corner,
                    new Vector3(position.x, .83f, position.y), new Vector3(next.x, 4.70f, next.y),
                    .11f, .11f, Finish.Steel);
                Beam(parts, "Silo" + silo + "_BraceB" + corner,
                    new Vector3(position.x, 4.70f, position.y), new Vector3(next.x, .83f, next.y),
                    .11f, .11f, Finish.Steel);
            }
            parts.Add(new Part { Name = "Silo" + silo + "_LoadRing", Shape = Shape.SiloRing,
                Finish = Finish.Steel, Position = new Vector3(center.x, 7.87f, center.y),
                Size = new Vector3(8.24f, .30f, 8.24f), Solid = true });
        }

        // Real, continuous floor skins exactly follow the pre-existing East,
        // Rear and split-landing collision pieces. No slab closes the stairwell.
        foreach (BoxCollider collider in FloorColliders(root))
        {
            Matrix4x4 local = root.transform.worldToLocalMatrix * collider.transform.localToWorldMatrix;
            Vector3 center = local.MultiplyPoint3x4(collider.center);
            Vector3 size = Abs(local.MultiplyVector(collider.size));
            Box(parts, "FloorSkin_" + collider.name, center, size, Finish.Concrete, false);
        }

        // Exterior steel framing makes the open sides read as deliberate grain
        // elevator structure while preserving every entry and stair aperture.
        // The original narrow front doors/windows crossed both real landing
        // turns. Keep the entire x=.5..6.5 route open, with 2.40 m headroom;
        // solid infill occupies only the remaining side bays and overhead band.
        for (int tier = 0; tier < 6; tier++)
        {
            float level = tier * 5f;
            Box(parts, "ElevatorFrontWestInfill_" + tier,
                new Vector3(.25f, level + 2.35f, .15f), new Vector3(.50f, 4.70f, .30f), Finish.Cladding, true);
            Box(parts, "ElevatorFrontEastInfill_" + tier,
                new Vector3(8.25f, level + 2.35f, .15f), new Vector3(3.50f, 4.70f, .30f), Finish.Cladding, true);
            Box(parts, "ElevatorFrontHighHeader_" + tier,
                new Vector3(3.50f, level + 3.55f, .15f), new Vector3(6f, 2.30f, .30f), Finish.Cladding, true);
        }
        Vector2[] towerCorners = { new Vector2(-.16f, -.14f), new Vector2(10.16f, -.14f),
            new Vector2(10.16f, 12.14f), new Vector2(-.16f, 12.14f) };
        for (int corner = 0; corner < towerCorners.Length; corner++)
        {
            Vector2 p = towerCorners[corner];
            AddSupport(parts, "ElevatorCorner" + corner, p, 29.72f, 30f, .56f, .32f);
        }
        for (int tier = 1; tier <= 6; tier++)
        {
            // Edge girders meet the .20 m slab underside, not its walking
            // plane. Their Not Walkable classification must not stamp a
            // coplanar stripe across the A/B turn or the catwalk entry.
            float y = ElevatorBandCenterY(tier);
            Box(parts, "ElevatorRearBand_" + tier, new Vector3(5f, y, 12.14f),
                new Vector3(10.32f, .30f, .30f), Finish.Steel, true);
            Box(parts, "ElevatorWestBand_" + tier, new Vector3(-.16f, y, 6f),
                new Vector3(.30f, .30f, 12.28f), Finish.Steel, true);
            Box(parts, "ElevatorEastBand_" + tier, new Vector3(10.16f, y, 6f),
                new Vector3(.30f, .30f, 12.28f), Finish.Steel, true);
            Box(parts, "ElevatorFrontBand_" + tier, new Vector3(5f, y, -.14f),
                new Vector3(10.32f, .30f, .30f), Finish.Ochre, true);
            if (tier % 2 == 0)
            {
                float lowerBandY = ElevatorBandCenterY(tier - 1);
                Beam(parts, "ElevatorRearBraceA_" + tier, new Vector3(5f, lowerBandY, 12.20f),
                    new Vector3(10.16f, y, 12.20f), .15f, .15f, Finish.Steel);
                Beam(parts, "ElevatorRearBraceB_" + tier, new Vector3(5f, y, 12.20f),
                    new Vector3(10.16f, lowerBandY, 12.20f), .15f, .15f, Finish.Steel);
            }
        }

        // Roof edges are capped; the broad center roof opening and the rising
        // switchback remain unobstructed. Fascia sits in existing roof depth.
        Box(parts, "WarehouseWestFascia", new Vector3(.03f, 6.61f, 48f), new Vector3(.12f, .28f, 16.10f), Finish.Ochre, false);
        Box(parts, "WarehouseEastFascia", new Vector3(23.97f, 6.61f, 48f), new Vector3(.12f, .28f, 16.10f), Finish.Ochre, false);
        foreach (float x in new[] { 4f, 20f })
        {
            Box(parts, "WarehouseFrontFascia_" + x, new Vector3(x, 6.61f, 40.02f), new Vector3(8f, .28f, .12f), Finish.Ochre, false);
            Box(parts, "WarehouseRearFascia_" + x, new Vector3(x, 6.61f, 55.98f), new Vector3(8f, .28f, .12f), Finish.Ochre, false);
            Box(parts, "WarehouseRoofGirder_" + x, new Vector3(x, 6.32f, 48f), new Vector3(.34f, .36f, 16f), Finish.Steel, false);
        }
        foreach (float x in new[] { 7.98f, 16.02f })
            Box(parts, "WarehouseOpeningCoping_" + x, new Vector3(x, 6.61f, 48f), new Vector3(.12f, .28f, 16f), Finish.Steel, false);

        // Replace filled review rail walls with posts, toe boards and two
        // rails. The original solid safety boundary remains collision authority.
        foreach (Transform rail in RequireOverrides(root, true).Cast<Transform>()
                     .Where(item => item.name.StartsWith("OVERRIDE_CatwalkRail_", StringComparison.Ordinal)))
        {
            Vector3 center = rail.localPosition;
            Vector3 size = rail.localScale;
            AddRail(parts, "Rail_" + rail.name, center, size, false);
        }
        Box(parts, "SiloNorthGirderA", new Vector3(21.5f, 21.68f, 1.37f), new Vector3(25f, .24f, .16f), Finish.Steel, true);
        Box(parts, "SiloNorthGirderB", new Vector3(21.5f, 21.68f, 2.63f), new Vector3(25f, .24f, .16f), Finish.Steel, true);
        Box(parts, "SiloEastGirderA", new Vector3(33.37f, 21.68f, 12f), new Vector3(.16f, .24f, 20f), Finish.Steel, true);
        Box(parts, "SiloEastGirderB", new Vector3(34.63f, 21.68f, 12f), new Vector3(.16f, .24f, 20f), Finish.Steel, true);
        foreach (float x in new[] { 18f, 30f })
            Beam(parts, "SiloNorthBracket_" + x, new Vector3(x, 20.4f, 2.65f),
                new Vector3(x, 21.68f, 1.37f), .18f, .18f, Finish.Steel);
        foreach (float z in new[] { 6f, 18f })
            Beam(parts, "SiloEastBracket_" + z, new Vector3(33.35f, 20.4f, z),
                new Vector3(34.63f, 21.68f, z), .18f, .18f, Finish.Steel);
        AddRail(parts, "SiloNorthOuterGuard", new Vector3(22.1f, 22.55f, 1.22f),
            new Vector3(23.70f, 1.10f, .08f), true);
        AddRail(parts, "SiloEastOuterGuard", new Vector3(34.78f, 22.55f, 12f),
            new Vector3(.08f, 1.10f, 19.90f), true);
        // The elbow is a supported corner return, not a square floor extension
        // beyond the existing L-shaped deck. It spans between seated end posts.
        Beam(parts, "SiloGuardCornerHandrail", new Vector3(33.95f, 23.065f, 1.22f),
            new Vector3(34.78f, 23.065f, 2.05f), .065f, .065f, Finish.Ochre);
        Beam(parts, "SiloGuardCornerMidrail", new Vector3(33.95f, 22.56f, 1.22f),
            new Vector3(34.78f, 22.56f, 2.05f), .065f, .065f, Finish.Galvanized);
    }

    private static void BuildInvestigation(List<Part> parts)
    {
        for (int index = 0; index < 3; index++)
            AddSupport(parts, "Mezzanine" + index, new Vector2(4.2f + index * 4f, 9f),
                3.50f, 3.78f, 1.80f, .24f, true);
        // These stop before the mill stair and leave the annex ramp bay clear.
        Box(parts, "MezzanineFrontGirder", new Vector3(8.60f, 3.64f, 8.12f), new Vector3(9.20f, .28f, .20f), Finish.Steel, true);
        Box(parts, "MezzanineRearGirder", new Vector3(8.60f, 3.64f, 9.88f), new Vector3(9.20f, .28f, .20f), Finish.Steel, true);
        Box(parts, "MillFrontRoofFascia", new Vector3(15f, 7.88f, .025f), new Vector3(30.10f, .27f, .12f), Finish.Ochre, false);
        Box(parts, "MillRearRoofFascia", new Vector3(15f, 7.88f, 19.975f), new Vector3(30.10f, .27f, .12f), Finish.Ochre, false);
        Box(parts, "MillWestRoofFascia", new Vector3(.025f, 7.88f, 10f), new Vector3(.12f, .27f, 20f), Finish.Ochre, false);
        Box(parts, "MillEastRoofFascia", new Vector3(29.975f, 7.88f, 10f), new Vector3(.12f, .27f, 20f), Finish.Ochre, false);
        // A narrow closure bridges the authored wall top (3.20) to the lab
        // ceiling underside (3.25); every existing doorway/window stays open.
        Box(parts, "LabRoofFrontClosure", new Vector3(35f, 3.225f, .05f), new Vector3(10f, .08f, .20f), Finish.Steel, false);
        Box(parts, "LabRoofRearClosure", new Vector3(35f, 3.225f, 7.95f), new Vector3(10f, .08f, .20f), Finish.Steel, false);
        Box(parts, "LabRoofWestClosure", new Vector3(30.05f, 3.225f, 4f), new Vector3(.20f, .08f, 8f), Finish.Steel, false);
        Box(parts, "LabRoofEastClosure", new Vector3(39.95f, 3.225f, 4f), new Vector3(.20f, .08f, 8f), Finish.Steel, false);
        Box(parts, "LabFrontFascia", new Vector3(35f, 3.34f, .02f), new Vector3(10f, .18f, .12f), Finish.Ochre, false);
        Box(parts, "LabRearFascia", new Vector3(35f, 3.34f, 7.98f), new Vector3(10f, .18f, .12f), Finish.Ochre, false);
    }

    private static void AddSupport(List<Part> parts, string id, Vector2 ground, float columnTop,
        float bearingTop, float capWidth, float columnWidth, bool capAlongZ = false)
    {
        parts.Add(new Part { Name = id + "_Footing", Shape = Shape.Footing, Finish = Finish.Concrete,
            Position = new Vector3(ground.x, .11f, ground.y), Size = new Vector3(.86f, .34f, .86f), Solid = true });
        Box(parts, id + "_BasePlate", new Vector3(ground.x, .30f, ground.y),
            new Vector3(.55f, .04f, .55f), Finish.Steel, true);
        parts.Add(new Part { Name = id + "_Column", Shape = Shape.HBeam, Finish = Finish.Steel,
            Position = new Vector3(ground.x, (.28f + columnTop) * .5f, ground.y),
            Size = new Vector3(columnWidth, columnTop - .28f, columnWidth), Solid = true });
        Box(parts, id + "_Bearing", new Vector3(ground.x, (columnTop + bearingTop) * .5f, ground.y),
            capAlongZ ? new Vector3(.40f, bearingTop - columnTop, capWidth) :
                new Vector3(capWidth, bearingTop - columnTop, .40f), Finish.Steel, true);
        // Four shallow anchor heads are joined to the plate, not loose props.
        for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
                Box(parts, id + "_Anchor_" + x + "_" + z,
                    new Vector3(ground.x + x * .215f, .328f, ground.y + z * .215f),
                    new Vector3(.055f, .022f, .055f), Finish.Galvanized, false);
    }

    private static void AddRail(List<Part> parts, string id, Vector3 center, Vector3 size, bool solid)
    {
        bool alongX = size.x > size.z;
        float length = alongX ? size.x : size.z;
        float bottom = center.y - size.y * .5f;
        Vector3 axis = alongX ? Vector3.right : Vector3.forward;
        Vector3 thin = alongX ? new Vector3(length, .065f, .065f) : new Vector3(.065f, .065f, length);
        Box(parts, id + "_Handrail", new Vector3(center.x, bottom + 1.065f, center.z), thin, Finish.Ochre, solid);
        Box(parts, id + "_Midrail", new Vector3(center.x, bottom + .56f, center.z), thin, Finish.Galvanized, solid);
        Box(parts, id + "_ToeBoard", new Vector3(center.x, bottom + .075f, center.z),
            alongX ? new Vector3(length, .15f, .045f) : new Vector3(.045f, .15f, length), Finish.Steel, solid);
        int spans = Mathf.CeilToInt(length / 2f);
        for (int post = 0; post <= spans; post++)
        {
            Vector3 p = center + axis * (-length * .5f + length * post / spans);
            Box(parts, id + "_Post" + post, new Vector3(p.x, bottom + .55f, p.z),
                new Vector3(.075f, 1.10f, .075f), Finish.Galvanized, solid);
            Box(parts, id + "_Shoe" + post, new Vector3(p.x, bottom + .02f, p.z),
                new Vector3(.18f, .04f, .18f), Finish.Steel, solid);
        }
    }

    private static void Box(List<Part> parts, string name, Vector3 center, Vector3 size, Finish finish, bool solid)
    {
        parts.Add(new Part { Name = name, Shape = Shape.Box, Finish = finish,
            Position = center, Size = size, Solid = solid });
    }

    private static void Beam(List<Part> parts, string name, Vector3 from, Vector3 to,
        float width, float depth, Finish finish)
    {
        parts.Add(new Part { Name = name, Shape = Shape.HBeam, Finish = finish,
            Position = (from + to) * .5f, Rotation = Quaternion.FromToRotation(Vector3.up, to - from),
            Size = new Vector3(width, Vector3.Distance(from, to), depth), Solid = true });
    }

    private static bool ConfigurePart(Transform parent, Part part, Assets assets)
    {
        bool changed = false;
        Transform item = parent.Find(part.Name);
        if (item == null)
        {
            item = new GameObject(part.Name).transform;
            item.SetParent(parent, false);
            changed = true;
        }
        changed |= SetPose(item, part.Position, part.Rotation, part.Size);
        changed |= SetActive(item.gameObject, true);
        if (item.gameObject.layer != 0) { item.gameObject.layer = 0; changed = true; }
        StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(item.gameObject);
        if ((flags & StaticEditorFlags.BatchingStatic) == 0)
        {
            GameObjectUtility.SetStaticEditorFlags(item.gameObject, flags | StaticEditorFlags.BatchingStatic);
            changed = true;
        }
        MeshFilter filter = item.GetComponent<MeshFilter>();
        if (filter == null) { filter = item.gameObject.AddComponent<MeshFilter>(); changed = true; }
        if (filter.sharedMesh != assets.Meshes[part.Shape]) { filter.sharedMesh = assets.Meshes[part.Shape]; changed = true; }
        MeshRenderer renderer = item.GetComponent<MeshRenderer>();
        if (renderer == null) { renderer = item.gameObject.AddComponent<MeshRenderer>(); changed = true; }
        if (!renderer.enabled) { renderer.enabled = true; changed = true; }
        if (renderer.sharedMaterials.Length != 1 || renderer.sharedMaterial != assets.Materials[part.Finish])
        { renderer.sharedMaterials = new[] { assets.Materials[part.Finish] }; changed = true; }
        if (renderer.shadowCastingMode != ShadowCastingMode.On) { renderer.shadowCastingMode = ShadowCastingMode.On; changed = true; }
        changed |= ConfigurePartCollision(item, part, assets);
        return changed;
    }

    private static bool ConfigurePartCollision(Transform item, Part part, Assets assets)
    {
        Collider[] existing = item.GetComponents<Collider>();
        bool mesh = part.Shape == Shape.Footing || part.Shape == Shape.SiloRing;
        int expectedCount = !part.Solid ? 0 : part.Shape == Shape.HBeam ? 3 : 1;
        bool changed = false;
        if (existing.Length != expectedCount || existing.Any(collider => mesh ? !(collider is MeshCollider) : !(collider is BoxCollider)))
        {
            foreach (Collider collider in existing) UnityEngine.Object.DestroyImmediate(collider);
            for (int index = 0; index < expectedCount; index++)
            {
                if (mesh) item.gameObject.AddComponent<MeshCollider>();
                else item.gameObject.AddComponent<BoxCollider>();
            }
            existing = item.GetComponents<Collider>();
            changed = true;
        }
        for (int index = 0; index < existing.Length; index++)
        {
            Collider collider = existing[index];
            if (!collider.enabled || collider.isTrigger) { collider.enabled = true; collider.isTrigger = false; changed = true; }
            if (collider is MeshCollider meshCollider)
            {
                if (meshCollider.sharedMesh != assets.Meshes[part.Shape]) { meshCollider.sharedMesh = assets.Meshes[part.Shape]; changed = true; }
                if (meshCollider.convex) { meshCollider.convex = false; changed = true; }
            }
            else if (collider is BoxCollider box)
            {
                BeamBox(part.Shape, index, out Vector3 center, out Vector3 size);
                if (box.center != center || box.size != size) { box.center = center; box.size = size; changed = true; }
            }
        }
        return changed;
    }

    private static void ValidatePartCollision(Transform item, Part part, Assets assets)
    {
        Collider[] colliders = item.GetComponents<Collider>();
        int expected = !part.Solid ? 0 : part.Shape == Shape.HBeam ? 3 : 1;
        if (colliders.Length != expected) throw new InvalidOperationException("Finish collision count drifted: " + part.Name);
        for (int index = 0; index < colliders.Length; index++)
        {
            Collider collider = colliders[index];
            if (!collider.enabled || collider.isTrigger || collider.gameObject.layer != 0)
                throw new InvalidOperationException("Finish must retain solid Default-layer collision: " + part.Name);
            if (part.Shape == Shape.Footing || part.Shape == Shape.SiloRing)
            {
                var mesh = collider as MeshCollider;
                if (mesh == null || mesh.convex || mesh.sharedMesh != assets.Meshes[part.Shape])
                    throw new InvalidOperationException("Finish mesh collision drifted: " + part.Name);
            }
            else
            {
                BeamBox(part.Shape, index, out Vector3 center, out Vector3 size);
                var box = collider as BoxCollider;
                if (box == null || !Close(box.center, center) || !Close(box.size, size))
                    throw new InvalidOperationException("Finish box collision does not match visible shape: " + part.Name);
            }
        }
    }

    private static void BeamBox(Shape shape, int index, out Vector3 center, out Vector3 size)
    {
        center = Vector3.zero;
        size = Vector3.one;
        if (shape != Shape.HBeam) return;
        if (index < 2)
        {
            center = new Vector3(0f, 0f, index == 0 ? -.41f : .41f);
            size = new Vector3(1f, 1f, .18f);
        }
        else size = new Vector3(.18f, 1f, .64f);
    }

    private static Assets GetAssets(bool create, ref bool changed)
    {
        if (create) { EnsureFolder(AssetRoot + "/Materials"); EnsureFolder(AssetRoot + "/Meshes"); }
        var result = new Assets();
        foreach (Shape shape in Enum.GetValues(typeof(Shape)))
        {
            string path = AssetRoot + "/Meshes/Stillwater_" + shape + "_V1.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null && create)
            {
                mesh = CreateMesh(shape);
                mesh.name = "Stillwater_" + shape + "_V1";
                AssetDatabase.CreateAsset(mesh, path);
                changed = true;
            }
            if (mesh == null) throw new InvalidOperationException("Stillwater finishing mesh is missing: " + path);
            result.Meshes.Add(shape, mesh);
        }
        foreach (Finish finish in Enum.GetValues(typeof(Finish)))
        {
            string path = AssetRoot + "/Materials/Stillwater_" + finish + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null && create)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("URP Lit is required for Stillwater materials.");
                material = new Material(shader) { name = "Stillwater_" + finish };
                Color color = FinishColor(finish);
                material.SetColor("_BaseColor", color);
                if (material.HasProperty("_Color")) material.SetColor("_Color", color);
                material.SetFloat("_Metallic", finish == Finish.Concrete ? 0f : finish == Finish.Ochre ? .32f : .62f);
                material.SetFloat("_Smoothness", finish == Finish.Galvanized ? .31f : .17f);
                material.enableInstancing = true;
                AssetDatabase.CreateAsset(material, path);
                changed = true;
            }
            if (material == null) throw new InvalidOperationException("Stillwater finish material is missing: " + path);
            result.Materials.Add(finish, material);
        }
        return result;
    }

    private static Color FinishColor(Finish finish)
    {
        switch (finish)
        {
            case Finish.Steel: return new Color(.19f, .205f, .20f, 1f);
            case Finish.Galvanized: return new Color(.43f, .47f, .46f, 1f);
            case Finish.Concrete: return new Color(.39f, .375f, .33f, 1f);
            case Finish.Ochre: return new Color(.68f, .44f, .105f, 1f);
            default: return new Color(.35f, .39f, .37f, 1f);
        }
    }

    private static Mesh CreateMesh(Shape shape)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        if (shape == Shape.Box) AppendBox(vertices, triangles, Vector3.zero, Vector3.one);
        else if (shape == Shape.HBeam)
        {
            for (int index = 0; index < 3; index++)
            {
                BeamBox(shape, index, out Vector3 center, out Vector3 size);
                AppendBox(vertices, triangles, center, size);
            }
        }
        else if (shape == Shape.Footing)
        {
            Vector3[] lower = Square(.5f, -.5f);
            Vector3[] shoulder = Square(.5f, .25f);
            Vector3[] top = Square(.42f, .5f);
            Quad(vertices, triangles, lower[3], lower[2], lower[1], lower[0]);
            Quad(vertices, triangles, top[0], top[1], top[2], top[3]);
            for (int index = 0; index < 4; index++)
            {
                int next = (index + 1) % 4;
                Quad(vertices, triangles, lower[index], lower[next], shoulder[next], shoulder[index]);
                Quad(vertices, triangles, shoulder[index], shoulder[next], top[next], top[index]);
            }
        }
        else
        {
            const int segments = 32;
            const float inner = 3.55f / 8.24f;
            for (int index = 0; index < segments; index++)
            {
                float a = index * Mathf.PI * 2f / segments;
                float b = (index + 1) * Mathf.PI * 2f / segments;
                Vector3 ao = Circle(a, .5f, -.5f), bo = Circle(b, .5f, -.5f);
                Vector3 ai = Circle(a, inner, -.5f), bi = Circle(b, inner, -.5f);
                Quad(vertices, triangles, ao, bo, bo + Vector3.up, ao + Vector3.up);
                Quad(vertices, triangles, bi, ai, ai + Vector3.up, bi + Vector3.up);
                Quad(vertices, triangles, ao + Vector3.up, bo + Vector3.up, bi + Vector3.up, ai + Vector3.up);
                Quad(vertices, triangles, bo, ao, ai, bi);
            }
        }
        var mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AppendBox(List<Vector3> v, List<int> t, Vector3 center, Vector3 size)
    {
        Vector3 lo = center - size * .5f, hi = center + size * .5f;
        Vector3 a = new Vector3(lo.x, lo.y, lo.z), b = new Vector3(hi.x, lo.y, lo.z);
        Vector3 c = new Vector3(hi.x, hi.y, lo.z), d = new Vector3(lo.x, hi.y, lo.z);
        Vector3 e = new Vector3(lo.x, lo.y, hi.z), f = new Vector3(hi.x, lo.y, hi.z);
        Vector3 g = new Vector3(hi.x, hi.y, hi.z), h = new Vector3(lo.x, hi.y, hi.z);
        Quad(v, t, b, a, d, c); Quad(v, t, e, f, g, h);
        Quad(v, t, a, e, h, d); Quad(v, t, f, b, c, g);
        Quad(v, t, d, h, g, c); Quad(v, t, a, b, f, e);
    }

    private static void Quad(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        int start = v.Count;
        v.Add(a); v.Add(b); v.Add(c); v.Add(d);
        t.Add(start); t.Add(start + 1); t.Add(start + 2);
        t.Add(start); t.Add(start + 2); t.Add(start + 3);
    }

    private static Vector3[] Square(float half, float y)
    {
        return new[] { new Vector3(-half, y, -half), new Vector3(-half, y, half),
            new Vector3(half, y, half), new Vector3(half, y, -half) };
    }

    private static Vector3 Circle(float angle, float radius, float y)
    { return new Vector3(Mathf.Sin(angle) * radius, y, Mathf.Cos(angle) * radius); }

    private static Transform RequireModules(GameObject root, bool traversal)
    {
        Transform value = root.transform.Find(traversal ? "FBX_RenderAuthority_ModularAssembly_39Sources" : "FBX_RenderAuthority_ModularAssembly");
        if (value == null) throw new InvalidOperationException(root.name + " lacks its source module assembly.");
        return value;
    }

    private static Transform RequireOverrides(GameObject root, bool traversal)
    {
        Transform value = root.transform.Find(traversal ? "ReviewOnlyRobustTraversalOverrides__CATALOG_REMEDIATION_REQUIRED" :
            "ReviewOnlyRobustRouteOverrides__CATALOG_REMEDIATION_REQUIRED");
        if (value == null) throw new InvalidOperationException(root.name + " lacks its existing robust route geometry.");
        return value;
    }

    private static bool SupersededModule(string name, bool traversal)
    {
        if (!traversal)
            return name == "VAULT_DOORFRAME_CATALOG_1P20x2P20_CONDITIONAL";
        return name.EndsWith("__MC_SUPPORT", StringComparison.Ordinal) ||
               name.EndsWith("__GE_SILO_SUPPORT", StringComparison.Ordinal) ||
               name.StartsWith("CATWALK_SPINE_", StringComparison.Ordinal) ||
               name.StartsWith("CATWALK_RAIL_", StringComparison.Ordinal) ||
               name.StartsWith("WAREHOUSE_CATWALK_LANDING_", StringComparison.Ordinal) ||
               name.StartsWith("ELEVATOR_FLOOR_STAIR_", StringComparison.Ordinal) ||
               name.StartsWith("ELEVATOR_FLOOR_EAST_", StringComparison.Ordinal) ||
               name.StartsWith("ELEVATOR_LANDING_", StringComparison.Ordinal) ||
               name.StartsWith("ELEVATOR_FRONT_W_", StringComparison.Ordinal) ||
               name.StartsWith("ELEVATOR_FRONT_E_", StringComparison.Ordinal) ||
               name.StartsWith("WAREHOUSE_BEAM_10__", StringComparison.Ordinal) ||
               name.StartsWith("WAREHOUSE_COLUMN_12_44__", StringComparison.Ordinal);
    }

    private static bool SupersededOverride(string name, bool traversal)
    {
        return traversal && (name == "OVERRIDE_ElevatorFloorAndWall_2MStrips" ||
                             // This old review frame started over the roof void
                             // and its header crossed the highest stair flight.
                             // The connected roof-edge coping replaces it.
                             name.StartsWith("OVERRIDE_WarehouseStairRoofOpening_", StringComparison.Ordinal) ||
                             name.StartsWith("OVERRIDE_CatwalkRail_", StringComparison.Ordinal));
    }

    private static Finish OverrideFinish(string name)
    {
        if (name.IndexOf("POST", StringComparison.Ordinal) >= 0 || name.IndexOf("HEADER", StringComparison.Ordinal) >= 0)
            return Finish.Ochre;
        if (name.IndexOf("Floor", StringComparison.Ordinal) >= 0 || name.IndexOf("Approach", StringComparison.Ordinal) >= 0)
            return Finish.Concrete;
        if (name.IndexOf("Roof", StringComparison.Ordinal) >= 0 || name.IndexOf("Front", StringComparison.Ordinal) >= 0 ||
            name.IndexOf("Rear", StringComparison.Ordinal) >= 0) return Finish.Cladding;
        return Finish.Galvanized;
    }

    private static IEnumerable<BoxCollider> FloorColliders(GameObject root)
    {
        return root.GetComponentsInChildren<BoxCollider>(true).Where(collider =>
            collider.name.StartsWith("COLLIDER_ElevatorFloor", StringComparison.Ordinal) ||
            collider.name.StartsWith("COLLIDER_ElevatorABottomLanding_", StringComparison.Ordinal) ||
            collider.name.StartsWith("COLLIDER_ElevatorBTopAndFloorLanding_", StringComparison.Ordinal) ||
            collider.name.StartsWith("COLLIDER_ElevatorMidLanding_", StringComparison.Ordinal));
    }

    private static void ValidateFloorBackings(GameObject root, Transform owned)
    {
        BoxCollider[] colliders = FloorColliders(root).ToArray();
        if (colliders.Length != 29)
            throw new InvalidOperationException("Stillwater elevator must retain its 24 floor/landing and five mid-landing authority pieces.");
        foreach (BoxCollider collider in colliders)
        {
            Transform skin = owned.Find("FloorSkin_" + collider.name);
            Matrix4x4 local = root.transform.worldToLocalMatrix * collider.transform.localToWorldMatrix;
            Vector3 center = local.MultiplyPoint3x4(collider.center);
            Vector3 size = Abs(local.MultiplyVector(collider.size));
            if (skin == null || !Close(skin.localPosition, center) || !Close(skin.localScale, size))
                throw new InvalidOperationException("Elevator visible slab does not exactly match collision: " + collider.name);
        }
    }

    private static void ValidateFrontLandingClearance(Transform owned)
    {
        for (int tier = 0; tier < 6; tier++)
        {
            Transform west = owned.Find("ElevatorFrontWestInfill_" + tier);
            Transform east = owned.Find("ElevatorFrontEastInfill_" + tier);
            Transform header = owned.Find("ElevatorFrontHighHeader_" + tier);
            RequireClose(west.localPosition.x + west.localScale.x * .5f, .5f, "front landing west jamb");
            RequireClose(east.localPosition.x - east.localScale.x * .5f, 6.5f, "front landing east jamb");
            RequireClose(header.localPosition.y - header.localScale.y * .5f, tier * 5f + 2.4f,
                "front landing 2.40 m clear headroom");
        }
    }

    private static void ValidateInvestigationVaultOpening(GameObject root)
    {
        Transform obsolete = RequireModules(root, false).Find("VAULT_DOORFRAME_CATALOG_1P20x2P20_CONDITIONAL");
        if (obsolete == null || obsolete.gameObject.activeSelf)
            throw new InvalidOperationException("The misaligned narrow catalog vault frame must remain present but suppressed.");

        Transform overrides = RequireOverrides(root, false);
        const string frame = "OVERRIDE_VaultConnector_2P00x2P40";
        Transform postA = overrides.Find(frame + "_POST_A");
        Transform postB = overrides.Find(frame + "_POST_B");
        Transform header = overrides.Find(frame + "_HEADER");
        foreach (Transform member in new[] { postA, postB, header })
        {
            if (member == null || !member.gameObject.activeSelf ||
                member.GetComponent<Renderer>() == null || !member.GetComponent<Renderer>().enabled)
                throw new InvalidOperationException("The matching robust vault doorway must retain its visible frame.");
            RequireClose(member.localPosition.x, 40f, "vault frame wall alignment");
        }
        RequireClose(postA.localPosition.z + postA.localScale.z * .5f, 4f, "vault opening first jamb");
        RequireClose(postB.localPosition.z - postB.localScale.z * .5f, 6f, "vault opening second jamb");
        float headerBottom = header.localPosition.y - header.localScale.y * .5f;
        RequireClose(headerBottom, 2.4f, "vault header underside");

        BoxCollider threshold = root.GetComponentsInChildren<BoxCollider>(true).Single(collider =>
            collider.name == "OVERRIDE_COLLIDER_Threshold_RecordsToVault_2P00W");
        Matrix4x4 local = root.transform.worldToLocalMatrix * threshold.transform.localToWorldMatrix;
        float floorTop = local.MultiplyPoint3x4(threshold.center).y +
            Abs(local.MultiplyVector(threshold.size)).y * .5f;
        // The existing threshold stands .05 m above the lab slab. Its actual
        // clear headroom is 2.35 m, comfortably above the two-metre controller.
        if (!threshold.enabled || threshold.isTrigger || headerBottom - floorTop < 2.30f)
            throw new InvalidOperationException("The records-to-vault threshold no longer has safe clear headroom.");
    }

    private static float ElevatorBandCenterY(int tier)
    {
        return tier * 5f - (tier == 6 ? .15f : .35f);
    }

    private static void ValidateElevatorBandClearance(GameObject root, Transform owned)
    {
        for (int tier = 1; tier <= 6; tier++)
        {
            float level = tier * 5f;
            float receiverUnderside = 30f;
            if (tier < 6)
            {
                BoxCollider slab = FloorColliders(root).Single(collider =>
                    collider.name == "COLLIDER_ElevatorFloorEast_" + (tier * 5));
                Matrix4x4 local = root.transform.worldToLocalMatrix * slab.transform.localToWorldMatrix;
                receiverUnderside = local.MultiplyPoint3x4(slab.center).y -
                    Abs(local.MultiplyVector(slab.size)).y * .5f;
            }
            foreach (string side in new[] { "Front", "Rear", "West", "East" })
            {
                Transform band = owned.Find("Elevator" + side + "Band_" + tier);
                float top = band.localPosition.y + band.localScale.y * .5f;
                float bottom = band.localPosition.y - band.localScale.y * .5f;
                RequireClose(top, receiverUnderside, "elevator " + side + " band-to-slab underside contact");
                if (tier < 6 && (top > level - .19f || bottom - (level - 5f) < 2.4f))
                    throw new InvalidOperationException("An elevator edge band invades a walking plane/headroom: " + band.name);
            }
            if (tier % 2 == 0)
            {
                foreach (string diagonal in new[] { "A", "B" })
                {
                    Transform brace = owned.Find("ElevatorRearBrace" + diagonal + "_" + tier);
                    Vector3 halfAxis = brace.localRotation * Vector3.up * brace.localScale.y * .5f;
                    float lower = Mathf.Min((brace.localPosition - halfAxis).y, (brace.localPosition + halfAxis).y);
                    float upper = Mathf.Max((brace.localPosition - halfAxis).y, (brace.localPosition + halfAxis).y);
                    RequireClose(lower, ElevatorBandCenterY(tier - 1), "rear brace lower band attachment");
                    RequireClose(upper, ElevatorBandCenterY(tier), "rear brace upper band attachment");
                }
            }
        }
    }

    private static Vector2[] CatwalkSupports()
    {
        return new[] { new Vector2(11.65f, 14f), new Vector2(11.65f, 18f), new Vector2(11.65f, 22f),
            new Vector2(11.65f, 26f), new Vector2(11.65f, 30f), new Vector2(11.65f, 34f), new Vector2(11.65f, 38f),
            new Vector2(6.65f, 39f), new Vector2(6.65f, 42f), new Vector2(6.65f, 46f), new Vector2(6.65f, 48.75f) };
    }

    private static string CatwalkId(Vector2 point)
    { return "Catwalk_" + Mathf.RoundToInt(point.x * 100f) + "_" + Mathf.RoundToInt(point.y * 100f); }

    private static Vector2[] SiloCenters()
    { return new[] { new Vector2(18f, 6f), new Vector2(30f, 6f), new Vector2(18f, 18f), new Vector2(30f, 18f) }; }

    private static void ValidateLoadPath(Transform owned, string id, float baseY, float columnTop, float bearingTop)
    {
        Transform footing = owned.Find(id + "_Footing"), column = owned.Find(id + "_Column"), bearing = owned.Find(id + "_Bearing");
        if (footing == null || column == null || bearing == null)
            throw new InvalidOperationException("Incomplete structural load path: " + id);
        RequireClose(footing.localPosition.y + footing.localScale.y * .5f, baseY, id + " footing top");
        RequireClose(column.localPosition.y - column.localScale.y * .5f, baseY, id + " column foot contact");
        RequireClose(column.localPosition.y + column.localScale.y * .5f, columnTop, id + " column head contact");
        RequireClose(bearing.localPosition.y - bearing.localScale.y * .5f, columnTop, id + " bearing underside");
        RequireClose(bearing.localPosition.y + bearing.localScale.y * .5f, bearingTop, id + " deck/shell underside contact");
        if (footing.localPosition.y - footing.localScale.y * .5f > 0f ||
            Mathf.Abs(column.localPosition.x - bearing.localPosition.x) > Tolerance ||
            Mathf.Abs(column.localPosition.z - bearing.localPosition.z) > Tolerance)
            throw new InvalidOperationException("Stillwater support is not seated/aligned: " + id);
    }

    private static void ValidateBearingReceiver(GameObject root, Transform bearing, string colliderPrefix, float radius)
    {
        Vector3 top = bearing.localPosition + Vector3.up * bearing.localScale.y * .5f;
        foreach (BoxCollider collider in root.GetComponentsInChildren<BoxCollider>(true))
        {
            if (!collider.name.StartsWith(colliderPrefix, StringComparison.Ordinal)) continue;
            Matrix4x4 local = root.transform.worldToLocalMatrix * collider.transform.localToWorldMatrix;
            Vector3 center = local.MultiplyPoint3x4(collider.center);
            Vector3 size = Abs(local.MultiplyVector(collider.size));
            // Test the actual receiver's underside plane and a non-zero bearing
            // area at the column axis, not intersecting aggregate scene bounds.
            if (Mathf.Abs(center.y - size.y * .5f - top.y) <= .001f &&
                top.x - radius >= center.x - size.x * .5f - .001f &&
                top.x + radius <= center.x + size.x * .5f + .001f &&
                top.z - radius >= center.z - size.z * .5f - .001f &&
                top.z + radius <= center.z + size.z * .5f + .001f)
                return;
        }
        throw new InvalidOperationException("No physical deck receives the full column bearing: " + bearing.name);
    }

    private static void ValidateSiloShellContact(GameObject root, int silo, Transform loadRing)
    {
        Transform shell = RequireModules(root, true).Find("SILO_" + silo + "_RING_0__GE_SILO_RING");
        if (shell == null || !shell.gameObject.activeSelf)
            throw new InvalidOperationException("Silo load frame has no receiving shell: " + silo);
        MeshFilter filter = shell.GetComponentInChildren<MeshFilter>(true);
        if (filter == null || filter.sharedMesh == null)
            throw new InvalidOperationException("Silo shell has no mesh for support contact validation: " + silo);
        Bounds source = filter.sharedMesh.bounds;
        Matrix4x4 local = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
        float lowest = float.PositiveInfinity;
        for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                    lowest = Mathf.Min(lowest, local.MultiplyPoint3x4(source.center +
                        Vector3.Scale(source.extents, new Vector3(x, y, z))).y);
        float top = loadRing.localPosition.y + loadRing.localScale.y * .5f;
        if (lowest < top - .05f || lowest > top + .001f)
            throw new InvalidOperationException("Silo load ring is disconnected from its real first shell mesh: " + silo);
        Vector2 center = SiloCenters()[silo];
        for (int leg = 0; leg < 4; leg++)
        {
            Transform column = loadRing.parent.Find("Silo" + silo + "_Leg" + leg + "_Column");
            float radius = Vector2.Distance(new Vector2(column.localPosition.x, column.localPosition.z), center);
            if (radius < 3.55f + .15f || radius > 4.12f - .15f)
                throw new InvalidOperationException("Silo column does not sit under the annular load ring: " + column.name);
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, slash));
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }

    private static bool SetPose(Transform item, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        if (Close(item.localPosition, position) && Quaternion.Angle(item.localRotation, rotation) < .001f && Close(item.localScale, scale)) return false;
        item.localPosition = position; item.localRotation = rotation; item.localScale = scale;
        return true;
    }

    private static bool SetActive(GameObject item, bool active)
    { if (item.activeSelf == active) return false; item.SetActive(active); return true; }

    private static void RequirePose(Transform item, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        if (!Close(item.localPosition, position) || Quaternion.Angle(item.localRotation, rotation) > .001f || !Close(item.localScale, scale))
            throw new InvalidOperationException("Stillwater part alignment drifted: " + item.name);
    }

    private static bool Close(Vector3 a, Vector3 b) { return (a - b).sqrMagnitude <= Tolerance * Tolerance; }
    private static Vector3 Abs(Vector3 value) { return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z)); }
    private static void RequireClose(float actual, float expected, string label)
    {
        if (Mathf.Abs(actual - expected) > Tolerance)
            throw new InvalidOperationException("Stillwater " + label + " is disconnected: " + actual + " instead of " + expected + ".");
    }
}
#endif
