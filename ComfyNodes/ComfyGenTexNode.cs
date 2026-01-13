using UnityEngine;
using GraphProcessor;
using System.Collections;
using UnityEditor;

[System.Serializable, NodeMenuItem("ComfyUI/Generate Texture (Paint)")]
public class ComfyGenTexNode : BaseNode
{
    [Input(name = "Original Image")]
    public Texture2D inputImage;

    [Input(name = "Mesh Path"), ShowAsDrawer]
    public string inputMeshPath;

    [Input(name = "Settings"), ShowAsDrawer]
    public ComfyTexGenParams settings;

    public string serverAddress = "127.0.0.1:8188";
    public string outputModelPath;
    public string savePath = "Assets/Generated";

    public bool isProcessing = false;
    public Texture2D outputPreview;
    public string statusMessage = "Waiting inputs...";

    public override string name => "Texture Painting";

    protected override void Process() { }

    public IEnumerator StartGeneration()
    {
        try { var processor = new ProcessGraphProcessor(graph); processor.Run(); } catch {}

        if (inputImage == null) 
        {
            statusMessage = "Error: Missing Original Image!";
            yield break;
        }

        if (string.IsNullOrEmpty(inputMeshPath))
        {
            statusMessage = "Error: Missing Mesh Path (Generate 3D first)!";
            yield break;
        }

        if (!System.IO.File.Exists(inputMeshPath))
        {
            statusMessage = "Error: Mesh file not found on disk!";
            yield break;
        }

        if (settings.resolution == 0) {
            settings = new ComfyTexGenParams{
            maxNumViews = 5,
            resolution = 512,
            enableMMGP = true,
            createPBR = true,
            useRemesh = false
            };
        }

        isProcessing = true;
        statusMessage = "Uploading & Painting...";

        byte[] imgBytes = inputImage.EncodeToPNG();

        yield return ComfyUIClientTex.Generate(
            serverAddress,
            imgBytes,
            inputMeshPath,
            settings,
            savePath,
            (status) => statusMessage = status,
            (finalPath, previewTex) => {
                if (isProcessing) 
                {
                    statusMessage = "Success! Saved to: " + finalPath;
                    outputModelPath = finalPath;
                    outputPreview = previewTex; 
                    isProcessing = false;
                    
                    var asset = AssetDatabase.LoadAssetAtPath<Object>(finalPath);
                    EditorGUIUtility.PingObject(asset);
                    
                    Debug.Log($"[TexGen] Model Textured: {finalPath}");
                }
            },
            (err) => {
                statusMessage = "Error: " + err;
                isProcessing = false;
            }
        );
    }
}