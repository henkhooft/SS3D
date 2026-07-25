using FishNet.Broadcast;
using System;

namespace SS3D.Systems.Comms
{
    /// <summary>
    /// Non-positional comms line (radio / announcement). Plain text — presentation formats from
    /// channel id + sender on each client.
    /// </summary>
    [Serializable]
    public struct CommsMessage : IBroadcast
    {
        public string ChannelId;
        public string Sender;
        public string Text;
        public CommsChannelKind Kind;
    }
}
