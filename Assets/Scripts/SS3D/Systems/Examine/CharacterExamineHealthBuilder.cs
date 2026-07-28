using System;
using System.Collections.Generic;
using System.Text;
using SS3D.Localization;
using SS3D.Systems.Health;

namespace SS3D.Systems.Examine
{
    /// <summary>
    /// Builds qualitative health examine lines from a character's synced health state.
    /// Tier 0 (others): at most a state line + one consolidated appearance sentence.
    /// Tier 1 (self): per-zone / organ lines. Quiet when healthy.
    /// </summary>
    public static class CharacterExamineHealthBuilder
    {
        public const int MaxSections = 10;

        /// <summary>Organ function at or above this % is omitted from Tier 1 lines.</summary>
        public const float OrganHealthyPercent = 80f;

        public const float OrganStrainedPercent = 50f;
        public const float OrganFailingPercent = 20f;

        private enum LinePriority
        {
            State = 1,
            Severed = 2,
            Bleeding = 3,
            ZoneSeverity = 4,
            Organ = 5,
        }

        private readonly struct Candidate
        {
            public LinePriority Priority { get; }
            public int Order { get; }
            public string Text { get; }

            public Candidate(LinePriority priority, int order, string text)
            {
                Priority = priority;
                Order = order;
                Text = text;
            }
        }

        public static void AppendSections(
            HumanHealthController health,
            bool includeSelfDetail,
            List<ExamineSection> sections)
        {
            if (health == null || sections == null)
            {
                return;
            }

            AppendSections(health.Snapshot, health.DebugDetail, includeSelfDetail, sections);
        }

        public static void AppendSections(
            HealthSnapshot snapshot,
            HealthDebugDetail detail,
            bool includeSelfDetail,
            List<ExamineSection> sections)
        {
            if (sections == null)
            {
                return;
            }

            if (includeSelfDetail)
            {
                AppendSelfSections(snapshot, detail, sections);
            }
            else
            {
                AppendOtherSections(snapshot, detail, sections);
            }
        }

        private static void AppendOtherSections(
            HealthSnapshot snapshot,
            HealthDebugDetail detail,
            List<ExamineSection> sections)
        {
            // Dead: state only — no wound laundry list on a corpse glance.
            if (snapshot.State == HealthState.Dead)
            {
                sections.Add(new ExamineSection(Localized(
                    ExamineHealthKeys.DeadOther,
                    null,
                    ExamineHealthKeys.DeadOtherFallback)));
                return;
            }

            AppendStateLine(snapshot, isSelf: false, sections);

            string appearance = BuildOtherAppearanceLine(snapshot, detail);
            if (!string.IsNullOrEmpty(appearance))
            {
                sections.Add(new ExamineSection(appearance));
            }
        }

