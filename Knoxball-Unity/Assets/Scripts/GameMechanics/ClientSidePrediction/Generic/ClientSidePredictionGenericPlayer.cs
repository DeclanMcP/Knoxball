using Unity.Netcode;
using UnityEngine;

namespace ClientSidePredictionMultiplayer
{
    abstract public class ClientSidePredictionGenericPlayer<T> : NetworkBehaviour where T: INetworkPlayerInputState
    {
        private static int playerInputBufferSize = 1024;
        private T[] m_playerInputBuffer = new T[playerInputBufferSize];
        public int latestInputTick = 0;

        public ClientSidePredictionGenericPlayer()
        {
        }


        public void RecordPlayerInputForTick(int tick) //Required
        {
            if (!IsOwner) { return; }
            var playerInputState = GetPlayerInputState();
            playerInputState.Tick = tick;
            if (playerInputState == null) { return; }

            StorePlayerInputState(playerInputState);
            //Debug.Log($"Stored player input state ${playerInput.direction}");

            if (IsHost)
            {
                return;
            }
            SendInput_ServerRpc(playerInputState);
        }


        [ServerRpc] // Leave (RequireOwnership = true)
        private void SendInput_ServerRpc(T inputState)
        {
            //Debug.Log("[Input] Received input, tick: " + inputState.tick + ", inputstate: " + inputState.direction + "current tick: " + Game.instance.tick);
            StorePlayerInputState(inputState);
        }

        private void StorePlayerInputState(T inputState)
        {
            m_playerInputBuffer[inputState.Tick % playerInputBufferSize] = inputState;
            latestInputTick = Mathf.Max(latestInputTick, inputState.Tick);
        }


        public void ResetInputsForTick(int tick)
        {
            m_playerInputBuffer[tick % playerInputBufferSize] = default(T);
        }

        public void ResetInputBuffer()
        {
            m_playerInputBuffer = new T[playerInputBufferSize];//TODO problematic
            latestInputTick = 0;
        }

        protected T GetPlayerInputStateForTick(int tick) {
            return m_playerInputBuffer[tick % playerInputBufferSize];
        }

        abstract public T GetPlayerInputState();

        abstract public void SetInputsForTick(int tick);

        abstract public void AddForces();
    }
}
