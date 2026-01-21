using UnityEngine;
using GraphProcessor;

[System.Serializable, NodeMenuItem("ComfyUI/Stats Monitor")]
public class ComfyStatsNode : BaseNode
{
    public override string name => "📊 Model Stats";

    // Non ha input o output logici necessari, serve solo a visualizzare
    
    protected override void Process() { }
}