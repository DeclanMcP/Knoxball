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
        int tick = 0;
        private InGameState inGameState;
        private static int gameplayStateBufferSize = 1024;
        private GamePlayState[] gameplayStateBuffer = new GamePlayState[gameplayStateBufferSize];//For the host
        private GamePlayState latestGameplayState = new GamePlayState(); //Only used by client

        private LobbyUser m_LocalUser;
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


        public void Initialize(Action onConnectionVerified, int expectedPlayerCount, Action onGameEnd, LobbyUser localUser)
        {
            m_onConnectionVerified = onConnectionVerified;
            m_expectedPlayerCount = expectedPlayerCount;
            m_onGameEnd = onGameEnd;
            m_LocalUser = localUser;
            //Locator.Get.Provide(this); // Simplifies access since some networked objects can't easily communicate locally (e.g. the host might call a ClientRpc without that client knowing where the call originated).
        }


        public override void OnNetworkSpawn()
        {
            Debug.Log("[GameState]: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            VerifyConnection_ServerRpc(NetworkManager.Singleton.LocalClientId);
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
                Debug.Log("Time.deltaTime: " + Time.deltaTime + ", Time.fixedDeltaTime: " + Time.fixedDeltaTime);
                while (this.elapsedTime >= Time.fixedDeltaTime)
                {
                    this.elapsedTime -= Time.fixedDeltaTime;
                    Physics.Simulate(Time.fixedDeltaTime);
                    tick++;
                    //snapshot this tick state

                    if (IsHost)
                    {

                        var gameplayState = GetGamePlayStateForTick(tick);
                        gameplayStateBuffer[tick % gameplayStateBufferSize] = gameplayState;
                        SetLatestGameplayState_ClientRpc(gameplayState);
                    }

                    //Send inputs for tick
                    localPlayer.RecordPlayerInputForTick(tick);
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

        private GamePlayState GetGamePlayStateForTick(int stateTick)
        {
            var gamePlayerStates = new GamePlayerState[NetworkManager.Singleton.ConnectedClientsIds.Count];
            int i = 0;
            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                var clientPlayerObject = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject;
                var clientNeworkPlayerObject = clientPlayerObject.GetComponent<NetworkPlayerComponent>();
                gamePlayerStates[i] = clientNeworkPlayerObject.getCurrentPlayerState(clientId);
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

        IEnumerator ResetGame()
        {
            yield return new WaitForSeconds(2f);
            ResetGameObject(ball, new Vector3(0, 0, 0));
            localPlayer.ResetLocation();
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
            StartCoroutine(ResetGame());
        }

        [ClientRpc]
        private void SetLatestGameplayState_ClientRpc(GamePlayState gameplayState)
        {
            if (latestGameplayState.tick < gameplayState.tick)
            {
                this.latestGameplayState = gameplayState;
                //Take tick from latestGameplay, from that tick with that
                //gameplay state, replay physics which user inputs included in each step
                //(this only applies to clients)
                var replayTick = this.latestGameplayState.tick;
                SetGamePlayStateToState(gameplayState);
                while (replayTick <= tick)
                {
                    //Apply inputs of this tick from locally stored states
                    localPlayer.SetInputsForTick(replayTick);
                    Physics.Simulate(Time.fixedDeltaTime);
                    replayTick++;
                }
            }
        }

        private void SetGamePlayStateToState(GamePlayState gamePlayState)
        {
            foreach (GamePlayerState gamePlayerState in gamePlayState.playerStates)
            {
                var playerObject = NetworkManager.Singleton.ConnectedClients[gamePlayerState.ID].PlayerObject;
                playerObject.GetComponent<NetworkPlayerComponent>().SetPlayerState(gamePlayerState);
            }
            ball.GetComponent<BallComponent>().SetState(gamePlayState.ballState);
        }

        //Only handled by the server
        public void ReplayGameFromTick(int replayTick)
        {
            if (replayTick > tick)
            {
                Debug.Log("Shouldnt get here: replaytick: " + replayTick + "tick: " + tick);
            }
            //We need to reset state to state of this tick
            SetGamePlayStateToState(gameplayStateBuffer[replayTick]);

            while (replayTick <= tick)
            {
                //Apply inputs of this tick from locally stored states
                foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
                {
                    var clientPlayerObject = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject;
                    var clientNeworkPlayerObject = clientPlayerObject.GetComponent<NetworkPlayerComponent>();
                    clientNeworkPlayerObject.SetInputsForTick(replayTick);

                }
                Physics.Simulate(Time.fixedDeltaTime);
                //Now we have resimulated this state, we need to save it again.
                gameplayStateBuffer[replayTick % gameplayStateBufferSize] = GetGamePlayStateForTick(replayTick);

                replayTick++;
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
            StartCoroutine(ResetGame());
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
            print(CurrentScore());
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
            print("onMenuClicked!");
            menuOverlay.SetActive(true);
        }

        public void OnKickClicked(bool isPressed)
        {
            print("onKickClicked!" + isPressed);
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

        public void ReplayFromTick(int tick)
        {

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
        private void VerifyConnection_ServerRpc(ulong clientId)
        {
            Debug.Log("[GameState]: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            VerifyConnection_ClientRpc(clientId);
        }

        [ClientRpc]
        private void VerifyConnection_ClientRpc(ulong clientId)
        {
            Debug.Log("[GameState]: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                Debug.Log("[GameState]: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
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

            Debug.Log("[GameState]: " + System.Reflection.MethodBase.GetCurrentMethod().Name + "areAllPlayersConnected: " + areAllPlayersConnected);
            VerifyConnectionConfirm_ClientRpc(clientId, areAllPlayersConnected);
        }

        [ClientRpc]
        private void VerifyConnectionConfirm_ClientRpc(ulong clientId, bool canBeginGame)
        {
            Debug.Log("[GameState]: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
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
    }
}