using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Knoxball
{
    public class ClientSidePredictionClient : IClientSidePredictionExecutor
    {
        private static int gameplayStateBufferSize = 1024;
        private NetworkGamePlayState latestGameplayState = new NetworkGamePlayState(); //Only used by client
        bool receivedLatestGameplayState = false;
        IClientSidePredictionGameManipulator manipulator;
        ClientSidePredictionPlayer m_localPlayer;

        public void SetGameManipulator(IClientSidePredictionGameManipulator manipulator)
        {
            this.manipulator = manipulator;
        }


        ClientSidePredictionPlayer GetLocalPlayer()
        {
            if (m_localPlayer == null)
            {
                m_localPlayer = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject().GetComponent<ClientSidePredictionPlayer>();
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
                var replayTick = this.latestGameplayState.tick;
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
            latestGameplayState = new NetworkGamePlayState(); //Only used by client
            receivedLatestGameplayState = false;
            GetLocalPlayer().ResetInputBuffer();
        }

        public void ReceivedGamePlayState(NetworkGamePlayState gamePlayState)
        {
            if (latestGameplayState.tick < gamePlayState.tick)
            {
                //Debug.Log($"current tick: ${tick} newGPTick: ${gameplayState.tick}");
                this.latestGameplayState = gamePlayState;
                this.receivedLatestGameplayState = true;

            }
        }
    }
}