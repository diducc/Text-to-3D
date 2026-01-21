using GraphProcessor;
using UnityEngine;


public enum ComfyToggle
{
    True,
    False
}

[System.Serializable, NodeMenuItem("ComfyUI/Settings 2D")]
public class ComfyParam2DNode : BaseNode
{
    [Output(name = "Settings")]
    public Comfy2DParams paramsOut;

    public int width = 1024;
    public int height = 1024;
    [Tooltip("Più passi = più qualità ma più lento")]
    public int steps = 35;
    [Tooltip("Creatività vs Fedeltà al prompt (consigliato 7-9)")]
    [Range (0, 100)]
    public float cfg = 8.0f;

    public override string name => "2D Parameters";

    protected override void Process()
    {
        paramsOut = new Comfy2DParams
        {
            width = width,
            height = height,
            steps = steps,
            cfg = cfg
        };
    }
}

// 3D NODE


[System.Serializable, NodeMenuItem("ComfyUI/Settings 3D")]
public class ComfyParam3DNode : BaseNode
{
    [Output(name = "Settings")]
    public Comfy3DParams paramsOut;

    public int steps = 40;
    public float guidance = 7.5f;
    public int octreeRes = 256;
    public int maxFaces = 40000;
    public ComfyToggle removeBackground = ComfyToggle.True;

    public override string name => "3D Parameters";

    protected override void Process()
    {
        paramsOut = new Comfy3DParams
        {
            steps = steps,
            guidance = guidance,
            octreeResolution = octreeRes,
            maxFaces = maxFaces,
            removeBackground = (removeBackground == ComfyToggle.True)
        };
    }
}

public enum TexResolution
{
    _512 = 512,
    _728 = 728,
    _1024 = 1024
}

[System.Serializable, NodeMenuItem("ComfyUI/Settings Texture")]
public class ComfyParamTexNode : BaseNode
{
    [Output(name = "Settings")]
    public ComfyTexGenParams paramsOut;

    [Range(4, 12)] 
    public int maxNumViews = 5;

    public TexResolution resolution = TexResolution._512;

    [Header("Options")]
    public ComfyToggle enableMMGP = ComfyToggle.True;
    public ComfyToggle createPBR = ComfyToggle.True;
    public ComfyToggle useRemesh = ComfyToggle.False;

    public override string name => "Texture Parameters";

    protected override void Process()
    {
        paramsOut = new ComfyTexGenParams
        {
            maxNumViews = maxNumViews,
            
            resolution = (int)resolution, 
            
            enableMMGP = (enableMMGP == ComfyToggle.True),
            createPBR = (createPBR == ComfyToggle.True),
            useRemesh = (useRemesh == ComfyToggle.True)
        };
    }
}