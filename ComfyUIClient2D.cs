#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Text;
using System.IO;

public static class ComfyUIClient2D
{
    public static IEnumerator Generate(string server, string prompt, Comfy2DParams settings, string saveFolder, Action<string> onStatus, Action<Texture2D> onSuccess, Action<string> onError)
    {
        int seed = UnityEngine.Random.Range(0, 999999999);
        string promptId = null;

        onStatus("Sending prompt to ComfyUI (2D)...");
        using (var w = new UnityWebRequest($"http://{server}/prompt", "POST"))
        {
            string jsonPayload = GetJSONTemplate(prompt, seed, settings);
            byte[] bodyRaw = Encoding.UTF8.GetBytes($"{{\"prompt\":{jsonPayload}}}");
            w.uploadHandler = new UploadHandlerRaw(bodyRaw);
            w.downloadHandler = new DownloadHandlerBuffer();
            w.SetRequestHeader("Content-Type", "application/json");
            w.useHttpContinue = false;

            var op = w.SendWebRequest();
            while (!op.isDone) yield return null;

            if (w.result != UnityWebRequest.Result.Success) { onError($"Connection Error: {w.error}"); yield break; }

            string res = w.downloadHandler.text;
            if (res.Contains("prompt_id"))
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
                        ParseHistoryForFile(json, out fileName, out subFolder, out fileType);
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
        
        string url = $"http://{server}/view?filename={fileName}&type={fileType}";
        if (!string.IsNullOrEmpty(subFolder)) url += $"&subfolder={subFolder}";

        using (var w = UnityWebRequest.Get(url))
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
                    tex.name = "ComfyResult";
                    if (!Directory.Exists(saveFolder)) Directory.CreateDirectory(saveFolder);
                    string path = $"{saveFolder}/Comfy_{seed}.png";
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


    private static void ParseHistoryForFile(string json, out string filename, out string subfolder, out string type)
    {
        filename = null; subfolder = ""; type = "output";
        try {
            int outputsIndex = json.IndexOf("\"outputs\""); if (outputsIndex == -1) return;
            int openBracket = json.IndexOf("[", outputsIndex);
            int closeBracket = json.IndexOf("]", openBracket);
            if (openBracket == -1) return;
            string block = json.Substring(openBracket, closeBracket - openBracket);
            
            filename = ExtractValue(block, "\"filename\":");
            subfolder = ExtractValue(block, "\"subfolder\":");
            string tp = ExtractValue(block, "\"type\":");
            if (!string.IsNullOrEmpty(tp)) type = tp;
        } catch { }
    }

    private static string ExtractValue(string source, string key)
    {
        int idx = source.IndexOf(key); if (idx == -1) return null;
        int s = source.IndexOf("\"", idx + key.Length) + 1;
        int e = source.IndexOf("\"", s);
        return source.Substring(s, e - s);
    }

    // In ComfyUIClient2D.cs

private static string GetJSONTemplate(string prompt, int seed, Comfy2DParams p)
    {
        string safePrompt = prompt.Replace("\"", "\\\"").Replace("\n", " ");
        string json = @"
        {
            ""1"": { ""inputs"": { ""clip_name1"": ""clip_g.safetensors"", ""clip_name2"": ""clip_l.safetensors"", ""clip_name3"": ""t5xxl_fp16.safetensors"" }, ""class_type"": ""TripleCLIPLoader"" },
            ""2"": { ""inputs"": { ""text"": ""bad quality"", ""clip"": [ ""1"", 0 ] }, ""class_type"": ""CLIPTextEncode"" },
            ""5"": { ""inputs"": { ""width"": %WIDTH%, ""height"": %HEIGHT%, ""batch_size"": 1 }, ""class_type"": ""EmptySD3LatentImage"" },
            ""6"": { ""inputs"": { ""text"": ""%PROMPT%"", ""clip"": [ ""1"", 0 ] }, ""class_type"": ""CLIPTextEncode"" },
            ""7"": { ""inputs"": { ""seed"": %SEED%, ""steps"": %STEPS%, ""cfg"": %CFG%, ""sampler_name"": ""euler"", ""scheduler"": ""simple"", ""denoise"": 1, ""model"": [ ""10"", 0 ], ""positive"": [ ""6"", 0 ], ""negative"": [ ""2"", 0 ], ""latent_image"": [ ""5"", 0 ] }, ""class_type"": ""KSampler"" },
            ""8"": { ""inputs"": { ""samples"": [ ""7"", 0 ], ""vae"": [ ""10"", 2 ] }, ""class_type"": ""VAEDecode"" },
            ""10"": { ""inputs"": { ""ckpt_name"": ""sd3.5_large.safetensors"" }, ""class_type"": ""CheckpointLoaderSimple"" },
            ""11"": { ""inputs"": { ""anything"": [ ""8"", 0 ] }, ""class_type"": ""easy cleanGpuUsed"" },
            ""14"": { ""inputs"": { ""filename_prefix"": ""2d"", ""images"": [ ""8"", 0 ] }, ""class_type"": ""SaveImage"" },
            ""17"": { ""inputs"": { ""clean_file_cache"": true, ""clean_processes"": true, ""clean_dlls"": true, ""retry_times"": 3, ""anything"": [ ""8"", 0 ] }, ""class_type"": ""RAMCleanup"" },
            ""18"": { ""inputs"": { ""offload_model"": true, ""offload_cache"": true, ""anything"": [ ""8"", 0 ] }, ""class_type"": ""VRAMCleanup"" }
        }";

        return json
            .Replace("%PROMPT%", safePrompt)
            .Replace("%SEED%", seed.ToString())
            .Replace("%WIDTH%", p.width.ToString())
            .Replace("%HEIGHT%", p.height.ToString())
            .Replace("%STEPS%", p.steps.ToString())
            .Replace("%CFG%", p.cfg.ToString("F1").Replace(',', '.'));
    }
}
#endif