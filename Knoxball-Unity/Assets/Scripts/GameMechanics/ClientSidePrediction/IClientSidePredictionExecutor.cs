using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Knoxball
{
    public interface IClientSidePredictionExecutor
    {

        void SetGameManipulator(IClientSidePredictionGameManipulator manipulator);

        void ResetPlayerInputsForTick(int tick);

        void SaveCurrentGamePlayStateWithTick(int tick);

        void ReplayPhysics(int currentTick);

        void ResetGameBuffers();

        void ReceivedGamePlayState(NetworkGamePlayState gamePlayState);
    }
}