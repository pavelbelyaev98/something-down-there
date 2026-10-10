using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace SomethingDownThere
{
    // The title screen's backdrop: the salvage crane over the fenced dig site with the cliffs
    // behind, framed in the right two thirds so the logo and menu have the left. Like the
    // graphics test's survey, the pose is swapped onto the view camera only while it renders,
    // so the player (and the position a New Game saves) never moves. A shallow depth of field
    // softens the cliffs behind the crane. Off while the graphics test measures its own view.
    public sealed class TitleView : IDisposable
    {
        private static readonly Vector3 Position = new Vector3(-30f, 1.5f, 22f);
        private static readonly Vector3 LookAt = new Vector3(15f, 12f, 0f);
        private const float FieldOfView = 50f;

        private readonly FpsPlayer player;
        private readonly GameObject volumeRoot;
        private readonly Volume volume;
        private readonly VolumeProfile profile;
        private bool active, swapped;
        private Vector3 savedPosition;
        private Quaternion savedRotation;
        private float savedFov;

        public TitleView(Transform parent, FpsPlayer player)
        {
            this.player = player;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.hideFlags = HideFlags.DontSave;
            var focus = profile.Add<DepthOfField>();
            focus.mode.Override(DepthOfFieldMode.Gaussian);
            focus.gaussianStart.Override(60f);
            focus.gaussianEnd.Override(170f);
            focus.gaussianMaxRadius.Override(1.5f);
            focus.highQualitySampling.Override(true);
            volumeRoot = new GameObject("Title view volume");
            volumeRoot.transform.SetParent(parent, false);
            volume = volumeRoot.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100;
            volume.sharedProfile = profile;
            volume.weight = 0;
            RenderPipelineManager.beginCameraRendering += Begin;
            RenderPipelineManager.endCameraRendering += End;
        }

        public void Tick()
        {
            active = player.Persistence != null && player.Persistence.AwaitingGameChoice && !player.GraphicsTuner.Running;
            volume.weight = active ? 1 : 0;
        }

        private void Begin(ScriptableRenderContext context, Camera rendering)
        {
            var camera = player.ViewCamera;
            if (!active || rendering != camera) return;
            var view = camera.transform;
            savedPosition = view.position; savedRotation = view.rotation; savedFov = camera.fieldOfView;
            view.SetPositionAndRotation(Position, Quaternion.LookRotation(LookAt - Position));
            camera.fieldOfView = FieldOfView;
            swapped = true;
        }

        private void End(ScriptableRenderContext context, Camera rendering)
        {
            if (!swapped || rendering != player.ViewCamera) return;
            rendering.transform.SetPositionAndRotation(savedPosition, savedRotation);
            rendering.fieldOfView = savedFov;
            swapped = false;
        }

        public void Dispose()
        {
            RenderPipelineManager.beginCameraRendering -= Begin;
            RenderPipelineManager.endCameraRendering -= End;
            Object.Destroy(volumeRoot);
            Object.Destroy(profile);
        }
    }
}
