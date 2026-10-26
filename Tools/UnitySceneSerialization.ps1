function ConvertTo-UnityNodeYaml {
param($nodes, $project, $scriptGuid, $text)
$unit = '{x: 1, y: 1, z: 1}'
$zero = '{x: 0, y: 0, z: 0}'
$blocks = [Text.StringBuilder]::new()
$baseFields = "  m_ObjectHideFlags: 0`n  m_CorrespondingSourceObject: {fileID: 0}`n  m_PrefabInstance: {fileID: 0}`n  m_PrefabAsset: {fileID: 0}"
foreach ($node in $nodes) {
    if ($text -match ('&' + $node.Id + '\b')) { throw 'Generated object ID collision.' }
    $components = "  - component: {fileID: $($node.Transform)}"
    if ($node.Material) { $components += "`n  - component: {fileID: $($node.Mesh)}`n  - component: {fileID: $($node.Renderer)}" }
    if ($node.PlatformId) { $components += "`n  - component: {fileID: $($node.Collider)}`n  - component: {fileID: $($node.Behaviour)}" }
    if ($node.Sun) { $components += "`n  - component: {fileID: $($node.Light)}" }
    $children = @($nodes | Where-Object Parent -EQ $node.Transform)
    $childText = if ($children.Count) { "  m_Children:`n" + (($children | ForEach-Object { "  - {fileID: $($_.Transform)}" }) -join "`n") } else { '  m_Children: []' }
    $rotation = if ($node.Sun) { '{x: 0.2904595, y: -0.246840, z: 0.077828, w: 0.9212198}' } else { '{x: 0, y: 0, z: 0, w: 1}' }
    [void]$blocks.Append(@"
--- !u!1 &$($node.Id)
GameObject:
$baseFields
  serializedVersion: 6
  m_Component:
$components
  m_Layer: 8
  m_Name: $($node.Name)
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &$($node.Transform)
Transform:
$baseFields
  m_GameObject: {fileID: $($node.Id)}
  serializedVersion: 2
  m_LocalRotation: $rotation
  m_LocalPosition: $($node.Position)
  m_LocalScale: $($node.Scale)
  m_ConstrainProportionsScale: 0
$childText
  m_Father: {fileID: $($node.Parent)}
  m_LocalEulerAnglesHint: $zero

"@)
    if ($node.Material) {
        [void]$blocks.Append(@"
--- !u!33 &$($node.Mesh)
MeshFilter:
$baseFields
  m_GameObject: {fileID: $($node.Id)}
  m_Mesh: {fileID: 10202, guid: 0000000000000000e000000000000000, type: 0}
--- !u!23 &$($node.Renderer)
MeshRenderer:
$baseFields
  m_GameObject: {fileID: $($node.Id)}
  m_Enabled: 1
  m_CastShadows: 1
  m_ReceiveShadows: 1
  m_DynamicOccludee: 1
  m_MotionVectors: 1
  m_LightProbeUsage: 1
  m_ReflectionProbeUsage: 1
  m_RayTracingMode: 2
  m_RenderingLayerMask: 1
  m_RendererPriority: 0
  m_Materials:
  - {fileID: 2100000, guid: $($node.Material), type: 2}
  m_StaticBatchInfo:
    firstSubMesh: 0
    subMeshCount: 0
  m_StaticBatchRoot: {fileID: 0}
  m_ProbeAnchor: {fileID: 0}
  m_LightProbeVolumeOverride: {fileID: 0}
  m_ScaleInLightmap: 1
  m_ReceiveGI: 1
  m_PreserveUVs: 0
  m_IgnoreNormalsForChartDetection: 0
  m_ImportantGI: 0
  m_StitchLightmapSeams: 1
  m_SelectedEditorRenderState: 3
  m_MinimumChartSize: 4
  m_AutoUVMaxDistance: 0.5
  m_AutoUVMaxAngle: 89
  m_LightmapParameters: {fileID: 0}
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 0
  m_AdditionalVertexStreams: {fileID: 0}

"@)
    }
    if ($node.PlatformId) {
        [void]$blocks.Append(@"
--- !u!65 &$($node.Collider)
BoxCollider:
$baseFields
  m_GameObject: {fileID: $($node.Id)}
  m_Material: {fileID: 0}
  m_IncludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ExcludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_LayerOverridePriority: 0
  m_IsTrigger: 0
  m_ProvidesContacts: 0
  m_Enabled: 1
  serializedVersion: 3
  m_Size: $unit
  m_Center: $zero
--- !u!114 &$($node.Behaviour)
MonoBehaviour:
$baseFields
  m_GameObject: {fileID: $($node.Id)}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: $scriptGuid, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  id: $($node.PlatformId)
  bodyColor: $($node.Color)

"@)
    }
    if ($node.Sun) {
        $sample = [IO.File]::ReadAllText((Join-Path $project 'Assets/Scenes/SampleScene.unity'))
        $lightBlock = [regex]::Match($sample, '(?ms)^--- !u!108 &\d+\r?\n.*?(?=^---|\z)').Value
        if (-not $lightBlock) { throw 'Template directional light data missing.' }
        $lightBlock = [regex]::Replace($lightBlock, '^--- !u!108 &\d+', ('--- !u!108 &' + $node.Light))
        $lightBlock = [regex]::Replace($lightBlock, 'm_GameObject: \{fileID: \d+\}', ('m_GameObject: {fileID: ' + $node.Id + '}'))
        $lightBlock = [regex]::Replace($lightBlock, 'm_Intensity: [^\r\n]+', 'm_Intensity: 1.7')
        $lightBlock = [regex]::Replace($lightBlock, '(m_Shadows:\r?\n    m_Type:) \d+', '$1 0')
        [void]$blocks.Append($lightBlock.TrimEnd() + "`n")
    }
}

return $blocks.ToString()
}