using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Entities;

public class SPHParticleObject : MonoBehaviour
{
    public static GameObject ParticlePrefab;

    [SerializeField]
    private GameObject obj;

    private void Awake()
    {
        ParticlePrefab = obj;
    }

    private void Start()
    {
        World.DefaultGameObjectInjectionWorld.CreateSystem<SPHSystem>();
    }

    private void Update()
    {
        var s = World.DefaultGameObjectInjectionWorld.GetExistingSystem<SPHSystem>();
        s.Update();
    }
}
