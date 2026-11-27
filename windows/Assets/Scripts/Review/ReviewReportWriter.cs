using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using Newtonsoft.Json;

namespace VipSim.Review
{
    /// <summary>
    /// Writes a review as three files: report.html for people, findings.csv for issue
    /// trackers, findings.json for tools and for reopening the review. All three are built
    /// from the session alone, so writing them again after an edit is always safe.
    /// </summary>
    public static class ReviewReportWriter
    {
        public const string HtmlFile = "report.html";
        public const string CsvFile = "findings.csv";
        public const string JsonFile = "findings.json";

        private const string PaperDoi = "https://doi.org/10.1145/3746059.3747704";
        private const string SilvermanDoi = "https://doi.org/10.1177/1948550614559650";
        private const string NarioRedmondDoi = "https://doi.org/10.1037/rep0000127";
        private const string RepoUrl = "https://github.com/M-Colley/VIP-Sim2";

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ------------------------------------------------------------------ JSON

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            DateParseHandling = DateParseHandling.DateTimeOffset,
        };

        public static string Json(ReviewSession session) => JsonConvert.SerializeObject(session, JsonSettings);

        public static ReviewSession Load(string json)
        {
            var session = JsonConvert.DeserializeObject<ReviewSession>(json, JsonSettings);
            if (session == null || session.Schema != ReviewSession.SchemaId)
                throw new FormatException("Not a VIP-Sim review (expected schema " + ReviewSession.SchemaId + ").");
            return session;
        }

        // ------------------------------------------------------------------ CSV

        /// <summary>
        /// One row per finding, RFC 4180 quoting. Summary, Description, Priority and Labels are
        /// the columns Jira's CSV import maps by default; Azure DevOps maps Title from Summary
        /// at import. Labels carry no spaces because Jira splits labels on them.
        /// </summary>
        public static string Csv(ReviewSession session)
        {
            var sb = new StringBuilder();
            Row(sb, "Summary", "Description", "Priority", "Labels", "Finding", "Requirements",
                "Symptoms", "Simulated image", "Original image", "Captured");

            foreach (var f in session.Findings)
            {
                var labels = new List<string> { "vip-sim", "low-vision" };
                labels.AddRange(f.Criteria);

                Row(sb,
                    f.Summary(),
                    Description(session, f),
                    Priority(f.Impact),
                    string.Join(" ", labels),
                    f.Id,
                    string.Join("; ", f.Criteria.Select(CriterionText)),
                    SymptomsWithSettings(f),
                    f.SimulatedImage ?? "",
                    f.OriginalImage ?? "",
                    f.Captured.ToString("yyyy-MM-dd HH:mm:ss zzz", Inv));
            }
            return sb.ToString();
        }

