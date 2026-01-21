#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

public class ModelStats
{
    //tempo
    public string ModelName;
    public int RunCount;
    
    public float LastTime;
    public float TotalTime;
    public float AverageTime => RunCount > 0 ? TotalTime / RunCount : 0;
    
    // vram
    private float SumOfAverages; 
    private float SumOfPeaks; 

    public float GlobalAverageVRAM => RunCount > 0 ? SumOfAverages / RunCount : 0;
    public float GlobalAveragePeakVRAM => RunCount > 0 ? SumOfPeaks / RunCount : 0;

    public void AddEntry(float timeSeconds, float runAvgVram, float runPeakVram)
    {
        RunCount++;
        LastTime = timeSeconds;
        TotalTime += timeSeconds;
        
        SumOfAverages += runAvgVram;
        SumOfPeaks += runPeakVram;
    }
}

public static class ComfyStatsRegistry
{
    public static Dictionary<string, ModelStats> Stats = new Dictionary<string, ModelStats>();
    public static float CurrentVRAMUsed = 0;
    public static float TotalVRAMAvailable = 0;

    public static void LogRun(string modelName, float duration, float runAvgVram, float runPeakVram)
    {
        if (!Stats.ContainsKey(modelName))
            Stats[modelName] = new ModelStats { ModelName = modelName };

        Stats[modelName].AddEntry(duration, runAvgVram, runPeakVram);
    }
}
public static class ComfyStatsFetcher
{
    public static IEnumerator UpdateVRAM(string server, Action<float> onValueRead = null)
    {
        using (var w = UnityWebRequest.Get($"http://{server}/system_stats"))
        {
            w.useHttpContinue = false;
            var op = w.SendWebRequest();
            float timeout = Time.realtimeSinceStartup + 0.5f;
            while (!op.isDone) { if (Time.realtimeSinceStartup > timeout) yield break; yield return null; }

            if (w.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    JObject json = JObject.Parse(w.downloadHandler.text);
                    JArray devices = json["devices"] as JArray ?? json["system"]?["devices"] as JArray; 

                    if (devices != null && devices.Count > 0)
                    {
                        JObject gpu = (JObject)devices[0];
                        long total = (long)gpu["vram_total"];
                        long free = (long)gpu["vram_free"];
                        float usedMB = (total - free) / 1024f / 1024f;
                        float totalMB = total / 1024f / 1024f;

                        ComfyStatsRegistry.CurrentVRAMUsed = usedMB;
                        ComfyStatsRegistry.TotalVRAMAvailable = totalMB;
                        onValueRead?.Invoke(usedMB);
                    }
                }
                catch { }
            }
        }
    }
}
#endif