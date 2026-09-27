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
            Application.targetFrameRate = DisplayRefreshRate();
        }
        // The game pauses without focus; its window need not redraw faster than this.
        public const int BackgroundFrameLimit = 30;
        // Renderer 1 is renderer 0 without contact shading (GraphicsQualitySetup keeps them in step).
        public const int WithoutContactShadingRenderer = 1;
        private readonly bool applyToSystem;
        private readonly UniversalAdditionalCameraData cameraData;
        private readonly AntialiasingMode originalCameraAntialiasing;
        private readonly TerrainQualityOverrides originalTerrainOverrides;
        private readonly float originalDetailDistance, originalDetailDensity, originalBasemapDistance, originalPixelError, originalMeshLodThreshold;
        private readonly RenderPipelineAsset originalPipeline;
        private readonly UniversalRenderPipelineAsset pipeline;
        private readonly int authoredShadowResolution, authoredShadowCascades;
        private readonly float authoredShadowDistance;
        private readonly int originalVSync, originalFrameLimit, originalTextures;
        private readonly AnisotropicFiltering originalFiltering;
        private readonly float originalVolume;
        private readonly Light sun;
        private readonly LightShadows originalSunShadows;
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
        public int RefreshRate => DisplayRefreshRate();

        // The monitor showing the window, or the desktop mode before a window exists.
        internal static int DisplayRefreshRate()
        {
            double rate = Screen.mainWindowDisplayInfo.refreshRate.value;
            if (!(rate >= 30 && rate <= 1000)) rate = Screen.currentResolution.refreshRateRatio.value;
            return rate >= 30 && rate <= 1000 ? Mathf.RoundToInt((float)rate) : GamePreferences.FallbackFrameLimit;
        }

        public UnityGameSettingsPlatform(bool applyToSystem = true, Camera camera = null)
        {
            this.applyToSystem = applyToSystem;
            editorDisplay = new DisplaySelection(Mathf.Max(960, Screen.width), Mathf.Max(540, Screen.height), 0);
            Resolutions = DesktopWindow.ResolutionOptions(Screen.resolutions.Select(r => new Vector2Int(r.width, r.height)),
                new Vector2Int(NativeDisplay.Width, NativeDisplay.Height), new Vector2Int(Screen.width, Screen.height));
            if (!applyToSystem) return;
            originalVSync = QualitySettings.vSyncCount; originalFrameLimit = Application.targetFrameRate;
            originalTextures = QualitySettings.globalTextureMipmapLimit; originalFiltering = QualitySettings.anisotropicFiltering;
            originalVolume = AudioListener.volume;
            sun = RenderSettings.sun; originalSunShadows = sun != null ? sun.shadows : LightShadows.None;
            if (camera != null && camera.TryGetComponent(out cameraData)) originalCameraAntialiasing = cameraData.antialiasing;
            originalTerrainOverrides = QualitySettings.terrainQualityOverrides;
            originalDetailDistance = QualitySettings.terrainDetailDistance; originalDetailDensity = QualitySettings.terrainDetailDensityScale;
            originalBasemapDistance = QualitySettings.terrainBasemapDistance; originalPixelError = QualitySettings.terrainPixelError;
            originalMeshLodThreshold = QualitySettings.meshLodThreshold;
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
            bool background = !focused && !Application.isEditor;
            QualitySettings.vSyncCount = values.VSync && !background ? 1 : 0;
            Application.targetFrameRate = background ? BackgroundFrameLimit : values.VSync ? -1
                : values.FrameLimit == GamePreferences.DisplayFrameLimit ? DisplayRefreshRate() : values.FrameLimit;
            if (QualitySettings.globalTextureMipmapLimit != values.TextureLimit) QualitySettings.globalTextureMipmapLimit = values.TextureLimit;
            QualitySettings.anisotropicFiltering = (AnisotropicFiltering)values.Filtering;
            AudioListener.volume = values.MasterVolume / 100f;
            if (pipeline != null)
                ApplyRendering(values, pipeline, cameraData, sun, originalSunShadows,
                    authoredShadowResolution, authoredShadowCascades, authoredShadowDistance);
        }

        // Every presentation choice goes through global pipeline, camera and quality state, so
        // content added later inherits it without per-object setup. Shared with the benchmark.
        internal static void ApplyRendering(GamePreferenceValues values, UniversalRenderPipelineAsset pipeline,
            UniversalAdditionalCameraData cameraData, Light sun, LightShadows authoredSun,
            int authoredResolution, int authoredCascades, float authoredDistance)
        {
            float scale = values.RenderScale / 100f;
            if (!Mathf.Approximately(pipeline.renderScale, scale)) pipeline.renderScale = scale;
            pipeline.upscalingFilter = values.RenderScale < 100 ? UpscalingFilterSelection.FSR : UpscalingFilterSelection.Auto;
            int samples = GraphicsQuality.MsaaSamples(values.AntiAliasing);
            if (pipeline.msaaSampleCount != samples) pipeline.msaaSampleCount = samples;
            if (cameraData != null)
            {
                cameraData.antialiasing = values.AntiAliasing == GraphicsQuality.Fxaa
                    ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.None;
                cameraData.SetRenderer(values.AmbientOcclusion || RendererCount(pipeline) <= WithoutContactShadingRenderer
                    ? -1 : WithoutContactShadingRenderer);
            }
            ApplySunShadows(pipeline, sun, values.Shadows, authoredSun, authoredResolution, authoredCascades, authoredDistance);
            ApplyViewDistance(values.ViewDistance);
        }

        private static int RendererCount(UniversalRenderPipelineAsset pipeline) => pipeline.rendererDataList.Length;

        // Global terrain overrides reach every Terrain. High keeps the authored grass distances,
        // which cover the whole play area, and shades all visible terrain in full (no blurry
        // basemap that sharpens as the player approaches); lower tiers shorten grass and terrain
        // detail. The Mesh LOD threshold does the same for every mesh with generated detail
        // levels (finds): lower tiers switch to simpler levels sooner.
        internal static void ApplyViewDistance(int level)
        {
            QualitySettings.meshLodThreshold = GraphicsQuality.MeshLodThreshold(level);
            QualitySettings.terrainBasemapDistance = GraphicsQuality.BasemapDistance(level);
            if (level >= GraphicsQuality.ViewHigh) { QualitySettings.terrainQualityOverrides = TerrainQualityOverrides.BasemapDistance; return; }
            bool low = level <= GraphicsQuality.ViewLow;
            QualitySettings.terrainDetailDistance = low ? 70 : 120;
            QualitySettings.terrainDetailDensityScale = low ? .6f : .8f;
            QualitySettings.terrainPixelError = 10;
            QualitySettings.terrainQualityOverrides = TerrainQualityOverrides.DetailDistance | TerrainQualityOverrides.DetailDensity
                | TerrainQualityOverrides.BasemapDistance | (low ? TerrainQualityOverrides.PixelError : TerrainQualityOverrides.None);
        }

        // Shared with the environment benchmark so its isolated renderer matches a preference tier.
        internal static void ApplySunShadows(UniversalRenderPipelineAsset pipeline, Light sun, int shadows, LightShadows authoredSun,
            int authoredResolution, int authoredCascades, float authoredDistance)
        {
            // Local occlusion is gameplay: a work lamp must not shine through sealed soil.
            // Only sun shadow quality is optional; reserve the short lamp shadow range.
            if (sun != null) sun.shadows = shadows == 0 ? LightShadows.None : authoredSun;
            pipeline.shadowDistance = Mathf.Max(WorksiteTools.LightCullDistance + WorksiteTools.LightRange,
                shadows == 0 ? 0 : shadows == 3 ? authoredDistance : Mathf.Min(authoredDistance, shadows == 1 ? 25f : 35f));
            pipeline.mainLightShadowmapResolution = shadows == 3 ? authoredResolution
                : Mathf.Min(authoredResolution, shadows <= 1 ? 1024 : Mathf.Max(1024, authoredResolution / 2));
            pipeline.shadowCascadeCount = shadows == 3 ? authoredCascades
                : Mathf.Min(authoredCascades, shadows <= 1 ? 1 : 2);
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
            if (sun != null) sun.shadows = originalSunShadows;
            if (cameraData != null) { cameraData.antialiasing = originalCameraAntialiasing; cameraData.SetRenderer(-1); }
            QualitySettings.terrainQualityOverrides = originalTerrainOverrides;
            QualitySettings.terrainDetailDistance = originalDetailDistance; QualitySettings.terrainDetailDensityScale = originalDetailDensity;
            QualitySettings.terrainBasemapDistance = originalBasemapDistance; QualitySettings.terrainPixelError = originalPixelError;
            QualitySettings.meshLodThreshold = originalMeshLodThreshold;
            if (pipeline != null)
            {
                if (QualitySettings.renderPipeline == pipeline) QualitySettings.renderPipeline = originalPipeline;
                Object.Destroy(pipeline);
            }
        }
    }
}
