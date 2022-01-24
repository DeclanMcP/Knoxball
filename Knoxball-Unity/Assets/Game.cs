using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Game : MonoBehaviour
{
    int homeTeamScore = 0;
    int awayTeamScore = 0;

    public GameObject ball;
    public GameObject player;
    public GameObject stadium;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    IEnumerator resetGame() {
        yield return new WaitForSeconds(2f);
        resetGameObject(ball, new Vector3(0,0,0.5f));
        resetGameObject(player, new Vector3(-2,0,0.5f));
        stadium.GetComponent<StadiumComponent>().Reset();
    }

    void resetGameObject(GameObject gameObject, Vector3 position) {
        gameObject.transform.position = position;
        gameObject.GetComponent<Rigidbody>().velocity = new Vector3(0,0,0);
    }

    public void HomeTeamScored() {
        homeTeamScore++;
        print(currentScore());
        StartCoroutine(resetGame());
    }

    public void AwayTeamScored() {
        awayTeamScore++;
        print(currentScore());
        StartCoroutine(resetGame());
    }

    string currentScore() {
        return "Home: " + homeTeamScore + ":" + awayTeamScore + " :Away";
    }
}
