# Authors scene assets only. It does not start Unity, enter Play mode, or build a player.
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repo 'My project'
. (Join-Path $PSScriptRoot 'UnitySceneSerialization.ps1')
$tutorialPath = Join-Path $project 'Assets/_Game/Scenes/ProjectionPrototype.unity'
$tutorial = [IO.File]::ReadAllText($tutorialPath)
$tutorialHash = (Get-FileHash -LiteralPath $tutorialPath -Algorithm SHA256).Hash
$directory = Join-Path $project 'Assets/_Game/Scenes/Stages'
[void][IO.Directory]::CreateDirectory($directory)
if (-not (Test-Path -LiteralPath ($directory + '.meta'))) {
    [IO.File]::WriteAllText(($directory + '.meta'), "fileFormatVersion: 2`nguid: $([Guid]::NewGuid().ToString('N'))`nfolderAsset: yes`nDefaultImporter:`n  externalObjects: {}`n  userData: `n  assetBundleName: `n  assetBundleVariant: `n")
}
function Material-Guid($name) {
    $meta = Join-Path $project ('Assets/_Game/Materials/' + $name + '.mat.meta')
    return ([regex]::Match([IO.File]::ReadAllText($meta), 'guid: (\w+)')).Groups[1].Value
}
$floorMaterial = Material-Guid 'PrototypePlatformA'
$hubMaterial = Material-Guid 'PrototypePlatformB'
$exitMaterial = Material-Guid 'PrototypeExit'
$scriptGuid = ([regex]::Match([IO.File]::ReadAllText((Join-Path $project 'Assets/_Game/Scripts/ProjectableBox.cs.meta')), 'guid: (\w+)')).Groups[1].Value
$routeGuid = '6cafd93458b64a2fb072ec13819d6e42'
$floorColor = '{r: 0.13, g: 0.63, b: 0.7, a: 1}'
$hubColor = '{r: 0.94, g: 0.57, b: 0.22, a: 1}'
$unit = '{x: 1, y: 1, z: 1}'
$zero = '{x: 0, y: 0, z: 0}'
$stages = @(
    @{ Name='Stage01_RelayBend'; Title='01 - Relay Bend'; Steps=4; Zoom=6; Points=@(
        @(0,0,1,0), @(24,8,0,1), @(8,30,-1,0), @(-16,12,0,-1), @(8,-12,0,0)) },
    @{ Name='Stage02_SpiralAscent'; Title='02 - Spiral Ascent'; Steps=6; Zoom=6.5; Points=@(
        @(0,0,1,0), @(28,6,0,1), @(34,32,-1,0), @(6,42,0,-1), @(-18,18,1,0), @(-2,-10,0,1), @(25,-28,0,0)) },
    @{ Name='Stage03_Serpentine'; Title='03 - Serpentine'; Steps=4; Zoom=6.5; Points=@(
        @(0,0,1,0), @(24,6,0,1), @(26,34,-1,0), @(2,40,-1,0), @(-24,34,0,-1), @(-20,4,1,0), @(4,-18,1,0), @(32,-18,0,0)) },
    @{ Name='Stage04_CrossingHeights'; Title='04 - Crossing Heights'; Steps=6; Zoom=7; Points=@(
        @(0,0,1,0), @(26,8,0,1), @(2,30,-1,0), @(-24,8,0,-1), @(0,-16,1,0), @(28,16,-1,0), @(8,42,0,0)) },
    @{ Name='Stage05_LongTraverse'; Title='05 - Long Traverse'; Steps=6; Zoom=7; Points=@(
        @(0,0,1,0), @(26,6,0,1), @(40,32,-1,0), @(14,48,-1,0), @(-14,40,0,-1), @(-32,12,1,0), @(-12,-16,0,-1), @(16,-32,1,0), @(46,-10,0,0)) }
)
function Vec($x,$y,$z) { return ('{x: '+([string][double]$x)+', y: '+([string][double]$y)+', z: '+([string][double]$z)+'}') }
function New-Node($name,$parent,$position,$scale,$material='',$platformId=0,$color='',$sun=$false) {
    $node=[pscustomobject]@{ Name=$name; Parent=$parent; Position=$position; Scale=$scale; Material=$material; PlatformId=$platformId; Color=$color; Sun=$sun; Id=$script:nextId; Transform=$script:nextId+1; Mesh=$script:nextId+2; Renderer=$script:nextId+3; Collider=$script:nextId+4; Behaviour=$script:nextId+5; Light=$script:nextId+6 }
    $script:nextId += 10
    $script:nodes.Add($node)
    return $node
}
function Add-Floor($name,$parent,$x,$top,$z,$width,$depth,$hub=$false) {
    $script:floorId++
    $material=if($hub){$hubMaterial}else{$floorMaterial}
    $color=if($hub){$hubColor}else{$floorColor}
    return New-Node $name $parent (Vec $x ($top-0.5) $z) (Vec $width 1 $depth) $material $script:floorId $color
}
$manifest=[Collections.Generic.List[object]]::new()
foreach($stage in $stages) {
    $path=Join-Path $directory ($stage.Name+'.unity')
    if(Test-Path -LiteralPath $path){ throw ('Preserving existing stage; file already exists: '+$path) }
    $script:nextId=200000000
    $script:floorId=0
    $script:nodes=[Collections.Generic.List[object]]::new()
    $checkpoints=[Collections.Generic.List[object]]::new()
    $map=New-Node 'Map' 482666092 $zero $unit
    $platforms=New-Node 'Platforms' $map.Transform $zero $unit
    $points=$stage.Points
    $rise=0.55*$stage.Steps
    for($i=0;$i -lt $points.Count;$i++) {
        $p=$points[$i]; $x=[double]$p[0]; $z=[double]$p[1]; $h=$i*$rise
        $section=New-Node ('Section '+('{0:D2}' -f $i)) $platforms.Transform $zero $unit
        [void](Add-Floor ('Landing '+('{0:D2}' -f $i)) $section.Transform $x $h $z 4.6 4.6 $true)
        if($i -gt 0 -and $i -lt $points.Count-1) {
            $cp=New-Node ('Checkpoint '+('{0:D2}' -f $i)) $section.Transform (Vec $x $h $z) $unit
            $checkpoints.Add($cp)
            [void](New-Node 'Landing Pad' $cp.Transform '{x: 0, y: 0.045, z: 0}' '{x: 1.2, y: 0.06, z: 1.2}' $exitMaterial)
            [void](New-Node 'Beacon' $cp.Transform '{x: 0, y: 0.9, z: 0}' '{x: 0.12, y: 1.8, z: 0.12}' $exitMaterial)
            for($dot=0;$dot -lt $i;$dot++) {
                [void](New-Node ('Index '+($dot+1)) $cp.Transform (Vec (($dot-($i-1)*0.5)*0.22) 1.9 0) '{x: 0.12, y: 0.2, z: 0.12}' $exitMaterial)
            }
        }
        if($i -eq $points.Count-1){continue}
        $dx=[double]$p[2]; $dz=[double]$p[3]
        for($step=1;$step -le $stage.Steps;$step++) {
            $distance=3+1.4*($step-1)
            [void](Add-Floor ('Riser '+('{0:D2}' -f $step)) $section.Transform ($x+$dx*$distance) ($h+0.55*$step) ($z+$dz*$distance) 1.9 1.9)
        }
        $distance=3+1.4*($stage.Steps-1)+1.6
        [void](Add-Floor 'Projection Overlook' $section.Transform ($x+$dx*$distance) ($h+$rise) ($z+$dz*$distance) 3 3)
        # Short, spaced side terraces on the later maps add route choices without filling the void.
        if($stage.Name -match 'Stage0[345]' -and $i -gt 0 -and $i%2 -eq 1) {
            [void](Add-Floor 'Side Terrace' $section.Transform ($x-$dz*3.8) $h ($z+$dx*3.8) 3.2 3.2)
        }
    }
    $spawn=New-Node 'Spawn Point' $map.Transform '{x: -0.7, y: 0.02, z: 0}' $unit
    $focus=New-Node 'Camera Focus' $map.Transform '{x: 2, y: 1.2, z: 1}' $unit
    $last=$points[$points.Count-1]; $goalHeight=($points.Count-1)*$rise
    $exit=New-Node 'Exit Point' $map.Transform (Vec $last[0] $goalHeight $last[1]) $unit
    [void](New-Node 'Exit Marker' $exit.Transform '{x: 0, y: 0.07, z: 0}' '{x: 1.1, y: 0.1, z: 1.1}' $exitMaterial)
    [void](New-Node 'Exit Beacon' $exit.Transform '{x: 0, y: 1.1, z: 0}' '{x: 0.16, y: 2.2, z: 0.16}' $exitMaterial)
    $sun=New-Node 'Stage Sun' $map.Transform $zero $unit '' 0 '' $true

    $render=[regex]::Match($tutorial,'(?ms)^%YAML.*?(?=^--- !u!1 &482666090)').Value
    $render=[regex]::Replace($render,'m_Sun: \{fileID: \d+\}',('m_Sun: {fileID: '+$sun.Light+'}'))
    $rootObject=[regex]::Match($tutorial,'(?ms)^--- !u!1 &482666090\r?\n.*?(?=^---|\z)').Value
    $rootObject=$rootObject.Replace('m_Name: Projection Prototype',('m_Name: '+$stage.Title))
    $rootObject=$rootObject.Replace('  - component: {fileID: 482666091}',"  - component: {fileID: 482666091}`n  - component: {fileID: 600000005}")
    $rootComponent=[regex]::Match($tutorial,'(?ms)^--- !u!114 &482666091\r?\n.*?(?=^---|\z)').Value
    foreach($pair in @(@('MapRoot',$map.Transform),@('SpawnPoint',$spawn.Transform),@('ExitPoint',$exit.Transform),@('CameraFocusPoint',$focus.Transform))) {
        $rootComponent=[regex]::Replace($rootComponent,('(?m)^  '+$pair[0]+':.*$'),('  '+$pair[0]+': {fileID: '+$pair[1]+'}'))
    }
    foreach($setting in @('Route','initialZoomSize','minZoomSize','maxZoomSize')) {
        $rootComponent=[regex]::Replace($rootComponent,('(?m)^  '+$setting+':[^\r\n]*\r?\n'),'')
    }
    $rootComponent=$rootComponent.TrimEnd()+"`n  Route: {fileID: 600000005}`n  initialZoomSize: $($stage.Zoom)`n  minZoomSize: 1.5`n  maxZoomSize: 50`n"
    $rootTransform=@"
--- !u!4 &482666092
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 482666090}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: $zero
  m_LocalScale: $unit
  m_ConstrainProportionsScale: 0
  m_Children:
  - {fileID: $($map.Transform)}
  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: $zero