        private static string BuildOtherAppearanceLine(HealthSnapshot snapshot, HealthDebugDetail detail)
        {
            List<string> bleedingZones = new(HealthConstants.ZoneCount);
            List<string> missingZones = new(HealthConstants.ZoneCount);
            bool hurt = false;

            for (int i = 0; i < HealthConstants.ZoneCount; i++)
            {
                BodyZone zone = (BodyZone)i;
                if (snapshot.IsZoneSevered(zone))
                {
                    missingZones.Add(ZoneNameLower(zone));
                    continue;
                }

                if (snapshot.IsZoneBleeding(zone))
                {
                    bleedingZones.Add(ZoneNameLower(zone));
                }

                ZoneDamageState state = detail.GetZone(zone);
                if (state.Severity >= WoundSeverity.Wound)
                {
                    hurt = true;
                }
            }

            bool missing = missingZones.Count > 0;
            bool bleeding = bleedingZones.Count > 0;
            if (!missing && !hurt && !bleeding)
            {
                return null;
            }

            string missingList = FormatZoneList(missingZones);
            string bleedingList = FormatZoneList(bleedingZones);

            if (missing && hurt && bleeding)
            {
                return Localized(
                    ExamineHealthKeys.OtherMissingAndHurtAndBleeding,
                    new object[] { missingList, bleedingList },
                    string.Format(
                        ExamineHealthKeys.OtherMissingAndHurtAndBleedingFallback,
                        missingList,
                        bleedingList));
            }

            if (missing && bleeding)
            {
                return Localized(
                    ExamineHealthKeys.OtherMissingAndBleeding,
                    new object[] { missingList, bleedingList },
                    string.Format(
                        ExamineHealthKeys.OtherMissingAndBleedingFallback,
                        missingList,
                        bleedingList));
            }

            if (missing && hurt)
            {
                return Localized(
                    ExamineHealthKeys.OtherMissingAndHurt,
                    new object[] { missingList },
                    string.Format(ExamineHealthKeys.OtherMissingAndHurtFallback, missingList));
            }

            if (missing)
            {
                return Localized(
                    ExamineHealthKeys.OtherMissing,
                    new object[] { missingList },
                    string.Format(ExamineHealthKeys.OtherMissingFallback, missingList));
            }

            if (hurt && bleeding)
            {
                return Localized(
                    ExamineHealthKeys.OtherHurtAndBleeding,
                    new object[] { bleedingList },
                    string.Format(ExamineHealthKeys.OtherHurtAndBleedingFallback, bleedingList));
            }

            if (bleeding)
            {
                return Localized(
                    ExamineHealthKeys.OtherBleeding,
                    new object[] { bleedingList },
                    string.Format(ExamineHealthKeys.OtherBleedingFallback, bleedingList));
            }

            return Localized(
                ExamineHealthKeys.OtherHurt,
                null,
                ExamineHealthKeys.OtherHurtFallback);
        }

        private static string FormatZoneList(List<string> zones)
        {
            if (zones == null || zones.Count == 0)
            {
                return string.Empty;
            }

            if (zones.Count == 1)
            {
                return zones[0];
            }

            if (zones.Count == 2)
            {
                return $"{zones[0]} and {zones[1]}";
            }

            StringBuilder builder = new();
            for (int i = 0; i < zones.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(i == zones.Count - 1 ? ", and " : ", ");
                }

                builder.Append(zones[i]);
            }

            return builder.ToString();
        }

        private static void AppendSelfSections(
            HealthSnapshot snapshot,
            HealthDebugDetail detail,
            List<ExamineSection> sections)
        {
            List<Candidate> candidates = new(MaxSections * 2);
            CollectState(snapshot, isSelf: true, candidates);
            CollectSevered(snapshot, candidates);
            CollectBleeding(snapshot, candidates);
            CollectZoneSeverity(detail, includeSelfDetail: true, candidates);
            CollectOrgans(detail, candidates);

            if (candidates.Count == 0)
            {
                return;
            }

            candidates.Sort(CompareCandidates);
            int count = Math.Min(candidates.Count, MaxSections);
            for (int i = 0; i < count; i++)
            {
                sections.Add(new ExamineSection(candidates[i].Text));
            }
        }

        private static void AppendStateLine(HealthSnapshot snapshot, bool isSelf, List<ExamineSection> sections)
        {
            List<Candidate> candidates = new(1);
            CollectState(snapshot, isSelf, candidates);
            if (candidates.Count > 0)
            {
                sections.Add(new ExamineSection(candidates[0].Text));
            }
        }

        private static int CompareCandidates(Candidate a, Candidate b)
        {
            int byPriority = ((int)a.Priority).CompareTo((int)b.Priority);
            return byPriority != 0 ? byPriority : a.Order.CompareTo(b.Order);
        }

