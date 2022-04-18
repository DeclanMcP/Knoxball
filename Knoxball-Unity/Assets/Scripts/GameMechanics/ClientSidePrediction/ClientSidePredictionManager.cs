
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Knoxball
{
    public class ClientSidePredictionManager: NetworkBehaviour
    {
        public NetworkPlayerComponent localPlayer;
        public GameObject ball;

        public int tick = 0;
        float elapsedTime = 0;
        private static int gameplayStateBufferSize = 1024;
        private NetworkGamePlayState[] gameplayStateBuffer = new NetworkGamePlayState[gameplayStateBufferSize];//For the host
        int latestSentGamePlayStateTick = 0;

        private NetworkGamePlayState latestGameplayState = new NetworkGamePlayState(); //Only used by client
        bool receivedLatestGameplayState = false;

        private void Awake()
        {
            
        }

        private void Start()
        {
        }

        public override void OnNetworkSpawn()
        {
            ball = Game.Instance.ball;
            localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<NetworkPlayerComponent>();
        }

        public void GameLoop()
        {
            this.elapsedTime += Time.deltaTime;
            //Debug.Log("Time.deltaTime: " + Time.deltaTime + ", Time.fixedDeltaTime: " + Time.fixedDeltaTime);
            while (this.elapsedTime >= Time.fixedDeltaTime)
            {
                this.elapsedTime -= Time.fixedDeltaTime;
                //Send inputs for tick
                //Debug.Log("[Replay] Main loop: RecordPlayerInputForTick: " + localPlayer);
                localPlayer.RecordPlayerInputForTick(tick);


                SetPlayerInputsForTick(tick);
                AddForcesToGame();

                Physics.Simulate(Time.fixedDeltaTime);

                if (IsHost)
                {
                    ResetPlayerInputsForTick(tick + (gameplayStateBufferSize / 2));
                    var gameplayState = GetGamePlayStateWithTick(tick);
                    gameplayStateBuffer[tick % gameplayStateBufferSize] = gameplayState;
                }
                else
                {
                    localPlayer.ResetInputsForTick(tick + (gameplayStateBufferSize / 2));
                }
                tick++;
            }

            if (!IsHost && receivedLatestGameplayState)
            {
                var replayTick = this.latestGameplayState.tick;
                SetGamePlayStateToState(this.latestGameplayState);
                //Debug.Log($"Received GPState, GSPTick: ${replayTick}, current tick: ${tick}");
                while (replayTick < tick)
                {
                    //Apply inputs of this tick from locally stored states
                    localPlayer.SetInputsForTick(replayTick);
                    //We arent simulating
                    AddForcesToGame();
                    Physics.Simulate(Time.fixedDeltaTime);
                    replayTick++;
                }
                this.receivedLatestGameplayState = false;
            }
            else if (IsHost)
            {
                //Keep track of the earliest tick received that contained input that hadnt been simulated
                //Resimulate from there.
                int latestSyncedInputTick = LatestSyncedInputStateTick_Server();
                if (latestSyncedInputTick > latestSentGamePlayStateTick)
                {
                    var replayTick = latestSentGamePlayStateTick;
                    SetGamePlayStateToState(gameplayStateBuffer[replayTick % gameplayStateBufferSize]);

                    while (replayTick < tick)
                    {
                        SetPlayerInputsForTick(replayTick);
                        AddForcesToGame();
                        Physics.Simulate(Time.fixedDeltaTime);
                        //Now we have resimulated this state, we need to save it again.
                        gameplayStateBuffer[replayTick % gameplayStateBufferSize] = GetGamePlayStateWithTick(replayTick);

                        if (latestSyncedInputTick == replayTick)
                        {
                            //Debug.Log("[SendState] tick: " + tick + ", replayTick: " + replayTick);
                            SetLatestGameplayState_ClientRpc(gameplayStateBuffer[replayTick % gameplayStateBufferSize]);
                            latestSentGamePlayStateTick = latestSyncedInputTick;
                        }

                        replayTick++;
                    }
                }

            }
        }

        public void ResetGameBuffers()
        {
            tick = 0;
            if (IsHost)
            {
                gameplayStateBuffer = new NetworkGamePlayState[gameplayStateBufferSize];//For the host
                latestSentGamePlayStateTick = 0;
                ResetPlayerInputBuffers();

            }
            else
            {
                latestGameplayState = new NetworkGamePlayState(); //Only used by client
                receivedLatestGameplayState = false;
                localPlayer.ResetInputBuffer();
            }
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
            var ballState = ball.GetComponent<BallComponent>().GetCurrentState();
            return new NetworkGamePlayState(stateTick, gamePlayerStates, ballState);
        }


        [ClientRpc]
        private void SetLatestGameplayState_ClientRpc(NetworkGamePlayState gameplayState)
        {
            if (IsHost)
            {
                //Debug.Log("OOps shoudlnt be here");
                return;
            }
            if (latestGameplayState.tick < gameplayState.tick)
            {
                //Debug.Log($"current tick: ${tick} newGPTick: ${gameplayState.tick}");
                this.latestGameplayState = gameplayState;
                this.receivedLatestGameplayState = true;

            }
        }

        private void SetGamePlayStateToState(NetworkGamePlayState gamePlayState)
        {
            foreach (NetworkGamePlayerState gamePlayerState in gamePlayState.playerStates)
            {
                var playerObject = NetworkManager.Singleton.SpawnManager.SpawnedObjects[gamePlayerState.ID];
                //Debug.Log("[Client] gamePlayerState.ID: " + gamePlayerState.ID + ", playerObject: " + playerObject);
                playerObject.GetComponent<NetworkPlayerComponent>().SetPlayerState(gamePlayerState);
            }
            ball.GetComponent<BallComponent>().SetState(gamePlayState.ballState);
        }

        void SetPlayerInputsForTick(int tick)
        {
            //Apply inputs of this tick from locally stored states
            foreach (KeyValuePair<ulong, NetworkObject> keyValuePair in NetworkManager.Singleton.SpawnManager.SpawnedObjects)
            {
                var clientPlayerObject = keyValuePair.Value;
                var clientNeworkPlayerObject = clientPlayerObject.GetComponent<NetworkPlayerComponent>();
                if (clientNeworkPlayerObject != null)
                {
                    clientNeworkPlayerObject.SetInputsForTick(tick);
                }
            }
        }

        void ResetPlayerInputsForTick(int tick)
        {
            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                var clientPlayerObject = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject;
                var clientNeworkPlayerObject = clientPlayerObject.GetComponent<NetworkPlayerComponent>();
                //Debug.Log("[Replay] Found player by id: " + clientId + ", player: " + clientNeworkPlayerObject);
                clientNeworkPlayerObject.ResetInputsForTick(tick);

            }
        }
        void AddForcesToGame()
        {
            foreach (KeyValuePair<ulong, NetworkObject> keyValuePair in NetworkManager.Singleton.SpawnManager.SpawnedObjects)
            {
                var clientPlayerObject = keyValuePair.Value;
                var clientNeworkPlayerObject = clientPlayerObject.GetComponent<NetworkPlayerComponent>();
                if (clientNeworkPlayerObject != null)
                {
                    //Debug.Log("[GameState]: " + System.Reflection.MethodBase.GetCurrentMethod().Name + ", Key:" + keyValuePair.Key);
                    clientNeworkPlayerObject.ManualUpdate();
                }
            }
            ball.GetComponent<BallComponent>().ManualUpdate();
        }
    }
}
