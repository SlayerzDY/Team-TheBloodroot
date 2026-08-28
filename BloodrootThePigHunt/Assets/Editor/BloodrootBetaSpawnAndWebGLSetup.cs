#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Bloodroot.Features.AlphaMenu;
using Bloodroot.Features.FarmPrologue;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Applies the beta-safe Farm spawn boundary, removes Screecher from authored
/// spawner rosters, and hides application-exit controls in WebGL builds.
/// This authoring pass is intentionally idempotent so it can also validate the
/// checked-in project without rewriting current assets.
/// </summary>
public static class BloodrootBetaSpawnAndWebGLSetup
{
    private const string FarmScenePath =
        "Assets/Scenes/Campaign/Farm_PrologueHub.unity";
    private const string OpenWorldScenePath =
        "Assets/Scenes/OpenWorld/Bloodroot_OpenWorld.unity";
    private const string MainMenuScenePath =
        "Assets/Scenes/Alpha/MainMenu.unity";
    private const string SharedUiPrefabPath =
        "Assets/PreFabs/UI/UI.prefab";
    private const string WaveManagerPrefabPath =
        "Assets/PreFabs/Level Stuff/SpawnNewLevel/WaveManager.prefab";
    private const string ScreecherPrefabPath =
        "Assets/PreFabs/Enemies/Screecher.prefab";
    private const string RootBoarPrefabPath =
        "Assets/PreFabs/Enemies/BoarRoot.prefab";

    private const string SafetyRootName = "__BR_FARM_SPAWN_SAFETY_V1";
    private const string ContainmentName = "FARM_FENCE_SPAWN_CONTAINMENT";
    private static readonly Vector3 ContainmentCenter =
        new Vector3(60.5f, 3f, -18.5f);
    private static readonly Vector3 ContainmentSize =
        new Vector3(59f, 12f, 91f);

    private static readonly string[] EmergencePointNames =
    {
        "EMERGENCE_ZONE_01",
        "EMERGENCE_ZONE_02",
        "EMERGENCE_ZONE_03"
    };

    [MenuItem("Tools/Bloodroot/Beta/Apply Spawn Safety And WebGL Quit Guard")]
    public static void ApplyMenu()
    {
        ApplyInternal();
    }

    public static void ApplyBatch()
    {
        ApplyInternal();
    }

    private static void ApplyInternal()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        if (TryValidateCurrentProject(out string currentError))
        {
            Debug.Log(
                "BLOODROOT_BETA_SAFETY: PASS no_changes=1 " +
                "farm_containment_assignments=4 screecher_refs=0 " +
                "webgl_quit_guards=2");
            return;
        }

        Debug.Log("Bloodroot beta safety outputs require authoring: " + currentError);

        GameObject screecher = LoadRequiredPrefab(ScreecherPrefabPath);
        GameObject rootBoar = LoadRequiredPrefab(RootBoarPrefabPath);

        ReplacePrefabSpawnerReferences(
            WaveManagerPrefabPath,
            screecher,
            rootBoar);
        ConfigureSharedUiPrefab();
        ConfigureFarmScene();
        ConfigureMainMenuScene();
        ConfigureOpenWorldScene(screecher, rootBoar);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        if (!TryValidateCurrentProject(out string error))
            throw new InvalidOperationException(
                "Bloodroot beta spawn/WebGL setup failed validation: " + error);

