using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SS3D.Systems.Examine;
using SS3D.Systems.Health;

namespace EditorTests
{
    public class CharacterExamineHealthBuilderTests
    {
        private static HealthDebugDetail HealthyDetail()
        {
            return new HealthDebugDetail
            {
                Brain = OrganState.Default(OrganType.Brain),
                Heart = OrganState.Default(OrganType.Heart),
                LeftLung = OrganState.Default(OrganType.LeftLung),
                RightLung = OrganState.Default(OrganType.RightLung),
                Liver = OrganState.Default(OrganType.Liver),
            };
        }

        [Test]
        public void HealthySnapshotProducesNoSections()
        {
            List<ExamineSection> sections = new();

            CharacterExamineHealthBuilder.AppendSections(
                HealthSnapshot.Default,
                HealthyDetail(),
                includeSelfDetail: true,
                sections);

            Assert.IsEmpty(sections);
        }

        [Test]
        public void PublicTierUsesSingleConsolidatedAppearanceLine()
        {
            HealthSnapshot snapshot = HealthSnapshot.Default;
            snapshot.IsBleeding = true;
            snapshot.BleedingZoneMask = (1 << (int)BodyZone.Chest) | (1 << (int)BodyZone.Head);
            snapshot.SeveredZoneMask = 1 << (int)BodyZone.LeftLeg;

            HealthDebugDetail detail = HealthyDetail();
            detail.LeftArm = new ZoneDamageState
            {
                Severity = WoundSeverity.Wound,
                Brute = 20f,
            };
            detail.Head = new ZoneDamageState
            {
                Severity = WoundSeverity.Wound,
                Brute = 20f,
            };
            detail.RightArm = new ZoneDamageState
            {
                Severity = WoundSeverity.Bruised,
                Brute = 5f,
            };

            List<ExamineSection> sections = new();
            CharacterExamineHealthBuilder.AppendSections(snapshot, detail, includeSelfDetail: false, sections);

            Assert.AreEqual(1, sections.Count);
            string line = sections[0].Text;
            Assert.IsTrue(line.Contains("missing his left leg"), line);
            Assert.IsTrue(line.Contains("hurt"), line);
            Assert.IsTrue(line.Contains("bleeding from the"), line);
            Assert.IsTrue(line.Contains("head") && line.Contains("chest"), line);
            Assert.IsFalse(line.Contains("Left arm"), line);
            Assert.IsFalse(line.Contains("wounded"), line);
            Assert.IsFalse(sections.Any(s => s.Text.Contains("Brain") || s.Text.Contains("Heart")));
        }

        [Test]
        public void PublicTierHurtAndBleedingWithoutMissing()
        {
            HealthSnapshot snapshot = HealthSnapshot.Default;
            snapshot.IsBleeding = true;
            snapshot.BleedingZoneMask = (1 << (int)BodyZone.Chest) | (1 << (int)BodyZone.Head);

            HealthDebugDetail detail = HealthyDetail();
            detail.Chest = new ZoneDamageState { Severity = WoundSeverity.Wound };

            List<ExamineSection> sections = new();
            CharacterExamineHealthBuilder.AppendSections(snapshot, detail, includeSelfDetail: false, sections);

            Assert.AreEqual(1, sections.Count);
            Assert.AreEqual(
                "He seems to be hurt and bleeding from the head and chest.",
                sections[0].Text);
        }

        [Test]
        public void SelfTierAddsBruiseAndOrganBands()
        {
            HealthSnapshot snapshot = HealthSnapshot.Default;
            HealthDebugDetail detail = HealthyDetail();
            detail.RightArm = new ZoneDamageState
            {
                Severity = WoundSeverity.Bruised,
                Brute = 5f,
            };
            detail.Heart = new OrganState
            {
                Type = OrganType.Heart,
                FunctionPercent = 60f,
            };

            List<ExamineSection> sections = new();
            CharacterExamineHealthBuilder.AppendSections(snapshot, detail, includeSelfDetail: true, sections);

            Assert.IsTrue(sections.Any(s => s.Text.Contains("Right arm") && s.Text.Contains("bruised")));
            Assert.IsTrue(sections.Any(s => s.Text.Contains("Heart") && s.Text.Contains("strained")));
        }

        [Test]
        public void DeadUsesSelfPronounWhenSelf()
        {
            HealthSnapshot snapshot = HealthSnapshot.Default;
            snapshot.State = HealthState.Dead;
            snapshot.IsConscious = false;

            List<ExamineSection> selfSections = new();
            CharacterExamineHealthBuilder.AppendSections(
                snapshot,
                HealthyDetail(),
                includeSelfDetail: true,
                selfSections);

            List<ExamineSection> otherSections = new();
            CharacterExamineHealthBuilder.AppendSections(
                snapshot,
                HealthyDetail(),
                includeSelfDetail: false,
                otherSections);

            Assert.AreEqual(1, selfSections.Count);
            Assert.AreEqual("You are dead.", selfSections[0].Text);
            Assert.AreEqual(1, otherSections.Count);
            Assert.AreEqual("He is dead.", otherSections[0].Text);
        }

        [Test]
        public void CapTruncatesLowestPriorityLines()
        {
            HealthSnapshot snapshot = HealthSnapshot.Default;
            snapshot.State = HealthState.Critical;
            snapshot.IsBleeding = true;
            snapshot.BleedingZoneMask = (1 << HealthConstants.ZoneCount) - 1;
            snapshot.SeveredZoneMask = (1 << (int)BodyZone.LeftArm)
                | (1 << (int)BodyZone.RightArm)
                | (1 << (int)BodyZone.LeftLeg)
                | (1 << (int)BodyZone.RightLeg);

            HealthDebugDetail detail = HealthyDetail();
            detail.Head = new ZoneDamageState { Severity = WoundSeverity.Severe };
            detail.Chest = new ZoneDamageState { Severity = WoundSeverity.Wound };
            detail.Groin = new ZoneDamageState { Severity = WoundSeverity.Wound };
            detail.Heart = new OrganState { Type = OrganType.Heart, FunctionPercent = 10f };
            detail.Brain = new OrganState { Type = OrganType.Brain, FunctionPercent = 40f };
            detail.Liver = new OrganState { Type = OrganType.Liver, FunctionPercent = 55f };
            detail.LeftLung = new OrganState { Type = OrganType.LeftLung, FunctionPercent = 30f };
            detail.RightLung = new OrganState { Type = OrganType.RightLung, FunctionPercent = 30f };

            List<ExamineSection> sections = new();
            CharacterExamineHealthBuilder.AppendSections(snapshot, detail, includeSelfDetail: true, sections);

            Assert.AreEqual(CharacterExamineHealthBuilder.MaxSections, sections.Count);
            Assert.IsTrue(sections[0].Text.Contains("critical"));
            Assert.IsFalse(sections.Any(s => s.Text.Contains("Liver")));
        }

        [Test]
        public void NullHealthControllerAppendsNothing()
        {
            List<ExamineSection> sections = new();
            CharacterExamineHealthBuilder.AppendSections((HumanHealthController)null, true, sections);
            Assert.IsEmpty(sections);
        }
    }
}
