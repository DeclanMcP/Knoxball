using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameMenuManager : MonoBehaviour
{
    public GameObject menuOverlay;

    public void onResumeClicked() {
        menuOverlay.SetActive(false);
    }

    public void onBackToMainMenuClicked() {
        SceneManager.LoadScene("MenuScene");
    }
}
