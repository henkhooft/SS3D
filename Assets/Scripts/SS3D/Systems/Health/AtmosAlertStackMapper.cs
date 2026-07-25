using SS3D.Systems.Atmospherics.Pipes;

namespace SS3D.Systems.Health
{
    /// <summary>
    /// Maps synced <see cref="HealthEnvironmentState"/> onto atmos alert-stack signals.
    /// Health-local severities so this assembly never references Main HUD types.
    /// </summary>
    public static class AtmosAlertStackMapper
    {
        public enum Severity
        {
            None,
            Warning,
            Critical,
        }

        public struct AtmosAlertSignals
        {
            public Severity Fire;
            public Severity Hot;
            public Severity Cold;
            public Severity LowPressure;
            public Severity HighPressure;
            public Severity LowOxygen;
        }

        public static AtmosAlertSignals Compute(HealthEnvironmentState env)
        {
            if (HealthEnvironmentSettings.AtmosphericDamageDisabled || !env.HasSample)
            {
                return default;
            }

            return new AtmosAlertSignals
            {
                Fire = ComputeFire(env),
                Hot = ComputeHot(env),
                Cold = ComputeCold(env),
                LowPressure = ComputeLowPressure(env),
                HighPressure = ComputeHighPressure(env),
                LowOxygen = ComputeLowOxygen(env),
            };
        }

        private static Severity ComputeFire(HealthEnvironmentState env)
        {
            if (env.BurnIntensity >= 0.5f)
            {
                return Severity.Critical;
            }

            if (env.BurnIntensity > 0.05f)
            {
                return Severity.Warning;
            }

            return Severity.None;
        }

        private static Severity ComputeHot(HealthEnvironmentState env)
        {
            if (env.TemperatureKelvin >= AirAlarmConstants.HighTemperatureKelvin + 20f)
            {
                return Severity.Critical;
            }

            if (env.TemperatureKelvin >= AirAlarmConstants.HighTemperatureKelvin)
            {
                return Severity.Warning;
            }

            return Severity.None;
        }

        private static Severity ComputeCold(HealthEnvironmentState env)
        {
            if (env.TemperatureKelvin <= HealthConstants.FreezingTemperatureKelvin)
            {
                return Severity.Critical;
            }

            if (env.TemperatureKelvin <= HealthConstants.ColdWarningTemperatureKelvin)
            {
                return Severity.Warning;
            }

            return Severity.None;
        }

        private static Severity ComputeLowPressure(HealthEnvironmentState env)
        {
            if (env.IsVacuum || env.PressureKpa <= 0f)
            {
                return Severity.Critical;
            }

            if (env.PressureKpa < AirAlarmConstants.LowPressureKpa)
            {
                return Severity.Warning;
            }

            return Severity.None;
        }

        private static Severity ComputeHighPressure(HealthEnvironmentState env)
        {
            if (env.PressureKpa >= AirAlarmConstants.HighPressureKpa * 1.25f)
            {
                return Severity.Critical;
            }

            if (env.PressureKpa >= AirAlarmConstants.HighPressureKpa)
            {
                return Severity.Warning;
            }

            return Severity.None;
        }

        private static Severity ComputeLowOxygen(HealthEnvironmentState env)
        {
            // Turf hypoxia (distinct from systemic oxy-debt alert; Main HUD takes max of both).
            if (env.IsVacuum || env.AtmosphereBreathability <= 0.05f)
            {
                return Severity.Critical;
            }

            if (env.AtmosphereBreathability < 0.75f
                || env.OxygenMoleFraction < AirAlarmConstants.LowOxygenMoleFraction)
            {
                return Severity.Warning;
            }

            return Severity.None;
        }
    }
}
