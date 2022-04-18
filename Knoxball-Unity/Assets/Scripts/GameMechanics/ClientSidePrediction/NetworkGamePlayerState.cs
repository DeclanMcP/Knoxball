using Unity.Netcode;
using UnityEngine;

namespace Knoxball
{

    public struct NetworkGamePlayerState : INetworkSerializable
    {
        public ulong ID;
        internal Vector3 position;
        internal Vector3 velocity;
        internal Quaternion rotation;
        internal bool kicking;

        public NetworkGamePlayerState(ulong iD, Vector3 position, Vector3 velocity, Quaternion rotation, bool kicking)
        {
            ID = iD;
            this.position = position;
            this.velocity = velocity;
            this.rotation = rotation;
            this.kicking = kicking;
        }

        // INetworkSerializable
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ID);
            serializer.SerializeValue(ref position);
            serializer.SerializeValue(ref velocity);
            serializer.SerializeValue(ref rotation);
            serializer.SerializeValue(ref kicking);
        }
        // ~INetworkSerializable
    }
}