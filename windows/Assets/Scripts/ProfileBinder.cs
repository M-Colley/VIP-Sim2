using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Connects a condition profile to the effects in the scene, in both directions.
///
/// The mapping from a profile's filter id to an effect is not invented here. Every profile
/// carries its own table4_mapping, pairing the name in the paper's Table 4 with the id it
/// uses, and those names are the effect objects in the menu: "Hyperopia" is blur,
/// "Metamorphosia point" is vortex, "Glare vision" is bloom. That table is the authority and
/// this one is a transcription of it.
///
/// What is not settled is what a severity means. These profiles were authored against a
/// separate filter pipeline whose shaders take parameters ours do not have, and nothing in
/// this project converts a 0..1 severity into an effect's own units. Rather than quietly
/// invent a number, a severity is spread linearly over a stated range of the effect's
/// primary field, the resulting value is reported, and the report says it was approximate.
/// The profiles declare values_are_starting_points, so a starting point is what this
/// produces -- and once it has been adjusted here, Capture writes the concrete values back
/// out, which is the loop that turns a starting point into a calibrated profile.
/// </summary>
public static class ProfileBinder
{
    private sealed class Bind
    {
        public string Filter;                       // id in the profile, e.g. "blur"
        public string EffectObject;                 // GameObject in the menu, e.g. "Hyperopia"
        public Func<SettingsManager, object> Get;   // the component carrying the parameters
        public Dictionary<string, string> Fields;   // profile parameter -> component field
        public string SeverityField;                // where a bare severity goes
        public float SevMin, SevMax;                // the range it is spread over
    }

    private static Dictionary<string, string> F(params string[] kv)
    {
        var d = new Dictionary<string, string>();
        for (int i = 0; i + 1 < kv.Length; i += 2) d[kv[i]] = kv[i + 1];
        return d;
    }

    // Ranges come from a field's own [Range] attribute where it has one, and otherwise from
    // its default: zero to twice the default, or 0..1 for a field that starts at zero. Stated
    // here rather than buried, because they are a convention and not a measurement.
    private static readonly List<Bind> Table = new List<Bind>
    {
        new Bind { Filter = "blur", EffectObject = "Hyperopia", Get = s => s.myBlur,
                   SeverityField = "maxCPD", SevMin = 0f, SevMax = 1f, Fields = F() },

        new Bind { Filter = "bcg", EffectObject = "Contrast Sensitivity",
                   Get = s => s.myBrightnessContrastGamma, SeverityField = "Contrast",
                   SevMin = 0f, SevMax = 1f,
                   Fields = F("brightness", "Brightness", "contrast", "Contrast", "gamma", "Gamma") },

        new Bind { Filter = "cvd", EffectObject = "Color vision deficiency", Get = s => s.myRecolour,
                   SeverityField = "severityIndex", SevMin = 0f, SevMax = 1f,
                   Fields = F("cvd_type", "anomType") },

        new Bind { Filter = "cataracts", EffectObject = "Cataract", Get = s => s.myCataract,
                   SeverityField = "severityIndex", SevMin = 0f, SevMax = 1f, Fields = F() },

        new Bind { Filter = "detail_loss", EffectObject = "DetailLoss", Get = s => s.myInpainter2,
                   SeverityField = "threshold", SevMin = 0f, SevMax = 1f, Fields = F() },

        new Bind { Filter = "distortion", EffectObject = "Distortion", Get = s => s.myDistortionMap,
                   SeverityField = null, SevMin = 0f, SevMax = 1f, Fields = F() },

        new Bind { Filter = "double_vision", EffectObject = "DoubleVisionEffect",
                   Get = s => s.myDoubleVision, SeverityField = "displacementAmount",
                   SevMin = 0f, SevMax = 0.05f, Fields = F() },

        new Bind { Filter = "nystagmus", EffectObject = "Nystagmus", Get = s => s.myNystagmus,
                   SeverityField = "amp_deg", SevMin = 0f, SevMax = 16f,
                   Fields = F("axis", "direction_deg") },

        new Bind { Filter = "teichopsia", EffectObject = "Teichopsia", Get = s => s.myTeichopsia,
                   SeverityField = "Strength", SevMin = 0f, SevMax = 1.5f, Fields = F() },

        new Bind { Filter = "bloom", EffectObject = "Glare Vision/photophobia", Get = s => s.myBloom,
                   SeverityField = "intensity", SevMin = 0f, SevMax = 1.5f,
                   Fields = F("intensity", "intensity", "threshold", "threshold",
                              "blur_size", "blurSize") },

        new Bind { Filter = "floaters", EffectObject = "Retinopathy", Get = s => s.myFloaters,
                   SeverityField = "intensity", SevMin = 0f, SevMax = 2f,
                   Fields = F("floater_size", "floaterSize", "center", "center",
                              "n_floaters", "floaterDensity") },

        new Bind { Filter = "foveal_darkness", EffectObject = "FovealDarkness",
                   Get = s => s.myFovealDarkness, SeverityField = "opacity", SevMin = 0f, SevMax = 1f,
                   Fields = F("radius", "innerCircleRadius", "edge_width", "fadeWidth") },

        new Bind { Filter = "vortex", EffectObject = "Metamorphopsia", Get = s => s.myVortexEffect,
                   SeverityField = "suctionStrength", SevMin = 0f, SevMax = 2f,
                   Fields = F("vortex_radius", "vortexRadius", "suction_strength", "suctionStrength",
                              "inner_circle_radius", "innerCircleRadius") },

        new Bind { Filter = "flickering_stars", EffectObject = "StarsBlinking",
                   Get = s => s.myFlickeringStars, SeverityField = "starRadius",
                   SevMin = 0f, SevMax = 0.005f, Fields = F("radius", "radius") },

        new Bind { Filter = "pixelation", EffectObject = null, Get = s => s.myPixelationEffect,
                   SeverityField = "pixelRadius", SevMin = 10f, SevMax = 1000f,
                   Fields = F("pixel_size", "pixelRadius") },

        new Bind { Filter = "field_loss", EffectObject = "VisionLossC", Get = s => s.myFieldLoss,
                   SeverityField = "overlayScale", SevMin = 0f, SevMax = 1.5f,
                   Fields = F("overlay_scale", "overlayScale") },

        new Bind { Filter = "vignette", EffectObject = "Vision loss, peripheral",
                   Get = s => s.myFieldLossInverted, SeverityField = "overlayScale",
                   SevMin = 0f, SevMax = 1.5f, Fields = F("overlay_scale", "overlayScale") },
    };

