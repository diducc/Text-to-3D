#if UNITY_EDITOR
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using UnityEditor;
using Newtonsoft.Json.Linq;

public static class ComfyUIClient3D
{
    private const string TEMPLATE_PATH_RELATIVE = "Editor/ComfyJSON/3D"; 

    public static IEnumerator Generate(string server, string modelFileName, byte[] imageBytes, Comfy3DParams settings, string saveFolder, Action<string> onStatus, Action<string, Texture2D> onSuccess, Action<string> onError)
    {
        float startTime = Time.realtimeSinceStartup;
        float vramSum = 0;
        float vramMax = 0;
        int vramSamples = 0;

        int seed = UnityEngine.Random.Range(0, 999999999);
        string jsonPath = Path.Combine(Application.dataPath, TEMPLATE_PATH_RELATIVE, modelFileName);
        string uploadName = $"unity_input_{seed}.png";

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

        InjectParams(workflow, settings, seed);

        bool imageNodeFound = false;
        foreach (var item in workflow)
        {
            if (item.Value["class_type"]?.ToString() == "LoadImage")
            {
                if (item.Value["inputs"] != null)
                {
                    item.Value["inputs"]["image"] = uploadName;
                    imageNodeFound = true;
                }
            }
        }
        if (!imageNodeFound) Debug.LogWarning("Comfy3D: Nodo 'LoadImage' non trovato nel JSON. L'upload potrebbe essere ignorato.");

        string expectedFileName = $"Mesh_{seed}.glb"; 
        string promptId = null;

        onStatus("Uploading image...");
        WWWForm form = new WWWForm();
        form.AddBinaryData("image", imageBytes, uploadName, "image/png");
        form.AddField("overwrite", "true");

        using (var w = UnityWebRequest.Post($"http://{server}/upload/image", form))
        {
            w.useHttpContinue = false;
            var op = w.SendWebRequest();
            while (!op.isDone) yield return null;
            if (w.result != UnityWebRequest.Result.Success) 
            { 
                ComfyStatsRegistry.CurrentVRAMUsed = 0;
                onError($"Upload Failed: {w.error}"); 
                yield break; 
            }
        }

        onStatus("Sending Workflow...");
        
        string url = $"http://{server}/prompt";
        using (var w = new UnityWebRequest(url, "POST"))
        {
            JObject payload = new JObject();
            payload["prompt"] = workflow;
            
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
                onError($"Conn Error: {w.error}"); 
                yield break; 
            }

            string res = w.downloadHandler.text;
            try { promptId = JObject.Parse(res)["prompt_id"]?.ToString(); } catch { }
        }
        
        if (string.IsNullOrEmpty(promptId)) 
        { 
            ComfyStatsRegistry.CurrentVRAMUsed = 0;
            onError("Failed to get Prompt ID"); 
            yield break; 
        }

        bool isReady = false;
        onStatus($"Processing 3D (ID: {promptId})...");
        
        for (int i = 0; i < 600; i++) 
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
                    if (json.Contains($"\"{promptId}\"")) 
                    {
                        if (json.Contains("status_str\": \"error") || json.Contains("\"error\""))
                        {
                            ComfyStatsRegistry.CurrentVRAMUsed = 0;
                            onError("ComfyUI Error during generation.");
                            yield break;
                        }
                        
                        isReady = true;
                        break; 
                    }
                }
            }
            int elapsed = (int)(Time.realtimeSinceStartup - startTime);
            onStatus($"Generating Model... ({elapsed}s) [VRAM: {ComfyStatsRegistry.CurrentVRAMUsed:F0}MB]");
        }

        if (!isReady) 
        { 
            ComfyStatsRegistry.CurrentVRAMUsed = 0;
            onError("Timeout: Generation took too long."); 
            yield break; 
        }

        float duration = Time.realtimeSinceStartup - startTime;
        float runAverage = vramSamples > 0 ? vramSum / vramSamples : 0;
        ComfyStatsRegistry.LogRun(modelFileName, duration, runAverage, vramMax);
        
        ComfyStatsRegistry.CurrentVRAMUsed = 0;

        onStatus($"Downloading {expectedFileName}...");
        
        string expectedUrl = $"http://{server}/view?filename={expectedFileName}&type=output";

        using (var w = UnityWebRequest.Get(expectedUrl))
        {
            w.downloadHandler = new DownloadHandlerBuffer();
            w.useHttpContinue = false;
            var op = w.SendWebRequest();
            while (!op.isDone) yield return null;

            if (w.result == UnityWebRequest.Result.Success)
            {
                if (!Directory.Exists(saveFolder)) Directory.CreateDirectory(saveFolder);
                string path = $"{saveFolder}/{expectedFileName}";
                File.WriteAllBytes(path, w.downloadHandler.data);
                AssetDatabase.Refresh();

                int waitFrames = 0;
                GameObject model = null;
                while (waitFrames < 50) 
                {
                    model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (model != null) break;
                    yield return null;
                    waitFrames++;
                }

                Texture2D previewTex = null;
                if (model != null)
                {
                    for(int k=0; k<15; k++)
                    {
                        previewTex = AssetPreview.GetAssetPreview(model);
                        if(previewTex != null && previewTex.width > 20) break; 
                        yield return new WaitForSeconds(0.2f);
                    }
                }

                onSuccess(path, previewTex);
            }
            else 
            {
                onError($"Download Error: {w.error} (File missing?)");
            }
        }
    }

    private static void InjectParams(JObject workflow, Comfy3DParams s, int seed)
    {
        foreach (var item in workflow)
        {
            JToken node = item.Value;
            string title = node["_meta"]?["title"]?.ToString();
            JObject inputs = (JObject)node["inputs"];
            if (inputs == null) continue;
            
            if (title == "Params")
            { 
                InjectIfPresent(inputs, "steps", s.steps);
                InjectIfPresent(inputs, "guidance", s.guidance);
                InjectIfPresent(inputs, "octree_resolution", s.octreeResolution);
                InjectIfPresent(inputs, "max_faces", s.maxFaces);
                InjectIfPresent(inputs, "remove_background", s.removeBackground);
            }
            if (inputs.ContainsKey("save_path"))
            {
                string currentPath = inputs["save_path"].ToString();
                if(currentPath.Contains("Mesh_")) 
                    inputs["save_path"] = $"Mesh_{seed}.glb";
            }
            if (inputs.ContainsKey("seed")) inputs["seed"] = seed;
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
    }
}
#endif