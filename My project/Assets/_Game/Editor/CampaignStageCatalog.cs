using System;
using System.Collections.Generic;
using UnityEngine;

namespace PuzzleGame.Editor
{
    public enum CampaignMechanicKind { Key, Rail, Power }

    [Serializable]
    public sealed class CampaignRoomSpec
    {
        public CampaignMechanicKind Kind;
        public float Yaw;
        public float Pitch;
        // Positive drop: the output landing is lower than the operating landing.
        public float Drop;
        public float Depth;
        // Explicit world-space placement relative to the first operating block.
        // Consecutive power rooms reuse the previous charged terminal instead.
        public Vector3 SourceOffset;
        public Vector2 SourceSize;
        public Vector2 TargetSize;
        public int RailSegments = 2;
        public float RailLength = 3.6f;
        public float[] RailDepths = new float[0];
        public float[] RailJointYawOffsets = new float[0];
        // Heading offsets in each rail floor's horizontal plane. Top-down routes can turn.
        public float[] RailPlanarAngles = new float[0];
        public float PowerSpan = 8.8f;
        public float BranchYawOffset = 60f;
        public float BranchDepthOffset = 1.5f;
        public float BranchDropOffset;
        // A power fork has two required outputs; either output may be charged first.
        public bool Branch;
        public string Role;
        // Only power rooms use this to identify the device that energizes their source.
        public int RequiresRoom = -1;
        // Completion of this room energizes the following room's initial power source.
        public bool SuppliesPower;
    }

    [Serializable]
    public sealed class CampaignStageSpec
    {
        public int Number;
        public string SceneName;
        public string Title;
        public string MainMechanic;
        public CampaignMechanicKind MainKind;
        public string LearningGoal;
        public string LayoutName;
        // Relative to the final room's output, independent of the route into it.
        public Vector3 ExitOffset;
        public CampaignRoomSpec[] Rooms;
    }

    /// <summary>
    /// Main-mechanic introduction, variation, supporting combinations, then synthesis.
    /// Precision tolerances are deliberately not part of the difficulty curve.
    /// </summary>
    public static class CampaignStageCatalog
    {
        public static readonly CampaignStageSpec Tutorial = Stage(0, "Tutorial_CoreMechanics", "튜토리얼 / 시점과 세 장치",
            CampaignMechanicKind.Key, "시점을 맞추고 2D에서 장치를 사용하는 공통 조작을 짧게 익힌다.",
            Key(15f, 0.55f, 4.5f, "가까운 두 열쇠 조각을 맞춰 2D에서 획득한다."),
            Rail(-15f, 0.55f, 6.5f, 2, "55도 탑뷰에서 스위치를 밀고, 두 번째 구간은 W/S도 사용해 노란 끝점에 도착시킨다."),
            Power(25f, 2.4f, 7f, false, "아래쪽 전선과 전구를 연결해 충전 흐름을 익힌다."));

