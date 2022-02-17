using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public delegate void KickCallBack(bool isPressed);

namespace Knoxball
{
    public class Game : NetworkBehaviour, IReceiveMessages
    {
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
        public Text score;

        private LobbyUser m_LocalUser;
        public NetworkPlayerComponent LocalPlayer;
        public static Game instance;

        private void Awake()
        {
            //Check if instance already exists
            if (instance == null)
                instance = this;
            else if (instance != this)
                Destroy(gameObject);

            DontDestroyOnLoad(gameObject);
        }

        // Start is called before the first frame update
        void Start()
        {
            Locator.Get.Messenger.Subscribe(this);
            kickButton.customEvent = OnKickClicked;
            score.text = CurrentScore();
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
                SetHomeTeamScore_ServerRpc(m_homeTeamScore.Value+1);
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
            return "Home: " + homeTeamScore + ":" + awayTeamScore + " :Away";
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

        public void OnLocalUserChanged(LobbyUser localUser)
        {
            m_LocalUser = localUser;
        }
    }
}