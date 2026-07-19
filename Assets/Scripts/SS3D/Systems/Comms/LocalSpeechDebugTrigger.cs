using Coimbra.Services.Events;
using Coimbra.Services.PlayerLoopEvents;
using SS3D.Core.Behaviours;
using SS3D.Systems.Entities;
using SS3D.Systems.Entities.Events;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SS3D.Systems.Comms
{
    /// <summary>
    /// PLACEHOLDER: replace with real compose-input UI (comms.md §5 - T/Enter/Shift+Enter/Ctrl+Enter)
    /// once that slice lands. Until then, this is the only way to manually trigger a local speech
    /// event for testing: press F3 to speak one of a few rotating test lines as the locally
    /// controlled entity. Self-bootstraps at scene load (same pattern as
    /// ScreenEffectsDebugMenuView) so it needs no scene/prefab wiring - safe to delete this single
    /// file with no other class depending on it.
    /// </summary>
    public sealed class LocalSpeechDebugTrigger : Actor
    {
        private static readonly string[] TestLines =
        {
            "Cargo's here, someone sign for it.",
            "Anyone seen the captain.",
            "Vent's clogged in dorms.",
            "Who left the airlock open.",
        };

        private static bool s_bootstrapped;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (s_bootstrapped)
            {
                return;
            }

            s_bootstrapped = true;

            GameObject host = new(nameof(LocalSpeechDebugTrigger));
            DontDestroyOnLoad(host);
            host.AddComponent<LocalSpeechDebugTrigger>();
        }

        private Entity _localEntity;
        private int _lineIndex;

        protected override void OnAwake()
        {
            base.OnAwake();

            AddHandle(LocalPlayerObjectChanged.AddListener(HandlePlayerObjectChanged));
            AddHandle(UpdateEvent.AddListener(HandleUpdate));
        }

        private void HandlePlayerObjectChanged(ref EventContext context, in LocalPlayerObjectChanged e)
        {
            _localEntity = e.PlayerHasObject ? e.PlayerObject.GetComponent<Entity>() : null;
        }

        private void HandleUpdate(ref EventContext context, in UpdateEvent updateEvent)
        {
            if (Keyboard.current == null || !Keyboard.current[Key.F3].wasPressedThisFrame)
            {
                return;
            }

            if (_localEntity == null || !_localEntity.TryGetComponent(out LocalSpeechEmitter emitter))
            {
                return;
            }

            string line = TestLines[_lineIndex % TestLines.Length];
            _lineIndex++;

            emitter.CmdSpeak(line);
        }
    }
}
