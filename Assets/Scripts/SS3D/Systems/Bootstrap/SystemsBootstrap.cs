using SS3D.Core;
using SS3D.Core.Behaviours;
using SS3D.Systems.ScreenEffects;
using SS3D.Systems.Testing;
using SS3D.Systems.Vision;
using SS3D.Systems.WorldReadiness;
using UnityEngine;

namespace SS3D.Systems.Bootstrap
{
    /// <summary>
    /// Single cold-start owner for process-wide Systems-layer services. Replaces scattered
    /// <c>RuntimeInitializeOnLoadMethod</c> self-bootstraps for those types. UI hosts
    /// (UiShell / MainHud / StoragePanel) still self-bootstrap in their own assemblies until
    /// folded under UiShell consolidation.
    /// </summary>
    public static class SystemsBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AfterSceneLoad()
        {
            EnsureProcessWideServices();
        }

        public static void EnsureProcessWideServices()
        {
            EnsureSubSystem<WorldReadinessSubSystem>();
            EnsureSubSystem<ScreenEffectsSubSystem>();
            EnsureSubSystem<AutomationSubSystem>();
            EnsureSubSystem<VisionSubSystem>();
        }

        public static T EnsureSubSystem<T>() where T : SubSystem
        {
            if (SubSystems.TryGet(out T existing))
            {
                return existing;
            }

            GameObject host = new(typeof(T).Name);
            Object.DontDestroyOnLoad(host);
            return host.AddComponent<T>();
        }
    }
}
