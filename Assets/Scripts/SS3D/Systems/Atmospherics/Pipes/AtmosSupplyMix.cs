namespace SS3D.Systems.Atmospherics.Pipes
{
    /// <summary>
    /// The gas a storage chamber or starting line fill holds.
    /// </summary>
    public enum AtmosSupplyMix
    {
        Air,
        Oxygen,
        Nitrogen,
    }

    public static class AtmosSupplyMixes
    {
        /// <summary>
        /// Writes <paramref name="totalMoles"/> of the mix into a buffer indexed by gas id, clearing the other gases.
        /// </summary>
        public static void FillMoles(AtmosSupplyMix mix, float totalMoles, float[] moles)
        {
            System.Array.Clear(moles, 0, moles.Length);
            float oxygen = mix switch
            {
                AtmosSupplyMix.Oxygen => 1f,
                AtmosSupplyMix.Nitrogen => 0f,
                _ => AtmosConstants.StationOxygenMoles / (AtmosConstants.StationOxygenMoles + AtmosConstants.StationNitrogenMoles),
            };

            moles[AtmosConstants.Oxygen.Value] = totalMoles * oxygen;
            moles[AtmosConstants.Nitrogen.Value] = totalMoles * (1f - oxygen);
        }
    }
}
