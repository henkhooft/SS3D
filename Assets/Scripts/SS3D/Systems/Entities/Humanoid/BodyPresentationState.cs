namespace SS3D.Systems.Entities.Humanoid
{
    /// <summary>
    /// Replicated body pose authority for humanoids. Health and combat write intent;
    /// <see cref="Ragdoll"/> is the sole applier (physics, animator suppress, movement gate).
    /// </summary>
    public enum BodyPresentationState : byte
    {
        Locomotion = 0,
        Collapsed = 1,
        Dead = 2,
    }
}
