using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SoR.Core;
using SoR.Shared;
using SoR.Systems.Inventory;
using SoR.Systems.Crafting;
using SoR.Systems.Gacha;
using SoR.Systems.Shop;
using SoR.Systems.Quests;
using SoR.Gameplay;
using SoR.UI;
using UnityEngine.EventSystems;

namespace SoR.Testing
{
    /// <summary>
    /// Builds all UI screens at runtime with test data and keyboard shortcuts.
    /// Attach alongside TestSceneSetup or on its own in any scene.
    ///
    /// Keybinds (hold TAB to see hint bar):
    ///   I = Inventory       C = Crafting      M = Map
    ///   B = Shop (Buy)      G = Gacha         Q = Quest Log
    ///   K = Skills          P = Companions    E = Equipment
    ///   ESC = Close current screen
    /// </summary>
    public class TestMenuUI : MonoBehaviour
    {
        // ---- systems (created here if not already registered) ----
        private InventorySystem _inventory;
        private EquipmentSystem _equipment;
        private CraftingSystem _crafting;
        private GachaSystem _gacha;
        private ShopSystem _shop;
        private QuestManager _questManager;

        // ---- UI root ----
        private Canvas _canvas;
        private Font _font;

        // ---- screen panels ----
        private GameObject _activeScreen;
        private readonly Dictionary<KeyCode, System.Action> _screenKeys = new();

        // ---- test data ----
        private BannerDefinitionSO _testBanner;
        private ShopInventorySO _testShopDef;
        private readonly List<RecipeDefinitionSO> _testRecipes = new();
        private readonly List<QuestDefinitionSO> _testQuests = new();
        private int _gold = 5000;
        private int _accordEssence;

        // ---- gacha log ----
        private Text _gachaLogText;
        private readonly List<string> _gachaLog = new();

        // ---- hint bar ----
        private GameObject _hintBar;

        // ---- gacha roulette ----
        private GachaRouletteUI _roulette;
        private bool _isGachaAnimating;

        // ---- persistent HUD elements ----
        private ItemNotificationUI _notificationUI;
        private QuestTrackerUI _questTrackerUI;

        // ---- cheat menu ref ----
        private TestSceneSetup _sceneSetup;
        private readonly HashSet<string> _unlockedCompanions = new();

        // ---- party slots ----
        private string _partyActiveId;
        private string _partySupportId;

        private void Start()
        {
            _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (_font == null)
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _sceneSetup = GetComponent<TestSceneSetup>();

            // Ensure an EventSystem exists so UI buttons work
            if (FindObjectOfType<EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<EventSystem>();
                esGo.AddComponent<StandaloneInputModule>();
            }

            RegisterSystems();
            SeedTestData();
            BuildCanvas();
            BuildHintBar();

            // Map keybinds to screen builders
            _screenKeys[KeyCode.I] = ShowInventory;
            _screenKeys[KeyCode.B] = ShowShop;
            _screenKeys[KeyCode.C] = ShowCrafting;
            _screenKeys[KeyCode.G] = ShowGacha;
            _screenKeys[KeyCode.P] = ShowCompanions;
            _screenKeys[KeyCode.M] = ShowMap;
            _screenKeys[KeyCode.Q] = ShowQuestLog;
            _screenKeys[KeyCode.K] = ShowSkills;
            _screenKeys[KeyCode.E] = ShowEquipment;
            _screenKeys[KeyCode.BackQuote] = ShowCheatMenu;

            // Persistent HUD elements (event-driven, no manual wiring needed)
            _notificationUI = ItemNotificationUI.Create(_canvas, _font);
            _questTrackerUI = QuestTrackerUI.Create(_canvas, _font);

            // Quest objective tracker — bridges game events to QuestManager.UpdateObjective()
            var trackerGo = new GameObject("QuestObjectiveTracker");
            trackerGo.AddComponent<SoR.Systems.Quests.QuestObjectiveTracker>();
        }

        private void Update()
        {
            // Show/hide hint bar while holding TAB
            if (_hintBar != null)
                _hintBar.SetActive(UnityEngine.Input.GetKey(KeyCode.Tab));

            // Close active screen
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                CloseActiveScreen();
                return;
            }

            // Only open screens when nothing is open (prevents accidental toggling)
            if (_activeScreen != null) return;

            foreach (var kvp in _screenKeys)
            {
                if (UnityEngine.Input.GetKeyDown(kvp.Key))
                {
                    kvp.Value.Invoke();
                    return;
                }
            }
        }

        // ================================================================
        // System bootstrap
        // ================================================================

        private void RegisterSystems()
        {
            // Register systems only if not already present
            if (!ServiceLocator.TryResolve<InventorySystem>(out _inventory))
            {
                _inventory = new InventorySystem();
                ServiceLocator.Register(_inventory);
            }

            if (!ServiceLocator.TryResolve<EquipmentSystem>(out _equipment))
            {
                _equipment = new EquipmentSystem();
                ServiceLocator.Register(_equipment);
            }

            if (!ServiceLocator.TryResolve<CraftingSystem>(out _crafting))
            {
                _crafting = new CraftingSystem();
                ServiceLocator.Register(_crafting);
            }

            if (!ServiceLocator.TryResolve<GachaSystem>(out _gacha))
            {
                _gacha = new GachaSystem();
                ServiceLocator.Register(_gacha);
            }

            if (!ServiceLocator.TryResolve<ShopSystem>(out _shop))
            {
                _shop = new ShopSystem();
                ServiceLocator.Register(_shop);
            }

            if (!ServiceLocator.TryResolve<QuestManager>(out _questManager))
            {
                _questManager = new QuestManager();
                ServiceLocator.Register(_questManager);
            }

            // Listen for essence
            EventBus.Subscribe<AccordEssenceGainedEvent>(e => _accordEssence += e.Amount);
        }

        // ================================================================
        // Test data seeding
        // ================================================================

