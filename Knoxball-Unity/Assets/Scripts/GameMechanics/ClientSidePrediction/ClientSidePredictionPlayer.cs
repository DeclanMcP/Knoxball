using ClientSidePredictionMultiplayer;
using Unity.Netcode;

namespace Knoxball
{
    public class ClientSidePredictionPlayer: ClientSidePredictionGenericPlayer
    {
        private IClientSidePredictionPlayerController m_PlayerController;

        public void SetPlayerController(IClientSidePredictionPlayerController playerController)
        {
            this.m_PlayerController = playerController;
        }

        override public INetworkPlayerInputState GetPlayerInputState()
        {
            if (Game.Instance.inGameState != InGameState.Playing) { return null; }
            m_PlayerController.UpdatePlayerInput();//Should this be here..? probably not
            var playerInput = m_PlayerController.GetPlayerInputState();
            return playerInput;
        }

        override public void SetInputsForTick(int tick) //Required
        {
            //Not ideal, casting, but the best of many evils for now (cannot use Generics + NGO)
            var playerInputState = (NetworkPlayerInputState)GetPlayerInputStateForTick(tick);
            if (playerInputState == null)
            {
                m_PlayerController.ResetInputState();
                return;
            }
            m_PlayerController.SetInputStateToState(playerInputState);
        }


        override public void AddForces()
        {
            m_PlayerController.AddForces();
        }

        override public void SendAndStoreToServer(INetworkPlayerInputState inputState)
        {
            SendInput_ServerRpc((NetworkPlayerInputState)inputState);
        }


        public NetworkGamePlayerState GetCurrentPlayerState(ulong iD)//TODO problematic
        {
            var playerState = m_PlayerController.GetPlayerState();
            playerState.ID = iD;
            return playerState;
        }

        public void SetPlayerState(NetworkGamePlayerState playerState)//TODO problematic//Make this state generic?
        {
            m_PlayerController.SetPlayerState(playerState);
        }


        [ServerRpc] // Leave (RequireOwnership = true)
        private void SendInput_ServerRpc(NetworkPlayerInputState inputState)
        {
            //Debug.Log("[Input] Received input, tick: " + inputState.tick + ", inputstate: " + inputState.direction + "current tick: " + Game.instance.tick);
            StorePlayerInputState(inputState);
        }

    }
}