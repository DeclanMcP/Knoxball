

using System.Collections.Generic;
using ClientSidePredictionMultiplayer;
using Unity.Netcode;

namespace Knoxball
{
    public class KnoxballClientSidePredictionHandler: NetworkBehaviour, INetworkGamePlayStateDelegate<NetworkGamePlayState>
    {
        ClientSidePredictionGenericManager<NetworkGamePlayState> CSPManager = new ClientSidePredictionGenericManager<NetworkGamePlayState>();

        public KnoxballClientSidePredictionHandler()
        {
            CSPManager.gamePlayStateDelegate = this;
        }

        public override void OnNetworkSpawn()
        {
            CSPManager.Start();
        }

        public NetworkGamePlayState GetGamePlayStateWithTick(int stateTick)
        {
            var gamePlayerStates = new NetworkGamePlayerState[NetworkManager.Singleton.ConnectedClientsIds.Count];
            int i = 0;
            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                var clientPlayerObject = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject;
                var clientNeworkPlayerObject = clientPlayerObject.GetComponent<ClientSidePredictionPlayer>();
                gamePlayerStates[i] = clientNeworkPlayerObject.GetCurrentPlayerState(clientNeworkPlayerObject.NetworkObjectId);
                i++;
            }
            var ballState = Game.Instance.ball.GetComponent<BallComponent>().GetCurrentState();
            return new NetworkGamePlayState(stateTick, gamePlayerStates, ballState);
        }

        public void SetGamePlayStateToState(NetworkGamePlayState gamePlayState)
        {
            foreach (NetworkGamePlayerState gamePlayerState in gamePlayState.playerStates)
            {
                var playerObject = NetworkManager.Singleton.SpawnManager.SpawnedObjects[gamePlayerState.ID];
                playerObject.GetComponent<ClientSidePredictionPlayer>().SetPlayerState(gamePlayerState);
            }
            Game.Instance.ball.GetComponent<BallComponent>().SetState(gamePlayState.ballState);
        }

        public void AddForcesToGame()
        {
            foreach (KeyValuePair<ulong, NetworkObject> keyValuePair in NetworkManager.Singleton.SpawnManager.SpawnedObjects)
            {
                var clientPlayerObject = keyValuePair.Value;
                var clientNeworkPlayerObject = clientPlayerObject.GetComponent<ClientSidePredictionGenericPlayer>();
                if (clientNeworkPlayerObject != null)
                {
                    //Debug.Log("[GameState]: " + System.Reflection.MethodBase.GetCurrentMethod().Name + ", Key:" + keyValuePair.Key);
                    clientNeworkPlayerObject.AddForces();
                }
            }
            Game.Instance.ball.GetComponent<BallComponent>().ManualUpdate();
        }

        bool INetworkGamePlayStateDelegate<NetworkGamePlayState>.IsHost()
        {
            return this.IsHost;
        }

        public void GameLoop()
        {
            CSPManager.GameLoop();
        }

        public void ResetGameBuffers()
        {
            CSPManager.ResetGameBuffers();
        }

        public void SendGamePlayState(NetworkGamePlayState gamePlayState)
        {
            SetLatestGameplayState_ClientRpc(gamePlayState);
        }

        [ClientRpc]
        private void SetLatestGameplayState_ClientRpc(NetworkGamePlayState gamePlayState)
        {
            CSPManager.ReceivedGamePlayState(gamePlayState);
        }
    }
}