"@
    $cpReferences=($checkpoints|ForEach-Object{'  - {fileID: '+$_.Transform+'}'})-join "`n"
    $route=@"
--- !u!114 &600000005
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 482666090}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: $routeGuid, type: 3}
  m_Name:
  m_EditorClassIdentifier:
  Prototype: {fileID: 482666091}
  StageTitle: $($stage.Title)
  Checkpoints:
$cpReferences
  ActivationRadius: 1.15
  HeightTolerance: 0.2
  PendingColor: {r: 0.25, g: 0.42, b: 0.52, a: 1}
  CurrentColor: {r: 1, g: 0.72, b: 0.2, a: 1}
  CompletedColor: {r: 0.3, g: 0.9, b: 0.6, a: 1}

"@
    # Keep the designer's remaining HUD and EventSystem, excluding the tutorial map and extra camera.
    $ui=[regex]::Matches($tutorial,'(?ms)^--- !u!\d+ &(\d+)\r?\n.*?(?=^---|\z)') |
        Where-Object { $id=[long]$_.Groups[1].Value; ($id -ge 9000000000 -and $id -lt 9100000000) -or ($id -ge 1226013844 -and $id -le 1226013847) } |
        ForEach-Object{$_.Value}
    $nodeYaml=ConvertTo-UnityNodeYaml $script:nodes $project $scriptGuid $tutorial
    $scene=$render+$rootObject+$rootComponent+$rootTransform+$route+$nodeYaml+($ui -join '')+@"
