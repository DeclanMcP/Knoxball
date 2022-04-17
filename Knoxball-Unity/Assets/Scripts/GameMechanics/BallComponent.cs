using Unity.Netcode;
using UnityEngine;

namespace Knoxball
{
    public class BallComponent : NetworkBehaviour
    {
        float maxSpeed = 5.0f;
        float m_DistanceForKick = 2.0f;
        float m_kickStrength = 5000.0f;

        public void ManualUpdate()
        {
            UpdateVelocity();
        }

        void UpdateVelocity()
        {
            if (GetComponent<Rigidbody>().velocity.magnitude > maxSpeed)
            {
                GetComponent<Rigidbody>().velocity = Vector3.ClampMagnitude(GetComponent<Rigidbody>().velocity, maxSpeed); ;
            }
        }

        public void Kick(Vector3 origin)
        {
            if (IsHost)
            {
                var directionVector = (gameObject.transform.position - origin);
                float distanceSquared = directionVector.sqrMagnitude;
                if (distanceSquared < m_DistanceForKick)
                {
                    gameObject.GetComponent<Rigidbody>().AddForce(directionVector.normalized * m_kickStrength * Time.fixedDeltaTime);
                    UpdateVelocity();
                }
            }
        }

        internal NetworkBallState getCurrentState()
        {
            return new NetworkBallState(transform.position, GetComponent<Rigidbody>().velocity, transform.rotation);
        }

        public void SetState(NetworkBallState ballState)
        {
            transform.position = ballState.position;
            GetComponent<Rigidbody>().velocity = ballState.velocity;
            transform.rotation = ballState.rotation;
        }
    }
}