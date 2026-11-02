using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace PuzzleGame.Editor
{
    /// <summary>The shared, editable HUD prefab and scene bindings.</summary>
    public static class ProjectionHudAuthoring
    {
        public const string PrefabPath = "Assets/_Game/Prefabs/UI/ProjectionHUD.prefab";
        private static readonly Color Panel = new Color(.035f,.055f,.085f,.94f);
        private static readonly Color Muted = new Color(.64f,.72f,.8f);
        private static readonly Color Amber = new Color(1f,.72f,.32f);

        [MenuItem("Puzzle Game/Add or Select Scene HUD")]
        public static void AddOrSelectSceneHud()
        {
            ProjectionPrototype owner=Selection.activeGameObject!=null
                ?Selection.activeGameObject.GetComponentInParent<ProjectionPrototype>():null;
            if(owner==null)owner=UnityEngine.Object.FindFirstObjectByType<ProjectionPrototype>(FindObjectsInactive.Include);
            if(owner==null){Debug.LogWarning("Projection Prototype 오브젝트가 있는 씬을 열어주세요.");return;}
            Selection.activeGameObject=CreateHud(owner).gameObject;
        }

        [MenuItem("Puzzle Game/UI/Rebuild HUD Prefab")]
        public static void RebuildHudPrefab()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            AssetDatabase.Refresh();
            ProjectionPrototypeHud hud=BuildHudObject();
            PrefabUtility.SaveAsPrefabAsset(hud.gameObject,PrefabPath);
            UnityEngine.Object.DestroyImmediate(hud.gameObject);
            AssetDatabase.SaveAssets();
        }

        public static ProjectionPrototypeHud CreateHud(ProjectionPrototype owner)
        {
            if(owner==null)throw new ArgumentNullException(nameof(owner));
            if(owner.Hud!=null)return owner.Hud;
            return InstantiateHud(owner);
        }

        private static ProjectionPrototypeHud InstantiateHud(ProjectionPrototype owner)
        {
            GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if(prefab==null){RebuildHudPrefab();prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);}
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,owner.gameObject.scene);
            ProjectionPrototypeHud hud=instance.GetComponent<ProjectionPrototypeHud>();
            hud.Prototype=owner;
            owner.Hud=hud;
            ProjectionMinimap minimap=ProjectionMinimap.AttachTo(owner);
            hud.RefreshLabels();
            PrefabUtility.RecordPrefabInstancePropertyModifications(hud);
            PrefabUtility.RecordPrefabInstancePropertyModifications(minimap);
            PrefabUtility.RecordPrefabInstancePropertyModifications(minimap.rectTransform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(hud.ModeHintText);
            EditorUtility.SetDirty(owner);
            EditorSceneManager.MarkSceneDirty(owner.gameObject.scene);
            return hud;
        }

        // Batch migration only replaces the HUD; hand-authored map objects remain untouched.
        public static void RebuildAllSceneHudsForAutomation()
        {
            RebuildHudPrefab();
            int count=0;
            foreach(string guid in AssetDatabase.FindAssets("t:Scene",new[]{"Assets/_Game/Scenes"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);
                Scene scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
                ProjectionPrototype owner=UnityEngine.Object.FindFirstObjectByType<ProjectionPrototype>();
                if(owner==null)continue;
                ProjectionPrototypeHud oldHud=owner.Hud;
                owner.Hud=null;
                ProjectionPrototypeHud hud=InstantiateHud(owner);
                CampaignStageFlow flow=owner.GetComponent<CampaignStageFlow>();
                if(flow!=null)
                {
                    if(flow.TutorialText!=null)
                        flow.TutorialText.transform.SetParent(hud.transform.Find("Safe Area") ?? hud.transform,false);
                    flow.ClearText=hud.ClearText;
                    EditorUtility.SetDirty(flow);
                }
                if(oldHud!=null)UnityEngine.Object.DestroyImmediate(oldHud.gameObject);
                // Explicitly save scene overrides while retaining the prefab connection.
                PrefabUtility.RecordPrefabInstancePropertyModifications(hud);
                ProjectionMinimap map=hud.GetComponentInChildren<ProjectionMinimap>(true);
                PrefabUtility.RecordPrefabInstancePropertyModifications(map);
                PrefabUtility.RecordPrefabInstancePropertyModifications(map.rectTransform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(hud.ModeHintText);
                if(!EditorSceneManager.SaveScene(scene,path))throw new IOException("Could not save HUD: "+path);
                count++;
            }
            Selection.objects=new UnityEngine.Object[0];
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            AssetDatabase.SaveAssets();
            Debug.Log("HUD PREFAB AUTHORED: "+count+" scenes use "+PrefabPath+"; no Play Mode.");
        }

        private static ProjectionPrototypeHud BuildHudObject()
        {
            var go=new GameObject("ProjectionHUD",typeof(RectTransform),typeof(Canvas),
                typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(ProjectionPrototypeHud));
            go.layer=5;
            Canvas canvas=go.GetComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;
            CanvasScaler scaler=go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1280f,720f);
            scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;scaler.matchWidthOrHeight=.5f;
            ProjectionPrototypeHud hud=go.GetComponent<ProjectionPrototypeHud>();
            hud.KoreanFont=KoreanUiFonts.FontAsset;
            RectTransform root=(RectTransform)go.transform;
            Image header=Image("Header",root,new Vector2(24f,-24f),new Vector2(-48f,88f),Panel);
            header.rectTransform.anchorMax=Vector2.one;
            hud.AccentTop=Image("Accent Top",header.transform,Vector2.zero,new Vector2(3f,0f),Amber);
            hud.AccentTop.rectTransform.anchorMin=Vector2.zero;hud.AccentTop.rectTransform.anchorMax=Vector2.up;
            hud.ModeBadge=Image("Mode Badge",header.transform,new Vector2(18f,-20f),new Vector2(80f,48f),
                new Color(Amber.r,Amber.g,Amber.b,.14f));
            hud.ModeText=Label("Mode Text",hud.ModeBadge.transform,Vector2.zero,Vector2.zero,"3D",30,Amber,TextAlignmentOptions.Midline);
            RectTransform mode=hud.ModeText.rectTransform;
            mode.anchorMin=Vector2.zero;mode.anchorMax=Vector2.one;mode.offsetMin=mode.offsetMax=Vector2.zero;
            hud.YawText=Label("Yaw Value",header.transform,new Vector2(120f,-20f),new Vector2(128f,48f),
                "좌우 25.0°",22,Amber,TextAlignmentOptions.Midline);
            hud.PitchText=Label("Pitch Value",header.transform,new Vector2(264f,-20f),new Vector2(128f,48f),
                "상하 25.0°",22,Amber,TextAlignmentOptions.Midline);
            hud.ModeHintText=Label("Objective Progress",header.transform,new Vector2(-24f,-20f),new Vector2(220f,48f),
                "클리어 조건 (0/0)",22,hud.ObjectivePendingColor,TextAlignmentOptions.MidlineRight);
            hud.ModeHintText.rectTransform.anchorMin=hud.ModeHintText.rectTransform.anchorMax=Vector2.one;
            hud.ModeHintText.rectTransform.pivot=Vector2.one;

            Image controls=Image("Controls",root,new Vector2(24f,24f),new Vector2(308f,174f),Panel);
            hud.ControlsRoot=controls.gameObject;
            RectTransform controlsRect=controls.rectTransform;
            controlsRect.anchorMin=controlsRect.anchorMax=controlsRect.pivot=Vector2.zero;
            TMP_Text title=Label("Controls Title",controls.transform,new Vector2(16f,-12f),new Vector2(276f,22f),
                "조작",16,Color.white,TextAlignmentOptions.MidlineLeft);
            title.fontStyle=FontStyles.Bold;
            hud.ControlsText=Label("Key Settings",controls.transform,new Vector2(16f,-42f),new Vector2(276f,120f),
                "WASD  이동 · A/D  횡스크롤\nSpace  점프\nR  2D ↔ 3D 전환\n좌클 드래그  화면 이동\n우클 드래그  회전 · 휠  줌\nBackspace  다시 시작",14,Muted,TextAlignmentOptions.TopLeft);
            hud.ControlsText.fontStyle=FontStyles.Normal;
            hud.ClearText=Label("Stage Clear",header.transform,new Vector2(408f,-20f),new Vector2(-668f,48f),
                "Enter를 눌러 다음스테이지로 이동",18,hud.ObjectiveCompleteColor,TextAlignmentOptions.Midline);
            RectTransform clear=hud.ClearText.rectTransform;
            clear.anchorMax=Vector2.one;
            hud.ClearText.enableAutoSizing=true;
            hud.ClearText.fontSizeMin=12f;hud.ClearText.fontSizeMax=18f;
            hud.ClearText.gameObject.SetActive(false);
            ProjectionMinimap.CreateForHud(hud);
            RectTransform[] panels=new RectTransform[root.childCount];
            for(int i=0;i<panels.Length;i++)panels[i]=(RectTransform)root.GetChild(i);
            RectTransform safeArea=Rect("Safe Area",root,Vector2.zero,Vector2.zero);
            safeArea.anchorMin=Vector2.zero;safeArea.anchorMax=Vector2.one;
            safeArea.pivot=new Vector2(.5f,.5f);safeArea.offsetMin=safeArea.offsetMax=Vector2.zero;
            foreach(RectTransform panel in panels)panel.SetParent(safeArea,false);
            ProjectionHudLayout layout=go.AddComponent<ProjectionHudLayout>();
            layout.SafeArea=safeArea;
            layout.RefreshLayout();
            return hud;
        }

        private static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform));go.layer=5;
            RectTransform rect=(RectTransform)go.transform;rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0f,1f);
            rect.anchoredPosition=position;rect.sizeDelta=size;return rect;
        }

        private static Image Image(string name,Transform parent,Vector2 position,Vector2 size,Color color)
        {
            Image image=Rect(name,parent,position,size).gameObject.AddComponent<Image>();
            image.color=color;image.raycastTarget=false;return image;
        }

        private static TMP_Text Label(string name,Transform parent,Vector2 position,Vector2 size,string value,
            int fontSize,Color color,TextAlignmentOptions alignment)
        {
            TMP_Text text=Rect(name,parent,position,size).gameObject.AddComponent<TextMeshProUGUI>();
            text.font=KoreanUiFonts.FontAsset;text.text=value;
            text.fontSize=fontSize;text.fontStyle=FontStyles.Bold;text.color=color;text.alignment=alignment;
            text.richText=false;text.raycastTarget=false;
            text.textWrappingMode=TextWrappingModes.NoWrap;text.overflowMode=TextOverflowModes.Overflow;
            return text;
        }
    }
}
