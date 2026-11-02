using UnityEngine;
using UnityEngine.UI;

namespace PuzzleGame
{
    /// <summary>Fits the authored HUD inside the display safe area without changing panel layouts.</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Canvas), typeof(CanvasScaler))]
    public sealed class ProjectionHudLayout : MonoBehaviour
    {
        public RectTransform SafeArea;
        public bool RespectSafeArea = true;
        private Canvas canvas;
        private CanvasScaler scaler;
        private Vector2 designResolution;
        private bool capturedResolution;

        private void OnEnable()
        {
            canvas=GetComponent<Canvas>();
            scaler=GetComponent<CanvasScaler>();
            designResolution=scaler.referenceResolution;
            capturedResolution=true;
            if(Application.isPlaying && SafeArea!=null)
                for(int i=transform.childCount-1;i>=0;i--)
                {
                    Transform child=transform.GetChild(i);
                    if(child!=SafeArea && child is RectTransform)child.SetParent(SafeArea,false);
                }
            RefreshLayout();
        }

        private void LateUpdate() => RefreshLayout();

        public void RefreshLayout()
        {
            if(SafeArea==null || canvas==null || scaler==null) return;
            // Expand keeps the complete design visible on narrow and tall screens.
            scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            if(!Application.isPlaying)
            {
                SafeArea.anchorMin=Vector2.zero;
                SafeArea.anchorMax=Vector2.one;
                SafeArea.offsetMin=SafeArea.offsetMax=Vector2.zero;
                return;
            }
            float width=Mathf.Max(1f,Screen.width),height=Mathf.Max(1f,Screen.height);
            Rect safe=RespectSafeArea ? Screen.safeArea : new Rect(0f,0f,width,height);
            if(safe.width<=0f || safe.height<=0f) safe=new Rect(0f,0f,width,height);
            Vector2 minimum=new Vector2(Mathf.Clamp01(safe.xMin/width),Mathf.Clamp01(safe.yMin/height));
            Vector2 maximum=new Vector2(Mathf.Clamp01(safe.xMax/width),Mathf.Clamp01(safe.yMax/height));
            SafeArea.anchorMin=minimum;
            SafeArea.anchorMax=maximum;
            SafeArea.offsetMin=SafeArea.offsetMax=Vector2.zero;
            // Fit against usable pixels so cutouts do not compress the authored header.
            Vector2 fraction=maximum-minimum;
            scaler.referenceResolution=new Vector2(designResolution.x/Mathf.Max(.01f,fraction.x),
                designResolution.y/Mathf.Max(.01f,fraction.y));
            canvas.scaleFactor=Mathf.Max(.01f,Mathf.Min(width*fraction.x/Mathf.Max(1f,designResolution.x),
                height*fraction.y/Mathf.Max(1f,designResolution.y)));
        }

        private void OnDisable()
        {
            if(capturedResolution && scaler!=null) scaler.referenceResolution=designResolution;
        }
    }
}
