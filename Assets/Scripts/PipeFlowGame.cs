using System;
using System.Collections.Generic;
using UnityEngine;

namespace PipeFlow
{
    [Serializable] public sealed class WorldPoint { public float x, y; }
    [Serializable] public sealed class WorldZone { public WorldPoint[] nodes, path; public float[] cuts; }
    [Serializable] public sealed class WorldBundle { public WorldZone[] zones; }
    [Serializable] public sealed class RoadTransitionInfo { public int zone, segment, x, y, w, h; }
    [Serializable] public sealed class RoadTransitionBundle { public RoadTransitionInfo[] segments; }

    [DisallowMultipleComponent]
    public sealed class PipeFlowGame : MonoBehaviour
    {
        private const float W = 420f, H = 860f;
        private const float BoardLeft = 24f, BoardTop = 245f, BoardWidth = 372f;
        private static readonly int[] DX = { 0, 1, 0, -1 };
        private static readonly int[] DY = { -1, 0, 1, 0 };
        private static readonly string[] LanguageNames = { "tr", "en", "de", "fr", "es" };
        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        private readonly Texture2D[,] pipeTiles = new Texture2D[16, 2];
        private readonly Texture2D[] stoneTiles = new Texture2D[2];
        private readonly Texture2D[,] rotationSprites = new Texture2D[16, 12];
        private readonly bool[] rotationSpritesReady = new bool[16];
        private readonly Dictionary<int, float> rotationProgress = new Dictionary<int, float>();
        private readonly Dictionary<int, int> rotatingFrom = new Dictionary<int, int>();
        private readonly List<int> rotationKeys = new List<int>(64);
        private readonly Dictionary<int, int> wetEntry = new Dictionary<int, int>();
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private readonly Dictionary<int, float> wet = new Dictionary<int, float>();
        private readonly List<int> wetKeys = new List<int>(64);
        private readonly Dictionary<int, int> newlyWet = new Dictionary<int, int>(64);
        private readonly HashSet<int> reachableCache = new HashSet<int>();
        private readonly Queue<int> reachQueue = new Queue<int>(64);
        private bool reachableDirty = true;
        private readonly List<int> highlighted = new List<int>();
        private readonly List<HitArea> hits = new List<HitArea>();
        private readonly Dictionary<string, GUIStyle> styles = new Dictionary<string, GUIStyle>();
        private readonly Dictionary<string, int> labelSizes = new Dictionary<string, int>();
        private PipeLevelGenerator generator;
        private PipeProgress progress;
        private PipePuzzle puzzle;
        private WorldBundle worlds;
        private RoadTransitionInfo[] roadTransitionData;
        private readonly Dictionary<string, GUIContent> textContents = new Dictionary<string, GUIContent>();
        private AudioSource effects, music, pipeTurnAudio;
        private float lastPipeTurnAt = -10f;
        private Texture2D white, circle, star, rounded;
        private string screen = "home", previousScreen = "home";
        private string tutorial = "", toast = "";
        private string shopNotice = "";
        private float shopNoticeUntil, chestHintUntil, shopPressedUntil;
        private int shopPressedIndex = -1;
        private float toastTimer, hintTimer, chestTime, advanceTime, elapsed, bossTimer = 75f, winWait = -1f, animTime;
        private float contentScale = 1f, offsetX, offsetY;
        private const float WorldTransitionDuration = 2.25f;
        private int zone, selectedLevel = 1, level = 1, moves, rewardIndex = -1, winStars, winGold;
        private int selectedTool = -1, shapeCell = -1, advanceFrom, advanceTo, advanceOldStars, advanceNewStars;
        private int nextAfterChest;
        private bool pendingChest, returnToMapAfterChest, bossRunning;
        private bool latestWinUnlockedNew, animateNewConnection, crossingWorldBoundary;
        private struct HitArea { public Rect rect; public string action; public int value; }



        // Loads the imported Unity texture first, then the PNG bytes bundled offline.
        // The raw fallback avoids blank screens if a platform cannot import a PNG asset.
        private readonly Dictionary<string, Texture2D> nativeArtCache = new Dictionary<string, Texture2D>();
        private readonly List<Texture2D> runtimeDecodedArt = new List<Texture2D>();
        private readonly HashSet<string> missingNativeArt = new HashSet<string>();
        private Texture2D LoadTexture(string path)
        {
            if (nativeArtCache.TryGetValue(path, out Texture2D cached)) return cached;
            Texture2D result = Resources.Load<Texture2D>(path);
            if (result == null)
            {
                Sprite sprite = Resources.Load<Sprite>(path);
                if (sprite != null) result = sprite.texture;
            }
            if (result == null)
            {
                TextAsset png = Resources.Load<TextAsset>("RawArt/" + path);
                byte[] imageBytes = png != null ? png.bytes : null;
#if UNITY_EDITOR
                // Editor-only last resort: read the source PNG directly from disk.
                if (imageBytes == null || imageBytes.Length == 0)
                {
                    string originalPath = System.IO.Path.Combine(Application.dataPath, "Resources", path + ".png");
                    if (System.IO.File.Exists(originalPath)) imageBytes = System.IO.File.ReadAllBytes(originalPath);
                }
#endif
                if (imageBytes != null && imageBytes.Length > 0)
                {
                    var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (ImageConversion.LoadImage(decoded, imageBytes, true))
                    {
                        decoded.name = "PipeFlow/" + path;
                        decoded.wrapMode = TextureWrapMode.Clamp;
                        decoded.filterMode = FilterMode.Bilinear;
                        decoded.anisoLevel = 1;
                        result = decoded;
                        runtimeDecodedArt.Add(decoded);
                    }
                    else Destroy(decoded);
                }
            }
            nativeArtCache[path] = result;
            if (result == null && missingNativeArt.Add(path))
                Debug.LogError("Pipe Flow artwork not found (imported texture and offline PNG): " + path);
            return result;
        }

        private readonly Dictionary<string, Texture2D> artTextures = new Dictionary<string, Texture2D>();
        private readonly HashSet<string> failedArt = new HashSet<string>();
        private Texture2D ArtTexture(string path)
        {
            if (artTextures.TryGetValue(path, out Texture2D result)) return result;
            result = LoadTexture("Art/" + path);
            artTextures[path] = result;
            if (result == null && failedArt.Add(path))
                Debug.LogError("Pipe Flow artwork missing: Assets/Resources/Art/" + path + ".png");
            return result;
        }
        private void DrawArt(string path, Rect target, float alpha = 1f)
        {
            Texture2D img = ArtTexture(path);
            if (img == null) return;
            Color old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            GUI.DrawTexture(target, img, ScaleMode.StretchToFill, true);
            GUI.color = old;
        }
        private void DrawScreenArtwork(string name)
        {
            DrawArt("Screens/" + name, new Rect(0f, 0f, W, H));
        }
        private void LabelInRect(Rect target, string value, int size, Color color, string action = null, int n = -1)
        {
            Label(value, target.center.x, target.center.y + 1f, size, color, target.width - 4, target.height - 5);
            if (action != null) Register(target, action, n);
        }

        private void Awake()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            generator = new PipeLevelGenerator();
            progress = new PipeProgress();
            var json = Resources.Load<TextAsset>("Data/Worlds");
            if (json != null) worlds = JsonUtility.FromJson<WorldBundle>(json.text);
            var roads = Resources.Load<TextAsset>("Data/RoadTransitionMeta");
            if (roads != null)
            {
                var bundle = JsonUtility.FromJson<RoadTransitionBundle>(roads.text);
                if (bundle != null) roadTransitionData = bundle.segments;
            }
            textures["closed"] = LoadTexture("Images/Chests/Closed");
            textures["open"] = LoadTexture("Images/Chests/Open");
            textures["ui/Board_Frame"] = LoadTexture("Images/UI/Board_Frame");
            textures["ui/Button_dark"] = LoadTexture("Images/UI/Button_dark");
            textures["ui/Button_gold"] = LoadTexture("Images/UI/Button_gold");
            textures["ui/Button_light"] = LoadTexture("Images/UI/Button_light");
            textures["ui/Button_primary"] = LoadTexture("Images/UI/Button_primary");
            textures["ui/CoinBar"] = LoadTexture("Images/UI/CoinBar");
            textures["ui/IconButton_back"] = LoadTexture("Images/UI/IconButton_back");
            textures["ui/IconButton_back_dark"] = LoadTexture("Images/UI/IconButton_back_dark");
            textures["ui/IconButton_bag"] = LoadTexture("Images/UI/IconButton_bag");
            textures["ui/IconButton_map"] = LoadTexture("Images/UI/IconButton_map");
            textures["ui/IconButton_settings"] = LoadTexture("Images/UI/IconButton_settings");
            textures["ui/IconButton_settings_dark"] = LoadTexture("Images/UI/IconButton_settings_dark");
            textures["ui/IconButton_shop"] = LoadTexture("Images/UI/IconButton_shop");
            textures["ui/IconButton_x"] = LoadTexture("Images/UI/IconButton_x");
            textures["ui/IconButton_x_dark"] = LoadTexture("Images/UI/IconButton_x_dark");
            textures["ui/Icon_coin"] = LoadTexture("Images/UI/Icon_coin");
            textures["ui/Icon_drop"] = LoadTexture("Images/UI/Icon_drop");
            textures["ui/Icon_lock"] = LoadTexture("Images/UI/Icon_lock");
            textures["ui/Icon_star"] = LoadTexture("Images/UI/Icon_star");
            textures["ui/Panel_Large"] = LoadTexture("Images/UI/Panel_Large");
            textures["ui/Panel_Medium"] = LoadTexture("Images/UI/Panel_Medium");
            textures["ui/Panel_Tall"] = LoadTexture("Images/UI/Panel_Tall");
            textures["ui/SmallButton_dark"] = LoadTexture("Images/UI/SmallButton_dark");
            textures["ui/SmallButton_gold"] = LoadTexture("Images/UI/SmallButton_gold");
            textures["ui/SmallButton_light"] = LoadTexture("Images/UI/SmallButton_light");
            textures["ui/Source_Left"] = LoadTexture("Images/UI/Source_Left");
            textures["ui/Source_Right"] = LoadTexture("Images/UI/Source_Right");
            textures["ui/ToolCard_locked"] = LoadTexture("Images/UI/ToolCard_locked");
            textures["ui/ToolCard_ready"] = LoadTexture("Images/UI/ToolCard_ready");
            textures["ui/ToolCard_selected"] = LoadTexture("Images/UI/ToolCard_selected");
            textures["front"] = LoadTexture("Images/Chests/Front");

