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
        string templatePath = Path.Combine(Application.dataPath, TEMPLATE_PATH_RELATIVE, modelFileName);
        
        if (!File.Exists(templatePath))
        {
            onError($"Template non trovato qui: {templatePath}");
            yield break;
        }

        JObject workflow;
        try
        {
            string jsonContent = File.ReadAllText(templatePath);
            workflow = JObject.Parse(jsonContent);
        }
        catch (Exception ex)
        {
            onError($"Errore lettura JSON: {ex.Message}");
            yield break;
        }

        InjectParams(workflow, prompt, settings);

        string promptId = null;
        string url = $"http://{server}/prompt";

        onStatus($"Sending prompt to {url}...");
        
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
                onError($"Connection Error ({w.responseCode}): {w.error}\nSu: {url}"); 
                yield break; 
            }

            string res = w.downloadHandler.text;
            
            try {
                JObject jsonRes = JObject.Parse(res);
                promptId = jsonRes["prompt_id"]?.ToString();
            } catch { }

            if (string.IsNullOrEmpty(promptId) && res.Contains("prompt_id"))
            {
                int s = res.IndexOf("\"prompt_id\":") + 12;
                while (s < res.Length && (res[s] == ' ' || res[s] == '"')) s++;
                int e = res.IndexOf("\"", s);
                if (e > s) promptId = res.Substring(s, e - s);
            }
        }

        if (string.IsNullOrEmpty(promptId)) { onError("Failed to retrieve Prompt ID."); yield break; }


        onStatus($"Rendering 2D (ID: {promptId})...");
        string fileName = null;
        string subFolder = "";
        string fileType = "output";

        for (int i = 0; i < 300; i++)
        {
            yield return new WaitForSeconds(1.0f);
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
                    else if (json.Contains("status_str\": \"error")) { onError("ComfyUI reported an error."); yield break; }
                }
            }
            onStatus($"Rendering 2D... ({i}s)");
        }

        if (string.IsNullOrEmpty(fileName)) { onError("Timeout waiting for generation."); yield break; }


        yield return new WaitForSeconds(0.5f);
        onStatus("Downloading 2D image...");
        
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
                byte[] data = w.downloadHandler.data;
                Texture2D tex = new Texture2D(2, 2);
                if (tex.LoadImage(data))
                {
                    tex.name = fileName;
                    if (!Directory.Exists(saveFolder)) Directory.CreateDirectory(saveFolder);
                    
                    string path = Path.Combine(saveFolder, $"Comfy_{DateTime.Now.Ticks}.png");
                    File.WriteAllBytes(path, data);
                    AssetDatabase.Refresh();
                    
                    onStatus($"✅ Saved to {path}");
                    onSuccess(tex);
                }
                else onError("Failed to decode downloaded image.");
            }
            else onError($"Download Failed ({w.responseCode}): {w.error}");
        }
    }

    private static void ParseHistoryForFileJObject(string json, string promptId, out string filename, out string subfolder, out string type)
    {
        filename = null; subfolder = ""; type = "output";
        try
        {
            JObject root = JObject.Parse(json);
            JObject outputs = (JObject)root[promptId]?["outputs"];
            if (outputs != null)
            {
                foreach (var node in outputs)
                {
                    JArray images = (JArray)node.Value["images"];
                    if (images.Count > 0)
                    {
                        filename = images[0]["filename"]?.ToString();
                        subfolder = images[0]["subfolder"]?.ToString();
                        type = images[0]["type"]?.ToString();
                        return;
                    }
                }
            }
        }
        catch {}
    }

    private static void InjectParams(JObject workflow, string prompt, Comfy2DParams s)
    {

        foreach (var item in workflow)
        {
            JToken node = item.Value;
            string title = node["_meta"]?["title"]?.ToString();
            JObject inputs = (JObject)node["inputs"];
            
            if (inputs == null) continue;

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
                InjectIfPresent(inputs, "guidance", s.cfg);     //esistono nodi che usano guidance al posto di cfg
            }
        }
    }

    private static void InjectIfPresent(JObject inputs, string key, object value)
    {
        if (inputs.ContainsKey(key))
        {
            if (value is int i && i > 0) inputs[key] = i;
            else if (value is float f && f > 0) inputs[key] = f;
        }
    }
}
#endif