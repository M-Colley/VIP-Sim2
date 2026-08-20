using System.Collections.Generic;
using Newtonsoft.Json.Linq;

/// <summary>
/// A condition profile: one clinical presentation, as a stack of filters with parameters.
///
/// These are authored elsewhere and describe more than this build can draw. They came from a
/// separate filter pipeline whose shaders take parameters ours do not have -- halo colours on
/// the vortex, seeded placement and regions for floaters, elliptical and inverted masks for
/// foveal darkness. Slightly over half of what a profile specifies has no counterpart here.
///
/// That is survivable and it is not something to paper over. A profile is loaded for what it
/// can express, and every parameter that could not be applied is named in the report, per
/// filter, so the difference between "this is what the profile says" and "this is what you
/// are looking at" is never something the user has to guess at.
/// </summary>
public sealed class ConditionProfile
{
    public string Id;
    public string Version;
    public string Description;
    public string Caveat;
    public List<ProfileFilter> Filters = new List<ProfileFilter>();

    /// <summary>One filter and its parameters, kept as JSON because they differ per filter.</summary>
    public sealed class ProfileFilter
    {
        public string Name;
        public JObject Parameters;
    }

    /// <summary>What happened when a profile was applied, in enough detail to act on.</summary>
    public sealed class Report
    {
        public readonly List<string> Applied = new List<string>();
        public readonly List<string> Unsupported = new List<string>();
        public readonly List<string> UnknownFilters = new List<string>();

        public int Total => Applied.Count + Unsupported.Count;

        public string Summary(string profileId)
        {
            var s = $"[ConditionProfile] {profileId}: applied {Applied.Count} of {Total} parameters";
            if (UnknownFilters.Count > 0)
                s += $"; {UnknownFilters.Count} filter(s) this build does not have: " +
                     string.Join(", ", UnknownFilters);
            if (Unsupported.Count > 0)
                s += $"; no counterpart here for: {string.Join(", ", Unsupported)}";
            return s;
        }
    }

    /// <summary>
    /// Read a profile, or return null and say why. Recognising the format is the caller's
    /// business -- see SettingsManager, which has to tell a profile from a saved settings
    /// file before it applies either, having once applied the wrong one to everything.
    /// </summary>
    public static ConditionProfile Parse(JObject root, out string error)
    {
        error = null;

        var filters = root["filters"] as JArray;
        if (filters == null)
        {
            error = "no \"filters\" array, so this is not a condition profile";
            return null;
        }

        var p = new ConditionProfile
        {
            Id          = (string)root["id"] ?? "(unnamed)",
            Version     = (string)root["version"] ?? "(no version)",
            Description = (string)root["description"] ?? "",
            Caveat      = (string)root["caveat"] ?? "",
        };

        foreach (var f in filters)
        {
            var o = f as JObject;
            var name = o != null ? (string)o["filter"] : null;
            if (string.IsNullOrEmpty(name)) continue;
            p.Filters.Add(new ProfileFilter { Name = name, Parameters = o });
        }

        if (p.Filters.Count == 0)
        {
            error = "the \"filters\" array holds nothing this build can read";
            return null;
        }
        return p;
    }
}