        public static readonly CampaignStageSpec[] Stages =
        {
            Stage(1, "Stage01_FirstKey", "01 / 처음 맞는 조각", CampaignMechanicKind.Key,
                "떨어진 조각이 화면에서 이어지면 실제 획득 가능한 열쇠가 된다는 규칙을 발견한다.",
                Key(10f, 0.7f, 4.8f, "눈앞의 짧은 틈을 사이에 둔 조각으로 기본 규칙을 소개한다.")),

            Stage(2, "Stage02_SideKey", "02 / 옆에서 보는 열쇠", CampaignMechanicKind.Key,
                "정면에서 보이는 조각의 간격보다 접합면과 모양을 기준으로 올바른 시점을 고른다.",
                Key(-50f, 1.1f, 8.2f, "한쪽 옆으로 더 멀리 떨어진 조각을 다른 관찰 방향에서 맞춘다.")),

            Stage(3, "Stage03_KeyRelay", "03 / 서로 다른 두 시점", CampaignMechanicKind.Key,
                "서로 다른 관찰 발판의 열쇠를 원하는 순서로 완성하고 획득 상태가 유지됨을 배운다.",
                Key(25f, 1.1f, 5.5f, "가까운 아래쪽 관찰 발판의 열쇠를 완성한다."),
                Key(-35f, 1.65f, 15f, "멀리 높은 전망대에서 55도 탑뷰로 나머지 조각을 맞추고 평면 이동해 획득한다.")),

            Stage(4, "Stage04_FirstRail", "04 / 움직이는 접합부", CampaignMechanicKind.Rail,
                "정렬된 경로를 따라 몸으로 스위치를 밀면 다른 공간의 장치가 작동한다는 점을 배운다.",
                Rail(10f, 1.1f, 11f, 2, "긴 깊이 틈을 사이에 둔 두 구간의 접합부를 통과시킨다.")),

            Stage(5, "Stage05_RailRelay", "05 / 두 번 잇는 레일", CampaignMechanicKind.Rail,
                "세 구간의 순서를 읽고, 이미 옮긴 스위치 위치를 이용해 다음 접합을 해결한다.",
                Rail(-25f, 1.65f, 5f, 3, "가까운 첫 구간과 멀리 꺾인 마지막 구간의 접합을 나누어 해결한다.")),

            Stage(6, "Stage06_KeyedRail", "06 / 열쇠와 선로", CampaignMechanicKind.Rail,
                "옆에서 맞추는 열쇠와 위에서 미는 꺾인 선로를 구별하고, 원하는 순서로 해결한다.",
                Key(40f, 1.1f, 4.8f, "시작점 가까이에 있는 보조 열쇠를 얻는다."),
                Rail(-20f, 2.2f, 17f, 2, "멀리 높은 뒤편의 선로에서 55도 탑뷰와 WASD로 꺾인 선로를 끝까지 민다.")),

            Stage(7, "Stage07_FirstCurrent", "07 / 첫 번째 전류", CampaignMechanicKind.Power,
                "켜진 전원에서 비어 있는 전원으로 전력이 시간에 따라 전달된다는 규칙을 이해한다.",
                Power(40f, 4.2f, 11.5f, false, "깊은 절벽 아래 전구의 절단면을 연결해 첫 충전을 끝낸다.")),

            Stage(8, "Stage08_StoredCurrent", "08 / 남겨 둔 전력", CampaignMechanicKind.Power,
                "모든 전선을 동시에 맞추는 대신 충전된 중간 전원을 남기고 다음 발판으로 이동한다.",
                Power(35f, 1.8f, 6.2f, false, "가까운 첫 수신 전원을 충전하고 낮은 작업 발판으로 내려간다."),
                Power(-50f, 6f, 19f, false, "저장된 전력을 멀리 아래쪽 마지막 전구로 이어 보낸다.")),

            Stage(9, "Stage09_RailSupply", "09 / 밀어서 켜는 회로", CampaignMechanicKind.Power,
                "레일은 첫 전원을 켜는 보조 수단이고, 전력 전달 순서가 전체 진행을 결정한다.",
                Rail(30f, 1.65f, 7f, 2, "가까운 선로의 스위치를 밀어 왼쪽 아래 회로의 전원을 켠다."),
                Power(15f, 2.8f, 10f, false, "왼쪽 아래 전원에서 중간 수신부로 충전한다."),
                Power(-40f, 4f, 22f, false, "중간 전원을 이용해 멀리 떨어진 마지막 전구를 켠다.")),

            Stage(10, "Stage10_KeyDetour", "10 / 세 곳의 시점", CampaignMechanicKind.Key,
                "레일과 서로 다른 두 열쇠의 관찰 위치를 구별하고 가까운 장치부터 자유롭게 해결한다.",
                Rail(-15f, 1.65f, 15f, 2, "중앙에서 뒤로 길게 떨어진 보조 레일을 해결한다."),
                Key(55f, 1.1f, 5.5f, "왼쪽 위의 가까운 조각을 관찰한다."),
                Key(-35f, 2.2f, 18f, "오른쪽 아래의 멀리 떨어진 조각을 완성한다.")),

            Stage(11, "Stage11_PoweredTracks", "11 / 전류와 두 선로", CampaignMechanicKind.Rail,
                "머리 공간이 분리된 층의 전력과 두 레일을 직접 투영 이동으로 오가며 해결 순서를 고른다.",
                Power(-15f, 3f, 8.5f, false, "중간 높이의 전원에서 아래 전구까지 충전한다."),
                Rail(20f, 1.65f, 12f, 2, "왼쪽 뒤 높은 군집의 첫 레일을 해결한다."),
                Rail(-30f, 2.2f, 6f, 3, "오른쪽 아래 독립 군집에서 가까운 접합과 먼 접합을 차례로 통과시킨다.")),

            Stage(12, "Stage12_CurrentFork", "12 / 갈라지는 전류", CampaignMechanicKind.Power,
                "옆에서 잇는 분기와 탑뷰에서 잇는 분기를 구별하고, 충전 순서를 스스로 정한다.",
                Power(10f, 3.2f, 9f, true, "첫 출력은 횡스크롤 시점, 깊은 출력은 55도 탑뷰로 모두 충전한다."),
                Key(-45f, 1.1f, 11f, "뒤쪽 높은 열쇠를 얻어 최종 전원을 켠다."),
                Power(70f, 5.5f, 14f, false, "반대편 아래 회로의 최종 릴레이에 전력을 전달한다.")),

            Stage(13, "Stage13_KeySequence", "13 / 조각과 회로", CampaignMechanicKind.Key,
                "열쇠가 켜는 전원과 독립적인 두 번째 열쇠·레일을 구별해 해결 순서를 고른다.",
                Key(-20f, 1.1f, 5f, "가까운 첫 열쇠로 오른쪽 아래 보조 회로의 전원을 켠다."),
                Power(45f, 3.5f, 13f, false, "가까운 회로에 공급된 전력을 아래 전구로 전달한다."),
                Key(-55f, 1.65f, 18f, "왼쪽 위 먼 전망대에서 두 번째 열쇠를 완성한다."),
                Rail(35f, 1.65f, 9f, 2, "깊은 아래층에 독립된 보조 스위치를 해결한다.")),

            Stage(14, "Stage14_TrackDependencies", "14 / 세 장치의 관찰", CampaignMechanicKind.Rail,
                "두 선로는 자유 순서로 해결하고, 열쇠가 회로의 전원을 켜는 실제 연결을 구분한다.",
                Rail(-35f, 1.65f, 7f, 2, "시작 군집의 가까운 첫 선로를 해결한다."),
                Key(30f, 1.1f, 15f, "오른쪽 아래 열쇠가 반대편 회로의 초기 전원을 켠다."),
                Power(-45f, 5f, 17f, false, "왼쪽 아래의 큰 낙차를 가진 회로를 충전한다."),
                Rail(25f, 2.2f, 18f, 3, "뒤쪽 높은 군집의 긴 첫 접합과 짧은 둘째 접합을 해결한다.")),

            Stage(15, "Stage15_FinalCircuit", "15 / 마지막 전력망", CampaignMechanicKind.Power,
                "전력 분기의 충전 순서와 보조 장치의 역할을 종합해 마지막 회로를 완성한다.",
                Rail(15f, 2.2f, 16f, 2, "뒤로 깊게 떨어진 스위치가 아래층 전력망의 첫 전원을 켠다."),
                Power(-25f, 2.8f, 10f, true, "가까운 출력과 깊고 낮은 출력을 원하는 순서로 충전한다."),
                Key(60f, 1.65f, 7f, "오른쪽 위 독립된 열쇠를 획득해 최종 전원을 켠다."),
                Power(45f, 7f, 21f, false, "가장 깊은 회로를 55도 탑뷰로 맞춰 먼 마지막 전구까지 전달한 뒤 출구에 도착한다."))
        };

