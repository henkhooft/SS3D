
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;

namespace SS3D.Engine.AtmosphericsRework
{
    public struct AtmosObject
    {
        private AtmosState state;
        private AtmosContainer container;
        private bool temperatureSetting;

        private NativeArray<AtmosObject> neighbours;
        private bool4 activeDirection;

        private float4 tileFlux;
        private float4 neighbourFlux;
        private float4 difference;

        private bool4 neighbourUpdate;
        private float4 neighbourPressure;

        // Performance makers
        static ProfilerMarker s_CalculateFluxPerfMarker = new ProfilerMarker("AtmosObject.CalculateFlux");
        // static ProfilerMarker s_CalculateFluxOnePerfMarker = new ProfilerMarker("AtmosObject.CalculateFlux.One");
        static ProfilerMarker s_SimulateFluxPerfMarker = new ProfilerMarker("AtmosObject.SimulateFlux");
        static ProfilerMarker s_SimlateMixingPerfMarker = new ProfilerMarker("AtmosObject.SimulateMixing");

        public void Setup()
        {
            container = new AtmosContainer();
            container.Setup();

            state = AtmosState.Active;
            temperatureSetting = false;
            neighbours = new NativeArray<AtmosObject>(4, Allocator.Persistent);
            activeDirection = false;
            tileFlux = 0f;
            neighbourFlux = 0f;
            difference = 0f;
            neighbourUpdate = false;
            neighbourPressure = 0f;
        }


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

        public void SetContainer(AtmosContainer container)
        {
            this.container = container;
        }

        public void SetNeighbours(AtmosObject[] neighbours)
        {
            this.neighbours.CopyFrom(neighbours);
        }

        public void SetBlocked(bool isBlocked)
        {
            if (isBlocked)
                state = AtmosState.Blocked;
            else
                state = AtmosState.Active;
        }

        public void MakeAir()
        {
            container.MakeEmpty();

            container.AddCoreGas(CoreAtmosGasses.Oxygen, 20.79f);
            container.AddCoreGas(CoreAtmosGasses.Nitrogen, 83.17f);
            container.SetTemperature(293f); ;
        }

        public void CalculateFlux()
        {
            s_CalculateFluxPerfMarker.Begin();

            float pressure = container.GetPressure();

            /*
            for (int i = 0; i < neighbours.Length; i++)
            {
                
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
                
            }
            */

            /// Testing
            for (int i = 0; i < neighbours.Length; i++)
            {
                if ((!neighbours[i].Equals(default(AtmosObject))) && neighbours[i].GetState() != AtmosState.Blocked)
                {
                    neighbourPressure[i] = neighbours[i].GetContainer().GetPressure();
                    neighbourUpdate[i] = true;
                }
                else
                {
                    neighbourPressure[i] = 0f;
                    neighbourUpdate[i] = false;
                }
            }

            neighbourFlux = math.min(tileFlux * GasConstants.drag + (pressure - neighbourPressure) * GasConstants.dt, 1000f);

            for (int i = 0; i < neighbours.Length; i++)
            {
                if (neighbourUpdate[i] && neighbourFlux[i] < 0f)
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
                    if ((!neighbours[i].Equals(default(AtmosObject))) && neighbours[i].GetState() != AtmosState.Vacuum)
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
                    if ((!neighbours[i].Equals(default(AtmosObject))) && neighbours[i].GetState() != AtmosState.Blocked)
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