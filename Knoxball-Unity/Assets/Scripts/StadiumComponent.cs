using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StadiumComponent : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void Reset() {
        var goals = GetComponentsInChildren<GoalComponent>();
        foreach (GoalComponent goal in goals)
            goal.Reset();
    }
}
