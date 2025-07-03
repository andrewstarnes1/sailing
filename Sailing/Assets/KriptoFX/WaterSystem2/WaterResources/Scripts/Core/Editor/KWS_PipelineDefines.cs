#if UNITY_EDITOR
using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace KWS
{
    [InitializeOnLoad]
    public static class KWS_PipelineDefines
    {
#if KWS_DEBUG
        [InitializeOnLoadMethod]
        static void Init()
        {
           
            EditorApplication.update -= CheckPipelineChange;
            EditorApplication.update += CheckPipelineChange;

        }

        static void CheckPipelineChange()
        {
            var currentRP = GraphicsSettings.currentRenderPipeline;
            if (currentRP != _lastPipelineAsset)
            {
                _lastPipelineAsset = currentRP;
                UpdatePipelineDefine();
            }
        }
#endif
        
        //
        // [UnityEditor.Callbacks.DidReloadScripts]
        // private static void OnScriptsReloaded()
        // {
        //     CheckAndUpdateShaderPipelineDefines(); 
        // }

        private static RenderPipelineAsset _lastPipelineAsset;

        static KWS_PipelineDefines()
        {
            UpdatePipelineDefine();
            
        }


        static void UpdatePipelineDefine()
        {
            var group = BuildTargetGroup.Standalone;
            string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);

            defines = Remove(defines, "KWS_BUILTIN");
            defines = Remove(defines, "KWS_URP");
            defines = Remove(defines, "KWS_HDRP");

            KWS_EditorUtils.DisableAllShaderTextDefines(KWS_Settings.ShaderPaths.KWS_WaterDefines, lockFile:true, "KWS_BUILTIN", "KWS_URP", "KWS_HDRP");

            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;

            if (pipeline == null)
            {
                defines += ";KWS_BUILTIN";
                KWS_EditorUtils.SetShaderTextDefine(KWS_Settings.ShaderPaths.KWS_WaterDefines, lockFile: true, "KWS_BUILTIN", true);
                Debug.Log("KWS2 pipeline changed to Built-in");
                
            }
            else
            {
                string rpName = pipeline.GetType().ToString();

                if (rpName.Contains("UniversalRenderPipelineAsset") || rpName.Contains("UniversalRenderPipeline"))
                {
                    defines += ";KWS_URP";
                    Debug.Log("KWS2 pipeline changed to URP");
                    KWS_EditorUtils.SetShaderTextDefine(KWS_Settings.ShaderPaths.KWS_WaterDefines, lockFile: true, "KWS_URP", true);
                }
                else if (rpName.Contains("HDRenderPipelineAsset"))
                {
                    defines += ";KWS_HDRP";
                    Debug.Log("KWS2 pipeline changed to HDRP");
                    KWS_EditorUtils.SetShaderTextDefine(KWS_Settings.ShaderPaths.KWS_WaterDefines, lockFile: true, "KWS_HDRP", true);
                }
                else
                {
                    Debug.LogError("KWS2 Water Unknown RenderPipeline: " + rpName);
                }
            }

            _lastPipelineAsset = pipeline;
            PlayerSettings.SetScriptingDefineSymbolsForGroup(group, defines);

           // CheckAndUpdateShaderPipelineDefines();
            AssetDatabase.Refresh();


        }

        static  void CheckAndUpdateShaderPipelineDefines()
        {
            var shaderPipelineDefine = GetActivePipelineDefine(KWS_Settings.ShaderPaths.KWS_WaterDefines);
            #if KWS_BUILTIN
                if(shaderPipelineDefine == string.Empty || shaderPipelineDefine != "KWS_BUILTIN")
                {
                      KWS_EditorUtils.SetShaderTextDefine(KWS_Settings.ShaderPaths.KWS_WaterDefines, lockFile: true, "KWS_BUILTIN", true);
                }
            #endif
            
            #if KWS_URP
            {
                if(shaderPipelineDefine == string.Empty || shaderPipelineDefine != "KWS_URP")
                {
                    KWS_EditorUtils.SetShaderTextDefine(KWS_Settings.ShaderPaths.KWS_WaterDefines, lockFile: true, "KWS_URP", true);
                }
            }
            #endif
            
            #if KWS_HDRP
            {
                if(shaderPipelineDefine == string.Empty || shaderPipelineDefine != "KWS_HDRP")
                {
                    KWS_EditorUtils.SetShaderTextDefine(KWS_Settings.ShaderPaths.KWS_WaterDefines, lockFile: true, "KWS_HDRP", true);
                }
            }
            #endif
            
            
        }
        
        public static string GetActivePipelineDefine(string shaderPath)
        {
            var pathToShadersFolder = KW_Extensions.GetPathToWaterShadersFolder();
            var fullPath            = Path.Combine(pathToShadersFolder, shaderPath);

            if (!File.Exists(fullPath))
            {
                Debug.LogError("Shader file not found: " + fullPath);
                return null;
            }

            var lines = File.ReadAllLines(fullPath);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                if (trimmed.StartsWith("//")) continue;

                if (trimmed == "#define KWS_BUILTIN") return "KWS_BUILTIN";
                if (trimmed == "#define KWS_URP")     return "KWS_URP";
                if (trimmed == "#define KWS_HDRP")    return "KWS_HDRP";
            }

            return String.Empty;
        }

        static string Remove(string input, string keyword)
        {
            return input.Replace(keyword + ";", "").Replace(";" + keyword, "").Replace(keyword, "");
        }
    }
}
#endif