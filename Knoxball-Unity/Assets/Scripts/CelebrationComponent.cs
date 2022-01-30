using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CelebrationComponent : MonoBehaviour
{
    public ParticleSystem confetti;
    public GameObject celebrationCamera;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void Celebrate() {
        confetti.Play();
        celebrationCamera.SetActive(true);
    }

    public void StopCelebration() {
        confetti.Stop();
        celebrationCamera.SetActive(false);
    }
}
