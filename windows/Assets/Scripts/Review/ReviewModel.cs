using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

// A review: what a team looked at under which simulated symptoms, what it found, and the
// conditions needed to repeat it. Plain C# with no UnityEngine reference, so the model and
// the report writers run under EditMode tests and plain .NET alike.
namespace VipSim.Review
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum Impact { Observation, Minor, Major, Blocker }

    public sealed class ReviewSession
    {
        public const string SchemaId = "vipsim-review/1";

        [JsonProperty("schema")] public string Schema = SchemaId;
        [JsonProperty("product")] public string Product = "";
        [JsonProperty("reviewer")] public string Reviewer = "";
        [JsonProperty("started")] public DateTimeOffset Started;
        [JsonProperty("app_version")] public string AppVersion = "";
        [JsonProperty("findings")] public List<Finding> Findings = new List<Finding>();

        /// <summary>F01, F02, ... never reusing a number, so a deleted finding's files stay unambiguous.</summary>
        public string NextId()
        {
            int max = 0;
            foreach (var f in Findings)
                if (f.Id != null && f.Id.Length > 1 &&
                    int.TryParse(f.Id.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
                    max = Math.Max(max, n);
            return "F" + (max + 1).ToString("00", CultureInfo.InvariantCulture);
        }
    }

    public sealed class Finding
    {
        [JsonProperty("id")] public string Id = "";
        [JsonProperty("captured")] public DateTimeOffset Captured;
        [JsonProperty("note")] public string Note = "";
        [JsonProperty("impact")] public Impact Impact = Impact.Observation;
        [JsonProperty("criteria")] public List<string> Criteria = new List<string>();
        [JsonProperty("window_title", NullValueHandling = NullValueHandling.Ignore)] public string WindowTitle;
        [JsonProperty("viewing")] public ViewingConditions Viewing = new ViewingConditions();
        [JsonProperty("symptoms")] public List<Symptom> Symptoms = new List<Symptom>();
        [JsonProperty("simulated_image")] public string SimulatedImage = "";
        [JsonProperty("original_image", NullValueHandling = NullValueHandling.Ignore)] public string OriginalImage;
        [JsonProperty("profile", NullValueHandling = NullValueHandling.Ignore)] public string Profile;

        /// <summary>The note's first line, for titles and issue summaries.</summary>
        public string Summary(int max = 90)
        {
            string first = (Note ?? "").Trim();
            int nl = first.IndexOfAny(new[] { '\r', '\n' });
            if (nl >= 0) first = first.Substring(0, nl).Trim();
            if (first.Length == 0)
                return "Low-vision finding " + Id + " (" + SymptomList() + ")";
            return first.Length <= max ? first : first.Substring(0, max - 3).TrimEnd() + "...";
        }

        public string SymptomList()
        {
            if (Symptoms.Count == 0) return "no symptoms active";
            var names = new List<string>();
            foreach (var s in Symptoms) names.Add(s.Label);
            return string.Join(", ", names);
        }
    }

    /// <summary>What converts the simulation's degrees into pixels, and how gaze was placed.</summary>
    public sealed class ViewingConditions
    {
        [JsonProperty("distance_cm")] public double DistanceCm;
        [JsonProperty("screen_width_cm")] public double ScreenWidthCm;
        [JsonProperty("screen_width_source")] public string ScreenWidthSource = "";
        [JsonProperty("monitor", NullValueHandling = NullValueHandling.Ignore)] public string Monitor;
        [JsonProperty("pixels_across")] public int PixelsAcross;
        [JsonProperty("pixels_per_degree")] public double PixelsPerDegree;
        [JsonProperty("screen_span_deg")] public double ScreenSpanDeg;
        [JsonProperty("gaze")] public string Gaze = "";
        [JsonProperty("os")] public string Os = "";

        public string Describe()
        {
            var c = CultureInfo.InvariantCulture;
            return string.Format(c, "{0:0.#} cm from a {1:0.#} cm wide screen ({2}), {3} px across: " +
                                    "1 degree = {4:0.#} px at the centre, the screen spans {5:0.#} degrees",
                                 DistanceCm, ScreenWidthCm, ScreenWidthSource.ToLowerInvariant(),
                                 PixelsAcross, PixelsPerDegree, ScreenSpanDeg);
        }

        public bool SameAs(ViewingConditions o) =>
            o != null && Math.Abs(DistanceCm - o.DistanceCm) < 0.05 &&
            Math.Abs(ScreenWidthCm - o.ScreenWidthCm) < 0.05 && PixelsAcross == o.PixelsAcross;
    }

    public sealed class Symptom
    {
        [JsonProperty("filter")] public string Filter = "";
        [JsonProperty("label")] public string Label = "";
        [JsonProperty("term")] public string Term = "";
        [JsonProperty("parameters")] public List<Parameter> Parameters = new List<Parameter>();

        /// <summary>
        /// Symptoms from a profile as ProfileBinder.Capture writes it: a "filters" array whose
        /// entries carry "filter" and the effect's parameters in its own units.
        /// </summary>
        public static List<Symptom> FromProfile(JObject profile)
        {
            var list = new List<Symptom>();
            if (!(profile?["filters"] is JArray filters)) return list;

            foreach (var token in filters)
            {
                if (!(token is JObject o)) continue;
                string filter = (string)o["filter"] ?? "";
                var names = ReviewCatalog.SymptomNames(filter);
                var s = new Symptom { Filter = filter, Label = names.Label, Term = names.Term };
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var p in o.Properties())
                {
                    if (p.Name == "filter") continue;
                    // Capture writes the severity under the effect's field name as well as
                    // under the profile key ("overlay_scale" and "overlayScale"). One row each.
                    if (!seen.Add(p.Name.Replace("_", "").ToLowerInvariant())) continue;
                    s.Parameters.Add(new Parameter
                    {
                        Name = p.Name,
                        Value = Format(p.Value),
                        Unit = ReviewCatalog.UnitFor(p.Name),
                    });
                }
                list.Add(s);
            }
            return list;
        }

        private static string Format(JToken v)
        {
            switch (v.Type)
            {
                case JTokenType.Float:
                    return ((double)v).ToString("0.###", CultureInfo.InvariantCulture);
                case JTokenType.Integer:
                    return ((long)v).ToString(CultureInfo.InvariantCulture);
                case JTokenType.Boolean:
                    return (bool)v ? "on" : "off";
                default:
                    return v.ToString(Formatting.None).Trim('"');
            }
        }
    }

    public sealed class Parameter
    {
        [JsonProperty("name")] public string Name = "";
        [JsonProperty("value")] public string Value = "";

        /// <summary>Physical unit, or null for a value on the effect's own uncalibrated scale.</summary>
        [JsonProperty("unit")] public string Unit;

        /// <summary>A number, as opposed to a mode such as "horizontal" or "on".</summary>
        [JsonIgnore]
        public bool IsNumber => double.TryParse(Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
    }

    /// <summary>One accessibility requirement a finding can be filed against.</summary>
    public sealed class Criterion
    {
        public string Id;          // stable key stored in findings: "wcag-1.4.3", "en-4.2.2", "beyond"
        public string Name;        // "1.4.3 Contrast (Minimum)"
        public string Level;       // "AA", or "" where WCAG levels do not apply
        public string En301549;    // matching EN 301 549 V3.2.1 clauses, or "" where there are none
        public string Url;         // where the requirement is defined
    }
}