        Debug.Log(
            "BLOODROOT_BETA_SAFETY: PASS no_changes=0 " +
            "farm_containment_assignments=4 screecher_refs=0 " +
            "webgl_quit_guards=2");
    }

    private static void ConfigureFarmScene()
    {
        Scene scene = EditorSceneManager.OpenScene(
            FarmScenePath,
            OpenSceneMode.Single);

        Transform safetyRoot = FindTransforms(scene, SafetyRootName).SingleOrDefault();
        if (safetyRoot == null)
        {
            safetyRoot = new GameObject(SafetyRootName).transform;
            SceneManager.MoveGameObjectToScene(safetyRoot.gameObject, scene);
        }

        safetyRoot.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        safetyRoot.localScale = Vector3.one;

        Transform containmentTransform = safetyRoot.Find(ContainmentName);
        if (containmentTransform == null)
        {
            containmentTransform = new GameObject(ContainmentName).transform;
            containmentTransform.SetParent(safetyRoot, false);
        }

        containmentTransform.SetPositionAndRotation(
            ContainmentCenter,
            Quaternion.identity);
        containmentTransform.localScale = Vector3.one;

        BoxCollider containment =
            GetOrAddSingleComponent<BoxCollider>(containmentTransform.gameObject);
        containment.center = Vector3.zero;
        containment.size = ContainmentSize;
        containment.isTrigger = true;
        // This collider is serialized boundary geometry, not a physics volume.
        // Keeping it disabled avoids blocking trap clearance checks throughout
        // the farmyard while runtime containment still uses its size/transform.
        containment.enabled = false;

        int borderLayer = LayerMask.NameToLayer("Border");
        if (borderLayer >= 0)
            containmentTransform.gameObject.layer = borderLayer;

        NavMeshModifier navMeshModifier =
            GetOrAddSingleComponent<NavMeshModifier>(
                containmentTransform.gameObject);
        navMeshModifier.ignoreFromBuild = true;

        Transform[] emergencePoints = EmergencePointNames
            .Select(name => FindTransforms(scene, name).Single())
            .ToArray();

        MoveWorldXZ(emergencePoints[0], 45f, 25f);
        MoveWorldXZ(emergencePoints[1], 33f, -28.3f);

        MobSpawner mobSpawner = FindSceneComponents<MobSpawner>(scene).Single();
        mobSpawner.transform.position = new Vector3(
            55f,
            mobSpawner.transform.position.y,
            -20f);
        mobSpawner.ConfigureSpawnContainment(containment);
        EditorUtility.SetDirty(mobSpawner);

        InfestationSpawner infestation =
            FindSceneComponents<InfestationSpawner>(scene).Single();
        infestation.ConfigureSpawnContainment(containment, emergencePoints);
        EditorUtility.SetDirty(infestation);

        FarmRecurringEmergenceDirector recurring =
            FindSceneComponents<FarmRecurringEmergenceDirector>(scene).Single();
        recurring.ConfigureSpawnContainment(containment);
        EditorUtility.SetDirty(recurring);

        TreeSpawner[] treeSpawners =
            FindSceneComponents<TreeSpawner>(scene).ToArray();
        if (treeSpawners.Length != 1)
        {
            throw new InvalidOperationException(
                "Farm must contain exactly one authored TreeSpawner.");
        }

        treeSpawners[0].ConfigureSpawnContainment(containment);
        EditorUtility.SetDirty(treeSpawners[0]);
        EditorUtility.SetDirty(safetyRoot.gameObject);
        EditorUtility.SetDirty(containmentTransform.gameObject);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, FarmScenePath))
            throw new InvalidOperationException("Could not save the Farm scene.");
    }

    private static void ConfigureMainMenuScene()
    {
        Scene scene = EditorSceneManager.OpenScene(
            MainMenuScenePath,
            OpenSceneMode.Single);
        Transform exitButton = FindTransforms(scene, "Exit Button").Single();
        Selectable selectable = exitButton.GetComponent<Selectable>();
        if (selectable == null)
        {
            throw new InvalidOperationException(
                "The authored Main Menu Exit Button has no Selectable.");
        }

        ConfigureQuitGuard(selectable);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, MainMenuScenePath))
            throw new InvalidOperationException("Could not save the Main Menu scene.");
    }

    private static void ConfigureOpenWorldScene(
        GameObject screecher,
        GameObject replacement)
    {
        Scene scene = EditorSceneManager.OpenScene(
            OpenWorldScenePath,
            OpenSceneMode.Single);

        int replaced = ReplaceSceneSpawnerReferences(
            scene,
            screecher,
            replacement);
        RenameScreecherSpawnMarkers(scene);

        if (replaced > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, OpenWorldScenePath))
            {
                throw new InvalidOperationException(
                    "Could not save the Open World scene.");
            }
        }
        else if (scene.isDirty)
        {
            if (!EditorSceneManager.SaveScene(scene, OpenWorldScenePath))
                throw new InvalidOperationException(
                    "Could not save renamed Open World spawn markers.");
        }
    }

    private static void ConfigureSharedUiPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(SharedUiPrefabPath);
        try
        {
            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            Button quitButton = buttons.Single(button =>
                HasPersistentHandler(button, typeof(buttonFunctions), "quit"));
            ConfigureQuitGuard(quitButton);

            if (PrefabUtility.SaveAsPrefabAsset(root, SharedUiPrefabPath) == null)
            {
                throw new InvalidOperationException(
                    "Could not save the shared UI prefab.");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ReplacePrefabSpawnerReferences(
        string prefabPath,
        GameObject source,
        GameObject replacement)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            int replaced = 0;
            foreach (MonoBehaviour behaviour in
                     root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (IsSpawner(behaviour))
                    replaced += ReplaceObjectReferences(
                        behaviour,
                        source,
                        replacement);
            }

            if (replaced > 0 &&
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath) == null)
            {
                throw new InvalidOperationException(
                    "Could not save spawner prefab " + prefabPath + ".");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static int ReplaceSceneSpawnerReferences(
        Scene scene,
        GameObject source,
        GameObject replacement)
    {
        int replaced = 0;
        foreach (MonoBehaviour behaviour in FindSceneComponents<MonoBehaviour>(scene))
        {
            if (IsSpawner(behaviour))
                replaced += ReplaceObjectReferences(
                    behaviour,
                    source,
                    replacement);
        }

        return replaced;
    }

    private static int ReplaceObjectReferences(
        MonoBehaviour behaviour,
        UnityEngine.Object source,
        UnityEngine.Object replacement)
    {
        if (behaviour == null)
            return 0;

        SerializedObject data = new SerializedObject(behaviour);
        SerializedProperty property = data.GetIterator();
        int replaced = 0;
        while (property.NextVisible(true))
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference ||
                property.objectReferenceValue != source)
            {
                continue;
            }

            property.objectReferenceValue = replacement;
            replaced++;
        }

        if (replaced > 0)
        {
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(behaviour);
        }

        return replaced;
    }

    private static void RenameScreecherSpawnMarkers(Scene scene)
    {
        foreach (Transform transform in FindSceneComponents<Transform>(scene))
        {
            if (transform.name.IndexOf(
                    "Screecher",
                    StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            transform.name = ReplaceOrdinalIgnoreCase(
                transform.name,
                "Screecher",
                "RootBoar");
            EditorUtility.SetDirty(transform.gameObject);
        }
    }

    private static bool TryValidateCurrentProject(out string error)
    {
        try
        {
            GameObject screecher = LoadRequiredPrefab(ScreecherPrefabPath);
            ValidateFarmScene();
            ValidateMainMenuScene();
            ValidateSharedUiPrefab();

            if (CountPrefabSpawnerReferences(
                    WaveManagerPrefabPath,
                    screecher) != 0)
            {
                throw new InvalidOperationException(
                    "WaveManager prefab still contains a Screecher spawner reference.");
            }

            Scene openWorld = EditorSceneManager.OpenScene(
                OpenWorldScenePath,
                OpenSceneMode.Single);
            if (CountSceneSpawnerReferences(openWorld, screecher) != 0)
            {
                throw new InvalidOperationException(
                    "Open World still contains a Screecher spawner reference.");
            }

            if (FindSceneComponents<Transform>(openWorld).Any(transform =>
                    transform.name.IndexOf(
                        "Screecher",
                        StringComparison.OrdinalIgnoreCase) >= 0))
            {
                throw new InvalidOperationException(
                    "Open World still contains a Screecher spawn marker.");
            }

            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static void ValidateFarmScene()
    {
        Scene scene = EditorSceneManager.OpenScene(
            FarmScenePath,
            OpenSceneMode.Single);
        Transform safetyRoot = FindTransforms(scene, SafetyRootName).Single();
        Transform containmentTransform = safetyRoot.Find(ContainmentName);
        if (containmentTransform == null)
            throw new InvalidOperationException("Farm spawn containment is missing.");

        BoxCollider containment = containmentTransform.GetComponent<BoxCollider>();
        NavMeshModifier modifier =
            containmentTransform.GetComponent<NavMeshModifier>();
        if (containment == null || containment.enabled || !containment.isTrigger ||
            containment.size != ContainmentSize ||
            containmentTransform.position != ContainmentCenter ||
            modifier == null || !modifier.ignoreFromBuild)
        {
            throw new InvalidOperationException(
                "Farm spawn containment is not authored to the required bounds.");
        }

        Transform[] emergencePoints = EmergencePointNames
            .Select(name => FindTransforms(scene, name).Single())
            .ToArray();
        foreach (Transform point in emergencePoints)
            ValidateSpawnVolumeInside(point, containment);

        MobSpawner mobSpawner = FindSceneComponents<MobSpawner>(scene).Single();
        InfestationSpawner infestation =
            FindSceneComponents<InfestationSpawner>(scene).Single();
        FarmRecurringEmergenceDirector recurring =
            FindSceneComponents<FarmRecurringEmergenceDirector>(scene).Single();
        TreeSpawner treeSpawner =
            FindSceneComponents<TreeSpawner>(scene).Single();

        if (mobSpawner.SpawnContainment != containment ||
            infestation.SpawnContainment != containment ||
            recurring.SpawnContainment != containment ||
            treeSpawner.SpawnContainment != containment)
        {
            throw new InvalidOperationException(
                "Every Farm spawning system must share the fence containment.");
        }

        if (!IsInside(containment, mobSpawner.transform.position) ||
            !IsInside(containment, infestation.transform.position) ||
            !IsInside(containment, treeSpawner.transform.position))
        {
            throw new InvalidOperationException(
                "A Farm spawner root remains outside the contained farmyard.");
        }

        if (infestation.spawnPoints == null ||
            infestation.spawnPoints.Length != emergencePoints.Length ||
            !infestation.spawnPoints.SequenceEqual(emergencePoints))
        {
            throw new InvalidOperationException(
                "InfestationSpawner must use only the contained emergence zones.");
        }

        if (treeSpawner.spawnPoint == null ||
            treeSpawner.spawnPoint.Any(point =>
                point == null || !IsInside(containment, point.position)))
        {
            throw new InvalidOperationException(
                "A TreeSpawner point remains outside the farmyard.");
        }
    }

    private static void ValidateMainMenuScene()
    {
        Scene scene = EditorSceneManager.OpenScene(
            MainMenuScenePath,
            OpenSceneMode.Single);
        Selectable exitButton = FindTransforms(scene, "Exit Button")
            .Single()
            .GetComponent<Selectable>();
        ValidateQuitGuard(exitButton, "Main Menu Exit Button");
    }

    private static void ValidateSharedUiPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(SharedUiPrefabPath);
        try
        {
            Button quitButton = root.GetComponentsInChildren<Button>(true)
                .Single(button =>
                    HasPersistentHandler(button, typeof(buttonFunctions), "quit"));
            ValidateQuitGuard(quitButton, "Pause Menu Quit button");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureQuitGuard(Selectable quitControl)
    {
        WebGLQuitButtonGuard guard =
            GetOrAddSingleComponent<WebGLQuitButtonGuard>(quitControl.gameObject);
        guard.Configure(quitControl);
        EditorUtility.SetDirty(guard);
    }

    private static void ValidateQuitGuard(Selectable quitControl, string label)
    {
        if (quitControl == null)
            throw new InvalidOperationException(label + " is missing.");

        WebGLQuitButtonGuard[] guards =
            quitControl.GetComponents<WebGLQuitButtonGuard>();
        if (guards.Length != 1)
        {
            throw new InvalidOperationException(
                label + " must have exactly one WebGL quit guard.");
        }

        if (!guards[0].ValidateConfiguration(out string guardError))
        {
            throw new InvalidOperationException(
                label + " WebGL guard is invalid: " + guardError);
        }
    }

    private static int CountPrefabSpawnerReferences(
        string prefabPath,
        UnityEngine.Object target)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            return root.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(IsSpawner)
                .Sum(component => CountObjectReferences(component, target));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static int CountSceneSpawnerReferences(
        Scene scene,
        UnityEngine.Object target)
    {
        return FindSceneComponents<MonoBehaviour>(scene)
            .Where(IsSpawner)
            .Sum(component => CountObjectReferences(component, target));
    }

    private static int CountObjectReferences(
        MonoBehaviour behaviour,
        UnityEngine.Object target)
    {
        if (behaviour == null)
            return 0;

        SerializedObject data = new SerializedObject(behaviour);
        SerializedProperty property = data.GetIterator();
        int count = 0;
        while (property.NextVisible(true))
        {
            if (property.propertyType == SerializedPropertyType.ObjectReference &&
                property.objectReferenceValue == target)
            {
                count++;
            }
        }

        return count;
    }

    private static bool IsSpawner(MonoBehaviour behaviour)
    {
        return behaviour != null &&
               behaviour.GetType().Name.IndexOf(
                   "Spawn",
                   StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool HasPersistentHandler(
        Button button,
        Type targetType,
        string methodName)
    {
        for (int index = 0; index < button.onClick.GetPersistentEventCount(); index++)
        {
            UnityEngine.Object target = button.onClick.GetPersistentTarget(index);
            if (target != null && targetType.IsInstanceOfType(target) &&
                string.Equals(
                    button.onClick.GetPersistentMethodName(index),
                    methodName,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void ValidateSpawnVolumeInside(
        Transform point,
        BoxCollider containment)
    {
        BoxCollider volume = point.GetComponent<BoxCollider>();
        if (volume == null)
        {
            if (!IsInside(containment, point.position))
                throw new InvalidOperationException(
                    "Farm spawn point is outside containment: " + point.name);
            return;
        }

        // Collider.bounds is empty for disabled colliders. Validate serialized
        // geometry directly, including rotation and scale, without physics.
        Vector3 halfSize = volume.size * 0.5f;
        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
        {
            Vector3 corner = volume.transform.TransformPoint(
                volume.center + Vector3.Scale(
                    halfSize,
                    new Vector3(x, y, z)));
            if (!IsInside(containment, corner))
            {
                throw new InvalidOperationException(
                    "Farm spawn volume crosses the fence containment: " + point.name);
            }
        }
    }

    private static bool IsInside(BoxCollider containment, Vector3 worldPosition)
    {
        Vector3 local = containment.transform.InverseTransformPoint(worldPosition) -
                        containment.center;
        Vector3 halfSize = containment.size * 0.5f;
        return Mathf.Abs(local.x) <= halfSize.x + 0.001f &&
               Mathf.Abs(local.z) <= halfSize.z + 0.001f;
    }

    private static GameObject LoadRequiredPrefab(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
            throw new InvalidOperationException("Required prefab is missing: " + path);
        return prefab;
    }

    private static IEnumerable<T> FindSceneComponents<T>(Scene scene)
        where T : Component
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true))
            .Where(component => component != null);
    }

    private static IEnumerable<Transform> FindTransforms(Scene scene, string name)
    {
        return FindSceneComponents<Transform>(scene)
            .Where(transform => string.Equals(
                transform.name,
                name,
                StringComparison.Ordinal));
    }

    private static T GetOrAddSingleComponent<T>(GameObject owner)
        where T : Component
    {
        T[] components = owner.GetComponents<T>();
        if (components.Length > 1)
            throw new InvalidOperationException(
                owner.name + " has duplicate " + typeof(T).Name + " components.");
        return components.Length == 1
            ? components[0]
            : owner.AddComponent<T>();
    }

    private static void MoveWorldXZ(Transform transform, float x, float z)
    {
        Vector3 position = transform.position;
        transform.position = new Vector3(x, position.y, z);
        EditorUtility.SetDirty(transform);
    }

    private static string ReplaceOrdinalIgnoreCase(
        string source,
        string oldValue,
        string newValue)
    {
        int index = source.IndexOf(oldValue, StringComparison.OrdinalIgnoreCase);
        return index < 0
            ? source
            : source.Substring(0, index) + newValue +
              source.Substring(index + oldValue.Length);
    }
}
#endif
