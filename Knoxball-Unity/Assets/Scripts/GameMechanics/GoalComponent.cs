using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Knoxball
{
    public class GoalComponent : MonoBehaviour
    {
        public GameObject ball;
        public UnityEvent ballInNet;
        bool alreadyInside;
        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            var boxCollider2D = gameObject.GetComponent<BoxCollider2D>();
            if (boxCollider2D.OverlapPoint(ball.transform.position) && !alreadyInside)
            {
                ballInNet.Invoke();
                alreadyInside = true;
            }
        }

        public void Reset()
        {
            alreadyInside = false;
        }
    }
}