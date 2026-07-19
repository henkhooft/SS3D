using SS3D.Core.Behaviours;
using SS3D.Systems.Entities;
using System;
using UnityEngine;

namespace SS3D.Systems.Comms
{
    /// <summary>
    /// Server-side validation and the client-side event hub for local speech, modeled on
    /// ChatSubSystem. Unlike ChatSubSystem, this does not register a global FishNet broadcast -
    /// delivery is scoped per-speaker via ObserversRpc on LocalSpeechEmitter (see that class),
    /// which piggybacks on the proximity-based observers every Entity's NetworkObject already
    /// maintains (Entity.cs's HashGrid observer rebuild).
    /// </summary>
    public class CommsSubSystem : NetworkSubSystem
    {
        private const int MaxMessageLength = 256;

        /// <summary>
        /// Raised on every client that receives a speech line, with the speaking Entity and the
        /// raw event data. Subscribers (e.g. the bubble UI) decide how/whether to render it based
        /// on their own viewer-relative audibility tier.
        /// </summary>
        public Action<Entity, SpeechEvent> OnLocalSpeechReceived;

        /// <summary>
        /// Server-only entry point: validates and dispatches a local speech line from the given
        /// emitter's owning Entity.
        /// </summary>
        public void HandleSpeakRequest(LocalSpeechEmitter emitter, string text, SpeechMode mode)
        {
            if (!IsServer)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (text.Length > MaxMessageLength)
            {
                text = text.Substring(0, MaxMessageLength);
            }

            if (!Enum.IsDefined(typeof(SpeechMode), mode))
            {
                mode = SpeechMode.Speak;
            }

            SpeechEvent speechEvent = new()
            {
                Text = text,
                Mode = mode,
                ServerTimestamp = Time.time,
            };

            emitter.ServerBroadcastSpeech(speechEvent);
        }

        /// <summary>
        /// Called by a LocalSpeechEmitter on every client that receives its ObserversRpc.
        /// </summary>
        public void NotifyLocalSpeechReceived(Entity speaker, SpeechEvent speechEvent)
        {
            OnLocalSpeechReceived?.Invoke(speaker, speechEvent);
        }
    }
}
