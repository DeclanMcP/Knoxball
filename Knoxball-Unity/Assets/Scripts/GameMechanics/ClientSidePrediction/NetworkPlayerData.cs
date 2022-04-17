using Unity.Netcode;

namespace Knoxball
{

    public struct NetworkPlayerData : INetworkSerializable
    {
        internal ulong clientId;
        internal NetworkString lobbyId;

        public NetworkPlayerData(ulong clientId, NetworkString lobbyId)
        {
            this.clientId = clientId;
            this.lobbyId = lobbyId;
        }

        // INetworkSerializable
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref clientId);
            serializer.SerializeValue(ref lobbyId);
        }
        // ~INetworkSerializable
    }
}