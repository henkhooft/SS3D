namespace SS3D.Tests
{
    public static class TestCategories
    {
        public const string Prerequisites = "Prerequisites";
        public const string RequiresCompiledBuild = "RequiresCompiledBuild";
        public const string Lifecycle = "Lifecycle";

        /// <summary>Too slow for the agent loop; runs in the nightly (<c>validate.sh --nightly</c>).</summary>
        public const string Nightly = "Nightly";
    }
}
