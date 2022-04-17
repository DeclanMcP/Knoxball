using Unity.Netcode;

namespace Knoxball
{
    internal class NetworkGamePlayState : INetworkSerializable
    {
        internal int tick;
        internal NetworkBallState ballState = new NetworkBallState();
        internal NetworkGamePlayerState[] playerStates;

        public NetworkGamePlayState()
        {
            this.ballState = new NetworkBallState();
        }

        public NetworkGamePlayState(int tick, NetworkGamePlayerState[] playerStates, NetworkBallState ballState)
        {
            this.tick = tick;
            this.ballState = ballState;
            this.playerStates = playerStates;
        }

        // INetworkSerializable
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref tick);
            serializer.SerializeValue(ref ballState);

            int length = 0;
            if (!serializer.IsReader)
            {
                length = playerStates.Length;
            }

            serializer.SerializeValue(ref length);

            if (serializer.IsReader)
            {
                playerStates = new NetworkGamePlayerState[length];
            }

            for (int n = 0; n < length; ++n)
            {
                serializer.SerializeValue(ref playerStates[n]);
            }
        }
        // ~INetworkSerializable
    }
}