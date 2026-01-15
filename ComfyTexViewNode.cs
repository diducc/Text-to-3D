using UnityEditor;
using UnityEngine.UIElements;
using GraphProcessor;
using UnityEngine;

[NodeCustomEditor(typeof(ComfyGenTexNode))]
public class ComfyTexNodeView : BaseNodeView
{
    private Button generateButton;
    private Button resetButton; 
    private Label statusLabel; 
    private Image outputPreview;

    public override void Enable()
    {
        var node = nodeTarget as ComfyGenTexNode;
        style.width = 300; 
        var buttonContainer = new VisualElement();
        buttonContainer.style.flexDirection = FlexDirection.Row;

        generateButton = new Button(() => GenerateClick(node))
        {
            text = "PAINT TEXTURE"
        };
        generateButton.style.height = 30;
        generateButton.style.flexGrow = 1;
        generateButton.style.backgroundColor = new Color(0.6f, 0.2f, 0.6f); 
        generateButton.style.marginBottom = 5;

        resetButton = new Button(() => { 
            node.isProcessing = false;
            node.statusMessage = "Ready";
            node.outputModelPath = null;
            UpdateUI(node);
        }) { text = "Reset" }; 
        resetButton.style.width = 40;
        resetButton.style.height = 30;
        resetButton.style.backgroundColor = new Color(0.8f, 0.2f, 0.2f);
        resetButton.style.marginLeft = 5;

        buttonContainer.Add(generateButton);
        buttonContainer.Add(resetButton);

        statusLabel = new Label("Waiting inputs...");
        statusLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        statusLabel.style.whiteSpace = WhiteSpace.Normal;
        statusLabel.style.marginBottom = 5;

        outputPreview = new Image();
        outputPreview.scaleMode = ScaleMode.ScaleToFit;
        outputPreview.style.height = 200;
        outputPreview.style.width = Length.Percent(100);
        outputPreview.style.marginTop = 10;
        outputPreview.style.backgroundColor = new Color(0.3f, 0.1f, 0.3f, 0.4f);
        outputPreview.style.display = DisplayStyle.None;
        outputPreview.Add(new Label("Output Texture Preview") { 
            style = { color = new Color(0.8f, 0.4f, 1f), fontSize = 10, unityFontStyleAndWeight = FontStyle.Bold } 
        });

        controlsContainer.Add(buttonContainer);
        controlsContainer.Add(statusLabel);
        controlsContainer.Add(outputPreview);

        schedule.Execute(() => UpdateUI(node)).Every(100);
    }

    private void GenerateClick(ComfyGenTexNode node)
    {
        var window = EditorWindow.GetWindow<ComfyGraphWindow>();
        if (window != null) 
            EditorCoroutineRunner.StartCoroutine(node.StartGeneration(), window);
        else 
            Debug.LogError("Graph Window not found!");
    }

    private void UpdateUI(ComfyGenTexNode node)
    {
        statusLabel.text = node.statusMessage;
        generateButton.SetEnabled(!node.isProcessing);

        if (node.outputPreview != null)
        {
            outputPreview.image = node.outputPreview;
            outputPreview.style.display = DisplayStyle.Flex;
        }
        else
        {
            outputPreview.style.display = DisplayStyle.None;
            outputPreview.image = null;
        }
    }
}