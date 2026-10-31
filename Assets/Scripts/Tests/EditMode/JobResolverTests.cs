using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SS3D.Systems.Roles;

namespace EditorTests
{
    public class JobResolverTests
    {
        private const string Fallback = "Assistant";

        private static readonly JobSlotInfo[] Jobs =
        {
            new() { Name = Fallback, Slots = 0 },
            new() { Name = "Security", Slots = 1 },
            new() { Name = "Engineer", Slots = 2 },
        };

        private static Dictionary<string, IReadOnlyDictionary<string, JobPriority>> Prefs(
            params (string Player, string Job, JobPriority Priority)[] entries)
        {
            Dictionary<string, IReadOnlyDictionary<string, JobPriority>> result = new();
            foreach (IGrouping<string, (string Player, string Job, JobPriority Priority)> player in entries.GroupBy(e => e.Player))
            {
                result[player.Key] = player.ToDictionary(e => e.Job, e => e.Priority);
            }

            return result;
        }

        [Test]
        public void Resolve_NoPreferences_AllGetFallback()
        {
            Dictionary<string, string> result = JobResolver.Resolve(
                new[] { "a", "b" }, Prefs(), Jobs, Fallback, new System.Random(1));

            Assert.AreEqual(Fallback, result["a"]);
            Assert.AreEqual(Fallback, result["b"]);
        }

        [Test]
        public void Resolve_RespectsSlotCap_OverflowFallsBack()
        {
            Dictionary<string, string> result = JobResolver.Resolve(
                new[] { "a", "b", "c" },
                Prefs(("a", "Security", JobPriority.High), ("b", "Security", JobPriority.High), ("c", "Security", JobPriority.High)),
                Jobs, Fallback, new System.Random(2));

            Assert.AreEqual(1, result.Values.Count(job => job == "Security"));
            Assert.AreEqual(2, result.Values.Count(job => job == Fallback));
        }

        [Test]
        public void Resolve_HighBeatsMediumForTheSameSlot()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                Dictionary<string, string> result = JobResolver.Resolve(
                    new[] { "medium", "high" },
                    Prefs(("medium", "Security", JobPriority.Medium), ("high", "Security", JobPriority.High)),
                    Jobs, Fallback, new System.Random(seed));

                Assert.AreEqual("Security", result["high"], $"seed {seed}");
                Assert.AreEqual(Fallback, result["medium"], $"seed {seed}");
            }
        }

        [Test]
        public void Resolve_FullFirstChoice_MovesDownTheList()
        {
            Dictionary<string, string> result = JobResolver.Resolve(
                new[] { "a", "b" },
                Prefs(
                    ("a", "Security", JobPriority.High),
                    ("b", "Security", JobPriority.High),
                    ("a", "Engineer", JobPriority.Low),
                    ("b", "Engineer", JobPriority.Low)),
                Jobs, Fallback, new System.Random(3));

            CollectionAssert.AreEquivalent(new[] { "Security", "Engineer" }, result.Values);
        }

        [Test]
        public void Resolve_NeverIsNotAssigned()
        {
            Dictionary<string, string> result = JobResolver.Resolve(
                new[] { "a" }, Prefs(("a", "Security", JobPriority.Never)), Jobs, Fallback, new System.Random(4));

            Assert.AreEqual(Fallback, result["a"]);
        }

        [Test]
        public void Resolve_FallbackIgnoresItsOwnCap()
        {
            JobSlotInfo[] jobs = { new() { Name = Fallback, Slots = 1 } };

            Dictionary<string, string> result = JobResolver.Resolve(
                new[] { "a", "b", "c" }, Prefs(), jobs, Fallback, new System.Random(5));

            Assert.IsTrue(result.Values.All(job => job == Fallback));
        }

        [Test]
        public void ResolveLateJoin_SkipsFullJobs()
        {
            JobSlotInfo[] jobs =
            {
                new() { Name = Fallback, Slots = 0 },
                new() { Name = "Security", Slots = 1, Assigned = 1 },
                new() { Name = "Engineer", Slots = 2, Assigned = 1 },
            };
            Dictionary<string, JobPriority> prefs = new()
            {
                ["Security"] = JobPriority.High,
                ["Engineer"] = JobPriority.Low,
            };

            Assert.AreEqual("Engineer", JobResolver.ResolveLateJoin(prefs, jobs, Fallback, new System.Random(6)));
        }

        [Test]
        public void ResolveLateJoin_NoPreferences_ReturnsFallback()
        {
            Assert.AreEqual(Fallback, JobResolver.ResolveLateJoin(null, Jobs, Fallback, new System.Random(7)));
        }
    }
}
