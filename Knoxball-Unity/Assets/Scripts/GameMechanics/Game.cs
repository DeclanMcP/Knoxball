using System.Collections;
using UnityEngine;
using Unity.Netcode;
using TMPro;
using System;
using System.Collections.Generic;

public delegate void KickCallBack(bool isPressed);

namespace Knoxball
{

    public class Game : NetworkBehaviour, IReceiveMessages
    {
        public static Game Instance;

        int homeTeamScore = 0;
        int awayTeamScore = 0;
        private NetworkVariable<int> m_homeTeamScore = new NetworkVariable<int>(NetworkVariableReadPermission.Everyone, 0);
        private NetworkVariable<int> m_awayTeamScore = new NetworkVariable<int>(NetworkVariableReadPermission.Everyone, 0);
        private NetworkVariable<float> m_timeRemaining = new NetworkVariable<float>(NetworkVariableReadPermission.Everyone, 180);
        private NetworkVariable<InGameState> m_InGameState = new NetworkVariable<InGameState>(NetworkVariableReadPermission.Everyone, InGameState.Starting);

        public VariableJoystick variableJoystick;
        public KickButton kickButton;
        public KickCallBack kickCallBack;

        public GameObject ball;
        public GameObject stadium;
        public GameObject celebration;
        public GameObject mainCamera;
        public GameObject menuOverlay;
        public WinnerAnnouncement winnerAnnouncement;

        //MOVE TO THE GAME UI!!
        public TMP_Text timeText;
        public TMP_Text score;
        float timeRemaining = 180;
        public InGameState inGameState;

        private LobbyUser m_LocalUser;
        private LocalLobby m_lobby;
        private Dictionary<ulong, string> m_clientIdToLobbyUserId = new Dictionary<ulong, string>();
        private Action m_onConnectionVerified, m_onGameEnd;
        private int m_expectedPlayerCount;

        private void Awake()
        {
            //Check if instance already exists
            if (Instance == null)
                Instance = this;
            else if (Instance != this)
                Destroy(gameObject);

        }

        // Start is called before the first frame update
        void Start()
        {
            Locator.Get.Messenger.Subscribe(this);
            kickButton.customEvent = OnKickClicked;
            score.text = CurrentScore();
        }


        public void Initialize(Action onConnectionVerified, int expectedPlayerCount, Action onGameEnd, LobbyUser localUser, LocalLobby lobby)
        {
            m_onConnectionVerified = onConnectionVerified;
            m_expectedPlayerCount = expectedPlayerCount;
            m_onGameEnd = onGameEnd;
            m_LocalUser = localUser;
            m_lobby = lobby;
            //foreach (KeyValuePair<string, LobbyUser> entry in m_lobby.LobbyUsers)
            //{
            //    Debug.Log("Lobby User: Id: " + entry.Key + ", name: " + entry.Value.DisplayName + "");
            //}
        }


