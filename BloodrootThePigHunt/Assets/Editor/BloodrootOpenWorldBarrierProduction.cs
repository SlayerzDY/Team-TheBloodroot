#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Bloodroot.OpenWorld;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

internal static class BloodrootOpenWorldBarrierProduction
{
    private const string OpenWorldScenePath =
        "Assets/Scenes/OpenWorld/Bloodroot_OpenWorld.unity";

    private const string BackupScenePath =
        "Assets/Scenes/OpenWorld/Backups/" +
        "Bloodroot_OpenWorld_PreInvisibleBarriers.unity";

    private const string WorldRootName = "Bloodroot_OpenWorld";
    private const string BoundaryPath = "_TERRAIN/World Boundaries";
    private const string ContainerName =
        "Generated Invisible Progression Barriers";
    private const string OuterBoundsRootName = "OUTER_WORLD_BOUNDS";

    private const string SignaturePrefix =
        "_INVISIBLE_PROGRESSION_BARRIERS_SIGNATURE_";

    private const string PreviousShortCutlineSignature =
        "_INVISIBLE_PROGRESSION_BARRIERS_SIGNATURE_AF53E11078D75DCB";

    private const float PreviousShortTransverseSize = 1420f;

    private const string BarrierLayerName = "Border";
    private const string PlayerLayerName = "Player";
    private const int SegmentCount = 1;

    private static readonly BarrierSpec[] BarrierSpecs =
    {
        new BarrierSpec(
            "BARRIER_01_STILLWATER_FEED_MILL",
            OpenWorldAreaId.StillwaterFeedMill,
            "AREA_01_STILLWATER_FEED_MILL",
            "AREA_01_STILLWATER_FEED_MILL/Area Spawn",
            "AREA_01_STILLWATER_FEED_MILL/Locked Entrance",
            BoundaryAxis.X,
            235f,
            new Vector3(235f, 0f, -40f),
            new Vector3(4f, 1000f, 1600f),
            new Vector3(229f, 0f, -40f),
            new Vector3(8f, 1000f, 1600f)),

        new BarrierSpec(
            "BARRIER_02_HARROW_ESTATE",
            OpenWorldAreaId.HarrowEstate,
            "AREA_02_HARROW_ESTATE",
            "AREA_02_HARROW_ESTATE/Area Spawn",
            "AREA_02_HARROW_ESTATE/Locked Entrance",
            BoundaryAxis.Z,
            260f,
            new Vector3(40f, 0f, 260f),
            new Vector3(1600f, 1000f, 4f),
            new Vector3(40f, 0f, 254f),
            new Vector3(1600f, 1000f, 8f)),

        new BarrierSpec(
            "BARRIER_03_BLOODROOT_HOLLOW",
            OpenWorldAreaId.BloodrootHollow,
            "AREA_03_BLOODROOT_HOLLOW",
            "AREA_03_BLOODROOT_HOLLOW/Boss Arena Spawn",
            "AREA_03_BLOODROOT_HOLLOW/Locked Entrance",
            BoundaryAxis.Z,
            540f,
            new Vector3(50f, 0f, 540f),
            new Vector3(1600f, 1000f, 4f),
            new Vector3(50f, 0f, 534f),
            new Vector3(1600f, 1000f, 8f))
    };

    private static readonly OuterBoundarySpec[] OuterBoundarySpecs =
    {
        new OuterBoundarySpec(
            "World Edge West",
            new Vector3(-705f, 0f, 0f),
            new Vector3(10f, 1000f, 1420f)),
        new OuterBoundarySpec(
            "World Edge East",
            new Vector3(705f, 0f, 0f),
            new Vector3(10f, 1000f, 1420f)),
        new OuterBoundarySpec(
            "World Edge South",
            new Vector3(0f, 0f, -705f),
            new Vector3(1420f, 1000f, 10f)),
        new OuterBoundarySpec(
            "World Edge North",
            new Vector3(0f, 0f, 705f),
            new Vector3(1420f, 1000f, 10f))
    };

