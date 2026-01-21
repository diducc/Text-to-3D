using UnityEditor;
using UnityEngine.UIElements;
using GraphProcessor;
using UnityEngine;

[NodeCustomEditor(typeof(ComfyPromptNode))]
public class ComfyPromptNodeView : BaseNodeView
{
    public override void Enable()
    {
        var node = nodeTarget as ComfyPromptNode;

        var textField = new TextField
        {
            multiline = true,
            value = node.promptText
        };
        
        textField.style.height = 130;
        textField.style.minHeight = 130; 

        textField.style.whiteSpace = WhiteSpace.Normal;
        textField.style.unityTextAlign = TextAnchor.UpperLeft;
        
        textField.style.width = 250; 

        textField.RegisterValueChangedCallback(evt => {
            node.promptText = evt.newValue;
        });

        controlsContainer.Add(textField);
    }
}