        static CampaignStageCatalog()
        {
            // Each map is composed deliberately in world space. These are not
            // evenly spaced rooms extruded from the previous room's endpoint.
            Layout(Tutorial, "흩어진 삼각 관찰장", new Vector3(14f,-5f,19f),
                Vector3.zero, new Vector3(21f,-6f,-18f), new Vector3(-24f,-13f,28f));
            Tracks(Tutorial.Rooms[1],3.4f,new[]{6.5f},new[]{0f});
            Tutorial.Rooms[1].Yaw=0f;
            TopDownTrack(Tutorial.Rooms[1],55f,new[]{0f,90f});
            PowerShape(Tutorial.Rooms[2],8f);

            Layout(Stages[0], "떨어진 첫 조각", new Vector3(13f,-5f,-12f), Vector3.zero);
            Layout(Stages[1], "먼 측면", new Vector3(-18f,7f,12f), Vector3.zero);
            Layout(Stages[2], "대각의 두 전망대", new Vector3(24f,-18f,-21f),
                Vector3.zero,new Vector3(-35f,14f,46f));
            Stages[2].Rooms[1].Pitch=55f;
            Stages[2].Rooms[1].SourceSize=new Vector2(4.8f,5.4f);
            Stages[2].Rooms[1].TargetSize=new Vector2(4.4f,4.8f);

            Layout(Stages[3], "긴 수평 선로", new Vector3(-16f,9f,23f), Vector3.zero);
            Tracks(Stages[3].Rooms[0],3.4f,new[]{20f},new[]{0f});
            Layout(Stages[4], "짧고 긴 꺾임", new Vector3(26f,-11f,-18f), Vector3.zero);
            Tracks(Stages[4].Rooms[0],3.2f,new[]{8f,32f},new[]{0f,-40f});
            Layout(Stages[5], "근경 열쇠와 높은 선로", new Vector3(-32f,-22f,24f),
                Vector3.zero,new Vector3(36f,15f,-48f));
            Tracks(Stages[5].Rooms[1],3.6f,new[]{24f},new[]{0f});
            Stages[5].Rooms[1].Yaw=0f;
            TopDownTrack(Stages[5].Rooms[1],55f,new[]{0f,70f});

            Layout(Stages[6], "절벽 아래 전구", new Vector3(-16f,8f,22f),Vector3.zero);
            PowerShape(Stages[6].Rooms[0],8.6f);
            Layout(Stages[7], "가까운 저장고와 깊은 회로", new Vector3(-29f,7f,-19f),
                Vector3.zero,Vector3.zero); // The second supply is the charged first terminal.
            PowerShape(Stages[7].Rooms[0],8f);
            PowerShape(Stages[7].Rooms[1],10f);
            Layout(Stages[8], "왼쪽 아래 전력망", new Vector3(31f,-9f,-24f),
                Vector3.zero,new Vector3(-43f,-14f,39f),Vector3.zero);
            Tracks(Stages[8].Rooms[0],3.4f,new[]{7f},new[]{0f});
            PowerShape(Stages[8].Rooms[1],9.2f);
            PowerShape(Stages[8].Rooms[2],8.4f);

            Layout(Stages[9], "세 방향 관찰장", new Vector3(-27f,11f,-22f),
                Vector3.zero,new Vector3(-42f,17f,-26f),new Vector3(34f,-21f,58f));
            Tracks(Stages[9].Rooms[0],3.8f,new[]{15f},new[]{0f});
            Layout(Stages[10], "세 높이의 독립 군집", new Vector3(-31f,-13f,24f),
                Vector3.zero,new Vector3(-48f,23f,-39f),new Vector3(49f,-21f,56f));
            PowerShape(Stages[10].Rooms[0],9f);
            Tracks(Stages[10].Rooms[1],3.4f,new[]{12f},new[]{0f});
            Tracks(Stages[10].Rooms[2],3.4f,new[]{6f,20f},new[]{0f,65f});
            Layout(Stages[11], "두 높이로 갈라지는 폭포", new Vector3(-23f,-8f,-31f),
                Vector3.zero,new Vector3(-39f,18f,-33f),new Vector3(45f,-29f,21f));
            PowerShape(Stages[11].Rooms[0],9f,110f,8f,3.2f);
            CampaignRoomSpec mixedFork=Stages[11].Rooms[0];
            mixedFork.BranchDropOffset=(mixedFork.Depth+mixedFork.BranchDepthOffset)*Mathf.Tan(55f*Mathf.Deg2Rad)-mixedFork.Drop;
            PowerShape(Stages[11].Rooms[2],10f);

            Layout(Stages[12], "가까운 쌍과 먼 전망대", new Vector3(34f,15f,-21f),
                Vector3.zero,new Vector3(23f,-13f,15f),new Vector3(-55f,22f,48f),new Vector3(-24f,-38f,-47f));
            PowerShape(Stages[12].Rooms[1],9.6f);
            Tracks(Stages[12].Rooms[3],3.4f,new[]{9f},new[]{0f});
            Layout(Stages[13], "뒤편 높은 교차 선로", new Vector3(41f,-29f,28f),
                Vector3.zero,new Vector3(39f,-15f,-24f),new Vector3(-47f,-27f,44f),new Vector3(-26f,19f,-63f));
            Tracks(Stages[13].Rooms[0],3.4f,new[]{7f},new[]{0f});
            PowerShape(Stages[13].Rooms[2],10.4f);
            Tracks(Stages[13].Rooms[3],3.6f,new[]{18f,6f},new[]{0f,-60f});
            Layout(Stages[14], "멀리 갈라지는 최종 전력망", new Vector3(29f,11f,-34f),
                Vector3.zero,new Vector3(-44f,-19f,-36f),new Vector3(49f,23f,45f),new Vector3(-59f,-42f,61f));
            Tracks(Stages[14].Rooms[0],3.4f,new[]{16f},new[]{0f});
            PowerShape(Stages[14].Rooms[1],9.2f,85f,12f,4.2f);
            PowerShape(Stages[14].Rooms[3],10.6f);
            Stages[14].Rooms[3].Pitch=55f;
            Stages[14].Rooms[3].Drop=Stages[14].Rooms[3].Depth*Mathf.Tan(55f*Mathf.Deg2Rad);

            // Preserve answer angles while separating even the single-device maps.
            Tutorial.Rooms[0].Depth=7f;
            Stages[0].Rooms[0].Depth=8.5f;
            Stages[1].Rooms[0].Depth=15f;
            Stages[2].Rooms[0].Depth=10f;
            Stages[2].Rooms[1].Depth=23f;
            SpreadPower(Stages[6].Rooms[0],1.65f);
            SpreadPower(Stages[7].Rooms[0],1.6f);
            SpreadPower(Stages[7].Rooms[1],1.6f);
        }

