using SS3D.Systems.Health;
using System.Collections.Generic;

namespace SS3D.UI.MachineInterface
{
    public enum HealthScannerScenario
    {
        NoSubject = 0,
        Nominal = 1,
        Impaired = 2,
        Critical = 3,
    }

    /// <summary>
    /// Pre-computed visual state for a single body zone in <see cref="Components.AnatomyBodyMap"/>.
    /// </summary>
    public readonly struct AnatomyZoneVisual
    {
        public AnatomyZoneVisual(float severity01, StatusTone tone, string tooltip, bool severed)
        {
            Severity01 = severity01;
            Tone = tone;
            Tooltip = tooltip;
            Severed = severed;
        }

        public float Severity01 { get; }

        public StatusTone Tone { get; }

        public string Tooltip { get; }

        public bool Severed { get; }

        public static AnatomyZoneVisual Empty { get; } = new(0f, StatusTone.Neutral, string.Empty, false);
    }

    /// <summary>
    /// Small readout row for a single tracked organ (label, function %, tone).
    /// </summary>
    public readonly struct OrganReadoutData
    {
        public OrganReadoutData(string label, string valueText, StatusTone tone)
        {
            Label = label;
            ValueText = valueText;
            Tone = tone;
        }

        public string Label { get; }

        public string ValueText { get; }

        public StatusTone Tone { get; }
    }

    public sealed class HealthScannerInterfaceViewModel : IMachineInterfaceViewModel
    {
        public string Title { get; set; } = "VITALS SCAN";

        public string ModelLabel { get; set; } = "VSU-5R · anatomical scan unit";

        public string Subtitle { get; set; } = "Full Body · Anatomical Layer Scan";

        public string ConnectionStatus { get; set; } = "WIRED · DIAG BED · PORT J1";

        public string FooterText { get; set; } = "SS3D Vitals Scan Unit — Model VSU-5R";

        public bool ChassisPowerOk { get; set; } = true;

        public HealthScannerScenario Scenario { get; set; } = HealthScannerScenario.NoSubject;

        public string StatusBadgeText { get; set; } = "NO SUBJECT";

        public string StatusHeadline { get; set; } = "Awaiting Subject";

        public string StatusSubline { get; set; } = "Position a patient within scan range.";

        public Dictionary<BodyZone, AnatomyZoneVisual> Zones { get; } = new();

        public string BloodValueText { get; set; } = "— / — CC";

        public float BloodFraction { get; set; }

        public StatusTone BloodTone { get; set; } = StatusTone.Neutral;

        public string CnsValueText { get; set; } = "—";

        public float CnsFraction { get; set; }

        public StatusTone CnsTone { get; set; } = StatusTone.Neutral;

        public string ToxinValueText { get; set; } = "—";

        public float ToxinFraction { get; set; }

        public StatusTone ToxinTone { get; set; } = StatusTone.Neutral;

        public string SpO2ValueText { get; set; } = "—";

        public float SpO2Fraction { get; set; }

        public StatusTone SpO2Tone { get; set; } = StatusTone.Neutral;

        public List<OrganReadoutData> Organs { get; set; } = new();
    }
}
