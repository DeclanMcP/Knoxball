using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using TMPro;
using System;
using System.Collections.Generic;

public delegate void KickCallBack(bool isPressed);

namespace Knoxball
{
    public enum InGameState
    {
        Starting = 1,
        Playing,
        GoalCelebrating,
        Ending,
        Finished
    }

    public enum GameTeam
    {
        Unknown,
        Home,
        Away
    }

    internal class GamePlayState: INetworkSerializable
    {
        internal int tick;
        internal BallState ballState = new BallState();
        internal GamePlayerState[] playerStates;

        public GamePlayState()
        {
            this.ballState = new BallState();
        }

        public GamePlayState(int tick, GamePlayerState[] playerStates, BallState ballState)
        {
            this.tick = tick;
            this.ballState = ballState;
            this.playerStates = playerStates;
        }

        // INetworkSerializable
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref tick);
            serializer.SerializeValue(ref ballState);
            //serializer.SerializeValue(ref playerStates);
            // Length
            int length = 0;
            if (!serializer.IsReader)
            {
                length = playerStates.Length;
            }

            serializer.SerializeValue(ref length);

            // Array
            if (serializer.IsReader)
            {
                playerStates = new GamePlayerState[length];
            }

            for (int n = 0; n < length; ++n)
            {
                serializer.SerializeValue(ref playerStates[n]);
            }
        }
        // ~INetworkSerializable
    }

    public struct GamePlayerState: INetworkSerializable
    {
        internal ulong ID;
        internal Vector3 position;
        internal Vector3 velocity;
        internal Quaternion rotation;
        internal bool kicking;

        public GamePlayerState(ulong iD, Vector3 position, Vector3 velocity, Quaternion rotation, bool kicking)
        {
            ID = iD;
            this.position = position;
            this.velocity = velocity;
            this.rotation = rotation;
            this.kicking = kicking;
        }

        // INetworkSerializable
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ID);
            serializer.SerializeValue(ref position);
            serializer.SerializeValue(ref velocity);
            serializer.SerializeValue(ref rotation);
            serializer.SerializeValue(ref kicking);
        }
        // ~INetworkSerializable
    }

    public struct BallState: INetworkSerializable
    {
        internal Vector3 position;
        internal Vector3 velocity;
        internal Quaternion rotation;

        //public BallState()
        //{
        //}

        public BallState(Vector3 position, Vector3 velocity, Quaternion rotation)
        {
            this.position = position;
            this.velocity = velocity;
            this.rotation = rotation;
        }

        // INetworkSerializable
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref position);
            serializer.SerializeValue(ref velocity);
            serializer.SerializeValue(ref rotation);
        }
        // ~INetworkSerializable
    }

    public class Game : NetworkBehaviour, IReceiveMessages
    {
        public static Game instance;

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
        public NetworkPlayerComponent localPlayer;
        public WinnerAnnouncement winnerAnnouncement;

        //MOVE TO THE GAME UI!!
        public TMP_Text timeText;
        public TMP_Text score;

        float timeRemaining = 180;
        float elapsedTime = 0;
        public int tick = 0;
        private InGameState inGameState;
        private static int gameplayStateBufferSize = 1024;

        private GamePlayState[] gameplayStateBuffer = new GamePlayState[gameplayStateBufferSize];//For the host
        int latestSentGamePlayStateTick = 0;
        int latestReceivedInputTick = 0;


        private GamePlayState latestGameplayState = new GamePlayState(); //Only used by client
        bool receivedLatestGameplayState = false;

        private LobbyUser m_LocalUser;
        private LocalLobby m_lobby;
        private Dictionary<ulong, string> m_clientIdToLobbyUserId = new Dictionary<ulong, string>();
        private Action m_onConnectionVerified, m_onGameEnd;
        private int m_expectedPlayerCount;

        private void Awake()
        {
            //Check if instance already exists
            if (instance == null)
                instance = this;
            else if (instance != this)
                Destroy(gameObject);

            //DontDestroyOnLoad(gameObject);
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
            foreach (KeyValuePair<string, LobbyUser> entry in m_lobby.LobbyUsers)
            {
                //print("Lobby User: Id: " + entry.Key + ", name: " + entry.Value.DisplayName + "");
                //Looks g9ood on server side
            }
            //Locator.Get.Provide(this); // Simplifies access since some networked objects can't easily communicate locally (e.g. the host might call a ClientRpc without that client knowing where the call originated).
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
                this.elapsedTime += Time.deltaTime;
                //Debug.Log("Time.deltaTime: " + Time.deltaTime + ", Time.fixedDeltaTime: " + Time.fixedDeltaTime);
                while (this.elapsedTime >= Time.fixedDeltaTime)
                {
                    this.elapsedTime -= Time.fixedDeltaTime;
                    //Send inputs for tick
                    //Debug.Log("[Replay] Main loop: RecordPlayerInputForTick: " + localPlayer);
                    localPlayer.RecordPlayerInputForTick(tick);


                    SetPlayerInputsForTick(tick);
                    AddForcesToPlayers();

                    Physics.Simulate(Time.fixedDeltaTime);
                    //snapshot this tick state


                    if (IsHost)
                    {
                        ResetPlayerInputsForTick(tick + (gameplayStateBufferSize/2));
                        var gameplayState = GetGamePlayStateWithTick(tick);
                        gameplayStateBuffer[tick % gameplayStateBufferSize] = gameplayState;
                        //if (tick % 10 == 0)
                        //{
                        //    //SetLatestGameplayState_ClientRpc(gameplayState);
                        //    //TODO This is probably not the best time to send the state. It might be better to store
                        //    //A 'last sent tick' and as inputs come in, physics is replayed from last sent tick to that tick and then sent from that state.
                        //    //The simulation would need to keep simulating to the locally current tick though
                        //}
                    } else
                    {
                        localPlayer.ResetInputsForTick(tick + (gameplayStateBufferSize / 2));
                    }
                    tick++;
                }

                if (!IsHost && receivedLatestGameplayState)
                {
                    var replayTick = this.latestGameplayState.tick;
                    SetGamePlayStateToState(this.latestGameplayState);
                    while (replayTick < tick)
                    {
                        //Apply inputs of this tick from locally stored states
                        localPlayer.SetInputsForTick(replayTick);
                        //We arent simulating
                        AddForcesToPlayers();
                        Physics.Simulate(Time.fixedDeltaTime);
                        replayTick++;
                    }
                    this.receivedLatestGameplayState = false;
                } else if (IsHost)
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
                            AddForcesToPlayers();
                            Physics.Simulate(Time.fixedDeltaTime);
                            //Now we have resimulated this state, we need to save it again.
                            gameplayStateBuffer[replayTick % gameplayStateBufferSize] = GetGamePlayStateWithTick(replayTick);

                            if (latestSyncedInputTick == replayTick)
                            {
                                Debug.Log("[SendState] tick: " + tick + ", replayTick: " + replayTick);
                                SetLatestGameplayState_ClientRpc(gameplayStateBuffer[replayTick % gameplayStateBufferSize]);
                                latestSentGamePlayStateTick = latestSyncedInputTick;
                            }

                            replayTick++;
                        }
                    }
                    
                }
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
            return currentLowestTick;
        }

        private GamePlayState GetGamePlayStateWithTick(int stateTick)
        {
            var gamePlayerStates = new GamePlayerState[NetworkManager.Singleton.ConnectedClientsIds.Count];
            int i = 0;
            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                var clientPlayerObject = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject;
                //clientPlayerObject.NetworkObjectId
                var clientNeworkPlayerObject = clientPlayerObject.GetComponent<NetworkPlayerComponent>();
                gamePlayerStates[i] = clientNeworkPlayerObject.getCurrentPlayerState(clientNeworkPlayerObject.NetworkObjectId);
                i++;

            }
            var ballState = ball.GetComponent<BallComponent>().getCurrentState();
            return new GamePlayState(stateTick, gamePlayerStates, ballState);
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
            //Locator.Get.Messenger.OnReceiveMessage(MessageType.ChangeGameState, GameState.JoinMenu);
        }

        IEnumerator ResetGameAsync()
        {
            yield return new WaitForSeconds(2f);
            ResetGame();
        }
        void ResetGame() {
            if (IsHost)
            {
                ResetGameObject(ball, new Vector3(0, 0, 0));
                foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
                {
                    var clientPlayerObject = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject;
                    var clientNeworkPlayerObject = clientPlayerObject.GetComponent<NetworkPlayerComponent>();
                    clientNeworkPlayerObject.ResetLocation();
                }
            }
            stadium.GetComponent<StadiumComponent>().Reset();
            StopCelebration();
            SetInGameState(InGameState.Playing);
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

        [ClientRpc]
        private void SetLatestGameplayState_ClientRpc(GamePlayState gameplayState)
        {
            if (IsHost)
            {
                //Debug.Log("OOps shoudlnt be here");
                return;
            }
            if (latestGameplayState.tick < gameplayState.tick)
            {
                this.latestGameplayState = gameplayState;
                this.receivedLatestGameplayState = true;
                //Take tick from latestGameplay, from that tick with that
                //gameplay state, replay physics which user inputs included in each step
                //(this only applies to clients)
                
            }
        }

        private void SetGamePlayStateToState(GamePlayState gamePlayState)
        {
            foreach (GamePlayerState gamePlayerState in gamePlayState.playerStates)
            {
                var playerObject = NetworkManager.Singleton.SpawnManager.SpawnedObjects[gamePlayerState.ID];
                //Debug.Log("[Client] gamePlayerState.ID: " + gamePlayerState.ID + ", playerObject: " + playerObject);
                playerObject.GetComponent<NetworkPlayerComponent>().SetPlayerState(gamePlayerState);
            }
            ball.GetComponent<BallComponent>().SetState(gamePlayState.ballState);
        }

        //Only handled by the server
        //Lets move this to the main update loop
        public void ReplayGameFromTick_Server(int replayTick)//Might want to kill this function
        {
            if (replayTick > tick)
            {
                Debug.Log("[Error] Shouldnt get here: replaytick: " + replayTick + "tick: " + tick);
                return;
            }
            //We need to reset state to state of this tick
            if (gameplayStateBuffer[replayTick % gameplayStateBufferSize] == null)
            {
                //Debug.Log("gameplayStateBuffer is null for tick: " + replayTick + ", current tick: " + tick);
                return;
            }

            
            //Debug.Log("[Replay] Replaying with input finished");
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
        void AddForcesToPlayers()
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
        }


        [ServerRpc]
        private void SetHomeTeamScore_ServerRpc(int score)
        {
            m_homeTeamScore.Value = score;
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
            //if (type == MessageType.)
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
            //Debug.Log("[GameState]: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            if (!m_clientIdToLobbyUserId.ContainsKey(clientId))
            {
                m_clientIdToLobbyUserId.Add(clientId, lobbyId);
            }
            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                //Debug.Log("[GameState]: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                VerifyConnectionConfirm_ServerRpc(clientId);
            }
        }

        /// <summary>
        /// Once the connection is confirmed, spawn a player cursor and check if all players have connected.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        private void VerifyConnectionConfirm_ServerRpc(ulong clientId)
        {
            //m_dataStore.AddPlayer(clientData.id, clientData.name);
            bool areAllPlayersConnected = NetworkManager.ConnectedClients.Count >= m_expectedPlayerCount; // The game will begin at this point, or else there's a timeout for booting any unconnected players.

            //Debug.Log("[GameState]: " + System.Reflection.MethodBase.GetCurrentMethod().Name + "areAllPlayersConnected: " + areAllPlayersConnected);
            VerifyConnectionConfirm_ClientRpc(clientId, areAllPlayersConnected);
        }

        [ClientRpc]
        private void VerifyConnectionConfirm_ClientRpc(ulong clientId, bool canBeginGame)
        {
            //Debug.Log("[GameState]: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                m_onConnectionVerified?.Invoke();

            }

            if (canBeginGame)
            {
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