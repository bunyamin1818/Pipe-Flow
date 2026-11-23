using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PipeFlow.EditorTools
{
    public static class OfflineArtworkValidator
    {
        [MenuItem("Pipe Flow/Validate Offline Artwork")]
        public static void Check()
        {
            string root = "Assets/Resources/RawArt";
            string[] required = {
                "Art/Menu/en_play", "Art/Menu/tr_play", "Art/Screens/home",
                "Art/Screens/game0", "Art/Screens/map0", "Art/Tiles/Tile_3_0",
                "Images/Chests/Closed", "Images/Chests/Open", "Images/Items/Item0"
            };
            int missing = 0;
            foreach (string path in required)
            {
                string asset = root + "/" + path + ".bytes";
                if (!File.Exists(asset)) { Debug.LogError("Missing offline art: " + asset); missing++; continue; }
                TextAsset loaded = Resources.Load<TextAsset>("RawArt/" + path);
                if (loaded == null || loaded.bytes.Length == 0)
                { Debug.LogError("Failed to load offline art: " + path); missing++; }
            }
            if (missing == 0)
            {
                Debug.Log("Pipe Flow: All required offline artwork loaded successfully.");
                EditorUtility.DisplayDialog("Pipe Flow", "Offline artwork ready. Open Main.unity and press Play.", "OK");
            }
            else EditorUtility.DisplayDialog("Pipe Flow", missing + " offline art files failed. See Console.", "OK");
        }
    }
}
