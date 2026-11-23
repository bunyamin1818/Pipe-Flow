using UnityEditor;
using UnityEngine;

namespace PipeFlow.EditorTools
{
    public sealed class PipeFlowAssetImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/Images/") &&
                !assetPath.StartsWith("Assets/Resources/Art/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            bool large = assetPath.Contains("/Art/Menu/") ||
                         assetPath.Contains("/Art/RotationFrames/") ||
                         assetPath.Contains("/Art/Map/RoadTransition/") ||
                         assetPath.Contains("/Art/Screens/") ||
                         assetPath.Contains("/Images/Maps/") || assetPath.EndsWith("/Images/Home.png");
            importer.textureCompression = large ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Compressed;
            importer.maxTextureSize = 2048;
            importer.isReadable = false;
        }
    }
}