        private static void SpreadPower(CampaignRoomSpec room,float factor)
        {
            room.Depth*=factor;
            room.Drop*=factor;
            room.BranchDepthOffset*=factor;
            room.BranchDropOffset*=factor;
        }

        private static void Layout(CampaignStageSpec stage,string name,Vector3 exit,params Vector3[] sources)
        {
            if(sources.Length!=stage.Rooms.Length)throw new InvalidOperationException("Missing room positions: "+stage.SceneName);
            stage.LayoutName=name;
            stage.ExitOffset=exit;
            for(int i=0;i<sources.Length;i++)stage.Rooms[i].SourceOffset=sources[i];
        }

        private static void Tracks(CampaignRoomSpec room,float length,float[] depths,float[] yawOffsets)
        {
            room.RailLength=length;
            room.RailDepths=depths;
            room.RailJointYawOffsets=yawOffsets;
            room.RailPlanarAngles=new float[room.RailSegments];
            room.TargetSize=new Vector2(5.2f,4.2f);
        }

        private static void TopDownTrack(CampaignRoomSpec room,float pitch,float[] headings)
        {
            room.Pitch=pitch;
            room.RailPlanarAngles=headings;
            room.SourceSize=new Vector2(6.8f,6.8f);
            room.TargetSize=new Vector2(6.8f,6.8f);
        }

