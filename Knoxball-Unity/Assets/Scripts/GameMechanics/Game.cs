using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using LobbyRelaySample;

public delegate void KickCallBack(bool isPressed);

public class Game : MonoBehaviour, IReceiveMessages
{
    int homeTeamScore = 0;
    int awayTeamScore = 0;

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
    public NetworkPlayerComponent LocalPlayer;//TODO move all network logic here

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
    }

    IEnumerator ResetGame() {
        yield return new WaitForSeconds(2f);
        ResetGameObject(ball, new Vector3(0,0,0));
        LocalPlayer.ResetLocation();
        stadium.GetComponent<StadiumComponent>().Reset();
        StopCelebration();
    }

    void ResetGameObject(GameObject gameObject, Vector3 position) {
        gameObject.transform.position = position;
        gameObject.GetComponent<Rigidbody>().velocity = new Vector3(0,0,0);
    }

    public void HomeTeamScored() {
        homeTeamScore++;
        Celebrate();
        StartCoroutine(ResetGame());
    }

    public void AwayTeamScored() {
        awayTeamScore++;
        Celebrate();
        StartCoroutine(ResetGame());
    }

    public void Celebrate() {
        score.text = CurrentScore();
        print(CurrentScore());
        celebration.GetComponent<CelebrationComponent>().Celebrate();
        mainCamera.SetActive(false);
    }

    public void StopCelebration() {
        mainCamera.SetActive(true);
        celebration.GetComponent<CelebrationComponent>().StopCelebration();
    }

    string CurrentScore() {
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
