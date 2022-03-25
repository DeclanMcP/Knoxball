﻿using Unity.Collections;
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

        internal class PlayerInputState
        {
            internal readonly int tick;
            Vector3 direction;
            bool kicking;


            internal PlayerInputState(int tick, Vector3 direction, bool kicking)
            {
                this.tick = tick;
                this.direction = direction;
                this.kicking = kicking;
            }
        }

        //private NetworkVariable<Vector3> m_position = new NetworkVariable<Vector3>(NetworkVariableReadPermission.Everyone, Vector3.zero); // (Using a NetworkTransform to sync position would also work.)
        //private NetworkVariable<Vector3> m_velocity = new NetworkVariable<Vector3>(NetworkVariableReadPermission.Everyone, Vector3.zero); // (Using a NetworkTransform to sync position would also work.)
        private NetworkVariable<bool> m_kicking = new NetworkVariable<bool>(NetworkVariableReadPermission.Everyone, false);
        private NetworkVariable<NetworkString> m_name = new NetworkVariable<NetworkString>(NetworkVariableReadPermission.Everyone, "");
        private static int playerInputBufferSize = 1024;
        private PlayerInputState[] m_playerInputBuffer = new PlayerInputState[playerInputBufferSize];

        // Start is called before the first frame update

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
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
                displayName.text = m_name.Value;
            }

            var playerComponent = gameObject.GetComponent<PlayerComponent>();
            playerComponent.SetLocalPlayer(IsOwner);
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
            playerComponent.OnKickStateChange(m_kickButtonState || Input.GetKey(kick));
            if (previousKicking != playerComponent.IsKicking())
            {
                UpdatePlayerKickState();
            }
        }

        // Update is called once per frame
        void FixedUpdate()
        {
            if (IsOwner)
            {
                UpdatePlayerInput();
                UpdatePlayerPosition();
                UpdatePlayerVelocity();
                UpdatePlayerName();
            }
            else
            {
                //transform.position = m_position.Value;
                var playerComponent = gameObject.GetComponent<PlayerComponent>();
                playerComponent.OnKickStateChange(m_kicking.Value);
                //GetComponent<Rigidbody>().velocity = m_velocity.Value;
                displayName.text = m_name.Value;
            }
        }

        void UpdatePlayerInput()
        {
            var force = GenerateKeypadForce() + GenerateJoystickForce();
            gameObject.GetComponent<Rigidbody>().AddForce(force, ForceMode.VelocityChange);
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
            var playerInput = new PlayerInputState(tick, GenerateKeypadForce() + GenerateJoystickForce(), IsKicking());

            if (IsHost) {
                storePlayerInputState(playerInput);
                return;
            }
            SendInput_ServerRpc(playerInput);
        }

        private bool IsKicking()
        {
            return m_kickButtonState || Input.GetKey(kick);
        }

        [ServerRpc] // Leave (RequireOwnership = true) for these so that only the player whose cursor this is can make updates.
        private void SendInput_ServerRpc(PlayerInputState inputState)
        {
            storePlayerInputState(inputState);
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

        void UpdatePlayerPosition()
        {
            //Vector3 targetPosition = transform.position;
            //SetPosition_ServerRpc(targetPosition); // Client can't set a network variable value.

        }

        //[ServerRpc] // Leave (RequireOwnership = true) for these so that only the player whose cursor this is can make updates.
        //private void SetPosition_ServerRpc(Vector3 position)
        //{
        //    m_position.Value = position;
        //}

        void UpdatePlayerVelocity()
        {
            //Vector3 targetVelocity = GetComponent<Rigidbody>().velocity;
            //SetVelocity_ServerRpc(targetVelocity);
        }

        //[ServerRpc]
        //private void SetVelocity_ServerRpc(Vector3 velocity)
        //{
        //    m_velocity.Value = velocity;
        //}

        void UpdatePlayerKickState()
        {
            var playerComponent = gameObject.GetComponent<PlayerComponent>();
            SetKicking_ServerRpc(playerComponent.IsKicking());
        }

        [ServerRpc]
        private void SetKicking_ServerRpc(bool kicking)
        {
            m_kicking.Value = kicking;
        }

        void UpdatePlayerName()
        {
            if (IsDisplayNameAvailable())
            {
                SetName_ServerRpc(Game.instance.LocalUser().DisplayName);
            }
        }

        [ServerRpc]
        private void SetName_ServerRpc(string name)
        {
            m_name.Value = name;
        }

        private bool IsDisplayNameAvailable()
        {
            return !(Game.instance == null || Game.instance.LocalUser() == null);
        }

        public void ResetLocation()
        {
            Debug.Log("setting kickCallback!");
            Game.instance.kickCallBack = new KickCallBack(OnKick);
            if (Game.instance.LocalUser().UserTeam == UserTeam.Home)
            {
                transform.position = new Vector3(-5, 0, 0);
            }
            else if (Game.instance.LocalUser().UserTeam == UserTeam.Away)
            {
                transform.position = new Vector3(5, 0, 0);
            }
            else //Spectator
            {
                //gameObject.gameObject.collider.enabled = false;
                gameObject.GetComponent<Collider>().enabled = false;
            }
        }

        //private void OnCollisionEnter(Collision collision)
        //{

        //    //Debug.Log("OnCollisionEnter!");
        //    //if (collision.gameObject.GetComponent<PlayerComponent>() == null && collision.gameObject.GetComponent<BallComponent>() == null)
        //    //{
        //    //    return;
        //    //}
        //    //collision.rigidbody.AddForce(collision.relativeVelocity);
        //}
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