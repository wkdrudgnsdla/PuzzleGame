using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PuzzleGame
{
    public sealed class DimensionRippleRendererFeature : FullScreenPassRendererFeature
    {
        private static readonly int Progress = Shader.PropertyToID("_DimensionRippleProgress");
        private static readonly int Direction = Shader.PropertyToID("_DimensionRippleDirection");

        [SerializeField] private Shader rippleShader;
        private Material rippleMaterial;
        private bool passCreated;

        public override void Create()
        {
            ReleasePass(true);
            if (rippleShader == null) return;

            rippleMaterial = CoreUtils.CreateEngineMaterial(rippleShader);
            passMaterial = rippleMaterial;
            injectionPoint = InjectionPoint.AfterRenderingPostProcessing;
            fetchColorBuffer = true;
            base.Create();
            passCreated = true;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            if (!passCreated || camera.cameraType != CameraType.Game ||
                !DimensionRipple.TryGet(camera, out float progress, out float direction)) return;

            rippleMaterial.SetFloat(Progress, progress);
            rippleMaterial.SetFloat(Direction, direction);
            base.AddRenderPasses(renderer, ref renderingData);
        }

        protected override void Dispose(bool disposing) => ReleasePass(disposing);

        private void ReleasePass(bool disposing)
        {
            if (!passCreated) return;
            base.Dispose(disposing);
            CoreUtils.Destroy(rippleMaterial);
            rippleMaterial = null;
            passMaterial = null;
            passCreated = false;
        }
    }
}
