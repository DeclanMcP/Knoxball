using Unity.Netcode;
using UnityEngine;
using TMPro;
using UnityStandardAssets._2D;
using ClientSidePredictionMultiplayer;

namespace Knoxball
{
    public class NetworkPlayerComponent : ClientSidePredictionGenericPlayer
    {
        //if the keyboard button panning is enabling, player will be able to use keyboard keys to move the camera
        [System.Serializable]
        public struct KeyboardKeyMovement
        {
            public bool enabled;
            public KeyCode up;
            public KeyCode down;
            public KeyCode right;
            public KeyCode left;
        }

        public KeyCode kick = KeyCode.Space;

        float m_ForceStrength = 10.0f;
        float maxSpeed = 3.0f;
        private bool m_kickButtonState = false;
        public TMP_Text displayName;

        [SerializeField, Tooltip("Move the player using keys.")]
        private KeyboardKeyMovement m_KeyboardKeyMovement = new KeyboardKeyMovement
        {
            enabled = true,
            up = KeyCode.UpArrow,
            down = KeyCode.DownArrow,
            right = KeyCode.RightArrow,
            left = KeyCode.LeftArrow
        };


        private bool m_kickState = false;
        private Vector3 m_directionState;

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {

                //TODO fix codesmell, let Game set it to whatever it wants?
                Game.Instance.kickCallBack = new KickCallBack(OnKick);
                //Game.Instance.GetComponent<ClientSidePredictionManager>().localPlayer = this;

                var followCamera = Game.Instance.mainCamera.GetComponent<Camera2DFollow>();
                followCamera.target2 = transform;

                ResetLocation();
                Debug.Log("DisplayName: " + Game.Instance.LocalUser().DisplayName);
                displayName.text = Game.Instance.LocalUser().DisplayName;
            }
            else
            {
                //Could be used for all names
                displayName.text = GetUsername();
            }

            var playerComponent = gameObject.GetComponent<PlayerComponent>();
            playerComponent.SetLocalPlayer(IsOwner);
        }

        string GetUsername()
        {
            var lobbyUser = Game.Instance.GetLobbyUserForClientId(GetComponent<NetworkObject>().OwnerClientId);
            if (lobbyUser == null)
            {
                Debug.Log("Could not find lobby user: " + lobbyUser);
                return "";
            }
            return lobbyUser.DisplayName;
        }

        void OnKick(bool isPressed)
        {
            m_kickButtonState = isPressed;
            HandleKickEvent();
        }

        void HandleKickEvent()
        {
            var playerComponent = gameObject.GetComponent<PlayerComponent>();
            playerComponent.OnKickStateChange(m_kickState);
        }

        // Update is called once per frame
        public void ManualUpdate()
        {
            //Debug.Log("Manual Update");
            AddForces();
        }

        override public void UpdatePlayerInput()
        {
            m_directionState = GenerateKeypadForce() + GenerateJoystickForce();
            m_kickState = m_kickButtonState || Input.GetKey(kick);
        }

        Vector3 GenerateKeypadForce()
        {
            var force = new Vector3();
            if (Input.GetKey(m_KeyboardKeyMovement.up))
                force.y = m_ForceStrength;
            if (Input.GetKey(m_KeyboardKeyMovement.down))
                force.y = -m_ForceStrength;
            if (Input.GetKey(m_KeyboardKeyMovement.right))
                force.x = m_ForceStrength;
            if (Input.GetKey(m_KeyboardKeyMovement.left))
                force.x = -m_ForceStrength;
            return force * Time.fixedDeltaTime;
        }

        Vector3 GenerateJoystickForce()
        {
            Vector3 direction = Vector3.up * Game.Instance.variableJoystick.Vertical + Vector3.right * Game.Instance.variableJoystick.Horizontal;
            return direction * m_ForceStrength * Time.fixedDeltaTime;
        }

        public void ResetInputState()
        {
            //Debug.Log("No inputs found for this player..");
            m_kickState = false;
            m_directionState = Vector3.zero;
        }

        public void SetInputStateToState(NetworkPlayerInputState inputState)
        {
            //Debug.Log("SetInputsForTick Tick: " + tick + ", input: " + playerInputState.direction);
            m_kickState = inputState.kicking;
            m_directionState = inputState.direction;
        }

        public void SetPlayerState(NetworkGamePlayerState playerState)
        {
            //Debug.Log("SetPlayerState, id: " + playerState.ID + "networkId: " + this.NetworkObjectId);
            transform.position = playerState.position;
            GetComponent<Rigidbody>().velocity = playerState.velocity;
            transform.rotation = playerState.rotation;
            m_kickState = playerState.kicking;
        }

        public void ResetLocation()
        {
            //Debug.Log("setting location!");
            GetComponent<Rigidbody>().velocity = Vector3.zero;
            var lobbyUser = Game.Instance.GetLobbyUserForClientId(GetComponent<NetworkObject>().OwnerClientId);
            if (lobbyUser == null)
            {
                Debug.Log("Could not find lobby user: " + lobbyUser);
                return;
            }
            displayName.text = GetUsername();
            if (lobbyUser.UserTeam == UserTeam.Home)
            {
                transform.position = new Vector3(-5, 0, 0);
            }
            else if (lobbyUser.UserTeam == UserTeam.Away)
            {
                transform.position = new Vector3(5, 0, 0);
            }
            else //Spectator
            {
                gameObject.GetComponent<Collider>().enabled = false;
            }
        }

        public NetworkGamePlayerState GetPlayerState()
        {
            return new NetworkGamePlayerState(0, transform.position, GetComponent<Rigidbody>().velocity, transform.rotation, m_kickState);
        }

        override public INetworkPlayerInputState GetPlayerInputState()
        {
            if (Game.Instance.inGameState != InGameState.Playing) { return null; }
            //UpdatePlayerInput();//TODO Should this be here..? probably not
            var playerInput = new NetworkPlayerInputState(0, m_directionState, m_kickState);
            return playerInput;
        }

        override public void SetInputsForTick(int tick) //Required
        {
            //Not ideal, casting, but the best of many evils for now (cannot use Generics + NGO)
            var playerInputState = (NetworkPlayerInputState)GetPlayerInputStateForTick(tick);
            if (playerInputState == null)
            {
                ResetInputState();
                return;
            }
            SetInputStateToState(playerInputState);
        }


        override public void AddForces()
        {
            gameObject.GetComponent<Rigidbody>().AddForce(m_directionState, ForceMode.VelocityChange);

            if (GetComponent<Rigidbody>().velocity.magnitude > maxSpeed)
            {
                GetComponent<Rigidbody>().velocity = Vector3.ClampMagnitude(GetComponent<Rigidbody>().velocity, maxSpeed); ;
            }
            HandleKickEvent();
        }

        override public void SendAndStoreToServer(INetworkPlayerInputState inputState)
        {
            SendInput_ServerRpc((NetworkPlayerInputState)inputState);
        }

        public NetworkGamePlayerState GetCurrentPlayerState(ulong iD)
        {
            var playerState = GetPlayerState();
            playerState.ID = iD;
            return playerState;
        }

        [ServerRpc] // Leave (RequireOwnership = true)
        private void SendInput_ServerRpc(NetworkPlayerInputState inputState)
        {
            //Debug.Log("[Input] Received input, tick: " + inputState.tick + ", inputstate: " + inputState.direction + "current tick: " + Game.instance.tick);
            StorePlayerInputState(inputState);
        }
    }

}