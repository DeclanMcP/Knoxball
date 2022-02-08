using System.Collections.Generic;
using UnityEngine;

namespace LobbyRelaySample.UI
{
    /// <summary>
    /// Contains the InLobbyUserUI instances while showing the UI for a lobby.
    /// </summary>
    [RequireComponent(typeof(LocalLobbyObserver))]
    public class InLobbyUserList : ObserverPanel<LocalLobby>
    {
        [SerializeField]
        List<InLobbyUserUI> m_HomeUserUIObjects = new List<InLobbyUserUI>();

        [SerializeField]
        List<InLobbyUserUI> m_SpectatorUserUIObjects = new List<InLobbyUserUI>();

        [SerializeField]
        List<InLobbyUserUI> m_AwayUserUIObjects = new List<InLobbyUserUI>();

        /// <summary>
        /// When the observed data updates, we need to detect changes to the list of players.
        /// </summary>
        public override void ObservedUpdated(LocalLobby observed)
        {
            ResetTeam(m_HomeUserUIObjects);
            ResetTeam(m_SpectatorUserUIObjects);
            ResetTeam(m_AwayUserUIObjects);

            foreach (var lobbyUserKvp in observed.LobbyUsers)
            {
                UserJoinedTeam(UITeamFromUserTeam(lobbyUserKvp.Value.UserTeam), lobbyUserKvp.Value);
            }
        }

        void OnUserLeft(string userID)
        {
            //if (!m_CurrentUsers.Contains(userID))
            //    return;
            //m_CurrentUsers.Remove(userID);
        }


            List<InLobbyUserUI> UITeamFromUserTeam(UserTeam team)
        {
            switch(team)
            {
                case UserTeam.Home:
                    return m_HomeUserUIObjects;
                case UserTeam.Spectator:
                    return m_SpectatorUserUIObjects;
                case UserTeam.Away:
                    return m_AwayUserUIObjects;
            }
            return m_HomeUserUIObjects;
        }

        void UserJoinedTeam(List<InLobbyUserUI> uiTeamList, LobbyUser user)
        {
            foreach (var ui in uiTeamList)
            {
                if (ui.IsAssigned)
                    continue;
                ui.SetUser(user);
                break;
            }
        }

        void ResetTeam(List<InLobbyUserUI> uiTeamList)
        {
            foreach (var ui in uiTeamList)
            {
                if (!ui.IsAssigned)
                    continue;
                ui.OnUserLeft();
            }
        }


        public void OnHomeClicked()
        {
            OnTeamClicked(UserTeam.Home);
        }

        public void OnSpectatorClicked()
        {
            OnTeamClicked(UserTeam.Spectator);
        }

        public void OnAwayClicked()
        {
            OnTeamClicked(UserTeam.Away);
        }

        void OnTeamClicked(UserTeam team)
        {
            Locator.Get.Messenger.OnReceiveMessage(MessageType.UserSetTeam, team);
        }
    }
}
