using NUnit.Framework;
using SS3D.Networking;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace SS3D.Tests
{
    public class Issue0990_PropagatingPocketProblem_HostPerspective : PlayModeTest
    {
        [UnitySetUp]
        public IEnumerator UnitySetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return LoadAndSetInLobby(NetworkType.Host);
            ServerHelpers.SetPlayerReadiness(ServerHelpers.LocalCkey, true);
            yield return TestHelpers.StartAndEnterRound();
            yield return GetHumanoidController();
            yield return GetInteractionController();
            yield return new WaitForSeconds(1f);
        }

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return TestHelpers.TryFinishAndExitRound();
            yield return PrepareNetworkTestEnvironment();
        }

        [UnityTest]
        public IEnumerator PlayerHasTheSameNumberOfPocketsAfterEndingRoundAndStartingNewOne()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return IssueReproduction.Issue0990_PlayerHasTheSameNumberOfPocketsAfterEndingRoundAndStartingNewOne(useProgrammaticRoundControl: true);
        }

        protected override bool UseMockUpInputs() => false;
    }
}
