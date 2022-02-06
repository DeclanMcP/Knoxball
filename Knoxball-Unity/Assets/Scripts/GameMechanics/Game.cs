using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;


public delegate void KickCallBack(bool isPressed);

public class Game : MonoBehaviour
{
    int homeTeamScore = 0;
    int awayTeamScore = 0;

    public VariableJoystick variableJoystick;
    public KickButton kickButton;
    public KickCallBack kickCallBack;

    public GameObject ball;
    //public GameObject player;
    public GameObject stadium;
    public GameObject celebration;
    public GameObject mainCamera;
    public GameObject menuOverlay;
    public Text score;

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
        kickButton.customEvent = onKickClicked;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    IEnumerator resetGame() {
        yield return new WaitForSeconds(2f);
        resetGameObject(ball, new Vector3(0,0,0.5f));
        //resetGameObject(player, new Vector3(-2,0,0.5f));
        stadium.GetComponent<StadiumComponent>().Reset();
        StopCelebration();
    }

    void resetGameObject(GameObject gameObject, Vector3 position) {
        gameObject.transform.position = position;
        gameObject.GetComponent<Rigidbody>().velocity = new Vector3(0,0,0);
    }

    public void HomeTeamScored() {
        homeTeamScore++;
        Celebrate();
        StartCoroutine(resetGame());
    }

    public void AwayTeamScored() {
        awayTeamScore++;
        Celebrate();
        StartCoroutine(resetGame());
    }

    public void Celebrate() {
        score.text = currentScore();
        print(currentScore());
        celebration.GetComponent<CelebrationComponent>().Celebrate();
        mainCamera.SetActive(false);
    }

    public void StopCelebration() {
        mainCamera.SetActive(true);
        celebration.GetComponent<CelebrationComponent>().StopCelebration();
    }

    string currentScore() {
        return "Home: " + homeTeamScore + ":" + awayTeamScore + " :Away";
    }

    public void onMenuClicked()
    {
        print("onMenuClicked!");
        menuOverlay.SetActive(true);
    }

    public void onKickClicked(bool isPressed)
    {
        print("onKickClicked!" + isPressed);
        kickCallBack(isPressed);
    }
}
