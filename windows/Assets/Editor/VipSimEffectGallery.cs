using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VipSim.EditorTools
{
    /// <summary>
    /// Renders every effect on its own through the real scene and writes the result to disk.
    ///
    /// VipSimAlphaTest checks one property of each shader in isolation. This checks what a
    /// user sees: the effect components on the LeftEye camera, with the parameters stored in
    /// VIP_SIM.unity, drawing over a known test image that stands in for a captured window.
    /// The test image covers the middle of the frame and leaves a transparent margin, so an
    /// effect that turns the area around a window opaque shows up as a measured value.
    ///
    /// Three passes:
    ///   default_*   each effect component enabled alone, as the row's Enable button does;
    ///   sev050_*    each profile filter at severity 0.5 through ProfileBinder.Apply;
    ///   sev100_*    the same at severity 1.0.
    /// Each is followed by a render with every effect switched off again (*__off), which has
    /// to match the baseline; a difference there is state the effect left behind.
    ///
    /// Images are the raw RGBA frame. gallery.csv lists, per image, which components were
    /// enabled, the camera's local rotation and position, and any errors logged while it
    /// rendered; gallery.json gives the frame size and the test image's rectangle.
    /// tools/effect_gallery.py composites them as the desktop would show them and measures
    /// each one against the baseline.
    ///
    /// Run (NOT with -nographics; this needs a real device):
    ///   Unity -batchmode -projectPath windows -executeMethod VipSim.EditorTools.VipSimEffectGallery.Run
    ///         -galleryOut C:\path\to\out -logFile C:\path\to\gallery.log [-galleryOnly regex]
    ///         [-gallerySeverities 0.25,0.5,0.75,1] [-galleryScreenCm 53.1] [-galleryDistanceCm 60]
    /// -galleryOnly keeps the jobs whose name matches (case-insensitive), e.g. "pixel|foveal".
    /// -galleryScreenCm and -galleryDistanceCm set the viewing geometry; the defaults are a
    /// 24-inch 16:9 screen at 60 cm, independent of the monitor attached.
    /// Entering play mode reloads the domain, so progress is resumed from SessionState.
    /// </summary>
    [InitializeOnLoad]
    public static class VipSimEffectGallery
    {
        private const string ScenePath = "Assets/Scenes/VIP_SIM.unity";
        private const string KeyRunning = "VipSimEffectGallery.Running";
        private const string KeyOut = "VipSimEffectGallery.Out";
        private const int SettleFrames = 12;
        private const double TimeoutSeconds = 900;
        private static readonly float[] Severities = { 0.5f, 1f };

        private sealed class Job
        {
            public string Name;
            public Action Apply;
        }

        private static List<Job> _jobs;
        private static int _jobIndex = -1;
        private static int _renderAtFrame;
        private static double _startedAt;
        private static string _out;
        private static SettingsManager _sm;
        private static Camera _cam;
        private static MonoBehaviour[] _effects;
        private static int _w, _h, _uiLayer;
        private static RectInt _stim;
        private static StringBuilder _csv;
        private static readonly List<string> _errors = new List<string>();

        static VipSimEffectGallery()
        {
            if (SessionState.GetBool(KeyRunning, false))
                EditorApplication.update += Tick;
        }

        public static void Run()
        {
            string dir = Arg("-galleryOut") ?? Path.Combine(Path.GetTempPath(), "VIP-Sim-EffectGallery");
            Directory.CreateDirectory(dir);
            SessionState.SetString(KeyOut, Path.GetFullPath(dir));
            SessionState.SetBool(KeyRunning, true);

            EditorSceneManager.OpenScene(ScenePath);

            // Ask for a 1080p Game view. Several effects scale with Screen.width, and the
            // default batch-mode frame is 640x480. Setup logs the size actually used.
            try
            {
                typeof(EditorWindow).Assembly.GetType("UnityEditor.PlayModeWindow")
                    ?.GetMethod("SetCustomRenderingResolution", BindingFlags.Public | BindingFlags.Static)
                    ?.Invoke(null, new object[] { 1920u, 1080u, "VIP-Sim effect gallery" });
            }
            catch (Exception e)
            {
                Debug.Log("GALLERY could not set the Game view size: " + e.Message);
            }

            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            if (_startedAt == 0) _startedAt = EditorApplication.timeSinceStartup;
            if (EditorApplication.timeSinceStartup - _startedAt > TimeoutSeconds)
            {
                Finish(1, $"GALLERY TIMEOUT after {TimeoutSeconds} s at job {_jobIndex}");
                return;
            }
            if (!EditorApplication.isPlaying || Time.frameCount < 30) return;

            try
            {
                if (_jobs == null)
                {
                    Setup();
                    Next();
                    return;
                }
                if (Time.frameCount < _renderAtFrame) return;

                Capture(_jobs[_jobIndex].Name);
                Next();
            }
            catch (Exception e)
            {
                Finish(1, "GALLERY ERROR " + e);
            }
        }

        private static void Setup()
        {
            _out = SessionState.GetString(KeyOut, Path.GetTempPath());
            Application.logMessageReceived += OnLog;

            _sm = UnityEngine.Object.FindFirstObjectByType<SettingsManager>();
            var gaze = UnityEngine.Object.FindFirstObjectByType<GazeTracker>();
            if (_sm == null || gaze == null) throw new Exception("SettingsManager or GazeTracker not found in the scene");

            // Gaze fixed at the centre, so gaze-following effects are comparable across runs.
            gaze.gazeSource = GazeTracker.GazeSource.None;

            _cam = _sm.myBlur.GetComponent<Camera>();
            _effects = _cam.GetComponents<MonoBehaviour>();
            _uiLayer = LayerMask.NameToLayer("UI");
            _w = Screen.width;
            _h = Screen.height;
            Debug.Log($"GALLERY screen {_w}x{_h}, camera {_cam.name} ortho={_cam.orthographic}, " +
                      $"clear={_cam.clearFlags} {_cam.backgroundColor}, {_effects.Length} effect components");

            PlaceStimulus();

            _csv = new StringBuilder("image,enabled,camera_euler,camera_position,errors\n");
            _jobs = new List<Job> { new Job { Name = "00_baseline", Apply = DisableAll } };
            var effectJobs = new List<Job>();

            foreach (var fx in _effects)
            {
                var target = fx;
                effectJobs.Add(new Job
                {
                    Name = "default_" + target.GetType().Name,
                    Apply = () => { DisableAll(); target.enabled = true; },
                });
            }

            // -gallerySeverities 0.25,0.5 replaces the default severities.
            float[] severities = Severities;
            string sevArg = Arg("-gallerySeverities");
            if (!string.IsNullOrEmpty(sevArg))
                severities = Array.ConvertAll(sevArg.Split(','),
                    v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture));

            // Degrees become pixels through the monitor's size and the saved viewing distance.
            // Both are fixed here so a run renders the same frames on every machine: by default
            // a 24-inch 16:9 screen (53.1 cm wide) at 60 cm. The monitor's own value is logged.
            Debug.Log($"GALLERY monitor {VisSim.ViewingGeometry.MonitorName}: " +
                      $"{VisSim.ViewingGeometry.MonitorWidthCm:0.0} cm wide (0 = not reported)");
            float screenCm = FloatArg("-galleryScreenCm", VisSim.ViewingGeometry.AssumedWidthCm);
            float distanceCm = FloatArg("-galleryDistanceCm", VisSim.ViewingGeometry.DefaultDistanceCm);
            VisSim.ViewingGeometry.SetFixed(screenCm, distanceCm);
            Debug.Log($"GALLERY geometry {screenCm} cm wide at {distanceCm} cm: " +
                      $"{VisSim.ViewingGeometry.PixelsPerDegree:0.00} px per degree at {Screen.width} px");

            foreach (float sev in severities)
            {
                foreach (string filter in ProfileFilters())
                {
                    string f = filter;
                    float s = sev;
                    effectJobs.Add(new Job
                    {
                        Name = $"sev{Mathf.RoundToInt(s * 100):000}_{f}",
                        Apply = () => ApplyProfile(f, s),
                    });
                }
            }

            // -galleryOnly <regex> keeps the matching jobs, to re-check a few effects quickly.
            string only = Arg("-galleryOnly");
            if (!string.IsNullOrEmpty(only))
                effectJobs.RemoveAll(j => !System.Text.RegularExpressions.Regex.IsMatch(
                    j.Name, only, System.Text.RegularExpressions.RegexOptions.IgnoreCase));

            // Every effect is followed by a render with everything switched off again. It
            // has to match the baseline: an effect that leaves state behind (a moved camera,
            // a changed global) shows up here, under the name of the effect that caused it.
            foreach (var job in effectJobs)
            {
                _jobs.Add(job);
                _jobs.Add(new Job { Name = job.Name + "__off", Apply = DisableAll });
            }
        }

        private static void Next()
        {
            _jobIndex++;
            if (_jobIndex >= _jobs.Count)
            {
                File.WriteAllText(Path.Combine(_out, "gallery.csv"), _csv.ToString());
                Finish(0, $"GALLERY DONE {_jobs.Count} images in {_out}");
                return;
            }
            _errors.Clear();
            _jobs[_jobIndex].Apply();
            _renderAtFrame = Time.frameCount + SettleFrames;
        }

        private static void DisableAll()
        {
            foreach (var fx in _effects) fx.enabled = false;
        }

        private static IEnumerable<string> ProfileFilters()
        {
            var table = typeof(ProfileBinder).GetField("Table", BindingFlags.NonPublic | BindingFlags.Static)
                ?.GetValue(null) as IEnumerable;
            if (table == null) throw new Exception("ProfileBinder.Table not found");
            foreach (var bind in table)
                yield return (string)bind.GetType().GetField("Filter").GetValue(bind);
        }

        private static void ApplyProfile(string filter, float severity)
        {
            var root = new JObject
            {
                ["id"] = "effect-gallery",
                ["filters"] = new JArray { new JObject { ["filter"] = filter, ["severity"] = severity } },
            };
            var profile = ConditionProfile.Parse(root, out string error);
            if (profile == null) throw new Exception($"profile for {filter}: {error}");
            var report = ProfileBinder.Apply(_sm, profile);
            if (report.UnknownFilters.Count > 0)
                _errors.Add("unknown: " + string.Join("; ", report.UnknownFilters));
        }

        /// A quad in front of the camera, covering the middle of the frame, textured with a
        /// test image. It takes the place of a captured window, at the depth VIP-Sim lays
        /// captured windows out at (WindowManager), so an effect that moves the camera meets
        /// the same near and far planes a real window does.
        private static void PlaceStimulus()
        {
            int layer = 0;
            for (int i = 0; i < 32; i++)
                if ((_cam.cullingMask & (1 << i)) != 0 && i != _uiLayer) { layer = i; break; }

            // 80% x 76% of the frame, centred: the margin stays transparent.
            const float fx = 0.8f, fy = 0.76f;
            int sw = Mathf.RoundToInt(_w * fx), sh = Mathf.RoundToInt(_h * fy);
            _stim = new RectInt((_w - sw) / 2, (_h - sh) / 2, sw, sh);

            float d = Mathf.Lerp(_cam.nearClipPlane, _cam.farClipPlane, 0.5f);
            foreach (var root in _cam.gameObject.scene.GetRootGameObjects())
                if (root.name == "WindowManager")
                    d = Vector3.Dot(root.transform.position - _cam.transform.position, _cam.transform.forward);
            float frustumH = _cam.orthographic
                ? 2f * _cam.orthographicSize
                : 2f * d * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float frustumW = frustumH * _w / _h;

            // Texel for pixel, so the test image is not resampled on its way to the frame.
            var mat = new Material(Shader.Find("Unlit/Texture")) { mainTexture = TestImage(sw, sh) };
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "EffectGalleryStimulus";
            UnityEngine.Object.Destroy(quad.GetComponent<Collider>());
            quad.layer = layer;
            quad.GetComponent<MeshRenderer>().sharedMaterial = mat;
            quad.transform.SetPositionAndRotation(_cam.transform.position + _cam.transform.forward * d,
                                                  _cam.transform.rotation);
            quad.transform.localScale = new Vector3(frustumW * sw / _w, frustumH * sh / _h, 1f);

            File.WriteAllText(Path.Combine(_out, "gallery.json"), string.Format(CultureInfo.InvariantCulture,
                "{{\"width\": {0}, \"height\": {1}, \"stimulus\": [{2}, {3}, {4}, {5}], \"depth\": {6}}}",
                _w, _h, _stim.x, _stim.y, _stim.width, _stim.height, d));
        }

        private static void Capture(string name)
        {
            var px = Render();
            var png = new Texture2D(_w, _h, TextureFormat.RGBA32, false);
            png.SetPixels32(px);
            File.WriteAllBytes(Path.Combine(_out, name + ".png"), png.EncodeToPNG());
            UnityEngine.Object.Destroy(png);

            string enabled = string.Join("+", _effects.Where(e => e.enabled).Select(e => e.GetType().Name));
            // Windows exception messages carry \r\n; a bare \r also ends a CSV row.
            string errs = string.Join(" | ", _errors.Distinct()).Replace(",", ";").Replace('\r', ' ').Replace('\n', ' ');
            var t = _cam.transform;
            _csv.AppendLine(string.Join(",", name, enabled,
                V(t.localEulerAngles), V(t.localPosition), errs));
            Debug.Log($"GALLERY {name} enabled={enabled} euler={V(t.localEulerAngles)}");
        }

        private static string V(Vector3 v) =>
            string.Format(CultureInfo.InvariantCulture, "{0:F3} {1:F3} {2:F3}", v.x, v.y, v.z);

        /// The camera's output without VIP-Sim's own interface, as VipSimExport renders it.
        private static Color32[] Render()
        {
            var rt = RenderTexture.GetTemporary(_w, _h, 24, RenderTextureFormat.ARGB32);
            var prevTarget = _cam.targetTexture;
            int prevMask = _cam.cullingMask;
            var prevActive = RenderTexture.active;
            try
            {
                if (_uiLayer >= 0) _cam.cullingMask &= ~(1 << _uiLayer);
                _cam.targetTexture = rt;
                _cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(_w, _h, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, _w, _h), 0, 0);
                tex.Apply();
                var px = tex.GetPixels32();
                UnityEngine.Object.Destroy(tex);
                return px;
            }
            finally
            {
                _cam.targetTexture = prevTarget;
                _cam.cullingMask = prevMask;
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        /// Grid, colour patches, square-wave gratings in both orientations (blur and its
        /// direction), concentric rings (distortion), a grey ramp and lines of text-like bars.
        private static Texture2D TestImage(int w, int h)
        {
            var px = new Color32[w * h];
            var paper = new Color32(240, 240, 240, 255);
            var ink = new Color32(20, 20, 20, 255);
            Color32[] patches =
            {
                new Color32(220, 40, 40, 255), new Color32(40, 170, 60, 255), new Color32(40, 80, 220, 255),
                new Color32(40, 200, 220, 255), new Color32(210, 50, 200, 255), new Color32(240, 220, 40, 255),
                new Color32(240, 140, 30, 255), new Color32(200, 160, 130, 255),
            };
            int cx = w / 2, cy = h / 2;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color32 c = paper;
                    int top = h - 1 - y; // author top-down
                    if (top < 90)
                    {
                        c = patches[Math.Min(patches.Length - 1, x * patches.Length / w)];
                    }
                    else if (top >= h - 50)
                    {
                        byte g = (byte)(255 * x / (w - 1));
                        c = new Color32(g, g, g, 255);
                    }
                    else if (x < 420)
                    {
                        // Gratings: periods 2, 4, 8, 16 px; vertical stripes above, horizontal below.
                        int band = (top - 90) / 72;
                        int period = 2 << (band % 4);
                        bool vertical = band < 4;
                        int coord = vertical ? x : top;
                        if (band < 8) c = (coord % period) < period / 2 ? ink : paper;
                    }
                    else if (x > w - 420)
                    {
                        // Text-like lines: bars of varying length on a 22 px leading.
                        int line = (top - 110) / 22, inLine = (top - 110) % 22;
                        int word = (x - (w - 400)) / 34;
                        bool gap = ((line * 7 + word * 13) % 5) == 0;
                        if (top >= 110 && inLine >= 6 && inLine < 16 && x > w - 400 && x < w - 20 && !gap &&
                            ((x - (w - 400)) % 34) < 28)
                            c = ink;
                    }
                    else
                    {
                        double r = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                        if (((int)(r / 12)) % 2 == 0 && r < 220) c = ink;
                        if (Math.Abs(x - cx) < 2 || Math.Abs(y - cy) < 2) c = patches[0];
                    }
                    if (x % 80 == 0 || y % 80 == 0)
                        c = new Color32((byte)(c.r * 3 / 5), (byte)(c.g * 3 / 5), (byte)(c.b * 3 / 5), 255);
                    px[y * w + x] = c;
                }
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        private static void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                _errors.Add(message.Length > 160 ? message.Substring(0, 160) : message);
        }

        private static void Finish(int code, string message)
        {
            Debug.Log(message);
            SessionState.EraseBool(KeyRunning);
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            EditorApplication.Exit(code);
        }

        private static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }

        private static float FloatArg(string name, float fallback)
        {
            string v = Arg(name);
            return string.IsNullOrEmpty(v) ? fallback : float.Parse(v, CultureInfo.InvariantCulture);
        }
    }
}
