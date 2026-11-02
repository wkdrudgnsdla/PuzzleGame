using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace PuzzleGame.Editor
{
    /// <summary>Persistent Korean TMP fonts for the HUD.</summary>
    public static class KoreanUiFonts
    {
        public const string SourcePath="Assets/_Game/Fonts/NotoSansCJKkr-Regular.otf";
        public const string TmpPath="Assets/_Game/Fonts/NotoSansCJKkr-Regular SDF.asset";
        public static Font SourceFont => AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
        public static TMP_FontAsset FontAsset => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpPath);

        [MenuItem("Puzzle Game/UI/Configure Korean Fonts")]
        public static void Configure()
        {
            AssetDatabase.Refresh();
            Font source=SourceFont;
            if(source==null)throw new InvalidOperationException("Missing Korean source font: "+SourcePath);
            var importer=(TrueTypeFontImporter)AssetImporter.GetAtPath(SourcePath);
            if(!importer.includeFontData){importer.includeFontData=true;importer.SaveAndReimport();source=SourceFont;}
            TMP_FontAsset font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpPath);
            if(font==null)
            {
                font=TMP_FontAsset.CreateFontAsset(source,64,8,GlyphRenderMode.SDFAA,1024,1024,
                    AtlasPopulationMode.Dynamic,true);
                if(font==null)throw new InvalidOperationException("Could not create Korean TMP font");
                font.name="NotoSansCJKkr-Regular SDF";
                AssetDatabase.CreateAsset(font,TmpPath);
                AssetDatabase.AddObjectToAsset(font.material,font);
                foreach(Texture2D texture in font.atlasTextures)AssetDatabase.AddObjectToAsset(texture,font);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(TmpPath);
                font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpPath);
            }
            // The public mode setter clears the source when the editor-only cache has
            // not been initialized after import. Persist the real source explicitly.
            var fontData=new SerializedObject(font);
            fontData.FindProperty("m_AtlasPopulationMode").intValue=(int)AtlasPopulationMode.Dynamic;
            fontData.FindProperty("m_SourceFontFile").objectReferenceValue=source;
            fontData.FindProperty("m_SourceFontFileGUID").stringValue=AssetDatabase.AssetPathToGUID(SourcePath);
            fontData.ApplyModifiedPropertiesWithoutUndo();
            font.isMultiAtlasTexturesEnabled=true;
            if(font.sourceFontFile!=source)throw new InvalidOperationException("Korean TMP asset source font mismatch");
            string warmup=new string(Enumerable.Range(32,95).Select(c=>(char)c).ToArray())+
                "한글 테스트 조작 이동 점프 화면 회전 확대 축소 설정 클리어 조건 좌우 상하 다시 시작 전환 복귀 완료 스테이지 튜토리얼 다음 전구 레일 열쇠 가나다라마 힣 ㄱㄴㅏㅑ °↔→↑";
            if(!font.TryAddCharacters(warmup,out string missing))
                throw new InvalidOperationException("Korean UI glyphs missing: "+missing);
            if(font.atlasTexture.width<=1 || !font.characterLookupTable.ContainsKey('한'))
                throw new InvalidOperationException("Korean TMP atlas was not populated");
            if(!AssetDatabase.Contains(font.material))AssetDatabase.AddObjectToAsset(font.material,font);
            foreach(Texture2D texture in font.atlasTextures)
            {
                if(texture==null)continue;
                if(!AssetDatabase.Contains(texture))AssetDatabase.AddObjectToAsset(texture,font);
                EditorUtility.SetDirty(texture);
            }
            EditorUtility.SetDirty(font);

            TMP_Settings settings=TMP_Settings.instance;
            if(settings==null)throw new InvalidOperationException("Import TMP Essential Resources first");
            var serialized=new SerializedObject(settings);
            SerializedProperty defaultFont=serialized.FindProperty("m_defaultFontAsset");
            if(defaultFont.objectReferenceValue==null || defaultFont.objectReferenceValue.name.StartsWith("LiberationSans"))
                defaultFont.objectReferenceValue=font;
            SerializedProperty fallbacks=serialized.FindProperty("m_fallbackFontAssets");
            bool contains=false;
            for(int i=0;i<fallbacks.arraySize;i++)if(fallbacks.GetArrayElementAtIndex(i).objectReferenceValue==font)contains=true;
            if(!contains){int index=fallbacks.arraySize;fallbacks.arraySize++;fallbacks.GetArrayElementAtIndex(index).objectReferenceValue=font;}
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            // Coverage checks use the source face without filling thousands of atlas slots.
            if(FontEngine.LoadFontFace(source,64)!=FontEngineError.Success)throw new InvalidOperationException("Korean face could not load");
            int unsupported=0;
            for(uint unicode=0xAC00;unicode<=0xD7A3;unicode++)
                if(!FontEngine.TryGetGlyphWithUnicodeValue(unicode,GlyphLoadFlags.LOAD_NO_BITMAP,out _))unsupported++;
            if(unsupported>0)throw new InvalidOperationException("Missing Hangul syllables: "+unsupported);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source,out string sourceGuid,out long sourceId);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(font,out string tmpGuid,out long tmpId);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(font.material,out string materialGuid,out long materialId);
            string report=JsonUtility.ToJson(new FontReport{SourceGuid=sourceGuid,SourceId=sourceId,
                TmpGuid=tmpGuid,TmpId=tmpId,MaterialId=materialId},true);
            string reportPath=Path.GetFullPath(Path.Combine(Application.dataPath,"../Builds/KoreanUiFontReport.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath,report);
            Debug.Log("KOREAN UI FONTS CONFIGURED: 11,172 Hangul syllables covered; dynamic multi-atlas TMP, project default and fallback; no Play Mode.");
        }

        [Serializable] private sealed class FontReport
        {
            public string SourceGuid,TmpGuid;
            public long SourceId,TmpId,MaterialId;
        }
    }
}
