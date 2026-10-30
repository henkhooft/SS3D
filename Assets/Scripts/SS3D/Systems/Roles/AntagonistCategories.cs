namespace SS3D.Systems.Roles
{
    /// <summary>
    /// Antagonist categories a player can opt into from the lobby (lobby.md §4). Opting in is
    /// eligibility only; gamemodes decide who is chosen.
    /// </summary>
    public static class AntagonistCategories
    {
        public const string NuclearOperative = "nukeop";
        public const string Traitor = "traitor";

        public static readonly (string Key, string Name)[] All =
        {
            (NuclearOperative, "Nuclear Operative"),
            (Traitor, "Traitor"),
        };

        public static bool IsKnown(string key)
        {
            foreach ((string known, string _) in All)
            {
                if (known == key)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
