using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// An organisation's settings for VIP-Sim, read from policy.json. Plain C# with no UnityEngine
// reference, so the parsing runs under EditMode tests. VipSimPolicy applies it at start.
namespace VipSim.Policy
{
    public sealed class PolicyDocument
    {
        // The ranges ViewingGeometry accepts. Duplicated because this assembly cannot see it;
        // PolicyDocumentTests pins them.
        public const float MinDistanceCm = 20f, MaxDistanceCm = 300f;
        public const float MinWidthCm = 10f, MaxWidthCm = 500f;

        public bool? UpdateCheck;
        public bool? TelemetryAllowed;
        public bool? WebcamAllowed;
        public float? ViewingDistanceCm;
        public float? ScreenWidthCm;
        public string ReportFolder;
        public string Profile;

        /// <summary>Why a value was ignored. Logged; a bad entry never stops VIP-Sim starting.</summary>
        public readonly List<string> Warnings = new List<string>();

        public bool IsEmpty =>
            UpdateCheck == null && TelemetryAllowed == null && WebcamAllowed == null &&
            ViewingDistanceCm == null && ScreenWidthCm == null && ReportFolder == null && Profile == null;

        /// <summary>
        /// Parse a policy. Paths are expanded with <paramref name="getEnv"/> (%VAR%, $VAR,
        /// ${VAR}) and a leading ~ becomes <paramref name="home"/>. Invalid entries are
        /// dropped with a warning; the rest still apply.
        /// </summary>
        public static PolicyDocument Parse(string json, Func<string, string> getEnv, string home)
        {
            var doc = new PolicyDocument();
            JObject root;
            try
            {
                root = JObject.Parse(json ?? "");
            }
            catch (JsonException e)
            {
                doc.Warnings.Add("not valid JSON, so no policy applies: " + e.Message);
                return doc;
            }

            foreach (var p in root.Properties())
            {
                switch (p.Name)
                {
                    case "updateCheck": doc.UpdateCheck = Bool(doc, p); break;
                    case "telemetryAllowed": doc.TelemetryAllowed = Bool(doc, p); break;
                    case "webcamAllowed": doc.WebcamAllowed = Bool(doc, p); break;
                    case "viewingDistanceCm": doc.ViewingDistanceCm = Number(doc, p, MinDistanceCm, MaxDistanceCm); break;
                    case "screenWidthCm": doc.ScreenWidthCm = Number(doc, p, MinWidthCm, MaxWidthCm); break;
                    case "reportFolder": doc.ReportFolder = PathValue(doc, p, getEnv, home); break;
                    case "profile": doc.Profile = PathValue(doc, p, getEnv, home); break;
                    default: doc.Warnings.Add($"unknown setting \"{p.Name}\" ignored"); break;
                }
            }
            return doc;
        }

        private static bool? Bool(PolicyDocument doc, JProperty p)
        {
            if (p.Value.Type == JTokenType.Boolean) return (bool)p.Value;
            doc.Warnings.Add($"\"{p.Name}\" must be true or false; ignored");
            return null;
        }

        private static float? Number(PolicyDocument doc, JProperty p, float min, float max)
        {
            if (p.Value.Type == JTokenType.Integer || p.Value.Type == JTokenType.Float)
            {
                float v = (float)p.Value;
                if (v >= min && v <= max) return v;
                doc.Warnings.Add(string.Format(CultureInfo.InvariantCulture,
                    "\"{0}\" must be between {1} and {2} cm; {3} ignored", p.Name, min, max, v));
                return null;
            }
            doc.Warnings.Add($"\"{p.Name}\" must be a number of centimetres; ignored");
            return null;
        }

        private static string PathValue(PolicyDocument doc, JProperty p, Func<string, string> getEnv, string home)
        {
            if (p.Value.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string)p.Value))
                return Expand(((string)p.Value).Trim(), getEnv, home);
            doc.Warnings.Add($"\"{p.Name}\" must be a path; ignored");
            return null;
        }

        private static readonly Regex Variable =
            new Regex(@"%(?<w>[A-Za-z_][A-Za-z0-9_]*)%|\$\{(?<b>[A-Za-z_][A-Za-z0-9_]*)\}|\$(?<u>[A-Za-z_][A-Za-z0-9_]*)");

        /// <summary>Environment variables in both notations, and ~ at the start. Unknown variables stay as written.</summary>
        public static string Expand(string path, Func<string, string> getEnv, string home)
        {
            if (string.IsNullOrEmpty(path)) return path;
            if ((path == "~" || path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal))
                && !string.IsNullOrEmpty(home))
                path = home + path.Substring(1);

            return Variable.Replace(path, m =>
            {
                string name = m.Groups["w"].Success ? m.Groups["w"].Value
                            : m.Groups["b"].Success ? m.Groups["b"].Value
                            : m.Groups["u"].Value;
                string value = getEnv?.Invoke(name);
                return string.IsNullOrEmpty(value) ? m.Value : value;
            });
        }

        /// <summary>The settings in force, for the log: "updateCheck=false, viewingDistanceCm=60".</summary>
        public string Describe()
        {
            var parts = new List<string>();
            void Add(string name, object v)
            {
                if (v == null) return;
                parts.Add(name + "=" + (v is bool b ? (b ? "true" : "false")
                                       : v is float f ? f.ToString("0.##", CultureInfo.InvariantCulture)
                                       : v.ToString()));
            }
            Add("updateCheck", UpdateCheck);
            Add("telemetryAllowed", TelemetryAllowed);
            Add("webcamAllowed", WebcamAllowed);
            Add("viewingDistanceCm", ViewingDistanceCm);
            Add("screenWidthCm", ScreenWidthCm);
            Add("reportFolder", ReportFolder);
            Add("profile", Profile);
            return parts.Count == 0 ? "no settings" : string.Join(", ", parts);
        }
    }
}
