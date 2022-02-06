using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class BallComponent : NetworkBehaviour
{
    private NetworkVariable<Vector3> m_position = new NetworkVariable<Vector3>(NetworkVariableReadPermission.Everyone, Vector3.zero);
    private NetworkVariable<Vector3> m_velocity = new NetworkVariable<Vector3>(NetworkVariableReadPermission.Everyone, Vector3.zero);

    float maxSpeed = 10.0f;
    float m_DistanceForKick = 5.0f;
    float m_kickStrength = 80.0f;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (IsHost)
        {
            UpdatePosition();
            UpdateVelocity();
        }
        else
        {
            transform.position = m_position.Value;
            GetComponent<Rigidbody>().velocity = m_velocity.Value;
        }

        //print(gameObject.GetComponent<Rigidbody>().velocity);
        
    }

    void UpdatePosition()
    {
        Vector3 targetPosition = transform.position;
        SetPosition_ServerRpc(targetPosition); // Client can't set a network variable value.

    }

    [ServerRpc] // Leave (RequireOwnership = true) for these so that only the player whose cursor this is can make updates.
    private void SetPosition_ServerRpc(Vector3 position)
    {
        m_position.Value = position;
    }

    void UpdateVelocity()
    {
        if (GetComponent<Rigidbody>().velocity.magnitude > maxSpeed)
        {
            GetComponent<Rigidbody>().velocity = Vector3.ClampMagnitude(GetComponent<Rigidbody>().velocity, maxSpeed); ;
        }
        Vector3 targetVelocity = GetComponent<Rigidbody>().velocity;
        SetVelocity_ServerRpc(targetVelocity);
    }

    [ServerRpc] // Leave (RequireOwnership = true) for these so that only the player whose cursor this is can make updates.
    private void SetVelocity_ServerRpc(Vector3 velocity)
    {
        m_velocity.Value = velocity;
    }

    public void Kick(Vector3 origin)
    {
        if (IsHost)
        {
            var directionVector = (gameObject.transform.position - origin);
            float distanceSquared = directionVector.sqrMagnitude;
            if (distanceSquared < m_DistanceForKick)
            {
                gameObject.GetComponent<Rigidbody>().AddForce(directionVector.normalized * m_kickStrength);
                UpdateVelocity();
            }
        }
    }
}
