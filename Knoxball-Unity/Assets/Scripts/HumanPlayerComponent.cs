﻿using System.Collections;
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

    float m_ForceStrength = 1.0f;
    float m_kickStrength = 3.0f;
    float m_DistanceForKick = 5.0f;
    
    [SerializeField, Tooltip("Move the player using keys.")]
    private KeyboardKeyMovement m_KeyboardKeyMovement = new KeyboardKeyMovement { enabled = true};

    private NetworkVariable<Vector3> m_position = new NetworkVariable<Vector3>(NetworkVariableReadPermission.Everyone, Vector3.zero); // (Using a NetworkTransform to sync position would also work.)


    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        print("Calling update on " + gameObject);
        if (IsOwner)
        {
            print("Calling IsOwner update on " + gameObject);
            UpdatePlayerInput();
            UpdatePlayerPosition();
        } else
        {
            transform.position = m_position.Value;
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
        print("Calling adding force on " + gameObject);

        var playerComponent = gameObject.GetComponent<PlayerComponent>();
        if (Input.GetKey(kick))
        {
            var directionVector = (ball.transform.position - gameObject.transform.position);
            float distanceSquared = directionVector.sqrMagnitude;
            if (distanceSquared < m_DistanceForKick)
            {
                ball.GetComponent<Rigidbody>().AddForce(directionVector.normalized * m_kickStrength);
            }
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
}
