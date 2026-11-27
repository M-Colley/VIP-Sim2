using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using VipSim.Review;

namespace VipSim.Tests
{
    /// <summary>
    /// The review model and its writers. These files go into issue trackers and compliance
    /// records, so a broken quote or an unescaped note is a defect someone else inherits.
    /// </summary>
    public class ReviewReportTests
    {
        private static ReviewSession Sample()
        {
            var profile = JObject.Parse(@"{""filters"":[
                {""filter"":""vignette"",""overlay_scale"":0.62,""overlayScale"":0.62},
                {""filter"":""blur"",""logMAR"":0.7},
                {""filter"":""nystagmus"",""axis"":""horizontal"",""amp_deg"":4.5}]}");
            var s = new ReviewSession
            {
                Product = "Checkout <beta> & \"quotes\"",
                Reviewer = "R",
                Started = new DateTimeOffset(2026, 11, 27, 14, 0, 0, TimeSpan.Zero),
                AppVersion = "test",
            };
            s.Findings.Add(new Finding
            {
                Id = s.NextId(),
                Captured = s.Started,
                Note = "Pay button, \"Pay now\", at the edge.\n<script>x</script>",
                Impact = Impact.Blocker,
                Criteria = { "en-4.2.2", "wcag-1.4.3" },
                Symptoms = Symptom.FromProfile(profile),
                SimulatedImage = "F01-simulated.png",
                OriginalImage = "F01-original.png",
                Viewing = new ViewingConditions { DistanceCm = 60, ScreenWidthCm = 53.1, PixelsAcross = 1920 },
            });
            return s;
        }

        [Test]
        public void SymptomsCarryNamesUnitsAndNoDuplicates()
        {
            var symptoms = Sample().Findings[0].Symptoms;
            Assert.AreEqual("Vision Loss, Peripheral", symptoms[0].Label);
            Assert.AreEqual(1, symptoms[0].Parameters.Count, "overlay_scale and overlayScale are one setting");
            Assert.IsNull(symptoms[0].Parameters[0].Unit);
            Assert.AreEqual("logMAR", symptoms[1].Parameters[0].Unit);
            Assert.AreEqual("degrees of visual angle", symptoms[2].Parameters.Single(p => p.Name == "amp_deg").Unit);
        }

        [Test]
        public void JsonRoundTripsAndRefusesForeignFiles()
        {
            string json = ReviewReportWriter.Json(Sample());
            Assert.AreEqual(json, ReviewReportWriter.Json(ReviewReportWriter.Load(json)));
            Assert.Throws<FormatException>(() => ReviewReportWriter.Load("{\"schema\":\"other\"}"));
        }

        [Test]
        public void CsvQuotesEveryCellThatNeedsIt()
        {
            string csv = ReviewReportWriter.Csv(Sample());
            StringAssert.StartsWith("Summary,Description,Priority,Labels,", csv);
            StringAssert.Contains("\"Pay button, \"\"Pay now\"\", at the edge.", csv);
            StringAssert.Contains(",Highest,vip-sim low-vision en-4.2.2 wcag-1.4.3,", csv);
        }

        [Test]
        public void HtmlEscapesUserText()
        {
            string html = ReviewReportWriter.Html(Sample(), DateTimeOffset.UnixEpoch);
            StringAssert.DoesNotContain("<script>", html);
            StringAssert.Contains("&lt;script&gt;", html);
            StringAssert.Contains("Checkout &lt;beta&gt; &amp; &quot;quotes&quot;", html);
        }

        [Test]
        public void FindingNumbersAreNeverReused()
        {
            var s = Sample();
            s.Findings.Add(new Finding { Id = s.NextId() });
            s.Findings.RemoveAt(0);
            Assert.AreEqual("F03", s.NextId());
        }

        [Test]
        public void EveryCriterionIdIsUnique()
        {
            var ids = ReviewCatalog.Criteria.Select(c => c.Id).ToList();
            CollectionAssert.AllItemsAreUnique(ids);
        }
    }
}