        public override void OnNetworkSpawn()
        {
            Debug.Log("[GameState]: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            VerifyConnection_ServerRpc(NetworkManager.Singleton.LocalClientId, m_LocalUser.ID);
        }

        void Update()
        {
            CheckHomeScore();
            CheckAwayScore();
            CheckInGameState();
        }

        void CheckHomeScore()
        {
            if (homeTeamScore != m_homeTeamScore.Value)
            {
                homeTeamScore = m_homeTeamScore.Value;
                score.text = CurrentScore();
            }
        }

        void CheckAwayScore()
        {
            if (awayTeamScore != m_awayTeamScore.Value)
            {
                awayTeamScore = m_awayTeamScore.Value;
                score.text = CurrentScore();
            }
        }

        void CheckInGameState()
        {
            if (inGameState != m_InGameState.Value)
            {
                inGameState = m_InGameState.Value;
                timeRemaining = m_timeRemaining.Value;
            }
            if (inGameState == InGameState.Playing)
            {
                timeRemaining -= Time.deltaTime;
                timeRemaining = Mathf.Max(timeRemaining, 0);

                GetComponent<KnoxballClientSidePredictionHandler>().GameLoop();
                
                DisplayTime(timeRemaining);
            }
            if (timeRemaining <= 0 && IsHost)
            {
                SetInGameState(InGameState.Ending);
            }
            if (inGameState == InGameState.Ending)
            {
                AnounceWinner();
                StartCoroutine(EndGame());
            }
        }

        void AnounceWinner()
        {
            winnerAnnouncement.AnnounceWinner(WinningTeam());
        }

        GameTeam WinningTeam()
        {
            if (homeTeamScore > awayTeamScore)
            {
                return GameTeam.Home;
            }
            else if (awayTeamScore > homeTeamScore)
            {
                return GameTeam.Away;
            }
            return GameTeam.Unknown;
        }

        IEnumerator EndGame()
        {
            yield return new WaitForSeconds(2f);
            SetInGameState(InGameState.Finished);
            m_onGameEnd();
        }

        IEnumerator ResetGameAsync()
        {
            yield return new WaitForSeconds(2f);
            ResetGame();
        }
        void ResetGame() {
            
            ResetGameObject(ball, new Vector3(0, 0, 0));
            ResetPlayerLocations();
            if (IsHost)
            {
                //TODO look at putting this outside of the if statement? It has non host code
                GetComponent<KnoxballClientSidePredictionHandler>().ResetGameBuffers();
            }
            stadium.GetComponent<StadiumComponent>().Reset();
            StopCelebration();
            SetInGameState(InGameState.Playing);
        }

        void ResetPlayerLocations()
        {
            foreach (KeyValuePair<ulong, NetworkObject> keyValuePair in NetworkManager.Singleton.SpawnManager.SpawnedObjects)
            {
                var clientPlayerObject = keyValuePair.Value;
                var clientNeworkPlayerObject = clientPlayerObject.GetComponent<NetworkPlayerComponent>();
                if (clientNeworkPlayerObject != null)
                {
                    clientNeworkPlayerObject.ResetLocation();
                }
            }
        }

        void ResetGameObject(GameObject gameObject, Vector3 position)
        {
            gameObject.transform.position = position;
            gameObject.GetComponent<Rigidbody>().velocity = new Vector3(0, 0, 0);
        }

        public void HomeTeamScored()
        {
            if (IsHost)
            {
                SetHomeTeamScore_ServerRpc(m_homeTeamScore.Value + 1);
            }
            Celebrate();
            StartCoroutine(ResetGameAsync());
        }

        [ServerRpc]
        private void SetHomeTeamScore_ServerRpc(int score)
        {
            m_homeTeamScore.Value = score;
            ResetGameBuffers_ClientRpc();
        }

        [ClientRpc]
        private void ResetGameBuffers_ClientRpc()
        {
            GetComponent<KnoxballClientSidePredictionHandler>().ResetGameBuffers();
        }

        public void AwayTeamScored()
        {
            if (IsHost)
            {
                SetAwayTeamScore_ServerRpc(m_awayTeamScore.Value + 1);
            }
            Celebrate();
            StartCoroutine(ResetGameAsync());
        }


        [ServerRpc]
        private void SetAwayTeamScore_ServerRpc(int score)
        {
            m_awayTeamScore.Value = score;
            ResetGameBuffers_ClientRpc();
        }

        public void Celebrate()
        {
            SetInGameState(InGameState.GoalCelebrating);
            score.text = CurrentScore();
            //print(CurrentScore());
            celebration.GetComponent<CelebrationComponent>().Celebrate();
            mainCamera.SetActive(false);
        }

        public void StopCelebration()
        {
            mainCamera.SetActive(true);
            celebration.GetComponent<CelebrationComponent>().StopCelebration();
        }

        string CurrentScore()
        {
            return "" + homeTeamScore + ":" + awayTeamScore + "";
        }

        public void OnMenuClicked()
        {
            //print("onMenuClicked!");
            menuOverlay.SetActive(true);
        }

        public void OnKickClicked(bool isPressed)
        {
            //print("onKickClicked!" + isPressed);
            kickCallBack(isPressed);
        }

        public void OnReceiveMessage(MessageType type, object msg)
        {

        }

        public LobbyUser LocalUser()
        {
            return m_LocalUser;
        }

        void DisplayTime(float timeToDisplay)
        {
            float minutes = Mathf.FloorToInt(timeToDisplay / 60);
            float seconds = Mathf.FloorToInt(timeToDisplay % 60);
            timeText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }

        /// <summary>
        /// To verify the connection, invoke a server RPC call that then invokes a client RPC call. After this, the actual setup occurs.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        private void VerifyConnection_ServerRpc(ulong clientId, string lobbyId)
        {
            //Debug.Log("[GameState]: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            m_clientIdToLobbyUserId.Add(clientId, lobbyId);
            VerifyConnection_ClientRpc(clientId, lobbyId);
        }

        [ClientRpc]
        private void VerifyConnection_ClientRpc(ulong clientId, string lobbyId)
        {
            Debug.Log("[GameState]: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                //Debug.Log("[GameState]: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                VerifyConnectionConfirm_ServerRpc(clientId);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void VerifyConnectionConfirm_ServerRpc(ulong clientId)
        {
            bool areAllPlayersConnected = NetworkManager.ConnectedClients.Count >= m_expectedPlayerCount;
            //The game will begin at this point, or else there's a timeout for booting any unconnected players.
            //Debug.Log("[GameState]: " + System.Reflection.MethodBase.GetCurrentMethod().Name + "areAllPlayersConnected: " + areAllPlayersConnected);
            var gameData = GenerateGameData();
            VerifyConnectionConfirm_ClientRpc(clientId, areAllPlayersConnected, gameData);
        }

        private NetworkGameData GenerateGameData()
        {
            var playerDatas = new NetworkPlayerData[m_clientIdToLobbyUserId.Keys.Count];
            var index = 0;
            foreach (var clientId in m_clientIdToLobbyUserId.Keys)
            {
                var lobbyId = m_clientIdToLobbyUserId[clientId];
                playerDatas[index] = new NetworkPlayerData(clientId, lobbyId);
                index++;
            }
            var gameData = new NetworkGameData(playerDatas);
            return gameData;
        }

        [ClientRpc]
        private void VerifyConnectionConfirm_ClientRpc(ulong clientId, bool canBeginGame, NetworkGameData gameData)
        {
            Debug.Log("[GameState]: " + System.Reflection.MethodBase.GetCurrentMethod().Name + "clientId: " + clientId);
            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                m_onConnectionVerified?.Invoke();
                
            }

            if (canBeginGame)
            {
                foreach (var playerData in gameData.playerDatas)
                {
                    if (!m_clientIdToLobbyUserId.ContainsKey(clientId))
                    {
                        Debug.Log($"[GameState]: gameData: ${playerData.clientId} ${playerData.lobbyId}");
                        m_clientIdToLobbyUserId.Add(playerData.clientId, playerData.lobbyId);
                    }
                }
                BeginGame();
            }
        }

        private void BeginGame()
        {
            Debug.Log("[GameState]: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            
            ResetGame();
            SetInGameState(InGameState.Playing);
        }

        private void SetInGameState(InGameState inGameState)
        {
            if (IsHost)
            {
                SetInGameState_ServerRpc(inGameState);
                SetTimeRemaining_ServerRpc(timeRemaining);
            }
        }

        [ServerRpc]
        private void SetInGameState_ServerRpc(InGameState inGameState)
        {
            m_InGameState.Value = inGameState;
        }

        [ServerRpc]
        private void SetTimeRemaining_ServerRpc(float timeRemaining)
        {
            m_timeRemaining.Value = timeRemaining;
        }

        public LobbyUser GetLobbyUserForClientId(ulong clientId)
        {
            if (!m_clientIdToLobbyUserId.ContainsKey(clientId))
            {
                Debug.Log("User does not exist: " + clientId);
                return null;
            }
            var lobbyUserId = m_clientIdToLobbyUserId[clientId];
            Debug.Log("[LobbyUser] clientId: " + clientId + ", lobbyUserId: " + lobbyUserId);
            return m_lobby.LobbyUsers[lobbyUserId];
        }
    }
}