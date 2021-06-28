using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InputManager : MonoBehaviour
{
    public GameObject player;
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

    float m_ForceStrength = 7.0f;
    float m_kickStrength = 50.0f;
    float m_DistanceForKick = 10.0f;
    
    [SerializeField, Tooltip("Pan the camera using keys.")]
    private KeyboardKeyMovement m_KeyboardKeyMovement = new KeyboardKeyMovement { enabled = true};

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        UpdatePlayer();
    }

    void UpdatePlayer()
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

        player.GetComponent<Rigidbody>().AddForce(force);

        if (Input.GetKey(kick))
        {
            var directionVector = (ball.transform.position - player.transform.position);
            float distanceSquared = directionVector.sqrMagnitude;
            if (distanceSquared < m_DistanceForKick)
            {
                ball.GetComponent<Rigidbody>().AddForce(directionVector.normalized * m_kickStrength);
            }
        }
    }
}
