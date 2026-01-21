using UnityEngine.UIElements;
using GraphProcessor;
using UnityEngine;

[NodeCustomEditor(typeof(ComfyStatsNode))]
public class ComfyStatsNodeView : BaseNodeView
{
    private Label vramLabel;
    private VisualElement vramBarBG;
    private VisualElement vramBarFill;
    private ScrollView statsContainer;

    public override void Enable()
    {
        style.width = 350;
        
        var header = new Label("GPU VRAM STATUS");
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginTop = 5;
        controlsContainer.Add(header);

        vramBarBG = new VisualElement();
        vramBarBG.style.height = 20;
        vramBarBG.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
        
        vramBarBG.style.borderTopWidth = 1;
        vramBarBG.style.borderBottomWidth = 1;
        vramBarBG.style.borderLeftWidth = 1;
        vramBarBG.style.borderRightWidth = 1;

        vramBarBG.style.borderTopColor = Color.black;
        vramBarBG.style.borderBottomColor = Color.black;
        vramBarBG.style.borderLeftColor = Color.black;
        vramBarBG.style.borderRightColor = Color.black;
        
        vramBarBG.style.marginBottom = 5;
        
        vramBarFill = new VisualElement();
        vramBarFill.style.height = 20;
        vramBarFill.style.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
        vramBarFill.style.width = 0; 
        
        vramBarBG.Add(vramBarFill);
        controlsContainer.Add(vramBarBG);

        vramLabel = new Label("Waiting for data...");
        controlsContainer.Add(vramLabel);

        var listHeader = new Label("HISTORY PER MODEL");
        listHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
        listHeader.style.marginTop = 15;
        controlsContainer.Add(listHeader);

        statsContainer = new ScrollView();
        statsContainer.style.height = 200;
        controlsContainer.Add(statsContainer);

        schedule.Execute(UpdateStatsUI).Every(1000);
    }

    private void UpdateStatsUI()
    {
        float used = ComfyStatsRegistry.CurrentVRAMUsed;
        float total = ComfyStatsRegistry.TotalVRAMAvailable;
        if (total > 0)
        {
            float perc = used / total;
            vramBarFill.style.width = Length.Percent(perc * 100);
            vramLabel.text = $"{used:F0} MB / {total:F0} MB ({perc*100:F1}%)";
            if (perc > 0.9f) vramBarFill.style.backgroundColor = Color.red;
            else if (perc > 0.7f) vramBarFill.style.backgroundColor = new Color(1f, 0.5f, 0f); 
            else vramBarFill.style.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
        }

        statsContainer.Clear();
        
        if (ComfyStatsRegistry.Stats.Count == 0)
        {
            statsContainer.Add(new Label("No generations yet."));
            return;
        }

        foreach (var entry in ComfyStatsRegistry.Stats)
        {
            var model = entry.Key;
            var data = entry.Value;

            var card = new VisualElement();
            card.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f);
            card.style.paddingTop = 5; card.style.paddingBottom = 5; card.style.paddingLeft = 5;
            card.style.marginBottom = 2;
            card.style.borderTopLeftRadius = 5; card.style.borderTopRightRadius = 5;
            card.style.borderBottomLeftRadius = 5; card.style.borderBottomRightRadius = 5;

            var title = new Label(FormatName(model));
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            
            var timeStats = new Label($"⏱ Time: {data.LastTime:F1}s (Avg: {data.AverageTime:F1}s)");
            
            var vramStats = new Label($"💾 VRAM: Avg {data.GlobalAverageVRAM:F0} MB | Peak {data.GlobalAveragePeakVRAM:F0} MB");
            vramStats.style.color = new Color(0.7f, 0.7f, 0.7f);

            card.Add(title);
            card.Add(timeStats);
            card.Add(vramStats);
            
            statsContainer.Add(card);
        }
    }

    private string FormatName(string jsonFile)
    {
        return jsonFile.Replace(".json", "").Replace("_", " ");
    }
}