        private static void PowerShape(CampaignRoomSpec room,float span,float branchYaw=60f,float branchDepth=1.5f,float branchDrop=0f)
        {
            room.PowerSpan=span;
            room.BranchYawOffset=branchYaw;
            room.BranchDepthOffset=branchDepth;
            room.BranchDropOffset=branchDrop;
        }

        public static IReadOnlyList<CampaignStageSpec> All => Stages;

        public static CampaignStageSpec Get(int number)
        {
            if (number == 0) return Tutorial;
            if (number < 1 || number > Stages.Length) throw new ArgumentOutOfRangeException(nameof(number));
            return Stages[number - 1];
        }

        public static void Validate()
        {
            if (Stages.Length != 15) throw new InvalidOperationException("The campaign must contain exactly fifteen gameplay stages.");
            var names = new HashSet<string> { Tutorial.SceneName };
            for (int i = 0; i < Stages.Length; i++)
            {
                CampaignStageSpec stage = Stages[i];
                if (stage.Number != i + 1 || string.IsNullOrWhiteSpace(stage.SceneName) || !names.Add(stage.SceneName)
                    || string.IsNullOrWhiteSpace(stage.LayoutName) || stage.ExitOffset.sqrMagnitude < 4f)
                    throw new InvalidOperationException("Invalid or duplicate campaign stage at index " + i);
                if (stage.Rooms == null || stage.Rooms.Length < 1 || stage.Rooms.Length > 4)
                    throw new InvalidOperationException("A gameplay stage must have one to four puzzle rooms: " + stage.SceneName);
                bool hasMain = false;
                for (int roomIndex = 0; roomIndex < stage.Rooms.Length; roomIndex++)
                {
                    CampaignRoomSpec room = stage.Rooms[roomIndex];
                    if (room == null || room.Drop <= 0f || room.Depth <= 0f || room.RequiresRoom >= roomIndex || room.RequiresRoom < -1)
                        throw new InvalidOperationException("Invalid room geometry or dependency in " + stage.SceneName);
                    if (room.Branch && room.Kind != CampaignMechanicKind.Power)
                        throw new InvalidOperationException("Only a power room may branch: " + stage.SceneName);
                    if (room.RequiresRoom >= 0 && room.Kind != CampaignMechanicKind.Power)
                        throw new InvalidOperationException("Only power supplies may depend on another room: " + stage.SceneName);
                    if (float.IsNaN(room.Pitch) || float.IsInfinity(room.Pitch) || Mathf.Abs(room.Pitch)>80f)
                        throw new InvalidOperationException("Invalid operating pitch in "+stage.SceneName);
                    if (room.RailSegments < 2 || room.RailSegments > 3)
                        throw new InvalidOperationException("Rail route sections must be two or three.");
                    if(room.Kind==CampaignMechanicKind.Rail && (room.RailLength<2f ||
                        room.RailDepths.Length!=room.RailSegments-1 || room.RailJointYawOffsets.Length!=room.RailSegments-1 ||
                        room.RailPlanarAngles.Length!=room.RailSegments))
                        throw new InvalidOperationException("Each rail joint needs an explicit layout: "+stage.SceneName);
                    foreach(float depth in room.RailDepths)
                        if(depth<=0f)throw new InvalidOperationException("Rail depth must remain positive: "+stage.SceneName);
                    if(room.Kind==CampaignMechanicKind.Power && (room.PowerSpan<=0f ||
                        room.Depth+room.BranchDepthOffset<=0f || room.Drop+room.BranchDropOffset<=0f))
                        throw new InvalidOperationException("Invalid explicit power layout: "+stage.SceneName);
                    hasMain |= room.Kind == stage.MainKind;
                }
                if (!hasMain) throw new InvalidOperationException("Stage has no room for its main mechanic: " + stage.SceneName);
            }
        }

