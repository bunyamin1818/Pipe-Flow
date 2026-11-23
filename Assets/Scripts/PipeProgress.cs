using System;
using System.IO;
using UnityEngine;

namespace PipeFlow
{
    [Serializable]
    public sealed class PipeSaveData
    {
        public int version = 2;
        public int unlocked = 1;
        public int gold = 65;
        public int chestStars = 0;
        public int[] stars = new int[121];
        public bool[] cleared = new bool[121];
        public bool[] tutorialsSeen = new bool[121];
        public int[] inventory = { 1, 0, 0, 0, 0 };
        public string language = "en";
        public bool languageSelected = false;
        public int speed = 1;
        public bool sound = true;
        public bool music = true;
    }

    public sealed class PipeProgress
    {
        public const int ChestTarget = 15;
        public static readonly int[] ItemUnlocks = { 1, 7, 10, 14, 20 };
        public static readonly int[] ItemCosts = { 60, 85, 110, 125, 160 };
        public PipeSaveData Data { get; private set; }
        public const string SaveFileName = "pipeflow_progress.json";
        private const string LegacySaveFileName = "pipe_flow_native_v44_fix_progress.json";
        private string FilePath { get { return Path.Combine(Application.persistentDataPath, SaveFileName); } }
        private string LegacyFilePath { get { return Path.Combine(Application.persistentDataPath, LegacySaveFileName); } }

        public PipeProgress() { Load(); }
        public void Load()
        {
            try
            {
                // Migrate existing progress without resetting player achievements.
                string source = File.Exists(FilePath) ? FilePath : LegacyFilePath;
                Data = File.Exists(source) ? JsonUtility.FromJson<PipeSaveData>(File.ReadAllText(source)) : null;
                if (Data != null && source == LegacyFilePath && !File.Exists(FilePath)) Save();
            }
            catch (Exception ex) { Debug.LogWarning("Save could not be loaded: " + ex.Message); }
            if (Data == null) Data = new PipeSaveData();
            if (Data.stars == null || Data.stars.Length != 121) Data.stars = new int[121];
            if (Data.cleared == null || Data.cleared.Length != 121) Data.cleared = new bool[121];
            if (Data.tutorialsSeen == null || Data.tutorialsSeen.Length != 121) Data.tutorialsSeen = new bool[121];
            if (Data.inventory == null || Data.inventory.Length != 5) Data.inventory = new int[] { 1, 0, 0, 0, 0 };
            Data.unlocked = Mathf.Clamp(Data.unlocked, 1, 120);
            Data.gold = Math.Max(0, Data.gold);
            Data.chestStars = Math.Max(0, Data.chestStars);
            if (Data.speed != 1 && Data.speed != 2 && Data.speed != 4) Data.speed = 1;
            if (!Data.languageSelected || string.IsNullOrEmpty(Data.language)) Data.language = "en";
        }
        public void ResetAllProgress()
        {
            Data = new PipeSaveData();
            Save();
        }

        public void Save()
        {
            try { Directory.CreateDirectory(Application.persistentDataPath); File.WriteAllText(FilePath, JsonUtility.ToJson(Data, true)); }
            catch (Exception ex) { Debug.LogWarning("Save could not be written: " + ex.Message); }
        }
        public int BestStars(int level) { return level >= 1 && level <= 120 ? Mathf.Clamp(Data.stars[level], 0, 3) : 0; }
        public bool IsCleared(int level) { return level >= 1 && level <= 120 && Data.cleared[level]; }
        public CompletionResult Complete(int level, int moves, int par)
        {
            var previous = BestStars(level);
            bool wasCleared = IsCleared(level);
            int excess = moves - par;
            int earned = excess <= 0 ? 3 : excess <= 4 ? 2 : 1;
            int best = Math.Max(previous, earned);
            int[] prizes = { 0, 9, 16, 24 };
            int gold = prizes[best] - (wasCleared ? prizes[previous] : 0);
            Data.stars[level] = best;
            Data.cleared[level] = true;
            Data.gold += gold;
            Data.chestStars += best - previous;
            if (level == Data.unlocked) Data.unlocked = Math.Min(120, level + 1);
            Save();
            return new CompletionResult { Stars = best, NewStars = best - previous, Gold = gold };
        }
        public bool ChestReady { get { return Data.chestStars >= ChestTarget; } }
        public int ClaimChest(int level)
        {
            if (!ChestReady) return -1;
            Data.chestStars -= ChestTarget;
            int[] available = new int[5]; int count = 0;
            for (int i = 0; i < 5; i++) if (Data.unlocked >= ItemUnlocks[i]) available[count++] = i;
            int choice = available[((level * 11 + Data.unlocked * 7 + Data.chestStars * 3) % count + count) % count];
            Data.inventory[choice]++;
            Save();
            return choice;
        }
        public bool Buy(int index)
        {
            if (index < 0 || index >= 5 || Data.unlocked < ItemUnlocks[index] || Data.gold < ItemCosts[index]) return false;
            Data.gold -= ItemCosts[index]; Data.inventory[index]++; Save(); return true;
        }
        public bool Use(int index)
        {
            if (index < 0 || index >= 5 || Data.inventory[index] < 1) return false;
            Data.inventory[index]--; Save(); return true;
        }
    }

    public struct CompletionResult { public int Stars, NewStars, Gold; }
}
