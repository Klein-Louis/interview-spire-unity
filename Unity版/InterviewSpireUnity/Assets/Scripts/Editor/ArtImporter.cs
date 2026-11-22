#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace InterviewSpire.Editor
{
    /// <summary>把从 Python 版复制过来的 PNG 统一导入为 Sprite。</summary>
    public class ArtImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Resources/Art/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 100;
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Bilinear;
            ti.alphaIsTransparency = true;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
        }

        [MenuItem("Tools/Interview Spire/Reimport Art")]
        public static void ReimportArt()
        {
            AssetDatabase.ImportAsset("Assets/Resources/Art", ImportAssetOptions.ForceUpdate |
                                      ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("美术资源已按 Sprite 重新导入。");
        }
    }
}
#endif
