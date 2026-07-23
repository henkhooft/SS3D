using NUnit.Framework;
using SS3D.Systems.Health;

namespace EditorTests
{
    public class HealthPersonalAudioMapperTests
    {
        [Test]
        public void HealthySnapshotProducesNoHeartbeat()
        {
            Assert.AreEqual(0f, HealthPersonalAudioMapper.ComputeHeartbeatIntensity(HealthSnapshot.Default), 0.001f);
        }

        [Test]
        public void DeadSnapshotSilencesHeartbeatEntirely()
        {
            HealthSnapshot snapshot = HealthSnapshot.Default;
            snapshot.State = HealthState.Dead;
            snapshot.BrainFunctionPercent = 0f;

            Assert.AreEqual(0f, HealthPersonalAudioMapper.ComputeHeartbeatIntensity(snapshot), 0.001f);
        }

        [Test]
        public void CardiacArrestSilencesHeartbeatEvenWhileStateStaysCritical()
        {
            // Mirrors HealthScreenEffectMapperTests.CardiacArrestSetsDyingCriticalToFull's setup —
            // IsCardiacArrest can be true while State reads Critical — but the heartbeat cue means
            // the opposite of the screen vignette: the heart has stopped, so it goes silent.
            HealthSnapshot snapshot = HealthSnapshot.Default;
            snapshot.State = HealthState.Critical;
            snapshot.IsCardiacArrest = true;
            snapshot.BrainFunctionPercent = 80f;

            Assert.AreEqual(0f, HealthPersonalAudioMapper.ComputeHeartbeatIntensity(snapshot), 0.001f);
        }

        [Test]
        public void CriticalWithoutArrestRampsWithBrainFunction()
        {
            HealthSnapshot highBrain = HealthSnapshot.Default;
            highBrain.State = HealthState.Critical;
            highBrain.BrainFunctionPercent = 100f;

            HealthSnapshot lowBrain = HealthSnapshot.Default;
            lowBrain.State = HealthState.Critical;
            lowBrain.BrainFunctionPercent = HealthConstants.ConsciousnessBrainFunctionPercent;

            float high = HealthPersonalAudioMapper.ComputeHeartbeatIntensity(highBrain);
            float low = HealthPersonalAudioMapper.ComputeHeartbeatIntensity(lowBrain);

            Assert.AreEqual(0.4f, high, 0.001f);
            Assert.AreEqual(1f, low, 0.001f);
            Assert.Greater(low, high);
        }

        [Test]
        public void ResumingFromCardiacArrestToCriticalProducesNonzeroHeartbeat()
        {
            // Worked example C: defib succeeds -> heartbeat resumes.
            HealthSnapshot resumed = HealthSnapshot.Default;
            resumed.State = HealthState.Critical;
            resumed.IsCardiacArrest = false;
            resumed.BrainFunctionPercent = 60f;

            Assert.Greater(HealthPersonalAudioMapper.ComputeHeartbeatIntensity(resumed), 0f);
        }
    }
}
