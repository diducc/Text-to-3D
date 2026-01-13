using UnityEditor;
using UnityEngine.UIElements;
using GraphProcessor;
using UnityEngine;


[NodeCustomEditor(typeof(ComfyGen2DNode))]
public class Comfy2DNodeView : BaseNodeView
{
    private Button generateButton;
    private Button resetButton;
    private Label statusLabel;
    private Image previewImage;

    public override void Enable()
    {
        style.width = 300;

        var node = nodeTarget as ComfyGen2DNode;

        var buttonContainer = new VisualElement();
        buttonContainer.style.flexDirection = FlexDirection.Row;

        generateButton = new Button(() => GenerateClick(node))
        {
            text = "GENERATE 2D"
        };
        generateButton.style.flexGrow = 1;
        generateButton.style.height = 30;
        generateButton.style.backgroundColor = new Color(0.2f, 0.6f, 0.2f); 

        resetButton = new Button(() => { 
            node.ResetNode(); 
            UpdateUI(node); 
        }) { text = "Reset" };
        resetButton.style.width = 40;
        resetButton.style.height = 30;
        resetButton.style.backgroundColor = new Color(0.8f, 0.2f, 0.2f);
        resetButton.style.marginLeft = 5;

        buttonContainer.Add(generateButton);
        buttonContainer.Add(resetButton);

        statusLabel = new Label("Ready");
        
        previewImage = new Image();
        previewImage.scaleMode = ScaleMode.ScaleToFit;
        previewImage.style.height = 150;
        previewImage.style.display = DisplayStyle.None;

        controlsContainer.Add(buttonContainer);
        controlsContainer.Add(statusLabel);
        controlsContainer.Add(previewImage);
        
        // Aggiorna la UI ogni 100ms per mostrare lo stato
        schedule.Execute(() => UpdateUI(node)).Every(100);
    }

    private void GenerateClick(ComfyGen2DNode node)
    {
        var window = EditorWindow.GetWindow<ComfyGraphWindow>();
        
        if (window != null)
        {
            EditorCoroutineRunner.StartCoroutine(node.StartGeneration(), window);
        }
        else
        {
            Debug.LogError("Impossibile trovare la finestra ComfyGraphWindow!");
        }
    }

    private void UpdateUI(ComfyGen2DNode node)
    {
        statusLabel.text = node.statusMessage;
        generateButton.SetEnabled(!node.isProcessing);
        if (node.outputTexture != null)
        {
            previewImage.image = node.outputTexture;
            previewImage.style.display = DisplayStyle.Flex;
        }
        else
        {
            previewImage.style.display = DisplayStyle.None;
        }
    }
}