using UnityEngine;
using Unity.Netcode;

namespace Knoxball
{
    public class NetworkPlayerInputState : INetworkSerializable
    {
        public int tick;
        public Vector3 direction = Vector3.zero;
        public bool kicking;

        public NetworkPlayerInputState()
        {

        }

        public NetworkPlayerInputState(int tick, Vector3 direction, bool kicking)
        {
            this.tick = tick;
            this.direction = direction;
            this.kicking = kicking;
        }


        // INetworkSerializable
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref tick);
            serializer.SerializeValue(ref direction);
            serializer.SerializeValue(ref kicking);
        }
        // ~INetworkSerializable
    }
}