--- !u!1660057539 &9223372036854775807
SceneRoots:
  m_ObjectHideFlags: 0
  m_Roots:
  - {fileID: 482666092}
  - {fileID: 1226013847}
  - {fileID: 9000000001}
"@
    [IO.File]::WriteAllText($path,$scene)
    $guid=[Guid]::NewGuid().ToString('N')
    [IO.File]::WriteAllText(($path+'.meta'),"fileFormatVersion: 2`nguid: $guid`nDefaultImporter:`n  externalObjects: {}`n  userData: `n  assetBundleName: `n  assetBundleVariant: `n")
    $manifest.Add([pscustomobject]@{Name=$stage.Name; Path=('Assets/_Game/Scenes/Stages/'+$stage.Name+'.unity'); Guid=$guid; Platforms=$script:floorId; Checkpoints=$checkpoints.Count; Height=$goalHeight})
}
$buildPath=Join-Path $project 'ProjectSettings/EditorBuildSettings.asset'
$build=[IO.File]::ReadAllText($buildPath)
$extra=($manifest|ForEach-Object{"  - enabled: 1`n    path: $($_.Path)`n    guid: $($_.Guid)`n"})-join ''
$build=$build.Replace('  m_configObjects:',($extra+'  m_configObjects:'))
[IO.File]::WriteAllText($buildPath,$build)
if((Get-FileHash -LiteralPath $tutorialPath -Algorithm SHA256).Hash -ne $tutorialHash){throw 'Tutorial changed unexpectedly.'}
$manifest|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $PSScriptRoot 'ChallengeStages.json') -Encoding utf8
$manifest|Format-Table Name,Platforms,Checkpoints,Height
