﻿
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Knoxball
{
    public class ClientSidePredictionManager: NetworkBehaviour, IClientSidePredictionGameManipulator
    {
        public int tick = 0;
        float elapsedTime = 0;
        IClientSidePredictionExecutor executor;

        public override void OnNetworkSpawn()
        {
            Debug.Log($"Is host? ${IsHost}, Is client? ${IsClient}");
            if (IsHost)
            {
                executor = new ClientSidePredictionServer();
            } else
            {
                executor = new ClientSidePredictionClient();
            }
            executor.SetGameManipulator(this);
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
                var localPlayer = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject().GetComponent<NetworkPlayerComponent>();
                localPlayer.RecordPlayerInputForTick(tick);


                SetPlayerInputsForTick(tick);
                AddForcesToGame();

                Physics.Simulate(Time.fixedDeltaTime);

                executor.ResetPlayerInputsForTick(tick);
                executor.SaveCurrentGamePlayStateWithTick(tick);
                tick++;
            }

            executor.ReplayPhysics(tick);
        }


        public void ResetGameBuffers()
        {
            if (executor != null)
            {
                executor.ResetGameBuffers();
            }
        }


        public void SendGamePlayState(NetworkGamePlayState gamePlayState)
        {
            SetLatestGameplayState_ClientRpc(gamePlayState);
        }

        [ClientRpc]
        private void SetLatestGameplayState_ClientRpc(NetworkGamePlayState gamePlayState)
        {
            executor.ReceivedGamePlayState(gamePlayState);
        }

        public void SetPlayerInputsForTick(int tick)
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


        public void AddForcesToGame()
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
            Game.Instance.ball.GetComponent<BallComponent>().ManualUpdate();
        }

        public void SetGamePlayStateToState(NetworkGamePlayState gamePlayState)
        {
            foreach (NetworkGamePlayerState gamePlayerState in gamePlayState.playerStates)
            {
                var playerObject = NetworkManager.Singleton.SpawnManager.SpawnedObjects[gamePlayerState.ID];
                //Debug.Log("[Client] gamePlayerState.ID: " + gamePlayerState.ID + ", playerObject: " + playerObject);
                playerObject.GetComponent<NetworkPlayerComponent>().SetPlayerState(gamePlayerState);
            }
            Game.Instance.ball.GetComponent<BallComponent>().SetState(gamePlayState.ballState);
        }
    }
}