        private static string Description(ReviewSession s, Finding f)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(f.Note)) sb.Append(f.Note.Trim()).Append("\n\n");
            sb.Append("Simulated symptoms: ").Append(SymptomsWithSettings(f)).Append('\n');
            sb.Append("Viewing conditions: ").Append(f.Viewing.Describe()).Append('\n');
            if (!string.IsNullOrEmpty(f.WindowTitle)) sb.Append("Window: ").Append(f.WindowTitle).Append('\n');
            sb.Append("Product: ").Append(s.Product).Append(". Reviewer: ").Append(s.Reviewer).Append('.');
            return sb.ToString();
        }

        private static string Priority(Impact i)
        {
            switch (i)
            {
                case Impact.Blocker: return "Highest";
                case Impact.Major: return "High";
                case Impact.Minor: return "Medium";
                default: return "Low";
            }
        }

        private static void Row(StringBuilder sb, params string[] cells)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                if (i > 0) sb.Append(',');
                string c = cells[i] ?? "";
                if (c.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0)
                    sb.Append('"').Append(c.Replace("\"", "\"\"")).Append('"');
                else
                    sb.Append(c);
            }
            sb.Append("\r\n");
        }

        // ------------------------------------------------------------------ text helpers

        private static string CriterionText(string id)
        {
            var c = ReviewCatalog.Find(id);
            if (c == null) return id;
            string text = c.Id.StartsWith("en-", StringComparison.Ordinal)
                ? ReviewCatalog.EnStandard + " " + c.Name
                : c.Id == "beyond" ? c.Name : "WCAG 2.2 " + c.Name + " (" + c.Level + ")";
            return text;
        }

        private static string SymptomsWithSettings(Finding f)
        {
            if (f.Symptoms.Count == 0) return "none";
            return string.Join("; ", f.Symptoms.Select(s =>
                s.Label + (s.Parameters.Count == 0 ? "" :
                    " (" + string.Join(", ", s.Parameters.Select(ParameterText)) + ")")));
        }

        private static string ParameterText(Parameter p) =>
            p.Name + " " + p.Value + (p.Unit == null ? "" : " " + p.Unit);

        // ------------------------------------------------------------------ HTML

        private static string H(string s) => WebUtility.HtmlEncode(s ?? "");

        /// <summary>A file name next to the report, as a relative link.</summary>
        private static string Href(string file) =>
            H((file ?? "").Replace("%", "%25").Replace(" ", "%20").Replace("#", "%23").Replace("?", "%3F"));

        /// <summary>The note as paragraphs, keeping the reviewer's line breaks.</summary>
        private static string Paragraphs(string text)
        {
            var parts = (text ?? "").Replace("\r\n", "\n").Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();
            foreach (var p in parts)
                sb.Append("<p>").Append(H(p.Trim()).Replace("\n", "<br>")).Append("</p>");
            return sb.ToString();
        }

        public static string Html(ReviewSession s, DateTimeOffset generated)
        {
            string product = string.IsNullOrWhiteSpace(s.Product) ? "Untitled product" : s.Product.Trim();
            var sb = new StringBuilder();

            sb.Append("<!DOCTYPE html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\">\n")
              .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n")
              .Append("<title>Low-vision review: ").Append(H(product)).Append("</title>\n")
              .Append("<style>").Append(Css).Append("</style>\n</head>\n<body>\n<main>\n");

            // Header
            sb.Append("<header>\n<p class=\"kicker\">Low-vision review with VIP-Sim</p>\n<h1>")
              .Append(H(product)).Append("</h1>\n<dl class=\"meta\">\n");
            Meta(sb, "Reviewer", string.IsNullOrWhiteSpace(s.Reviewer) ? "Not stated" : s.Reviewer);
            Meta(sb, "Started", s.Started.ToString("d MMMM yyyy, HH:mm", Inv));
            Meta(sb, "Findings", s.Findings.Count.ToString(Inv));
            Meta(sb, "VIP-Sim", s.AppVersion);
            sb.Append("</dl>\n</header>\n");

            Summary(sb, s);
            Findings(sb, s);
            Method(sb, s);
            Limitations(sb);

            sb.Append("<footer><p>Generated by VIP-Sim ").Append(H(s.AppVersion)).Append(" on ")
              .Append(generated.ToString("d MMMM yyyy, HH:mm", Inv))
              .Append(". VIP-Sim is open source under the MIT licence: <a href=\"").Append(RepoUrl).Append("\">")
              .Append(RepoUrl.Replace("https://", "")).Append("</a>.</p></footer>\n</main>\n</body>\n</html>\n");
            return sb.ToString();
        }

        private static void Meta(StringBuilder sb, string term, string value) =>
            sb.Append("<div><dt>").Append(H(term)).Append("</dt><dd>").Append(H(value)).Append("</dd></div>\n");

        private static void Summary(StringBuilder sb, ReviewSession s)
        {
            sb.Append("<section aria-labelledby=\"summary\">\n<h2 id=\"summary\">Summary</h2>\n");
            if (s.Findings.Count == 0)
            {
                sb.Append("<p>No findings were recorded.</p>\n</section>\n");
                return;
            }

            var byImpact = Enum.GetValues(typeof(Impact)).Cast<Impact>().Reverse()
                .Select(i => (i, n: s.Findings.Count(f => f.Impact == i))).Where(x => x.n > 0)
                .Select(x => x.n.ToString(Inv) + " " + ImpactWord(x.i, x.n));
            sb.Append("<p>").Append(s.Findings.Count.ToString(Inv))
              .Append(s.Findings.Count == 1 ? " finding: " : " findings: ")
              .Append(H(string.Join(", ", byImpact))).Append(".</p>\n");

            var used = ReviewCatalog.Criteria.Where(c => s.Findings.Any(f => f.Criteria.Contains(c.Id))).ToList();
            int untagged = s.Findings.Count(f => f.Criteria.Count == 0);
            if (used.Count > 0)
            {
                sb.Append("<section class=\"scroll\" aria-label=\"Findings by requirement\" tabindex=\"0\">\n")
                  .Append("<table>\n<caption>Findings by requirement</caption>\n<thead><tr>")
                  .Append("<th scope=\"col\">Requirement</th><th scope=\"col\">")
                  .Append(H(ReviewCatalog.EnStandard)).Append("</th><th scope=\"col\">Findings</th></tr></thead>\n<tbody>\n");
                foreach (var c in used)
                {
                    var ids = s.Findings.Where(f => f.Criteria.Contains(c.Id)).Select(f => f.Id);
                    sb.Append("<tr><th scope=\"row\">").Append(CriterionLink(c)).Append("</th><td>")
                      .Append(H(c.En301549.Length == 0 ? "None" : c.En301549)).Append("</td><td>")
                      .Append(string.Join(", ", ids.Select(id => "<a href=\"#" + H(id) + "\">" + H(id) + "</a>")))
                      .Append("</td></tr>\n");
                }
                sb.Append("</tbody>\n</table>\n</section>\n");
            }
            if (untagged > 0)
                sb.Append("<p>").Append(untagged.ToString(Inv))
                  .Append(untagged == 1 ? " finding is" : " findings are").Append(" not linked to a requirement.</p>\n");
            sb.Append("</section>\n");
        }

        private static string ImpactWord(Impact i, int n)
        {
            string w = i.ToString().ToLowerInvariant();
            return n == 1 ? w : w + "s";
        }

        private static string CriterionLink(Criterion c)
        {
            string text = H(c.Name) + (c.Level.Length > 0 ? " (" + H(c.Level) + ")" : "");
            if (c.Id.StartsWith("wcag-", StringComparison.Ordinal)) text = "WCAG 2.2 " + text;
            else if (c.Id.StartsWith("en-", StringComparison.Ordinal)) text = "EN 301 549 " + text;
            return c.Url.Length == 0 ? text : "<a href=\"" + H(c.Url) + "\">" + text + "</a>";
        }

        private static void Findings(StringBuilder sb, ReviewSession s)
        {
            if (s.Findings.Count == 0) return;
            sb.Append("<section aria-labelledby=\"findings\">\n<h2 id=\"findings\">Findings</h2>\n");

            foreach (var f in s.Findings)
            {
                sb.Append("<article id=\"").Append(H(f.Id)).Append("\">\n<h3><span class=\"id\">")
                  .Append(H(f.Id)).Append("</span> <span class=\"impact impact-")
                  .Append(f.Impact.ToString().ToLowerInvariant()).Append("\">").Append(H(f.Impact.ToString()))
                  .Append("</span> ").Append(H(f.Summary(140))).Append("</h3>\n");

                // The heading already shows the note's first line; the body carries the rest.
                string body = (f.Note ?? "").Trim();
                string summary = f.Summary(140);
                if (body.StartsWith(summary, StringComparison.Ordinal)) body = body.Substring(summary.Length).Trim();
                if (string.IsNullOrWhiteSpace(f.Note))
                    sb.Append("<p class=\"muted\">No note was written for this finding.</p>\n");
                else if (body.Length > 0)
                    sb.Append(Paragraphs(body)).Append('\n');

                if (f.Criteria.Count > 0)
                {
                    sb.Append("<p><strong>Requirements:</strong> ")
                      .Append(string.Join("; ", f.Criteria.Select(id =>
                      {
                          var c = ReviewCatalog.Find(id);
                          return c == null ? H(id) : CriterionLink(c);
                      })))
                      .Append("</p>\n");
                }

                sb.Append("<div class=\"pair\">\n");
                if (!string.IsNullOrEmpty(f.OriginalImage))
                    Figure(sb, f.OriginalImage, "Without simulation",
                           "The captured window without simulated symptoms.");
                Figure(sb, f.SimulatedImage, "Simulated: " + f.SymptomList(),
                       "The captured window with simulated " + f.SymptomList() + ".");
                sb.Append("</div>\n");

                if (f.Symptoms.Count > 0)
                {
                    sb.Append("<section class=\"scroll\" aria-label=\"Symptoms and settings for ").Append(H(f.Id))
                      .Append("\" tabindex=\"0\">\n<table class=\"settings\">\n<caption>Symptoms and settings for ").Append(H(f.Id))
                      .Append("</caption>\n<thead><tr><th scope=\"col\">Symptom</th><th scope=\"col\">Setting</th>")
                      .Append("<th scope=\"col\">Value</th><th scope=\"col\">Unit</th></tr></thead>\n<tbody>\n");
                    foreach (var sym in f.Symptoms)
                    {
                        if (sym.Parameters.Count == 0)
                        {
                            sb.Append("<tr><th scope=\"row\">").Append(SymptomCell(sym))
                              .Append("</th><td colspan=\"3\" class=\"muted\">Default settings</td></tr>\n");
                            continue;
                        }
                        for (int i = 0; i < sym.Parameters.Count; i++)
                        {
                            var p = sym.Parameters[i];
                            sb.Append("<tr>");
                            if (i == 0)
                                sb.Append("<th scope=\"rowgroup\" rowspan=\"").Append(sym.Parameters.Count.ToString(Inv))
                                  .Append("\">").Append(SymptomCell(sym)).Append("</th>");
                            sb.Append("<td>").Append(H(p.Name)).Append("</td><td class=\"num\">").Append(H(p.Value))
                              .Append("</td><td>")
                              .Append(p.Unit != null ? H(p.Unit)
                                      : p.IsNumber ? "<span class=\"muted\">Effect scale, uncalibrated</span>" : "")
                              .Append("</td></tr>\n");
                        }
                    }
                    sb.Append("</tbody>\n</table>\n</section>\n");
                }

                sb.Append("<p class=\"small\">Captured ").Append(H(f.Captured.ToString("d MMMM yyyy, HH:mm:ss", Inv)));
                if (!string.IsNullOrEmpty(f.WindowTitle)) sb.Append(" from the window “").Append(H(f.WindowTitle)).Append("”");
                sb.Append(". ").Append(H(Capitalise(f.Viewing.Describe()))).Append('.');
                if (!string.IsNullOrEmpty(f.Profile))
                    sb.Append(" Settings file: <a href=\"").Append(Href(f.Profile)).Append("\">")
                      .Append(H(f.Profile)).Append("</a>, which loads back into VIP-Sim.");
                sb.Append("</p>\n</article>\n");
            }
            sb.Append("</section>\n");
        }

        private static string SymptomCell(Symptom s) =>
            H(s.Label) + (s.Term.Length > 0 && s.Term != s.Label ? "<br><span class=\"muted\">" + H(s.Term) + "</span>" : "");

        private static void Figure(StringBuilder sb, string file, string caption, string alt)
        {
            sb.Append("<figure><a href=\"").Append(Href(file)).Append("\"><img src=\"")
              .Append(Href(file)).Append("\" alt=\"").Append(H(alt)).Append("\" loading=\"lazy\"></a>")
              .Append("<figcaption>").Append(H(caption)).Append("</figcaption></figure>\n");
        }

        private static string Shorten(string s, int max) =>
            s.Length <= max ? s : s.Substring(0, max - 3).TrimEnd() + "...";

        private static string Capitalise(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        private static void Method(StringBuilder sb, ReviewSession s)
        {
            sb.Append("<section aria-labelledby=\"method\">\n<h2 id=\"method\">Method</h2>\n")
              .Append("<p>VIP-Sim renders symptoms of vision impairment over the application under review. ")
              .Append("For each finding it saved three files: the window with the symptoms applied, the same window ")
              .Append("without them, and the settings in use. The settings file reproduces the simulation when it is ")
              .Append("loaded into VIP-Sim. For each image the gaze point was fixed at the centre of the captured window.</p>\n");

            var distinct = new List<ViewingConditions>();
            foreach (var f in s.Findings)
                if (!distinct.Any(d => d.SameAs(f.Viewing))) distinct.Add(f.Viewing);
            if (distinct.Count == 1)
                sb.Append("<p>Viewing conditions: ").Append(H(distinct[0].Describe())).Append(".</p>\n");
            else if (distinct.Count > 1)
            {
                sb.Append("<p>The viewing conditions changed during the review:</p>\n<ul>\n");
                foreach (var d in distinct) sb.Append("<li>").Append(H(Capitalise(d.Describe()))).Append(".</li>\n");
                sb.Append("</ul>\n");
            }

            sb.Append("<p>Settings in logMAR, degrees of visual angle or prism dioptres were converted to pixels ")
              .Append("with these viewing conditions. Settings marked as uncalibrated are positions on the effect's ")
              .Append("own scale and correspond to no clinical measurement.</p>\n</section>\n");
        }

        private static void Limitations(StringBuilder sb)
        {
            sb.Append("<section aria-labelledby=\"limitations\">\n<h2 id=\"limitations\">Limitations</h2>\n<ul>\n")
              .Append("<li>Each effect approximates one symptom. Real conditions combine several symptoms, vary between ")
              .Append("people and change over time.</li>\n")
              .Append("<li>VIP-Sim was developed with seven people with vision impairments (Rädler, Colley and Rukzio, ")
              .Append("<a href=\"").Append(PaperDoi).Append("\">UIST 2025</a>). Seven people cannot represent everyone ")
              .Append("with a given condition.</li>\n")
              .Append("<li>Disability simulations can lower how capable observers judge disabled people to be ")
              .Append("(<a href=\"").Append(SilvermanDoi).Append("\">Silverman et al., 2015</a>) and can raise discomfort ")
              .Append("without raising willingness to interact (<a href=\"").Append(NarioRedmondDoi)
              .Append("\">Nario-Redmond et al., 2017</a>). Use these findings alongside testing with people with ")
              .Append("vision impairments.</li>\n")
              .Append("<li>WCAG success criteria are tested by measurement. A finding linked to a criterion points the ")
              .Append("team to that test, and the test decides conformance.</li>\n")
              .Append("</ul>\n</section>\n");
        }

        private const string Css = @"
:root{color-scheme:light dark;--bg:#fff;--fg:#1a1a1a;--muted:#555;--line:#d0d0d0;--accent:#0b57d0;--card:#f6f7f9;
--blocker:#a50e0e;--major:#b04a00;--minor:#5f5f00;--observation:#2f5f8a}
@media (prefers-color-scheme:dark){:root{--bg:#121212;--fg:#ececec;--muted:#b0b0b0;--line:#3a3a3a;--accent:#8ab4f8;--card:#1d1f22;
--blocker:#ff8a80;--major:#ffb74d;--minor:#e6e67a;--observation:#9ecbff}}
*{box-sizing:border-box}
body{margin:0;background:var(--bg);color:var(--fg);font:16px/1.55 system-ui,-apple-system,'Segoe UI',Roboto,sans-serif}
main{max-width:1100px;margin:0 auto;padding:32px 16px 64px}
a{color:var(--accent)}
h1{font-size:2rem;line-height:1.2;margin:.2em 0 .6em}
h2{font-size:1.4rem;margin:2.2em 0 .6em;padding-top:.6em;border-top:1px solid var(--line)}
h3{font-size:1.15rem;margin:0 0 .6em}
.kicker{margin:0;color:var(--muted);text-transform:uppercase;letter-spacing:.06em;font-size:.85rem}
.meta{display:flex;flex-wrap:wrap;gap:8px 32px;margin:0}
.meta div{min-width:140px}.meta dt{color:var(--muted);font-size:.85rem}.meta dd{margin:0;font-weight:600}
.scroll{overflow-x:auto;margin:1em 0}.scroll:focus-visible{outline:2px solid var(--accent);outline-offset:2px}
table{border-collapse:collapse;width:100%;font-size:.95rem}
h3,p,figcaption{overflow-wrap:anywhere}
caption{text-align:left;font-weight:600;margin-bottom:.4em}
th,td{border-bottom:1px solid var(--line);padding:6px 8px;text-align:left;vertical-align:top}
thead th{border-bottom:2px solid var(--line)}
td.num{font-variant-numeric:tabular-nums}
article{background:var(--card);border:1px solid var(--line);border-radius:8px;padding:20px;margin:0 0 24px}
.id{font-variant-numeric:tabular-nums}
.impact{display:inline-block;border:1.5px solid currentColor;border-radius:4px;padding:0 6px;font-size:.85rem;margin:0 4px}
.impact-blocker{color:var(--blocker)}.impact-major{color:var(--major)}.impact-minor{color:var(--minor)}.impact-observation{color:var(--observation)}
.pair{display:grid;grid-template-columns:repeat(auto-fit,minmax(280px,1fr));gap:16px;margin:16px 0}
figure{margin:0}figure img{width:100%;height:auto;border:1px solid var(--line);display:block}
figcaption{font-size:.9rem;color:var(--muted);margin-top:4px}
.muted{color:var(--muted)}.small{font-size:.9rem;color:var(--muted)}
footer{margin-top:3em;font-size:.9rem;color:var(--muted)}
@media print{:root{--bg:#fff;--fg:#000;--card:#fff}main{max-width:none;padding:0}article{break-inside:avoid}a{color:inherit}}
";
    }
}
