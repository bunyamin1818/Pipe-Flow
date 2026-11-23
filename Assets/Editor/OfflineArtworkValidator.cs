using System.IO;
using UnityEditor;
using UnityEngine;

namespace PipeFlow.EditorTools
{
    public static class OfflineArtworkValidator
    {
        [MenuItem("Pipe Flow/Validate Artwork")]
        public static void Check()
        {
            string root = "Assets/Resources";
            string[] required = {
                "Art/Menu/en_play", "Art/Menu/tr_play", "Art/Screens/home",
                "Art/Screens/game0", "Art/Screens/map0", "Art/Tiles/Tile_3_0",
                "Images/Chests/Closed", "Images/Chests/Open", "Images/Items/Item0"
            };
            int missing = 0;
            foreach (string path in required)
            {
                string asset = root + "/" + path + ".png";
                if (!File.Exists(asset)) { Debug.LogError("Missing artwork: " + asset); missing++; }
            }
            if (missing == 0)
            {
                Debug.Log("Pipe Flow: All required artwork is present.");
                EditorUtility.DisplayDialog("Pipe Flow", "Artwork ready. Open Main.unity and press Play.", "OK");
            }
            else EditorUtility.DisplayDialog("Pipe Flow", missing + " artwork files are missing. See Console.", "OK");
        }
    }
}
