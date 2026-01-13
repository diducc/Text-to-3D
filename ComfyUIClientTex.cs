#if UNITY_EDITOR
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using UnityEditor;

public static class ComfyUIClientTex
{
    public static IEnumerator Generate(string server, byte[] imageBytes, string localMeshPath, ComfyTexGenParams settings, string saveFolder, Action<string> onStatus, Action<string, Texture2D> onSuccess, Action<string> onError)
    {
        int seed = UnityEngine.Random.Range(0, 999999999);
        string meshFileName = Path.GetFileName(localMeshPath);
        string uploadImgName = $"tex_source_{seed}.png";
        
        string expectedOutputName = $"Textured_{seed}.glb"; 
        string promptId = null;

        onStatus("Uploading Source Image...");
        yield return UploadFileCoroutine(server, imageBytes, uploadImgName, "image/png", onError);

        onStatus("Uploading Mesh...");
        byte[] meshBytes = File.ReadAllBytes(localMeshPath);
        yield return UploadFileCoroutine(server, meshBytes, meshFileName, "application/octet-stream", onError);

        onStatus("Sending Paint Workflow...");
        
        string jsonPayload = GetJSONTemplate(uploadImgName, meshFileName, seed, settings, expectedOutputName);
        
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
            if (res.Contains("prompt_id")) promptId = ExtractJsonValueSimple(res, "prompt_id");
        }

        if (string.IsNullOrEmpty(promptId)) { onError("No Prompt ID"); yield break; }

        bool isReady = false;
        onStatus($"Texturing (ID: {promptId})...");
        for (int i = 0; i < 600; i++) 
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
                    if (json.Contains($"\"{promptId}\"")) 
                    {
                        if (json.Contains("error")) { onError("ComfyUI Error"); yield break; }
                        isReady = true; break; 
                    }
                }
            }
            onStatus($"Painting... ({i}s)");
        }
        if (!isReady) { onError("Timeout"); yield break; }

        onStatus("Downloading Textured Model...");
        string url = $"http://{server}/view?filename={expectedOutputName}&type=output";
        
        using (var w = UnityWebRequest.Get(url))
        {
            w.downloadHandler = new DownloadHandlerBuffer();
            w.useHttpContinue = false;
            var op = w.SendWebRequest();
            while (!op.isDone) yield return null;

            if (w.result == UnityWebRequest.Result.Success)
            {
                if (!Directory.Exists(saveFolder)) Directory.CreateDirectory(saveFolder);
                string finalPath = $"{saveFolder}/{expectedOutputName}";
                File.WriteAllBytes(finalPath, w.downloadHandler.data);
                AssetDatabase.Refresh();
                
                int waitFrames = 0;
                GameObject model = null;
                while (waitFrames < 50) 
                {
                    model = AssetDatabase.LoadAssetAtPath<GameObject>(finalPath);
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
                
                onSuccess(finalPath, previewTex);
            }
            else onError($"Download Fail: {w.error}");
        }
    }

    private static IEnumerator UploadFileCoroutine(string server, byte[] data, string filename, string mimeType, Action<string> onError)
    {
        WWWForm form = new WWWForm();
        form.AddBinaryData("image", data, filename, mimeType);
        form.AddField("overwrite", "true");

        using (var w = UnityWebRequest.Post($"http://{server}/upload/image", form))
        {
            w.useHttpContinue = false;
            var op = w.SendWebRequest();
            while (!op.isDone) yield return null;
            if (w.result != UnityWebRequest.Result.Success) 
            {
                onError($"Upload {filename} Failed: {w.error}");
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

    private static string GetJSONTemplate(string imgName, string meshName, int seed, ComfyTexGenParams p, string outputName)
    {
        
        string json = @"
        {
        ""2"": {
            ""inputs"": {
                ""max_num_view"": %MAXVIEWS%,
                ""resolution"": %RES%,
                ""enable_mmgp"": %MMGP%
            },
            ""class_type"": ""[Comfy3D] Load Hunyuan3D 21 TexGen Pipeline""
        },
        ""3"": {
                ""inputs"": {
                ""mesh_path"": ""/basedir/output/%MESHNAME%"",
                ""create_pbr"": %PBR%,
                ""use_remesh"": false,
                ""texgen_pipe"": [ ""2"", 0 ],
                ""image"": [ ""5"", 0 ]
            },
            ""class_type"": ""[Comfy3D] Hunyuan3D 21 TexGen""
        },
        ""5"": {
            ""inputs"": {
                ""image"": ""%IMGNAME%""
            },
            ""class_type"": ""LoadImage""
        },
        ""6"": {
            ""inputs"": {
                ""save_path"": ""%OUTPUTNAME%"",
                ""use_fastmesh"": true,
                ""mesh"": [ ""3"", 0 ]
            },
            ""class_type"": ""[Comfy3D] Save 3D Mesh""
        },
        ""8"": {
            ""inputs"": { ""clean_file_cache"": true, ""clean_processes"": true, ""clean_dlls"": true, ""retry_times"": 5, ""anything"": [ ""6"", 0 ] },
            ""class_type"": ""RAMCleanup""
        },
        ""9"": {
            ""inputs"": { ""offload_model"": true, ""offload_cache"": true, ""anything"": [ ""6"", 0 ] },
            ""class_type"": ""VRAMCleanup""
        }
        }";

        return json
            .Replace("%IMGNAME%", imgName)
            .Replace("%MESHNAME%", meshName)
            .Replace("%OUTPUTNAME%", outputName)
            .Replace("%MAXVIEWS%", p.maxNumViews.ToString())
            .Replace("%RES%", p.resolution.ToString())
            .Replace("%PBR%", p.createPBR.ToString().ToLower())
            .Replace("%MMGP%", p.enableMMGP.ToString().ToLower());
    }
}
#endif