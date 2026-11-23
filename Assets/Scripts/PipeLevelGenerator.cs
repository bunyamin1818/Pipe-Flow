using System;
using System.Collections.Generic;
using UnityEngine;

namespace PipeFlow
{
    public sealed class PipeCell
    {
        public int Mask;
        public int Correct;
        public bool Stone;
        public bool Fixed;
        public bool Star;
    }

    public sealed class PipePuzzle
    {
        public int Size, Start, Exit, Star, Par;
        public PipeCell[] Cells;
        public List<int> Route;
    }

    public sealed class PipeLevelGenerator
    {
        private uint seed;
        private float NextFloat()
        {
            unchecked { seed = seed * 1664525u + 1013904223u; }
            return seed / 4294967296f;
        }
        private int NextInt(int length) { return Mathf.Clamp(Mathf.FloorToInt(NextFloat() * length), 0, length - 1); }
        public static int Rotate(int mask) { return ((mask << 1) & 15) | ((mask >> 3) & 1); }
        public static bool Has(int mask, int dir) { return (mask & (1 << dir)) != 0; }
        private static int Index(int x, int y, int n) { return y * n + x; }
        private static int RotationCost(int a, int target)
        {
            for (int i = 0; i <= 4; i++) { if (a == target) return i; a = Rotate(a); }
            return 4;
        }
        private sealed class Option { public int Mask, Cost; }
        private sealed class Slot { public int Index, Selected; public List<Option> Options = new List<Option>(); }
        private static int Direction(Vector2Int from, Vector2Int to)
        {
            if (to.x > from.x) return 1;
            if (to.x < from.x) return 3;
            return to.y > from.y ? 2 : 0;
        }
        public PipePuzzle Generate(int level)
        {
            level = Mathf.Clamp(level, 1, 120);
            unchecked { seed = (uint)(level * 6317 + 237); }
            int n = level <= 3 ? 4 : level <= 12 ? 5 : level <= 36 ? 6 : 7;
            int turns = level == 1 ? 0 : level == 2 ? 1 : level == 3 ? 2 : Math.Min(n - 1, 2 + (level - 3) / 2);
            var columns = new List<int>();
            for (int i = 0; i < n - 1; i++) columns.Add(i);
            for (int i = columns.Count - 1; i > 0; i--) { int j = NextInt(i + 1); int tmp = columns[i]; columns[i] = columns[j]; columns[j] = tmp; }
            var bends = new HashSet<int>();
            for (int i = 0; i < turns; i++) bends.Add(columns[i]);
            int row = level == 1 ? 1 : 1 + NextInt(n - 2);
            var route = new List<Vector2Int>();
            for (int x = 0; x < n; x++)
            {
                route.Add(new Vector2Int(x, row));
                if (x >= n - 1 || !bends.Contains(x)) continue;
                float chance = level >= 45 ? .67f : level >= 24 ? .42f : level >= 12 ? .22f : 0f;
                int jump = NextFloat() < chance ? 2 : 1;
                var choices = new List<int>();
                foreach (int sign in new int[] { -1, 1 }) if (row + sign * jump >= 0 && row + sign * jump < n) choices.Add(sign * jump);
                if (choices.Count == 0)
                {
                    jump = 1;
                    foreach (int sign in new int[] { -1, 1 }) if (row + sign >= 0 && row + sign < n) choices.Add(sign);
                }
                int target = row + choices[NextInt(choices.Count)];
                while (row != target) { row += target > row ? 1 : -1; route.Add(new Vector2Int(x, row)); }
            }
            PipeCell[] cells = new PipeCell[n * n];
            for (int i = 0; i < cells.Length; i++) cells[i] = new PipeCell();
            var routeIds = new List<int>();
            for (int i = 0; i < route.Count; i++)
            {
                var p = route[i];
                var prev = i == 0 ? new Vector2Int(-1, p.y) : route[i - 1];
                var next = i == route.Count - 1 ? new Vector2Int(n, p.y) : route[i + 1];
                int mask = (1 << Direction(p, prev)) | (1 << Direction(p, next));
                int index = Index(p.x, p.y, n);
                cells[index].Mask = cells[index].Correct = mask;
                routeIds.Add(index);
            }
            int src = routeIds[0], dst = routeIds[routeIds.Count - 1];
            cells[src].Fixed = cells[dst].Fixed = true;
            var slots = new List<Slot>();
            int par = 0;
            for (int i = 1; i < route.Count - 1; i++)
            {
                var p = route[i];
                int index = Index(p.x, p.y, n), desired = cells[index].Correct;
                int arriving = Direction(p, route[i - 1]);
                var slot = new Slot { Index = index };
                for (int turn = 1; turn <= 3; turn++)
                {
                    int mask = desired;
                    for (int k = 0; k < turn; k++) mask = Rotate(mask);
                    bool duplicate = slot.Options.Exists(o => o.Mask == mask);
                    if (mask == desired || (i == 1 && Has(mask, arriving)) || duplicate) continue;
                    slot.Options.Add(new Option { Mask = mask, Cost = RotationCost(mask, desired) });
                }
                if (slot.Options.Count == 0)
                {
                    for (int turn = 1; turn <= 3; turn++)
                    {
                        int mask = desired;
                        for (int k = 0; k < turn; k++) mask = Rotate(mask);
                        if (mask != desired && !slot.Options.Exists(o => o.Mask == mask)) slot.Options.Add(new Option { Mask = mask, Cost = RotationCost(mask, desired) });
                    }
                }
                slot.Options.Sort((a, b) => a.Cost.CompareTo(b.Cost));
                if (slot.Options.Count > 0) { slots.Add(slot); par += slot.Options[0].Cost; }
            }
            int[] targets = { 0, 2, 4, 6, 8, 10, 12, 13, 14, 15, 16, 17, 18 };
            int goal = level <= 12 ? targets[level] : 18 + (level - 12) / 3;
            int need = Mathf.Max(0, goal - par);
            var order = new List<int>();
            for (int i = 0; i < slots.Count; i++) order.Add(i);
            for (int i = order.Count - 1; i > 0; i--) { int j = NextInt(i + 1); int a = order[i]; order[i] = order[j]; order[j] = a; }
            for (int round = 0; round < 3 && need > 0; round++)
            {
                foreach (int z in order)
                {
                    Slot slot = slots[z];
                    int chosen = -1, maxGain = -1;
                    for (int k = 0; k < slot.Options.Count; k++)
                    {
                        int inc = slot.Options[k].Cost - slot.Options[slot.Selected].Cost;
                        if (inc > 0 && inc <= need && inc > maxGain) { chosen = k; maxGain = inc; }
                    }
                    if (chosen >= 0) { slot.Selected = chosen; need -= maxGain; }
                }
            }
            if (need > 0)
            {
                foreach (int z in order)
                {
                    var slot = slots[z];
                    int best = -1, smallest = 100;
                    for (int k = 0; k < slot.Options.Count; k++)
                    {
                        int inc = slot.Options[k].Cost - slot.Options[slot.Selected].Cost;
                        if (inc > 0 && inc < smallest) { best = k; smallest = inc; }
                    }
                    if (best >= 0) { slot.Selected = best; break; }
                }
            }
            par = 0;
            foreach (var s in slots) { cells[s.Index].Mask = s.Options[s.Selected].Mask; par += s.Options[s.Selected].Cost; }
            int[] valid = { 3, 5, 6, 9, 10, 12, 7, 11, 13, 14 };
            int[] decoys = level <= 2 ? new int[] { 5, 10 } : level <= 6 ? new int[] { 3, 5, 6, 9, 10, 12 } : valid;
            for (int j = 0; j < cells.Length; j++)
            {
                if (cells[j].Correct != 0 || cells[j].Fixed) continue;
                int mask = decoys[NextInt(decoys.Length)];
                int rotate = NextInt(4);
                for (int k = 0; k < rotate; k++) mask = Rotate(mask);
                cells[j].Mask = mask;
            }
            int star = -1;
            if (level >= 3) { star = routeIds[route.Count / 2]; cells[star].Star = true; }
            if (level >= 7)
            {
                var options = new List<int>();
                for (int i = 0; i < cells.Length; i++) if (cells[i].Correct == 0 && i != src && i != dst) options.Add(i);
                int number = Math.Min(1 + (level - 7) / 16, options.Count);
                for (int k = 0; k < number; k++) { int ix = NextInt(options.Count); cells[options[ix]].Stone = true; cells[options[ix]].Mask = 0; options.RemoveAt(ix); }
            }
            int allowance = level <= 40 ? 2 : level <= 80 ? 1 : 0;
            return new PipePuzzle { Size = n, Cells = cells, Start = src, Exit = dst, Star = star, Route = routeIds, Par = Math.Max(2, par) + allowance };
        }
    }
}