        private static void CollectState(HealthSnapshot snapshot, bool isSelf, List<Candidate> candidates)
        {
            if (snapshot.State == HealthState.Dead)
            {
                candidates.Add(new Candidate(
                    LinePriority.State,
                    0,
                    Localized(isSelf ? ExamineHealthKeys.DeadSelf : ExamineHealthKeys.DeadOther,
                        null,
                        isSelf ? ExamineHealthKeys.DeadSelfFallback : ExamineHealthKeys.DeadOtherFallback)));
                return;
            }

            if (snapshot.State == HealthState.CardiacArrest || snapshot.IsCardiacArrest)
            {
                candidates.Add(new Candidate(
                    LinePriority.State,
                    0,
                    Localized(
                        isSelf ? ExamineHealthKeys.CardiacArrestSelf : ExamineHealthKeys.CardiacArrestOther,
                        null,
                        isSelf
                            ? ExamineHealthKeys.CardiacArrestSelfFallback
                            : ExamineHealthKeys.CardiacArrestOtherFallback)));
                return;
            }

            if (snapshot.State == HealthState.Critical)
            {
                candidates.Add(new Candidate(
                    LinePriority.State,
                    0,
                    Localized(
                        isSelf ? ExamineHealthKeys.CriticalSelf : ExamineHealthKeys.CriticalOther,
                        null,
                        isSelf ? ExamineHealthKeys.CriticalSelfFallback : ExamineHealthKeys.CriticalOtherFallback)));
                return;
            }

            if (!snapshot.IsConscious)
            {
                candidates.Add(new Candidate(
                    LinePriority.State,
                    0,
                    Localized(
                        isSelf ? ExamineHealthKeys.UnconsciousSelf : ExamineHealthKeys.UnconsciousOther,
                        null,
                        isSelf
                            ? ExamineHealthKeys.UnconsciousSelfFallback
                            : ExamineHealthKeys.UnconsciousOtherFallback)));
            }
        }

        private static void CollectSevered(HealthSnapshot snapshot, List<Candidate> candidates)
        {
            for (int i = 0; i < HealthConstants.ZoneCount; i++)
            {
                BodyZone zone = (BodyZone)i;
                if (!snapshot.IsZoneSevered(zone))
                {
                    continue;
                }

                string zoneName = ZoneNameLower(zone);
                candidates.Add(new Candidate(
                    LinePriority.Severed,
                    i,
                    Localized(
                        ExamineHealthKeys.Severed,
                        new object[] { zoneName },
                        string.Format(ExamineHealthKeys.SeveredFallback, zoneName))));
            }
        }

        private static void CollectBleeding(HealthSnapshot snapshot, List<Candidate> candidates)
        {
            for (int i = 0; i < HealthConstants.ZoneCount; i++)
            {
                BodyZone zone = (BodyZone)i;
                if (!snapshot.IsZoneBleeding(zone))
                {
                    continue;
                }

                string zoneName = ZoneNameLower(zone);
                candidates.Add(new Candidate(
                    LinePriority.Bleeding,
                    i,
                    Localized(
                        ExamineHealthKeys.Bleeding,
                        new object[] { zoneName },
                        string.Format(ExamineHealthKeys.BleedingFallback, zoneName))));
            }
        }

        private static void CollectZoneSeverity(
            HealthDebugDetail detail,
            bool includeSelfDetail,
            List<Candidate> candidates)
        {
            WoundSeverity minSeverity = includeSelfDetail ? WoundSeverity.Bruised : WoundSeverity.Wound;

            for (int i = 0; i < HealthConstants.ZoneCount; i++)
            {
                BodyZone zone = (BodyZone)i;
                ZoneDamageState state = detail.GetZone(zone);
                if (state.IsSevered)
                {
                    continue;
                }

                if (state.Severity < minSeverity)
                {
                    continue;
                }

                string zoneName = ZoneNameTitle(zone);
                string severityWord = SeverityWord(state.Severity);
                candidates.Add(new Candidate(
                    LinePriority.ZoneSeverity,
                    i,
                    Localized(
                        ExamineHealthKeys.ZoneSeverity,
                        new object[] { zoneName, severityWord },
                        string.Format(ExamineHealthKeys.ZoneSeverityFallback, zoneName, severityWord))));
            }
        }

