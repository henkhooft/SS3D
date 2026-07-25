using System;
using SS3D.Systems.Atmospherics.Pipes;
using UnityEngine;

namespace SS3D.Systems.Health
{
    /// <summary>
    /// Pure helpers for turf → health exposure: breathability, toxin intake, env burn, alerts.
    /// Sampling that needs SubSystems lives on <see cref="HumanHealthController"/>.
    /// </summary>
    public static class HealthEnvironmentExposure
    {
        public static float NormalizeBreathability(float oxygenMoleFraction, float pressureKpa, bool isVacuum)
        {
            if (isVacuum || pressureKpa <= 0f)
            {
                return 0f;
            }

            float stationFraction = HealthEnvironmentState.StationOxygenMoleFraction;
            float breathability = stationFraction > 0f
                ? Mathf.Clamp01(oxygenMoleFraction / stationFraction)
                : 0f;

            if (pressureKpa < AirAlarmConstants.LowPressureKpa)
            {
                breathability *= Mathf.Clamp01(pressureKpa / AirAlarmConstants.LowPressureKpa);
            }

            return breathability;
        }

        public static float ToxinIntakeFromPlasma(float plasmaMoleFraction)
        {
            if (plasmaMoleFraction <= 0f)
            {
                return HealthConstants.BaseToxinIntake;
            }

            return plasmaMoleFraction * HealthConstants.PlasmaToxinIntakeScale;
        }

        public static float EnvironmentalBurnDamage(
            float temperatureKelvin,
            float burnIntensity)
        {
            float burn = 0f;

            if (temperatureKelvin >= AirAlarmConstants.HighTemperatureKelvin)
            {
                burn += (temperatureKelvin - AirAlarmConstants.HighTemperatureKelvin)
                    * HealthConstants.HotDamageBurnPerKelvin;
            }

            if (temperatureKelvin <= HealthConstants.ColdDamageTemperatureKelvin)
            {
                burn += (HealthConstants.ColdDamageTemperatureKelvin - temperatureKelvin)
                    * HealthConstants.ColdDamageBurnPerKelvin;
            }

            if (burnIntensity > 0f)
            {
                burn += burnIntensity * HealthConstants.FireBurnPerIntensity;
            }

            return Math.Min(burn, HealthConstants.MaxEnvironmentalBurnPerTick);
        }

        public static HealthEnvironmentState FromTileSample(
            AtmosAreaSample sample,
            bool isVacuum,
            float burnIntensity)
        {
            float breathability = NormalizeBreathability(
                sample.OxygenMoleFraction,
                sample.AveragePressureKpa,
                isVacuum);

            return new HealthEnvironmentState
            {
                HasSample = true,
                IsVacuum = isVacuum,
                AtmosphereBreathability = breathability,
                TemperatureKelvin = sample.TemperatureKelvin,
                PressureKpa = sample.AveragePressureKpa,
                BurnIntensity = burnIntensity,
                PlasmaMoleFraction = sample.PlasmaMoleFraction,
                OxygenMoleFraction = sample.OxygenMoleFraction,
            };
        }

        public static HealthEnvironmentState FromVacuumCell(float temperatureKelvin, float burnIntensity)
        {
            return new HealthEnvironmentState
            {
                HasSample = true,
                IsVacuum = true,
                AtmosphereBreathability = 0f,
                TemperatureKelvin = temperatureKelvin,
                PressureKpa = 0f,
                BurnIntensity = burnIntensity,
                PlasmaMoleFraction = 0f,
                OxygenMoleFraction = 0f,
            };
        }

        /// <summary>
        /// Breath moles of O₂ to pull this tick (0 when unbreathable / disabled).
        /// </summary>
        public static float BreathOxygenMoles(float atmosphereBreathability, float lungFunction01)
        {
            if (atmosphereBreathability <= 0f || lungFunction01 <= 0f)
            {
                return 0f;
            }

            return HealthConstants.BreathOxygenMolesPerTick
                * atmosphereBreathability
                * Mathf.Clamp01(lungFunction01);
        }
    }
}
