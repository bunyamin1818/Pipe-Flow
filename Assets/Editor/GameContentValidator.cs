using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PipeFlow.EditorTools
{
    public static class GameContentValidator
    {
        [MenuItem("Pipe Flow/Validate Game Content")]
        public static void Validate()
        {
            var missing = new List<string>();
            foreach (string language in new[] { "en", "tr", "de", "fr", "es" })
            {
                string key = "Art/Menu/" + language + "_play";
                if (Resources.Load<Texture2D>(key) == null) missing.Add(key);
            }
            for (int i = 0; i < 16; i++)
            {
                string key = "Images/Tiles/PipeOverlay_" + i;
                if (Resources.Load<Texture2D>(key) == null) missing.Add(key);
            }
            foreach (int world in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 })
            {
                string key = "Art/Screens/map" + world;
                if (Resources.Load<Texture2D>(key) == null) missing.Add(key);
            }
            foreach (string asset in new[] { "Art/Screens/shopClean", "Art/UI/ShopUnlockedBadge",
                "Images/UI/SmallButton_gold", "Images/UI/SmallButton_dark", "Images/UI/Icon_lock" })
                if (Resources.Load<Texture2D>(asset) == null) missing.Add(asset);
            if (Resources.Load<TextAsset>("Data/Worlds") == null) missing.Add("Data/Worlds");
            if (Resources.Load<TextAsset>("Data/RoadTransitionMeta") == null) missing.Add("Data/RoadTransitionMeta");
            string message = missing.Count == 0
                ? "Game asset paths validated. Run Play Mode to verify runtime behavior."
                : "Missing " + missing.Count + " resources:\n" + string.Join("\n", missing);
            if (missing.Count > 0) Debug.LogError(message);
            else Debug.Log(message);
            EditorUtility.DisplayDialog("Pipe Flow", message, "OK");
        }
    }
}