        private static void CollectOrgans(HealthDebugDetail detail, List<Candidate> candidates)
        {
            TryAddOrgan(detail.GetOrgan(OrganType.Brain), "Brain", 0, candidates);
            TryAddOrgan(detail.GetOrgan(OrganType.Heart), "Heart", 1, candidates);

            float leftLung = detail.GetOrgan(OrganType.LeftLung).FunctionPercent;
            float rightLung = detail.GetOrgan(OrganType.RightLung).FunctionPercent;
            float lungs = Math.Min(leftLung, rightLung);
            TryAddOrganPercent(lungs, "Lungs", 2, candidates);

            TryAddOrgan(detail.GetOrgan(OrganType.Liver), "Liver", 3, candidates);
        }

        private static void TryAddOrgan(OrganState organ, string displayName, int order, List<Candidate> candidates)
        {
            TryAddOrganPercent(organ.FunctionPercent, displayName, order, candidates);
        }

        private static void TryAddOrganPercent(
            float functionPercent,
            string displayName,
            int order,
            List<Candidate> candidates)
        {
            if (functionPercent >= OrganHealthyPercent)
            {
                return;
            }

            string key;
            string fallback;
            if (functionPercent <= 0f)
            {
                key = ExamineHealthKeys.OrganDestroyed;
                fallback = string.Format(ExamineHealthKeys.OrganDestroyedFallback, displayName);
            }
            else if (functionPercent < OrganFailingPercent)
            {
                key = ExamineHealthKeys.OrganCritical;
                fallback = string.Format(ExamineHealthKeys.OrganCriticalFallback, displayName);
            }
            else if (functionPercent < OrganStrainedPercent)
            {
                key = ExamineHealthKeys.OrganFailing;
                fallback = string.Format(ExamineHealthKeys.OrganFailingFallback, displayName);
            }
            else
            {
                key = ExamineHealthKeys.OrganStrained;
                fallback = string.Format(ExamineHealthKeys.OrganStrainedFallback, displayName);
            }

            candidates.Add(new Candidate(
                LinePriority.Organ,
                order,
                Localized(key, new object[] { displayName }, fallback)));
        }

        private static string Localized(string key, object[] args, string fallback)
        {
            string result = LocalizedTextService.GetFormattedString(
                ExamineCanonicalKeyGenerator.ExamineTableName,
                key,
                args,
                fallback);

            // Editor FormatMissing ignores englishFallback; keep examine readable in tests / before keys load.
            if (!string.IsNullOrEmpty(result)
                && result.StartsWith("[MISSING:", StringComparison.Ordinal)
                && !string.IsNullOrEmpty(fallback))
            {
                return fallback;
            }

            return result;
        }

        private static string ZoneNameLower(BodyZone zone)
        {
            return zone switch
            {
                BodyZone.Head => "head",
                BodyZone.Chest => "chest",
                BodyZone.LeftArm => "left arm",
                BodyZone.RightArm => "right arm",
                BodyZone.LeftLeg => "left leg",
                BodyZone.RightLeg => "right leg",
                BodyZone.Groin => "groin",
                _ => "body",
            };
        }

        private static string ZoneNameTitle(BodyZone zone)
        {
            return zone switch
            {
                BodyZone.Head => "Head",
                BodyZone.Chest => "Chest",
                BodyZone.LeftArm => "Left arm",
                BodyZone.RightArm => "Right arm",
                BodyZone.LeftLeg => "Left leg",
                BodyZone.RightLeg => "Right leg",
                BodyZone.Groin => "Groin",
                _ => "Body",
            };
        }

        private static string SeverityWord(WoundSeverity severity)
        {
            return severity switch
            {
                WoundSeverity.Bruised => "bruised",
                WoundSeverity.Wound => "wounded",
                WoundSeverity.Severe => "badly damaged",
                WoundSeverity.Disabled => "useless",
                WoundSeverity.Severed => "missing",
                _ => "hurt",
            };
        }
    }
}
