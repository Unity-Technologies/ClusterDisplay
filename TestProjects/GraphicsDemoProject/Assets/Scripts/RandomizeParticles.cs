using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

[RequireComponent(typeof(ParticleSystem))]
public class RandomizeParticles : MonoBehaviour
{
    ParticleSystem m_ParticleSystem;

    [SerializeField]
    float m_Delay;
    
    void OnEnable()
    {
        var particleSystem = GetComponent<ParticleSystem>();
        StartCoroutine(RandomizeCoroutine(particleSystem));
    }

    void OnDisable()
    {
        StopAllCoroutines();
    }

    IEnumerator RandomizeCoroutine(ParticleSystem particleSystem)
    {
        yield return null;
        Randomize(particleSystem);
        
        for (;;)
        {
            yield return new WaitForSeconds(m_Delay);
            Randomize(particleSystem);
        }
    }

    static void Randomize(ParticleSystem particleSystem)
    {
        particleSystem.Stop();
        particleSystem.Clear();
        particleSystem.randomSeed = (uint)Random.Range(0, 128);
        particleSystem.Simulate(0, true);
        particleSystem.Play();
    }
}
