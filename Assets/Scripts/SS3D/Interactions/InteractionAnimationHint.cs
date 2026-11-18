namespace SS3D.Interactions
{
    /// <summary>
    /// The body animation an interaction asks for (tech-debt P4.3). The humanoid side
    /// (<c>InteractionAnimation.ChooseClip</c>) turns it into a clip, picking floor or reach by height where it says so.
    /// Interactions used to be matched by their generic name, so a rename silently changed when the effect landed.
    /// </summary>
    public enum InteractionAnimationHint : byte
    {
        /// <summary>No body animation.</summary>
        None = 0,

        /// <summary>Pick up: the floor bend below knee height, else the standing reach.</summary>
        Pickup = 1,

        /// <summary>Put down: the floor put-down below knee height, else the standing reach.</summary>
        PutDown = 2,

        /// <summary>A locker door: open or close, by the door's state.</summary>
        LockerDoor = 3,

        /// <summary>A tap on a locker's ID panel (lock, unlock).</summary>
        LockerTap = 4,

        /// <summary>A flick of a wall light switch.</summary>
        LightSwitch = 5,

        /// <summary>A reach at door height (airlocks, wall charges, the nuke's disk slot and switches).</summary>
        DoorReach = 6,

        /// <summary>Down on one knee for a delayed interaction's timer (defusing, CPR, floor construction).</summary>
        Kneel = 7,

        /// <summary>Both hands at the wall for a delayed interaction's timer (wall construction, the nuke panel).</summary>
        WallWork = 8,
    }
}
