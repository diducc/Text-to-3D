using UnityEngine;
using GraphProcessor;

[System.Serializable, NodeMenuItem("ComfyUI/Prompt Text")]
public class ComfyPromptNode : BaseNode
{
    [Input(name = "Prompt"), ShowAsDrawer]
    [TextArea(3, 10)] 
    public string promptText = "";

    [Output(name = "Prompt String")]
    public string outputText;

    public override string name => "Prompt";

    protected override void Process()
    {
        outputText = promptText;
    }
}