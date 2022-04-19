using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Knoxball
{
    public class ClientSidePredictionServer : IClientSidePredictionExecutor
    {
        private static int gameplayStateBufferSize = 1024;
        private NetworkGamePlayState[] gameplayStateBuffer = new NetworkGamePlayState[gameplayStateBufferSize];//For the host
        int latestSentGamePlayStateTick = 0;
        IClientSidePredictionGameManipulator manipulator;

        public void SetGameManipulator(IClientSidePredictionGameManipulator manipulator)
        {
            this.manipulator = manipulator;
        }

        public void ResetPlayerInputsForTick(int tick)
        {
            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                var clientPlayerObject = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject;
                var clientNeworkPlayerObject = clientPlayerObject.GetComponent<NetworkPlayerComponent>();
                //Debug.Log("[Replay] Found player by id: " + clientId + ", player: " + clientNeworkPlayerObject);
                clientNeworkPlayerObject.ResetInputsForTick(tick);

            }
        }

        public void SaveCurrentGamePlayStateWithTick(int tick)
        {
            var gameplayState = GetGamePlayStateWithTick(tick);
            gameplayStateBuffer[tick % gameplayStateBufferSize] = gameplayState;
        }


        private NetworkGamePlayState GetGamePlayStateWithTick(int stateTick)
        {
            var gamePlayerStates = new NetworkGamePlayerState[NetworkManager.Singleton.ConnectedClientsIds.Count];
            int i = 0;
            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                var clientPlayerObject = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject;
                var clientNeworkPlayerObject = clientPlayerObject.GetComponent<NetworkPlayerComponent>();
                gamePlayerStates[i] = clientNeworkPlayerObject.GetCurrentPlayerState(clientNeworkPlayerObject.NetworkObjectId);
                i++;

            }
            var ballState = Game.Instance.ball.GetComponent<BallComponent>().GetCurrentState();
            return new NetworkGamePlayState(stateTick, gamePlayerStates, ballState);
        }

        public void ReplayPhysics(int currentTick)
        {
            //Keep track of the earliest tick received that contained input that hadnt been simulated
            //Resimulate from there.
            int latestSyncedInputTick = LatestSyncedInputStateTick_Server();
            if (latestSyncedInputTick > latestSentGamePlayStateTick)
            {
                var replayTick = latestSentGamePlayStateTick;
                manipulator.SetGamePlayStateToState(gameplayStateBuffer[replayTick % gameplayStateBufferSize]);

                while (replayTick < currentTick)
                {
                    manipulator.SetPlayerInputsForTick(replayTick);
                    manipulator.AddForcesToGame();
                    Physics.Simulate(Time.fixedDeltaTime);
                    //Now we have resimulated this state, we need to save it again.
                    gameplayStateBuffer[replayTick % gameplayStateBufferSize] = GetGamePlayStateWithTick(replayTick);

                    if (latestSyncedInputTick == replayTick)
                    {
                        //Debug.Log("[SendState] tick: " + tick + ", replayTick: " + replayTick);
                        manipulator.SendGamePlayState(gameplayStateBuffer[replayTick % gameplayStateBufferSize]);
                        latestSentGamePlayStateTick = latestSyncedInputTick;
                    }

                    replayTick++;
                }
            }
        }


        private int LatestSyncedInputStateTick_Server()
        {
            int currentLowestTick = int.MaxValue;
            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                var clientPlayerObject = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject;
                var clientNeworkPlayerObject = clientPlayerObject.GetComponent<NetworkPlayerComponent>();
                currentLowestTick = Math.Min(clientNeworkPlayerObject.latestInputTick, currentLowestTick);
            }
            if (currentLowestTick == int.MaxValue)
            {
                currentLowestTick = 0;
            }
            //Debug.Log($"currentLowestTick: ${currentLowestTick}");
            return currentLowestTick;
        }


        public void ResetGameBuffers()
        {
            gameplayStateBuffer = new NetworkGamePlayState[gameplayStateBufferSize];//For the host
            latestSentGamePlayStateTick = 0;
            ResetPlayerInputBuffers();
        }

        void ResetPlayerInputBuffers()
        {
            foreach (KeyValuePair<ulong, NetworkObject> keyValuePair in NetworkManager.Singleton.SpawnManager.SpawnedObjects)
            {
                var clientPlayerObject = keyValuePair.Value;
                var clientNeworkPlayerObject = clientPlayerObject.GetComponent<NetworkPlayerComponent>();
                if (clientNeworkPlayerObject != null)
                {
                    clientNeworkPlayerObject.ResetInputBuffer();
                }
            }
        }

        public void ReceivedGamePlayState(NetworkGamePlayState gamePlayState)
        {
        }
    }
}