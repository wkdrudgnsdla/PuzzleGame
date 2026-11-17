using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PuzzleGame
{
    /// <summary>A north-up XZ map of the real scene, independent of the puzzle camera.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ProjectionMinimap : MaskableGraphic
    {
        public ProjectionPrototype Prototype;
        [Header("Panel colors")]
        public Color BackgroundColor = new Color(.07f,.11f,.15f,.96f);
        public Color BorderColor = new Color(.4f,.55f,.62f,.65f);
        private readonly List<FloorShape> floors = new List<FloorShape>();
        private ProjectionKeyPuzzle[] keys = new ProjectionKeyPuzzle[0];
        private ProjectionRailSwitch[] rails = new ProjectionRailSwitch[0];
        private ProjectionWire[] wires = new ProjectionWire[0];
        private ProjectionPowerNode[] nodes = new ProjectionPowerNode[0];
        private Vector2 worldMin, worldMax, mapOrigin;
        private float mapScale;
        private bool layoutReady;

        private static readonly Color Cyan = new Color(.1f,.8f,1f);
        private static readonly Color Gold = new Color(1f,.8f,.2f);
        private static readonly Color Green = new Color(.3f,1f,.58f);

        public static ProjectionMinimap AttachTo(ProjectionPrototype owner)
        {
            if (owner == null || owner.Hud == null) return null;
            ProjectionMinimap existing=owner.Hud.GetComponentInChildren<ProjectionMinimap>(true);
            if (existing == null) existing=CreateForHud(owner.Hud);
            existing.Prototype=owner;
            existing.RebuildLayout();
            return existing;
        }

        public static ProjectionMinimap CreateForHud(ProjectionPrototypeHud hud)
        {
            var go=new GameObject("Tactical Minimap",typeof(RectTransform),typeof(ProjectionMinimap));
            go.layer=5;
            RectTransform rect=(RectTransform)go.transform;
            rect.SetParent(hud.transform.Find("Safe Area") ?? hud.transform,false);
            rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one;
            rect.anchoredPosition=new Vector2(-24f,-128f);
            rect.sizeDelta=new Vector2(244f,258f);
            ProjectionMinimap map=go.GetComponent<ProjectionMinimap>();
            map.raycastTarget=false;
            MapLabel(rect,"Map Title","MAP",new Vector2(12f,-8f),new Vector2(100f,20f),TextAlignmentOptions.MidlineLeft);
            MapLabel(rect,"North","N ↑",new Vector2(190f,-8f),new Vector2(42f,20f),TextAlignmentOptions.MidlineRight);
            return map;
        }

        private static void MapLabel(Transform parent,string name,string value,Vector2 position,Vector2 size,TextAlignmentOptions align)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));
            go.layer=5;
            RectTransform rect=(RectTransform)go.transform;
            rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0f,1f);
            rect.anchoredPosition=position;rect.sizeDelta=size;
            TMP_Text label=go.GetComponent<TMP_Text>();
            label.font=TMP_Settings.defaultFontAsset;
            label.fontSize=12;label.fontStyle=FontStyles.Bold;label.text=value;
            label.textWrappingMode=TextWrappingModes.NoWrap;
            label.color=new Color(.7f,.82f,.86f);label.alignment=align;label.raycastTarget=false;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
            layoutReady = false;
        }

        protected override void Start()
        {
            base.Start();
            RebuildLayout();
        }

        private void LateUpdate()
        {
            if (!layoutReady && Prototype != null) RebuildLayout();
            if (Application.isPlaying) SetVerticesDirty();
        }

        public void RebuildLayout()
        {
            floors.Clear();
            if (Prototype == null || Prototype.MapRoot == null) return;
            worldMin = new Vector2(float.PositiveInfinity,float.PositiveInfinity);
            worldMax = new Vector2(float.NegativeInfinity,float.NegativeInfinity);
            foreach (ProjectableBox block in Prototype.MapRoot.GetComponentsInChildren<ProjectableBox>(true))
            {
                BoxCollider box = block.SourceCollider;
                var corners = new Vector2[4];
                for (int i=0;i<4;i++)
                {
                    Vector3 local = box.center + Vector3.Scale(box.size*.5f,
                        new Vector3(i==0||i==3?-1f:1f,1f,i<2?-1f:1f));
                    Vector3 world = box.transform.TransformPoint(local);
                    corners[i] = XZ(world);
                    Include(world);
                }
                floors.Add(new FloorShape { Block=block, Corners=corners });
            }
            keys = Prototype.MapRoot.GetComponentsInChildren<ProjectionKeyPuzzle>(true);
            rails = Prototype.MapRoot.GetComponentsInChildren<ProjectionRailSwitch>(true);
            wires = Prototype.MapRoot.GetComponentsInChildren<ProjectionWire>(true);
            nodes = Prototype.MapRoot.GetComponentsInChildren<ProjectionPowerNode>(true);
            // Cable arms can extend beyond the small carrier blocks.
            foreach (ProjectionWire wire in wires)
            {
                foreach (Transform point in wire.SourcePath) if (point != null) Include(point.position);
                foreach (Transform point in wire.TargetPath) if (point != null) Include(point.position);
            }
            if (Prototype.ExitPoint != null) Include(Prototype.ExitPoint.position);
            if (Prototype.SpawnPoint != null) Include(Prototype.SpawnPoint.position);
            layoutReady = floors.Count>0;
            SetVerticesDirty();
        }

        private void Include(Vector3 world)
        {
            worldMin = Vector2.Min(worldMin,XZ(world));
            worldMax = Vector2.Max(worldMax,XZ(world));
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect panel = rectTransform.rect;
            Quad(mesh,new Vector2(panel.xMin,panel.yMin),new Vector2(panel.xMax,panel.yMin),
                new Vector2(panel.xMax,panel.yMax),new Vector2(panel.xMin,panel.yMax),BackgroundColor);
            Outline(mesh,new Rect(panel.xMin+.5f,panel.yMin+.5f,panel.width-1f,panel.height-1f),
                BorderColor);
            if (!layoutReady || Prototype == null) return;
            Rect area = new Rect(panel.xMin+15f,panel.yMin+15f,panel.width-30f,panel.height-48f);
            Vector2 span = worldMax-worldMin+Vector2.one*4f;
            mapScale = Mathf.Min(area.width/Mathf.Max(1f,span.x),area.height/Mathf.Max(1f,span.y));
            mapOrigin = area.center-(worldMin+worldMax)*.5f*mapScale;
            foreach (FloorShape floor in floors)
            {
                if (floor.Block == null || !floor.Block.isActiveAndEnabled || !floor.Block.SourceCollider.enabled) continue;
                bool support = Application.isPlaying && floor.Block.Id==Prototype.CurrentSupportId;
                Color fill = support ? new Color(.28f,.47f,.52f) : new Color(.18f,.25f,.29f);
                Vector2[] p = floor.Corners;
                Quad(mesh,Map(p[0]),Map(p[1]),Map(p[2]),Map(p[3]),fill);
                for (int i=0;i<4;i++) Line(mesh,Map(p[i]),Map(p[(i+1)%4]),1f,
                    support ? new Color(.65f,.9f,.92f) : new Color(.4f,.5f,.54f));
            }
            foreach (ProjectionWire wire in wires)
            {
                if (wire == null || !wire.isActiveAndEnabled || !wire.IsValid) continue;
                float total = PathLength(wire.SourcePath)+PathLength(wire.TargetPath);
                float remaining = total*wire.FillProgress;
                Cable(mesh,wire.SourcePath,ref remaining);
                Cable(mesh,wire.TargetPath,ref remaining);
            }
            foreach (ProjectionRailSwitch rail in rails)
            {
                if (rail == null || !rail.isActiveAndEnabled || rail.Segments.Length==0) continue;
                foreach (ProjectionRailSegment segment in rail.Segments)
                    if (segment != null && segment.IsValid)
                        Line(mesh,Map(segment.Start),Map(segment.End),2f,new Color(.65f,.75f,.72f));
                ProjectionRailSegment first=rail.Segments[0],last=rail.Segments[rail.Segments.Length-1];
                if (first == null || last == null || !first.IsValid || !last.IsValid) continue;
                Diamond(mesh,Map(rail.GoalPoint != null ? rail.GoalPoint.position : last.End),3.5f,
                    rail.IsComplete ? Green : Gold);
                Dot(mesh,Map(Application.isPlaying ? rail.SliderWorldPosition : first.Start),3f,Green);
            }
            foreach (ProjectionKeyPuzzle key in keys)
            {
                if (key == null || !key.isActiveAndEnabled || key.IsComplete) continue;
                if (key.FirstJoin != null) Diamond(mesh,Map(key.FirstJoin.position),3.2f,Gold);
                if (key.SecondJoin != null) Diamond(mesh,Map(key.SecondJoin.position),3.2f,Gold);
            }
            foreach (ProjectionPowerNode node in nodes)
            {
                if (node == null || !node.isActiveAndEnabled) continue;
                bool powered = Application.isPlaying ? node.IsPowered : node.StartsPowered;
                Color c = powered ? Cyan : Gold;
                if (node.IsFinal) Diamond(mesh,Map(node.transform.position),5.2f,c);
                else Dot(mesh,Map(node.transform.position),3.5f,c);
            }
            if (Prototype.ExitPoint != null)
            {
                Vector2 exit=Map(Prototype.ExitPoint.position);
                bool open=Prototype.PuzzleStage==null || Prototype.PuzzleStage.ObjectivesComplete;
                Diamond(mesh,exit,6f,open ? Green : new Color(.5f,.6f,.58f));
                Diamond(mesh,exit,3f,BackgroundColor);
            }
            Vector2 player=ClampToArea(Map(Prototype.PlayerWorldFoot),area,6f);
            float yaw=Prototype.YawDegrees*Mathf.Deg2Rad;
            Vector2 forward=new Vector2(Mathf.Sin(yaw),Mathf.Cos(yaw));
            Vector2 side=new Vector2(forward.y,-forward.x);
            Triangle(mesh,player,ClampToArea(player+forward*19f+side*10f,area,1f),
                ClampToArea(player+forward*19f-side*10f,area,1f),
                new Color(.55f,.9f,1f,.16f));
            Dot(mesh,player,5.3f,BackgroundColor);
            Triangle(mesh,player+forward*6f,player-forward*3.8f-side*4f,player-forward*3.8f+side*4f,
                Prototype.Is2D ? Cyan : Color.white);
        }

        private void Cable(VertexHelper mesh,Transform[] path,ref float remaining)
        {
            for (int i=1;i<path.Length;i++)
            {
                if (path[i-1]==null || path[i]==null) continue;
                Vector3 a=path[i-1].position,b=path[i].position;
                float length=Vector3.Distance(a,b);
                Line(mesh,Map(a),Map(b),1.5f,new Color(.5f,.53f,.48f));
                if (remaining>0f && length>.0001f)
                    Line(mesh,Map(a),Map(Vector3.Lerp(a,b,Mathf.Clamp01(remaining/length))),2f,Cyan);
                remaining=Mathf.Max(0f,remaining-length);
            }
        }

        private static float PathLength(Transform[] path)
        {
            float length=0f;
            for (int i=1;i<path.Length;i++)
                if (path[i-1]!=null && path[i]!=null) length+=Vector3.Distance(path[i-1].position,path[i].position);
            return length;
        }

        private Vector2 Map(Vector3 world) => Map(XZ(world));
        private Vector2 Map(Vector2 world) => mapOrigin+world*mapScale;
        private static Vector2 XZ(Vector3 world) => new Vector2(world.x,world.z);
        private static Vector2 ClampToArea(Vector2 point,Rect area,float margin)
            => new Vector2(Mathf.Clamp(point.x,area.xMin+margin,area.xMax-margin),
                Mathf.Clamp(point.y,area.yMin+margin,area.yMax-margin));

        private static void Outline(VertexHelper mesh,Rect r,Color c)
        {
            Line(mesh,new Vector2(r.xMin,r.yMin),new Vector2(r.xMax,r.yMin),1f,c);
            Line(mesh,new Vector2(r.xMax,r.yMin),new Vector2(r.xMax,r.yMax),1f,c);
            Line(mesh,new Vector2(r.xMax,r.yMax),new Vector2(r.xMin,r.yMax),1f,c);
            Line(mesh,new Vector2(r.xMin,r.yMax),new Vector2(r.xMin,r.yMin),1f,c);
        }

        private static void Line(VertexHelper mesh,Vector2 a,Vector2 b,float width,Color c)
        {
            Vector2 normal=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;
            Quad(mesh,a-normal,b-normal,b+normal,a+normal,c);
        }

        private static void Diamond(VertexHelper mesh,Vector2 p,float radius,Color c)
            => Quad(mesh,p+Vector2.left*radius,p+Vector2.down*radius,p+Vector2.right*radius,p+Vector2.up*radius,c);

        private static void Dot(VertexHelper mesh,Vector2 p,float radius,Color c)
        {
            for (int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6f,b=(i+1)*Mathf.PI/6f;
                Triangle(mesh,p,p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,
                    p+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,c);
            }
        }

        private static void Triangle(VertexHelper mesh,Vector2 a,Vector2 b,Vector2 c,Color color)
        {
            int start=mesh.currentVertCount;
            mesh.AddVert(a,color,Vector2.zero);mesh.AddVert(b,color,Vector2.zero);mesh.AddVert(c,color,Vector2.zero);
            mesh.AddTriangle(start,start+1,start+2);
        }

        private static void Quad(VertexHelper mesh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color)
        {
            int start=mesh.currentVertCount;
            mesh.AddVert(a,color,Vector2.zero);mesh.AddVert(b,color,Vector2.zero);
            mesh.AddVert(c,color,Vector2.zero);mesh.AddVert(d,color,Vector2.zero);
            mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
        }

        private sealed class FloorShape { public ProjectableBox Block; public Vector2[] Corners; }
    }
}
