using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class BallComponent : NetworkBehaviour
{
    private NetworkVariable<Vector3> m_position = new NetworkVariable<Vector3>(NetworkVariableReadPermission.Everyone, Vector3.zero); // (Using a NetworkTransform to sync position would also work.)

    float maxSpeed = 10.0f;
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
        }
        else
        {
            transform.position = m_position.Value;
        }

        //print(gameObject.GetComponent<Rigidbody>().velocity);
        if(GetComponent<Rigidbody>().velocity.magnitude > maxSpeed)
        {
            GetComponent<Rigidbody>().velocity = Vector3.ClampMagnitude(GetComponent<Rigidbody>().velocity, maxSpeed);;
        }
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
}
