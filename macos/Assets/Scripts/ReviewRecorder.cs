using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using VipSim.Review;

/// <summary>
/// The review in progress, and the folder it is written to.
///
/// A saved image on its own does not survive a team's process: nobody can say a month later
/// which symptoms were on, at what severity, at what viewing distance, or what the reviewer
/// meant by it. Each press of the toolbar's save button therefore adds a finding: the window
/// with the symptoms, the same window without them, the settings that reproduce it, and the
/// viewing conditions. The F1 panel's Review tab adds the note, the impact and the
/// requirements. The report, the CSV and the JSON are rewritten from the session after every
/// change, so a crash loses at most the note being typed.
/// </summary>
public static class ReviewRecorder
{
    private const string ProductKey = "VipSim.Review.Product";
    private const string ReviewerKey = "VipSim.Review.Reviewer";
    private const string TitlesKey = "VipSim.Review.WindowTitles";

    private static ReviewSession _session;
    private static string _folder;
    private static bool _dirty;

    /// <summary>The session, started on first use.</summary>
    public static ReviewSession Session
    {
        get
        {
            if (_session == null) Start();
            return _session;
        }
    }

    /// <summary>Where this session's files go. Created with the first finding.</summary>
    public static string Folder
    {
        get
        {
            if (_session == null) Start();
            return _folder;
        }
    }

    public static bool HasFindings => _session != null && _session.Findings.Count > 0;

    /// <summary>
    /// Off by default. A window title often names a customer, a document or an account, and
    /// it would travel with the report and the issue tracker export.
    /// </summary>
    public static bool IncludeWindowTitles
    {
        get => PlayerPrefs.GetInt(TitlesKey, 0) == 1;
        set => PlayerPrefs.SetInt(TitlesKey, value ? 1 : 0);
    }

    public static void SetProduct(string product)
    {
        if (Session.Product == product) return;
        Session.Product = product ?? "";
        PlayerPrefs.SetString(ProductKey, Session.Product);
        MarkDirty();
    }

    public static void SetReviewer(string reviewer)
    {
        if (Session.Reviewer == reviewer) return;
        Session.Reviewer = reviewer ?? "";
        PlayerPrefs.SetString(ReviewerKey, Session.Reviewer);
        MarkDirty();
    }

    public static void MarkDirty() => _dirty = true;

    /// <summary>
    /// A fresh session in a fresh folder. The previous session's files stay where they are,
    /// complete, because every change was already written.
    /// </summary>
    public static void Start()
    {
        WriteIfDirty();
        var now = DateTimeOffset.Now;
        _session = new ReviewSession
        {
            Started = now,
            AppVersion = Application.version,
            // Carried over: the same person usually reviews the same product all afternoon.
            Product = PlayerPrefs.GetString(ProductKey, ""),
            Reviewer = PlayerPrefs.GetString(ReviewerKey, ""),
        };
        _folder = Path.Combine(OutputRoot(), "Review " + now.ToString("yyyy-MM-dd HH-mm-ss"));
        _dirty = false;
    }

    /// <summary>
    /// Record one finding from the two rendered images and the profile that reproduces the
    /// simulated one. Returns null, with the reason logged, if nothing could be written.
    /// </summary>
    public static Finding AddFinding(byte[] simulatedPng, byte[] originalPng, JObject profile, string windowTitle)
    {
        var session = Session;
        string id = session.NextId();
        var finding = new Finding
        {
            Id = id,
            Captured = DateTimeOffset.Now,
            Viewing = CurrentViewing(),
            Symptoms = Symptom.FromProfile(profile),
            SimulatedImage = id + "-simulated.png",
            OriginalImage = originalPng == null ? null : id + "-original.png",
            Profile = profile == null ? null : id + "-profile.json",
            WindowTitle = IncludeWindowTitles && !string.IsNullOrWhiteSpace(windowTitle) ? windowTitle.Trim() : null,
        };

        try
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllBytes(Path.Combine(_folder, finding.SimulatedImage), simulatedPng);
            if (originalPng != null) File.WriteAllBytes(Path.Combine(_folder, finding.OriginalImage), originalPng);
            if (profile != null)
                File.WriteAllText(Path.Combine(_folder, finding.Profile),
                                  profile.ToString(Newtonsoft.Json.Formatting.Indented));
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            Debug.LogError($"[ReviewRecorder] could not write finding {id} to {_folder}: {e.Message}");
            return null;
        }

