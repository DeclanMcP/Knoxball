using Unity.Netcode;
using UnityEngine;

namespace Knoxball
{

    public struct NetworkBallState : INetworkSerializable
    {
        internal Vector3 position;
        internal Vector3 velocity;
        internal Quaternion rotation;


        public NetworkBallState(Vector3 position, Vector3 velocity, Quaternion rotation)
        {
            this.position = position;
            this.velocity = velocity;
            this.rotation = rotation;
        }

        // INetworkSerializable
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref position);
            serializer.SerializeValue(ref velocity);
            serializer.SerializeValue(ref rotation);
        }
        // ~INetworkSerializable
    }
}