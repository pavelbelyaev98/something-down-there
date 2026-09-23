using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SomethingDownThere
{
    public sealed class UnityGameSettingsPlatform : IGameSettingsPlatform
    {
        // Bound startup/loading before FpsPlayer can read device preferences. An explicit
        // saved FPS limit or VSync selection takes over through Apply as usual.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LimitStartupFrames()
        {
            if (Application.isEditor) return;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = GamePreferences.DefaultFrameLimit;
        }
        private readonly bool applyToSystem;
        private readonly RenderPipelineAsset originalPipeline;
        private readonly UniversalRenderPipelineAsset pipeline;
        private readonly int authoredShadowResolution, authoredShadowCascades;
        private readonly float authoredShadowDistance;
        private readonly int originalVSync, originalFrameLimit, originalTextures;
        private readonly AnisotropicFiltering originalFiltering;
        private readonly float originalVolume;
        private DisplaySelection editorDisplay;
        public DisplaySelection CurrentDisplay => Application.isEditor ? editorDisplay
            : new DisplaySelection(Screen.width, Screen.height, Mode(Screen.fullScreenMode));
        public Vector2Int[] Resolutions { get; }
        public DisplaySelection NativeDisplay
        {
            get
            {
                var monitor = Screen.mainWindowDisplayInfo;
                return DesktopWindow.RecommendedDisplay(monitor.width, monitor.height);
            }
        }
        public bool RenderingAvailable => !applyToSystem || pipeline != null;

        public UnityGameSettingsPlatform(bool applyToSystem = true)
        {
            this.applyToSystem = applyToSystem;
            editorDisplay = new DisplaySelection(Mathf.Max(960, Screen.width), Mathf.Max(540, Screen.height), 0);
            Resolutions = DesktopWindow.ResolutionOptions(Screen.resolutions.Select(r => new Vector2Int(r.width, r.height)),
                new Vector2Int(NativeDisplay.Width, NativeDisplay.Height), new Vector2Int(Screen.width, Screen.height));
            if (!applyToSystem) return;
            originalVSync = QualitySettings.vSyncCount; originalFrameLimit = Application.targetFrameRate;
            originalTextures = QualitySettings.globalTextureMipmapLimit; originalFiltering = QualitySettings.anisotropicFiltering;
            originalVolume = AudioListener.volume;
            originalPipeline = QualitySettings.renderPipeline;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset source)
            {
                authoredShadowResolution = source.mainLightShadowmapResolution;
                authoredShadowCascades = source.shadowCascadeCount;
                authoredShadowDistance = source.shadowDistance;
                pipeline = Object.Instantiate(source);
                pipeline.name = source.name + " (player settings)";
                pipeline.hideFlags = HideFlags.DontSave;
                QualitySettings.renderPipeline = pipeline;
            }
        }

        public void Apply(GamePreferenceValues values, bool focused)
        {
            if (!applyToSystem) return;
            QualitySettings.vSyncCount = values.VSync ? 1 : 0;
            Application.targetFrameRate = values.VSync ? -1 : values.FrameLimit;
            if (QualitySettings.globalTextureMipmapLimit != values.TextureLimit) QualitySettings.globalTextureMipmapLimit = values.TextureLimit;
            QualitySettings.anisotropicFiltering = (AnisotropicFiltering)values.Filtering;
            AudioListener.volume = values.MasterVolume / 100f;
            if (pipeline != null)
            {
                if (!Mathf.Approximately(pipeline.renderScale, 1f)) pipeline.renderScale = 1f;
                if (pipeline.msaaSampleCount != values.Msaa) pipeline.msaaSampleCount = values.Msaa;
                int shadows = values.Shadows;
                pipeline.shadowDistance = shadows == 0 ? 0 : shadows == 3 ? authoredShadowDistance
                    : Mathf.Min(authoredShadowDistance, shadows == 1 ? 25f : 35f);
                pipeline.mainLightShadowmapResolution = shadows == 3 ? authoredShadowResolution
                    : Mathf.Min(authoredShadowResolution, shadows <= 1 ? 1024 : 2048);
                pipeline.shadowCascadeCount = shadows == 3 ? authoredShadowCascades
                    : Mathf.Min(authoredShadowCascades, shadows <= 1 ? 1 : 2);
            }
        }

        public void SetDisplay(DisplaySelection selection)
        {
            if (selection.Mode == 0) selection = NativeDisplay;
            editorDisplay = selection;
            if (applyToSystem && !Application.isEditor)
                Screen.SetResolution(selection.Width, selection.Height, selection.Mode == 0 ? FullScreenMode.FullScreenWindow
                    : selection.Mode == 1 ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.Windowed);
        }

        private static int Mode(FullScreenMode mode) => mode == FullScreenMode.ExclusiveFullScreen ? 1 : mode == FullScreenMode.Windowed ? 2 : 0;

        public void Dispose()
        {
            if (!applyToSystem) return;
            QualitySettings.vSyncCount = originalVSync; Application.targetFrameRate = originalFrameLimit;
            QualitySettings.globalTextureMipmapLimit = originalTextures; QualitySettings.anisotropicFiltering = originalFiltering;
            AudioListener.volume = originalVolume;
            if (pipeline != null)
            {
                if (QualitySettings.renderPipeline == pipeline) QualitySettings.renderPipeline = originalPipeline;
                Object.Destroy(pipeline);
            }
        }
    }
}