        session.Findings.Add(finding);
        _dirty = true;
        WriteIfDirty();
        return finding;
    }

    /// <summary>Remove a finding and its files. Its number is not reused.</summary>
    public static void Delete(Finding finding)
    {
        if (_session == null || finding == null || !_session.Findings.Remove(finding)) return;
        foreach (var file in new[] { finding.SimulatedImage, finding.OriginalImage, finding.Profile })
        {
            if (string.IsNullOrEmpty(file)) continue;
            try { File.Delete(Path.Combine(_folder, file)); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[ReviewRecorder] could not delete {file}: {e.Message}");
            }
        }
        _dirty = true;
        WriteIfDirty();
    }

    /// <summary>
    /// Rewrite report.html, findings.csv and findings.json. Nothing is written for a session
    /// without findings, so opening the panel and closing it leaves no empty folders behind.
    /// </summary>
    public static bool WriteIfDirty()
    {
        if (_session == null || !_dirty || _session.Findings.Count == 0) return true;
        try
        {
            Directory.CreateDirectory(_folder);
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            Debug.LogError($"[ReviewRecorder] could not create {_folder}: {e.Message}");
            return false;
        }

        bool ok = SettingsManager.WriteFileSafely(Path.Combine(_folder, ReviewReportWriter.JsonFile),
                                                  ReviewReportWriter.Json(_session));
        ok &= SettingsManager.WriteFileSafely(Path.Combine(_folder, ReviewReportWriter.CsvFile),
                                              ReviewReportWriter.Csv(_session));
        ok &= SettingsManager.WriteFileSafely(Path.Combine(_folder, ReviewReportWriter.HtmlFile),
                                              ReviewReportWriter.Html(_session, DateTimeOffset.Now));
        if (ok) _dirty = false;
        return ok;
    }

    public static void OpenReport()
    {
        WriteIfDirty();
        string html = Path.Combine(Folder, ReviewReportWriter.HtmlFile);
        Application.OpenURL(new Uri(File.Exists(html) ? html : Folder).AbsoluteUri);
    }

    /// <summary>The geometry that converted degrees to pixels when the image was rendered.</summary>
    public static ViewingConditions CurrentViewing()
    {
        string gaze = "Mouse pointer";
        var tracker = UnityEngine.Object.FindAnyObjectByType<GazeTracker>(FindObjectsInactive.Include);
        if (tracker != null)
            gaze = tracker.gazeSource == GazeTracker.GazeSource.UnitEye ? "Webcam eye tracking" : "Mouse pointer";

        return new ViewingConditions
        {
            DistanceCm = Math.Round(VisSim.ViewingGeometry.DistanceCm, 1),
            ScreenWidthCm = Math.Round(VisSim.ViewingGeometry.WidthCm, 1),
            ScreenWidthSource = VisSim.ViewingGeometry.Source.ToString(),
            Monitor = string.IsNullOrEmpty(VisSim.ViewingGeometry.MonitorName) ? null : VisSim.ViewingGeometry.MonitorName,
            PixelsAcross = VisSim.ViewingGeometry.PixelsAcross,
            PixelsPerDegree = Math.Round(VisSim.ViewingGeometry.PixelsPerDegree, 2),
            ScreenSpanDeg = Math.Round(VisSim.ViewingGeometry.ScreenSpanDeg, 1),
            Gaze = gaze,
            Os = SystemInfo.operatingSystem,
        };
    }

    /// <summary>
    /// Replaces Pictures/VIP-Sim as the folder reviews are written to. Takes effect for the
    /// next session started.
    /// </summary>
    public static string RootOverride { get; set; }

    /// <summary>Pictures/VIP-Sim, or next to the log where a platform reports no Pictures folder.</summary>
    public static string OutputRoot()
    {
        if (!string.IsNullOrEmpty(RootOverride)) return RootOverride;
        string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        string root = string.IsNullOrEmpty(pictures) ? Application.persistentDataPath : pictures;
        return Path.Combine(root, "VIP-Sim");
    }
}
