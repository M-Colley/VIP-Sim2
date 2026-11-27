using System;
using System.Collections;
using System.IO;
using UnityEngine;
using VipSim.Policy;

/// <summary>
/// Settings an organisation fixes for every user of a machine.
///
/// An IT department deploying VIP-Sim needs to decide some things once: whether the app may
/// contact GitHub for updates, whether the webcam may be used, the viewing distance its
/// screens are set up for, and where review reports go. They cannot click through a panel
/// on every machine, and PlayerPrefs live in each user's own registry hive. So VIP-Sim reads
/// an optional policy.json from a machine-wide location, or from --policy on the command
/// line. Settings it fixes cannot be changed in the app; the panel says why.
///
/// The format is documented in docs/ORGANISATIONS.md. Parsing is in VipSim.Policy, which
/// has no Unity dependency and is tested in EditMode.
/// </summary>
public static class VipSimPolicy
{
    private static PolicyDocument _current;

    /// <summary>The file the policy came from, or null if there is none.</summary>
    public static string SourcePath { get; private set; }

    public static PolicyDocument Current
    {
        get
        {
            if (_current == null) Load();
            return _current;
        }
    }

    public static bool UpdateCheckAllowed => Current.UpdateCheck != false;
    public static bool TelemetryAllowed => Current.TelemetryAllowed != false;
    public static bool WebcamAllowed => Current.WebcamAllowed != false;

    /// <summary>The machine-wide policy location for this platform.</summary>
    public static string DefaultPath()
    {
        switch (Application.platform)
        {
            case RuntimePlatform.WindowsPlayer:
            case RuntimePlatform.WindowsEditor:
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                                    "VIP-Sim", "policy.json");
            case RuntimePlatform.OSXPlayer:
            case RuntimePlatform.OSXEditor:
                return "/Library/Application Support/VIP-Sim/policy.json";
            default:
                return "/etc/vip-sim/policy.json";
        }
    }

    private static void Load()
    {
        _current = new PolicyDocument();
        string path = Arg("--policy") ?? DefaultPath();
        if (!File.Exists(path))
        {
            if (Arg("--policy") != null)
                Debug.LogWarning($"[VipSimPolicy] --policy {path}: file not found; no policy applies.");
            return;
        }

        try
        {
            _current = PolicyDocument.Parse(File.ReadAllText(path), Environment.GetEnvironmentVariable,
                                            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            SourcePath = path;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            Debug.LogWarning($"[VipSimPolicy] could not read {path}: {e.Message}; no policy applies.");
            return;
        }

        foreach (var w in _current.Warnings) Debug.LogWarning($"[VipSimPolicy] {path}: {w}");
        Debug.Log($"[VipSimPolicy] {path}: {_current.Describe()}");
    }

    /// <summary>
    /// Apply what has to be in place before the user does anything: the viewing geometry and
    /// the report folder now, the start profile once the scene has run a frame.
    /// </summary>
    public static void ApplyAtStart(MonoBehaviour host)
    {
        var p = Current;
        if (p.ViewingDistanceCm.HasValue || p.ScreenWidthCm.HasValue)
            VisSim.ViewingGeometry.SetFixed(p.ScreenWidthCm, p.ViewingDistanceCm);
        if (!string.IsNullOrEmpty(p.ReportFolder))
            ReviewRecorder.RootOverride = p.ReportFolder;

        // The command line names a profile for this run; the policy names the default.
        string profile = Arg("--profile") ?? p.Profile;
        if (!string.IsNullOrEmpty(profile)) host.StartCoroutine(LoadProfileWhenReady(profile));
    }

    private static IEnumerator LoadProfileWhenReady(string path)
    {
        // Two frames, so every effect has run Start and the binder finds what it sets.
        yield return null;
        yield return null;

        if (!File.Exists(path))
        {
            Debug.LogWarning($"[VipSimPolicy] start profile {path} not found.");
            yield break;
        }
        var manager = UnityEngine.Object.FindAnyObjectByType<SettingsManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            Debug.LogWarning("[VipSimPolicy] no SettingsManager in the scene; start profile not loaded.");
            yield break;
        }
        manager.LoadFromFile(path);
        Debug.Log($"[VipSimPolicy] loaded start profile {path}");
    }

    private static string Arg(string name)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (string.Equals(args[i], name, StringComparison.Ordinal)) return args[i + 1];
        return null;
    }
}
