namespace SS3D.Systems.Atmospherics.Pipes
{
    public static class AtmosPortConstants
    {
        /// <summary>Rated vent throughput under ideal differential (moles/s total mixture).</summary>
        public const float VentRatedFlowMolesPerSecond = 50f;

        /// <summary>Rated scrubber throughput for filtered gases (moles/s).</summary>
        public const float ScrubberRatedFlowMolesPerSecond = 40f;

        /// <summary>Backpressure differential at which port flow tapers to zero (kPa).</summary>
        public const float PortMaxDifferentialKpa = 101.325f;

        /// <summary>Rated gas pump throughput under ideal differential (moles/s total mixture).</summary>
        public const float PumpRatedFlowMolesPerSecond = 100f;

        /// <summary>Motor stall threshold — no flow above this differential (kPa).</summary>
        public const float PumpMaxDifferentialKpa = 450f;

        /// <summary>Starting target of a pressure pump (kPa): room pressure, for an engineer to raise.</summary>
        public const int PumpDefaultTargetKpa = 101;

        /// <summary>Scrubber flow rate setting outside a panic.</summary>
        public const int ScrubberNormalFlowRate = 5;

        /// <summary>Highest scrubber flow rate setting.</summary>
        public const int ScrubberMaxFlowRate = 10;

        /// <summary>Starting set rate of a volume pump (L/s).</summary>
        public const int VolumePumpDefaultLitresPerSecond = 500;

        /// <summary>Highest set rate of a volume pump (L/s).</summary>
        public const int VolumePumpMaxLitresPerSecond = 2000;

        /// <summary>Share of the pressure gap an open valve closes each tick.</summary>
        public const float ValveEqualiseFraction = 0.5f;

        /// <summary>Moles a tick below which a device counts as idle, so a settled valve goes quiet.</summary>
        public const float FlowingMolesThreshold = 0.001f;
    }
}
