using UnityEngine;
using GraphProcessor;
using System.Collections;

[System.Serializable, NodeMenuItem("ComfyUI/Generate 2D")]
public class ComfyGen2DNode : BaseNode
{
    public string serverAddress = "127.0.0.1:8189";
    public string savePath = "Assets/Generated";
    public override string name => "2D Generator";
    public bool isProcessing = false;
    public string statusMessage = "Ready";

    [SerializeField]
    public string selectedModelFile = "";

    // INPUT
    [Input(name = "Prompt"), ShowAsDrawer]
    public string prompt = "";

    [Input(name = "Settings"), ShowAsDrawer]
    public Comfy2DParams settings;

    //OUTPUT
    [Output(name = "Generated Texture")]
    public Texture2D outputTexture;

    protected override void Process()
    {
        //Viene avviato con processor.Run
        // Called when the graph is process, process inputs and assign the result in output.
    }

    public IEnumerator StartGeneration()
    {
        if (string.IsNullOrEmpty(selectedModelFile))
        {
            statusMessage = "Error: Select a Model!";
            yield break;
        }

        statusMessage = "Syncing Data...";

        // LETTURA DEI CAVI
        try 
        {
            var processor = new ProcessGraphProcessor(graph);
            processor.Run(); 
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Generator] Graph Process Error: {e.Message}");
        }
        if (settings.width == 0) settings = new Comfy2DParams 
        { 
            width = 1024, height = 1024, steps = 30, cfg = 8.0f 
        };

        if (string.IsNullOrEmpty(prompt))
        {
            statusMessage = "Error: Prompt Missing!";
            yield break;
        }

        isProcessing = true;
        statusMessage = "Generating...";

        // CHIAMATA AL CLIENT
        yield return ComfyUIClient2D.Generate(
            serverAddress,
            selectedModelFile,
            prompt,
            settings,
            savePath,
            (status) => { 
                statusMessage = status;
            },
            (tex) => { 
                outputTexture = tex; 
                statusMessage = "Success!"; 
                isProcessing = false;
                Debug.Log("[Generator] Success Texture received.");
            },
            (err) => { 
                statusMessage = $"Error: {err}"; 
                isProcessing = false;
                Debug.LogError($"[Generator] Comfy Error: {err}");
            }
        );
    }

    //RESET DEL NODO
    public void ResetNode()
    {
        isProcessing = false;
        statusMessage = "Reset by User";
        outputTexture = null;
        Debug.LogWarning("[Generator 2D] Node Reset Manually.");
    }
}