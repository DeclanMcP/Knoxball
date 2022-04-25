using UnityEngine;

namespace Knoxball
{
    public interface IClientSidePredictionPlayerController
    {
        NetworkGamePlayerState GetPlayerState();

        void SetPlayerState(NetworkGamePlayerState playerState);

        NetworkPlayerInputState GetPlayerInputState();

        public void ResetInputState();

        public void SetInputStateToState(NetworkPlayerInputState inputState);

        void UpdatePlayerInput();

        void AddForces();
    }
}
