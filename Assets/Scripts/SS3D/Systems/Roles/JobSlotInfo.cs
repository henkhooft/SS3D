namespace SS3D.Systems.Roles
{
    /// <summary>
    /// Replicated lobby view of one job: slot cap (0 = unlimited), current assignments and how many
    /// online players have it anywhere in their preferences.
    /// </summary>
    public struct JobSlotInfo
    {
        public string Name;
        public byte Department;
        public int Slots;
        public int Assigned;
        public int Interested;

        public bool Unlimited => Slots <= 0;

        public int Open => Unlimited ? int.MaxValue : System.Math.Max(0, Slots - Assigned);
    }
}
