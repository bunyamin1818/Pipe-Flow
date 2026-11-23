using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PipeFlow.EditorTools
{
    public static class PipeFlowValidation
    {
        [MenuItem("Pipe Flow/Validate Levels and Assets")]
        public static void Run()
        {
            int failures = 0;
            var generator = new PipeLevelGenerator();
            for (int level = 1; level <= 120; level++)
            {
                PipePuzzle puzzle = generator.Generate(level);
                if (puzzle.Cells == null || puzzle.Cells.Length != puzzle.Size * puzzle.Size ||
                    puzzle.Route == null || puzzle.Route.Count < puzzle.Size ||
                    !puzzle.Cells[puzzle.Start].Fixed || !puzzle.Cells[puzzle.Exit].Fixed) { failures++; continue; }
                bool routeValid = true;
                for (int i = 0; i < puzzle.Route.Count - 1; i++)
                {
                    int a = puzzle.Route[i], b = puzzle.Route[i + 1];
                    int dx = b % puzzle.Size - a % puzzle.Size;
                    int dy = b / puzzle.Size - a / puzzle.Size;
                    int direction = dx == 1 ? 1 : dx == -1 ? 3 : dy == 1 ? 2 : 0;
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) != 1 ||
                        !PipeLevelGenerator.Has(puzzle.Cells[a].Correct, direction) ||
                        !PipeLevelGenerator.Has(puzzle.Cells[b].Correct, (direction + 2) % 4)) routeValid = false;
                }
                if (!routeValid) failures++;
            }
            foreach (string name in new[] { "Images/Home", "Images/Chests/Closed", "Images/Chests/Open", "Images/Chests/Front", "Images/Maps/Map01", "Images/Maps/Map10", "Images/Items/Item0", "Images/Items/Item4", "Images/UI/Button_gold", "Images/UI/Button_dark", "Images/UI/Button_light", "Images/UI/Panel_Large", "Images/UI/Board_Frame", "Images/UI/ToolCard_ready", "Images/UI/IconButton_settings", "Images/UI/Source_Left", "Images/UI/Source_Right" })
                if (Resources.Load<Texture2D>(name) == null) { Debug.LogError("Missing texture: " + name); failures++; }
            for (int odd = 0; odd < 2; odd++)
            {
                for (int mask = 0; mask < 16; mask++)
                {
                    string tile = "Images/Tiles/Tile_" + mask + "_" + odd;
                    if (Resources.Load<Texture2D>(tile) == null) { Debug.LogError("Missing pipe artwork: " + tile); failures++; }
                }
                string stone = "Images/Tiles/Stone_" + odd;
                if (Resources.Load<Texture2D>(stone) == null) { Debug.LogError("Missing stone artwork: " + stone); failures++; }
            }
            for (int mask = 0; mask < 16; mask++)
            {
                string path = "Images/Tiles/PipeOverlay_" + mask;
                if (Resources.Load<Texture2D>(path) == null) { Debug.LogError("Missing rotation artwork: " + path); failures++; }
            }
            for (int index = 0; index < 10; index++)
            {
                string path = "Images/Maps/Map" + (index + 1).ToString("D2");
                if (Resources.Load<Texture2D>(path) == null) { Debug.LogError("Missing original map: " + path); failures++; }
            }
            for (int index = 0; index < 5; index++)
            {
                string path = "Images/Items/Item" + index;
                if (Resources.Load<Texture2D>(path) == null) { Debug.LogError("Missing item art: " + path); failures++; }
            }
            foreach (string name in new[] { "Audio/background", "Audio/chest", "Audio/pipe_turn", "Audio/win" })
                if (Resources.Load<AudioClip>(name) == null) { Debug.LogError("Missing sound: " + name); failures++; }
            if (Resources.Load<TextAsset>("Data/Worlds") == null) { Debug.LogError("World data is missing"); failures++; }
            if (failures == 0) Debug.Log("Pipe Flow: all 120 level routes and required assets passed validation.");
            else Debug.LogError("Pipe Flow validation failed: " + failures + " errors.");
            EditorUtility.DisplayDialog("Pipe Flow validation", failures == 0 ? "120 level routes and required assets passed." : failures + " errors. See the Unity Console.", "OK");
        }
    }
}
