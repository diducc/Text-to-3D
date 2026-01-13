using UnityEditor;
using UnityEngine;
using GraphProcessor;
using UnityEditor.Callbacks;

public class ComfyGraphWindow : BaseGraphWindow
{

    [OnOpenAsset(0)]
    public static bool OnBaseGraphOpened(int instanceID, int line)
    {
        var asset = EditorUtility.EntityIdToObject(instanceID) as ComfyGraph;

        if (asset != null)
        {
            var window = GetWindow<ComfyGraphWindow>();
            window.Show();
            window.InitializeGraph(asset); 
            
            return true;
        }
        return false;
    }

    [MenuItem("Window/ComfyUI Graph")]
    public static void Open()
    {
        var graphWindow = GetWindow<ComfyGraphWindow>();
        graphWindow.Show();
    }

    protected override void InitializeWindow(BaseGraph graph)
    {
        titleContent = new GUIContent("Comfy Workflow");

        if (graphView == null)
        {
            graphView = new BaseGraphView(this);
            graphView.Add(new ToolbarView(graphView));
        }

        rootVisualElement.Add(graphView);

        if (graph != null)
        {
            graphView.Initialize(graph);
        }
    }
}