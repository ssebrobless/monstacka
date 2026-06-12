using UnityEngine;
using UnityEngine.UI;

namespace MonStacka.UI
{
    [RequireComponent(typeof(Camera))]
    public sealed class ArtboardViewportController : MonoBehaviour
    {
        [SerializeField] private Vector2 referenceResolution = new(1920f, 1080f);
        [SerializeField] private Canvas[] canvases = new Canvas[0];

        private Camera targetCamera;
        private float baseOrthographicSize;
        private int lastScreenWidth = -1;
        private int lastScreenHeight = -1;

        private void Awake()
        {
            targetCamera = GetComponent<Camera>();
            baseOrthographicSize = targetCamera.orthographicSize;
            Apply(force: true);
        }

        private void LateUpdate()
        {
            Apply(force: false);
        }

        private void Apply(bool force)
        {
            if (!force && lastScreenWidth == Screen.width && lastScreenHeight == Screen.height)
            {
                return;
            }

            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;

            var targetAspect = referenceResolution.x / Mathf.Max(1f, referenceResolution.y);
            var currentAspect = Screen.width / Mathf.Max(1f, (float)Screen.height);
            targetCamera.orthographicSize = baseOrthographicSize;
            if (currentAspect >= targetAspect)
            {
                var viewportWidth = targetAspect / currentAspect;
                targetCamera.rect = new Rect((1f - viewportWidth) * 0.5f, 0f, viewportWidth, 1f);
            }
            else
            {
                var viewportHeight = currentAspect / targetAspect;
                targetCamera.rect = new Rect(0f, (1f - viewportHeight) * 0.5f, 1f, viewportHeight);
            }

            foreach (var canvas in canvases)
            {
                if (!canvas)
                {
                    continue;
                }

                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = targetCamera;
                canvas.planeDistance = 1f;

                if (canvas.TryGetComponent<CanvasScaler>(out var scaler))
                {
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = referenceResolution;
                    scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                    scaler.matchWidthOrHeight = 0.5f;
                }
            }
        }
    }
}