    [MenuItem(
        "Bloodroot/Open World/Build or Rebuild Invisible Progression Barriers",
        false,
        60)]
    public static void BuildOrRebuild()
    {
        try
        {
            RunBuild();

            EditorUtility.DisplayDialog(
                "Invisible Progression Barriers Built",
                "The visible gate blockers were removed. Stillwater, " +
                "Harrow Estate, and Bloodroot Hollow now use terrain-spanning, " +
                "renderer-free player barriers that can be disabled by " +
                "progression.",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            EditorUtility.DisplayDialog(
                "Invisible Barrier Build Failed",
                exception.Message,
                "OK");
        }
    }

    [MenuItem(
        "Bloodroot/Open World/Validate Invisible Progression Barriers",
        false,
        61)]
    public static void ValidateMenu()
    {
        try
        {
            Scene scene = RequireCleanOpenWorldScene();
            GameObject worldRoot = RequireWorldRoot(scene);
            List<string> problems = ValidateBuiltState(
                worldRoot,
                true,
                BarrierSchema.Current);

            if (problems.Count > 0)
            {
                throw new InvalidOperationException(
                    "Invisible barrier validation failed:\n\n- " +
                    string.Join("\n- ", problems));
            }

            EditorUtility.DisplayDialog(
                "Invisible Barrier Validation Passed",
                $"All {BarrierSpecs.Length} locked regions have " +
                "terrain-spanning player cutlines, no runtime " +
                "renderers, valid feedback triggers, and progression-ready " +
                "unlock components.",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            EditorUtility.DisplayDialog(
                "Invisible Barrier Validation Failed",
                exception.Message,
                "OK");
        }
    }

    private static void RunBuild()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        Scene scene = RequireCleanOpenWorldScene();
        GameObject worldRoot = RequireWorldRoot(scene);
        Transform boundaryRoot = RequirePath(worldRoot.transform, BoundaryPath);

        BuildState state = RecognizeBuildState(worldRoot, boundaryRoot);
        EnsurePersistentBackup();

        string absoluteScenePath = ToAbsoluteAssetPath(OpenWorldScenePath);
        byte[] sceneBytes = File.ReadAllBytes(absoluteScenePath);
        bool mutationStarted = false;

        try
        {
            mutationStarted = true;
            ReconcileBuiltState(worldRoot, boundaryRoot, state);

            List<string> problems = ValidateBuiltState(
                worldRoot,
                true,
                BarrierSchema.Current);

            if (problems.Count > 0)
            {
                throw new InvalidOperationException(
                    "Generated invisible barriers did not pass validation:\n\n- " +
                    string.Join("\n- ", problems));
            }

            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene, OpenWorldScenePath))
            {
                throw new IOException(
                    $"Unity could not save {OpenWorldScenePath}.");
            }

            Scene reloadedScene = EditorSceneManager.OpenScene(
                OpenWorldScenePath,
                OpenSceneMode.Single);
            GameObject reloadedWorldRoot = RequireWorldRoot(reloadedScene);
            List<string> reloadProblems =
                ValidateBuiltState(
                    reloadedWorldRoot,
                    true,
                    BarrierSchema.Current);

            if (reloadProblems.Count > 0)
            {
                throw new InvalidOperationException(
                    "Saved invisible barriers failed cold-reload validation:" +
                    "\n\n- " + string.Join("\n- ", reloadProblems));
            }

            Selection.activeGameObject =
                RequirePath(
                    RequirePath(reloadedWorldRoot.transform, BoundaryPath),
                    ContainerName).gameObject;

            Debug.Log(
                $"Invisible progression barriers built and validated: " +
                $"{BarrierSpecs.Length} terrain-spanning cutlines, " +
                $"{OuterBoundarySpecs.Length} permanent world-edge bounds, " +
                "zero visible barrier renderers.");
        }
        catch
        {
            if (mutationStarted)
            {
                RestoreSceneBytes(absoluteScenePath, sceneBytes);
            }

            throw;
        }
    }

    private static Scene RequireCleanOpenWorldScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException(
                "Exit Play Mode before building invisible barriers.");
        }

        Scene activeScene = SceneManager.GetActiveScene();

        if (!activeScene.IsValid() || !activeScene.isLoaded ||
            activeScene.path != OpenWorldScenePath)
        {
            throw new InvalidOperationException(
                $"Open {OpenWorldScenePath} as the active scene first.");
        }

        if (SceneManager.sceneCount != 1)
        {
            throw new InvalidOperationException(
                "Close additive scenes before building barriers. The tool " +
                "requires the open-world scene to be the only loaded scene " +
                "so rollback cannot disturb unrelated work.");
        }

        if (activeScene.isDirty)
        {
            throw new InvalidOperationException(
                "Save or discard the current scene changes before building " +
                "invisible barriers.");
        }

        return activeScene;
    }

    private static GameObject RequireWorldRoot(Scene scene)
    {
        GameObject worldRoot =
            scene.GetRootGameObjects().FirstOrDefault(
                root => root.name == WorldRootName);

        if (worldRoot == null)
        {
            throw new InvalidOperationException(
                $"The required scene root {WorldRootName} is missing.");
        }

        return worldRoot;
    }

    private static BuildState RecognizeBuildState(
        GameObject worldRoot,
        Transform boundaryRoot)
    {
        Transform container = boundaryRoot.Find(ContainerName);

        if (container == null)
        {
            foreach (BarrierSpec spec in BarrierSpecs)
            {
                Transform gate = RequirePath(worldRoot.transform, spec.gatePath);

                if (!IsRecognizedLegacyGate(gate))
                {
                    throw new InvalidOperationException(
                        $"{spec.gatePath} is neither the recognized visible " +
                        "placeholder nor a current invisible barrier. Refusing " +
                        "to replace unrecognized scene content.");
                }
            }

            if (boundaryRoot.Cast<Transform>().Any(
                    child => child.name.StartsWith(
                        SignaturePrefix,
                        StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "A barrier signature exists without its generated root.");
            }

            return BuildState.LegacyVisiblePlaceholders;
        }

        List<string> problems = ValidateBuiltState(
            worldRoot,
            false,
            BarrierSchema.Current);

        if (problems.Count > 0)
        {
            List<string> previousProblems = ValidateBuiltState(
                worldRoot,
                false,
                BarrierSchema.PreviousShortCutlines);

            if (previousProblems.Count == 0)
            {
                return BuildState.PreviousShortCutlines;
            }
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                "Rebuild refused because the existing generated barriers " +
                "are partial or unrecognized:\n\n- " +
                string.Join("\n- ", problems));
        }

        return BuildState.CurrentInvisibleBarriers;
    }

    private static bool IsRecognizedLegacyGate(Transform gate)
    {
        Transform barrier = gate.Find("Barrier");
        Transform beacon = gate.Find("Status Beacon");
        Transform feedback = gate.Find("Locked Feedback Trigger");

        return barrier != null &&
               barrier.GetComponent<BoxCollider>() != null &&
               barrier.GetComponent<Renderer>() != null &&
               beacon != null &&
               beacon.GetComponent<Collider>() != null &&
               beacon.GetComponent<Renderer>() != null &&
               feedback != null &&
               feedback.GetComponent<BoxCollider>() is BoxCollider trigger &&
               trigger.isTrigger;
    }

    private static void EnsurePersistentBackup()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BackupScenePath) == null)
        {
            if (File.Exists(ToAbsoluteAssetPath(BackupScenePath)) ||
                File.Exists(ToAbsoluteAssetPath(BackupScenePath + ".meta")))
            {
                throw new IOException(
                    "A partial pre-barrier backup exists on disk. Resolve it " +
                    "before running the build.");
            }

            if (!AssetDatabase.CopyAsset(OpenWorldScenePath, BackupScenePath))
            {
                throw new IOException(
                    $"Unity could not create {BackupScenePath}.");
            }

            AssetDatabase.ImportAsset(
                BackupScenePath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
        }

        ValidatePersistentBackup();
    }

    private static void ValidatePersistentBackup()
    {
        string absoluteBackupPath = ToAbsoluteAssetPath(BackupScenePath);

        if (!File.Exists(absoluteBackupPath) ||
            !File.Exists(absoluteBackupPath + ".meta"))
        {
            throw new IOException(
                "The pre-barrier scene backup or its meta file is missing.");
        }

        string backupText = File.ReadAllText(absoluteBackupPath);

        if (backupText.Contains(ContainerName, StringComparison.Ordinal) ||
            backupText.Contains(SignaturePrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The persistent pre-barrier backup contains generated " +
                "invisible barriers and is not a valid recovery point.");
        }

        if (CountOccurrences(backupText, "m_Name: Barrier") != 3 ||
            CountOccurrences(backupText, "m_Name: Status Beacon") != 3)
        {
            throw new InvalidOperationException(
                "The persistent backup does not contain the three recognized " +
                "pre-migration visible gate placeholders.");
        }

        if (EditorBuildSettings.scenes.Any(
                buildScene => buildScene.enabled &&
                              buildScene.path == BackupScenePath))
        {
            throw new InvalidOperationException(
                "The pre-barrier backup must not be enabled in Build Settings.");
        }
    }

    private static void ReconcileBuiltState(
        GameObject worldRoot,
        Transform boundaryRoot,
        BuildState state)
    {
        int barrierLayer = LayerMask.NameToLayer(BarrierLayerName);

        if (barrierLayer < 0)
        {
            throw new InvalidOperationException(
                $"The required {BarrierLayerName} layer is missing.");
        }

        if (state == BuildState.LegacyVisiblePlaceholders)
        {
            foreach (BarrierSpec spec in BarrierSpecs)
            {
                Transform gate = RequirePath(worldRoot.transform, spec.gatePath);
                DestroyDirectChild(gate, "Barrier");
                DestroyDirectChild(gate, "Status Beacon");
            }
        }

        GameObject containerObject =
            GetOrCreateDirectChild(boundaryRoot, ContainerName);
        Transform container = containerObject.transform;
        SetLocalIdentity(container);
        containerObject.SetActive(true);

        for (int specIndex = 0;
             specIndex < BarrierSpecs.Length;
             specIndex++)
        {
            BarrierSpec spec = BarrierSpecs[specIndex];
            Transform gate = RequirePath(worldRoot.transform, spec.gatePath);
            GameObject feedback = ConfigureFeedbackTrigger(spec, gate);

            GameObject barrierObject =
                GetOrCreateDirectChild(container, spec.rootName);
            Transform barrierRoot = barrierObject.transform;
            SetLocalIdentity(barrierRoot);
            barrierRoot.SetSiblingIndex(specIndex);
            barrierObject.layer = barrierLayer;
            barrierObject.SetActive(true);

            OpenWorldAreaBarrier barrier =
                barrierObject.GetComponent<OpenWorldAreaBarrier>();

            if (barrier == null)
            {
                barrier = barrierObject.AddComponent<OpenWorldAreaBarrier>();
            }

            barrier.enabled = true;

            NavMeshModifier modifier =
                barrierObject.GetComponent<NavMeshModifier>();

            if (modifier == null)
            {
                modifier = barrierObject.AddComponent<NavMeshModifier>();
            }

            modifier.enabled = true;
            modifier.ignoreFromBuild = true;
            modifier.applyToChildren = true;
            EditorUtility.SetDirty(modifier);

            BoxCollider[] blockingColliders =
                ReconcileSegments(barrierRoot, spec, barrierLayer);

            barrier.Configure(
                spec.areaId,
                false,
                feedback,
                blockingColliders);
            EditorUtility.SetDirty(barrier);

            ConfigureFeedbackPhysics(feedback);
        }

        ReconcileOuterBounds(container, barrierLayer);

        if (state == BuildState.PreviousShortCutlines)
        {
            DestroyDirectChild(container, PreviousShortCutlineSignature);
        }

        GameObject signature =
            GetOrCreateDirectChild(
                container,
                ExpectedSignatureName(BarrierSchema.Current));
        SetLocalIdentity(signature.transform);
        signature.SetActive(true);
        signature.transform.SetSiblingIndex(BarrierSpecs.Length + 1);

        foreach (Transform child in container.Cast<Transform>().ToArray())
        {
            bool expectedBarrier =
                BarrierSpecs.Any(spec => spec.rootName == child.name);
            bool expectedOuterBounds = child.name == OuterBoundsRootName;
            bool expectedSignature = child.name ==
                                     ExpectedSignatureName(
                                         BarrierSchema.Current);

            if (!expectedBarrier && !expectedOuterBounds &&
                !expectedSignature)
            {
                throw new InvalidOperationException(
                    $"Unrecognized object {child.name} exists under " +
                    $"{ContainerName}.");
            }
        }
    }

    private static void ReconcileOuterBounds(
        Transform container,
        int barrierLayer)
    {
        GameObject outerRootObject =
            GetOrCreateDirectChild(container, OuterBoundsRootName);
        Transform outerRoot = outerRootObject.transform;
        SetLocalIdentity(outerRoot);
        outerRoot.SetSiblingIndex(BarrierSpecs.Length);
        outerRootObject.layer = barrierLayer;
        outerRootObject.SetActive(true);

        NavMeshModifier modifier =
            outerRootObject.GetComponent<NavMeshModifier>();

        if (modifier == null)
        {
            modifier = outerRootObject.AddComponent<NavMeshModifier>();
        }

        modifier.enabled = true;
        modifier.ignoreFromBuild = true;
        modifier.applyToChildren = true;
        EditorUtility.SetDirty(modifier);

        for (int index = 0; index < OuterBoundarySpecs.Length; index++)
        {
            OuterBoundarySpec spec = OuterBoundarySpecs[index];
            GameObject edge = GetOrCreateDirectChild(outerRoot, spec.name);
            edge.transform.SetSiblingIndex(index);
            edge.transform.position = spec.center;
            edge.transform.rotation = Quaternion.identity;
            edge.transform.localScale = Vector3.one;
            edge.layer = barrierLayer;
            edge.SetActive(true);

            BoxCollider collider = edge.GetComponent<BoxCollider>();

            if (collider == null)
            {
                collider = edge.AddComponent<BoxCollider>();
            }

            collider.enabled = true;
            collider.isTrigger = false;
            collider.center = Vector3.zero;
            collider.size = spec.size;
            EditorUtility.SetDirty(collider);
        }

        foreach (Transform child in outerRoot.Cast<Transform>().ToArray())
        {
            if (!OuterBoundarySpecs.Any(spec => spec.name == child.name))
            {
                throw new InvalidOperationException(
                    $"Unrecognized child {child.name} exists under " +
                    $"{OuterBoundsRootName}.");
            }
        }
    }

    private static GameObject ConfigureFeedbackTrigger(
        BarrierSpec spec,
        Transform gate)
    {
        Transform feedback = gate.Find("Locked Feedback Trigger");

        if (feedback == null)
        {
            throw new InvalidOperationException(
                $"{spec.gatePath}/Locked Feedback Trigger is missing.");
        }

        feedback.position = spec.feedbackCenter;
        feedback.rotation = Quaternion.identity;
        feedback.localScale = Vector3.one;
        feedback.gameObject.layer = LayerMask.NameToLayer(BarrierLayerName);
        feedback.gameObject.SetActive(true);

        BoxCollider trigger = feedback.GetComponent<BoxCollider>();

        if (trigger == null)
        {
            trigger = feedback.gameObject.AddComponent<BoxCollider>();
        }

        trigger.enabled = true;
        trigger.isTrigger = true;
        trigger.center = Vector3.zero;
        trigger.size = spec.feedbackSize;
        EditorUtility.SetDirty(trigger);

        return feedback.gameObject;
    }

    private static void ConfigureFeedbackPhysics(GameObject feedback)
    {
        Rigidbody body = feedback.GetComponent<Rigidbody>();

        if (body == null)
        {
            body = feedback.AddComponent<Rigidbody>();
        }

        body.isKinematic = true;
        body.useGravity = false;
        body.collisionDetectionMode = CollisionDetectionMode.Discrete;
        EditorUtility.SetDirty(body);

    }

    private static BoxCollider[] ReconcileSegments(
        Transform barrierRoot,
        BarrierSpec spec,
        int barrierLayer)
    {
        BoxCollider[] blockingColliders = new BoxCollider[SegmentCount];

        for (int index = 0; index < SegmentCount; index++)
        {
            string segmentName = $"Boundary Segment {index:00}";
            GameObject segment =
                GetOrCreateDirectChild(barrierRoot, segmentName);
            segment.layer = barrierLayer;
            segment.SetActive(true);
            segment.transform.SetSiblingIndex(index);

            GetSegmentGeometry(
                spec,
                index,
                BarrierSchema.Current,
                out Vector3 position,
                out Quaternion rotation,
                out Vector3 colliderSize);

            segment.transform.position = position;
            segment.transform.rotation = rotation;
            segment.transform.localScale = Vector3.one;

            BoxCollider collider = segment.GetComponent<BoxCollider>();

            if (collider == null)
            {
                collider = segment.AddComponent<BoxCollider>();
            }

            collider.enabled = true;
            collider.isTrigger = false;
            collider.center = Vector3.zero;
            collider.size = colliderSize;
            EditorUtility.SetDirty(collider);
            blockingColliders[index] = collider;

            Component[] components = segment.GetComponents<Component>();

            if (components.Any(
                    component => component is not Transform &&
                                 component is not BoxCollider))
            {
                throw new InvalidOperationException(
                    $"{spec.rootName}/{segmentName} contains an " +
                    "unrecognized component.");
            }
        }

        foreach (Transform child in barrierRoot.Cast<Transform>().ToArray())
        {
            if (!TryParseSegmentIndex(child.name, out int index) ||
                index < 0 || index >= SegmentCount)
            {
                throw new InvalidOperationException(
                    $"Unrecognized child {child.name} exists under " +
                    $"{spec.rootName}.");
            }
        }

        return blockingColliders;
    }

    private static List<string> ValidateBuiltState(
        GameObject worldRoot,
        bool validateBackup,
        BarrierSchema schema)
    {
        List<string> problems = new List<string>();
        Transform boundaryRoot = worldRoot.transform.Find(BoundaryPath);

        if (boundaryRoot == null)
        {
            problems.Add($"Missing {BoundaryPath}.");
            return problems;
        }

        Transform container = boundaryRoot.Find(ContainerName);

        if (container == null)
        {
            problems.Add($"Missing {BoundaryPath}/{ContainerName}.");
            return problems;
        }

        if (!container.gameObject.activeSelf)
        {
            problems.Add("The generated barrier container is inactive.");
        }

        Transform[] signatures =
            container.Cast<Transform>()
                .Where(child => child.name.StartsWith(
                    SignaturePrefix,
                    StringComparison.Ordinal))
                .ToArray();

        string expectedSignature = ExpectedSignatureName(schema);

        if (signatures.Length != 1 ||
            signatures[0].name != expectedSignature)
        {
            problems.Add(
                $"Expected exactly one signature named " +
                $"{expectedSignature}.");
        }

        int borderLayer = LayerMask.NameToLayer(BarrierLayerName);
        int playerLayer = LayerMask.NameToLayer(PlayerLayerName);

        if (borderLayer < 0)
        {
            problems.Add($"Layer {BarrierLayerName} is missing.");
        }

        if (playerLayer < 0)
        {
            problems.Add($"Layer {PlayerLayerName} is missing.");
        }

        if (borderLayer >= 0 && playerLayer >= 0 &&
            Physics.GetIgnoreLayerCollision(borderLayer, playerLayer))
        {
            problems.Add(
                $"Layers {BarrierLayerName} and {PlayerLayerName} do not " +
                "collide in the Physics settings.");
        }

        foreach (BarrierSpec spec in BarrierSpecs)
        {
            ValidateGateIsInvisible(
                problems,
                worldRoot.transform,
                spec,
                schema);
            ValidateBarrier(
                problems,
                worldRoot.transform,
                container,
                spec,
                schema);
        }

        ValidateOuterBounds(problems, container, borderLayer);

        Renderer[] renderers =
            container.GetComponentsInChildren<Renderer>(includeInactive: true);

        if (renderers.Length != 0)
        {
            problems.Add(
                "Generated invisible barriers contain runtime renderers.");
        }

        MeshFilter[] meshFilters =
            container.GetComponentsInChildren<MeshFilter>(includeInactive: true);

        if (meshFilters.Length != 0)
        {
            problems.Add(
                "Generated invisible barriers contain mesh geometry.");
        }

        int expectedChildren = BarrierSpecs.Length + 2;

        if (container.childCount != expectedChildren)
        {
            problems.Add(
                $"{ContainerName} must contain exactly {expectedChildren} " +
                "direct children.");
        }

        Transform arrival = worldRoot.transform.Find(
            "AREA_00_BLACK_PINES_FOREST/World Arrival Spawn");

        if (arrival == null)
        {
            problems.Add("Black Pines World Arrival Spawn is missing.");
        }
        else
        {
            Vector2 arrivalPoint =
                new Vector2(arrival.position.x, arrival.position.z);

            foreach (BarrierSpec spec in BarrierSpecs)
            {
                if (SignedDistanceToLockedSide(arrivalPoint, spec) >= 0f)
                {
                    problems.Add(
                        $"Black Pines arrival is on the locked side of " +
                        $"{spec.rootName}.");
                }
            }
        }

        if (validateBackup)
        {
            try
            {
                ValidatePersistentBackup();
            }
            catch (Exception exception)
            {
                problems.Add(exception.Message);
            }
        }

        return problems;
    }

    private static void ValidateOuterBounds(
        ICollection<string> problems,
        Transform container,
        int borderLayer)
    {
        Transform outerRoot = container.Find(OuterBoundsRootName);

        if (outerRoot == null)
        {
            problems.Add($"Missing {OuterBoundsRootName}.");
            return;
        }

        NavMeshModifier modifier =
            outerRoot.GetComponent<NavMeshModifier>();

        if (!outerRoot.gameObject.activeSelf ||
            outerRoot.gameObject.layer != borderLayer ||
            modifier == null || !modifier.enabled ||
            !modifier.ignoreFromBuild || !modifier.applyToChildren)
        {
            problems.Add(
                $"{OuterBoundsRootName} has invalid layer or NavMesh settings.");
        }

        if (outerRoot.childCount != OuterBoundarySpecs.Length)
        {
            problems.Add(
                $"{OuterBoundsRootName} must contain exactly " +
                $"{OuterBoundarySpecs.Length} world-edge colliders.");
        }

        foreach (OuterBoundarySpec spec in OuterBoundarySpecs)
        {
            Transform edge = outerRoot.Find(spec.name);
            BoxCollider collider =
                edge != null ? edge.GetComponent<BoxCollider>() : null;

            if (edge == null || !edge.gameObject.activeSelf ||
                edge.gameObject.layer != borderLayer ||
                Vector3.Distance(edge.position, spec.center) > 0.02f ||
                Quaternion.Angle(edge.rotation, Quaternion.identity) > 0.05f ||
                !Approximately(edge.localScale, Vector3.one, 0.001f) ||
                collider == null || !collider.enabled || collider.isTrigger ||
                !Approximately(collider.center, Vector3.zero, 0.001f) ||
                !Approximately(collider.size, spec.size, 0.02f) ||
                edge.GetComponents<Component>().Any(
                    component => component is not Transform &&
                                 component is not BoxCollider))
            {
                problems.Add(
                    $"{OuterBoundsRootName}/{spec.name} is invalid.");
            }
        }

        BoxCollider west = GetDirectBoxCollider(outerRoot, "World Edge West");
        BoxCollider east = GetDirectBoxCollider(outerRoot, "World Edge East");
        BoxCollider south = GetDirectBoxCollider(outerRoot, "World Edge South");
        BoxCollider north = GetDirectBoxCollider(outerRoot, "World Edge North");

        ValidatePositiveBoundsOverlap(
            problems,
            west,
            south,
            "World Edge West and World Edge South");
        ValidatePositiveBoundsOverlap(
            problems,
            west,
            north,
            "World Edge West and World Edge North");
        ValidatePositiveBoundsOverlap(
            problems,
            east,
            south,
            "World Edge East and World Edge South");
        ValidatePositiveBoundsOverlap(
            problems,
            east,
            north,
            "World Edge East and World Edge North");
    }

    private static BoxCollider GetDirectBoxCollider(
        Transform parent,
        string childName)
    {
        Transform child = parent != null ? parent.Find(childName) : null;
        return child != null ? child.GetComponent<BoxCollider>() : null;
    }

    private static void ValidatePositiveBoundsOverlap(
        ICollection<string> problems,
        BoxCollider first,
        BoxCollider second,
        string seamName)
    {
        if (first == null || second == null ||
            !first.enabled || !second.enabled ||
            !BoundsOverlapByAtLeast(first.bounds, second.bounds, 1f))
        {
            problems.Add(
                $"{seamName} must overlap by at least one metre on every " +
                "axis so the player cannot pass through the seam.");
        }
    }

    private static bool BoundsOverlapByAtLeast(
        Bounds first,
        Bounds second,
        float minimumDepth)
    {
        Vector3 overlapMinimum = Vector3.Max(first.min, second.min);
        Vector3 overlapMaximum = Vector3.Min(first.max, second.max);
        Vector3 overlap = overlapMaximum - overlapMinimum;

        return overlap.x >= minimumDepth &&
               overlap.y >= minimumDepth &&
               overlap.z >= minimumDepth;
    }

    private static void ValidateGateIsInvisible(
        ICollection<string> problems,
        Transform worldRoot,
        BarrierSpec spec,
        BarrierSchema schema)
    {
        Transform gate = worldRoot.Find(spec.gatePath);

        if (gate == null)
        {
            problems.Add($"Missing {spec.gatePath}.");
            return;
        }

        if (gate.Find("Barrier") != null ||
            gate.Find("Status Beacon") != null)
        {
            problems.Add(
                $"{spec.gatePath} still contains a visible physical " +
                "placeholder.");
        }

        if (gate.GetComponentsInChildren<Renderer>(includeInactive: true)
                .Length != 0 ||
            gate.GetComponentsInChildren<MeshFilter>(includeInactive: true)
                .Length != 0)
        {
            problems.Add(
                $"{spec.gatePath} still contains visible mesh content.");
        }

        Collider[] solidGateColliders =
            gate.GetComponentsInChildren<Collider>(includeInactive: true)
                .Where(collider => !collider.isTrigger)
                .ToArray();

        if (solidGateColliders.Length != 0)
        {
            problems.Add(
                $"{spec.gatePath} still contains a local solid blocker.");
        }

        Transform feedback = gate.Find("Locked Feedback Trigger");
        BoxCollider trigger =
            feedback != null ? feedback.GetComponent<BoxCollider>() : null;
        Rigidbody body =
            feedback != null ? feedback.GetComponent<Rigidbody>() : null;
        if (feedback == null || !feedback.gameObject.activeSelf ||
            feedback.gameObject.layer != GetExpectedFeedbackLayer(schema) ||
            trigger == null || !trigger.enabled || !trigger.isTrigger ||
            Vector3.Distance(feedback.position, spec.feedbackCenter) > 0.02f ||
            Quaternion.Angle(feedback.rotation, Quaternion.identity) > 0.05f ||
            !Approximately(
                trigger.size,
                GetExpectedFeedbackSize(spec, schema),
                0.01f) ||
            body == null || !body.isKinematic || body.useGravity ||
            body.collisionDetectionMode != CollisionDetectionMode.Discrete)
        {
            problems.Add(
                $"{spec.gatePath} has an invalid locked-feedback trigger.");
        }
    }

    private static void ValidateBarrier(
        ICollection<string> problems,
        Transform worldRoot,
        Transform container,
        BarrierSpec spec,
        BarrierSchema schema)
    {
        Transform barrierRoot = container.Find(spec.rootName);

        if (barrierRoot == null)
        {
            problems.Add($"Missing {ContainerName}/{spec.rootName}.");
            return;
        }

        int borderLayer = LayerMask.NameToLayer(BarrierLayerName);
        OpenWorldAreaBarrier barrier =
            barrierRoot.GetComponent<OpenWorldAreaBarrier>();
        NavMeshModifier modifier =
            barrierRoot.GetComponent<NavMeshModifier>();

        if (!barrierRoot.gameObject.activeSelf ||
            barrierRoot.gameObject.layer != borderLayer)
        {
            problems.Add($"{spec.rootName} is inactive or on the wrong layer.");
        }

        Transform feedback =
            worldRoot.Find(spec.gatePath + "/Locked Feedback Trigger");

        if (barrier == null || !barrier.enabled ||
            barrier.Area != spec.areaId || barrier.StartsUnlocked ||
            barrier.LockedFeedbackTrigger !=
                (feedback != null ? feedback.gameObject : null) ||
            barrier.BlockingColliderCount != SegmentCount)
        {
            problems.Add(
                $"{spec.rootName} has invalid progression configuration.");
        }

        if (modifier == null || !modifier.enabled ||
            !modifier.ignoreFromBuild || !modifier.applyToChildren)
        {
            problems.Add(
                $"{spec.rootName} must be excluded from static NavMesh baking.");
        }

        if (barrierRoot.childCount != SegmentCount)
        {
            problems.Add(
                $"{spec.rootName} must contain exactly {SegmentCount} " +
                "boundary segments.");
        }

        for (int index = 0; index < SegmentCount; index++)
        {
            string segmentName = $"Boundary Segment {index:00}";
            Transform segment = barrierRoot.Find(segmentName);

            if (segment == null)
            {
                problems.Add($"{spec.rootName}/{segmentName} is missing.");
                continue;
            }

            GetSegmentGeometry(
                spec,
                index,
                schema,
                out Vector3 expectedPosition,
                out Quaternion expectedRotation,
                out Vector3 expectedSize);

            BoxCollider collider = segment.GetComponent<BoxCollider>();

            if (!segment.gameObject.activeSelf ||
                segment.gameObject.layer != borderLayer ||
                Vector3.Distance(segment.position, expectedPosition) > 0.02f ||
                Quaternion.Angle(segment.rotation, expectedRotation) > 0.05f ||
                !Approximately(segment.localScale, Vector3.one, 0.001f) ||
                collider == null || !collider.enabled || collider.isTrigger ||
                !Approximately(collider.center, Vector3.zero, 0.001f) ||
                !Approximately(collider.size, expectedSize, 0.02f))
            {
                problems.Add(
                    $"{spec.rootName}/{segmentName} does not match its " +
                    "terrain-cutline specification.");
            }

            if (segment.GetComponents<Component>().Any(
                    component => component is not Transform &&
                                 component is not BoxCollider))
            {
                problems.Add(
                    $"{spec.rootName}/{segmentName} has an unexpected " +
                    "component.");
            }

            if (collider != null && barrier != null &&
                !barrier.OwnsCollider(collider))
            {
                problems.Add(
                    $"{spec.rootName} does not own {segmentName}'s collider.");
            }
        }

        if (schema == BarrierSchema.Current)
        {
            Transform segment = barrierRoot.Find("Boundary Segment 00");
            BoxCollider blocker =
                segment != null ? segment.GetComponent<BoxCollider>() : null;
            Transform feedbackTransform = worldRoot.Find(
                spec.gatePath + "/Locked Feedback Trigger");
            BoxCollider feedbackCollider = feedbackTransform != null
                ? feedbackTransform.GetComponent<BoxCollider>()
                : null;

            ValidateCutlineClosure(
                problems,
                container,
                spec,
                blocker,
                feedbackCollider);
        }

        Transform spawn = worldRoot.Find(spec.spawnPath);

        if (spawn == null ||
            SignedDistanceToLockedSide(
                new Vector2(spawn.position.x, spawn.position.z),
                spec) <= 10f)
        {
            problems.Add(
                $"{spec.spawnPath} is not safely beyond {spec.rootName}.");
        }

        Transform gate = worldRoot.Find(spec.gatePath);

        if (gate == null ||
            Mathf.Abs(SignedDistanceToLockedSide(
                new Vector2(gate.position.x, gate.position.z),
                spec)) > 0.1f)
        {
            problems.Add(
                $"{spec.gatePath} is not aligned to {spec.rootName}.");
        }
    }

    private static void ValidateCutlineClosure(
        ICollection<string> problems,
        Transform container,
        BarrierSpec spec,
        BoxCollider blocker,
        BoxCollider feedback)
    {
        Transform outerRoot = container.Find(OuterBoundsRootName);
        string firstEdgeName = spec.axis == BoundaryAxis.X
            ? "World Edge South"
            : "World Edge West";
        string secondEdgeName = spec.axis == BoundaryAxis.X
            ? "World Edge North"
            : "World Edge East";
        BoxCollider firstEdge =
            GetDirectBoxCollider(outerRoot, firstEdgeName);
        BoxCollider secondEdge =
            GetDirectBoxCollider(outerRoot, secondEdgeName);

        ValidatePositiveBoundsOverlap(
            problems,
            blocker,
            firstEdge,
            $"{spec.rootName} and {firstEdgeName}");
        ValidatePositiveBoundsOverlap(
            problems,
            blocker,
            secondEdge,
            $"{spec.rootName} and {secondEdgeName}");
        ValidatePositiveBoundsOverlap(
            problems,
            feedback,
            firstEdge,
            $"{spec.rootName} feedback and {firstEdgeName}");
        ValidatePositiveBoundsOverlap(
            problems,
            feedback,
            secondEdge,
            $"{spec.rootName} feedback and {secondEdgeName}");

        if (blocker == null || feedback == null)
        {
            return;
        }

        Bounds blockerBounds = blocker.bounds;
        Bounds feedbackBounds = feedback.bounds;
        float blockerMinimum = spec.axis == BoundaryAxis.X
            ? blockerBounds.min.z
            : blockerBounds.min.x;
        float blockerMaximum = spec.axis == BoundaryAxis.X
            ? blockerBounds.max.z
            : blockerBounds.max.x;
        float feedbackMinimum = spec.axis == BoundaryAxis.X
            ? feedbackBounds.min.z
            : feedbackBounds.min.x;
        float feedbackMaximum = spec.axis == BoundaryAxis.X
            ? feedbackBounds.max.z
            : feedbackBounds.max.x;

        if (feedbackMinimum > blockerMinimum + 0.02f ||
            feedbackMaximum < blockerMaximum - 0.02f)
        {
            problems.Add(
                $"{spec.rootName}'s feedback trigger must cover the entire " +
                "transverse blocker span.");
        }
    }

    private static Vector3 GetExpectedBlockerSize(
        BarrierSpec spec,
        BarrierSchema schema)
    {
        Vector3 size = spec.blockerSize;

        if (schema == BarrierSchema.PreviousShortCutlines)
        {
            if (spec.axis == BoundaryAxis.X)
            {
                size.z = PreviousShortTransverseSize;
            }
            else
            {
                size.x = PreviousShortTransverseSize;
            }
        }

        return size;
    }

    private static Vector3 GetExpectedFeedbackSize(
        BarrierSpec spec,
        BarrierSchema schema)
    {
        Vector3 size = spec.feedbackSize;

        if (schema == BarrierSchema.PreviousShortCutlines)
        {
            if (spec.axis == BoundaryAxis.X)
            {
                size.z = PreviousShortTransverseSize;
            }
            else
            {
                size.x = PreviousShortTransverseSize;
            }
        }

        return size;
    }

    private static int GetExpectedFeedbackLayer(BarrierSchema schema)
    {
        return schema == BarrierSchema.PreviousShortCutlines
            ? LayerMask.NameToLayer("Default")
            : LayerMask.NameToLayer(BarrierLayerName);
    }

    private static void GetSegmentGeometry(
        BarrierSpec spec,
        int index,
        BarrierSchema schema,
        out Vector3 position,
        out Quaternion rotation,
        out Vector3 colliderSize)
    {
        if (index != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        position = spec.blockerCenter;
        rotation = Quaternion.identity;
        colliderSize = GetExpectedBlockerSize(spec, schema);
    }

    private static float SignedDistanceToLockedSide(
        Vector2 point,
        BarrierSpec spec)
    {
        float coordinate =
            spec.axis == BoundaryAxis.X ? point.x : point.y;
        return coordinate - spec.cutlineCoordinate;
    }

    private static string ExpectedSignatureName(BarrierSchema schema)
    {
        if (schema == BarrierSchema.PreviousShortCutlines)
        {
            return PreviousShortCutlineSignature;
        }

        StringBuilder builder = new StringBuilder(
            "BloodrootInvisibleProgressionBarriers|v3|sealed-cutlines|");

        builder.Append(SegmentCount).Append('|');
        builder.Append(BarrierLayerName).Append('|');

        foreach (BarrierSpec spec in BarrierSpecs)
        {
            builder.Append(spec.rootName).Append('|');
            builder.Append((int)spec.areaId).Append('|');
            builder.Append(spec.areaPath).Append('|');
            builder.Append(spec.spawnPath).Append('|');
            builder.Append(spec.gatePath).Append('|');
            builder.Append((int)spec.axis).Append('|');
            AppendFloat(builder, spec.cutlineCoordinate);
            AppendVector3(builder, spec.blockerCenter);
            AppendVector3(builder, spec.blockerSize);
            AppendVector3(builder, spec.feedbackCenter);
            AppendVector3(builder, spec.feedbackSize);
        }

        builder.Append(OuterBoundsRootName).Append('|');

        foreach (OuterBoundarySpec spec in OuterBoundarySpecs)
        {
            builder.Append(spec.name).Append('|');
            AppendVector3(builder, spec.center);
            AppendVector3(builder, spec.size);
        }

        string hash = Hash128.Compute(builder.ToString()).ToString();
        return SignaturePrefix + hash.Substring(0, 16).ToUpperInvariant();
    }

    private static void AppendVector3(StringBuilder builder, Vector3 value)
    {
        AppendFloat(builder, value.x);
        AppendFloat(builder, value.y);
        AppendFloat(builder, value.z);
    }

    private static void AppendFloat(StringBuilder builder, float value)
    {
        builder.Append(value.ToString("R", CultureInfo.InvariantCulture));
        builder.Append('|');
    }

    private static GameObject GetOrCreateDirectChild(
        Transform parent,
        string name)
    {
        Transform[] matches =
            parent.Cast<Transform>()
                .Where(child => child.name == name)
                .ToArray();

        if (matches.Length > 1)
        {
            throw new InvalidOperationException(
                $"Multiple direct children named {name} exist under " +
                $"{parent.name}.");
        }

        if (matches.Length == 1)
        {
            return matches[0].gameObject;
        }

        GameObject created = new GameObject(name);
        created.transform.SetParent(parent, false);
        return created;
    }

    private static void DestroyDirectChild(Transform parent, string name)
    {
        Transform[] matches =
            parent.Cast<Transform>()
                .Where(child => child.name == name)
                .ToArray();

        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"Expected exactly one {name} under {parent.name}.");
        }

        Object.DestroyImmediate(matches[0].gameObject);
    }

    private static Transform RequirePath(Transform root, string path)
    {
        Transform result = root.Find(path);

        if (result == null)
        {
            throw new InvalidOperationException(
                $"Required hierarchy path is missing: {root.name}/{path}");
        }

        return result;
    }

    private static void SetLocalIdentity(Transform transform)
    {
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }

    private static bool TryParseSegmentIndex(string name, out int index)
    {
        const string prefix = "Boundary Segment ";

        if (!name.StartsWith(prefix, StringComparison.Ordinal))
        {
            index = -1;
            return false;
        }

        return int.TryParse(
            name.Substring(prefix.Length),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out index);
    }

    private static bool Approximately(
        Vector3 actual,
        Vector3 expected,
        float tolerance)
    {
        return Mathf.Abs(actual.x - expected.x) <= tolerance &&
               Mathf.Abs(actual.y - expected.y) <= tolerance &&
               Mathf.Abs(actual.z - expected.z) <= tolerance;
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int start = 0;

        while ((start = text.IndexOf(
                   value,
                   start,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            start += value.Length;
        }

        return count;
    }

    private static string ToAbsoluteAssetPath(string assetPath)
    {
        string projectRoot =
            Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static void RestoreSceneBytes(
        string absoluteScenePath,
        byte[] sceneBytes)
    {
        try
        {
            File.WriteAllBytes(absoluteScenePath, sceneBytes);
            AssetDatabase.ImportAsset(
                OpenWorldScenePath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            EditorSceneManager.OpenScene(
                OpenWorldScenePath,
                OpenSceneMode.Single);
            Debug.LogWarning(
                "Invisible barrier build failed; the open-world scene was " +
                "restored from its per-run snapshot.");
        }
        catch (Exception rollbackException)
        {
            throw new InvalidOperationException(
                "Invisible barrier generation failed and automatic rollback " +
                $"also failed. Recover from {BackupScenePath}. " +
                rollbackException.Message,
                rollbackException);
        }
    }

    private enum BuildState
    {
        LegacyVisiblePlaceholders,
        PreviousShortCutlines,
        CurrentInvisibleBarriers
    }

    private enum BarrierSchema
    {
        PreviousShortCutlines,
        Current
    }

    private enum BoundaryAxis
    {
        X,
        Z
    }

    private readonly struct BarrierSpec
    {
        public BarrierSpec(
            string rootName,
            OpenWorldAreaId areaId,
            string areaPath,
            string spawnPath,
            string gatePath,
            BoundaryAxis axis,
            float cutlineCoordinate,
            Vector3 blockerCenter,
            Vector3 blockerSize,
            Vector3 feedbackCenter,
            Vector3 feedbackSize)
        {
            this.rootName = rootName;
            this.areaId = areaId;
            this.areaPath = areaPath;
            this.spawnPath = spawnPath;
            this.gatePath = gatePath;
            this.axis = axis;
            this.cutlineCoordinate = cutlineCoordinate;
            this.blockerCenter = blockerCenter;
            this.blockerSize = blockerSize;
            this.feedbackCenter = feedbackCenter;
            this.feedbackSize = feedbackSize;
        }

        public readonly string rootName;
        public readonly OpenWorldAreaId areaId;
        public readonly string areaPath;
        public readonly string spawnPath;
        public readonly string gatePath;
        public readonly BoundaryAxis axis;
        public readonly float cutlineCoordinate;
        public readonly Vector3 blockerCenter;
        public readonly Vector3 blockerSize;
        public readonly Vector3 feedbackCenter;
        public readonly Vector3 feedbackSize;
    }

    private readonly struct OuterBoundarySpec
    {
        public OuterBoundarySpec(
            string name,
            Vector3 center,
            Vector3 size)
        {
            this.name = name;
            this.center = center;
            this.size = size;
        }

        public readonly string name;
        public readonly Vector3 center;
        public readonly Vector3 size;
    }
}
#endif
