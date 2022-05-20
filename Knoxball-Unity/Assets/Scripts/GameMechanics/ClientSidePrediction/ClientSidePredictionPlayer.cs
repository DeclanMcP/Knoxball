﻿using Unity.Netcode;
using UnityEngine;

namespace Knoxball
{
    public class ClientSidePredictionPlayer: NetworkBehaviour
    {
        private static int playerInputBufferSize = 1024;
        private NetworkPlayerInputState[] m_playerInputBuffer = new NetworkPlayerInputState[playerInputBufferSize];//TODO problematic
        public int latestInputTick = 0;
        private IClientSidePredictionPlayerController m_PlayerController;//TODO problematic

        public void SetPlayerController(IClientSidePredictionPlayerController playerController)
        {
            this.m_PlayerController = playerController;
        }

        public void RecordPlayerInputForTick(int tick)
        {
            if (!IsOwner) { return; }//TODO problematic
            if (Game.Instance.inGameState != InGameState.Playing) { return; }//TODO problematic
            m_PlayerController.UpdatePlayerInput();//Should this be here..?

            //Have the player generate the inputstate, we want this monobehaviour to be generic if possible
            var playerInput = m_PlayerController.GetPlayerInputState();
            playerInput.tick = tick;

            StorePlayerInputState(playerInput);
            //Debug.Log($"Stored player input state ${playerInput.direction}");

            if (IsHost)//TODO problematic
            {
                return;
            }
            SendInput_ServerRpc(playerInput);
        }

        [ServerRpc] // Leave (RequireOwnership = true)
        private void SendInput_ServerRpc(NetworkPlayerInputState inputState)
        {
            //Debug.Log("[Input] Received input, tick: " + inputState.tick + ", inputstate: " + inputState.direction + "current tick: " + Game.instance.tick);
            StorePlayerInputState(inputState);
        }

        private void StorePlayerInputState(NetworkPlayerInputState inputState)
        {
            m_playerInputBuffer[inputState.tick % playerInputBufferSize] = inputState;
            latestInputTick = Mathf.Max(latestInputTick, inputState.tick);
        }

        public NetworkGamePlayerState GetCurrentPlayerState(ulong iD)//TODO problematic
        {
            var playerState = m_PlayerController.GetPlayerState();
            playerState.ID = iD;
            return playerState;
        }

        public void SetPlayerState(NetworkGamePlayerState playerState)//TODO problematic//Make this state generic?
        {
            m_PlayerController.SetPlayerState(playerState);
        }

        public void SetInputsForTick(int tick)
        {
            if (m_playerInputBuffer[tick % playerInputBufferSize] == null)
            {
                m_PlayerController.ResetInputState();
                return;
            }
            NetworkPlayerInputState playerInputState = m_playerInputBuffer[tick % playerInputBufferSize];//TODO problematic
            m_PlayerController.SetInputStateToState(playerInputState);
        }

        public void ResetInputsForTick(int tick)
        {
            m_playerInputBuffer[tick % playerInputBufferSize] = null;
        }

        public void ResetInputBuffer()
        {
            m_playerInputBuffer = new NetworkPlayerInputState[playerInputBufferSize];//TODO problematic
            latestInputTick = 0;
        }

        public void AddForces()
        {
            m_PlayerController.AddForces();
        }
    }
}