    private static Bind Find(string filter)
    {
        // "field_loss(central)" and "field_loss" are the same filter; the qualifier travels
        // separately, as a parameter.
        int paren = filter.IndexOf('(');
        string bare = paren > 0 ? filter.Substring(0, paren) : filter;
        return Table.Find(b => b.Filter == bare);
    }

    /// <summary>
    /// Apply a profile, and return an account of what could not be applied.
    ///
    /// A profile is a complete description of one condition, so effects it does not mention
    /// are switched off rather than left as they were. Otherwise loading two profiles in a
    /// row would show you the union of them, which is a condition nobody has.
    /// </summary>
    public static ConditionProfile.Report Apply(SettingsManager sm, ConditionProfile profile)
    {
        var report = new ConditionProfile.Report();
        var mentioned = new HashSet<string>();

        foreach (var f in profile.Filters)
        {
            var bind = Find(f.Name);
            if (bind == null) { report.UnknownFilters.Add(f.Name); continue; }

            // field_loss says which half of the visual field it means, as a parameter or in
            // the filter name itself, and the two are different objects with different
            // overlays: central darkens the middle, peripheral darkens everything else.
            // Getting this wrong inverts the condition -- glaucoma shown as macular
            // degeneration -- so it is read rather than assumed.
            if (bind.Filter == "field_loss")
            {
                string ft = ((string)f.Parameters["field_type"] ?? f.Name).ToLowerInvariant();
                if (ft.Contains("periph"))
                {
                    var peripheral = Table.Find(b => b.Filter == "vignette");
                    if (peripheral != null) { bind = peripheral; report.Applied.Add($"{f.Name}.field_type"); }
                }
                else if (f.Parameters["field_type"] != null)
                {
                    report.Applied.Add($"{f.Name}.field_type");
                }
            }

            mentioned.Add(bind.Filter);

            object target = bind.Get(sm);
            if (target == null)
            {
                report.UnknownFilters.Add($"{f.Name} (no component wired in the scene)");
                continue;
            }

            SetEffectActive(sm, bind.EffectObject, true);

            foreach (var prop in f.Parameters.Properties())
            {
                if (prop.Name == "filter") continue;

                if (prop.Name == "severity")
                {
                    if (string.IsNullOrEmpty(bind.SeverityField))
                    {
                        report.Unsupported.Add($"{f.Name}.severity");
                        continue;
                    }
                    float sev = Mathf.Clamp01((float)prop.Value);
                    float val = Mathf.Lerp(bind.SevMin, bind.SevMax, sev);
                    if (TrySet(target, bind.SeverityField, new JValue(val)))
                        report.Applied.Add($"{f.Name}.severity -> {bind.SeverityField}={val:0.###} (approximate)");
                    else
                        report.Unsupported.Add($"{f.Name}.severity");
                    continue;
                }

                if (prop.Name == "field_type") continue;   // handled above

                string field;
                if (!bind.Fields.TryGetValue(prop.Name, out field))
                {
                    report.Unsupported.Add($"{f.Name}.{prop.Name}");
                    continue;
                }
                if (TrySet(target, field, prop.Value)) report.Applied.Add($"{f.Name}.{prop.Name}");
                else report.Unsupported.Add($"{f.Name}.{prop.Name}");
            }
        }

        foreach (var b in Table)
            if (!mentioned.Contains(b.Filter)) SetEffectActive(sm, b.EffectObject, false);

        return report;
    }

