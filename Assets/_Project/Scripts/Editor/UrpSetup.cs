using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HollowTide.EditorTools
{
    /// <summary>Cria o URP Asset + Renderer 2D e os ativa em Graphics e Quality Settings.</summary>
    static class UrpSetup
    {
        const string Dir = "Assets/_Project/Settings";
        const string RendererPath = Dir + "/Renderer2D.asset";
        const string PipelinePath = Dir + "/URP2D.asset";

        public static void Ensure()
        {
            try
            {
                Directory.CreateDirectory(Dir);

                var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
                if (pipeline == null)
                {
                    var renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>(RendererPath);
                    if (renderer == null)
                    {
                        renderer = ScriptableObject.CreateInstance<Renderer2DData>();
                        AssetDatabase.CreateAsset(renderer, RendererPath);
                    }
                    pipeline = UniversalRenderPipelineAsset.Create(renderer);
                    AssetDatabase.CreateAsset(pipeline, PipelinePath);
                }

                GraphicsSettings.defaultRenderPipeline = pipeline;
                int current = QualitySettings.GetQualityLevel();
                for (int i = 0; i < QualitySettings.names.Length; i++)
                {
                    QualitySettings.SetQualityLevel(i, false);
                    QualitySettings.renderPipeline = pipeline;
                }
                QualitySettings.SetQualityLevel(current, false);

                AssetDatabase.SaveAssets();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Build Demo] Não consegui configurar o URP 2D automaticamente: " + e.Message +
                                 "\nManual: Assets > Create > Rendering > URP Asset (with 2D Renderer) e atribua em Project Settings > Graphics.");
            }
        }
    }
}
