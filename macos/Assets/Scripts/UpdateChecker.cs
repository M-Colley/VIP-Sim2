using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Tells the user when a newer VIP-Sim exists.
///
/// A tool distributed as a zip has no other way to say so: there is no store, no package
/// manager and no auto-updater, so without this a user stays on whatever build they first
/// downloaded and every bug fixed since is invisible to them. That is a support burden as
/// much as a user problem -- most "it does X" reports for software like this come from
/// versions where X was already fixed.
///
/// It deliberately does NOT download or install anything. Silently replacing a signed
/// binary is a security-sensitive thing to build and an easy thing to build badly; the
/// honest version of this feature points at the release page and lets the user decide.
///
/// Privacy: one HTTPS GET to the public GitHub releases API, carrying nothing but the
/// request itself -- no identifier, no usage data, no personal data. GitHub will see the
/// IP address, as any web request does. It can be switched off from the F1 panel, the
/// setting persists, and docs/PRIVACY.md documents it. Consistent with the project's
/// existing stance, where the research telemetry is off until explicitly consented to.
/// </summary>
public class UpdateChecker : MonoBehaviour
{
    private const string PrefKey = "vipsim.updatecheck";
    private const string ApiUrl = "https://api.github.com/repos/M-Colley/VIP-Sim2/releases/latest";

    /// <summary>Where a user goes to get the new build.</summary>
    public const string ReleasesUrl = "https://github.com/M-Colley/VIP-Sim2/releases";

    /// <summary>Where a user reports a problem.</summary>
    public const string SupportUrl = "https://github.com/M-Colley/VIP-Sim2/issues";

    private static UpdateChecker _instance;

    /// <summary>Null until the check has completed; false if it failed or was disabled.</summary>
    public static bool UpdateAvailable { get; private set; }

    /// <summary>One short line for the F1 panel. Never null.</summary>
    public static string Status { get; private set; } = "";

    public static bool Enabled
    {
        get => PlayerPrefs.GetInt(PrefKey, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
            PlayerPrefs.Save();
            if (value && _instance != null) _instance.Begin();
            else if (!value) Status = "Update check is off.";
        }
    }

    /// <summary>Attach at runtime, the same way the tutorial is attached.</summary>
    public static void Install(GameObject host)
    {
        if (host.GetComponent<UpdateChecker>() == null) host.AddComponent<UpdateChecker>();
    }

    private void Awake() => _instance = this;

    private void Start()
    {
        if (!Enabled)
        {
            Status = "Update check is off.";
            return;
        }
        Begin();
    }

    private void Begin()
    {
        Status = "Checking for updates...";
        StartCoroutine(Check());
    }

    private System.Collections.IEnumerator Check()
    {
        // Deferred: startup is already contended -- window acquisition, capture, the
        // camera -- and nothing here is urgent.
        yield return new WaitForSeconds(5f);

        using (var req = UnityWebRequest.Get(ApiUrl))
        {
            req.timeout = 10;
            // GitHub's API rejects requests without a User-Agent.
            req.SetRequestHeader("User-Agent", "VIP-Sim");
            req.SetRequestHeader("Accept", "application/vnd.github+json");
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                // Offline is the normal case, not an error worth shouting about.
                Status = "Could not check for updates.";
                Debug.Log($"[UpdateChecker] Check failed: {req.error}");
                yield break;
            }

            string tag = ExtractTag(req.downloadHandler.text);
            if (string.IsNullOrEmpty(tag))
            {
                Status = "Could not read the latest version.";
                yield break;
            }

            string latest = tag.TrimStart('v', 'V');
            string current = Application.version;
            if (IsNewer(latest, current))
            {
                UpdateAvailable = true;
                Status = $"Version {latest} is available (you have {current}).";
            }
            else
            {
                Status = $"VIP-Sim {current} is up to date.";
            }
            Debug.Log($"[UpdateChecker] {Status}");
        }
    }

    /// <summary>
    /// Pull tag_name out of the response without a JSON library. The field is a flat
    /// string in a known shape, and adding a parser dependency to read one value is not
    /// a trade worth making.
    /// </summary>
    private static string ExtractTag(string json)
    {
        const string key = "\"tag_name\"";
        int i = json.IndexOf(key, System.StringComparison.Ordinal);
        if (i < 0) return null;
        i = json.IndexOf('"', i + key.Length + 1);
        if (i < 0) return null;
        int end = json.IndexOf('"', i + 1);
        return end < 0 ? null : json.Substring(i + 1, end - i - 1);
    }

    /// <summary>
    /// Numeric-segment comparison, so 2.10.0 beats 2.9.0 -- which a string compare gets
    /// backwards. Anything non-numeric (a "2.0.0beta" suffix, say) sorts as older than
    /// the same version without it, which is the conventional reading of a pre-release.
    /// </summary>
    private static bool IsNewer(string latest, string current)
    {
        var a = latest.Split('.');
        var b = current.Split('.');
        for (int i = 0; i < Mathf.Max(a.Length, b.Length); i++)
        {
            int x = SegmentValue(i < a.Length ? a[i] : "0");
            int y = SegmentValue(i < b.Length ? b[i] : "0");
            if (x != y) return x > y;
        }
        return false;
    }

    private static int SegmentValue(string seg)
    {
        int n = 0, i = 0;
        while (i < seg.Length && char.IsDigit(seg[i])) { n = n * 10 + (seg[i] - '0'); i++; }
        // A trailing suffix means pre-release: rank it just below the clean number.
        return i < seg.Length ? n * 10 - 1 : n * 10;
    }
}
