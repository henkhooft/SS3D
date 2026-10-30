using FishNet.Broadcast;

namespace SS3D.Systems.Roles.Messages
{
    /// <summary>
    /// Client → server: the sender's full job priority list and antagonist opt-ins. The server
    /// resolves the player from the sending connection.
    /// </summary>
    public struct JobPreferencesMessage : IBroadcast
    {
        public string[] Jobs;
        public byte[] Priorities;
        public string[] AntagonistOptIns;
    }
}
