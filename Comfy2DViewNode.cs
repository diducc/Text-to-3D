using UnityEditor;
using UnityEngine.UIElements;
using GraphProcessor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;


[NodeCustomEditor(typeof(ComfyGen2DNode))]
public class Comfy2DNodeView : BaseNodeView
{
    private DropdownField modelDropdown;
    private Button generateButton;
    private Button resetButton;
    private Label statusLabel;
    private Image previewImage;

    public override void Enable()
    {
        style.width = 300;

        var node = nodeTarget as ComfyGen2DNode;

        var modelsPath = Path.Combine(Application.dataPath, "Editor/ComfyJSON/2D");
        List<string> jsonFiles = new List<string>();

        if (Directory.Exists(modelsPath))
        {
            jsonFiles = Directory.GetFiles(modelsPath, "*.json")
                                .Select(Path.GetFileName)
                                .ToList();
        }
        else
        {
            Debug.LogError($"Cartella non trovata: {modelsPath}. Creala e metti i JSON!");
            jsonFiles.Add("No Templates Found");
        }

        modelDropdown = new DropdownField("Model", jsonFiles, 0);
        modelDropdown.style.marginBottom = 10;
        
        if (!string.IsNullOrEmpty(node.selectedModelFile) && jsonFiles.Contains(node.selectedModelFile))
        {
            modelDropdown.value = node.selectedModelFile;
        }
        else if (jsonFiles.Count > 0)
        {
            node.selectedModelFile = jsonFiles[0];
            modelDropdown.value = jsonFiles[0];
        }

        modelDropdown.RegisterValueChangedCallback(evt => {
            node.selectedModelFile = evt.newValue;
        });

        controlsContainer.Add(modelDropdown);

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
        
        schedule.Execute(() => UpdateUI(node)).Every(100);
    }

    private void GenerateClick(ComfyGen2DNode node)
    {
        node.selectedModelFile = modelDropdown.value;

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