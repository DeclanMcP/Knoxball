using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class HumanPlayerComponent : NetworkBehaviour
{

    public GameObject ball;
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

    public KeyCode kick;

    float m_ForceStrength = 10.0f;
    
    [SerializeField, Tooltip("Move the player using keys.")]
    private KeyboardKeyMovement m_KeyboardKeyMovement = new KeyboardKeyMovement { enabled = true};

    private NetworkVariable<Vector3> m_position = new NetworkVariable<Vector3>(NetworkVariableReadPermission.Everyone, Vector3.zero); // (Using a NetworkTransform to sync position would also work.)
    private NetworkVariable<bool> m_kicking = new NetworkVariable<bool>(NetworkVariableReadPermission.Everyone, false);
    private bool kicking = false;

    // Start is called before the first frame update
    void Start()
    {
        //variableJoystick = (VariableJoystick)GameObject.Find("YourPanelName");
        if (IsOwner)
        {
            print("setting kickCallback!");
            Game.instance.kickCallBack = new KickCallBack(onKick);
        }
    }

    void onKick(bool isPressed)
    {
        print("KICK!");
        var playerComponent = gameObject.GetComponent<PlayerComponent>();
        if (isPressed)
        {
            Game.instance.ball.GetComponent<BallComponent>().Kick(gameObject.transform.position);
            playerComponent.LightUp();
        }
        else
        {
            playerComponent.LightDown();
        }
        this.kicking = isPressed;
        UpdatePlayerKickState();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        print("Calling update on " + gameObject);
        if (IsOwner)
        {
            print("Calling IsOwner update on " + gameObject);
            UpdatePlayerInput();
            UpdatePlayerPosition();
        }
        else
        {
            transform.position = m_position.Value;
            if (!kicking && m_kicking.Value)
            {
                //Call kick on ball to check if an update is required
                Game.instance.ball.GetComponent<BallComponent>().Kick(gameObject.transform.position);
            }
            kicking = m_kicking.Value;
        }
    }

    void UpdatePlayerInput()
    {
        var force = new Vector3();
        if (Input.GetKey(m_KeyboardKeyMovement.up))
            force.y = m_ForceStrength;
        if (Input.GetKey(m_KeyboardKeyMovement.down))
            force.y = - m_ForceStrength;
        if (Input.GetKey(m_KeyboardKeyMovement.right))
            force.x = m_ForceStrength;
        if (Input.GetKey(m_KeyboardKeyMovement.left))
            force.x = - m_ForceStrength;

        gameObject.GetComponent<Rigidbody>().AddForce(force);

        Vector3 direction = Vector3.up * Game.instance.variableJoystick.Vertical + Vector3.right * Game.instance.variableJoystick.Horizontal;
        gameObject.GetComponent<Rigidbody>().AddForce(direction * m_ForceStrength * Time.fixedDeltaTime, ForceMode.VelocityChange);

        print("Calling adding force " + direction.x + "," + direction.y);

        var playerComponent = gameObject.GetComponent<PlayerComponent>();
        if (Input.GetKey(kick))
        {
            Game.instance.ball.GetComponent<BallComponent>().Kick(gameObject.transform.position);
            playerComponent.LightUp();
        } else {
            playerComponent.LightDown();
        }
    }

    void UpdatePlayerPosition()
    {
        Vector3 targetPosition = transform.position;
        SetPosition_ServerRpc(targetPosition); // Client can't set a network variable value.

    }

    [ServerRpc] // Leave (RequireOwnership = true) for these so that only the player whose cursor this is can make updates.
    private void SetPosition_ServerRpc(Vector3 position)
    {
        m_position.Value = position;
    }

    void UpdatePlayerKickState()
    {
        SetKicking_ServerRpc(this.kicking);
    }

    [ServerRpc]
    private void SetKicking_ServerRpc(bool kicking)
    {
        m_kicking.Value = kicking;
    }
}
