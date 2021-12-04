using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

public class StateScriptableObject : ScriptableObject
{
    public string Name;
    public Color Color;
}

public class CellStateManager : MonoBehaviour
{
    public List<StateScriptableObject> Cells;
}

public struct CellStateData
{
    public float4 CellColor;
}

public struct CellStatesBlobAsset
{
    public BlobArray<CellStateData> CellStates;
}

public class CellStateBuilder : GameObjectConversionSystem
{

    public BlobAssetReference<CellStatesBlobAsset> CellStatesBlobReference;

    protected override void OnUpdate()
    {
        using BlobBuilder blobBuilder = new();

        ref CellStatesBlobAsset cellStatesBlobAsset = ref blobBuilder.ConstructRoot<CellStatesBlobAsset>();
        blobBuilder.Allocate(ref cellStatesBlobAsset.CellStates, 256);

        CellStatesBlobReference = blobBuilder.CreateBlobAssetReference<CellStatesBlobAsset>(Allocator.Persistent);
    }
}
