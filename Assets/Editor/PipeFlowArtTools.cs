using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PipeFlow.EditorTools
{
    public static class PipeFlowArtTools
    {
        [MenuItem("Pipe Flow/Reimport Original ArtTexture Artwork")]
        public static void Reimport()
        {
            string root = "Assets/Resources/Art";
            var ids = AssetDatabase.FindAssets("t:Texture2D", new[] { root });
            foreach (string id in ids)
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(id), ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Pipe Flow", "Reimported " + ids.Length + " artwork textures. Run Validate Levels and Assets next.", "OK");
        }

        [MenuItem("Pipe Flow/Inspect Original Artwork")]
        public static void Preview()
        {
            PipeFlowArtGallery window = EditorWindow.GetWindow<PipeFlowArtGallery>("Original ArtTexture Art");
            window.minSize = new Vector2(430, 500);
            window.Show();
        }
    }

    public sealed class PipeFlowArtGallery : EditorWindow
    {
        private Vector2 scroll;
        private readonly string[] previewPaths =
        {
            "Art/Screens/home", "Art/Screens/map0", "Art/Screens/game0",
            "Art/Tiles/Tile_3_0", "Art/Tiles/Tile_5_1", "Art/Tiles/Tile_10_0",
            "Art/Tiles/Wet_5_0_8", "Art/Tiles/left", "Art/Tiles/right",
            "Art/ChestFrames/C0_00", "Art/ChestFrames/C0_20",
            "Art/Map/Road_0_4", "Art/Map/Meter_0", "Art/Map/Meter_15"
        };

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Pipe Flow artwork", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("These are the actual textures Unity loads. If anything is missing, use Pipe Flow > Reimport Original ArtTexture Artwork.", MessageType.Info);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (string path in previewPaths)
            {
                var texture = Resources.Load<Texture2D>(path);
                EditorGUILayout.Space(9);
                EditorGUILayout.LabelField(path, EditorStyles.boldLabel);
                if (texture == null)
                {
                    EditorGUILayout.HelpBox("MISSING TEXTURE", MessageType.Error);
                    continue;
                }
                float width = Mathf.Min(365f, texture.width);
                float height = width * texture.height / texture.width;
                Rect r = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height));
                EditorGUI.DrawPreviewTexture(r, texture, null, ScaleMode.ScaleToFit);
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