            for (int i = 0; i < 5; i++) textures["item" + i] = LoadTexture("Images/Items/Item" + i);
            for (int odd = 0; odd < 2; odd++)
            {
                for (int mask = 0; mask < 16; mask++)
                    pipeTiles[mask, odd] = LoadTexture("Images/Tiles/Tile_" + mask + "_" + odd);
                stoneTiles[odd] = LoadTexture("Images/Tiles/Stone_" + odd);
            }
            foreach (string key in new string[] { "background", "pipe_turn", "win", "fail", "chest", "coin", "tool", "ui_tap" })
                clips[key] = Resources.Load<AudioClip>("Audio/" + key);
            effects = gameObject.AddComponent<AudioSource>();
            music = gameObject.AddComponent<AudioSource>();
            music.clip = clips["background"];
            music.loop = true;
            music.volume = .13f;
            effects.volume = .65f;
            // A dedicated non-overlapping channel for a single pipe rotation.
            pipeTurnAudio = gameObject.AddComponent<AudioSource>();
            pipeTurnAudio.playOnAwake = false;
            pipeTurnAudio.spatialBlend = 0f;
            pipeTurnAudio.loop = false;
            pipeTurnAudio.volume = .65f;
            pipeTurnAudio.clip = clips["pipe_turn"];
            SyncMusic();
            white = MakeSolid();
            circle = MakeShape(128, (x, y) => x * x + y * y <= .94f * .94f);
            star = MakeShape(128, (x, y) =>
            {
                float a = Mathf.Atan2(y, x) + Mathf.PI * .5f;
                float radius = Mathf.Sqrt(x * x + y * y);
                float sp = Mathf.Cos(5f * a);
                return radius <= .36f + .46f * Mathf.Clamp01(sp * .5f + .5f);
            });
            rounded = MakeShape(96, (x, y) =>
            {
                float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
                float cx = Mathf.Max(0, ax - .64f), cy = Mathf.Max(0, ay - .64f);
                return cx * cx + cy * cy <= .35f * .35f;
            });
            zone = Mathf.Clamp((progress.Data.unlocked - 1) / 12, 0, 9);
            string[] requiredArtAssets = { "Screens/home", "Screens/shopClean", "UI/ShopUnlockedBadge", "Screens/map0", "Screens/game0", "Tiles/Tile_3_0", "Tiles/Tile_5_0", "Tiles/left", "Tiles/right", "ChestFrames/C0_20" };
            foreach (string name in requiredArtAssets) ArtTexture(name);
            foreach (var item in textures)
                if (item.Value == null) Debug.LogError("Pipe Flow: required texture unavailable: " + item.Key);
            for (int mask = 0; mask < 16; mask++)
                for (int odd = 0; odd < 2; odd++)
                    if (pipeTiles[mask, odd] == null) Debug.LogError("Pipe Flow: missing pipe sprite " + mask + "_" + odd);
        }

        private void OnDestroy()
        {
            if (white != null) Destroy(white);
            if (circle != null) Destroy(circle);
            if (star != null) Destroy(star);
            if (rounded != null) Destroy(rounded);
            foreach (var image in runtimeDecodedArt) if (image != null) Destroy(image);
            runtimeDecodedArt.Clear();
        }