        private void SeedTestData()
        {
            // --- Items ---
            _inventory.AddItem("health_potion", 12);
            _inventory.AddItem("verdance_shard", 5);
            _inventory.AddItem("iron_ore", 30);
            _inventory.AddItem("herb_bundle", 20);
            _inventory.AddItem("fire_crystal", 3);
            _inventory.AddItem("silk_thread", 15);
            _inventory.AddItem("runic_dust", 8);
            _inventory.AddItem("ancient_seed", 2);
            _inventory.AddItem("wolf_pelt", 7);
            _inventory.AddItem("moonstone", 1);

            // --- Equipment ---
            _equipment.Equip("iron_sword", EquipmentSlot.Weapon);
            _equipment.Equip("leather_helm", EquipmentSlot.Head);

            // --- Crafting Recipes ---
            _testRecipes.Add(CreateRecipe("Healing Salve", "healing_salve",
                CraftingDiscipline.Herbalism, 1,
                ("herb_bundle", 3)));
            _testRecipes.Add(CreateRecipe("Iron Blade", "iron_blade",
                CraftingDiscipline.Forging, 2,
                ("iron_ore", 5), ("fire_crystal", 1)));
            _testRecipes.Add(CreateRecipe("Verdant Seed Bomb", "seed_bomb",
                CraftingDiscipline.Seedcraft, 1,
                ("ancient_seed", 1), ("runic_dust", 2)));
            _testRecipes.Add(CreateRecipe("Rune of Warding", "rune_warding",
                CraftingDiscipline.Runebinding, 3,
                ("runic_dust", 4), ("moonstone", 1)));

            // --- Shop ---
            _testShopDef = ScriptableObject.CreateInstance<ShopInventorySO>();
            _testShopDef.ShopName = "Greenreach General Store";
            _testShopDef.ShopId = "greenreach_shop";
            _testShopDef.Items = new List<ShopItem>
            {
                new ShopItem { ItemId = "health_potion", Price = 50, Stock = -1 },
                new ShopItem { ItemId = "verdance_shard", Price = 120, Stock = 5 },
                new ShopItem { ItemId = "iron_ore", Price = 25, Stock = -1 },
                new ShopItem { ItemId = "herb_bundle", Price = 15, Stock = -1 },
                new ShopItem { ItemId = "fire_crystal", Price = 300, Stock = 3 },
                new ShopItem { ItemId = "silk_thread", Price = 40, Stock = 10 },
                new ShopItem { ItemId = "runic_dust", Price = 80, Stock = 8 },
                new ShopItem { ItemId = "moonstone", Price = 500, Stock = 1 }
            };
            _shop.RegisterShop(_testShopDef);

            // --- Gacha Banner ---
            var pityConfig = ScriptableObject.CreateInstance<PityConfigSO>();
            pityConfig.LegendarySoftPity = 70;
            pityConfig.LegendaryHardPity = 90;
            pityConfig.MythicHardPity = 180;
            pityConfig.SoftPityRateBoost = 0.05f;

            _testBanner = ScriptableObject.CreateInstance<BannerDefinitionSO>();
            _testBanner.BannerName = "Verdant Bloom";
            _testBanner.BannerId = "verdant_bloom_01";
            _testBanner.Description = "Featured: Lyra, the Forest Warden";
            _testBanner.CommonRate = 0.80f;
            _testBanner.RareRate = 0.15f;
            _testBanner.LegendaryRate = 0.04f;
            _testBanner.MythicRate = 0.01f;
            _testBanner.PityConfig = pityConfig;
            _testBanner.CommonPool = new List<string>
                { "companion_villager", "companion_farmer", "companion_scout", "companion_apprentice" };
            _testBanner.RarePool = new List<string>
                { "companion_knight", "companion_pyromancer", "companion_ranger", "companion_priest" };
            _testBanner.LegendaryPool = new List<string>
                { "companion_lyra", "companion_thorne", "companion_selene" };
            _testBanner.MythicPool = new List<string> { "companion_eldara" };
            _testBanner.FeaturedCompanionId = "companion_lyra";
            _testBanner.FeaturedRarity = Rarity.Legendary;

            // --- Quests ---
            _testQuests.Add(CreateQuest("The Blight Spreads", "quest_blight_01",
                QuestType.MainStory,
                "Investigate the source of the blight in Greenreach.",
                new[] { ("Defeat blighted wolves", ObjectiveType.Kill, "blighted_wolf", 5) },
                new[] { ("health_potion", 3, 100, 50) }));

            _testQuests.Add(CreateQuest("Herb Gathering", "quest_herbs_01",
                QuestType.SideQuest,
                "Collect herbs for the village healer.",
                new[] { ("Collect herb bundles", ObjectiveType.Collect, "herb_bundle", 10) },
                new[] { ("verdance_shard", 1, 50, 30) }));

            _testQuests.Add(CreateQuest("The Forgemaster's Test", "quest_forge_01",
                QuestType.GuildContract,
                "Craft an iron blade to prove your skill.",
                new[] { ("Craft an Iron Blade", ObjectiveType.Craft, "iron_blade", 1) },
                new[] { ("fire_crystal", 2, 200, 100) }));

            _testQuests.Add(CreateQuest("Lyra's Memory", "quest_lyra_01",
                QuestType.CompanionQuest,
                "Help Lyra recover fragments of her lost memory in the Ashen Steppe.",
                new[] {
                    ("Find memory fragments", ObjectiveType.Explore, "memory_fragment", 3),
                    ("Talk to the Elder", ObjectiveType.Talk, "elder_npc", 1)
                },
                new[] { ("ancient_seed", 1, 300, 75) }));

            // --- Main Story quest chain (Greenreach → Withered Heart) ---

            var elderQuest = CreateQuest("The Elder's Warning", "quest_elder_02",
                QuestType.MainStory,
                "Elder Mirren senses a deeper corruption behind the blight. Visit the village shrine to learn more.",
                new[] {
                    ("Talk to Elder Mirren", ObjectiveType.Talk, "elder_mirren", 1),
                    ("Explore the village shrine", ObjectiveType.Explore, "village_shrine", 1)
                },
                new[] { ("verdance_shard", 2, 120, 60) });
            elderQuest.PrerequisiteQuestIds = new[] { "quest_blight_01" };
            _testQuests.Add(elderQuest);

            var wardenQuest = CreateQuest("The Border Watch", "quest_warden_03",
                QuestType.MainStory,
                "Warden Sable reports strange creatures at the forest edge. Patrol the border and eliminate them.",
                new[] {
                    ("Kill border prowlers", ObjectiveType.Kill, "border_prowler", 6),
                    ("Explore the forest edge", ObjectiveType.Explore, "forest_edge", 1)
                },
                new[] { ("iron_shield", 1, 150, 80) });
            wardenQuest.PrerequisiteQuestIds = new[] { "quest_elder_02" };
            _testQuests.Add(wardenQuest);

            var healerQuest = CreateQuest("Maren's Remedy", "quest_healer_04",
                QuestType.MainStory,
                "Healer Maren needs blight samples to formulate an antidote before the corruption spreads further.",
                new[] {
                    ("Collect blight samples", ObjectiveType.Collect, "blight_sample", 8)
                },
                new[] { ("health_potion", 5, 130, 70) });
            healerQuest.PrerequisiteQuestIds = new[] { "quest_elder_02" };
            _testQuests.Add(healerQuest);

            // Ashen Steppe
            var steppeQuest = CreateQuest("Ashes and Omens", "quest_steppe_05",
                QuestType.MainStory,
                "Watcher Sera has seen dark omens in the scorched plains. Seek her counsel and purge the scorched husks.",
                new[] {
                    ("Talk to Watcher Sera", ObjectiveType.Talk, "watcher_sera", 1),
                    ("Kill scorched husks", ObjectiveType.Kill, "scorched_husk", 8)
                },
                new[] { ("fire_crystal", 2, 250, 120) });
            steppeQuest.PrerequisiteQuestIds = new[] { "quest_warden_03" };
            _testQuests.Add(steppeQuest);

            // Frosthollow Peaks
            var peaksQuest = CreateQuest("Frozen Lore", "quest_peaks_06",
                QuestType.MainStory,
                "Scholar Veylin believes ancient scrolls hidden in the peaks hold the key to understanding the blight's origin.",
                new[] {
                    ("Collect ancient scrolls", ObjectiveType.Collect, "ancient_scroll", 4),
                    ("Talk to Scholar Veylin", ObjectiveType.Talk, "scholar_veylin", 1)
                },
                new[] { ("frost_shard", 3, 350, 150) });
            peaksQuest.PrerequisiteQuestIds = new[] { "quest_steppe_05" };
            _testQuests.Add(peaksQuest);

            var gateQuest = CreateQuest("The Mountain Gate", "quest_gate_07",
                QuestType.MainStory,
                "Sentinel Kaelos guards the mountain gate — but frost sentinels have overrun the pass. Clear the way.",
                new[] {
                    ("Kill frost sentinels", ObjectiveType.Kill, "frost_sentinel", 6),
                    ("Talk to Sentinel Kaelos", ObjectiveType.Talk, "sentinel_kaelos", 1)
                },
                new[] { ("runic_core", 1, 400, 180) });
            gateQuest.PrerequisiteQuestIds = new[] { "quest_peaks_06" };
            _testQuests.Add(gateQuest);

            // Gloomtide Marshes
            var marshQuest = CreateQuest("Visions in the Mire", "quest_marsh_08",
                QuestType.MainStory,
                "Oracle Nyx receives visions of an approaching darkness. Explore the vision sites she has marked.",
                new[] {
                    ("Talk to Oracle Nyx", ObjectiveType.Talk, "oracle_nyx", 1),
                    ("Explore vision sites", ObjectiveType.Explore, "vision_site", 3)
                },
                new[] { ("shadow_essence", 3, 450, 200) });
            marshQuest.PrerequisiteQuestIds = new[] { "quest_gate_07" };
            _testQuests.Add(marshQuest);

            var gloomQuest = CreateQuest("Through the Gloom", "quest_gloom_09",
                QuestType.MainStory,
                "Ranger Theron knows a path through the deepest marshes, but marsh wraiths block the way.",
                new[] {
                    ("Kill marsh wraiths", ObjectiveType.Kill, "marsh_wraith", 8),
                    ("Talk to Ranger Theron", ObjectiveType.Talk, "ranger_theron", 1)
                },
                new[] { ("moonstone_fragment", 2, 500, 220) });
            gloomQuest.PrerequisiteQuestIds = new[] { "quest_marsh_08" };
            _testQuests.Add(gloomQuest);

            // Withered Heart
            var echoQuest = CreateQuest("Echoes of the Past", "quest_echo_10",
                QuestType.MainStory,
                "A spectral echo of Elder Mirren lingers at the blight's nexus. Seek the truth before it fades.",
                new[] {
                    ("Talk to the Echo of Mirren", ObjectiveType.Talk, "echo_mirren", 1),
                    ("Explore blight nexus points", ObjectiveType.Explore, "blight_nexus", 3)
                },
                new[] { ("ancient_seed", 3, 600, 280) });
            echoQuest.PrerequisiteQuestIds = new[] { "quest_gloom_09" };
            _testQuests.Add(echoQuest);

            var finalQuest = CreateQuest("The Blightcaller", "quest_final_11",
                QuestType.MainStory,
                "The source of all corruption awaits at the heart of the Withered lands. End this — once and for all.",
                new[] {
                    ("Defeat the Blightcaller", ObjectiveType.Kill, "blightcaller", 1)
                },
                new[] { ("ancient_seed", 5, 1000, 500) });
            finalQuest.PrerequisiteQuestIds = new[] { "quest_echo_10" };
            _testQuests.Add(finalQuest);

            // --- Companion Quests ---

            var thorneQuest = CreateQuest("Thorne's Bounty", "quest_thorne_01",
                QuestType.CompanionQuest,
                "Thorne is tracking a bounty in the Frosthollow Peaks. Help him hunt frost reavers and question an informant.",
                new[] {
                    ("Kill frost reavers", ObjectiveType.Kill, "frost_reaver", 5),
                    ("Talk to the informant", ObjectiveType.Talk, "thorne_informant", 1)
                },
                new[] { ("frost_shard", 2, 350, 160) });
            thorneQuest.PrerequisiteQuestIds = new[] { "quest_peaks_06" };
            _testQuests.Add(thorneQuest);

            var seleneQuest = CreateQuest("Selene's Rite", "quest_selene_01",
                QuestType.CompanionQuest,
                "Selene must perform an ancient rite in the Gloomtide Marshes. Gather moonlight essence to aid her.",
                new[] {
                    ("Collect moonlight essence", ObjectiveType.Collect, "moonlight_essence", 5),
                    ("Talk to Selene", ObjectiveType.Talk, "selene", 1)
                },
                new[] { ("moonstone_fragment", 3, 450, 200) });
            seleneQuest.PrerequisiteQuestIds = new[] { "quest_marsh_08" };
            _testQuests.Add(seleneQuest);

            var eldaraQuest = CreateQuest("Eldara's Awakening", "quest_eldara_01",
                QuestType.CompanionQuest,
                "Eldara senses verdant seeds buried within the Withered Heart. Help her awaken them to reclaim the land.",
                new[] {
                    ("Explore verdant seed sites", ObjectiveType.Explore, "verdant_seed", 4),
                    ("Talk to Eldara", ObjectiveType.Talk, "eldara", 1)
                },
                new[] { ("ancient_seed", 3, 700, 350) });
            eldaraQuest.PrerequisiteQuestIds = new[] { "quest_final_11" };
            _testQuests.Add(eldaraQuest);

            // Accept the first two quests
            _questManager.AcceptQuest(_testQuests[0]);
            _questManager.AcceptQuest(_testQuests[1]);
        }

