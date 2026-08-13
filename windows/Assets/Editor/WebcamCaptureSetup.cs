using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Mediapipe.Unity;

namespace VipSim.EditorTools
{
    /// <summary>
    /// Sets the webcam capture width that MediaPipe is fed.
    ///
    /// Measured problem: gaze updated at 5.0Hz on an RTX 5080 while the renderer
    /// ran at 30fps and the whole 16-effect shader chain cost 0.2ms. Gaze is gated
    /// on WebCamSource.didUpdateThisFrame, so the camera was the ceiling.
    ///
    /// Cause: the camera is a Logitech HD 1080p (C920 class). Those deliver 720p
    /// and 1080p at 30fps only via MJPEG; over uncompressed YUY2 — which is what
    /// Unity's WebCamTexture typically negotiates on Windows — USB bandwidth caps
    /// 1280x720 at around 5fps. UnitEye requests _preferableDefaultWidth = 1280,
    /// so the device fell back to roughly 5fps and every downstream stage was
    /// starved regardless of how fast the machine is.
    ///
    /// 640x480 fits comfortably in uncompressed USB bandwidth at 30fps on the same
    /// hardware, and costs nothing in accuracy: MediaPipe's face detector operates
    /// on roughly 192-256px input, so the extra width was being downscaled away
    /// before inference anyway.
    ///
    /// If gaze accuracy drops at typical seating distance, raise this — but
    /// re-measure the Hz with F10, because anything above 640 risks dropping back
    /// to the MJPEG-only rates on this class of camera.
    /// </summary>
    public static class WebcamCaptureSetup
    {
        private const string ScenePath = "Assets/Scenes/VIP_SIM.unity";
        private const int TargetWidth = 640;
        private const int TargetFps = 30;

        [MenuItem("VIP-Sim/Set webcam capture width (gaze throughput)")]
        public static void Setup()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var sources = Object.FindObjectsByType<WebCamSource>(FindObjectsInactive.Include);
            if (sources.Length == 0)
            {
                Debug.LogError("WEBCAM_SETUP_FAILED: no WebCamSource in the scene.");
                return;
            }

            int changed = 0;
            foreach (var src in sources)
            {
                var so = new SerializedObject(src);
                var width = so.FindProperty("_preferableDefaultWidth");
                var fps = so.FindProperty("_requestedFps");

                if (width == null)
                {
                    Debug.LogError("WEBCAM_SETUP_FAILED: _preferableDefaultWidth not found " +
                                   "(renamed upstream?).");
                    return;
                }

                bool dirty = false;
                if (width.intValue != TargetWidth)
                {
                    Debug.Log($"WEBCAM_SETUP: {src.name} width {width.intValue} -> {TargetWidth}");
                    width.intValue = TargetWidth;
                    dirty = true;
                }
                // 60 was requested; the device cannot supply it uncompressed at any
                // useful resolution, and asking for an unsupported mode is part of
                // what pushes the fallback to a slow one.
                if (fps != null && fps.intValue != TargetFps)
                {
                    Debug.Log($"WEBCAM_SETUP: {src.name} fps {fps.intValue} -> {TargetFps}");
                    fps.intValue = TargetFps;
                    dirty = true;
                }

                if (dirty)
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(src);
                    changed++;
                }
            }

            if (changed == 0)
            {
                Debug.Log("WEBCAM_SETUP_SKIPPED: already configured.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"WEBCAM_SETUP_OK: {changed} source(s) -> {TargetWidth}px @ {TargetFps}fps.");
        }
    }
}
