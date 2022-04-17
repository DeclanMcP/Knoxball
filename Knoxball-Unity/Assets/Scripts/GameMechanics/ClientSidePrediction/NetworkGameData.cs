using Unity.Netcode;

namespace Knoxball
{
    internal class NetworkGameData : INetworkSerializable
    {
        internal NetworkPlayerData[] playerDatas;

        public NetworkGameData()
        {

        }

        public NetworkGameData(NetworkPlayerData[] playerDatas)
        {
            this.playerDatas = playerDatas;
        }

        // INetworkSerializable
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {

            int length = 0;
            if (!serializer.IsReader)
            {
                length = playerDatas.Length;
            }

            serializer.SerializeValue(ref length);

            if (serializer.IsReader)
            {
                playerDatas = new NetworkPlayerData[length];
            }

            for (int n = 0; n < length; ++n)
            {
                serializer.SerializeValue(ref playerDatas[n]);
            }
        }
        // ~INetworkSerializable
    }
}