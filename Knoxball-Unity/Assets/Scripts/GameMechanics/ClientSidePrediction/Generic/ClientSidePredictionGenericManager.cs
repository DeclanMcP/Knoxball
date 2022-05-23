﻿
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ClientSidePredictionMultiplayer
{
    public class ClientSidePredictionGenericManager<T>: IClientSidePredictionGenericGameManipulator<T> where T : INetworkGamePlayState
    {
        private int tick = 0;
        float elapsedTime = 0;
        IClientSidePredictionGenericExecutor<T> executor;
        private static int gameplayStateBufferSize = 1024;
        ClientSidePredictionGenericPlayer<INetworkPlayerInputState> m_localPlayer;
        public INetworkGamePlayStateDelegate<T> gamePlayStateDelegate;

        public void Start()
        {
            Debug.Log($"Is host? ${gamePlayStateDelegate.IsHost()}");
            if (gamePlayStateDelegate.IsHost())
            {
                executor = new ClientSidePredictionGenericServer<T>();
            }
            else
            {
                executor = new ClientSidePredictionGenericClient<T>();
            }
            executor.SetGameManipulator(this);
        }

        ClientSidePredictionGenericPlayer<INetworkPlayerInputState> GetLocalPlayer()
        {

            if (m_localPlayer == null)
            {
                m_localPlayer = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject().GetComponent<ClientSidePredictionGenericPlayer<INetworkPlayerInputState>>();
            }
            return m_localPlayer;
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
                GetLocalPlayer().RecordPlayerInputForTick(tick);


                SetPlayerInputsForTick(tick);
                AddForcesToGame();

                Physics.Simulate(Time.fixedDeltaTime);

                executor.ResetPlayerInputsForTick(tick + (gameplayStateBufferSize / 2));
                executor.SaveCurrentGamePlayStateWithTick(tick);
                tick++;
            }

            executor.ReplayPhysics(tick);
        }


        public void ResetGameBuffers()
        {
            tick = 0;
            if (executor != null)
            {
                executor.ResetGameBuffers();
            }
        }

        public void SendGamePlayState(T gamePlayState)
        {
            gamePlayStateDelegate.SendGamePlayState(gamePlayState);
        }

        public void ReceivedGamePlayState(T gamePlayState)
        {
            executor.ReceivedGamePlayState(gamePlayState);
        }

        public void SetPlayerInputsForTick(int tick)
        {
            //Apply inputs of this tick from locally stored states
            foreach (KeyValuePair<ulong, NetworkObject> keyValuePair in NetworkManager.Singleton.SpawnManager.SpawnedObjects)
            {
                var clientPlayerObject = keyValuePair.Value;
                var clientNeworkPlayerObject = clientPlayerObject.GetComponent<ClientSidePredictionGenericPlayer<INetworkPlayerInputState>>();
                if (clientNeworkPlayerObject != null)
                {
                    clientNeworkPlayerObject.SetInputsForTick(tick);
                }
            }
        }

        public void AddForcesToGame()
        {
            gamePlayStateDelegate.AddForcesToGame();
        }

        public void SetGamePlayStateToState(T gamePlayState)
        {
            gamePlayStateDelegate.SetGamePlayStateToState(gamePlayState);
        }

        public T GetGamePlayStateWithTick(int stateTick)
        {
            return gamePlayStateDelegate.GetGamePlayStateWithTick(stateTick);
        }
    }
}
