using UnityEngine;
using Unity.Netcode;
using ClientSidePredictionMultiplayer;

namespace Knoxball
{
    public class NetworkPlayerInputState : INetworkPlayerInputState
    {
        public int Tick;
        public Vector3 direction = Vector3.zero;
        public bool kicking;

        public NetworkPlayerInputState()
        {

        }

        public NetworkPlayerInputState(int tick, Vector3 direction, bool kicking)
        {
            this.Tick = tick;
            this.direction = direction;
            this.kicking = kicking;
        }

        int INetworkPlayerInputState.Tick { get => this.Tick; set => this.Tick = value; }

        //int INetworkPlayerInputState.Tick => this.Tick;

        // INetworkSerializable
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Tick);
            serializer.SerializeValue(ref direction);
            serializer.SerializeValue(ref kicking);
        }
        // ~INetworkSerializable
    }
}