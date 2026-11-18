namespace SS3D.Systems.Entities.Humanoid
{
    /// <summary>
    /// Which peer simulates a body's physics, and so moves it: the owning client while it lives, the server for corpses
    /// and for bodies nobody owns (a disconnected player, a combat dummy). Shared by <see cref="Ragdoll"/> and the
    /// body drag, which must push the body on the same peer (Documents/plans/drag-and-zero-g.plan.md Decision 22).
    /// </summary>
    public static class BodyPhysicsAuthority
    {
        public static bool IsAuthority(bool dead, bool ownerValid, bool isOwner, bool isServer)
        {
            if (dead || !ownerValid)
            {
                return isServer;
            }

            return isOwner;
        }
    }
}
