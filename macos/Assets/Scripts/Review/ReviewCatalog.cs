using System;
using System.Collections.Generic;

namespace VipSim.Review
{
    /// <summary>
    /// Names, units and requirements the report needs and the profile does not carry.
    /// Symptom names mirror docs/EFFECTS.md; the filter keys are ProfileBinder's.
    /// </summary>
    public static class ReviewCatalog
    {
        public const string EnStandard = "EN 301 549 V3.2.1";

        private static readonly Dictionary<string, (string Label, string Term)> Names =
            new Dictionary<string, (string, string)>(StringComparer.Ordinal)
            {
                ["field_loss"] = ("Vision Loss, Central", "Central scotoma"),
                ["foveal_darkness"] = ("Central Dark Spot", "Foveal darkness"),
                ["pixelation"] = ("Detail Loss", "Reduced acuity"),
                ["vignette"] = ("Vision Loss, Peripheral", "Peripheral scotoma"),
                ["detail_loss"] = ("In-Filling", "Perceptual filling-in"),
                ["distortion"] = ("Wavy Distortion", "Metamorphopsia"),
                ["wiggle"] = ("Wavy Distortion II", "Metamorphopsia (variant)"),
                ["vortex"] = ("Distortion", "Geometric distortion"),
                ["blur"] = ("Farsightedness", "Hyperopia"),
                ["cataracts"] = ("Cataract", "Cataract"),
                ["cvd"] = ("Color Vision Deficiency", "Dyschromatopsia"),
                ["bcg"] = ("Contrast Sensitivity", "Reduced contrast sensitivity"),
                ["bloom"] = ("Glare Vision/Photophobia", "Photophobia"),
                ["nystagmus"] = ("Eye Tremor", "Nystagmus"),
                ["double_vision"] = ("Double Vision", "Diplopia"),
                ["floaters"] = ("Retinopathy/Floaters", "Vitreous floaters"),
                ["flickering_stars"] = ("Flickering Specks", "Photopsia"),
                ["teichopsia"] = ("Visual Aura", "Teichopsia"),
            };

        public static (string Label, string Term) SymptomNames(string filter) =>
            filter != null && Names.TryGetValue(filter, out var n) ? n : (filter ?? "", "");

        /// <summary>
        /// The physical unit a profile parameter is stored in, from its name. Every other
        /// parameter is a position on the effect's own scale. The report labels those values
        /// as uncalibrated, so a bare number cannot pass for a measurement.
        /// </summary>
        public static string UnitFor(string parameter)
        {
            if (string.IsNullOrEmpty(parameter)) return null;
            if (parameter == "logMAR") return "logMAR";
            if (parameter == "direction_deg") return "degrees (direction)";
            if (parameter.EndsWith("_deg", StringComparison.Ordinal)) return "degrees of visual angle";
            if (parameter.EndsWith("_pd", StringComparison.Ordinal)) return "prism dioptres";
            return null;
        }

        private const string Wcag = "https://www.w3.org/WAI/WCAG22/Understanding/";
        private const string En =
            "https://www.etsi.org/deliver/etsi_en/301500_301599/301549/03.02.01_60/en_301549v030201p.pdf";

        /// <summary>
        /// Requirements a low-vision finding is usually filed against. The two EN 301 549
        /// functional performance statements come first: they are the clauses a simulation
        /// review speaks to directly. The WCAG criteria are tested by measurement. A finding
        /// tagged with one points the team to that test, and the test decides conformance.
        /// </summary>
        public static readonly IReadOnlyList<Criterion> Criteria = new[]
        {
            C("en-4.2.2", "4.2.2 Usage with limited vision", "", "4.2.2", En),
            C("en-4.2.4", "4.2.4 Usage without perception of colour", "", "4.2.4", En),
            C("wcag-1.3.3", "1.3.3 Sensory Characteristics", "A", "9.1.3.3, 11.1.3.3", Wcag + "sensory-characteristics"),
            C("wcag-1.4.1", "1.4.1 Use of Color", "A", "9.1.4.1, 11.1.4.1", Wcag + "use-of-color"),
            C("wcag-1.4.3", "1.4.3 Contrast (Minimum)", "AA", "9.1.4.3, 11.1.4.3", Wcag + "contrast-minimum"),
            C("wcag-1.4.4", "1.4.4 Resize Text", "AA", "9.1.4.4, 11.1.4.4.1", Wcag + "resize-text"),
            C("wcag-1.4.10", "1.4.10 Reflow", "AA", "9.1.4.10, 11.1.4.10", Wcag + "reflow"),
            C("wcag-1.4.11", "1.4.11 Non-text Contrast", "AA", "9.1.4.11, 11.1.4.11", Wcag + "non-text-contrast"),
            C("wcag-1.4.12", "1.4.12 Text Spacing", "AA", "9.1.4.12, 11.1.4.12", Wcag + "text-spacing"),
            C("wcag-1.4.13", "1.4.13 Content on Hover or Focus", "AA", "9.1.4.13, 11.1.4.13", Wcag + "content-on-hover-or-focus"),
            C("wcag-2.4.7", "2.4.7 Focus Visible", "AA", "9.2.4.7, 11.2.4.7", Wcag + "focus-visible"),
            C("wcag-2.4.11", "2.4.11 Focus Not Obscured (Minimum)", "AA", "", Wcag + "focus-not-obscured-minimum"),
            C("beyond", "Not covered by a success criterion", "", "", ""),
        };

        public static Criterion Find(string id)
        {
            foreach (var c in Criteria)
                if (c.Id == id) return c;
            return null;
        }

        private static Criterion C(string id, string name, string level, string en, string url) =>
            new Criterion { Id = id, Name = name, Level = level, En301549 = en, Url = url };
    }
}
