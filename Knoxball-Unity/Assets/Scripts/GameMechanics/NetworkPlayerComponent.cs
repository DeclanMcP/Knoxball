using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using TMPro;
using UnityStandardAssets._2D;

namespace Knoxball
{
    public class NetworkPlayerComponent : NetworkBehaviour
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

        public class PlayerInputState: INetworkSerializable
        {
            public int tick;
            public Vector3 direction = Vector3.zero;
            public bool kicking;

            public PlayerInputState()
            {

            }

            public PlayerInputState(int tick, Vector3 direction, bool kicking)
            {
                this.tick = tick;
                this.direction = direction;
                this.kicking = kicking;
            }


            // INetworkSerializable
            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref tick);
                serializer.SerializeValue(ref direction);
                serializer.SerializeValue(ref kicking);
            }
            // ~INetworkSerializable
        }

        //private NetworkVariable<Vector3> m_position = new NetworkVariable<Vector3>(NetworkVariableReadPermission.Everyone, Vector3.zero); // (Using a NetworkTransform to sync position would also work.)
        //private NetworkVariable<Vector3> m_velocity = new NetworkVariable<Vector3>(NetworkVariableReadPermission.Everyone, Vector3.zero); // (Using a NetworkTransform to sync position would also work.)
        //private NetworkVariable<bool> m_kicking = new NetworkVariable<bool>(NetworkVariableReadPermission.Everyone, false);
        //private NetworkVariable<NetworkString> m_name = new NetworkVariable<NetworkString>(NetworkVariableReadPermission.Everyone, "");
        private static int playerInputBufferSize = 1024;
        private PlayerInputState[] m_playerInputBuffer = new PlayerInputState[playerInputBufferSize];

        private bool m_kickState = false;
        private Vector3 m_directionState;

        // Start is called before the first frame update

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                Game.instance.kickCallBack = new KickCallBack(OnKick);
                Game.instance.localPlayer = this;
                var followCamera = Game.instance.mainCamera.GetComponent<Camera2DFollow>();
                followCamera.target2 = transform;

                ResetLocation();

                if (IsDisplayNameAvailable())
                {
                    Debug.Log("DisplayName: " + Game.instance.LocalUser().DisplayName);
                    displayName.text = Game.instance.LocalUser().DisplayName;//m_name.Value;
                }
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
            var lobbyUser = Game.instance.GetLobbyUserForClientId(GetComponent<NetworkObject>().OwnerClientId);
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
            var previousKicking = playerComponent.IsKicking();
            
            playerComponent.OnKickStateChange(m_kickState);
            //if (previousKicking != playerComponent.IsKicking())
            //{
            //    UpdatePlayerKickState();
            //}
        }

        // Update is called once per frame
        void FixedUpdate()
        {
            if (IsOwner)
            {
                UpdatePlayerInput();

            }
            if (IsOwner || IsHost)
            {
                UpdatePlayerState();
            }
        }

        void UpdatePlayerInput()
        {
            m_directionState = GenerateKeypadForce() + GenerateJoystickForce();
            m_kickState = m_kickButtonState || Input.GetKey(kick);
        }

        void UpdatePlayerState()
        {
            gameObject.GetComponent<Rigidbody>().AddForce(m_directionState, ForceMode.VelocityChange);
            HandleKickEvent();
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
            Vector3 direction = Vector3.up * Game.instance.variableJoystick.Vertical + Vector3.right * Game.instance.variableJoystick.Horizontal;
            return direction * m_ForceStrength * Time.fixedDeltaTime;
        }

        public void RecordPlayerInputForTick(int tick)
        {
            if (!IsOwner) { return; }
            var playerInput = new PlayerInputState(tick, m_directionState, IsKicking());

            if (IsHost) {
                storePlayerInputState(playerInput);
                return;
            }
            Debug.Log("Sending input, tick: " + playerInput.tick + ", inputstate: " + playerInput);
            if (ShouldSendInput(playerInput))
            {
                SendInput_ServerRpc(playerInput);
            }
        }

        bool ShouldSendInput(PlayerInputState playerInput)
        {
            return playerInput.direction != Vector3.zero || playerInput.kicking;
        }

        private bool IsKicking()
        {
            return m_kickState;
        }

        [ServerRpc] // Leave (RequireOwnership = true)
        private void SendInput_ServerRpc(PlayerInputState inputState)
        {
            Debug.Log("[Input] Received input, tick: " + inputState.tick + ", inputstate: " + inputState.direction + "current tick: " + Game.instance.tick);
            storePlayerInputState(inputState);
            //Server player received a clients input, replay the physics from tick provided.
            Game.instance.ReplayGameFromTick(inputState.tick);
        }

        private void storePlayerInputState(PlayerInputState inputState)
        {
            m_playerInputBuffer[inputState.tick % playerInputBufferSize] = inputState;

            //Now the server receives a given player input, it can update the simulation for a given tick. Each player can store a
            //list of tick states so that when we rerun the simulation on the server side, it can run through these new values.
        }

        internal GamePlayerState getCurrentPlayerState(ulong iD)
        {
            return new GamePlayerState(iD, transform.position, GetComponent<Rigidbody>().velocity, transform.rotation, IsKicking());
        }

        public void SetPlayerState(GamePlayerState playerState)
        {
            //Debug.Log("SetPlayerState, id: " + playerState.ID + "networkId: " + this.NetworkObjectId);
            transform.position = playerState.position;
            GetComponent<Rigidbody>().velocity = playerState.velocity;
            transform.rotation = playerState.rotation;
            //missing kick
        }

        public void SetInputsForTick(int tick)
        {
            if (m_playerInputBuffer[tick % playerInputBufferSize] == null) {
                Debug.Log("No inputs found for this player..");
                m_kickState = false;
                m_directionState = Vector3.zero;
                return;
            }
            PlayerInputState playerInputState = m_playerInputBuffer[tick % playerInputBufferSize];
            m_kickState = playerInputState.kicking;
            m_directionState = playerInputState.direction;
        }

        void UpdatePlayerKickState()
        {
            var playerComponent = gameObject.GetComponent<PlayerComponent>();
            SetKicking_ServerRpc(playerComponent.IsKicking());
        }

        [ServerRpc]
        private void SetKicking_ServerRpc(bool kicking)
        {
            Debug.Log("[TestRpc] SetKicking_ServerRpc");
            //m_kicking.Value = kicking;
        }

        private bool IsDisplayNameAvailable()
        {
            return !(Game.instance == null || Game.instance.LocalUser() == null);
        }

        public void ResetLocation()
        {
            Debug.Log("setting location!");
            var lobbyUser = Game.instance.GetLobbyUserForClientId(GetComponent<NetworkObject>().OwnerClientId);
            if (lobbyUser == null)
            {
                Debug.Log("Could not find lobby user: " + lobbyUser);
                return;
            }
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
                //gameObject.gameObject.collider.enabled = false;
                gameObject.GetComponent<Collider>().enabled = false;
            }
        }
    }

    public struct NetworkString : INetworkSerializable
    {
        private FixedString32Bytes info;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref info);
        }

        public override string ToString()
        {
            return info.ToString();
        }

        public static implicit operator string(NetworkString s) => s.ToString();
        public static implicit operator NetworkString(string s) => new NetworkString() { info = new FixedString32Bytes(s) };
    }
}