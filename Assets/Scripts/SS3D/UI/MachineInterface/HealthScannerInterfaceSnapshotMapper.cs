using SS3D.Systems.Health;

namespace SS3D.UI.MachineInterface
{
    public static class HealthScannerInterfaceSnapshotMapper
    {
        private const float NominalBloodVolumeCc = 560f;

        private const float ZoneWarningSeverity = 15f;

        private const float ZoneDangerSeverity = 40f;

        public static HealthScannerInterfaceViewModel ToViewModel(HealthScannerInterfaceSnapshot snapshot)
        {
            HealthScannerInterfaceViewModel model = new()
            {
                Title = snapshot.Title,
                ModelLabel = snapshot.ModelLabel,
                Subtitle = snapshot.Subtitle,
                ChassisPowerOk = snapshot.PowerOk,
            };

            if (!snapshot.HasSubject)
            {
                model.Scenario = HealthScannerScenario.NoSubject;
                model.StatusBadgeText = "NO SUBJECT";
                model.StatusHeadline = "Awaiting Subject";
                model.StatusSubline = "Position a patient within scan range.";
                ApplyEmptyZones(model);
                ApplyEmptySystemics(model);
                return model;
            }

            MapZones(snapshot, model);
            MapSystemics(snapshot, model);
            MapOrgans(snapshot, model);
            MapScenario(snapshot, model);

            return model;
        }

        private static void MapScenario(HealthScannerInterfaceSnapshot snapshot, HealthScannerInterfaceViewModel model)
        {
            HealthState state = (HealthState)snapshot.HealthState;

            if (state == HealthState.Dead)
            {
                model.Scenario = HealthScannerScenario.Critical;
                model.StatusBadgeText = "DECEASED";
                model.StatusHeadline = "Subject Deceased";
                model.StatusSubline = "No vital signs detected.";
                return;
            }

            if (state is HealthState.Critical or HealthState.CardiacArrest || !snapshot.IsConscious)
            {
                model.Scenario = HealthScannerScenario.Critical;
                model.StatusBadgeText = "CRITICAL";
                model.StatusHeadline = snapshot.IsCardiacArrest ? "Cardiac Arrest" : "Critical — Multiple Systems Flagged";
                model.StatusSubline = snapshot.CanDefibrillate
                    ? "Defibrillation window open."
                    : "Immediate treatment required.";
                return;
            }

            bool anyImpaired = model.CnsTone != StatusTone.Neutral
                || model.ToxinTone != StatusTone.Neutral
                || model.SpO2Tone != StatusTone.Neutral
                || model.BloodTone != StatusTone.Neutral;

            if (anyImpaired)
            {
                model.Scenario = HealthScannerScenario.Impaired;
                model.StatusBadgeText = "IMPAIRED";
                model.StatusHeadline = "Vitals Impaired";
                model.StatusSubline = "One or more systems outside nominal range.";
                return;
            }

            model.Scenario = HealthScannerScenario.Nominal;
            model.StatusBadgeText = "NOMINAL";
            model.StatusHeadline = "No Anomalies Detected";
            model.StatusSubline = "All monitored systems within nominal range.";
        }

        private static void MapZones(HealthScannerInterfaceSnapshot snapshot, HealthScannerInterfaceViewModel model)
        {
            SetZone(model, BodyZone.Head, "Head", snapshot.HeadBrute, snapshot.HeadBurn, snapshot.SeveredZoneMask);
            SetZone(model, BodyZone.Chest, "Chest", snapshot.ChestBrute, snapshot.ChestBurn, snapshot.SeveredZoneMask);
            SetZone(model, BodyZone.LeftArm, "L Arm", snapshot.LeftArmBrute, snapshot.LeftArmBurn, snapshot.SeveredZoneMask);
            SetZone(model, BodyZone.RightArm, "R Arm", snapshot.RightArmBrute, snapshot.RightArmBurn, snapshot.SeveredZoneMask);
            SetZone(model, BodyZone.LeftLeg, "L Leg", snapshot.LeftLegBrute, snapshot.LeftLegBurn, snapshot.SeveredZoneMask);
            SetZone(model, BodyZone.RightLeg, "R Leg", snapshot.RightLegBrute, snapshot.RightLegBurn, snapshot.SeveredZoneMask);
            SetZone(model, BodyZone.Groin, "Groin", snapshot.GroinBrute, snapshot.GroinBurn, snapshot.SeveredZoneMask);
        }

        private static void ApplyEmptyZones(HealthScannerInterfaceViewModel model)
        {
            model.Zones[BodyZone.Head] = AnatomyZoneVisual.Empty;
            model.Zones[BodyZone.Chest] = AnatomyZoneVisual.Empty;
            model.Zones[BodyZone.LeftArm] = AnatomyZoneVisual.Empty;
            model.Zones[BodyZone.RightArm] = AnatomyZoneVisual.Empty;
            model.Zones[BodyZone.LeftLeg] = AnatomyZoneVisual.Empty;
            model.Zones[BodyZone.RightLeg] = AnatomyZoneVisual.Empty;
            model.Zones[BodyZone.Groin] = AnatomyZoneVisual.Empty;
        }