        private static Texture2D MakeSolid()
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.SetPixel(0, 0, Color.white); t.Apply(); return t;
        }
        private static Texture2D MakeShape(int size, Func<float, float, bool> inside)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var p = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float fx = ((x + .5f) / size - .5f) * 2f, fy = ((y + .5f) / size - .5f) * 2f;
                p[y * size + x] = inside(fx, fy) ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
            t.SetPixels32(p); t.Apply(); t.filterMode = FilterMode.Bilinear; return t;
        }
        private void Play(string key)
        {
            if (!progress.Data.sound || !clips.ContainsKey(key) || clips[key] == null) return;
            if (key == "pipe_turn")
            {
                // Stop the previous rotation sound before a new one is played.
                if (Time.unscaledTime - lastPipeTurnAt < .07f) return;
                lastPipeTurnAt = Time.unscaledTime;
                if (pipeTurnAudio == null) return;
                pipeTurnAudio.Stop();
                pipeTurnAudio.Play();
                return;
            }
            effects.PlayOneShot(clips[key]);
        }
        private void SyncMusic()
        {
            if (progress.Data.music) { if (music.clip != null && !music.isPlaying) music.Play(); }
            else music.Stop();
        }
        private string T(string key) { return PipeLocale.Get(progress.Data.language, key); }
        private string ItemName(int i) { return PipeLocale.Item(progress.Data.language, i); }
        private void Message(string msg) { toast = msg; toastTimer = 2.2f; }
        private int Speed { get { return progress.Data.speed; } }

        private void Update()
        {
            float delta = Mathf.Min(.06f, Time.unscaledDeltaTime);
            animTime += delta;
            if (homePendingAction != null && Time.unscaledTime >= homePressUntil)
            {
                string action = homePendingAction;
                int val = homePendingValue;
                homePendingAction = null;
                if (screen == "home") Handle(action, val);
            }
            if (rotationProgress.Count != 0)
            {
                rotationKeys.Clear();
                rotationKeys.AddRange(rotationProgress.Keys);
                foreach (int index in rotationKeys)
                {
                    // A complete quarter-turn takes 0.28 s at 1x, 0.14 s at 2x and 0.09 s at 4x.
                    float duration = Speed == 4 ? .19f : Speed == 2 ? .25f : .33f;
                    float value = rotationProgress[index] + delta / duration;
                    if (value >= 1f) { rotationProgress.Remove(index); rotatingFrom.Remove(index); }
                    else rotationProgress[index] = value;
                }
            }
            toastTimer -= delta;
            hintTimer -= delta;
            if (screen == "chest") chestTime = Mathf.Min(2.6f, chestTime + delta * Speed);
            if (screen == "mapAdvance")
            {
                advanceTime += delta * Speed;
                if (advanceTime >= (crossingWorldBoundary ? WorldTransitionDuration : 1.9f))
                {
                    if (crossingWorldBoundary) zone = Mathf.Clamp((advanceTo - 1) / 12, 0, 9);
                    crossingWorldBoundary = false;
                    if (pendingChest) { pendingChest = false; StartChest(true); }
                    else if (advanceTo <= 120) StartLevel(advanceTo);
                    else screen = "map";
                }
            }
            if (screen == "game") AdvanceWater(delta);
            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began) HandlePointer(touch.position);
            }
            else if (Input.GetMouseButtonDown(0)) HandlePointer(Input.mousePosition);
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (screen == "game") { zone = (level - 1) / 12; screen = "map"; }
                else if (screen == "home") Application.Quit();
                else screen = "home";
            }
        }
        private void HandlePointer(Vector2 mouse)
        {
            float xx = (mouse.x - offsetX) / contentScale;
            float yy = (Screen.height - mouse.y - offsetY) / contentScale;
            if (xx < 0 || xx > W || yy < 0 || yy > H) return;
            var point = new Vector2(xx, yy);
            for (int i = hits.Count - 1; i >= 0; i--)
            {
                if (hits[i].rect.Contains(point))
                {
                    if (screen == "home")
                    {
                        homePendingAction = hits[i].action;
                        homePressedPoint = point;
                        homePendingValue = hits[i].value;
                        homePressUntil = Time.unscaledTime + .125f;
                    }
                    else Handle(hits[i].action, hits[i].value);
                    // A pipe tap has its own SFX. Do not play the generic UI tap too.
                    if (hits[i].action != "pipe") Play("ui_tap");
                    return;
                }
            }
            if (screen == "game" && tutorial != "") tutorial = "";
        }
        private void Register(Rect r, string action, int value = -1) { hits.Add(new HitArea { rect = r, action = action, value = value }); }
        private void Handle(string action, int val)
        {
            switch (action)
            {
                case "map": zone = Mathf.Clamp((progress.Data.unlocked - 1) / 12, 0, 9); screen = "map"; break;
                case "home": screen = "home"; break;
                case "play": StartLevel(Mathf.Clamp(progress.Data.unlocked,1,120)); break;
                case "challenge": previousScreen = "home"; screen = "challenge"; break;
                case "stats": previousScreen = "home"; screen = "stats"; break;
                case "mapPrev": zone = (zone + 9) % 10; break;
                case "mapNext": zone = (zone + 1) % 10; break;
                case "choose": if (val <= progress.Data.unlocked) { selectedLevel = val; screen = "intro"; } else Message(T("locked")); break;
                case "start": StartLevel(val); break;
                case "next": NextLevel(); break;
                case "back": zone = (level - 1) / 12; screen = "map"; break;
                case "chest":
                    if (progress.ChestReady) StartChest(false);
                    else chestHintUntil = Time.unscaledTime + 2.4f;
                    break;
                case "chestContinue": if (returnToMapAfterChest) screen = "map"; else if (nextAfterChest <= 120) StartLevel(nextAfterChest); else screen = "map"; break;
                case "settings": previousScreen = screen; screen = "settings"; break;
                case "shop": previousScreen = screen; screen = "shop"; break;
                case "bag": previousScreen = screen; screen = "bag"; break;
                case "close": screen = previousScreen; break;
                case "lang": progress.Data.language = LanguageNames[val]; progress.Data.languageSelected = true; progress.Save(); break;
                case "speed": progress.Data.speed = new int[] { 1, 2, 4 }[val]; progress.Save(); break;
                case "music": progress.Data.music = !progress.Data.music; progress.Save(); SyncMusic(); break;
                case "sound": progress.Data.sound = !progress.Data.sound; progress.Save(); break;
                case "buy":
                    if (val < 0 || val >= PipeProgress.ItemUnlocks.Length) break;
                    shopPressedIndex = val;
                    shopPressedUntil = Time.unscaledTime + .13f;
                    if (progress.Data.unlocked < PipeProgress.ItemUnlocks[val])
                        shopNotice = T("openAt") + " " + PipeProgress.ItemUnlocks[val];
                    else if (progress.Buy(val))
                    {
                        Play("coin");
                        shopNotice = T("popupBuy");
                    }
                    else shopNotice = T("notEnough");
                    shopNoticeUntil = Time.unscaledTime + 1.5f;
                    break;
                case "tool": UseTool(val); break;
                case "pipe": RotateCell(val); break;
                case "ad": Message(T("adHint")); break;
                case "hint": if (progress.Data.gold >= 100 && Hint()) { progress.Data.gold -= 100; progress.Save(); } else if (progress.Data.gold < 100) Message(T("notEnough")); break;
                case "shape": ChooseShape(val); break;
                case "challengeLevel": if (val <= progress.Data.unlocked) StartLevel(val); else Message(T("locked")); break;
                case "retry": StartLevel(level); break;
                case "tutorial": tutorial = ""; break;
            }
        }

        private void StartLevel(int next)
        {
            if (next < 1 || next > 120) return;
            level = next;
            puzzle = generator.Generate(next);
            EnsureInitialPuzzleIsNotSolved();
            moves = 0;
            elapsed = 0;
            bossTimer = 75f;
            bossRunning = false;
            winWait = -1f;
            wet.Clear(); wetEntry.Clear(); rotationProgress.Clear(); rotatingFrom.Clear();
            reachableDirty = true; reachableCache.Clear();
            wet[puzzle.Start] = 0; wetEntry[puzzle.Start] = 3;
            highlighted.Clear();
            selectedTool = -1; shapeCell = -1;
            if ((level == 1 || level == 3 || level == 7 || level == 12) && !progress.Data.tutorialsSeen[level])
            {
                tutorial = level == 1 ? "tutOne" : level == 3 ? "tutStar" : level == 7 ? "tutStone" : "tutBoss";
                progress.Data.tutorialsSeen[level] = true; progress.Save();
            }
            else tutorial = "";
            PreloadBoardArtwork();
            screen = "game";
        }

        private void PreloadBoardArtwork()
        {
            if (puzzle == null) return;
            var masks = new HashSet<int>();
            foreach (PipeCell cell in puzzle.Cells)
                if (!cell.Stone)
                {
                    masks.Add(cell.Mask & 15);
                    masks.Add(PipeLevelGenerator.Rotate(cell.Mask) & 15);
                }
            foreach (int mask in masks)
            {
                ArtTexture("Tiles/Tile_" + mask + "_0");
                ArtTexture("Tiles/Tile_" + mask + "_1");
                PrepareRotationSprites(mask);
            }
            var waterMasks = new HashSet<int>();
            foreach (int id in puzzle.Route)
                if (!puzzle.Cells[id].Stone) waterMasks.Add(puzzle.Cells[id].Correct & 15);
            foreach (int mask in waterMasks)
                for (int dir = 0; dir < 4; dir++)
                    if (PipeLevelGenerator.Has(mask, dir))
                        for (int step = 1; step <= 8; step++)
                            ArtTexture("Tiles/Wet_" + mask + "_" + dir + "_" + step);
        }

        private void EnsureInitialPuzzleIsNotSolved()
        {
            if (puzzle == null || puzzle.Cells == null || puzzle.Route == null) return;
            if (!HasSolutionConnection()) return;
            foreach (int id in puzzle.Route)
            {
                PipeCell cell = puzzle.Cells[id];
                if (cell.Fixed || cell.Stone) continue;
                int original = cell.Mask;
                for (int turns = 1; turns <= 3; turns++)
                {
                    cell.Mask = PipeLevelGenerator.Rotate(cell.Mask);
                    if (!HasSolutionConnection()) return;
                }
                cell.Mask = original;
            }
            Debug.LogWarning("Pipe Flow: initial puzzle unexpectedly has a complete path.");
        }

        private bool HasSolutionConnection()
        {
            if (puzzle == null) return false;
            reachableDirty = true;
            HashSet<int> reachable = Reachable();
            return reachable.Contains(puzzle.Exit) && (puzzle.Star < 0 || reachable.Contains(puzzle.Star));
        }

        private void PrepareRotationSprites(int mask)
        {
            if (mask <= 0 || mask >= 16 || rotationSpritesReady[mask]) return;
            rotationSpritesReady[mask] = true;
            for (int i = 0; i < 12; i++)
                rotationSprites[mask, i] = LoadTexture("Art/RotationFrames/R_" + mask + "_" + i.ToString("D2"));
        }

        private void RotateCell(int index)
        {
            if (screen != "game" || tutorial != "" || puzzle == null || index < 0 || index >= puzzle.Cells.Length) return;
            var cell = puzzle.Cells[index];
            if (selectedTool >= 0) { UseToolOnCell(index); return; }
            if (cell.Fixed || cell.Stone) return;
            int visibleMask = cell.Mask;
            PrepareRotationSprites(visibleMask);
            cell.Mask = PipeLevelGenerator.Rotate(cell.Mask);
            reachableDirty = true;
            wet.Remove(index); wetEntry.Remove(index);
            rotatingFrom[index] = visibleMask;
            rotationProgress[index] = 0f;
            moves++; bossRunning = true; Play("pipe_turn");
        }
        private List<int> Neighbors(int index)
        {
            var arr = new List<int>();
            int x = index % puzzle.Size, y = index / puzzle.Size;
            for (int dir = 0; dir < 4; dir++)
            {
                int nx = x + DX[dir], ny = y + DY[dir];
                if (nx < 0 || nx >= puzzle.Size || ny < 0 || ny >= puzzle.Size) continue;
                int ni = ny * puzzle.Size + nx;
                if (puzzle.Cells[ni].Stone) continue;
                if (PipeLevelGenerator.Has(puzzle.Cells[index].Mask, dir) && PipeLevelGenerator.Has(puzzle.Cells[ni].Mask, (dir + 2) % 4)) arr.Add(ni);
            }
            return arr;
        }
        private HashSet<int> Reachable()
        {
            if (!reachableDirty) return reachableCache;
            reachableDirty = false;
            reachableCache.Clear(); reachQueue.Clear();
            if (puzzle == null) return reachableCache;
            reachableCache.Add(puzzle.Start);
            reachQueue.Enqueue(puzzle.Start);
            int size = puzzle.Size;
            while (reachQueue.Count > 0)
            {
                int index = reachQueue.Dequeue();
                PipeCell cell = puzzle.Cells[index];
                int x = index % size, y = index / size;
                for (int dir = 0; dir < 4; dir++)
                {
                    if (!PipeLevelGenerator.Has(cell.Mask, dir)) continue;
                    int nx = x + DX[dir], ny = y + DY[dir];
                    if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;
                    int ni = ny * size + nx;
                    PipeCell neighbour = puzzle.Cells[ni];
                    if (neighbour.Stone || !PipeLevelGenerator.Has(neighbour.Mask, (dir + 2) % 4)) continue;
                    if (reachableCache.Add(ni)) reachQueue.Enqueue(ni);
                }
            }
            return reachableCache;
        }
        private void AdvanceWater(float dt)
        {
            if (tutorial != "") return;
            if (winWait >= 0) { winWait -= dt * Speed; if (winWait < 0) CompleteLevel(); return; }
            if (level % 12 == 0 && bossRunning)
            {
                bossTimer -= dt * Speed;
                if (bossTimer <= 0) { bossTimer = 0; screen = "fail"; Play("fail"); return; }
            }
            var reachable = Reachable();
            wetKeys.Clear(); newlyWet.Clear();
            foreach (int key in wet.Keys) wetKeys.Add(key);
            foreach (int index in wetKeys)
            {
                if (wet[index] < 1) wet[index] = Mathf.Min(1f, wet[index] + dt * Speed * 1.25f);
                else if (reachable.Contains(index))
                {
                    int x = index % puzzle.Size, y = index / puzzle.Size;
                    int mask = puzzle.Cells[index].Mask;
                    for (int dir = 0; dir < 4; dir++)
                    {
                        if (!PipeLevelGenerator.Has(mask, dir)) continue;
                        int nx = x + DX[dir], ny = y + DY[dir];
                        if (nx < 0 || nx >= puzzle.Size || ny < 0 || ny >= puzzle.Size) continue;
                        int n = ny * puzzle.Size + nx;
                        PipeCell target = puzzle.Cells[n];
                        if (target.Stone || !PipeLevelGenerator.Has(target.Mask, (dir + 2) % 4)) continue;
                        if (!wet.ContainsKey(n) && !newlyWet.ContainsKey(n))
                            newlyWet[n] = (dir + 2) % 4;
                    }
                }
            }
            foreach (var pair in newlyWet)
                if (!wet.ContainsKey(pair.Key)) { wet[pair.Key] = 0; wetEntry[pair.Key] = pair.Value; }
            if (reachable.Contains(puzzle.Exit) && (puzzle.Star == -1 || reachable.Contains(puzzle.Star)) &&
                wet.TryGetValue(puzzle.Exit, out float endWet) && endWet >= 1f &&
                (puzzle.Star == -1 || (wet.TryGetValue(puzzle.Star, out float starWet) && starWet >= 1f))) winWait = .08f;
        }
        private void CompleteLevel()
        {
            int oldUnlocked = progress.Data.unlocked;
            advanceOldStars = progress.Data.chestStars;
            CompletionResult result = progress.Complete(level, moves, puzzle.Par);
            latestWinUnlockedNew = level == oldUnlocked && progress.Data.unlocked > oldUnlocked;
            advanceNewStars = progress.Data.chestStars;
            winStars = result.Stars; winGold = result.Gold;
            pendingChest = progress.ChestReady;
            screen = "win"; Play("win");
        }
        private void NextLevel()
        {
            if (level >= 120) { zone = 9; if (pendingChest) { pendingChest = false; StartChest(true); } else screen = "map"; return; }
            advanceFrom = level; advanceTo = level + 1;
            animateNewConnection = latestWinUnlockedNew && advanceTo == progress.Data.unlocked;
            crossingWorldBoundary = animateNewConnection && advanceFrom % 12 == 0 && advanceTo <= 120;
            if (crossingWorldBoundary) {
                ArtTexture("Screens/map" + Mathf.Clamp((advanceTo - 1) / 12, 0, 9));
                ArtTexture("Map/Node_unlocked_0");
            }
            latestWinUnlockedNew = false;
            zone = (level - 1) / 12;
            if (animateNewConnection && advanceFrom % 12 != 0)
            {
                int pathIndex = Mathf.Clamp(advanceFrom - zone * 12 - 1, 0, 10);
                ArtTexture("Map/RoadTransition/Atlas_" + zone + "_" + pathIndex);
            }
            advanceTime = 0f; screen = "mapAdvance";
        }
        private void StartChest(bool fromTransition)
        {
            if (!progress.ChestReady) return;
            rewardIndex = progress.ClaimChest(level);
            if (rewardIndex < 0) return;
            returnToMapAfterChest = !fromTransition;
            nextAfterChest = Math.Min(120, level + 1);
            chestTime = 0f; toastTimer = 0f; screen = "chest"; Play("chest");
        }
        private bool Hint()
        {
            if (puzzle == null) return false;
            foreach (int index in puzzle.Route)
            {
                var c = puzzle.Cells[index];
                if (c.Fixed || c.Stone || c.Mask == c.Correct) continue;
                c.Mask = c.Correct;
                reachableDirty = true;
                wet.Remove(index); highlighted.Clear(); highlighted.Add(index); hintTimer = 2f;
                bossRunning = true; Play("tool"); return true;
            }
            Message(T("fine")); return false;
        }
        private void UseTool(int index)
        {
            if (puzzle == null || index < 0 || index > 4) return;
            if (level < PipeProgress.ItemUnlocks[index]) { Message(T("openAt") + " " + PipeProgress.ItemUnlocks[index]); return; }
            if (progress.Data.inventory[index] < 1) { previousScreen = "game"; screen = "shop"; return; }
            if (index == 0 || index == 3)
            {
                highlighted.Clear();
                foreach (int cellIndex in puzzle.Route)
                {
                    var c = puzzle.Cells[cellIndex];
                    if (c.Fixed || c.Stone || c.Mask == c.Correct) continue;
                    highlighted.Add(cellIndex);
                    if (highlighted.Count >= (index == 0 ? 1 : 3)) break;
                }
                if (highlighted.Count == 0) { Message(T("fine")); return; }
                hintTimer = 3.5f; progress.Use(index); Play("tool"); return;
            }
            if (index == 4)
            {
                int count = 0;
                foreach (int cellIndex in puzzle.Route)
                {
                    var c = puzzle.Cells[cellIndex];
                    if (c.Fixed || c.Stone || c.Mask == c.Correct) continue;
                    c.Mask = c.Correct; reachableDirty = true; wet.Remove(cellIndex); count++;
                    if (count >= 2) break;
                }
                if (count == 0) Message(T("fine"));
                else { progress.Use(index); Play("tool"); }
                return;
            }
            selectedTool = selectedTool == index ? -1 : index;
        }
        private void UseToolOnCell(int index)
        {
            var cell = puzzle.Cells[index];
            if (selectedTool == 1)
            {
                if (!cell.Stone) { Message(T("notUsable")); return; }
                cell.Stone = false; cell.Mask = 3; reachableDirty = true; wet.Remove(index);
                progress.Use(1); highlighted.Clear(); highlighted.Add(index); hintTimer = 1.7f;
                selectedTool = -1; Play("tool");
            }
            else if (selectedTool == 2 && !cell.Fixed && !cell.Stone) { shapeCell = index; selectedTool = -1; screen = "shape"; }
        }
        private void ChooseShape(int mask)
        {
            if (puzzle == null || shapeCell < 0 || shapeCell >= puzzle.Cells.Length) { screen = "game"; return; }
            puzzle.Cells[shapeCell].Mask = mask;
            reachableDirty = true; wet.Remove(shapeCell); progress.Use(2);
            highlighted.Clear(); highlighted.Add(shapeCell); hintTimer = 1.7f;
            shapeCell = -1; screen = "game"; Play("tool");
        }

        private void OnGUI()
        {
            // Touch detection is handled in Update; only the actual repaint draws the game.
            if (Event.current.type != EventType.Repaint || progress == null || white == null) return;
            var originalMatrix = GUI.matrix;
            var safe = Screen.safeArea;
            float top = Screen.height - safe.yMax;
            contentScale = Mathf.Min(safe.width / W, safe.height / H);
            offsetX = safe.xMin + (safe.width - W * contentScale) * .5f;
            offsetY = top + (safe.height - H * contentScale) * .5f;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offsetX, offsetY, 0), Quaternion.identity, Vector3.one * contentScale);
            GUI.BeginGroup(new Rect(0f, 0f, W, H));
            hits.Clear();
            switch (screen)
            {
                case "home": DrawHome(); break;
                case "map": case "mapAdvance": DrawMap(); break;
                case "intro": DrawIntro(); break;
                case "game": DrawGame(); break;
                case "win": DrawWin(); break;
                case "chest": DrawChest(); break;
                case "fail": DrawFail(); break;
                case "shop": DrawShop(); break;
                case "bag": DrawBag(); break;
                case "settings": DrawSettings(); break;
                case "shape": DrawShape(); break;
                case "challenge": DrawChallenge(); break;
                case "stats": DrawStats(); break;
            }
            if (toastTimer > 0) { Panel(new Rect(45, 103, 330, 55), C("#183251"), C("#dac187")); Label(toast, 210, 133, 15, C("#fff3c9"), 310); }
            GUI.EndGroup();
            GUI.matrix = originalMatrix;
        }

        private static Color C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
        private void RectFill(Rect r, Color color) { TintDraw(r, white, color); }
        private void TintDraw(Rect r, Texture2D tex, Color color)
        {
            if (tex == null) return;
            Color old = GUI.color; GUI.color = color; GUI.DrawTexture(r, tex, ScaleMode.StretchToFill, true); GUI.color = old;
        }
        private void RoundedRect(Rect r, Color color) { TintDraw(r, rounded, color); }
        private void DrawNineSlice(Rect rect, Texture2D tex, float borderX = 26, float borderY = 23)
        {
            if (tex == null) return;
            float sourceW = tex.width, sourceH = tex.height;
            float sx = Mathf.Min(sourceW / 3f, borderX * 2f);
            float sy = Mathf.Min(sourceH / 3f, borderY * 2f);
            float tx = Mathf.Min(rect.width * .39f, borderX);
            float ty = Mathf.Min(rect.height * .39f, borderY);
            float[] cx = { 0, tx, rect.width - tx, rect.width };
            float[] cy = { 0, ty, rect.height - ty, rect.height };
            float[] ix = { 0, sx, sourceW - sx, sourceW };
            float[] iy = { 0, sy, sourceH - sy, sourceH };
            for (int row = 0; row < 3; row++)
            for (int col = 0; col < 3; col++)
            {
                Rect target = new Rect(rect.x + cx[col], rect.y + cy[row], cx[col + 1] - cx[col], cy[row + 1] - cy[row]);
                Rect uv = new Rect(ix[col] / sourceW, 1f - iy[row + 1] / sourceH,
                    (ix[col + 1] - ix[col]) / sourceW, (iy[row + 1] - iy[row]) / sourceH);
                GUI.DrawTextureWithTexCoords(target, tex, uv, true);
            }
        }
        private void Panel(Rect r, Color fill, Color border)
        {
            if (textures.TryGetValue("ui/Panel_Large", out var skin) && skin != null)
            {
                DrawNineSlice(new Rect(r.x, r.y, r.width, r.height + 9), skin, 28, 28);
                return;
            }
            RoundedRect(new Rect(r.x, r.y + 9, r.width, r.height), C("#413357"));
            RoundedRect(new Rect(r.x, r.y + 3, r.width, r.height), border);
            RoundedRect(new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 7), fill);
        }
        private void Disk(Vector2 center, float radius, Color color)
        { TintDraw(new Rect(center.x-radius, center.y-radius, radius*2, radius*2), circle, color); }
        private void Star(Vector2 center, float radius, Color color)
        { TintDraw(new Rect(center.x-radius, center.y-radius, radius*2, radius*2), star, color); }
        private void Line(Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 diff = b - a;
            float length = diff.magnitude;
            if (length < .01f) return;
            Matrix4x4 m = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg, a);
            RectFill(new Rect(a.x, a.y - width*.5f, length, width), color);
            GUI.matrix = m;
        }
        private void Ring(Vector2 center, float radius, float width, float fraction, Color color)
        {
            int steps = Mathf.Max(2, Mathf.CeilToInt(56f * fraction));
            for (int i = 0; i < steps; i++)
            {
                float a = -Mathf.PI * .5f + i / (float)steps * Mathf.PI * 2 * fraction;
                float b = -Mathf.PI * .5f + (i + 1f) / steps * Mathf.PI * 2 * fraction;
                Line(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a))*radius,
                     center + new Vector2(Mathf.Cos(b), Mathf.Sin(b))*radius, width, color);
            }
        }
        private GUIStyle Style(int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter, bool wrap = false)
        {
            string key = size + "/" + ColorUtility.ToHtmlStringRGBA(color) + "/" + (int)anchor + "/" + wrap;
            if (styles.TryGetValue(key, out GUIStyle st)) return st;
            st = new GUIStyle(GUI.skin.label)
            {
                alignment = anchor,
                wordWrap = wrap,
                clipping = TextClipping.Clip,
                fontSize = size,
                fontStyle = FontStyle.Bold,
                richText = false
            };
            st.normal.textColor = color;
            st.hover.textColor = color;
            st.padding = new RectOffset(0, 0, 0, 0);
            st.margin = new RectOffset(0, 0, 0, 0);
            styles[key] = st;
            return st;
        }
        private void Label(string value, float x, float y, int size, Color color, float width = 300, float height = 44, bool wrap = false)
        {
            if (string.IsNullOrEmpty(value)) return;
            float availableW = Mathf.Max(1f, width - 8f);
            float availableH = Mathf.Max(1f, height - 6f);
            int fitted = size;
            if (!wrap)
            {
                string key = size + "/" + Mathf.RoundToInt(availableW) + "/" + Mathf.RoundToInt(availableH) + "/" + value;
                if (!labelSizes.TryGetValue(key, out fitted))
                {
                    while (fitted > 9)
                    {
                        GUIStyle sample = Style(fitted, color);
                        if (!textContents.TryGetValue(value, out GUIContent content))
                        {
                            content = new GUIContent(value);
                            if (textContents.Count > 256) textContents.Clear();
                            textContents[value] = content;
                        }
                        Vector2 needed = sample.CalcSize(content);
                        if (needed.x <= availableW && needed.y <= availableH) break;
                        fitted--;
                    }
                    if (labelSizes.Count > 384) labelSizes.Clear();
                    labelSizes[key] = fitted;
                }
            }
            GUI.Label(new Rect(x - width * .5f, y - height * .5f, width, height),
                value, Style(fitted, color, TextAnchor.MiddleCenter, wrap));
        }
        private void Image(string name, Rect rect, Color? tint = null)
        {
            if (!textures.TryGetValue(name, out Texture2D tex) && name.StartsWith("map"))
            {
                int number;
                if (int.TryParse(name.Substring(3), out number) && number >= 0 && number < 10)
                {
                    tex = LoadTexture("Images/Maps/Map" + (number + 1).ToString("D2"));
                    textures[name] = tex;
                }
            }
            if (tex != null) TintDraw(rect,tex,tint ?? Color.white);
        }
        private void Button(Rect rect, string caption, string action, int value = -1, string kind = "gold", int size = 20)
        {
            string name = "ui/Button_" + kind;
            if (textures.TryGetValue(name, out Texture2D tex) && tex != null)
                TintDraw(new Rect(rect.x, rect.y, rect.width, rect.height + 6f), tex, Color.white);
            else
            {
                RoundedRect(new Rect(rect.x, rect.y + 5, rect.width, rect.height), C("#5c4171"));
                RoundedRect(rect, C(kind == "gold" ? "#ffc76b" : kind == "dark" ? "#8583cc" : "#f6edf5"));
            }
            Color ink = C(kind == "dark" ? "#fff9de" : "#613650");
            Label(caption, rect.center.x, rect.center.y + 1f, size, ink, rect.width - 16, rect.height - 8);
            Register(rect, action, value);
        }
        private void CircleButton(Vector2 point, string caption, string action)
        {
            string symbol = caption == "⚙" ? "settings" : caption == "×" ? "x" : "back";
            string variant = screen == "game" ? "_dark" : "";
            string name = "ui/IconButton_" + symbol + variant;
            if (textures.TryGetValue(name, out Texture2D tex) && tex != null)
                TintDraw(new Rect(point.x - 27, point.y - 27, 54, 59), tex, Color.white);
            else
            {
                Disk(point + new Vector2(0, 4), 26, C("#473562"));
                Disk(point, 23, C("#fff5e2"));
                Label(caption, point.x, point.y, 27, C("#464574"), 50, 52);
            }
            Register(new Rect(point.x - 27, point.y - 27, 54, 58), action);
        }
        private void WorldBackground(int number, float dim)
        {
            Image("map"+Mathf.Clamp(number,0,9),new Rect(0,0,W,H));
            if (dim>0) RectFill(new Rect(0,0,W,H),new Color(.07f,.13f,.24f,dim));
        }

        private string homePendingAction;
        private Vector2 homePressedPoint;
        private int homePendingValue;
        private float homePressUntil;
        private Vector2 HomePointer()
        {
            return new Vector2((Input.mousePosition.x - offsetX) / contentScale,
                (Screen.height - Input.mousePosition.y - offsetY) / contentScale);
        }
        // Draw each menu button separately to preserve pointer feedback.
        private void HomeGameButton(Rect target, string kind, string action, string language)
        {
            bool hovered = Input.touchCount == 0 && target.Contains(HomePointer());
            bool down = homePendingAction == action && target.Contains(homePressedPoint)
                && Time.unscaledTime < homePressUntil;
            float amount = down ? .945f : hovered ? 1.035f : 1f;
            float drop = down ? 3.5f : hovered ? -2f : 0f;
            Rect visual = new Rect(target.center.x - target.width * amount * .5f,
                target.center.y - target.height * amount * .5f + drop,
                target.width * amount, target.height * amount);
            string buttonType = kind == "play" ? "gold" : kind == "map" ? "dark" : "light";
            if (textures.TryGetValue("ui/Button_" + buttonType, out Texture2D background) && background != null)
            {
                Color tint = down ? new Color(.89f, .90f, .93f, 1f)
                    : hovered ? new Color(1.07f, 1.07f, 1.07f, 1f) : Color.white;
                TintDraw(visual, background, tint);
            }
            else Button(visual, T(kind), action, -1, buttonType, 21);
            // Text and icon are one transparent foreground, kept inside the button margins.
            // The same press and hover transform applies to the full button.
            string foregroundPath = "Menu/" + language + "_" + kind;
            Texture2D foreground = ArtTexture(foregroundPath);
            if (foreground != null)
                TintDraw(visual, foreground, Color.white);
            else
                Label(T(kind), visual.center.x, visual.center.y, kind == "play" ? 32 : 20,
                    C("#53305a"), visual.width - 20, visual.height - 10);
            Register(target, action);
        }
        private void DrawHome()
        {
            string language = progress.Data.language;
            if (language != "en" && language != "tr" && language != "de" && language != "fr" && language != "es") language = "en";
            // Keep the decorative background separate from interactive controls.
            Texture2D homeArt = LoadTexture("Images/Home");
            if (homeArt != null) TintDraw(new Rect(0, 0, W, H), homeArt, Color.white);
            else RectFill(new Rect(0, 0, W, H), C("#073968"));
            RectFill(new Rect(0, 0, W, 124), new Color(.035f, .12f, .24f, .14f));
            CircleButton(new Vector2(43, 53), "⚙", "settings");
            if (textures.TryGetValue("ui/CoinBar", out Texture2D coins) && coins != null)
                TintDraw(new Rect(297, 31, 101, 47), coins, Color.white);
            DrawArt("UI/CoinMedallion", new Rect(303, 39, 28, 28));
            Label(progress.Data.gold.ToString(), 365f, 52f, 17, C("#774458"), 52f, 30f);
            Texture2D logo = ArtTexture("Menu/PipeFlowLogo");
            if (logo != null) TintDraw(new Rect(93, 207, 234, 177), logo, Color.white);
            else Label("PIPE FLOW", 210, 305, 55, C("#ffcb56"), 290, 89);
            Label(T("welcome"), 210, 393f, 16, C("#e9f7ff"), 280, 30);
            RoundedRect(new Rect(75, 412, 270, 47), new Color(.35f, .71f, .96f, .52f));
            RoundedRect(new Rect(77, 414, 266, 43), new Color(.035f, .19f, .42f, .93f));
            int currentWorld = Mathf.Clamp((progress.Data.unlocked - 1) / 12, 0, 9);
            Label(T("area") + " " + (currentWorld + 1) + "  ·  " + T("level") + " " + progress.Data.unlocked,
                210f, 434f, 16, C("#fff4dc"), 260f, 42f);
            HomeGameButton(new Rect(64, 486, 292, 76), "play", "play", language);
            HomeGameButton(new Rect(88, 582, 244, 58), "map", "map", language);
            HomeGameButton(new Rect(49, 667, 151, 54), "shop", "shop", language);
            HomeGameButton(new Rect(220, 667, 151, 54), "bag", "bag", language);
        }


        private WorldZone CurrentZone { get { return worlds != null && worlds.zones != null && zone >= 0 && zone < worlds.zones.Length ? worlds.zones[zone] : null; } }
        private Vector2 MapPoint(WorldPoint p) { return new Vector2(p.x,82+p.y*(705f/860f)); }

        private void DrawMeter()
        {
            int earned = progress.Data.chestStars;
            if (screen == "mapAdvance")
                earned = Mathf.RoundToInt(Mathf.Lerp(advanceOldStars, advanceNewStars,
                    Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((advanceTime - .14f)/.94f))));
            earned = Mathf.Clamp(earned, 0, 15);
            DrawArt("Map/Meter_" + earned, new Rect(0,183,82,106));
            Label(earned + "/15", 38, 268, 11, C("#fff9dd"), 36, 18);
            Register(new Rect(0,186,78,102), "chest");
            if (screen == "map" && Time.unscaledTime < chestHintUntil)
            {
                RoundedRect(new Rect(76f, 276f, 313f, 40f), new Color(.09f, .19f, .28f, .92f));
                Label(T("chestInfo"), 232f, 296f, 14, C("#fff5cf"), 300f, 34f);
            }
        }

        private Vector2 AnimatedRoadHead(int segment, float amount)
        {
            WorldZone data = CurrentZone;
            if (data == null || data.path == null || data.cuts == null || data.cuts.Length < 12) return Vector2.zero;
            int idx = Mathf.Clamp(segment, 0, 10);
            float first = data.cuts[idx], last = data.cuts[idx + 1];
            float t = Mathf.Clamp01(amount);
            int frame = Mathf.Clamp(Mathf.CeilToInt(t * 20f) - 1, 0, 19);
            string key = "Map/RoadTransition/Atlas_" + zone + "_" + idx;
            Texture2D tex = ArtTexture(key);
            int metadataIndex = zone * 11 + idx;
            if (tex != null && roadTransitionData != null && metadataIndex < roadTransitionData.Length)
            {
                RoadTransitionInfo info = roadTransitionData[metadataIndex];
                Rect uv = new Rect((frame % 4) * .25f, 1f - (frame / 4 + 1) * .2f, .25f, .2f);
                GUI.DrawTextureWithTexCoords(new Rect(info.x, info.y, info.w, info.h), tex, uv, true);
            }
            return SamplePath(data.path, Mathf.Lerp(first, last, t));
        }

        private Vector2 SamplePath(WorldPoint[] points, float index)
        {
            if (points == null || points.Length == 0) return Vector2.zero;
            float clamped = Mathf.Clamp(index, 0f, points.Length - 1f);
            int low = Mathf.FloorToInt(clamped);
            int high = Mathf.Min(points.Length - 1, low + 1);
            return Vector2.Lerp(MapPoint(points[low]), MapPoint(points[high]), clamped - low);
        }


        private WorldZone WorldData(int mapZone)
        {
            if (worlds == null || worlds.zones == null || mapZone < 0 || mapZone >= worlds.zones.Length) return null;
            return worlds.zones[mapZone];
        }

        private void WorldConnector(int whichZone, bool exit, float progress01)
        {
            WorldZone area = WorldData(whichZone);
            if (area == null || area.nodes == null || area.nodes.Length < 12) return;
            Vector2 node = MapPoint(area.nodes[exit ? 11 : 0]);
            Vector2 edge = exit ? new Vector2(Mathf.Min(390f, node.x + 23f), 82f)
                                : new Vector2(Mathf.Max(34f, node.x - 23f), 784f);
            Vector2 start = exit ? node : edge;
            Vector2 destination = exit ? edge : node;
            Vector2 control = exit ? new Vector2(node.x + 8f, node.y - 36f)
                                   : new Vector2(node.x - 14f, node.y + 26f);
            float fraction = Mathf.Clamp01(progress01);
            if (fraction <= 0f) return;
            int steps = Mathf.Max(1, Mathf.CeilToInt(12f * fraction));
            for (int layer = 0; layer < 3; layer++)
            {
                float width = layer == 0 ? 17f : layer == 1 ? 11.5f : 6f;
                Color color = layer == 0 ? C("#214b77") : layer == 1 ? C("#25b4e9") : C("#88f8ff");
                Vector2 last = start;
                for (int i = 1; i <= steps; i++)
                {
                    float t = Mathf.Min(fraction, i / 12f);
                    Vector2 first = Vector2.Lerp(start, control, t);
                    Vector2 second = Vector2.Lerp(control, destination, t);
                    Vector2 next = Vector2.Lerp(first, second, t);
                    Line(last, next, width, color);
                    last = next;
                }
            }
            if (fraction < 1f)
            {
                Vector2 a = Vector2.Lerp(start, control, fraction);
                Vector2 b = Vector2.Lerp(control, destination, fraction);
                Vector2 head = Vector2.Lerp(a, b, fraction);
                Disk(head, 11f, new Color(.52f, 1f, 1f, .26f));
                Disk(head, 4.5f, C("#f5ffff"));
            }
        }

        private void DrawPersistentWorldConnectors(int mapZone, WorldZone area)
        {
            if (area == null) return;
            int nextFirst = (mapZone + 1) * 12 + 1;
            if (mapZone < 9 && progress.Data.unlocked >= nextFirst) WorldConnector(mapZone, true, 1f);
            if (mapZone > 0 && progress.Data.unlocked >= mapZone * 12 + 1) WorldConnector(mapZone, false, 1f);
        }

        private void DrawWorldBoundaryTransition()
        {
            int previousZone = Mathf.Clamp((advanceFrom - 1) / 12, 0, 9);
            int newZone = Mathf.Clamp((advanceTo - 1) / 12, 0, 9);
            bool onNextWorld = advanceTime >= 1.02f;
            int displayedZone = onNextWorld ? newZone : previousZone;
            DrawScreenArtwork("map" + displayedZone);
            if (!onNextWorld)
            {
                DrawArt("Map/Road_" + previousZone + "_11", new Rect(0, 0, W, H));
                WorldConnector(previousZone, true, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((advanceTime - .08f) / .88f)));
            }
            else
            {
                WorldConnector(newZone, false, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((advanceTime - 1.02f) / 1.10f)));
            }
            WorldZone area = WorldData(displayedZone);
            int firstStage = displayedZone * 12 + 1;
            if (area != null && area.nodes != null)
            {
                for (int i = 0; i < Mathf.Min(12, area.nodes.Length); i++)
                {
                    Vector2 pos = MapPoint(area.nodes[i]);
                    int stage = firstStage + i;
                    bool clear = progress.IsCleared(stage), lockNode = stage > progress.Data.unlocked;
                    string marker = clear ? "Node_cleared_" + Mathf.Clamp(progress.BestStars(stage), 0, 3)
                        : lockNode ? "Node_locked_0" : "Node_unlocked_0";
                    DrawArt("Map/" + marker, new Rect(pos.x - 28f, pos.y - 28f, 56f, 56f));
                    if (!lockNode) Label(stage.ToString(), pos.x, pos.y, 16, C("#174962"), 44, 25);
                }
            }
            // A short cover fade hides the change of world artwork exactly when the path reaches the edge.
            float cover = 1f - Mathf.Clamp01(Mathf.Abs(advanceTime - 1.02f) / .16f);
            if (cover > 0f) RectFill(new Rect(0, 82, W, 702), new Color(.06f, .16f, .34f, cover * .72f));
            Label(T("movingOn") + "  " + advanceTo, 210, 119, 14, C("#fff8db"), 338, 28);
            Label(PipeLocale.Zone(progress.Data.language, displayedZone), 210, 34, 19, C("#fffae8"), 282, 34);
            Label(T("area") + " " + (displayedZone + 1) + " / 10", 210, 67, 12, C("#fff3dc"), 300);
            DrawMeter();
        }
        private void DrawMap()
        {
            if (screen == "mapAdvance" && crossingWorldBoundary) { DrawWorldBoundaryTransition(); return; }
            DrawScreenArtwork("map" + Mathf.Clamp(zone,0,9));
            int first=zone*12+1;
            int last=first+11;
            int current=progress.Data.unlocked;
            int completed = 0;
            while (completed < 12 && progress.IsCleared(first + completed)) completed++;
            int permanent = Mathf.Clamp(Mathf.Max(current - first, completed), 0, 11);
            bool isNewAdvance = screen == "mapAdvance" && animateNewConnection &&
                zone == (advanceFrom - 1) / 12 && advanceTo == current;
            int fixedPart = isNewAdvance ? Mathf.Min(permanent, Mathf.Max(0, advanceFrom - first)) : permanent;
            if (fixedPart > 0)
                DrawArt("Map/Road_" + zone + "_" + fixedPart, new Rect(0,0,W,H));
            if (isNewAdvance && fixedPart < permanent)
            {
                float fraction = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((advanceTime - .15f) / 1.5f));
                if (fraction >= 1f)
                    DrawArt("Map/Road_" + zone + "_" + permanent, new Rect(0,0,W,H));
                else if (fraction > 0f)
                {
                    Vector2 head = AnimatedRoadHead(fixedPart, fraction);
                    float pulse = .85f + .15f * Mathf.Sin(animTime * 16f);
                    Disk(head, 14f * pulse, new Color(.32f, .89f, 1f, .27f));
                    Disk(head, 6f * pulse, C("#eeffff"));
                }
            }
            WorldZone data=CurrentZone;
            DrawPersistentWorldConnectors(zone, data);
            if(data!=null && data.nodes!=null)
            for(int i=0;i<Mathf.Min(12,data.nodes.Length);i++)
            {
                Vector2 v=MapPoint(data.nodes[i]);
                int lv=first+i;
                bool done=progress.IsCleared(lv),locked=lv>current;
                int stars=progress.BestStars(lv);
                string marker=done?"Node_cleared_"+Mathf.Clamp(stars,0,3):locked?"Node_locked_0":"Node_unlocked_0";
                DrawArt("Map/"+marker,new Rect(v.x-28,v.y-28,56,56));
                if(!locked)
                    Label(lv.ToString(),v.x,v.y-2,16,C(done?"#165067":"#473455"),44,25);
                if(screen=="map")Register(new Rect(v.x-24,v.y-24,48,48),"choose",lv);
            }
            if (screen=="mapAdvance")
                Label(T("movingOn")+" · "+T("level")+" "+advanceTo,210,124,14,C("#fff8da"),310);
            Label(PipeLocale.Zone(progress.Data.language,zone),210,33,23,C("#fff9ee"),278);
            Label(T("area")+" "+(zone+1)+" / 10",210,67,12,C("#fff1d2"));
            Register(new Rect(14,27,44,45),"home");
            Register(new Rect(362,27,46,45),"settings");
            LabelInRect(new Rect(12,804,63,44),"‹",29,C("#fff9de"),"mapPrev");
            LabelInRect(new Rect(345,804,63,44),"›",29,C("#fff9de"),"mapNext");
            int picked=Mathf.Clamp(current,first,last);
            LabelInRect(new Rect(88,802,245,48),current>=first?T("level")+" "+picked:T("locked"),19,C("#613650"),"choose",picked);
            DrawMeter();
        }

        private void DrawIntro()
        {
            DrawScreenArtwork("intro"+Mathf.Clamp((selectedLevel-1)/12,0,9));
            Label(T("level")+" "+selectedLevel,210,273,17,C("#fff9e3"),174,29);
            Label(T("ready"),210,329,34,C("#304a70"));
            Label(T("goalTxt"),210,431,16,C("#435672"),310);
            if(selectedLevel>=3)Label(T("starGoal"),210,458,15,C("#a27d41"),320);
            Label(T("chest")+" "+Mathf.Min(15,progress.Data.chestStars)+"/15 ★",210,504,16,C("#7c815d"));
            LabelInRect(new Rect(74,552,272,70),T("go"),26,C("#613650"),"start",selectedLevel);
            Register(new Rect(340,220,40,42),"map");
        }

        private Rect TileRect(int index)
        {
            float step=BoardWidth/puzzle.Size;
            return new Rect(BoardLeft+(index%puzzle.Size)*step,BoardTop+(index/puzzle.Size)*step,step,step);
        }

        private void DrawTile(int index, Rect rect)
        {
            PipeCell cell=puzzle.Cells[index];
            int odd=(index%puzzle.Size+index/puzzle.Size)&1;
            if(cell.Stone) DrawArt("Tiles/Stone_"+odd,rect);
            else
            {
                int mask=cell.Mask&15;
                if (rotationProgress.TryGetValue(index, out float rotateT) &&
                    rotatingFrom.TryGetValue(index, out int oldMask))
                {
                    DrawArt("Tiles/Tile_0_" + odd, rect);
                    if (oldMask != 0)
                    {
                        int step = Mathf.Clamp(Mathf.FloorToInt(rotateT * 12f), 0, 11);
                        float margin = rect.width * (32f / 192f);
                        Rect expanded = new Rect(rect.x - margin, rect.y - margin,
                            rect.width + margin * 2f, rect.height + margin * 2f);
                        Texture2D pose = rotationSprites[oldMask, step];
                        if (pose != null) TintDraw(expanded, pose, Color.white);
                    }
                }
                else DrawArt("Tiles/Tile_" + mask + "_" + odd, rect);
                if(!rotationProgress.ContainsKey(index) && wet.TryGetValue(index,out float amount) && amount>.001f)
                {
                    int dir=wetEntry.TryGetValue(index,out int enter)?enter:3;
                    int step=Mathf.Clamp(Mathf.CeilToInt(amount*8f),1,8);
                    if(PipeLevelGenerator.Has(mask,dir))
                        DrawArt("Tiles/Wet_"+mask+"_"+dir+"_"+step,rect);
                }
                if(cell.Star)
                    DrawArt(wet.TryGetValue(index,out float ww)&&ww>=1f?"Tiles/waterstar":"Tiles/star",
                        new Rect(rect.center.x-rect.width*.175f,rect.center.y-rect.height*.175f,
                            rect.width*.35f,rect.height*.35f));
                if(hintTimer>0&&highlighted.Contains(index))
                {
                    var gl=new Color(1f,.89f,.45f,.18f);
                    RoundedRect(new Rect(rect.x+3,rect.y+3,rect.width-6,rect.height-6),gl);
                }
            }
        }






        private void DrawGame()
        {
            if(puzzle==null)return;
            int world=Mathf.Clamp((level-1)/12,0,9);
            DrawScreenArtwork((level%12==0?"boss":"game")+world);
            Register(new Rect(11,24,48,48),"back");
            Register(new Rect(358,24,48,48),"settings");
            Label(T("level")+"  "+level,210,47,27,C("#fff4da"),265);
            if(level%12==0)
                Label(T("time")+" "+bossTimer.ToString("F1")+"s",210,105,24,C("#ffe0a5"));
            else
            {
                Label(T("moves")+"  "+moves,112,170,16,C("#e8edcf"),180);
                Label(T("par")+"  "+puzzle.Par,313,170,16,C("#e8edcf"),190);
            }
            for(int index=0;index<puzzle.Cells.Length;index++)
            {
                Rect tile=TileRect(index);
                DrawTile(index,tile);
                Register(tile,"pipe",index);
            }
            float step=BoardWidth/puzzle.Size;
            float startY=BoardTop+(puzzle.Start/puzzle.Size+.5f)*step;
            float endY=BoardTop+(puzzle.Exit/puzzle.Size+.5f)*step;
            DrawArt("Tiles/left",new Rect(BoardLeft-8-48,startY-48,96,96));
            DrawArt("Tiles/right",new Rect(BoardLeft+BoardWidth+8-48,endY-48,96,96));
            Label(T("tools"),55,675,13,C("#dfe8cc"),110);
            Label(progress.Data.gold.ToString(),374,673,17,C("#ffdea2"),50);
            for(int k=0;k<5;k++)
            {
                float x=16+k*82.4f;
                bool unlocked=level>=PipeProgress.ItemUnlocks[k];
                if(unlocked)Label(progress.Data.inventory[k].ToString(),x+57,758,13,C("#79542e"),28,25);
                else Label("×",x+57,758,14,C("#ddd4ca"),28,25);
                Register(new Rect(x,697,72,74),"tool",k);
            }
            LabelInRect(new Rect(20,790,187,53),T("hint")+" · 100",17,C("#613650"),"hint");
            LabelInRect(new Rect(222,790,178,53),T("ad"),16,C("#fff9de"),"ad");
            if(selectedTool>=0)
                Label(T("chooseTile")+" · "+ItemName(selectedTool),210,628,13,C("#fff5db"),320);
            if(tutorial!="")
            {
                hits.Clear();
                RectFill(new Rect(0,0,W,H),new Color(.04f,.08f,.18f,.74f));
                Panel(new Rect(30,302,360,264),C("#fff6e7"),C("#b59ba9"));
                Label(T("help"),210,350,23,C("#304e7b"));
                Label(T(tutorial),210,443,15,C("#304563"),306,116,true);
                Button(new Rect(74,496,272,49),T("continue"),"tutorial",-1,"gold",19);
            }
        }

        private void DrawWin()
        {
            DrawScreenArtwork("win"+Mathf.Clamp(winStars,0,3));
            Label(T("completed"),210,233,14,C("#f9e5ca"),185,26);
            Label(T("won"),210,282,26,C("#5b4272"),330);
            Label("+"+winGold,237,456,30,C("#976d35"),170);
            Label(winGold>0?T("firstWin"):T("completed"),210,502,13,C("#8e6983"));
            LabelInRect(new Rect(65,555,290,62),T("next"),21,C("#613650"),"next");
            LabelInRect(new Rect(118,633,184,40),T("map"),15,C("#fff9de"),"map");
        }

        private void DrawChest()
        {
            DrawScreenArtwork("chestBackground0");
            Label(chestTime<.84f?T("chestProgress"):T("chestReady"),210,212,25,C("#2e4b77"),350);
            int index=Mathf.Clamp(rewardIndex,0,4);
            int f=Mathf.Clamp(Mathf.RoundToInt(chestTime*20f),0,30);
            DrawArt("ChestFrames/C"+index+"_"+f.ToString("D2"),new Rect(50,245,320,325));
            if(chestTime>=1.16f)
            {
                Label(T("reward"),210,560,13,C("#8a8969"));
                Label(ItemName(index)+"   +1",210,590,20,C("#334d74"),320);
            }
            if(chestTime>=1.38f)
                Button(new Rect(80,622,260,57),T("continue"),"chestContinue",-1,"gold",21);
        }

        private void DrawFail()
        {
            DrawScreenArtwork("fail0");
            Label(T("fail"),210,346,30,C("#985c4a"),315);
            LabelInRect(new Rect(75,459,270,61),T("retry"),21,C("#613650"),"retry");
            LabelInRect(new Rect(110,537,200,44),T("map"),16,C("#fff9de"),"map");
        }

        private void DrawItemRow(int i,float y,bool shop)
        {
            bool enabled=progress.Data.unlocked>=PipeProgress.ItemUnlocks[i];
            RoundedRect(new Rect(33,y,354,96),C("#e4e6d8"));
            Disk(new Vector2(78,y+46),34,C(enabled?"#efd59e":"#bdc9bd"));
            Image("item"+i,new Rect(49,y+16,58,58),enabled?Color.white:new Color(.4f,.4f,.4f,1));
            Label(ItemName(i),185,y+29,16,C("#345077"),137);
            if(shop)
            {
                Label(enabled?PipeProgress.ItemCosts[i]+" ◆":T("openAt")+" "+PipeProgress.ItemUnlocks[i],201,y+65,14,C("#9b794b"),142);
                Button(new Rect(284,y+56,94,33),enabled?T("buy"):"🔒","buy",i,enabled?"gold":"dark",13);
            }
            else Label(enabled?"×"+progress.Data.inventory[i]:T("openAt")+" "+PipeProgress.ItemUnlocks[i],327,y+35,16,C("#ac7c37"),85);
        }
        private void DrawShopPurchaseButton(int index, float rowY)
        {
            bool unlocked = progress.Data.unlocked >= PipeProgress.ItemUnlocks[index];
            bool affordable = progress.Data.gold >= PipeProgress.ItemCosts[index];
            bool canBuy = unlocked && affordable;
            Rect hitRect = new Rect(282f, rowY + 54f, 94f, 38f);
            bool pressed = shopPressedIndex == index && Time.unscaledTime < shopPressedUntil;
            bool hovered = Input.touchCount == 0 && hitRect.Contains(HomePointer());
            float scale = pressed ? .955f : hovered ? 1.025f : 1f;
            Rect buttonRect = new Rect(hitRect.center.x - 49f * scale,
                hitRect.center.y - 20f * scale + (pressed ? 2f : 0f),
                98f * scale, 44f * scale);
            string textureKey = canBuy ? "ui/SmallButton_gold" : "ui/SmallButton_dark";
            if (textures.TryGetValue(textureKey, out Texture2D skin) && skin != null)
                TintDraw(buttonRect, skin, Color.white);
            else RoundedRect(buttonRect, C(canBuy ? "#ffc46e" : "#706bb1"));

            string caption = unlocked
                ? PipeProgress.ItemCosts[index].ToString() + " ◆"
                : T("openAt") + " " + PipeProgress.ItemUnlocks[index];
            Label(caption, buttonRect.center.x, buttonRect.y + 18f,
                unlocked ? 13 : 11, C(canBuy ? "#69442e" : "#fff4dd"), 87f, 29f);
            Register(hitRect, "buy", index);
        }

        private void DrawShop()
        {
            // The background contains no baked purchase buttons; their states update at runtime.
            DrawScreenArtwork("shopClean");
            Label(T("shop"), 210f, 141f, 29, C("#2f4b75"));
            DrawArt("UI/CoinMedallion", new Rect(303f, 39f, 28f, 28f));
            Label(progress.Data.gold.ToString(), 363f, 52f, 17, C("#774458"), 53f, 30f);
            Register(new Rect(356f, 103f, 46f, 45f), "close");

            for (int i = 0; i < PipeProgress.ItemCosts.Length; i++)
            {
                float y = 186f + i * 109f;
                bool unlocked = progress.Data.unlocked >= PipeProgress.ItemUnlocks[i];
                if (unlocked)
                {
                    DrawArt("UI/ShopUnlockedBadge", new Rect(42f, y + 12f, 72f, 72f));
                    Image("item" + i, new Rect(49f, y + 16f, 58f, 58f));
                }
                else
                {
                    // Locked items remain grey until the required stage is reached.
                    Image("ui/Icon_lock", new Rect(89f, y + 55f, 20f, 20f));
                }
                Label(ItemName(i), 185f, y + 28f, 16, C("#344e75"), 145f);
                DrawShopPurchaseButton(i, y);
            }
            if (Time.unscaledTime < shopNoticeUntil)
                Label(shopNotice, 210f, 169f, 13, C("#987040"), 315f, 28f);
        }

        private void DrawBag()
        {
            DrawScreenArtwork("bag0");
            Label(T("fullBag"),210,152,28,C("#2f4b75"));
            Register(new Rect(356,111,46,43),"close");
            for(int i=0;i<5;i++)
            {
                float y=195+i*106;
                Label(ItemName(i),182,y+24,16,C("#344e75"),175);
                Label(progress.Data.unlocked>=PipeProgress.ItemUnlocks[i]?"×"+progress.Data.inventory[i]:T("openAt")+" "+PipeProgress.ItemUnlocks[i],
                    330,y+26,14,C("#ac7c37"),100);
            }

        }

        private void DrawSettings()
        {
            // The original screen's panel is preserved, but selected controls are fully dynamic.
            DrawScreenArtwork("settingsClean");
            Label(T("settings"), 210, 160, 29, C("#304b75"), 270, 42);
            Register(new Rect(344,105,48,46), "close");
            Label(T("language"), 210, 242, 15, C("#536682"), 180, 28);
            for (int i = 0; i < LanguageNames.Length; i++)
            {
                var r = new Rect(43+i*69,267,61,45);
                bool active = progress.Data.language == LanguageNames[i];
                SmallSettingsButton(r, LanguageNames[i].ToUpperInvariant(), active, "lang", i, 16);
            }
            Label(T("speed"), 210, 360, 15, C("#536682"), 200, 28);
            int[] speeds = {1,2,4};
            for (int i = 0; i < speeds.Length; i++)
            {
                var r = new Rect(58+i*112,388,98,62);
                SmallSettingsButton(r, speeds[i]+"×", progress.Data.speed == speeds[i], "speed", i, 23);
            }
            Label(T("music"), 210, 492, 15, C("#536682"), 200, 28);
            SmallSettingsButton(new Rect(120,515,180,44), T(progress.Data.music ? "on" : "off"),
                progress.Data.music, "music", -1, 16);
            Label(T("effects"), 210, 584, 15, C("#536682"), 200, 28);
            SmallSettingsButton(new Rect(120,608,180,43), T(progress.Data.sound ? "on" : "off"),
                progress.Data.sound, "sound", -1, 16);
        }

        private void SmallSettingsButton(Rect rect, string caption, bool selected, string action, int id, int fontSize)
        {
            string key = (rect.width > 125f ? "ui/Button_" : "ui/SmallButton_") + (selected ? "gold" : "light");
            if (!textures.TryGetValue(key, out Texture2D tex) || tex == null)
            {
                key = "ui/Button_" + (selected ? "gold" : "light");
                textures.TryGetValue(key, out tex);
            }
            if (tex != null) TintDraw(new Rect(rect.x,rect.y,rect.width,rect.height+5),tex,Color.white);
            Label(caption, rect.center.x, rect.center.y + 1, fontSize, C("#613650"), rect.width-8, rect.height-7);
            Register(rect, action, id);
        }


        private void DrawStats()
        {
            DrawScreenArtwork("home");
            RectFill(new Rect(0f, 0f, W, H), new Color(.02f, .09f, .22f, .65f));
            Panel(new Rect(24f, 168f, 372f, 520f), C("#fff2d9"), C("#876694"));
            Label(T("stats"), 210f, 211f, 29, C("#304b75"), 290f);
            int cleared = 0, stars = 0;
            for (int stage = 1; stage <= 120; stage++) { if (progress.IsCleared(stage)) cleared++; stars += progress.BestStars(stage); }
            Label(T("level") + "   " + cleared + "/120", 210f, 307f, 20, C("#304b75"), 314f);
            Label(T("stars") + "   " + stars + "/360", 210f, 385f, 22, C("#ab7630"), 314f);
            Label(T("coins") + "   " + progress.Data.gold, 210f, 460f, 20, C("#9a6632"), 314f);
            Label(T("chest") + "   " + progress.Data.chestStars + "/15", 210f, 529f, 19, C("#305a62"), 314f);
            Button(new Rect(92f, 603f, 236f, 62f), T("back"), "close", -1, "dark", 19);
            Register(new Rect(357, 183, 45, 44), "close");
        }

        private void DrawChallenge()
        {
            DrawScreenArtwork("home");
            RectFill(new Rect(0f, 0f, W, H), new Color(.02f, .09f, .22f, .68f));
            Panel(new Rect(24f, 150f, 372f, 560f), C("#fff3db"), C("#9271a1"));
            Label(T("challenge"), 210f, 204f, 27, C("#304b75"), 315f);
            Label(T("timed"), 210f, 251f, 16, C("#6a7890"), 312f);
            for (int i = 0; i < 4; i++)
            {
                int challengeLevel = (i+1) * 12;
                bool available = progress.Data.unlocked >= challengeLevel;
                Rect button = new Rect(62f, 298f + i * 78f, 296f, 62f);
                Button(button, T("level") + "  " + challengeLevel + (available ? "" : "  🔒"),
                    "challengeLevel", challengeLevel, available ? "gold" : "dark", 19);
            }
            Register(new Rect(359, 167, 44, 45), "close");
            Button(new Rect(105f, 624f, 210f, 55f), T("back"), "close", -1, "light", 17);
        }

        private void DrawShape()
        {
            DrawGame();hits.Clear();RectFill(new Rect(0,0,W,H),new Color(.05f,.08f,.20f,.78f));
            Panel(new Rect(28,308,364,255),C("#fff6e7"),C("#b59ba9"));
            Label(T("selectShape"),210,356,22,C("#304e7b"));
            int[] masks={5,3,7};
            for(int i=0;i<3;i++)
            {
                float x=60+i*107;
                Disk(new Vector2(x+46,426),39,C("#365276"));
                Star(new Vector2(x+46,426),23,C("#f5c878"));
                Label(T(i==0?"shapeStraight":i==1?"shapeElbow":"shapeT"),x+46,492,13,C("#53647c"),105);
                Register(new Rect(x,385,100,115),"shape",masks[i]);
            }
        }
    }
}
