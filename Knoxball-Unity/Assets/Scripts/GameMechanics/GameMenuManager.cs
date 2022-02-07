using UnityEngine;
using UnityEngine.SceneManagement;

public class GameMenuManager : MonoBehaviour
{
    public GameObject menuOverlay;

    public void OnResumeClicked() {
        menuOverlay.SetActive(false);
    }

    public void OnBackToMainMenuClicked() {
        SceneManager.LoadScene("MenuScene");
    }
}
