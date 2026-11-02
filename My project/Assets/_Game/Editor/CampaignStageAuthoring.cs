using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PuzzleGame.Editor
{
    /// <summary>Saved, editable gameplay scenes. Connections are outside solids and checked at authoring time.</summary>
    public static class CampaignStageAuthoring
    {
        public const string Folder = "Assets/_Game/Scenes/Campaign";
        private static readonly Color[] Palette =
        {
            new Color(.13f,.48f,.57f), new Color(.77f,.48f,.22f),
            new Color(.45f,.35f,.65f), new Color(.23f,.58f,.43f)
        };

        [MenuItem("Puzzle Game/Play Campaign/00 Tutorial")] public static void Tutorial() => Open(0);
        [MenuItem("Puzzle Game/Play Campaign/01 First Key")] private static void S01() => Open(1);
        [MenuItem("Puzzle Game/Play Campaign/02 Side Key")] private static void S02() => Open(2);
        [MenuItem("Puzzle Game/Play Campaign/03 Key Relay")] private static void S03() => Open(3);
        [MenuItem("Puzzle Game/Play Campaign/04 First Rail")] private static void S04() => Open(4);
        [MenuItem("Puzzle Game/Play Campaign/05 Rail Relay")] private static void S05() => Open(5);
        [MenuItem("Puzzle Game/Play Campaign/06 Keyed Rail")] private static void S06() => Open(6);
        [MenuItem("Puzzle Game/Play Campaign/07 First Current")] private static void S07() => Open(7);
        [MenuItem("Puzzle Game/Play Campaign/08 Stored Current")] private static void S08() => Open(8);
        [MenuItem("Puzzle Game/Play Campaign/09 Rail Supply")] private static void S09() => Open(9);
        [MenuItem("Puzzle Game/Play Campaign/10 Key Detour")] private static void S10() => Open(10);
        [MenuItem("Puzzle Game/Play Campaign/11 Powered Tracks")] private static void S11() => Open(11);
        [MenuItem("Puzzle Game/Play Campaign/12 Current Fork")] private static void S12() => Open(12);
        [MenuItem("Puzzle Game/Play Campaign/13 Key Sequence")] private static void S13() => Open(13);
        [MenuItem("Puzzle Game/Play Campaign/14 Track Dependencies")] private static void S14() => Open(14);
        [MenuItem("Puzzle Game/Play Campaign/15 Final Circuit")] private static void S15() => Open(15);

        private static void Open(int number)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CampaignStageSpec spec = CampaignStageCatalog.Get(number);
            string path = PathFor(spec);
            if (!File.Exists(path)) Create(spec);
            else EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        public static void BuildCampaignForAutomation()
        {
            CampaignStageCatalog.Validate();
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var failures=new List<string>();
            foreach(CampaignStageSpec spec in new[]{CampaignStageCatalog.Tutorial}.Concat(CampaignStageCatalog.Stages))
            {
                try { Create(spec); }
                catch(Exception exception)
                {
                    failures.Add(spec.SceneName);
                    Debug.LogError("CAMPAIGN LAYOUT FAILED: "+spec.SceneName+"; "+exception.Message);
                }
            }
            ConfigureBuildScenes();
            AssetDatabase.SaveAssets();
            if(failures.Count>0)throw new InvalidOperationException("Campaign layout authoring failed in "
                +failures.Count+" scenes: "+string.Join(", ",failures));
            Debug.Log("Campaign authored: tutorial + 15 gameplay stages. Connection geometry checked; no gameplay run.");
        }

        public static void BuildGameScenesForAutomation()
        {
            BuildCampaignForAutomation();
            LayeredMechanicStageAuthoring.RebuildForAutomation();
        }

        public static void RebuildStage11ForAutomation()
        {
            Create(CampaignStageCatalog.Get(11));
            AssetDatabase.SaveAssets();
        }

        public static void VerifySavedCampaignForAutomation()
        {
            int connections=0;
            foreach(CampaignStageSpec spec in new[]{CampaignStageCatalog.Tutorial}.Concat(CampaignStageCatalog.Stages))
            {
                EditorSceneManager.OpenScene(PathFor(spec),OpenSceneMode.Single);
                ProjectionPrototype owner=UnityEngine.Object.FindFirstObjectByType<ProjectionPrototype>();
                CampaignStageFlow flow=owner!=null?owner.GetComponent<CampaignStageFlow>():null;
                if(owner==null||flow==null||owner.PuzzleStage==null||owner.Route==null||owner.Route.RequireCompletionForExit)
                    throw new InvalidOperationException("Invalid saved campaign binding: "+spec.SceneName);
                var cameraSettings=new SerializedObject(owner);
                if(cameraSettings.FindProperty("initialYawDegrees").floatValue!=25f ||
                    cameraSettings.FindProperty("initialPitchDegrees").floatValue!=25f)
                    throw new InvalidOperationException("Saved campaign must start at 25/25 degrees: "+spec.SceneName);
                if(cameraSettings.FindProperty("topDownPitchThreshold").floatValue!=40f)
                    throw new InvalidOperationException("Saved campaign must enter top-down at 40 degrees: "+spec.SceneName);
                string next=spec.Number<15?CampaignStageCatalog.Get(spec.Number+1).SceneName:"";
                if(flow.NextSceneName!=next||flow.StageNumber!=spec.Number||flow.IsTutorial!=(spec.Number==0))
                    throw new InvalidOperationException("Invalid saved campaign sequence: "+spec.SceneName);
                if(owner.PuzzleStage.Mechanics.Length!=spec.Rooms.Length||
                    owner.MapRoot.GetComponentsInChildren<TextMesh>(true).Length>0||
                    (spec.Number>0&&flow.TutorialText!=null))
                    throw new InvalidOperationException("Unexpected mechanic or explanation binding: "+spec.SceneName);
                if(owner.PuzzleStage.Rewards.Length!=0 ||
                    owner.MapRoot.GetComponentsInChildren<ProjectionPuzzleReward>(true).Length!=0 ||
                    owner.PuzzleStage.Mechanics.Any(mechanic=>mechanic.Prerequisites!=null&&mechanic.Prerequisites.Length>0) ||
                    owner.MapRoot.GetComponentsInChildren<ProjectableBox>(true).Any(block=>!block.gameObject.activeInHierarchy))
                    throw new InvalidOperationException("Saved campaign must allow free access without prerequisite gates: "+spec.SceneName);
                if(owner.MapRoot.GetComponentsInChildren<ProjectableBox>(true).Any(block=>block.name.StartsWith("Projection Step")))
                    throw new InvalidOperationException("Saved campaign contains a removed connecting bridge: "+spec.SceneName);
                Physics.SyncTransforms();
                ValidateRailStandingSpace(owner);
                ValidateMechanicSeparation(owner);
                ProjectionMinimap minimap=owner.Hud.GetComponentInChildren<ProjectionMinimap>(true);
                if(minimap==null || minimap.Prototype!=owner || minimap.raycastTarget)
                    throw new InvalidOperationException("Saved campaign minimap missing or blocking input: "+spec.SceneName);
                ValidateSavedBlockConnectivity(owner);
                foreach(ProjectionPuzzleMechanic mechanic in owner.PuzzleStage.Mechanics)
                {
                    if(mechanic is ProjectionPowerNetwork network)
                    {
                        foreach(ProjectionWire wire in network.Wires)
                        {
                            Vector3 gap=wire.TargetPath[0].position-wire.SourcePath[wire.SourcePath.Length-1].position;
                            float yaw=Mathf.Atan2(gap.x,gap.z)*Mathf.Rad2Deg;
                            float pitch=-Mathf.Asin(gap.normalized.y)*Mathf.Rad2Deg;
                            if(!wire.CanConnect(new ProjectionFrame(Vector3.zero,yaw,pitch))||wire.OperatingPlatforms.Length==0)
                                throw new InvalidOperationException("Saved wire hidden or malformed: "+spec.SceneName+" / "+wire.name);
                            connections++;
                        }
                    }
                    else if(mechanic is ProjectionRailSwitch rail)
                    {
                        for(int i=0;i<rail.Segments.Length-1;i++)
                        {
                            ProjectionRailSegment a=rail.Segments[i],b=rail.Segments[i+1];
                            Vector3 gap=b.Start-a.End;
                            var frame=FrameAlong(gap);
                            if(!ProjectionConnectionVisibility.IsJoinExposed(frame,a.End,b.Start,-a.Direction,b.Direction))
                                throw new InvalidOperationException("Saved rail hidden: "+spec.SceneName);
                            connections++;
                        }
                    }
                    else if(mechanic is ProjectionKeyPuzzle key)
                    {
                        var frame=FrameAlong(key.SecondJoin.position-key.FirstJoin.position);
                        if(!ProjectionConnectionVisibility.IsExposed(frame,key.FirstJoin.position)||
                            !ProjectionConnectionVisibility.IsExposed(frame,key.SecondJoin.position))
                            throw new InvalidOperationException("Saved key hidden: "+spec.SceneName);
                        connections++;
                    }
                }
            }
            ConfigureBuildScenes();
            Debug.Log("SAVED CAMPAIGN VERIFIED: 16 scenes, "+connections+" exposed connections, separated mechanics, minimaps, direct platform travel and clear rail body corridors; no Play Mode.");
        }

        public static string PathFor(CampaignStageSpec spec) => Folder + "/" + spec.SceneName + ".unity";

        private static void Create(CampaignStageSpec spec)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject(spec.Title);
            ProjectionPrototype owner = root.AddComponent<ProjectionPrototype>();
            owner.SurfaceTemplate = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/PrototypeSurface.mat");
            owner.FlatTemplate = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/PrototypeFlat.mat");
            ProjectionPuzzleStage stage = root.AddComponent<ProjectionPuzzleStage>();
            owner.PuzzleStage = stage;
            stage.StageTitle = spec.Title;
            stage.Instructions = spec.LearningGoal; // Authoring metadata; never drawn in gameplay scenes.
            owner.MapRoot = Point("Map", root.transform, Vector3.zero);
            owner.SpawnPoint = Point("Spawn Point", owner.MapRoot, Vector3.zero);
            owner.ExitPoint = Point("Exit Point", owner.MapRoot, Vector3.zero);
            owner.CameraFocusPoint = Point("Camera Focus", owner.MapRoot, Vector3.zero);
            var context = new Context(owner, stage, spec);
            Room previous = null;
            for (int i = 0; i < spec.Rooms.Length; i++)
            {
                CampaignRoomSpec plan = spec.Rooms[i];
                bool sharePower = previous != null && previous.Plan.Kind == CampaignMechanicKind.Power &&
                    !previous.Plan.Branch && plan.Kind == CampaignMechanicKind.Power;
                Island source;
                if (sharePower) source = previous.End;
                else
                {
                    Vector2 size = SizeOr(plan.SourceSize,plan.Kind == CampaignMechanicKind.Rail ? new Vector2(6f,4.4f)
                        : plan.Kind == CampaignMechanicKind.Key ? new Vector2(4.6f,3.6f) : new Vector2(4f,3.2f));
                    source = context.Island("Room " + (i+1).ToString("00") + " " + plan.Kind, plan.SourceOffset, size, i);
                }
                Room room = plan.Kind == CampaignMechanicKind.Key ? MakeKey(context, plan, source, i)
                    : plan.Kind == CampaignMechanicKind.Rail ? MakeRail(context, plan, source, i)
                    : MakePower(context, plan, source, i, sharePower ? previous.EndNode : null);
                context.Rooms.Add(room);
                context.Mechanics.Add(room.Mechanic);
                if (previous != null && !sharePower)
                    context.ConnectIslands(previous.End, source, plan.Yaw);
                if (room.SourceNode != null && !sharePower)
                {
                    room.SourceNode.StartsPowered = plan.RequiresRoom < 0;
                    if (plan.RequiresRoom >= 0)
                        UnityEventTools.AddPersistentListener(Completed(context.Rooms[plan.RequiresRoom].Mechanic),
                            room.SourceNode.Activate);
                }
                previous = room;
            }

            Room last = context.Rooms[context.Rooms.Count-1];
            Vector2 exitSize = new Vector2(4.2f,3.4f);
            Vector3 exitCenter = last.End.Center+spec.ExitOffset;
            Island exit = context.Island("Exit Landing", exitCenter, exitSize, 3);
            context.ConnectIslands(last.End, exit, last.Plan.Yaw);
            owner.ExitPoint.position = exit.Center + Vector3.right * .9f + Vector3.up * .02f;
            Beacon(owner.ExitPoint, "Exit", new Color(.4f,1f,.6f));
            stage.Mechanics = context.Mechanics.ToArray();
            stage.Rewards = new ProjectionPuzzleReward[0];

            Island first = context.Rooms[0].Source;
            var openingView = new ProjectionFrame(Vector3.zero, spec.Rooms[0].Yaw);
            float startingOffset = spec.Rooms[0].Kind == CampaignMechanicKind.Rail ? 2.7f : 1.5f;
            owner.SpawnPoint.position = first.Center - openingView.Right * startingOffset + Vector3.up * .02f;
            owner.CameraFocusPoint.position = first.Center + Vector3.right * 2f + Vector3.up * 1.6f;
            foreach (Island island in context.Islands) context.Checkpoint(island);
            StageRoute route = root.AddComponent<StageRoute>();
            owner.Route = route;
            route.Prototype = owner;
            route.StageTitle = spec.Title;
            route.Checkpoints = context.Checkpoints.ToArray();
            route.CheckpointPlatforms = context.CheckpointFloors.ToArray();
            route.RequireCompletionForExit = false;
            // Preserve the authored height differences with the lowest landing at zero.
            owner.MapRoot.position -= Vector3.up * context.Islands.Min(island=>island.Center.y);
            var settings = new SerializedObject(owner);
            settings.FindProperty("initialZoomSize").floatValue = spec.Number == 0 ? 6f : 6.6f;
            settings.FindProperty("maxZoomSize").floatValue = 50f;
            settings.FindProperty("topDownPitchThreshold").floatValue = 40f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            Transform sunObject = Point("Sun", owner.MapRoot, Vector3.zero);
            sunObject.rotation = Quaternion.Euler(40f,-25f,0f);
            Light sun = sunObject.gameObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.65f;
            sun.shadows = LightShadows.None;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.44f,.48f,.56f);
            ProjectionHudAuthoring.CreateHud(owner);
            CampaignStageFlow flow = root.AddComponent<CampaignStageFlow>();
            flow.Prototype = owner;
            flow.StageNumber = spec.Number;
            flow.IsTutorial = spec.Number == 0;
            flow.NextSceneName = spec.Number < 15 ? CampaignStageCatalog.Get(spec.Number+1).SceneName : "";
            flow.ClearText = owner.Hud.ClearText;
            if (flow.IsTutorial)
            {
                flow.TutorialMechanics = context.Mechanics.ToArray();
                flow.TutorialText = TutorialLabel(owner.Hud.transform.Find("Safe Area") ?? owner.Hud.transform);
            }
            ProjectionMinimap minimap=ProjectionMinimap.AttachTo(owner);
            PrefabUtility.RecordPrefabInstancePropertyModifications(minimap);
            PrefabUtility.RecordPrefabInstancePropertyModifications(minimap.rectTransform);
            Physics.SyncTransforms();
            context.Validate();
            string path = PathFor(spec);
            if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("Could not save " + path);
            Debug.Log("Authored " + spec.SceneName + " · " + context.Rooms.Count + " rooms · exposed joins " + context.Joins.Count);
        }

        private static Room MakeKey(Context context, CampaignRoomSpec plan, Island source, int index)
        {
            var view = new ProjectionFrame(Vector3.zero, plan.Yaw, plan.Pitch);
            Island other = context.Island("Key " + (index+1) + " Far Half", source.Center + view.Forward*plan.Depth,
                SizeOr(plan.TargetSize,new Vector2(3.2f,2.8f)), index);
            var key = Point("Split Key " + (index+1), context.Devices, Vector3.zero).gameObject.AddComponent<ProjectionKeyPuzzle>();
            Vector3 mountingOffset=Vector3.zero;
            if(Mathf.Abs(plan.Pitch)>=context.Owner.TopDownPitchThresholdDegrees)
            {
                // Put a downward-looking cut beyond the upper plate's silhouette;
                // otherwise the upper plate hides the lower key half at its answer.
                Vector3 towardRear=new ProjectionFrame(Vector3.zero,plan.Yaw).Forward;
                mountingOffset=new Vector3((Mathf.Abs(towardRear.x)>.001f?Mathf.Sign(towardRear.x):0f)*source.Size.x*.5f,0f,
                    (Mathf.Abs(towardRear.z)>.001f?Mathf.Sign(towardRear.z):0f)*source.Size.y*.5f);
            }
            key.FirstJoin = Point("Bow Join", source.Root, source.Center + mountingOffset + Vector3.up*.95f);
            key.SecondJoin = Point("Teeth Join", other.Root, other.Center + mountingOffset + Vector3.up*.95f);
            key.FirstJoin.rotation = key.SecondJoin.rotation = Quaternion.Euler(plan.Pitch,plan.Yaw,0f);
            key.ConnectionTolerance = .4f;
            key.AngleTolerance = 15f;
            context.Join(key.FirstJoin,key.SecondJoin,view,Vector3.zero,Vector3.zero,"Key");
            return new Room { Plan=plan, Source=source, End=source, Mechanic=key };
        }

        private static Room MakeRail(Context context, CampaignRoomSpec plan, Island source, int index)
        {
            var view = new ProjectionFrame(Vector3.zero,plan.Yaw,plan.Pitch);
            var rail = Point("Body Rail " + (index+1),context.Devices,Vector3.zero).gameObject.AddComponent<ProjectionRailSwitch>();
            var segments = new List<ProjectionRailSegment>();
            var floors = new List<ProjectableBox> { source.Floor };
            Island island = source;
            Vector3 mountingOffset=Vector3.zero;
            if(Mathf.Abs(plan.Pitch)>=context.Owner.TopDownPitchThresholdDegrees)
                mountingOffset=view.Right*(ProjectedHalfWidth(source.Size,view.Right)+.18f-plan.RailLength*.5f);
            for (int section=0;section<plan.RailSegments;section++)
            {
                Vector3 direction=RailDirection(plan,section);
                Vector3 start = island.Center + mountingOffset - direction*(plan.RailLength*.5f) + Vector3.up*.9f;
                ProjectionRailSegment segment = Point("Rail Section " + (section+1),rail.transform,Vector3.zero)
                    .gameObject.AddComponent<ProjectionRailSegment>();
                segment.StartPoint = Point("Start",island.Root,start);
                segment.EndPoint = Point("End",island.Root,start+direction*plan.RailLength);
                segments.Add(segment);
                if (section+1<plan.RailSegments)
                {
                    float jointYaw = plan.Yaw + (plan.RailJointYawOffsets!=null&&section<plan.RailJointYawOffsets.Length
                        ? plan.RailJointYawOffsets[section] : section == 0 ? 0f : 45f);
                    float depth = plan.RailDepths!=null&&section<plan.RailDepths.Length?plan.RailDepths[section]:plan.Depth;
                    var jointView = new ProjectionFrame(Vector3.zero,jointYaw,plan.Pitch);
                    Vector3 nextDirection=RailDirection(plan,section+1);
                    Island next = context.Island("Rail " + (index+1) + " Carrier " + (section+2),
                        island.Center+mountingOffset+(direction+nextDirection)*(plan.RailLength*.5f)+jointView.Forward*depth,
                        SizeOr(plan.TargetSize,new Vector2(4.8f,3.8f)),index);
                    context.Join(segment.EndPoint, null,jointView,-direction,nextDirection,"Rail",next,nextDirection);
                    island=next;
                    mountingOffset=Vector3.zero;
                    floors.Add(island.Floor);
                }
            }
            // Complete the deferred records now every segment has its start Transform.
            int firstJoin=context.Joins.Count-(plan.RailSegments-1);
            for(int i=0;i<plan.RailSegments-1;i++) context.Joins[firstJoin+i].Second=segments[i+1].StartPoint;
            rail.Segments=segments.ToArray();
            rail.GoalPoint=Point("Yellow Destination",island.Root,segments[segments.Count-1].End);
            rail.OperatingPlatforms=floors.ToArray();
            rail.ConnectionTolerance=.4f;
            rail.AngleTolerance=15f;
            rail.AllowCornerJoints=plan.RailPlanarAngles!=null&&plan.RailPlanarAngles.Any(angle=>Mathf.Abs(angle)>.01f);
            return new Room {Plan=plan,Source=source,End=island,Mechanic=rail};
        }

        private static Vector3 RailDirection(CampaignRoomSpec plan,int section)
        {
            float angle=plan.RailPlanarAngles!=null&&section<plan.RailPlanarAngles.Length
                ?plan.RailPlanarAngles[section]*Mathf.Deg2Rad:0f;
            var horizontal=new ProjectionFrame(Vector3.zero,plan.Yaw);
            return horizontal.Right*Mathf.Cos(angle)+horizontal.Forward*Mathf.Sin(angle);
        }

        private static ProjectionFrame FrameAlong(Vector3 gap)
            => new ProjectionFrame(Vector3.zero,Mathf.Atan2(gap.x,gap.z)*Mathf.Rad2Deg,
                -Mathf.Asin(Mathf.Clamp(gap.normalized.y,-1f,1f))*Mathf.Rad2Deg);

        private static Room MakePower(Context context,CampaignRoomSpec plan,Island source,int index,ProjectionPowerNode carry)
        {
            var network=Point("Power Relay "+(index+1),context.Devices,Vector3.zero).gameObject.AddComponent<ProjectionPowerNetwork>();
            ProjectionPowerNode initial=carry!=null?carry:context.Node(source,"Supply "+(index+1),false);
            var nodes=new List<ProjectionPowerNode>{initial};
            var wires=new List<ProjectionWire>();
            Island primary=null;
            ProjectionPowerNode primaryNode=null;
            int branches=plan.Branch?2:1;
            for(int branch=0;branch<branches;branch++)
            {
                float yaw=plan.Yaw+branch*plan.BranchYawOffset;
                float depth=plan.Depth+branch*plan.BranchDepthOffset;
                float drop=plan.Drop+branch*plan.BranchDropOffset;
                float halfLength=plan.PowerSpan*.5f;
                var horizontal=new ProjectionFrame(Vector3.zero,yaw);
                float pitch=Mathf.Atan2(drop,depth)*Mathf.Rad2Deg;
                var answer=new ProjectionFrame(Vector3.zero,yaw,pitch);
                Island target=context.Island("Relay "+(index+1)+" Terminal "+(branch+1),
                    source.Center+horizontal.Right*plan.PowerSpan+horizontal.Forward*depth-Vector3.up*drop,
                    SizeOr(plan.TargetSize,new Vector2(4f,3.2f)),index+branch+1);
                ProjectionPowerNode node=context.Node(target,"Terminal "+(index+1)+"."+(branch+1),true);
                nodes.Add(node);
                ProjectionWire wire=Point("Exposed Cable "+(index+1)+"."+(branch+1),network.transform,Vector3.zero)
                    .gameObject.AddComponent<ProjectionWire>();
                wire.Source=initial;
                wire.Target=node;
                Vector3 first=source.Center+Vector3.up*.24f;
                Vector3 second=target.Center+Vector3.up*.24f;
                wire.SourcePath=new[]{Point("Socket",source.Root,first),Point("Outboard Cut",source.Root,first+horizontal.Right*halfLength)};
                wire.TargetPath=new[]{Point("Outboard Cut",target.Root,second-horizontal.Right*halfLength),Point("Socket",target.Root,second)};
                wire.OperatingPlatforms=new[]{source.Floor};
                wire.ConnectionTolerance=.4f;
                wire.AngleTolerance=15f;
                wire.SecondsToCharge=2.2f;
                wires.Add(wire);
                context.Join(wire.SourcePath[1],wire.TargetPath[0],answer,-horizontal.Right,horizontal.Right,"Wire");
                context.ConnectIslands(source,target);
                if(branch==0){primary=target;primaryNode=node;}
            }
            network.Nodes=nodes.ToArray();
            network.Wires=wires.ToArray();
            return new Room{Plan=plan,Source=source,End=primary,Mechanic=network,SourceNode=initial,EndNode=primaryNode};
        }

        private static Vector2 SizeOr(Vector2 value,Vector2 fallback)=>value.x>0f&&value.y>0f?value:fallback;

        private static float ProjectedHalfWidth(Vector2 size,Vector3 right)
            => Mathf.Abs(right.x)*size.x*.5f+Mathf.Abs(right.z)*size.y*.5f;

        private static UnityEvent Completed(ProjectionPuzzleMechanic mechanic)
        {
            if(mechanic is ProjectionKeyPuzzle key)return key.OnCollected;
            if(mechanic is ProjectionRailSwitch rail)return rail.OnCompleted;
            return ((ProjectionPowerNetwork)mechanic).OnCompleted;
        }

        private static void ConfigureBuildScenes()
        {
            var scenes=new List<EditorBuildSettingsScene>{new EditorBuildSettingsScene(PathFor(CampaignStageCatalog.Tutorial),true)};
            foreach(CampaignStageSpec spec in CampaignStageCatalog.Stages)scenes.Add(new EditorBuildSettingsScene(PathFor(spec),true));
            var campaignPaths=new HashSet<string>(scenes.Select(s=>s.path));
            foreach(EditorBuildSettingsScene old in EditorBuildSettings.scenes)
                if(!campaignPaths.Contains(old.path))scenes.Add(new EditorBuildSettingsScene(old.path,false));
            EditorBuildSettings.scenes=scenes.ToArray();
        }

        private static Transform Point(string name,Transform parent,Vector3 position)
        {
            var go=new GameObject(name);go.layer=ProjectionPrototype.World3DLayer;
            go.transform.SetParent(parent,false);go.transform.position=position;return go.transform;
        }

        private static Material Material(Color color)
        {
            string path="Assets/_Game/Materials/Campaign_"+ColorUtility.ToHtmlStringRGB(color)+".mat";
            Material material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material!=null)return material;
            material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.color=color;
            material.SetColor("_BaseColor",color);AssetDatabase.CreateAsset(material,path);return material;
        }

        private static GameObject Cube(Transform parent,string name,Vector3 center,Vector3 size,Color color)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.layer=ProjectionPrototype.World3DLayer;
            go.transform.SetParent(parent,false);go.transform.position=center;go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=Material(color);return go;
        }

        private static void Beacon(Transform point,string name,Color color)
        {
            GameObject go=Cube(point,name,point.position+Vector3.up*.55f,new Vector3(.13f,1.1f,.13f),color);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        private static TMP_Text TutorialLabel(Transform canvas)
        {
            var go=new GameObject("Tutorial Hint",typeof(RectTransform),typeof(TextMeshProUGUI));go.layer=5;
            RectTransform rect=(RectTransform)go.transform;rect.SetParent(canvas,false);
            rect.anchorMin=new Vector2(0f,1f);rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,1f);
            rect.offsetMin=new Vector2(32f,-182f);rect.offsetMax=new Vector2(-32f,-124f);
            TMP_Text text=go.GetComponent<TMP_Text>();text.font=KoreanUiFonts.FontAsset;
            text.fontSize=19;text.color=new Color(.92f,.96f,1f);text.raycastTarget=false;
            text.alignment=TextAlignmentOptions.TopLeft;text.textWrappingMode=TextWrappingModes.Normal;
            return text;
        }

        private static void ValidateRailStandingSpace(ProjectionPrototype owner)
        {
            ProjectableBox[] blocks=owner.MapRoot.GetComponentsInChildren<ProjectableBox>(true);
            foreach(ProjectionRailSwitch rail in owner.PuzzleStage.Mechanics.OfType<ProjectionRailSwitch>())
            {
                for(int joint=0;joint<rail.Segments.Length-1;joint++)
                {
                    Vector3 gap=rail.Segments[joint+1].Start-rail.Segments[joint].End;
                    var frame=FrameAlong(gap);
                    if(Mathf.Abs(frame.Pitch)>=owner.TopDownPitchThresholdDegrees)
                    {
                        ValidateTopDownRailFloor(rail,frame,blocks);
                        continue;
                    }
                    foreach(ProjectionRailSegment segment in rail.Segments)
                    {
                        Vector3 direction=segment.Direction.normalized;
                        Vector3 start=segment.Start-direction*.85f-Vector3.up*.9f;
                        Vector3 end=segment.End+direction*.4f-Vector3.up*.9f;
                        int samples=Mathf.CeilToInt(Vector3.Distance(start,end)/.35f);
                        for(int sample=0;sample<=samples;sample++)
                        {
                            Vector2 foot=frame.Project(Vector3.Lerp(start,end,(float)sample/samples));
                            ProjectableBox obstruction=FindBodyObstruction(frame,foot,blocks);
                            if(obstruction!=null)throw new ConnectionPlacementException(owner.name+": rail body corridor blocked at "
                                +frame.Yaw.ToString("F1")+" degrees by "+obstruction.transform.parent.name+"/"+obstruction.name);
                        }
                    }
                }
            }
        }

        private static void ValidateMechanicSeparation(ProjectionPrototype owner)
        {
            ProjectionPuzzleMechanic[] mechanics=owner.PuzzleStage.Mechanics;
            var frames=new List<ProjectionFrame>{new ProjectionFrame(Vector3.zero,25f,25f)};
            foreach(ProjectionPuzzleMechanic mechanic in mechanics)
            {
                if(mechanic is ProjectionKeyPuzzle key)
                    frames.Add(FrameAlong(key.SecondJoin.position-key.FirstJoin.position));
                else if(mechanic is ProjectionRailSwitch rail)
                    for(int joint=0;joint<rail.Segments.Length-1;joint++)
                        frames.Add(FrameAlong(rail.Segments[joint+1].Start-rail.Segments[joint].End));
                else if(mechanic is ProjectionPowerNetwork power)
                    foreach(ProjectionWire wire in power.Wires)
                        frames.Add(FrameAlong(wire.TargetPath[0].position-wire.SourcePath[wire.SourcePath.Length-1].position));
            }
            foreach(ProjectionFrame frame in frames)
                for(int first=0;first<mechanics.Length;first++)
                    for(int second=first+1;second<mechanics.Length;second++)
                    {
                        // Consecutive relays intentionally meet at the same physical battery.
                        if(mechanics[first] is ProjectionPowerNetwork a && mechanics[second] is ProjectionPowerNetwork b &&
                            a.Nodes.Intersect(b.Nodes).Any())continue;
                        List<Rect> firstShapes=DeviceShapes(mechanics[first],frame);
                        List<Rect> secondShapes=DeviceShapes(mechanics[second],frame);
                        if(firstShapes.Any(shape=>secondShapes.Any(other=>shape.Overlaps(other))))
                            throw new ConnectionPlacementException(owner.name+": independent mechanics overlap at yaw="
                                +frame.Yaw.ToString("F1")+",pitch="+frame.Pitch.ToString("F1")+": "
                                +mechanics[first].name+" / "+mechanics[second].name);
                    }
        }

        private static List<Rect> DeviceShapes(ProjectionPuzzleMechanic mechanic,ProjectionFrame frame)
        {
            var shapes=new List<Rect>();
            if(mechanic is ProjectionKeyPuzzle key)
            {
                AddDeviceShape(shapes,frame,key.FirstJoin.position,key.FirstJoin.position,1.35f);
                AddDeviceShape(shapes,frame,key.SecondJoin.position,key.SecondJoin.position,1.35f);
            }
            else if(mechanic is ProjectionRailSwitch rail)
                foreach(ProjectionRailSegment segment in rail.Segments)
                    AddDeviceShape(shapes,frame,segment.Start,segment.End,.7f);
            else if(mechanic is ProjectionPowerNetwork network)
            {
                foreach(ProjectionPowerNode node in network.Nodes)
                    AddDeviceShape(shapes,frame,node.transform.position,node.transform.position,.75f);
                foreach(ProjectionWire wire in network.Wires)
                    foreach(Transform[] path in new[]{wire.SourcePath,wire.TargetPath})
                        for(int i=1;i<path.Length;i++)AddDeviceShape(shapes,frame,path[i-1].position,path[i].position,.45f);
            }
            return shapes;
        }

        private static void AddDeviceShape(List<Rect> shapes,ProjectionFrame frame,Vector3 a,Vector3 b,float padding)
        {
            Vector2 minimum=Vector2.Min(frame.Project(a),frame.Project(b))-Vector2.one*padding;
            Vector2 maximum=Vector2.Max(frame.Project(a),frame.Project(b))+Vector2.one*padding;
            shapes.Add(Rect.MinMaxRect(minimum.x,minimum.y,maximum.x,maximum.y));
        }

        private static void ValidateTopDownRailFloor(ProjectionRailSwitch rail,ProjectionFrame frame,ProjectableBox[] blocks)
        {
            foreach(ProjectionRailSegment segment in rail.Segments)
            {
                Vector2 start=frame.Project(segment.Start),end=frame.Project(segment.End);
                Vector2 tangent=(end-start).normalized;
                Vector2 perpendicular=new Vector2(-tangent.y,tangent.x);
                int samples=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(start,end)/.2f));
                for(int sample=0;sample<=samples;sample++)
                {
                    Vector2 slider=Vector2.Lerp(start,end,(float)sample/samples);
                    bool supported=false;
                    foreach(float side in new[]{0f,.25f,-.25f,.45f,-.45f,.55f,-.55f})
                    {
                        Vector2 foot=slider-tangent*.45f+perpendicular*side;
                        if(blocks.Any(block=>block.isActiveAndEnabled&&block.SourceCollider.enabled&&
                            ProjectionMath.TryResolveBoxTopPoint(block.SourceCollider,frame,foot,.04f,out _)))
                        {supported=true;break;}
                    }
                    if(!supported)throw new ConnectionPlacementException(rail.name+": no top-down pushing floor at "
                        +segment.name+" sample "+sample+" ("+slider.x.ToString("F2")+", "+slider.y.ToString("F2")+")");
                }
            }
        }

        private static void ValidateTransitRoutes(List<TransitCase> required,List<Island> islands,ProjectionPrototype owner)
        {
            ProjectableBox[] blocks=owner.MapRoot.GetComponentsInChildren<ProjectableBox>(true);
            var edges=new Dictionary<int,bool>();
            foreach(TransitCase transit in required)
            {
                int sourceIndex=islands.IndexOf(transit.Source),targetIndex=islands.IndexOf(transit.Target);
                int directKey=EdgeKey(sourceIndex,targetIndex,islands.Count);
                bool direct=IsClearDirectTransit(transit,blocks,owner.TopDownPitchThresholdDegrees,out List<string> diagnostics);
                edges[directKey]=direct;
                if(direct)continue;
                var visited=new HashSet<int>{sourceIndex};
                var pending=new Queue<int>();pending.Enqueue(sourceIndex);
                while(pending.Count>0&&!visited.Contains(targetIndex))
                {
                    int current=pending.Dequeue();
                    for(int next=0;next<islands.Count;next++)
                    {
                        if(visited.Contains(next))continue;
                        int key=EdgeKey(current,next,islands.Count);
                        if(!edges.TryGetValue(key,out bool clear))
                        {
                            clear=IsClearDirectTransit(new TransitCase{Source=islands[current],Target=islands[next]},blocks,
                                owner.TopDownPitchThresholdDegrees,out _);
                            edges[key]=clear;
                        }
                        if(!clear)continue;
                        visited.Add(next);pending.Enqueue(next);
                    }
                }
                if(!visited.Contains(targetIndex))throw new ConnectionPlacementException(owner.name
                    +": no route through existing mechanic blocks between "+transit.Source.Root.name+" and "+transit.Target.Root.name
                    +"; reachable="+string.Join(",",visited.Select(index=>islands[index].Root.name))
                    +"; direct candidates="+string.Join(" | ",diagnostics));
            }
        }

        private static int EdgeKey(int first,int second,int count)=>Mathf.Min(first,second)*count+Mathf.Max(first,second);

        private static void ValidateSavedBlockConnectivity(ProjectionPrototype owner)
        {
            var islands=new List<Island>();
            foreach(ProjectableBox block in owner.MapRoot.GetComponentsInChildren<ProjectableBox>(true))
            {
                Bounds bounds=block.SourceCollider.bounds;
                islands.Add(new Island{Root=block.transform.parent,Floor=block,
                    Center=new Vector3(bounds.center.x,bounds.max.y,bounds.center.z),Size=new Vector2(bounds.size.x,bounds.size.z)});
            }
            if(islands.Count==0)throw new ConnectionPlacementException(owner.name+": no mechanic landing blocks");
            var required=new List<TransitCase>();
            foreach(Island island in islands.Skip(1))required.Add(new TransitCase{Source=islands[0],Target=island});
            ValidateTransitRoutes(required,islands,owner);
        }

        private static bool IsClearDirectTransit(TransitCase transit,ProjectableBox[] blocks,float topDownThreshold,
            out List<string> diagnostics)
        {
            var frames=new List<ProjectionFrame>();
            Vector3 sourceTop=PlatformTop(transit.Source),targetTop=PlatformTop(transit.Target);
            Vector3 delta=targetTop-sourceTop;
            if(transit.PreferredYaw.HasValue)
            {
                frames.Add(TransitFrame(delta,transit.PreferredYaw.Value));
                frames.Add(TransitFrame(delta,transit.PreferredYaw.Value+180f));
            }
            float heading=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;
            for(int side=-1;side<=1;side+=2)
            {
                float low=0f,high=89f;
                for(int iteration=0;iteration<24;iteration++)
                {
                    float angle=(low+high)*.5f;
                    var horizontal=new ProjectionFrame(Vector3.zero,heading+side*angle);
                    float separation=Mathf.Abs(Vector3.Dot(delta,horizontal.Right));
                    float width=ProjectedHalfWidth(transit.Source.Size,horizontal.Right)
                        +ProjectedHalfWidth(transit.Target.Size,horizontal.Right)-.3f;
                    if(separation>width)high=angle;else low=angle;
                }
                foreach(float separation in new[]{1f,.85f,.65f})
                {
                    float yaw=heading+side*(low+high)*.5f*separation;
                    frames.Add(TransitFrame(delta,yaw));
                    frames.Add(TransitFrame(delta,yaw+180f));
                }
            }
            diagnostics=new List<string>();
            foreach(ProjectionFrame frame in frames)
            {
                string view="yaw="+frame.Yaw.ToString("F1")+",pitch="+frame.Pitch.ToString("F1");
                if(Mathf.Abs(frame.Pitch)>=topDownThreshold)
                {
                    if(IsClearTopDownTransit(transit,frame))return true;
                    diagnostics.Add(view+": top-down floor faces do not form a traversable overlap");
                    continue;
                }
                Vector2 first=frame.Project(sourceTop),second=frame.Project(targetTop);
                float overlap=ProjectedHalfWidth(transit.Source.Size,frame.Right)+ProjectedHalfWidth(transit.Target.Size,frame.Right)
                    -Mathf.Abs(first.x-second.x);
                if(overlap<.15f){diagnostics.Add(view+": gap="+(-overlap).ToString("F2"));continue;}
                ProjectableBox obstruction=FindBodyObstruction(frame,EntryFoot(transit.Source,frame),blocks,transit.Source.Floor);
                if(obstruction==null)obstruction=FindBodyObstruction(frame,EntryFoot(transit.Target,frame),blocks,transit.Target.Floor);
                if(obstruction!=null)
                {
                    diagnostics.Add(view+": entry blocked by "+obstruction.transform.parent.name+"/"+obstruction.name);
                    continue;
                }
                bool clear=true;
                float previousHeight=float.NaN;
                for(int sample=0;sample<=20;sample++)
                {
                    float x=Mathf.Lerp(first.x,second.x,sample/20f);
                    bool onSource=ProjectionMath.TryGetProjectedSurface(transit.Source.Floor.SourceCollider,frame,x,out float sourceY,out Vector2 sourceNormal);
                    bool onTarget=ProjectionMath.TryGetProjectedSurface(transit.Target.Floor.SourceCollider,frame,x,out float targetY,out Vector2 targetNormal);
                    if(!onSource&&!onTarget){diagnostics.Add(view+": no floor at sample "+sample);clear=false;break;}
                    bool useSource=onSource&&(!onTarget||sourceY>=targetY);
                    float y=useSource?sourceY:targetY;
                    Vector2 normal=useSource?sourceNormal:targetNormal;
                    y+=PrototypePlayer3D.Radius*(1f/Mathf.Max(normal.y,.1f)-1f);
                    if(!float.IsNaN(previousHeight)&&Mathf.Abs(y-previousHeight)>PrototypePlayer3D.JumpHeight-.05f)
                    {
                        diagnostics.Add(view+": unjumpable floor step "+Mathf.Abs(y-previousHeight).ToString("F2")
                            +" at sample "+sample);
                        clear=false;break;
                    }
                    previousHeight=y;
                    obstruction=FindBodyObstruction(frame,new Vector2(x,y),blocks,transit.Source.Floor,transit.Target.Floor);
                    if(obstruction!=null)
                    {
                        diagnostics.Add(view+": sample "+sample+",x="+x.ToString("F2")+",overlap="+overlap.ToString("F2")
                            +" blocked by "+obstruction.transform.parent.name+"/"+obstruction.name);
                        clear=false;break;
                    }
                }
                if(clear)return true;
            }
            return false;
        }

        private static bool IsClearTopDownTransit(TransitCase transit,ProjectionFrame frame)
        {
            Vector2[] first=ProjectionMath.ProjectBoxTop(transit.Source.Floor.SourceCollider,frame);
            Vector2[] second=ProjectionMath.ProjectBoxTop(transit.Target.Floor.SourceCollider,frame);
            if(first.Length<3||second.Length<3)return false;
            List<Vector2> overlap=ClipFloor(first,second);
            if(overlap.Count<3)return false;
            float area=0f;
            Vector2 crossing=Vector2.zero;
            for(int i=0;i<overlap.Count;i++)
            {
                area+=Cross(overlap[i],overlap[(i+1)%overlap.Count]);
                crossing+=overlap[i];
            }
            if(Mathf.Abs(area)*.5f<.03f)return false;
            crossing/=overlap.Count;
            if(!ProjectionMath.TryResolveBoxTopPoint(transit.Source.Floor.SourceCollider,frame,crossing,.025f,out _)||
                !ProjectionMath.TryResolveBoxTopPoint(transit.Target.Floor.SourceCollider,frame,crossing,.025f,out _))return false;
            // Each face is convex, so a route through the interior overlap stays on
            // their union. Distinct return points also prevent a fully hidden lower
            // floor from being mistaken for a usable change of world depth.
            return HasExclusiveReturnPoint(transit.Source,transit.Target,frame)
                &&HasExclusiveReturnPoint(transit.Target,transit.Source,frame);
        }

        private static bool HasExclusiveReturnPoint(Island landing,Island other,ProjectionFrame frame)
        {
            Bounds bounds=landing.Floor.SourceCollider.bounds;
            float inset=PrototypePlayer3D.Radius+.1f;
            float halfX=bounds.extents.x-inset,halfZ=bounds.extents.z-inset;
            if(halfX<=0f||halfZ<=0f)return false;
            foreach(float x in new[]{0f,-1f,1f,-.5f,.5f})
                foreach(float z in new[]{0f,-1f,1f,-.5f,.5f})
                {
                    Vector3 foot=new Vector3(bounds.center.x+x*halfX,bounds.max.y,bounds.center.z+z*halfZ);
                    Vector2 point=frame.Project(foot);
                    if(ProjectionMath.TryResolveBoxTopPoint(other.Floor.SourceCollider,frame,point,0f,out _))continue;
                    Vector3 bottom=foot+Vector3.up*(PrototypePlayer3D.Radius+.025f);
                    Vector3 top=foot+Vector3.up*(PrototypePlayer3D.Height-PrototypePlayer3D.Radius);
                    if(!Physics.CheckCapsule(bottom,top,PrototypePlayer3D.Radius-.015f,
                        1<<ProjectionPrototype.World3DLayer,QueryTriggerInteraction.Ignore))return true;
                }
            return false;
        }

        private static List<Vector2> ClipFloor(Vector2[] subject,Vector2[] boundary)
        {
            var polygon=new List<Vector2>(subject);
            for(int edge=0;edge<boundary.Length&&polygon.Count>0;edge++)
            {
                Vector2 a=boundary[edge],direction=boundary[(edge+1)%boundary.Length]-a;
                var clipped=new List<Vector2>();
                Vector2 previous=polygon[polygon.Count-1];
                float previousSide=Cross(direction,previous-a);
                foreach(Vector2 current in polygon)
                {
                    float currentSide=Cross(direction,current-a);
                    bool wasInside=previousSide>=-.00001f,isInside=currentSide>=-.00001f;
                    if(wasInside!=isInside)
                        clipped.Add(Vector2.LerpUnclamped(previous,current,previousSide/(previousSide-currentSide)));
                    if(isInside)clipped.Add(current);
                    previous=current;previousSide=currentSide;
                }
                polygon=clipped;
            }
            return polygon;
        }

        private static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;

        private static Vector2 EntryFoot(Island island,ProjectionFrame frame)
        {
            Vector2 foot=frame.Project(PlatformTop(island));
            if(ProjectionMath.TryGetProjectedSurface(island.Floor.SourceCollider,frame,foot.x,out float height,out Vector2 normal))
                foot.y=height+PrototypePlayer3D.Radius*(1f/Mathf.Max(normal.y,.1f)-1f);
            return foot;
        }

        private static Vector3 PlatformTop(Island island)
        {
            Bounds bounds=island.Floor.SourceCollider.bounds;
            return new Vector3(bounds.center.x,bounds.max.y,bounds.center.z);
        }

        private static ProjectionFrame TransitFrame(Vector3 delta,float yaw)
        {
            Vector3 forward=new ProjectionFrame(Vector3.zero,yaw).Forward;
            float signedDepth=Vector3.Dot(delta,forward);
            if(Mathf.Abs(signedDepth)<.001f)signedDepth=signedDepth<0f?-.001f:.001f;
            float pitch=Mathf.Atan(-delta.y/signedDepth)*Mathf.Rad2Deg;
            return new ProjectionFrame(Vector3.zero,yaw,pitch);
        }

        private static ProjectableBox FindBodyObstruction(ProjectionFrame frame,Vector2 foot,ProjectableBox[] blocks,
            ProjectableBox ignoreFirst=null,ProjectableBox ignoreSecond=null)
        {
            float radius=PrototypePlayer3D.Radius-.02f;
            var body=new Rect(foot.x-radius,foot.y+.035f,radius*2f,PrototypePlayer3D.Height-.035f);
            foreach(ProjectableBox block in blocks)
            {
                if(block==ignoreFirst||block==ignoreSecond||!block.isActiveAndEnabled||!block.SourceCollider.enabled)continue;
                Vector2[] hull=ProjectionMath.ProjectBox(block.SourceCollider,frame);
                if(OverlapsBody(hull,body))return block;
            }
            return null;
        }

        private static bool OverlapsBody(Vector2[] polygon,Rect body)
        {
            if(polygon.Length<3)return false;
            for(int axisIndex=-2;axisIndex<polygon.Length;axisIndex++)
            {
                Vector2 axis=axisIndex==-2?Vector2.right:axisIndex==-1?Vector2.up:
                    new Vector2(-(polygon[(axisIndex+1)%polygon.Length]-polygon[axisIndex]).y,
                        (polygon[(axisIndex+1)%polygon.Length]-polygon[axisIndex]).x);
                float minimum=float.PositiveInfinity,maximum=float.NegativeInfinity;
                foreach(Vector2 point in polygon)
                {float value=Vector2.Dot(point,axis);minimum=Mathf.Min(minimum,value);maximum=Mathf.Max(maximum,value);}
                float center=Vector2.Dot(body.center,axis);
                float extent=Mathf.Abs(axis.x)*body.width*.5f+Mathf.Abs(axis.y)*body.height*.5f;
                if(maximum<=center-extent+.0001f||minimum>=center+extent-.0001f)return false;
            }
            return true;
        }

        private sealed class Context
        {
            public readonly ProjectionPrototype Owner;
            public readonly Transform Platforms,Devices;
            public readonly CampaignStageSpec Spec;
            public readonly List<Island> Islands=new List<Island>();
            public readonly List<Room> Rooms=new List<Room>();
            public readonly List<ProjectionPuzzleMechanic> Mechanics=new List<ProjectionPuzzleMechanic>();
            public readonly List<Transform> Checkpoints=new List<Transform>();
            public readonly List<ProjectableBox> CheckpointFloors=new List<ProjectableBox>();
            public readonly List<JoinCase> Joins=new List<JoinCase>();
            private readonly List<TransitCase> transits=new List<TransitCase>();
            private int nextId=1;
            public Context(ProjectionPrototype owner,ProjectionPuzzleStage stage,CampaignStageSpec spec)
            {
                Owner=owner;Spec=spec;Platforms=Point("Platforms",owner.MapRoot,Vector3.zero);
                Devices=Point("Mechanisms",owner.MapRoot,Vector3.zero);
            }
            public Island Island(string name,Vector3 center,Vector2 size,int palette)
            {
                Transform group=Point(name,Platforms,center);
                GameObject go=Cube(group,"Landing Block",center-Vector3.up*.45f,new Vector3(size.x,.9f,size.y),Palette[palette%Palette.Length]);
                ProjectableBox floor=go.AddComponent<ProjectableBox>();floor.Initialize(nextId++,Palette[palette%Palette.Length]);
                var island=new Island{Root=group,Floor=floor,Center=center,Size=size};Islands.Add(island);return island;
            }
            public ProjectionPowerNode Node(Island island,string name,bool final)
            {
                var node=Point(name,island.Root,island.Center+Vector3.up*.5f).gameObject.AddComponent<ProjectionPowerNode>();
                node.Label=name;node.IsFinal=final;return node;
            }
            public void Checkpoint(Island island)
            {
                Transform point=Point("Automatic Respawn",island.Root,island.Center+Vector3.forward*Mathf.Min(1f,island.Size.y*.25f)+Vector3.up*.02f);
                Checkpoints.Add(point);CheckpointFloors.Add(island.Floor);
            }
            public void ConnectIslands(Island source,Island target,float? preferredYaw=null)
            {
                transits.Add(new TransitCase{Source=source,Target=target,PreferredYaw=preferredYaw});
            }
            public void Join(Transform first,Transform second,ProjectionFrame frame,Vector3 inwardA,Vector3 inwardB,
                string kind,Island deferred=null,Vector3 direction=default)
            {
                Joins.Add(new JoinCase{First=first,Second=second,Frame=frame,InwardA=inwardA,InwardB=inwardB,Kind=kind});
            }
            public void Validate()
            {
                ValidateJoins();
                ValidateRailStandingSpace(Owner);
                ValidateMechanicSeparation(Owner);
                ValidateTransitRoutes(transits,Islands,Owner);
                foreach(Room room in Rooms)
                    if(room.Plan.Kind==CampaignMechanicKind.Power && room.End.Center.y>=room.Source.Center.y)
                        throw new InvalidOperationException(Spec.SceneName+": power route must descend");
            }
            private void ValidateJoins()
            {
                foreach(JoinCase join in Joins)
                {
                    if(join.First==null||join.Second==null)throw new InvalidOperationException(Spec.SceneName+": missing cut end");
                    float distance=Vector2.Distance(join.Frame.Project(join.First.position),join.Frame.Project(join.Second.position));
                    if(distance>.005f)throw new InvalidOperationException(Spec.SceneName+": unaligned authored "+join.Kind+" gap="+distance);
                }
                foreach(JoinCase join in Joins)
                {
                    if(!Visible(join))throw new ConnectionPlacementException(Spec.SceneName+": hidden "+join.Kind+" connection");
                }
            }
            private static bool Visible(JoinCase join)
            {
                return join.Kind=="Key"?
                    ProjectionConnectionVisibility.IsExposed(join.Frame,join.First.position)&&ProjectionConnectionVisibility.IsExposed(join.Frame,join.Second.position):
                    ProjectionConnectionVisibility.IsJoinExposed(join.Frame,join.First.position,join.Second.position,join.InwardA,join.InwardB);
            }
        }

        private sealed class Island { public Transform Root;public ProjectableBox Floor;public Vector3 Center;public Vector2 Size; }
        private sealed class Room
        {
            public CampaignRoomSpec Plan;public Island Source,End;public ProjectionPuzzleMechanic Mechanic;
            public ProjectionPowerNode SourceNode,EndNode;
        }
        private sealed class JoinCase
        {
            public Transform First,Second;public ProjectionFrame Frame;public Vector3 InwardA,InwardB;public string Kind;
        }
        private sealed class TransitCase { public Island Source,Target;public float? PreferredYaw; }
        private sealed class ConnectionPlacementException : InvalidOperationException
        {
            public ConnectionPlacementException(string message):base(message){}
        }
    }
}