        private static CampaignStageSpec Stage(int number, string sceneName, string title,
            CampaignMechanicKind mainKind, string goal, params CampaignRoomSpec[] rooms)
        {
            for (int i = 0; i < rooms.Length; i++)
            {
                rooms[i].RequiresRoom = rooms[i].Kind == CampaignMechanicKind.Power ? i - 1 : -1;
                rooms[i].SuppliesPower = i + 1 < rooms.Length && rooms[i + 1].Kind == CampaignMechanicKind.Power;
            }
            return new CampaignStageSpec { Number = number, SceneName = sceneName, Title = title,
                MainKind = mainKind, MainMechanic = Name(mainKind), LearningGoal = goal, Rooms = rooms };
        }

        private static CampaignRoomSpec Key(float yaw, float drop, float depth, string role)
            => Room(CampaignMechanicKind.Key, yaw, drop, depth, role);

        private static CampaignRoomSpec Rail(float yaw, float drop, float depth, int segments, string role)
        {
            CampaignRoomSpec room = Room(CampaignMechanicKind.Rail, yaw, drop, depth, role);
            room.RailSegments = segments;
            return room;
        }

        private static CampaignRoomSpec Power(float yaw, float drop, float depth, bool branch, string role)
        {
            CampaignRoomSpec room = Room(CampaignMechanicKind.Power, yaw, drop, depth, role);
            room.Pitch = (float)(Math.Atan2(drop, depth) * 180.0 / Math.PI);
            room.Branch = branch;
            return room;
        }

        private static CampaignRoomSpec Room(CampaignMechanicKind kind, float yaw, float drop, float depth, string role)
            => new CampaignRoomSpec { Kind = kind, Yaw = yaw, Pitch = 0f, Drop = drop, Depth = depth, Role = role };

        private static string Name(CampaignMechanicKind kind) => kind == CampaignMechanicKind.Key
            ? "열쇠" : kind == CampaignMechanicKind.Rail ? "레일" : "전력";
    }
}
