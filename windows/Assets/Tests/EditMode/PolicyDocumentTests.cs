using System.Collections.Generic;
using NUnit.Framework;
using VipSim.Policy;

namespace VipSim.Tests
{
    /// <summary>
    /// The policy file an IT department writes once for many machines. A typo there must
    /// cost one setting and a log line, never the whole policy or VIP-Sim's start.
    /// </summary>
    public class PolicyDocumentTests
    {
        private static readonly Dictionary<string, string> Env = new Dictionary<string, string>
        {
            ["USERPROFILE"] = @"C:\Users\pat",
            ["HOME"] = "/Users/pat",
        };

        private static PolicyDocument Parse(string json) =>
            PolicyDocument.Parse(json, n => Env.TryGetValue(n, out var v) ? v : null, "/Users/pat");

        [Test]
        public void ReadsEverySetting()
        {
            var p = Parse(@"{""updateCheck"": false, ""telemetryAllowed"": false, ""webcamAllowed"": false,
                             ""viewingDistanceCm"": 65, ""screenWidthCm"": 59.8,
                             ""reportFolder"": ""%USERPROFILE%\\Reviews"", ""profile"": ""~/p1.json""}");
            Assert.IsEmpty(p.Warnings);
            Assert.AreEqual(false, p.UpdateCheck);
            Assert.AreEqual(false, p.TelemetryAllowed);
            Assert.AreEqual(false, p.WebcamAllowed);
            Assert.AreEqual(65f, p.ViewingDistanceCm);
            Assert.AreEqual(59.8f, p.ScreenWidthCm.Value, 1e-4f);
            Assert.AreEqual(@"C:\Users\pat\Reviews", p.ReportFolder);
            Assert.AreEqual("/Users/pat/p1.json", p.Profile);
        }

        [Test]
        public void BadEntriesAreDroppedAndTheRestApply()
        {
            var p = Parse(@"{""updateCheck"": ""no"", ""viewingDistanceCm"": 5, ""screenWidthCm"": ""wide"",
                             ""webcamAllowed"": false, ""colour"": ""blue""}");
            Assert.IsNull(p.UpdateCheck);
            Assert.IsNull(p.ViewingDistanceCm);
            Assert.IsNull(p.ScreenWidthCm);
            Assert.AreEqual(false, p.WebcamAllowed);
            Assert.AreEqual(4, p.Warnings.Count);
        }

        [Test]
        public void BrokenJsonAppliesNothing()
        {
            var p = Parse("{ updateCheck: false,");
            Assert.IsTrue(p.IsEmpty);
            Assert.AreEqual(1, p.Warnings.Count);
        }

        [Test]
        public void ExpansionHandlesBothNotationsAndKeepsUnknownVariables()
        {
            string Expand(string s) => PolicyDocument.Expand(s, n => Env.TryGetValue(n, out var v) ? v : null, "/Users/pat");
            Assert.AreEqual("/Users/pat/r", Expand("$HOME/r"));
            Assert.AreEqual("/Users/pat/r", Expand("${HOME}/r"));
            Assert.AreEqual("%NOPE%/r", Expand("%NOPE%/r"));
            Assert.AreEqual("/Users/pat", Expand("~"));
        }

        [Test]
        public void RangesMatchTheViewingGeometry()
        {
            // ViewingGeometry lives in Assembly-CSharp, which tests cannot reference; these
            // must equal its MinDistanceCm, MaxDistanceCm, MinWidthCm and MaxWidthCm.
            Assert.AreEqual(20f, PolicyDocument.MinDistanceCm);
            Assert.AreEqual(300f, PolicyDocument.MaxDistanceCm);
            Assert.AreEqual(10f, PolicyDocument.MinWidthCm);
            Assert.AreEqual(500f, PolicyDocument.MaxWidthCm);
        }
    }
}
