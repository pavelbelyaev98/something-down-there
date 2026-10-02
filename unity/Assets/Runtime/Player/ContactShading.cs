using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SomethingDownThere
{
    // Developer admin: the strength of the default renderer's Ground contact shading (its SSAO feature)
    // for the session. URP keeps the setting private on the shared renderer asset, so it is reached by
    // reflection (as the surface performance fixture does) and the authored strength is put back when
    // the session ends; in the Editor a play session would otherwise leave the change in the asset.
    public static class ContactShading
    {
        // Admin steps around the authored strength (GroundTextureSetup.ContactShadingIntensity, 0.3).
        public static readonly float[] Steps = { .15f, .45f, .6f };
        private static float? authored;

        private static object Settings(out FieldInfo intensity)
        {
            intensity = null;
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            var renderer = pipeline != null && pipeline.rendererDataList.Length > 0 ? pipeline.rendererDataList[0] as UniversalRendererData : null;
            var contact = renderer != null ? renderer.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().FirstOrDefault() : null;
            if (contact == null) return null;
            var settings = typeof(ScreenSpaceAmbientOcclusion).GetField("m_Settings", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(contact);
            intensity = settings?.GetType().GetField("Intensity", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return intensity != null ? settings : null;
        }

        public static float Current
        {
            get { var settings = Settings(out var intensity); return settings != null ? (float)intensity.GetValue(settings) : 0; }
        }

        public static void Set(float value)
        {
            var settings = Settings(out var intensity);
            if (settings == null) return;
            authored ??= (float)intensity.GetValue(settings);
            intensity.SetValue(settings, value);
        }

        public static void Restore()
        {
            if (!authored.HasValue) return;
            var settings = Settings(out var intensity);
            if (settings != null) intensity.SetValue(settings, authored.Value);
            authored = null;
        }
    }
}
