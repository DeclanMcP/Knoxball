using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using TMPro;
using System;

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

    public class Game : NetworkBehaviour, IReceiveMessages
    {
        public static Game instance;

        int homeTeamScore = 0;
        int awayTeamScore = 0;
        private NetworkVariable<int> m_homeTeamScore = new NetworkVariable<int>(NetworkVariableReadPermission.Everyone, 0);
        private NetworkVariable<int> m_awayTeamScore = new NetworkVariable<int>(NetworkVariableReadPermission.Everyone, 0);

        public VariableJoystick variableJoystick;
        public KickButton kickButton;
        public KickCallBack kickCallBack;

        public GameObject ball;
        public GameObject stadium;
        public GameObject celebration;
        public GameObject mainCamera;
        public GameObject menuOverlay;
        public NetworkPlayerComponent LocalPlayer;
        public WinnerAnnouncement winnerAnnouncement;

        //MOVE TO THE GAME UI!!
        public TMP_Text timeText;
        public TMP_Text score;

        float timeRemaining = 120;
        private LobbyUser m_LocalUser;
        private Action m_onConnectionVerified, m_onGameEnd;
        private int m_expectedPlayerCount;
        private InGameState m_InGameState;

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
            if (homeTeamScore != m_homeTeamScore.Value)
            {
                homeTeamScore = m_homeTeamScore.Value;
                score.text = CurrentScore();
            }
            if (awayTeamScore != m_awayTeamScore.Value)
            {
                awayTeamScore = m_awayTeamScore.Value;
                score.text = CurrentScore();
            }

            if (m_InGameState == InGameState.Playing)
            {
                timeRemaining -= Time.deltaTime;
                timeRemaining = Mathf.Max(timeRemaining, 0);
            }
            if (timeRemaining <= 0 && IsHost)
            {
                m_InGameState = InGameState.Ending;
                AnounceWinner();
                EndGame();
                m_onGameEnd();
            }
            DisplayTime(timeRemaining);
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
            m_InGameState = InGameState.Finished;
            Locator.Get.Messenger.OnReceiveMessage(MessageType.ChangeGameState, GameState.JoinMenu);
        }

        IEnumerator ResetGame()
        {
            yield return new WaitForSeconds(2f);
            ResetGameObject(ball, new Vector3(0, 0, 0));
            LocalPlayer.ResetLocation();
            stadium.GetComponent<StadiumComponent>().Reset();
            StopCelebration();
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
                VerifyConnectionConfirm_ServerRpc(clientId);
                Debug.Log("[GameState]: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
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

            VerifyConnectionConfirm_ClientRpc(clientId, areAllPlayersConnected);
            Debug.Log("[GameState]: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
        }

        [ClientRpc]
        private void VerifyConnectionConfirm_ClientRpc(ulong clientId, bool canBeginGame)
        {
            Debug.Log("[GameState]: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                m_onConnectionVerified?.Invoke();

                if (canBeginGame)
                {
                    BeginGame();
                }
            }
        }

        private void BeginGame()
        {
            Debug.Log("[GameState]: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            m_InGameState = InGameState.Playing;
        }
    }
}