        // ================================================================
        // Canvas
        // ================================================================

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("TestMenuCanvas");
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 200;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();
        }

        private void BuildHintBar()
        {
            _hintBar = new GameObject("HintBar");
            _hintBar.transform.SetParent(_canvas.transform, false);
            var rt = _hintBar.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, 36f);

            var bg = _hintBar.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.85f);

            var textGo = new GameObject("HintText");
            textGo.transform.SetParent(_hintBar.transform, false);
            var text = textGo.AddComponent<Text>();
            text.text = "[I] Inventory  [E] Equipment  [B] Shop  [C] Crafting  [G] Gacha  [P] Companions  [M] Map  [Q] Quests  [K] Skills  [`] Cheats  [ESC] Close";
            text.font = _font;
            text.fontSize = 16;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            _hintBar.SetActive(false);
        }

        // ================================================================
        // Screen management
        // ================================================================

        private void CloseActiveScreen()
        {
            if (_roulette != null)
            {
                _roulette.Dismiss();
                _roulette = null;
                _isGachaAnimating = false;
            }

            if (_activeScreen != null)
            {
                Destroy(_activeScreen);
                _activeScreen = null;
            }
        }

        private GameObject CreateScreenPanel(string title)
        {
            CloseActiveScreen();

            var panel = new GameObject(title + "Screen");
            panel.transform.SetParent(_canvas.transform, false);
            var rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.1f, 0.05f);
            rt.anchorMax = new Vector2(0.9f, 0.95f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Semi-transparent background
            var bg = panel.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);

            // Title bar
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(panel.transform, false);
            var titleRt = titleGo.AddComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = Vector2.zero;
            titleRt.sizeDelta = new Vector2(0f, 50f);
            var titleBg = titleGo.AddComponent<Image>();
            titleBg.color = new Color(0.15f, 0.12f, 0.2f, 1f);

            var titleText = new GameObject("TitleText");
            titleText.transform.SetParent(titleGo.transform, false);
            var tt = titleText.AddComponent<Text>();
            tt.text = title;
            tt.font = _font;
            tt.fontSize = 24;
            tt.fontStyle = FontStyle.Bold;
            tt.color = new Color(0.95f, 0.85f, 0.5f);
            tt.alignment = TextAnchor.MiddleCenter;
            var ttRt = titleText.GetComponent<RectTransform>();
            ttRt.anchorMin = Vector2.zero;
            ttRt.anchorMax = Vector2.one;
            ttRt.offsetMin = Vector2.zero;
            ttRt.offsetMax = Vector2.zero;

            // Close hint
            AddLabel(titleGo.transform, "[ESC] Close", 12, TextAnchor.MiddleRight,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-10f, 0f), new Vector2(120f, 30f), new Color(0.6f, 0.6f, 0.6f));

            _activeScreen = panel;
            return panel;
        }

        // ================================================================
        // INVENTORY SCREEN
        // ================================================================

        private void ShowInventory()
        {
            var panel = CreateScreenPanel("Inventory");
            var items = _inventory.GetAllItems();

            var content = CreateScrollContent(panel.transform, new Vector2(0f, 0f), new Vector2(1f, 0.9f));

            int row = 0;
            foreach (var kvp in items)
            {
                string line = $"  {FormatItemName(kvp.Key)}  x{kvp.Value}";
                var label = AddRowLabel(content, line, row, GetRarityColor(kvp.Key));
                row++;
            }

            if (items.Count == 0)
                AddRowLabel(content, "  (empty)", 0, Color.gray);

            // Gold display
            AddLabel(panel.transform, $"Gold: {_gold}", 18, TextAnchor.MiddleRight,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-20f, -60f), new Vector2(200f, 30f), new Color(1f, 0.85f, 0.3f));

            SetContentHeight(content, row);
        }

        // ================================================================
        // EQUIPMENT SCREEN
        // ================================================================

        private void ShowEquipment()
        {
            var panel = CreateScreenPanel("Equipment");
            var content = CreateScrollContent(panel.transform, new Vector2(0f, 0f), new Vector2(1f, 0.9f));

            string[] slotNames = { "Weapon", "Head", "Chest", "Legs", "Accessory" };
            EquipmentSlot[] slots = {
                EquipmentSlot.Weapon, EquipmentSlot.Head, EquipmentSlot.Chest,
                EquipmentSlot.Legs, EquipmentSlot.Accessory
            };

            for (int i = 0; i < slots.Length; i++)
            {
                string equipped = _equipment.GetEquipped(slots[i]);
                string display = string.IsNullOrEmpty(equipped) ? "(empty)" : FormatItemName(equipped);
                Color color = string.IsNullOrEmpty(equipped) ? Color.gray : new Color(0.6f, 0.85f, 1f);
                AddRowLabel(content, $"  [{slotNames[i]}]  {display}", i, color);
            }

            // Stat bonuses
            var stats = _equipment.GetTotalStatBonuses();
            AddRowLabel(content, "", 5, Color.white);
            AddRowLabel(content, "  --- Stat Bonuses ---", 6, new Color(0.95f, 0.85f, 0.5f));
            AddRowLabel(content, $"  VIG {stats.Vigor:+0;-0;0}  STR {stats.Strength:+0;-0;0}  HAR {stats.Harvest:+0;-0;0}", 7, Color.white);
            AddRowLabel(content, $"  VER {stats.Verdance:+0;-0;0}  AGI {stats.Agility:+0;-0;0}  RES {stats.Resilience:+0;-0;0}", 8, Color.white);

            SetContentHeight(content, 9);
        }

        // ================================================================
        // SHOP SCREEN
        // ================================================================

        private void ShowShop()
        {
            var panel = CreateScreenPanel("Greenreach General Store");
            var content = CreateScrollContent(panel.transform, new Vector2(0f, 0f), new Vector2(1f, 0.9f));

            // Gold header
            AddLabel(panel.transform, $"Gold: {_gold}", 18, TextAnchor.MiddleRight,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-20f, -60f), new Vector2(200f, 30f), new Color(1f, 0.85f, 0.3f));

            int row = 0;
            foreach (var item in _testShopDef.Items)
            {
                string stockStr = item.Stock < 0 ? "inf" : item.Stock.ToString();
                bool canBuy = _shop.CanBuy(_testShopDef.ShopId, item.ItemId, _gold);
                Color color = canBuy ? Color.white : new Color(0.5f, 0.5f, 0.5f);
                string line = $"  {FormatItemName(item.ItemId)}   {item.Price}g   Stock: {stockStr}";
                AddRowLabel(content, line, row, color);

                // Buy button
                int capturedRow = row;
                string capturedId = item.ItemId;
                AddButton(content, "Buy", row, () =>
                {
                    if (_shop.Buy(_testShopDef.ShopId, capturedId, ref _gold))
                    {
                        Debug.Log($"[Shop] Bought {capturedId}");
                        CloseActiveScreen();
                        ShowShop(); // Refresh
                    }
                });

                row++;
            }

            SetContentHeight(content, row);
        }

        // ================================================================
        // CRAFTING SCREEN
        // ================================================================

        private void ShowCrafting()
        {
            var panel = CreateScreenPanel("Crafting");
            var content = CreateScrollContent(panel.transform, new Vector2(0f, 0f), new Vector2(1f, 0.9f));

            // Discipline levels header
            AddRowLabel(content, $"  Herbalism Lv{_crafting.GetDisciplineLevel(CraftingDiscipline.Herbalism)}  |  Forging Lv{_crafting.GetDisciplineLevel(CraftingDiscipline.Forging)}  |  Seedcraft Lv{_crafting.GetDisciplineLevel(CraftingDiscipline.Seedcraft)}  |  Runebinding Lv{_crafting.GetDisciplineLevel(CraftingDiscipline.Runebinding)}", 0, new Color(0.7f, 0.85f, 1f));
            AddRowLabel(content, "", 1, Color.white);

            int row = 2;
            foreach (var recipe in _testRecipes)
            {
                bool canCraft = _crafting.CanCraft(recipe);
                Color color = canCraft ? new Color(0.5f, 1f, 0.5f) : new Color(0.6f, 0.4f, 0.4f);

                string ingredients = "";
                foreach (var ing in recipe.Ingredients)
                    ingredients += $"{FormatItemName(ing.ItemId)} x{ing.Quantity}  ";

                AddRowLabel(content, $"  {recipe.RecipeName}  [{recipe.Discipline}]  Lv{recipe.RequiredSkillLevel}", row, color);
                row++;
                AddRowLabel(content, $"    Needs: {ingredients}", row, new Color(0.7f, 0.7f, 0.7f));

                // Craft button
                var capturedRecipe = recipe;
                AddButton(content, "Craft", row, () =>
                {
                    if (_crafting.Craft(capturedRecipe))
                    {
                        Debug.Log($"[Crafting] Crafted {capturedRecipe.RecipeName}");
                        CloseActiveScreen();
                        ShowCrafting(); // Refresh
                    }
                });

                row++;
            }

            SetContentHeight(content, row);
        }

        // ================================================================
        // GACHA SCREEN
        // ================================================================

        private void ShowGacha()
        {
            var panel = CreateScreenPanel("Summoning — " + _testBanner.BannerName);

            // Banner info
            var infoArea = new GameObject("Info");
            infoArea.transform.SetParent(panel.transform, false);
            var infoRt = infoArea.AddComponent<RectTransform>();
            infoRt.anchorMin = new Vector2(0f, 0.7f);
            infoRt.anchorMax = new Vector2(1f, 0.9f);
            infoRt.offsetMin = new Vector2(20f, 0f);
            infoRt.offsetMax = new Vector2(-20f, 0f);

            var pity = _gacha.GetPityTracker(_testBanner.BannerId);
            string infoStr = $"{_testBanner.Description}\n" +
                             $"Rates:  Common {_testBanner.CommonRate * 100:F0}%  Rare {_testBanner.RareRate * 100:F0}%  Legendary {_testBanner.LegendaryRate * 100:F0}%  Mythic {_testBanner.MythicRate * 100:F0}%\n" +
                             $"Pity: {pity.PullsSinceLegendary}/90 (Legendary)   {pity.PullsSinceMythic}/180 (Mythic)   Total Pulls: {pity.TotalPulls}   Essence: {_accordEssence}";
            var infoText = AddTextChild(infoArea.transform, infoStr, 16, TextAnchor.UpperLeft, Color.white);

            // Buttons area
            AddButtonAt(panel.transform, "Pull x1", new Vector2(0.25f, 0.55f), new Vector2(200f, 50f), () =>
            {
                if (_isGachaAnimating) return;

                var result = _gacha.Pull(_testBanner);
                var wheelCompanions = BuildWheelCompanions();
                int winIdx = FindCompanionIndex(wheelCompanions, result.CompanionId);

                _isGachaAnimating = true;
                _roulette = GachaRouletteUI.Create(_canvas, _font);
                _roulette.Spin(wheelCompanions, winIdx, () =>
                {
                    string star = RarityStars(result.Rarity);
                    string dup = result.IsDuplicate ? " (DUP)" : " NEW!";
                    _gachaLog.Insert(0, $"{star} {FormatItemName(result.CompanionId)}{dup}");
                    if (_gachaLog.Count > 20) _gachaLog.RemoveAt(20);

                    _roulette.Dismiss();
                    _roulette = null;
                    _isGachaAnimating = false;
                    CloseActiveScreen();
                    ShowGacha();
                });
            });

            AddButtonAt(panel.transform, "Pull x10", new Vector2(0.75f, 0.55f), new Vector2(200f, 50f), () =>
            {
                if (_isGachaAnimating) return;

                var results = _gacha.Pull10(_testBanner);

                // Feature the last drop in the roulette animation
                var featured = results[results.Count - 1];

                var wheelCompanions = BuildWheelCompanions();
                int winIdx = FindCompanionIndex(wheelCompanions, featured.CompanionId);

                _isGachaAnimating = true;
                _roulette = GachaRouletteUI.Create(_canvas, _font);
                _roulette.Spin(wheelCompanions, winIdx, () =>
                {
                    foreach (var r in results)
                    {
                        string star = RarityStars(r.Rarity);
                        string dup = r.IsDuplicate ? " (DUP)" : " NEW!";
                        _gachaLog.Insert(0, $"{star} {FormatItemName(r.CompanionId)}{dup}");
                    }
                    if (_gachaLog.Count > 20) _gachaLog.RemoveRange(20, _gachaLog.Count - 20);

                    _roulette.Dismiss();
                    _roulette = null;
                    _isGachaAnimating = false;
                    CloseActiveScreen();
                    ShowGacha();
                });
            });

            // Pull log
            var logContent = CreateScrollContent(panel.transform, new Vector2(0f, 0f), new Vector2(1f, 0.5f));
            for (int i = 0; i < _gachaLog.Count; i++)
            {
                Color c = Color.white;
                if (_gachaLog[i].Contains("***")) c = new Color(1f, 0.5f, 1f); // Mythic
                else if (_gachaLog[i].Contains("**")) c = new Color(1f, 0.85f, 0.3f); // Legendary
                else if (_gachaLog[i].Contains("*")) c = new Color(0.5f, 0.7f, 1f); // Rare
                AddRowLabel(logContent, "  " + _gachaLog[i], i, c);
            }
            SetContentHeight(logContent, _gachaLog.Count);
        }

        // ================================================================
        // COMPANIONS SCREEN
        // ================================================================

        private void ShowCompanions()
        {
            var panel = CreateScreenPanel("Companion Roster");

            string[] companionIds = {
                "companion_villager", "companion_farmer", "companion_scout", "companion_apprentice",
                "companion_knight", "companion_pyromancer", "companion_ranger", "companion_priest",
                "companion_lyra", "companion_thorne", "companion_selene", "companion_eldara"
            };
            Rarity[] rarities = {
                Rarity.Common, Rarity.Common, Rarity.Common, Rarity.Common,
                Rarity.Rare, Rarity.Rare, Rarity.Rare, Rarity.Rare,
                Rarity.Legendary, Rarity.Legendary, Rarity.Legendary, Rarity.Mythic
            };
            string[] elements = {
                "None", "Verdant", "Volt", "Hydro",
                "Geo", "Pyro", "Verdant", "Hydro",
                "Verdant", "Umbral", "Cryo", "Pyro"
            };
            string[] classes = {
                "Swordsman", "Guardian", "Archer", "Mage",
                "Guardian", "Mage", "Archer", "Healer",
                "Archer", "Necromancer", "Assassin", "Summoner"
            };

            // Helper to look up display info for a companion id
            string CompanionLine(string id)
            {
                int idx = System.Array.IndexOf(companionIds, id);
                if (idx < 0) return id;
                return $"{RarityStars(rarities[idx])} {FormatItemName(id)}   {elements[idx]}  {classes[idx]}";
            }

            // --- A) Party slots header (top 20% of panel) ---
            var headerContent = CreateScrollContent(panel.transform, new Vector2(0f, 0.78f), new Vector2(1f, 0.9f));

            // Active slot
            string activeLine = _partyActiveId != null
                ? $"  Active:  {CompanionLine(_partyActiveId)}"
                : "  Active:  (empty)";
            Color activeColor = _partyActiveId != null ? Color.white : new Color(0.4f, 0.4f, 0.4f);
            AddRowLabel(headerContent, activeLine, 0, activeColor);
            if (_partyActiveId != null)
                AddButton(headerContent, "Remove", 0, () => {
                    _partyActiveId = null;
                    if (_sceneSetup != null) _sceneSetup.SetPartyCompanion("Active", null);
                    CloseActiveScreen(); ShowCompanions();
                });

            // Support slot
            string supportLine = _partySupportId != null
                ? $"  Support: {CompanionLine(_partySupportId)}"
                : "  Support: (empty)";
            Color supportColor = _partySupportId != null ? Color.white : new Color(0.4f, 0.4f, 0.4f);
            AddRowLabel(headerContent, supportLine, 1, supportColor);
            if (_partySupportId != null)
                AddButton(headerContent, "Remove", 1, () => {
                    _partySupportId = null;
                    if (_sceneSetup != null) _sceneSetup.SetPartyCompanion("Support", null);
                    CloseActiveScreen(); ShowCompanions();
                });

            // Clear Party button
            AddButton(headerContent, "Clear Party", 2, () => {
                _partyActiveId = null; _partySupportId = null;
                if (_sceneSetup != null)
                {
                    _sceneSetup.SetPartyCompanion("Active", null);
                    _sceneSetup.SetPartyCompanion("Support", null);
                }
                CloseActiveScreen(); ShowCompanions();
            });

            SetContentHeight(headerContent, 3);

            // --- B) Companion list (scrollable, below header) ---
            var content = CreateScrollContent(panel.transform, new Vector2(0f, 0f), new Vector2(1f, 0.77f));

            int row = 0;
            for (int i = 0; i < companionIds.Length; i++)
            {
                string cId = companionIds[i];
                bool owned = IsCompanionOwned(cId);
                string star = RarityStars(rarities[i]);
                Color color = owned ? RarityToColor(rarities[i]) : new Color(0.3f, 0.3f, 0.3f);

                if (!owned)
                {
                    string line = $"  {star} {FormatItemName(cId)}   {elements[i]}  {classes[i]}  [LOCKED]";
                    AddRowLabel(content, line, row, color);
                }
                else if (cId == _partyActiveId)
                {
                    string line = $"  {star} {FormatItemName(cId)}   {elements[i]}  {classes[i]}  [ACTIVE]";
                    AddRowLabel(content, line, row, color);
                }
                else if (cId == _partySupportId)
                {
                    string line = $"  {star} {FormatItemName(cId)}   {elements[i]}  {classes[i]}  [SUPPORT]";
                    AddRowLabel(content, line, row, color);
                }
                else
                {
                    string line = $"  {star} {FormatItemName(cId)}   {elements[i]}  {classes[i]}";
                    AddRowLabel(content, line, row, color);
                    string capturedId = cId;
                    AddDualButtons(content, row,
                        "Active", () =>
                        {
                            if (_partySupportId == capturedId) _partySupportId = null;
                            _partyActiveId = capturedId;
                            if (_sceneSetup != null)
                            {
                                _sceneSetup.SetPartyCompanion("Support", _partySupportId);
                                _sceneSetup.SetPartyCompanion("Active", _partyActiveId);
                            }
                            CloseActiveScreen(); ShowCompanions();
                        },
                        "Support", () =>
                        {
                            if (_partyActiveId == capturedId) _partyActiveId = null;
                            _partySupportId = capturedId;
                            if (_sceneSetup != null)
                            {
                                _sceneSetup.SetPartyCompanion("Active", _partyActiveId);
                                _sceneSetup.SetPartyCompanion("Support", _partySupportId);
                            }
                            CloseActiveScreen(); ShowCompanions();
                        });
                }
                row++;
            }

            SetContentHeight(content, row);
        }

        // ================================================================
        // MAP SCREEN
        // ================================================================

        private void ShowMap()
        {
            var panel = CreateScreenPanel("World Map");
            var content = CreateScrollContent(panel.transform, new Vector2(0f, 0f), new Vector2(1f, 0.9f));

            var regions = new[]
            {
                ("Greenreach", "Starting zone — lush forests", "Verdant", 1, 0.05f),
                ("Ashen Steppe", "Scorched plains with volcanic vents", "Pyro", 15, 0.25f),
                ("Frosthollow Peaks", "Frozen mountain range", "Cryo", 25, 0.15f),
                ("Gloomtide Marshes", "Murky swamps filled with umbral energy", "Umbral", 20, 0.40f),
                ("Withered Heart", "The epicenter of the Blight", "None", 40, 0.80f)
            };

            // Main storyline NPCs per region
            var regionNpcs = new Dictionary<string, (string name, string role)[]>
            {
                ["Greenreach"] = new[]
                {
                    ("Elder Mirren", "Village Elder — quest giver"),
                    ("Healer Maren", "Village Healer — herbalist"),
                    ("Captain Aldric", "Militia Captain — blight investigation"),
                    ("Warden Sable", "Forest Warden — patrols the border")
                },
                ["Ashen Steppe"] = new[]
                {
                    ("Forgemaster Bram", "Guild Forgemaster — crafting master"),
                    ("Watcher Sera", "Outpost Scout — tracks blight spread"),
                    ("Lyra", "Forest Warden — memory fragments")
                },
                ["Frosthollow Peaks"] = new[]
                {
                    ("Scholar Veylin", "Blight Researcher — ancient texts"),
                    ("Sentinel Kaelos", "Mountain Guardian — peak passage"),
                    ("Thorne", "Wandering Sellsword — hired blade")
                },
                ["Gloomtide Marshes"] = new[]
                {
                    ("Oracle Nyx", "Marsh Seer — blight origin visions"),
                    ("Ranger Theron", "Marsh Guide — safe passage"),
                    ("Selene", "Moonlight Priestess — purification rites")
                },
                ["Withered Heart"] = new[]
                {
                    ("The Blightcaller", "Source of the corruption"),
                    ("Eldara", "Spirit of the Verdant — ancient guardian"),
                    ("Echo of Mirren", "Spectral guide — final trial")
                }
            };

            Color npcColor = new Color(0.85f, 0.75f, 1f);
            Color companionNpcColor = new Color(1f, 0.85f, 0.3f);

            int row = 0;
            foreach (var (name, desc, element, level, blight) in regions)
            {
                Color nameColor = blight > 0.5f ? new Color(0.8f, 0.3f, 0.3f) : new Color(0.5f, 1f, 0.5f);
                AddRowLabel(content, $"  {name}   (Lv {level}+)   [{element}]", row, nameColor);
                row++;
                AddRowLabel(content, $"    {desc}", row, new Color(0.7f, 0.7f, 0.7f));
                row++;

                // Blight bar
                int barWidth = 20;
                int filled = Mathf.RoundToInt(blight * barWidth);
                string bar = "[" + new string('#', filled) + new string('-', barWidth - filled) + "]";
                Color blightColor = Color.Lerp(new Color(0.3f, 0.8f, 0.3f), new Color(0.9f, 0.2f, 0.2f), blight);
                AddRowLabel(content, $"    Blight: {bar} {blight * 100:F0}%", row, blightColor);
                row++;

                // NPCs in this region
                if (regionNpcs.TryGetValue(name, out var npcs))
                {
                    foreach (var (npcName, npcRole) in npcs)
                    {
                        bool isCompanion = npcName == "Lyra" || npcName == "Thorne"
                            || npcName == "Selene" || npcName == "Eldara";
                        Color color = isCompanion ? companionNpcColor : npcColor;
                        string tag = isCompanion ? " [Companion]" : "";
                        AddRowLabel(content, $"      * {npcName} — {npcRole}{tag}", row, color);
                        row++;
                    }
                }

                AddRowLabel(content, "", row, Color.white);
                row++;
            }

            SetContentHeight(content, row);
        }

        // ================================================================
        // QUEST LOG SCREEN
        // ================================================================

        private void ShowQuestLog()
        {
            var panel = CreateScreenPanel("Quest Log");
            var content = CreateScrollContent(panel.transform, new Vector2(0f, 0f), new Vector2(1f, 0.9f));

            // Active quests
            var activeQuests = _questManager.GetActiveQuests();
            int row = 0;

            AddRowLabel(content, "  --- Active Quests ---", row, new Color(0.95f, 0.85f, 0.5f));
            row++;

            if (activeQuests.Count == 0)
            {
                AddRowLabel(content, "    (none)", row, Color.gray);
                row++;
            }

            foreach (var kvp in activeQuests)
            {
                var state = kvp.Value;
                var def = state.Definition;
                Color typeColor = def.Type switch
                {
                    QuestType.MainStory => new Color(1f, 0.85f, 0.3f),
                    QuestType.SideQuest => new Color(0.6f, 0.85f, 1f),
                    QuestType.GuildContract => new Color(0.5f, 1f, 0.5f),
                    QuestType.CompanionQuest => new Color(1f, 0.6f, 0.8f),
                    _ => Color.white
                };

                AddRowLabel(content, $"  [{def.Type}] {def.QuestName}", row, typeColor);
                row++;
                AddRowLabel(content, $"    {def.Description}", row, new Color(0.7f, 0.7f, 0.7f));
                row++;

                for (int i = 0; i < def.Objectives.Count; i++)
                {
                    var obj = def.Objectives[i];
                    int progress = state.ObjectiveProgress[i];
                    bool done = progress >= obj.RequiredCount;
                    string check = done ? "[X]" : "[ ]";
                    Color objColor = done ? new Color(0.5f, 1f, 0.5f) : Color.white;
                    AddRowLabel(content, $"    {check} {obj.ObjectiveDescription}  ({progress}/{obj.RequiredCount})", row, objColor);
                    row++;
                }

                AddRowLabel(content, "", row, Color.white);
                row++;
            }

            // Completed quests (uncollected)
            var uncollected = _questManager.GetCompletedUncollectedQuests();
            if (uncollected.Count > 0)
            {
                AddRowLabel(content, "  --- Completed Quests ---", row, new Color(0.4f, 0.9f, 0.4f));
                row++;

                foreach (var kvp in uncollected)
                {
                    var def = kvp.Value.Definition;
                    AddRowLabel(content, $"  [Complete] {def.QuestName}", row, new Color(0.4f, 0.9f, 0.4f));

                    var capturedId = def.QuestId;
                    var capturedType = def.Type;
                    AddButton(content, "Collect", row, () =>
                    {
                        if (capturedType == QuestType.GuildContract)
                        {
                            ShowPopupMessage("Visit the Guild to collect your rewards.");
                        }
                        else
                        {
                            _questManager.CollectQuestRewards(capturedId);
                            CloseActiveScreen();
                            ShowQuestLog();
                        }
                    });
                    row++;

                    // Show rewards preview
                    foreach (var reward in def.Rewards)
                    {
                        string rewardText = "";
                        if (!string.IsNullOrEmpty(reward.ItemId))
                            rewardText += $"{FormatItemName(reward.ItemId)} x{reward.Quantity}  ";
                        if (reward.Gold > 0) rewardText += $"{reward.Gold} Gold  ";
                        if (reward.XP > 0) rewardText += $"{reward.XP} XP";
                        if (!string.IsNullOrEmpty(rewardText))
                            AddRowLabel(content, $"      Reward: {rewardText.Trim()}", row, new Color(0.7f, 0.7f, 0.5f));
                        row++;
                    }
                }
                AddRowLabel(content, "", row, Color.white); row++;
            }

            // Available quests
            AddRowLabel(content, "  --- Available Quests ---", row, new Color(0.95f, 0.85f, 0.5f));
            row++;

            foreach (var quest in _testQuests)
            {
                if (activeQuests.ContainsKey(quest.QuestId)
                    || _questManager.GetCompletedQuestIds().Contains(quest.QuestId)
                    || uncollected.ContainsKey(quest.QuestId))
                    continue;

                AddRowLabel(content, $"  [{quest.Type}] {quest.QuestName}", row, new Color(0.5f, 0.5f, 0.5f));
                row++;

                var capturedQuest = quest;
                AddButton(content, "Accept", row - 1, () =>
                {
                    _questManager.AcceptQuest(capturedQuest);
                    CloseActiveScreen();
                    ShowQuestLog();
                });
            }

            SetContentHeight(content, row);
        }

        private void ShowPopupMessage(string message)
        {
            // Full-screen overlay that blocks clicks
            var overlayGo = new GameObject("PopupOverlay");
            overlayGo.transform.SetParent(_canvas.transform, false);
            var overlayRt = overlayGo.AddComponent<RectTransform>();
            overlayRt.anchorMin = Vector2.zero;
            overlayRt.anchorMax = Vector2.one;
            overlayRt.offsetMin = Vector2.zero;
            overlayRt.offsetMax = Vector2.zero;
            var overlayImg = overlayGo.AddComponent<Image>();
            overlayImg.color = new Color(0f, 0f, 0f, 0.5f);

            // Center panel
            var panelGo = new GameObject("PopupPanel");
            panelGo.transform.SetParent(overlayGo.transform, false);
            var panelRt = panelGo.AddComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(380f, 140f);
            var panelImg = panelGo.AddComponent<Image>();
            panelImg.color = new Color(0.12f, 0.1f, 0.18f, 0.95f);

            // Message text
            var textGo = new GameObject("MessageText");
            textGo.transform.SetParent(panelGo.transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0.05f, 0.35f);
            textRt.anchorMax = new Vector2(0.95f, 0.9f);
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var msgText = textGo.AddComponent<Text>();
            msgText.font = _font;
            msgText.fontSize = 16;
            msgText.color = Color.white;
            msgText.alignment = TextAnchor.MiddleCenter;
            msgText.text = message;

            // OK button
            var btnGo = new GameObject("OKButton");
            btnGo.transform.SetParent(panelGo.transform, false);
            var btnRt = btnGo.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.5f, 0.05f);
            btnRt.anchorMax = new Vector2(0.5f, 0.05f);
            btnRt.pivot = new Vector2(0.5f, 0f);
            btnRt.sizeDelta = new Vector2(80f, 30f);
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.25f, 0.22f, 0.35f);
            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.onClick.AddListener(() => Destroy(overlayGo));

            var btnTextGo = new GameObject("Text");
            btnTextGo.transform.SetParent(btnGo.transform, false);
            var btnTextRt = btnTextGo.AddComponent<RectTransform>();
            btnTextRt.anchorMin = Vector2.zero;
            btnTextRt.anchorMax = Vector2.one;
            btnTextRt.offsetMin = Vector2.zero;
            btnTextRt.offsetMax = Vector2.zero;
            var btnText = btnTextGo.AddComponent<Text>();
            btnText.font = _font;
            btnText.fontSize = 14;
            btnText.color = Color.white;
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.text = "OK";
        }

        // ================================================================
        // SKILLS SCREEN
        // ================================================================

        private void ShowSkills()
        {
            var panel = CreateScreenPanel("Skills");
            var content = CreateScrollContent(panel.transform, new Vector2(0f, 0f), new Vector2(1f, 0.9f));

            var skills = new[]
            {
                ("Verdant Surge", "Calls upon nature to heal and deal AOE damage", Element.Verdant, 80f, 4.0f, 25f, 3.5f),
                ("Flame Strike", "Engulfs weapon in flame for a powerful strike", Element.Pyro, 120f, 6.0f, 30f, 0f),
                ("Frost Nova", "Freezes enemies in a wide radius", Element.Cryo, 60f, 8.0f, 40f, 5.0f),
                ("Thunder Bolt", "Lightning bolt targeting a single enemy", Element.Volt, 150f, 5.0f, 35f, 0.5f),
                ("Shadow Step", "Phase through shadows to reposition", Element.Umbral, 0f, 3.0f, 15f, 0f),
                ("Stone Wall", "Raise a wall of earth for defense", Element.Geo, 0f, 10.0f, 20f, 4.0f),
                ("Tidal Wave", "Sweep enemies with a wave of water", Element.Hydro, 90f, 7.0f, 30f, 6.0f)
            };

            int row = 0;
            foreach (var (name, desc, element, dmg, cd, cost, aoe) in skills)
            {
                Color elemColor = ElementToColor(element);
                AddRowLabel(content, $"  {name}   [{element}]", row, elemColor);
                row++;
                AddRowLabel(content, $"    {desc}", row, new Color(0.7f, 0.7f, 0.7f));
                row++;

                string stats = $"    DMG: {dmg:F0}   CD: {cd:F1}s   Cost: {cost:F0} VP";
                if (aoe > 0f) stats += $"   AOE: {aoe:F1}m";
                AddRowLabel(content, stats, row, new Color(0.8f, 0.8f, 0.8f));
                row++;
                AddRowLabel(content, "", row, Color.white);
                row++;
            }

            SetContentHeight(content, row);
        }

        // ================================================================
        // CHEAT MENU
        // ================================================================

        private void ShowCheatMenu()
        {
            var panel = CreateScreenPanel("Cheat Menu");
            var content = CreateScrollContent(panel.transform, new Vector2(0f, 0f), new Vector2(1f, 0.9f));

            int row = 0;
            Color headerColor = new Color(1f, 0.4f, 0.4f);
            Color cheatColor = new Color(1f, 0.8f, 0.6f);
            Color activeColor = new Color(0.4f, 1f, 0.4f);

            // ---- TOGGLES ----
            AddRowLabel(content, "  --- Toggles ---", row, headerColor); row++;

            bool godMode = _sceneSetup != null && _sceneSetup.GodMode;
            AddRowLabel(content, $"  God Mode: {(godMode ? "ON" : "OFF")}", row, godMode ? activeColor : cheatColor);
            AddButton(content, "Toggle", row, () =>
            {
                if (_sceneSetup != null) _sceneSetup.GodMode = !_sceneSetup.GodMode;
                CloseActiveScreen(); ShowCheatMenu();
            }); row++;

            bool ohk = _sceneSetup != null && _sceneSetup.OneHitKill;
            AddRowLabel(content, $"  One-Hit Kill: {(ohk ? "ON" : "OFF")}", row, ohk ? activeColor : cheatColor);
            AddButton(content, "Toggle", row, () =>
            {
                if (_sceneSetup != null) _sceneSetup.OneHitKill = !_sceneSetup.OneHitKill;
                CloseActiveScreen(); ShowCheatMenu();
            }); row++;

            AddRowLabel(content, "", row, Color.white); row++;

            // ---- HEALTH / VP ----
            AddRowLabel(content, "  --- Health & Verdance ---", row, headerColor); row++;

            if (_sceneSetup != null)
            {
                AddRowLabel(content, $"  HP: {_sceneSetup.PlayerHealth:F0} / {_sceneSetup.PlayerMaxHealth:F0}", row, cheatColor);
                AddButton(content, "Full HP", row, () =>
                {
                    _sceneSetup.PlayerHealth = _sceneSetup.PlayerMaxHealth;
                    CloseActiveScreen(); ShowCheatMenu();
                }); row++;

                AddRowLabel(content, $"  Max HP: {_sceneSetup.PlayerMaxHealth:F0}", row, cheatColor);
                AddCheatValueButtons(content, row, new[] { 500f, 1000f, 5000f, 99999f }, v =>
                {
                    _sceneSetup.PlayerMaxHealth = v;
                    _sceneSetup.PlayerHealth = v;
                    CloseActiveScreen(); ShowCheatMenu();
                }); row++;

                AddRowLabel(content, $"  VP: {_sceneSetup.PlayerVerdance:F0} / {_sceneSetup.PlayerMaxVerdance:F0}", row, cheatColor);
                AddButton(content, "Full VP", row, () =>
                {
                    _sceneSetup.PlayerVerdance = _sceneSetup.PlayerMaxVerdance;
                    CloseActiveScreen(); ShowCheatMenu();
                }); row++;

                AddRowLabel(content, $"  Max VP: {_sceneSetup.PlayerMaxVerdance:F0}", row, cheatColor);
                AddCheatValueButtons(content, row, new[] { 100f, 500f, 1000f, 99999f }, v =>
                {
                    _sceneSetup.PlayerMaxVerdance = v;
                    _sceneSetup.PlayerVerdance = v;
                    CloseActiveScreen(); ShowCheatMenu();
                }); row++;
            }

            AddRowLabel(content, "", row, Color.white); row++;

            // ---- GOLD / ESSENCE ----
            AddRowLabel(content, "  --- Currency ---", row, headerColor); row++;

            AddRowLabel(content, $"  Gold: {_gold}", row, cheatColor);
            AddCheatValueButtons(content, row, new[] { 1000f, 10000f, 100000f, 999999f }, v =>
            {
                _gold = (int)v;
                CloseActiveScreen(); ShowCheatMenu();
            }); row++;

            AddRowLabel(content, $"  Accord Essence: {_accordEssence}", row, cheatColor);
            AddCheatValueButtons(content, row, new[] { 100f, 500f, 1000f, 9999f }, v =>
            {
                _accordEssence = (int)v;
                CloseActiveScreen(); ShowCheatMenu();
            }); row++;

            AddRowLabel(content, "", row, Color.white); row++;

            // ---- WEAPON STATS ----
            AddRowLabel(content, "  --- Weapon Stats ---", row, headerColor); row++;

            if (_sceneSetup != null && _sceneSetup.TestWeapon != null)
            {
                var w = _sceneSetup.TestWeapon;
                AddRowLabel(content, $"  Base Damage: {w.BaseDamage:F0}", row, cheatColor);
                AddCheatValueButtons(content, row, new[] { 40f, 100f, 500f, 9999f }, v =>
                {
                    w.BaseDamage = v;
                    CloseActiveScreen(); ShowCheatMenu();
                }); row++;

                AddRowLabel(content, $"  Attack Speed: {w.AttackSpeed:F1}", row, cheatColor);
                AddCheatValueButtons(content, row, new[] { 0.5f, 1.2f, 3.0f, 10f }, v =>
                {
                    w.AttackSpeed = v;
                    CloseActiveScreen(); ShowCheatMenu();
                }); row++;

                AddRowLabel(content, $"  Charged Multiplier: {w.ChargedMultiplier:F1}x", row, cheatColor);
                AddCheatValueButtons(content, row, new[] { 1.5f, 3f, 5f, 10f }, v =>
                {
                    w.ChargedMultiplier = v;
                    CloseActiveScreen(); ShowCheatMenu();
                }); row++;
            }

            AddRowLabel(content, "", row, Color.white); row++;

            // ---- PLAYER STATS ----
            AddRowLabel(content, "  --- Player Stats ---", row, headerColor); row++;

            if (_sceneSetup != null && _sceneSetup.PlayerStats != null)
            {
                var ps = _sceneSetup.PlayerStats;
                AddRowLabel(content, $"  Move Speed: {ps.MoveSpeed:F1}", row, cheatColor);
                AddCheatValueButtons(content, row, new[] { 6f, 12f, 20f, 50f }, v =>
                {
                    ps.MoveSpeed = v;
                    CloseActiveScreen(); ShowCheatMenu();
                }); row++;

                AddRowLabel(content, $"  Dodge Speed: {ps.DodgeSpeed:F1}", row, cheatColor);
                AddCheatValueButtons(content, row, new[] { 14f, 25f, 40f, 80f }, v =>
                {
                    ps.DodgeSpeed = v;
                    CloseActiveScreen(); ShowCheatMenu();
                }); row++;
            }

            AddRowLabel(content, "", row, Color.white); row++;

            // ---- MATERIALS (add all) ----
            AddRowLabel(content, "  --- Materials ---", row, headerColor); row++;

            AddRowLabel(content, "  Add all materials (x99)", row, cheatColor);
            AddButton(content, "Give All", row, () =>
            {
                string[] allItems = {
                    "health_potion", "verdance_shard", "iron_ore", "herb_bundle",
                    "fire_crystal", "silk_thread", "runic_dust", "ancient_seed",
                    "wolf_pelt", "moonstone", "healing_salve", "iron_blade",
                    "seed_bomb", "rune_warding"
                };
                foreach (var itemId in allItems)
                    _inventory.AddItem(itemId, 99);
                Debug.Log("[Cheat] Added 99x of all materials");
                CloseActiveScreen(); ShowCheatMenu();
            }); row++;

            AddRowLabel(content, "  Clear entire inventory", row, new Color(1f, 0.3f, 0.3f));
            AddButton(content, "Clear", row, () =>
            {
                var items = _inventory.GetAllItems();
                foreach (var kvp in items)
                    _inventory.RemoveItem(kvp.Key, kvp.Value);
                Debug.Log("[Cheat] Inventory cleared");
                CloseActiveScreen(); ShowCheatMenu();
            }); row++;

            AddRowLabel(content, "", row, Color.white); row++;

            // ---- EQUIPMENT ----
            AddRowLabel(content, "  --- Equipment ---", row, headerColor); row++;

            var equipSets = new[]
            {
                ("Starter Set", new[] {
                    (EquipmentSlot.Weapon, "iron_sword"),
                    (EquipmentSlot.Head, "leather_helm"),
                    (EquipmentSlot.Chest, "leather_chest"),
                    (EquipmentSlot.Legs, "leather_legs"),
                    (EquipmentSlot.Accessory, "copper_ring")
                }),
                ("Legendary Set", new[] {
                    (EquipmentSlot.Weapon, "verdant_blade"),
                    (EquipmentSlot.Head, "crown_of_thorns"),
                    (EquipmentSlot.Chest, "elderwood_plate"),
                    (EquipmentSlot.Legs, "greaves_of_the_wild"),
                    (EquipmentSlot.Accessory, "seed_of_eternity")
                })
            };

            foreach (var (setName, pieces) in equipSets)
            {
                AddRowLabel(content, $"  Equip: {setName}", row, cheatColor);
                var capturedPieces = pieces;
                AddButton(content, "Equip", row, () =>
                {
                    foreach (var (slot, itemId) in capturedPieces)
                        _equipment.Equip(itemId, slot);
                    Debug.Log($"[Cheat] Equipped {setName}");
                    CloseActiveScreen(); ShowCheatMenu();
                }); row++;
            }

            AddRowLabel(content, "  Unequip all", row, new Color(1f, 0.3f, 0.3f));
            AddButton(content, "Strip", row, () =>
            {
                _equipment.Unequip(EquipmentSlot.Weapon);
                _equipment.Unequip(EquipmentSlot.Head);
                _equipment.Unequip(EquipmentSlot.Chest);
                _equipment.Unequip(EquipmentSlot.Legs);
                _equipment.Unequip(EquipmentSlot.Accessory);
                Debug.Log("[Cheat] All equipment removed");
                CloseActiveScreen(); ShowCheatMenu();
            }); row++;

            AddRowLabel(content, "", row, Color.white); row++;

            // ---- COMPANIONS ----
            AddRowLabel(content, "  --- Companions ---", row, headerColor); row++;

            AddRowLabel(content, "  Unlock all companions", row, cheatColor);
            AddButton(content, "Unlock", row, () =>
            {
                string[] allCompanions = {
                    "companion_villager", "companion_farmer", "companion_scout", "companion_apprentice",
                    "companion_knight", "companion_pyromancer", "companion_ranger", "companion_priest",
                    "companion_lyra", "companion_thorne", "companion_selene", "companion_eldara"
                };
                foreach (var id in allCompanions)
                {
                    _gacha.RegisterOwnedCompanion(id);
                    _unlockedCompanions.Add(id);
                    if (!_gachaLog.Exists(e => e.Contains(FormatItemName(id))))
                        _gachaLog.Insert(0, $"{RarityStars(GetCompanionRarity(id))} {FormatItemName(id)} NEW!");
                }
                Debug.Log("[Cheat] All companions unlocked");
                CloseActiveScreen(); ShowCheatMenu();
            }); row++;

            AddRowLabel(content, "", row, Color.white); row++;

            // ---- CRAFTING ----
            AddRowLabel(content, "  --- Crafting Levels ---", row, headerColor); row++;

            foreach (CraftingDiscipline disc in System.Enum.GetValues(typeof(CraftingDiscipline)))
            {
                int lvl = _crafting.GetDisciplineLevel(disc);
                AddRowLabel(content, $"  {disc}: Lv {lvl}", row, cheatColor);
                var capturedDisc = disc;
                AddButton(content, "Max", row, () =>
                {
                    _crafting.AddDisciplineXP(capturedDisc, 100000);
                    Debug.Log($"[Cheat] {capturedDisc} maxed");
                    CloseActiveScreen(); ShowCheatMenu();
                }); row++;
            }

            AddRowLabel(content, "", row, Color.white); row++;

            // ---- QUESTS ----
            AddRowLabel(content, "  --- Quests ---", row, headerColor); row++;

            AddRowLabel(content, "  Accept all quests", row, cheatColor);
            AddButton(content, "Accept", row, () =>
            {
                foreach (var q in _testQuests)
                    _questManager.AcceptQuest(q);
                Debug.Log("[Cheat] All quests accepted");
                CloseActiveScreen(); ShowCheatMenu();
            }); row++;

            AddRowLabel(content, "  Complete all quest objectives", row, cheatColor);
            AddButton(content, "Complete", row, () =>
            {
                foreach (var q in _testQuests)
                {
                    var state = _questManager.GetQuestState(q.QuestId);
                    if (state == null) continue;
                    for (int i = 0; i < q.Objectives.Count; i++)
                        _questManager.UpdateObjective(q.QuestId, i, q.Objectives[i].RequiredCount);
                }
                Debug.Log("[Cheat] All quests completed");
                CloseActiveScreen(); ShowCheatMenu();
            }); row++;

            AddRowLabel(content, "", row, Color.white); row++;

            // ---- PITY RESET ----
            AddRowLabel(content, "  --- Gacha ---", row, headerColor); row++;

            AddRowLabel(content, "  Reset pity counters", row, cheatColor);
            AddButton(content, "Reset", row, () =>
            {
                var pity = _gacha.GetPityTracker(_testBanner.BannerId);
                pity.ResetLegendaryPity();
                pity.ResetMythicPity();
                pity.TotalPulls = 0;
                pity.LostLastFiftyFifty = false;
                Debug.Log("[Cheat] Pity reset");
                CloseActiveScreen(); ShowCheatMenu();
            }); row++;

            AddRowLabel(content, "  Set pity to 89 (next = guaranteed 5-star)", row, cheatColor);
            AddButton(content, "Set 89", row, () =>
            {
                var pity = _gacha.GetPityTracker(_testBanner.BannerId);
                pity.PullsSinceLegendary = 89;
                Debug.Log("[Cheat] Pity set to 89");
                CloseActiveScreen(); ShowCheatMenu();
            }); row++;

            SetContentHeight(content, row);
        }

        private void AddCheatValueButtons(Transform content, int row, float[] values, System.Action<float> onSet)
        {
            for (int i = 0; i < values.Length; i++)
            {
                float val = values[i];
                string label = val >= 1000f ? $"{val / 1000f:G}k" : val.ToString("G4");

                var go = new GameObject("CheatBtn_" + label);
                go.transform.SetParent(content, false);
                var rt = go.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(1f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-15f - (values.Length - 1 - i) * 65f, -row * 28f);
                rt.sizeDelta = new Vector2(60f, 26f);

                var img = go.AddComponent<Image>();
                img.color = new Color(0.4f, 0.25f, 0.2f);

                var btn = go.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => onSet?.Invoke(val));

                var textGo = new GameObject("Text");
                textGo.transform.SetParent(go.transform, false);
                var t = textGo.AddComponent<Text>();
                t.text = label;
                t.font = _font;
                t.fontSize = 12;
                t.color = Color.white;
                t.alignment = TextAnchor.MiddleCenter;
                var tRt = textGo.GetComponent<RectTransform>();
                tRt.anchorMin = Vector2.zero;
                tRt.anchorMax = Vector2.one;
                tRt.offsetMin = Vector2.zero;
                tRt.offsetMax = Vector2.zero;
            }
        }

        private List<(string id, string name, Rarity rarity)> BuildWheelCompanions()
        {
            var list = new List<(string, string, Rarity)>();
            void AddPool(List<string> pool, Rarity rarity)
            {
                foreach (var id in pool)
                    list.Add((id, FormatItemName(id), rarity));
            }
            AddPool(_testBanner.CommonPool, Rarity.Common);
            AddPool(_testBanner.RarePool, Rarity.Rare);
            AddPool(_testBanner.LegendaryPool, Rarity.Legendary);
            AddPool(_testBanner.MythicPool, Rarity.Mythic);
            return list;
        }

        private static int FindCompanionIndex(List<(string id, string name, Rarity rarity)> companions, string companionId)
        {
            for (int i = 0; i < companions.Count; i++)
            {
                if (companions[i].id == companionId)
                    return i;
            }
            return 0;
        }

        private static Rarity GetCompanionRarity(string id)
        {
            if (id.Contains("eldara")) return Rarity.Mythic;
            if (id.Contains("lyra") || id.Contains("thorne") || id.Contains("selene")) return Rarity.Legendary;
            if (id.Contains("knight") || id.Contains("pyromancer") || id.Contains("ranger") || id.Contains("priest")) return Rarity.Rare;
            return Rarity.Common;
        }

        // ================================================================
        // UI building helpers
        // ================================================================

        private Transform CreateScrollContent(Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            // Scroll view container
            var scrollGo = new GameObject("ScrollView");
            scrollGo.transform.SetParent(parent, false);
            var scrollRt = scrollGo.AddComponent<RectTransform>();
            scrollRt.anchorMin = anchorMin;
            scrollRt.anchorMax = anchorMax;
            scrollRt.offsetMin = new Vector2(10f, 10f);
            scrollRt.offsetMax = new Vector2(-10f, -10f);

            var scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.3f);
            scrollGo.AddComponent<Mask>().showMaskGraphic = true;

            // Viewport
            var viewport = scrollGo.GetComponent<RectTransform>();

            // Content
            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(scrollGo.transform, false);
            var contentRt = contentGo.AddComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0f, 500f);

            scrollRect.content = contentRt;
            scrollRect.viewport = viewport;
            scrollRect.vertical = true;
            scrollRect.horizontal = false;
            scrollRect.scrollSensitivity = 30f;

            return contentGo.transform;
        }

        private void SetContentHeight(Transform content, int rowCount)
        {
            var rt = content.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, Mathf.Max(rowCount * 28f + 20f, 100f));
        }

        private Text AddRowLabel(Transform content, string text, int row, Color color)
        {
            var go = new GameObject("Row_" + row);
            go.transform.SetParent(content, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(0f, -row * 28f);
            rt.sizeDelta = new Vector2(0f, 28f);

            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = _font;
            t.fontSize = 16;
            t.color = color;
            t.alignment = TextAnchor.MiddleLeft;
            return t;
        }

        private void AddButton(Transform content, string label, int row, System.Action onClick)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(content, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-15f, -row * 28f);
            rt.sizeDelta = new Vector2(80f, 26f);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.35f, 0.5f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick?.Invoke());

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var t = textGo.AddComponent<Text>();
            t.text = label;
            t.font = _font;
            t.fontSize = 14;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            var tRt = textGo.GetComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero;
            tRt.anchorMax = Vector2.one;
            tRt.offsetMin = Vector2.zero;
            tRt.offsetMax = Vector2.zero;
        }

        private void AddDualButtons(Transform content, int row,
            string labelA, System.Action onClickA,
            string labelB, System.Action onClickB)
        {
            float btnWidth = 70f;
            float gap = 4f;

            // Right button (Support)
            CreateSmallButton(content, labelB, new Vector2(-15f, -row * 28f), btnWidth, onClickB);
            // Left button (Active)
            CreateSmallButton(content, labelA, new Vector2(-15f - btnWidth - gap, -row * 28f), btnWidth, onClickA);
        }

        private void CreateSmallButton(Transform content, string label, Vector2 pos, float width, System.Action onClick)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(content, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(width, 26f);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.35f, 0.5f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick?.Invoke());

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var t = textGo.AddComponent<Text>();
            t.text = label;
            t.font = _font;
            t.fontSize = 13;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            var tRt = textGo.GetComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero;
            tRt.anchorMax = Vector2.one;
            tRt.offsetMin = Vector2.zero;
            tRt.offsetMax = Vector2.zero;
        }

        private void AddButtonAt(Transform parent, string label, Vector2 anchor, Vector2 size, System.Action onClick)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.25f, 0.4f, 0.6f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick?.Invoke());

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var t = textGo.AddComponent<Text>();
            t.text = label;
            t.font = _font;
            t.fontSize = 20;
            t.fontStyle = FontStyle.Bold;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            var tRt = textGo.GetComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero;
            tRt.anchorMax = Vector2.one;
            tRt.offsetMin = Vector2.zero;
            tRt.offsetMax = Vector2.zero;
        }

        private Text AddLabel(Transform parent, string text, int fontSize, TextAnchor anchor,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;

            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = _font;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = anchor;
            return t;
        }

        private Text AddTextChild(Transform parent, string text, int fontSize, TextAnchor anchor, Color color)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = _font;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = anchor;
            return t;
        }

        // ================================================================
        // Formatting helpers
        // ================================================================

        private static string FormatItemName(string id)
        {
            if (string.IsNullOrEmpty(id)) return "(none)";
            // "health_potion" -> "Health Potion", "companion_lyra" -> "Companion Lyra"
            var parts = id.Split('_');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 0)
                    parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1);
            }
            return string.Join(" ", parts);
        }

        private static string RarityStars(Rarity r) => r switch
        {
            Rarity.Common => "*",
            Rarity.Rare => "**",
            Rarity.Legendary => "***",
            Rarity.Mythic => "****",
            _ => "*"
        };

        private static Color RarityToColor(Rarity r) => r switch
        {
            Rarity.Common => new Color(0.7f, 0.7f, 0.7f),
            Rarity.Rare => new Color(0.5f, 0.7f, 1f),
            Rarity.Legendary => new Color(1f, 0.85f, 0.3f),
            Rarity.Mythic => new Color(1f, 0.5f, 1f),
            _ => Color.white
        };

        private Color GetRarityColor(string itemId)
        {
            // Simple heuristic based on known items
            if (itemId.Contains("moonstone") || itemId.Contains("ancient"))
                return RarityToColor(Rarity.Legendary);
            if (itemId.Contains("crystal") || itemId.Contains("runic"))
                return RarityToColor(Rarity.Rare);
            return Color.white;
        }

        private static Color ElementToColor(Element e) => e switch
        {
            Element.Verdant => new Color(0.3f, 0.9f, 0.4f),
            Element.Pyro => new Color(1f, 0.4f, 0.2f),
            Element.Hydro => new Color(0.3f, 0.6f, 1f),
            Element.Volt => new Color(1f, 1f, 0.3f),
            Element.Umbral => new Color(0.6f, 0.3f, 0.8f),
            Element.Cryo => new Color(0.6f, 0.9f, 1f),
            Element.Geo => new Color(0.8f, 0.65f, 0.3f),
            _ => Color.white
        };

        private bool IsCompanionOwned(string companionId)
        {
            if (_unlockedCompanions.Contains(companionId))
                return true;
            // Check the gacha log for any pull of this companion
            foreach (var entry in _gachaLog)
            {
                if (entry.Contains(FormatItemName(companionId)))
                    return true;
            }
            return false;
        }

        // ================================================================
        // Test data factory helpers
        // ================================================================

        private RecipeDefinitionSO CreateRecipe(string name, string id, CraftingDiscipline discipline,
            int requiredLevel, params (string itemId, int qty)[] ingredients)
        {
            var recipe = ScriptableObject.CreateInstance<RecipeDefinitionSO>();
            recipe.RecipeName = name;
            recipe.RecipeId = id;
            recipe.Discipline = discipline;
            recipe.RequiredSkillLevel = requiredLevel;
            recipe.OutputItemId = id;
            recipe.OutputQuantity = 1;
            recipe.Ingredients = new List<RecipeIngredient>();
            foreach (var (itemId, qty) in ingredients)
                recipe.Ingredients.Add(new RecipeIngredient { ItemId = itemId, Quantity = qty });
            return recipe;
        }

        private QuestDefinitionSO CreateQuest(string name, string id, QuestType type, string description,
            (string desc, ObjectiveType objType, string targetId, int count)[] objectives,
            (string itemId, int qty, int xp, int gold)[] rewards)
        {
            var quest = ScriptableObject.CreateInstance<QuestDefinitionSO>();
            quest.QuestName = name;
            quest.QuestId = id;
            quest.Type = type;
            quest.Description = description;
            quest.Objectives = new List<QuestObjectiveSO>();
            foreach (var (desc, objType, targetId, count) in objectives)
            {
                var obj = ScriptableObject.CreateInstance<QuestObjectiveSO>();
                obj.ObjectiveDescription = desc;
                obj.Type = objType;
                obj.TargetId = targetId;
                obj.RequiredCount = count;
                quest.Objectives.Add(obj);
            }
            quest.Rewards = new List<QuestReward>();
            foreach (var (itemId, qty, xp, gold) in rewards)
                quest.Rewards.Add(new QuestReward { ItemId = itemId, Quantity = qty, XP = xp, Gold = gold });
            return quest;
        }
    }
}
