using UnityEngine;

[System.Serializable]
public struct Comfy2DParams
{
    public int width;
    public int height;
    public int steps;
    public float cfg;
}

[System.Serializable]
public struct Comfy3DParams
{
    public int steps;
    public float guidance;
    public int octreeResolution;
    public int maxFaces;
    public bool removeBackground; 
}


[System.Serializable]
public struct ComfyTexGenParams
{
    public int maxNumViews;
    public int resolution;
    
    public bool enableMMGP;
    public bool createPBR;
    public bool useRemesh;
}