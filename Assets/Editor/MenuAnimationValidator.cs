using System.IO;
using UnityEditor;
using UnityEngine;

namespace PipeFlow.EditorTools
{
    public static class MenuAnimationValidator
    {
        [MenuItem("Pipe Flow/Validate Menu and Animations")]
        public static void Validate()
        {
            int missing = 0;
            int verified = 0;
            string[] languages = { "en", "tr", "de", "fr", "es" };
            string[] buttons = { "play", "map", "shop", "bag" };
            Check("Images/Home", ref verified, ref missing);
            Check("Art/Menu/PipeFlowLogo", ref verified, ref missing);
            foreach (string lang in languages)
                foreach (string button in buttons)
                    Check("Art/Menu/" + lang + "_" + button, ref verified, ref missing);
            for (int mask = 1; mask < 16; mask++)
                for (int frame = 0; frame < 12; frame++)
                    Check("Art/RotationFrames/R_" + mask + "_" + frame.ToString("D2"), ref verified, ref missing);
            for (int zone = 0; zone < 10; zone++)
                for (int path = 0; path < 11; path++)
                    Check("Art/Map/RoadTransition/Atlas_" + zone + "_" + path, ref verified, ref missing);
            if (missing == 0)
            {
                Debug.Log($"Pipe Flow: {verified} interactive-menu and animation assets verified.");
                EditorUtility.DisplayDialog("Pipe Flow", $"{verified} assets ready. Press Play to test.", "OK");
            }
            else
                EditorUtility.DisplayDialog("Pipe Flow", $"{missing} missing assets. Check the Console.", "OK");
        }

        private static void Check(string key, ref int verified, ref int missing)
        {
            string png = "Assets/Resources/" + key + ".png";
            if (!File.Exists(png))
            {
                missing++;
                Debug.LogError("Missing artwork: " + key);
            }
            else verified++;
        }
    }
}
