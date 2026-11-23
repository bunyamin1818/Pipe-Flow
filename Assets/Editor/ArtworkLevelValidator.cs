using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using PipeFlow;

namespace PipeFlow.EditorTools
{
    public static class ArtworkLevelValidator
    {
        [MenuItem("Pipe Flow/Validate ArtTexture Exact Artwork")]
        public static void Validate()
        {
            var missing = new List<string>();
            int count = 0;
            System.Action<string> check = path =>
            {
                count++;
                if (Resources.Load<Texture2D>("Art/" + path) == null)
                    missing.Add(path);
            };
            check("Screens/home");
            check("Screens/settingsClean");
            check("UI/PipeFlowLogo");
            for (int z = 0; z < 10; z++)
                for (int segment = 0; segment < 11; segment++)
                    check("Map/RoadTransition/Atlas_" + z + "_" + segment);
            for (int z = 0; z < 10; z++)
            {
                check("Screens/map" + z);
                check("Screens/game" + z);
                check("Screens/boss" + z);
                check("Screens/intro" + z);
                for (int stage = 1; stage <= 11; stage++)
                    check("Map/Road_" + z + "_" + stage);
            }
            for (int s = 0; s < 4; s++) check("Screens/win" + s);
            check("UI/ShopUnlockedBadge");
            foreach (string s in new[] {"fail0", "settings0", "shopClean", "bag0", "chestBackground0"})
                check("Screens/" + s);
            for (int mask = 0; mask < 16; mask++)
            {
                for (int odd = 0; odd < 2; odd++) check("Tiles/Tile_" + mask + "_" + odd);
                for (int dir = 0; dir < 4; dir++)
                {
                    if ((mask & (1 << dir)) == 0) continue;
                    for (int frame = 1; frame <= 8; frame++)
                        check("Tiles/Wet_" + mask + "_" + dir + "_" + frame);
                }
            }
            foreach (string s in new[] {"Stone_0", "Stone_1", "star", "waterstar", "left", "right"})
                check("Tiles/" + s);
            for (int reward = 0; reward < 5; reward++)
                for (int frame = 0; frame < 31; frame++)
                    check("ChestFrames/C" + reward + "_" + frame.ToString("D2"));
            for (int stars = 0; stars <= 15; stars++) check("Map/Meter_" + stars);
            foreach (string s in new[] {"Node_locked_0", "Node_unlocked_0",
                "Node_cleared_0", "Node_cleared_1", "Node_cleared_2", "Node_cleared_3"})
                check("Map/" + s);
            var generator = new PipeLevelGenerator();
            int puzzles = 0;
            for (int level = 1; level <= 120; level++)
            {
                PipePuzzle puzzle = generator.Generate(level);
                if (puzzle.Cells == null || puzzle.Cells.Length != puzzle.Size * puzzle.Size ||
                    puzzle.Start < 0 || puzzle.Exit >= puzzle.Cells.Length)
                    missing.Add("Invalid puzzle: " + level);
                else puzzles++;
            }
            if (missing.Count > 0)
            {
                Debug.LogError("Pipe Flow ArtTexture: " + missing.Count + " missing or invalid entries:\n" + string.Join("\n", missing));
                EditorUtility.DisplayDialog("Pipe Flow ArtTexture", missing.Count + " missing art/puzzle entries. Open Console.", "OK");
            }
            else
            {
                Debug.Log("Pipe Flow ArtTexture audit: " + count + " original art textures imported; " + puzzles + " puzzles generated successfully.");
                EditorUtility.DisplayDialog("Pipe Flow ArtTexture", count + " original visuals imported; " + puzzles + " puzzles validated.", "OK");
            }
        }
    }
}
