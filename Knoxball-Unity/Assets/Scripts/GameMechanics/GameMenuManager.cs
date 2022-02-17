using UnityEngine;
using UnityEngine.SceneManagement;

namespace Knoxball
{
    public class GameMenuManager : MonoBehaviour
    {
        public GameObject menuOverlay;

        public void OnResumeClicked()
        {
            menuOverlay.SetActive(false);
        }

        public void OnBackToMainMenuClicked()
        {
            SceneManager.LoadScene("MenuScene");
        }
    }
}
