using TMPro;
using UnityEngine;
namespace Knoxball
{
    public class WinnerAnnouncement : MonoBehaviour
    {
        public TMP_Text winnerText;

        // Start is called before the first frame update
        void Start()
        {
            gameObject.SetActive(false);
        }

        // Update is called once per frame
        void Update()
        {

        }

        public void AnnounceWinner(GameTeam gameTeam)
        {
            gameObject.SetActive(true);
            winnerText.text = CelebrationText(gameTeam);
        }

        string CelebrationText(GameTeam gameTeam)
        {
            if (gameTeam == GameTeam.Home)
            {
                return "Home Team Wins";
            }
            else if (gameTeam == GameTeam.Away)
            {
                return "Away Team Wins";
            }
            return "Draw";
        }
    }
}
