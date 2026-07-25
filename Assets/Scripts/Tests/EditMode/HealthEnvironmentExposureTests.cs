using NUnit.Framework;
using SS3D.Systems.Atmospherics;
using SS3D.Systems.Atmospherics.Pipes;
using SS3D.Systems.Health;
using UnityEngine;

namespace EditorTests
{
    public class HealthEnvironmentExposureTests
    {
        [Test]
        public void StationAirHasFullBreathability()
        {
            float station = HealthEnvironmentState.StationOxygenMoleFraction;
            float breathability = HealthEnvironmentExposure.NormalizeBreathability(
                station,
                AtmosConstants.StandardPressure,
                isVacuum: false);

            Assert.AreEqual(1f, breathability, 0.001f);
            float po2 = HealthEnvironmentExposure.OxygenPartialPressureKpa(
                station,
                AtmosConstants.StandardPressure);
            Assert.Greater(po2, HealthConstants.OxygenPartialPressureComfortableKpa);
        }

        [Test]
        public void SlightlyLeanMixAtStationPressureStaysFullBreathability()
        {
            // ~18% O₂ at 101 kPa → PO₂ ≈ 18.2 kPa — still at/above comfortable band.
            float breathability = HealthEnvironmentExposure.NormalizeBreathability(
                0.18f,
                AtmosConstants.StandardPressure,
                isVacuum: false);

            Assert.AreEqual(1f, breathability, 0.001f);
        }

        [Test]
        public void VacuumNormalizesToZeroBreathability()
        {
            float breathability = HealthEnvironmentExposure.NormalizeBreathability(
                0.2f,
                0f,
                isVacuum: true);

            Assert.AreEqual(0f, breathability, 0.001f);
        }

        [Test]
        public void LowPartialPressureReducesBreathability()
        {
            // 10% O₂ at 101 kPa → PO₂ ≈ 10.1 kPa — between unbreathable and comfortable.
            float breathability = HealthEnvironmentExposure.NormalizeBreathability(
                0.10f,
                AtmosConstants.StandardPressure,
                isVacuum: false);

            Assert.Greater(breathability, 0f);
            Assert.Less(breathability, 1f);
        }

        [Test]
        public void UnbreathablePartialPressureIsZero()
        {
            float breathability = HealthEnvironmentExposure.NormalizeBreathability(
                0.04f,
                AtmosConstants.StandardPressure,
                isVacuum: false);

            Assert.AreEqual(0f, breathability, 0.001f);
        }

        [Test]
        public void HotTemperatureProducesBurnDamage()
        {
            float burn = HealthEnvironmentExposure.EnvironmentalBurnDamage(
                AirAlarmConstants.HighTemperatureKelvin + 25f,
                burnIntensity: 0f);

            Assert.Greater(burn, 0f);
        }

        [Test]
        public void ColdTemperatureProducesBurnDamage()
        {
            float burn = HealthEnvironmentExposure.EnvironmentalBurnDamage(
                HealthConstants.ColdDamageTemperatureKelvin - 20f,
                burnIntensity: 0f);

            Assert.Greater(burn, 0f);
        }

        [Test]
        public void FireIntensityProducesBurnDamage()
        {
            float burn = HealthEnvironmentExposure.EnvironmentalBurnDamage(
                AtmosConstants.StandardTemperature,
                burnIntensity: 1f);

            Assert.AreEqual(HealthConstants.FireBurnPerIntensity, burn, 0.001f);
        }

        [Test]
        public void BreathMolesScaleWithBreathabilityAndLungs()
        {
            float full = HealthEnvironmentExposure.BreathOxygenMoles(1f, 1f);
            float half = HealthEnvironmentExposure.BreathOxygenMoles(0.5f, 1f);
            float none = HealthEnvironmentExposure.BreathOxygenMoles(0f, 1f);

            Assert.AreEqual(HealthConstants.BreathOxygenMolesPerTick, full, 0.001f);
            Assert.AreEqual(HealthConstants.BreathOxygenMolesPerTick * 0.5f, half, 0.001f);
            Assert.AreEqual(0f, none, 0.001f);
            Assert.Less(Mathf.Abs(half - full * 0.5f), 0.001f);
        }
    }
}
