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
    /// once that slice lands. Until then, F3 triggers local-chat test lines only (speak / whisper /
    /// shout / emote) — radio and station announcements live on other UI surfaces and are not
    /// previewed here. Self-bootstraps at scene load (same pattern as ScreenEffectsDebugMenuView).
    /// </summary>
    public sealed class LocalSpeechDebugTrigger : Actor
    {
        private readonly struct LocalTestLine
        {
            public readonly SpeechMode Mode;
            public readonly string Text;

            public LocalTestLine(SpeechMode mode, string text)
            {
                Mode = mode;
                Text = text;
            }
        }

        private static readonly LocalTestLine[] TestLines =
        {
            new(SpeechMode.Speak, "Cargo's here, someone sign for it."),
            new(SpeechMode.Speak, "Can someone fix atmos?"),
            new(SpeechMode.Whisper, "not on comms, ok?"),
            new(SpeechMode.Shout, "Need oxygen!"),
            new(SpeechMode.Shout, "Help! Fire in engineering!"),
            new(SpeechMode.Emote, "waves toward the console."),
            new(SpeechMode.Speak, "Anyone seen the captain."),
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

            LocalTestLine line = TestLines[_lineIndex % TestLines.Length];
            _lineIndex++;

            emitter.CmdSpeak(line.Text, line.Mode);
        }
    }
}
