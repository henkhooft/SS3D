
using Unity.Mathematics;
using Unity.Profiling;

namespace SS3D.Engine.AtmosphericsRework
{
    public class AbstractAtmosObject
    {
        private AtmosState state = AtmosState.Active;
        private AtmosContainer container = new AtmosContainer();
        private bool temperatureSetting = false;

        private AbstractAtmosObject[] neighbours = { null, null, null, null };
        private bool4 activeDirection = false;

        private float4 tileFlux = 0f;
        private float4 neighbourFlux = 0f;
        private float4 difference = 0f;

        // Performance makers
        static ProfilerMarker s_CalculateFluxPerfMarker = new ProfilerMarker("AtmosObject.CalculateFlux");
        // static ProfilerMarker s_CalculateFluxOnePerfMarker = new ProfilerMarker("AtmosObject.CalculateFlux.One");
        static ProfilerMarker s_SimulateFluxPerfMarker = new ProfilerMarker("AtmosObject.SimulateFlux");
        static ProfilerMarker s_SimlateMixingPerfMarker = new ProfilerMarker("AtmosObject.SimulateMixing");

        public AtmosState GetState()
        {
            return state;
        }

        public void SetState(AtmosState state)
        {
            this.state = state;
        }

        public AtmosContainer GetContainer()
        {
            return container;
        }

        public void SetBlocked(bool isBlocked)
        {
            if (isBlocked)
                state = AtmosState.Blocked;
            else
                state = AtmosState.Active;
        }

        public void CalculateFlux()
        {
            s_CalculateFluxPerfMarker.Begin();

            float pressure = container.GetPressure();

            // for each neighbour
            for (int i = 0; i < neighbours.Length; i++)
            {
                /*
                if (neighbours[i]?.GetState() != AtmosState.Blocked)
                {
                    neighbourFlux[i] = math.min(tileFlux[i] * GasConstants.drag + (pressure - neighbours[i].GetContainer().GetPressure()) * GasConstants.dt, 1000f);
                    activeDirection[i] = true;

                    if (neighbourFlux[i] < 0f)
                    {
                        neighbours[i].SetState(AtmosState.Active);
                        neighbourFlux[i] = 0f;
                    }
                }
                */
            }

            /// Testing
            float4 neighbourPressure = 0f;
            bool4 neighbourUpdate = false;
            for (int i = 0; i < neighbours.Length; i++)
            {
                if (neighbours[i].GetState() != AtmosState.Blocked)
                {
                    neighbourPressure = neighbours[i].GetContainer().GetPressure();
                    neighbourUpdate[i] = true;
                }
            }

            neighbourFlux = math.min(tileFlux * GasConstants.drag + (pressure - neighbourPressure) * GasConstants.dt, 1000f);

            for (int i = 0; i < neighbours.Length; i++)
            {
                if (neighbourFlux[i] < 0f)
                {
                    neighbours[i].SetState(AtmosState.Active);
                    neighbourFlux[i] = 0f;
                }
            }





                if (math.any(neighbourFlux > GasConstants.fluxEpsilon))
            {
                float scalingFactor = math.min(1f, pressure / math.csum(neighbourFlux) / GasConstants.dt);

                neighbourFlux *= scalingFactor;
                tileFlux = neighbourFlux;
            }
            else
            {
                tileFlux = 0f;
                if (!temperatureSetting)
                    state = AtmosState.Semiactive;
                else
                    temperatureSetting = false;
            }

            if (state == AtmosState.Semiactive || state == AtmosState.Active)
            {
                SimulateMixing();
            }

            s_CalculateFluxPerfMarker.End();
        }

        public void SimulateFlux()
        {
            s_SimulateFluxPerfMarker.Begin();

            if (state == AtmosState.Active)
            {
                SimulateFluxActive();
            }
            else if (state == AtmosState.Semiactive)
            {
                SimulateMixing();
            }

            s_SimulateFluxPerfMarker.End();
        }

        private void SimulateFluxActive()
        {
            float pressure = container.GetPressure();

            // for each neighbour
            if (math.any(tileFlux > 0f))
            {
                float4 factor = container.GetCoreGasses() * (tileFlux / pressure);

                for (int i = 0; i < neighbours.Length; i++)
                {
                    if (neighbours[i]?.GetState() != AtmosState.Vacuum)
                    {
                        neighbours[i].GetContainer().AddCoreGasses(factor);
                    }
                    else
                    {
                        activeDirection[i] = false;
                    }
                    container.RemoveCoreGasses(factor);
                }
            }

            float difference = 0f;
            for (int i = 0; i < neighbours.Length; i++)
            {
                if (activeDirection[i])
                {
                    difference = (container.GetTemperature() - neighbours[i].GetContainer().GetTemperature())
                        * GasConstants.thermalBase * container.GetVolume();

                    if (difference > GasConstants.thermalEpsilon)
                    {
                        AtmosContainer neighbourContainer = neighbours[i].GetContainer();
                        neighbourContainer.SetTemperature(neighbourContainer.GetTemperature() + difference);
                        container.SetTemperature(container.GetTemperature() - difference);
                        temperatureSetting = true;
                    }
                }
            }
        }

        public void SimulateMixing()
        {
            s_SimlateMixingPerfMarker.Begin();

            difference = 0f;
            bool mixed = false;
            if (math.any(container.GetCoreGasses() > 0f))
            {
                for (int i = 0; i < neighbours.Length; i++)
                {
                    if (neighbours[i] != null && neighbours[i].GetState() != AtmosState.Blocked)
                    {
                        AtmosContainer neighbourContainer = neighbours[i].GetContainer();
                        difference = (container.GetCoreGasses() - neighbourContainer.GetCoreGasses()) * GasConstants.mixRate;
                        if (math.any(difference > GasConstants.minMoleTransfer))
                        {
                            // Increase neighbouring tiles moles and decrease ours
                            neighbourContainer.AddCoreGasses(difference);
                            container.RemoveCoreGasses(difference);
                            mixed = true;

                            // Remain active if there is still a pressure difference
                            if (math.abs(neighbourContainer.GetPressure() - container.GetPressure()) > GasConstants.minMoleTransfer)
                            {
                                neighbours[i].SetState(AtmosState.Active);
                            }
                            else
                            {
                                neighbours[i].SetState(AtmosState.Semiactive);
                            }
                        }
                    }
                }
            }

            if (!mixed && state == AtmosState.Semiactive)
            {
                state = AtmosState.Inactive;
            }

            s_SimlateMixingPerfMarker.End();
        }
    }
}