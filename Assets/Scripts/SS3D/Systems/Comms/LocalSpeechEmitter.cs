using FishNet.Object;
using SS3D.Core;
using SS3D.Core.Behaviours;
using SS3D.Systems.Entities;
using UnityEngine;

namespace SS3D.Systems.Comms
{
    /// <summary>
    /// Attached to a player Entity. Client -> server request to speak, and the per-speaker RPC
    /// surface that broadcasts the resulting line to nearby observers. This is a component on the
    /// speaking Entity itself (rather than logic living solely on CommsSubSystem) because a FishNet
    /// RPC must be declared on a NetworkBehaviour that's part of the observed object graph - the
    /// singleton CommsSubSystem can't fire an RPC scoped to a specific Entity's observers.
    /// </summary>
    [RequireComponent(typeof(Entity))]
    public class LocalSpeechEmitter : NetworkActor
    {
        private Entity _entity;

        protected override void OnAwake()
        {
            base.OnAwake();

            _entity = GetComponent<Entity>();
        }

        /// <summary>
        /// Client -> server: ask to speak locally as this entity.
        /// </summary>
        [ServerRpc]
        public void CmdSpeak(string text)
        {
            // TEMP DIAGNOSTIC - remove once F3 root-caused. FishNet's codegen means this only
            // prints where the RPC body actually executes (server/host), not on the calling client.
            Debug.Log($"[CommsDebug] CmdSpeak executing server-side for {gameObject.name}: \"{text}\"");

            SubSystems.Get<CommsSubSystem>().HandleSpeakRequest(this, text);
        }

        /// <summary>
        /// Server-only: broadcasts a validated speech event to this entity's current observers.
        /// </summary>
        public void ServerBroadcastSpeech(SpeechEvent speechEvent)
        {
            // TEMP DIAGNOSTIC - remove once F3 root-caused.
            Debug.Log($"[CommsDebug] ServerBroadcastSpeech entered on {gameObject.name}. IsServer={IsServer}");

            if (!IsServer)
            {
                return;
            }

            RpcReceiveSpeech(speechEvent);
        }

        [ObserversRpc]
        private void RpcReceiveSpeech(SpeechEvent speechEvent)
        {
            // TEMP DIAGNOSTIC - remove once F3 root-caused. Should print on every observing client.
            Debug.Log($"[CommsDebug] RpcReceiveSpeech received on {gameObject.name}: \"{speechEvent.Text}\"");

            SubSystems.Get<CommsSubSystem>().NotifyLocalSpeechReceived(_entity, speechEvent);
        }
    }
}
