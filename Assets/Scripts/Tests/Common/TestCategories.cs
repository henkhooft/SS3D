namespace SS3D.Tests
{
    public static class TestCategories
    {
        public const string Prerequisites = "Prerequisites";
        public const string RequiresCompiledBuild = "RequiresCompiledBuild";
        public const string Lifecycle = "Lifecycle";

        /// <summary>Too slow for the agent loop; runs in the nightly (<c>validate.sh --nightly</c>).</summary>
        public const string Nightly = "Nightly";

        /// <summary>Tier 2 scenarios in the test room (ScenarioFixture). Run all with <c>validate.sh --steps scenarios:Scenarios</c>.</summary>
        public const string Scenarios = "Scenarios";

        public const string Medical = "Medical";

        public const string Atmos = "Atmos";
    }
}
