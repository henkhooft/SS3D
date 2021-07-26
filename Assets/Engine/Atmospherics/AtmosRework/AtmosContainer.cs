using Unity.Mathematics;

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