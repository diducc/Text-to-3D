using UnityEngine;
using GraphProcessor;
using System.Collections;
using UnityEditor;

[System.Serializable, NodeMenuItem("ComfyUI/Generate 3D")]
public class ComfyGen3DNode : BaseNode
{
    public string serverAddress = "127.0.0.1:8188";
    public string savePath = "Assets/Generated/3D";
    public Texture2D outputPreview;
    public override string name => "3D Generator";
    public bool isProcessing = false;
    public string statusMessage = "Waiting for Image...";

    //INPUT
    [Input(name = "Input Image")]
    public Texture2D inputImage; 

    [Input(name = "Settings"), ShowAsDrawer]
    public Comfy3DParams settings;

    // OUTPUT

    [Output(name = "Mesh File Path")]
    public string outputMeshPath;

    protected override void Process()
    {
        if (inputImage != null && !isProcessing && outputPreview == null)
            statusMessage = "Image Ready. Click Generate.";
    }

    public void ResetNode()
    {
        isProcessing = false;
        statusMessage = "Reset by User";
        outputPreview = null;
    }

    public IEnumerator StartGeneration()
    {
        outputPreview = null; 
        
        try { var p = new ProcessGraphProcessor(graph); p.Run(); } catch { }

        if (inputImage == null) { statusMessage = "Error: No Input Image!"; yield break; }
        if (settings.steps == 0 || settings.guidance == 0 || settings.octreeResolution == 0 || settings.maxFaces == 0)
        {
            settings = new Comfy3DParams 
            { 
                steps = 30, guidance = 7.5f, octreeResolution = 256, maxFaces = 35000
            };
        }
        
        
        isProcessing = true;
        statusMessage = "Encoding Image...";
        yield return null; 

        byte[] imageBytes = inputImage.EncodeToPNG();
        if (!isProcessing) yield break; 
        if (imageBytes == null) { statusMessage = "Error encoding PNG"; isProcessing = false; yield break; }

        statusMessage = "Uploading & Generating...";

        yield return ComfyUIClient3D.Generate(
            serverAddress,
            imageBytes,
            settings,
            savePath,
            (status) => { 
                if (isProcessing) statusMessage = status; 
            },
            (path, previewTex) => { 
                if (isProcessing) 
                {
                    outputPreview = previewTex;
                    outputMeshPath = path;
                    isProcessing = false;
                    
                    var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                    EditorGUIUtility.PingObject(asset);
                }
            },
            (err) => { 
                if (isProcessing)
                {
                    statusMessage = $"Error: {err}"; 
                    isProcessing = false;
                }
            }
        );
    }
}