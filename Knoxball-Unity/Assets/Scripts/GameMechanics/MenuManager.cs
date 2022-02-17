using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Knoxball
{
    public class MenuManager : MonoBehaviour
    {
        // Start is called before the first frame update
        void Start()
        {

        }

        public void onPracticeClicked()
        {
            print("Practice Clicked");
            SceneManager.LoadScene("GameScene");
        }

        public void onMultiplayerClicked()
        {
            print("Multiplayer Clicked");
            SceneManager.LoadScene("MultiplayerLobbyScene");
        }
    }
}