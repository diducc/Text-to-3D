#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Text;
using System.IO;
using Newtonsoft.Json.Linq;

public static class ComfyUIClient2D
{
    private const string TEMPLATE_PATH_RELATIVE = "Editor/ComfyJSON/2D";

    public static IEnumerator Generate(string server, string modelFileName, string prompt, Comfy2DParams settings, string saveFolder, Action<string> onStatus, Action<Texture2D> onSuccess, Action<string> onError)
    {
        int seed = UnityEngine.Random.Range(0, 999999999);
        float startTime = Time.realtimeSinceStartup;

        string jsonPath = Path.Combine(Application.dataPath, TEMPLATE_PATH_RELATIVE, modelFileName);
        
        if (!File.Exists(jsonPath)) 
        { 
            ComfyStatsRegistry.CurrentVRAMUsed = 0;
            onError($"Template non trovato: {jsonPath}"); 
            yield break; 
        }

        JObject workflow;
        try { workflow = JObject.Parse(File.ReadAllText(jsonPath)); }
        catch (Exception ex) 
        { 
            ComfyStatsRegistry.CurrentVRAMUsed = 0;
            onError($"Errore JSON: {ex.Message}"); 
            yield break; 
        }

        InjectParams(workflow, prompt, settings, seed);

        string promptId = null;
        string url = $"http://{server}/prompt";
        onStatus($"Sending prompt to {url}...");
        
        using (var w = new UnityWebRequest(url, "POST"))
        {
            JObject payload = new JObject(); payload["prompt"] = workflow;
            byte[] bodyRaw = Encoding.UTF8.GetBytes(payload.ToString(Newtonsoft.Json.Formatting.None));
            w.uploadHandler = new UploadHandlerRaw(bodyRaw);
            w.downloadHandler = new DownloadHandlerBuffer();
            w.SetRequestHeader("Content-Type", "application/json");
            w.useHttpContinue = false;

            var op = w.SendWebRequest();
            while (!op.isDone) yield return null;

            if (w.result != UnityWebRequest.Result.Success) 
            { 
                ComfyStatsRegistry.CurrentVRAMUsed = 0;
                onError($"Error: {w.error}"); 
                yield break; 
            }
            string res = w.downloadHandler.text;
            try { promptId = JObject.Parse(res)["prompt_id"]?.ToString(); } catch { }
        }
        
        if (string.IsNullOrEmpty(promptId)) 
        { 
            ComfyStatsRegistry.CurrentVRAMUsed = 0;
            onError("No Prompt ID."); 
            yield break; 
        }

        onStatus($"Rendering 2D (ID: {promptId})...");
        string fileName = null;
        string subFolder = "";
        string fileType = "output";

        float vramSum = 0;
        float vramMax = 0;
        int vramSamples = 0;

        for (int i = 0; i < 300; i++)
        {
            yield return new WaitForSeconds(1.0f);
            yield return ComfyStatsFetcher.UpdateVRAM(server, (currentVram) => 
            {
                vramSamples++;
                vramSum += currentVram;
                if (currentVram > vramMax) vramMax = currentVram;
            });

            using (var w = UnityWebRequest.Get($"http://{server}/history/{promptId}"))
            {
                w.useHttpContinue = false;
                var op = w.SendWebRequest();
                while (!op.isDone) yield return null;

                if (w.result == UnityWebRequest.Result.Success)
                {
                    string json = w.downloadHandler.text;
                    if (json.Contains("outputs") && json.Contains(promptId))
                    {
                        ParseHistoryForFileJObject(json, promptId, out fileName, out subFolder, out fileType);
                        if (!string.IsNullOrEmpty(fileName)) break;
                    }
                    else if (json.Contains("status_str\": \"error")) 
                    { 
                        ComfyStatsRegistry.CurrentVRAMUsed = 0;
                        onError("ComfyUI error."); 
                        yield break; 
                    }
                }
            }
            int elapsed = (int)(Time.realtimeSinceStartup - startTime);
            onStatus($"Rendering... ({elapsed}s) [VRAM: {ComfyStatsRegistry.CurrentVRAMUsed:F0}MB]");
        }

        if (string.IsNullOrEmpty(fileName)) 
        { 
            ComfyStatsRegistry.CurrentVRAMUsed = 0;
            onError("Timeout."); 
            yield break; 
        }

        float duration = Time.realtimeSinceStartup - startTime;
        float runAverage = vramSamples > 0 ? vramSum / vramSamples : 0;
        float runPeak = vramMax;

        ComfyStatsRegistry.LogRun(modelFileName, duration, runAverage, runPeak);
        
        ComfyStatsRegistry.CurrentVRAMUsed = 0;

        yield return new WaitForSeconds(0.5f);
        onStatus("Downloading...");
        string dlUrl = $"http://{server}/view?filename={fileName}&type={fileType}";
        if (!string.IsNullOrEmpty(subFolder)) dlUrl += $"&subfolder={subFolder}";

        using (var w = UnityWebRequest.Get(dlUrl))
        {
            w.downloadHandler = new DownloadHandlerBuffer();
            w.useHttpContinue = false;
            var op = w.SendWebRequest();
            while (!op.isDone) yield return null;

            if (w.result == UnityWebRequest.Result.Success)
            {
                Texture2D tex = new Texture2D(2, 2);
                if (tex.LoadImage(w.downloadHandler.data))
                {
                    tex.name = fileName;
                    if (!Directory.Exists(saveFolder)) Directory.CreateDirectory(saveFolder);
                    File.WriteAllBytes(Path.Combine(saveFolder, $"Comfy_{DateTime.Now.Ticks}.png"), w.downloadHandler.data);
                    AssetDatabase.Refresh();
                    onSuccess(tex);
                }
                else onError("Decode failed.");
            }
            else onError("Download failed.");
        }
    }
    private static void ParseHistoryForFileJObject(string json, string promptId, out string filename, out string subfolder, out string type)
    {
        filename = null; subfolder = ""; type = "output";
        try {
            JObject root = JObject.Parse(json);
            JObject outputs = (JObject)root[promptId]?["outputs"];
            if (outputs != null) {
                foreach (var node in outputs) {
                    JArray images = (JArray)node.Value["images"];
                    if (images.Count > 0) {
                        filename = images[0]["filename"]?.ToString();
                        subfolder = images[0]["subfolder"]?.ToString();
                        type = images[0]["type"]?.ToString();
                        return;
                    }
                }
            }
        } catch {}
    }

    private static void InjectParams(JObject workflow, string prompt, Comfy2DParams s, int seed)
    {
        foreach (var item in workflow)
        {
            JToken node = item.Value;
            string title = node["_meta"]?["title"]?.ToString();
            JObject inputs = (JObject)node["inputs"];
            if (inputs == null) continue;
            if (inputs.ContainsKey("seed")) inputs["seed"] = seed;
            if (title == "Positive Prompt") 
            { 
                if (inputs.ContainsKey("text")) inputs["text"] = prompt;
            }
            else if (title == "Resolution") 
            {
                if (s.width > 0) inputs["width"] = s.width;
                if (s.height > 0) inputs["height"] = s.height; 
            }
            else if (title == "Params")
            { 
                InjectIfPresent(inputs, "steps", s.steps); 
                InjectIfPresent(inputs, "cfg", s.cfg); 
                InjectIfPresent(inputs, "guidance", s.cfg); 
            }

        }
    }
    private static void InjectIfPresent(JObject inputs, string key, object value) 
    {
        if (inputs.ContainsKey(key)) 
        { 
            if (value is int i && i > 0) 
                inputs[key] = i; 
            else if (value is float f && f > 0) 
                inputs[key] = f; 
        } 
    }}
#endif