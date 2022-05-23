using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ClientSidePredictionMultiplayer
{
    public class ClientSidePredictionGenericClient<T> : IClientSidePredictionGenericExecutor<T> where T : INetworkGamePlayState
    {
        private static int gameplayStateBufferSize = 1024;
        private T latestGameplayState = default(T);
        bool receivedLatestGameplayState = false;
        IClientSidePredictionGenericGameManipulator<T> manipulator;
        ClientSidePredictionGenericPlayer m_localPlayer;

        public void SetGameManipulator(IClientSidePredictionGenericGameManipulator<T> manipulator)
        {
            this.manipulator = manipulator;
        }


        ClientSidePredictionGenericPlayer GetLocalPlayer()
        {
            if (m_localPlayer == null)
            {
                m_localPlayer = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject().GetComponent<ClientSidePredictionGenericPlayer>();
            }
            return m_localPlayer;
        }

        public void ResetPlayerInputsForTick(int tick)
        {
            GetLocalPlayer().ResetInputsForTick(tick + (gameplayStateBufferSize / 2));
        }

        public void SaveCurrentGamePlayStateWithTick(int tick)
        {
        }

        public void ReplayPhysics(int currentTick)
        {
            if (receivedLatestGameplayState)
            {
                var replayTick = this.latestGameplayState.Tick;
                manipulator.SetGamePlayStateToState(this.latestGameplayState);
                //Debug.Log($"Received GPState, GSPTick: ${replayTick}, current tick: ${tick}");
                while (replayTick < currentTick)
                {
                    //Apply inputs of this tick from locally stored states
                    GetLocalPlayer().SetInputsForTick(replayTick);
                    //We arent simulating
                    manipulator.AddForcesToGame();
                    Physics.Simulate(Time.fixedDeltaTime);
                    replayTick++;
                }
                this.receivedLatestGameplayState = false;
            }
        }

        public void ResetGameBuffers()
        {
            latestGameplayState = default(T);
            receivedLatestGameplayState = false;
            GetLocalPlayer().ResetInputBuffer();
        }

        public void ReceivedGamePlayState(T gamePlayState)
        {
            if (latestGameplayState == null || latestGameplayState?.Tick < gamePlayState.Tick)
            {
                //Debug.Log($"newGPTick: ${gamePlayState.Tick}");
                this.latestGameplayState = gamePlayState;
                this.receivedLatestGameplayState = true;

            }
        }
    }
}