        private static void SetZone(
            HealthScannerInterfaceViewModel model,
            BodyZone zone,
            string label,
            float brute,
            float burn,
            int severedMask)
        {
            bool severed = (severedMask & (1 << (int)zone)) != 0;
            float severity = brute > burn ? brute : burn;
            float severity01 = severity <= 0f ? 0f : (severity >= 100f ? 1f : severity / 100f);

            StatusTone tone;
            if (severed || severity >= ZoneDangerSeverity)
            {
                tone = StatusTone.Danger;
            }
            else if (severity >= ZoneWarningSeverity)
            {
                tone = StatusTone.Warning;
            }
            else
            {
                tone = StatusTone.Neutral;
            }

            string tooltip = severed
                ? $"{label.ToUpperInvariant()}  SEVERED"
                : $"{label.ToUpperInvariant()}  BRUTE {brute:F0}%  BURN {burn:F0}%";

            model.Zones[zone] = new AnatomyZoneVisual(severity01, tone, tooltip, severed);
        }

        private static void MapSystemics(HealthScannerInterfaceSnapshot snapshot, HealthScannerInterfaceViewModel model)
        {
            float bloodCc = snapshot.BloodVolumeRatio * NominalBloodVolumeCc;
            model.BloodFraction = Clamp01(snapshot.BloodVolumeRatio);
            model.BloodValueText = $"{bloodCc:F0} / {NominalBloodVolumeCc:F0} CC";
            model.BloodTone = snapshot.BloodVolumeRatio <= HealthConstants.CriticalBloodVolumeRatio
                ? StatusTone.Danger
                : snapshot.BloodVolumeRatio <= HealthConstants.TreatmentBloodLowThreshold
                    ? StatusTone.Warning
                    : StatusTone.Neutral;

            model.CnsFraction = Clamp01(snapshot.BrainFunctionPercent / 100f);
            model.CnsValueText = $"{snapshot.BrainFunctionPercent:F0}%";
            model.CnsTone = snapshot.BrainFunctionPercent <= HealthConstants.CriticalBrainFunctionPercent
                ? StatusTone.Danger
                : snapshot.BrainFunctionPercent <= 70f
                    ? StatusTone.Warning
                    : StatusTone.Neutral;

            model.ToxinFraction = Clamp01(snapshot.ToxinConcentration);
            model.ToxinValueText = $"{snapshot.ToxinConcentration * 100f:F0}%";
            model.ToxinTone = snapshot.ToxinConcentration >= HealthConstants.CriticalToxinConcentration
                ? StatusTone.Danger
                : snapshot.ToxinConcentration >= 0.4f
                    ? StatusTone.Warning
                    : StatusTone.Neutral;

            float spo2 = Clamp01(1f - snapshot.OxyDebt);
            model.SpO2Fraction = spo2;
            model.SpO2ValueText = $"{spo2 * 100f:F0}%";
            model.SpO2Tone = snapshot.OxyDebt >= HealthConstants.CriticalOxyDebt
                ? StatusTone.Danger
                : snapshot.OxyDebt >= 0.4f
                    ? StatusTone.Warning
                    : StatusTone.Neutral;
        }

        private static void ApplyEmptySystemics(HealthScannerInterfaceViewModel model)
        {
            model.BloodValueText = "— / — CC";
            model.CnsValueText = "—";
            model.ToxinValueText = "—";
            model.SpO2ValueText = "—";
        }

        private static void MapOrgans(HealthScannerInterfaceSnapshot snapshot, HealthScannerInterfaceViewModel model)
        {
            model.Organs.Clear();
            model.Organs.Add(BuildOrgan("Heart", snapshot.HeartFunctionPercent));

            float lungFunction = snapshot.LeftLungFunctionPercent < snapshot.RightLungFunctionPercent
                ? snapshot.LeftLungFunctionPercent
                : snapshot.RightLungFunctionPercent;
            model.Organs.Add(BuildOrgan("Lungs", lungFunction));

            model.Organs.Add(BuildOrgan("Liver", snapshot.LiverFunctionPercent));
        }

        private static OrganReadoutData BuildOrgan(string label, float functionPercent)
        {
            StatusTone tone = functionPercent >= 85f
                ? StatusTone.Neutral
                : functionPercent >= HealthConstants.CriticalOrganFunctionPercent
                    ? StatusTone.Warning
                    : StatusTone.Danger;

            return new OrganReadoutData(label, $"{functionPercent:F0}%", tone);
        }

        private static float Clamp01(float value)
        {
            return value <= 0f ? 0f : (value >= 1f ? 1f : value);
        }
    }
}
