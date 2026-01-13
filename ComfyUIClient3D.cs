#if UNITY_EDITOR
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using UnityEditor;

public static class ComfyUIClient3D
{
    public static IEnumerator Generate(string server, byte[] imageBytes, Comfy3DParams settings, string saveFolder, Action<string> onStatus, Action<string, Texture2D> onSuccess, Action<string> onError)
    {

        int seed = UnityEngine.Random.Range(0, 999999999);
        string uploadName = $"unity_input_{seed}.png";
        
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
            if (w.result != UnityWebRequest.Result.Success) { onError($"Upload Failed: {w.error}"); yield break; }
        }

        onStatus("Sending Workflow...");
        
        string jsonPayload = GetJSONTemplate(uploadName, seed, settings);
        
        using (var w = new UnityWebRequest($"http://{server}/prompt", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes($"{{\"prompt\":{jsonPayload}}}");
            w.uploadHandler = new UploadHandlerRaw(bodyRaw);
            w.downloadHandler = new DownloadHandlerBuffer();
            w.SetRequestHeader("Content-Type", "application/json");
            w.useHttpContinue = false;

            var op = w.SendWebRequest();
            while (!op.isDone) yield return null;
            if (w.result != UnityWebRequest.Result.Success) { onError($"Conn Error: {w.error}"); yield break; }

            string res = w.downloadHandler.text;
            if (res.Contains("prompt_id"))
            {
                promptId = ExtractJsonValueSimple(res, "prompt_id");
            }
        }
        
        if (string.IsNullOrEmpty(promptId)) { onError("Failed to get Prompt ID"); yield break; }

        bool isReady = false;
        onStatus($"Processing 3D (ID: {promptId})...");
        
        for (int i = 0; i < 600; i++) 
        {
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup < start + 1.0f) yield return null; 

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
                            onError("ComfyUI Error during generation.");
                            yield break;
                        }
                        
                        isReady = true;
                        break; 
                    }
                }
            }
            if (i % 2 == 0) onStatus($"Generating Model... ({i}s)");
        }

        if (!isReady) { onError("Timeout: Generation took too long."); yield break; }


        onStatus($"Downloading {expectedFileName}...");
        
        string url = $"http://{server}/view?filename={expectedFileName}&type=output";

        using (var w = UnityWebRequest.Get(url))
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

    private static string ExtractJsonValueSimple(string source, string key)
    {
        string searchKey = $"\"{key}\":";
        int idx = source.IndexOf(searchKey);
        if (idx == -1) return null;
        int start = source.IndexOf("\"", idx + searchKey.Length) + 1;
        int end = source.IndexOf("\"", start);
        return source.Substring(start, end - start);
    }

    private static string GetJSONTemplate(string filename, int seed, Comfy3DParams p)
    {
        string json = @"
        {
        ""3"": {
            ""inputs"": {
                ""subfolder"": ""hunyuan3d-dit-v2-1""
            },
            ""class_type"": ""[Comfy3D] Load Hunyuan3D 21 ShapeGen Pipeline""
        },
        ""4"": {
            ""inputs"": {
                ""seed"": %SEED%,
                ""steps"": %STEPS%,
                ""guidance_scale"": %GUIDANCE%,
                ""octree_resolution"": %OCTREE%,
                ""remove_background"": %REMOVEBG%,
                ""auto_cleanup"": true,
                ""max_faces"": %MAXFACES%,
                ""shapegen_pipe"": [ ""3"", 0 ],
                ""image"": [ ""6"", 0 ]
            },
            ""class_type"": ""[Comfy3D] Hunyuan3D 21 ShapeGen""
        },
        ""6"": {
            ""inputs"": {
                ""image"": ""%FILENAME%""
            },
            ""class_type"": ""LoadImage""
        },
        ""13"": {
            ""inputs"": {
                ""save_path"": ""Mesh_%SEED%.glb"",
                ""use_fastmesh"": true,
                ""mesh"": [ ""4"", 0 ]
            },
            ""class_type"": ""[Comfy3D] Save 3D Mesh""
        },
        ""23"": {
            ""inputs"": {
                ""offload_model"": true,
                ""offload_cache"": true,
                ""anything"": [ ""13"", 0 ]
            },
            ""class_type"": ""VRAMCleanup""
        },
        ""24"": {
            ""inputs"": {
                ""clean_file_cache"": true,
                ""clean_processes"": true,
                ""clean_dlls"": true,
                ""retry_times"": 3,
                ""anything"": [ ""13"", 0 ]
            },
            ""class_type"": ""RAMCleanup""
        }
        }";

        return json
            .Replace("%FILENAME%", filename)
            .Replace("%SEED%", seed.ToString())
            .Replace("%STEPS%", p.steps.ToString())
            .Replace("%GUIDANCE%", p.guidance.ToString("F1").Replace(',', '.')) 
            .Replace("%OCTREE%", p.octreeResolution.ToString())
            .Replace("%MAXFACES%", p.maxFaces.ToString())
            .Replace("%REMOVEBG%", p.removeBackground.ToString().ToLower());
    }
}
#endif