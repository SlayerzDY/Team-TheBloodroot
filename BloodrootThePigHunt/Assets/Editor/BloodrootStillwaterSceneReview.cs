#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Read-only scene captures for reviewing Stillwater's authored assembly.</summary>
public static class BloodrootStillwaterSceneReview
{
    public static void CaptureBeforeBatch() => Capture("Before");
    public static void CaptureAfterBatch() => Capture("After");

    private static void Capture(string label)
    {
        var scene = EditorSceneManager.OpenScene(
            "Assets/Scenes/OpenWorld/Bloodroot_OpenWorld.unity",
            OpenSceneMode.Single);
        Physics.SyncTransforms();
        foreach (Transform assembly in scene.GetRootGameObjects()
                     .Select(item => item.transform)
                     .Where(item => item.name.Contains("Siltwater")))
        {
            Collider[] colliders = assembly.GetComponentsInChildren<Collider>(true);
            Debug.Log("STILLWATER_REVIEW " + assembly.name + " expanded_boxes=" +
                      colliders.OfType<BoxCollider>().Count() + " expanded_meshes=" +
                      colliders.OfType<MeshCollider>().Count());
            foreach (Collider collider in colliders.Where(item => !item.enabled ||
                         item.isTrigger || item.transform.parent.name.Contains("Moving")))
                Debug.Log("STILLWATER_COLLIDER " + collider.name + " enabled=" +
                          collider.enabled + " trigger=" + collider.isTrigger +
                          " layer=" + collider.gameObject.layer);
        }

        string directory = Path.GetFullPath(Path.Combine(
            Application.dataPath, "../../Logs/StillwaterReview/" + label));
        Directory.CreateDirectory(directory);
        var cameraObject = new GameObject("Stillwater temporary review camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.nearClipPlane = .1f;
        camera.farClipPlane = 300f;
        camera.fieldOfView = 56f;
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        try
        {
            CaptureView(camera, directory, "01_Facility",
                new Vector3(399f, 40f, -568f), new Vector3(466f, 15f, -510f));
            CaptureView(camera, directory, "02_GrainComplex",
                new Vector3(402f, 25f, -547f), new Vector3(446f, 18f, -514f));
            CaptureView(camera, directory, "03_Catwalk",
                new Vector3(418f, 18f, -480f), new Vector3(439f, 15f, -493f));
            CaptureView(camera, directory, "04_FeedMill",
                new Vector3(478f, 20f, -553f), new Vector3(505f, 11f, -527f));
            CaptureView(camera, directory, "05_SiloGroundContact",
                new Vector3(449f, 8f, -541f), new Vector3(450f, 12f, -516f));
            CaptureView(camera, directory, "06_QualityVaultEntrance",
                new Vector3(521.651f, 8.991051f, -531.7732f),
                new Vector3(526.651f, 8.841051f, -531.7732f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
        Debug.Log("STILLWATER_SCENE_REVIEW=PASS directory=" + directory);
    }

    private static void CaptureView(Camera camera, string directory, string name,
        Vector3 position, Vector3 target)
    {
        camera.transform.SetPositionAndRotation(position,
            Quaternion.LookRotation(target - position, Vector3.up));
        var texture = new RenderTexture(1600, 1000, 24, RenderTextureFormat.ARGB32);
        texture.Create();
        var pixels = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            var request = new UniversalRenderPipeline.SingleCameraRequest
            {
                destination = texture
            };
            // Warm the pipeline before the saved frame so the first capture
            // cannot retain transient shader-import fallback colors.
            RenderPipeline.SubmitRenderRequest(camera, request);
            RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture.active = texture;
            pixels.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(Path.Combine(directory, name + ".png"),
                pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(pixels);
            texture.Release();
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
#endif
