
namespace Knoxball
{
    public interface IClientSidePredictionGameManipulator
    {
        void SetPlayerInputsForTick(int tick);

        void AddForcesToGame();

        void SendGamePlayState(NetworkGamePlayState gamePlayState);

        void SetGamePlayStateToState(NetworkGamePlayState gamePlayState);
    }
}
