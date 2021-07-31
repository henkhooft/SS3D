using Unity.Mathematics;
using UnityEngine;

namespace SS3D.Engine.AtmosphericsRework
{
    public struct AtmosContainer
    {
        private float volume;
        private float temperature;
        private float4 coreGasses;

        public void Setup()
        {
            volume = 2.5f;
            temperature = 293f;
            coreGasses = 0f;
        }

        public float GetTemperature()
        {
            return temperature;
        }

        public void SetTemperature(float temperature)
        {
            if (temperature >= 0)
                this.temperature = temperature;
        }

        public float GetVolume()
        {
            return volume;
        }

        public float GetRealVolume()
        {
            return volume - math.csum(coreGasses * GasConstants.coreGasDensity * math.pow(10, -6));
        }

        public void SetVolume(float volume)
        {
            this.volume = volume;
        }

        public float GetCoreGas(CoreAtmosGasses gas)
        {
            return coreGasses[(int)gas];
        }

        public float4 GetCoreGasses()
        {
            return coreGasses;
        }

        public void AddCoreGas(CoreAtmosGasses gas, float amount)
        {
            coreGasses[(int)gas] = math.max(coreGasses[(int)gas] + amount, 0f);
        }

        public void AddCoreGasses(float4 amount)
        {
            coreGasses = math.max(coreGasses + amount, 0f);

            if (temperature <= 0.1f)
                Debug.LogError("Temperatur set at 0");
        }

        public void RemoveCoreGas(CoreAtmosGasses gas, float amount)
        {
            coreGasses[(int)gas] = math.max(coreGasses[(int)gas] - amount, 0f);
        }

        public void RemoveCoreGasses(float4 amount)
        {
            coreGasses = math.max(coreGasses - amount, 0f);
        }

        public void OverrideCoreGasses(float4 amounts)
        {
            coreGasses = math.max(amounts, 0f);
        }

        public void MakeEmpty()
        {
            Setup();
        }

        public void AddHeat(float temp)
        {
            temperature += math.max(temp - temperature, 0f) / GetSpecificHeat() * (100 / GetTotalMoles()) * GasConstants.dt;
        }

        public void RemoveHeat(float temp)
        {
            temperature -= math.max(temp - temperature, 0f) / GetSpecificHeat() * (100 / GetTotalMoles()) * GasConstants.dt;
            temperature = math.max(temperature, 0f);
        }

        private float GetTotalMoles()
        {
            return math.csum(coreGasses);
        }

        /// <summary>
        /// Returns the pressure based on the ideal gas law
        /// </summary>
        /// <returns></returns>
        public float GetPressure()
        {
            float pressure = GetTotalMoles() * GasConstants.gasConstant * temperature / volume / 1000f;
            if (float.IsNaN(pressure))
                return 0f;
            else
                return pressure;
        }

        /// <summary>
        /// Returns the real pressure based on Van der Waals equation of state.
        /// 
        /// P = a(n^2 / V^2) + nRT / (V - nb)
        /// 
        /// </summary>
        /// <returns></returns>
        public float GetRealPressure()
        {
            float4 particialPressures = coreGasses * GasConstants.gasConstant * temperature / 
                GetRealVolume() / 1000f +
                100 * (GasConstants.interMolecularInteraction * math.pow(coreGasses, 2)) / math.pow(volume * 1000, 2);

            return math.csum(particialPressures);
        }

        public float GetPartialPressure(CoreAtmosGasses gas)
        {
            float pressure = (coreGasses[(int)gas] * GasConstants.gasConstant * temperature) / volume / 1000f;
            if (float.IsNaN(pressure))
                return 0f;
            else
                return pressure;
        }

        public float GetSpecificHeat()
        {
            return (math.csum(coreGasses * GasConstants.coreSpecificHeat) / GetTotalMoles());
        }

        /// <summary>
        /// Returns the mass for the container in grams
        /// </summary>
        /// <returns></returns>
        public float GetMass()
        {
            return math.csum(coreGasses * GasConstants.coreGasDensity);
        }

        /// <summary>
        /// Returns the amount of kinetic energy that the gasses inside the container have.
        /// 
        /// E = (3/2)nRT
        /// 
        /// </summary>
        /// <returns></returns>
        public float GetKineticEnergy()
        {
            return 1.5f * math.csum(coreGasses) * GasConstants.gasConstant * temperature;
        }

        public bool IsEmpty()
        {
            return math.all(coreGasses == 0f);
        }

        public void MakeAir()
        {
            MakeEmpty();

            AddCoreGas(CoreAtmosGasses.Oxygen, 20.79f);
            AddCoreGas(CoreAtmosGasses.Nitrogen, 83.17f);
            SetTemperature(293f); ;
        }

        public void MakeRandom()
        {
            MakeEmpty();

            for (int i = 0; i < 4; i++)
            {
                AddCoreGas((CoreAtmosGasses)i, UnityEngine.Random.Range(0, 300f));
            }
            // SetTemperature(UnityEngine.Random.Range(0, 300f));
        }
    }
}