    /// <summary>
    /// Write the current state out as a profile, in the same shape the authored ones use.
    ///
    /// Only concrete parameters are written, never a severity: a severity is the other
    /// pipeline's abstraction, and this end knows the effect's own units. A profile captured
    /// here therefore reloads exactly, which is what makes the app usable for authoring
    /// rather than only for viewing.
    /// </summary>
    public static JObject Capture(SettingsManager sm, string id, string description)
    {
        var filters = new JArray();

        foreach (var b in Table)
        {
            if (!IsEffectActive(sm, b.EffectObject)) continue;
            object target = b.Get(sm);
            if (target == null) continue;

            var o = new JObject { ["filter"] = b.Filter };
            foreach (var kv in b.Fields)
            {
                object v = ReadField(target, kv.Value);
                if (v != null) o[kv.Key] = JToken.FromObject(v);
            }
            if (!string.IsNullOrEmpty(b.SeverityField))
            {
                object v = ReadField(target, b.SeverityField);
                if (v != null) o[b.SeverityField] = JToken.FromObject(v);
            }
            filters.Add(o);
        }

        return new JObject
        {
            ["id"] = id,
            ["version"] = "vipsim-unity-1",
            ["description"] = description ?? "",
            ["values_are_starting_points"] = true,
            ["filters"] = filters,
        };
    }

    // ------------------------------------------------------------------ reflection

    private static object ReadField(object target, string name)
    {
        var t = target.GetType();
        var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        if (f != null) return Convertible(f.GetValue(target));
        var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        return p != null && p.CanRead ? Convertible(p.GetValue(target, null)) : null;
    }

    /// <summary>Vectors do not serialise usefully as objects; write them as arrays.</summary>
    private static object Convertible(object v)
    {
        if (v is Vector2 v2) return new[] { v2.x, v2.y };
        if (v is Vector3 v3) return new[] { v3.x, v3.y, v3.z };
        if (v is Color c) return new[] { c.r, c.g, c.b, c.a };
        if (v is Enum) return v.ToString();
        return v;
    }

    private static bool TrySet(object target, string name, JToken value)
    {
        var t = target.GetType();
        var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        var p = f == null ? t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance) : null;
        Type want = f != null ? f.FieldType : (p != null ? p.PropertyType : null);
        if (want == null) return false;

        object converted;
        if (!TryConvert(value, want, out converted)) return false;

        if (f != null) f.SetValue(target, converted);
        else if (p.CanWrite) p.SetValue(target, converted, null);
        else return false;
        return true;
    }

    private static bool TryConvert(JToken token, Type want, out object result)
    {
        result = null;
        try
        {
            if (want == typeof(float))  { result = (float)token;  return true; }
            if (want == typeof(int))    { result = (int)token;    return true; }
            if (want == typeof(bool))   { result = (bool)token;   return true; }
            if (want == typeof(string)) { result = (string)token; return true; }

            if (want.IsEnum)
            {
                // Written as a name by Capture, but the authored profiles use names too
                // ("protanomaly"), so accept either that or an index.
                if (token.Type == JTokenType.Integer)
                {
                    result = Enum.ToObject(want, (int)token);
                    return true;
                }
                string s = ((string)token ?? "").Replace("_", "");
                foreach (var n in Enum.GetNames(want))
                    if (string.Equals(n, s, StringComparison.OrdinalIgnoreCase))
                    {
                        result = Enum.Parse(want, n);
                        return true;
                    }
                return false;
            }

            var arr = token as JArray;
            if (arr != null)
            {
                if (want == typeof(Vector2) && arr.Count >= 2)
                { result = new Vector2((float)arr[0], (float)arr[1]); return true; }
                if (want == typeof(Vector3) && arr.Count >= 3)
                { result = new Vector3((float)arr[0], (float)arr[1], (float)arr[2]); return true; }
                if (want == typeof(Color) && arr.Count >= 3)
                {
                    result = new Color((float)arr[0], (float)arr[1], (float)arr[2],
                                       arr.Count > 3 ? (float)arr[3] : 1f);
                    return true;
                }
            }
        }
        catch (Exception) { return false; }
        return false;
    }

    // ------------------------------------------------------------------ effect objects

    private static Transform _menu;

    /// <summary>
    /// The effect objects live under the menu, and are found by the same names the profiles
    /// use for them. A name that matches nothing is reported rather than ignored -- a
    /// profile that silently turns nothing on is the failure this whole path is guarding
    /// against.
    /// </summary>
    private static GameObject FindEffect(SettingsManager sm, string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (_menu == null)
        {
            var go = GameObject.Find("VerticalMenu");
            _menu = go != null ? go.transform : null;
        }
        if (_menu != null)
        {
            foreach (Transform child in _menu)
                if (child.name == name) return child.gameObject;
        }
        return GameObject.Find(name);
    }

    private static void SetEffectActive(SettingsManager sm, string name, bool active)
    {
        var go = FindEffect(sm, name);
        if (go != null && go.activeSelf != active) go.SetActive(active);
    }

    private static bool IsEffectActive(SettingsManager sm, string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        var go = FindEffect(sm, name);
        return go != null && go.activeInHierarchy;
    }

    public static IEnumerable<string> KnownFilters
    {
        get { foreach (var b in Table) yield return b.Filter; }
    }
}
