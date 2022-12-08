using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class spillFX : MonoBehaviour
{
    ParticleSystem _particleSystem;
    // Start is called before the first frame update
    void Start()
    {
        _particleSystem = GetComponent<ParticleSystem>();
    }

    // Update is called once per frame
    void Update()
    {
        if(Vector3.Angle(Vector3.down,transform.forward) <= 90f)
        {
            _particleSystem.Play();
        }
        else
        {
            _particleSystem.Stop();
        }
    }
}
