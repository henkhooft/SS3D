using Unity.Mathematics;

namespace SS3D.Engine.AtmosphericsRework
{
    public class AtmosContainer
    {
        private float volume = 2.5f;
        private float temperature = 293f;
        private float4 coreGasses;

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
            coreGasses = 0f;
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

        public float GetPressure()
        {
            return GetTotalMoles() * GasConstants.gasConstant * temperature / volume / 1000f;
        }

        public float GetPartialPressure(CoreAtmosGasses gas)
        {
            return (coreGasses[(int)gas] * GasConstants.gasConstant * temperature) / volume / 1000f;
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
    }
}