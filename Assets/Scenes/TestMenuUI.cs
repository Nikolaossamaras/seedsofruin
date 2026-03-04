using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SoR.Core;
using SoR.Shared;
using SoR.Combat;
using SoR.Systems.Inventory;
using SoR.Systems.Crafting;
using SoR.Systems.Gacha;
using SoR.Systems.Shop;
using SoR.Systems.Quests;
using SoR.Gameplay;
using SoR.UI;
using UnityEngine.EventSystems;
using SoR.Progression;

namespace SoR.Testing
{
    /// <summary>
    /// Builds all UI screens at runtime with test data and keyboard shortcuts.
    /// Attach alongside TestSceneSetup or on its own in any scene.
    ///
    /// Keybinds (hold TAB to see hint bar):
    ///   I = Inventory       C = Crafting      M = Map
    ///   G = Gacha           Q = Quest Log     K = Skills
    ///   P = Companions      E = NPC Shop / Equipment
    ///   T = Titles          ESC = Close current screen
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

        private TitleSystem _titleSystem;
        private readonly List<TitleDefinitionSO> _testTitles = new();

        // ---- UI root ----
        private Canvas _canvas;
        private Font _font;

        // ---- screen panels ----
        private GameObject _activeScreen;
        private readonly Dictionary<KeyCode, System.Action> _screenKeys = new();

        // ---- test data ----
        private readonly List<BannerDefinitionSO> _testBanners = new();
        private int _gachaBannerIndex = 0;    // selected banner tab
        private int _gachaSubScreen = 0;      // 0=Main, 1=Wish History, 2=Rate Details
        private int _starseeds = 3200;        // summoning currency (~20 pulls)
        private readonly List<(string companionId, Rarity rarity, bool isDuplicate, string bannerName, int pullNumber)> _wishHistory = new();
        private const int StarseedCostSingle = 160;
        private const int StarseedCost10Pull = 1440;  // 10% discount
        private readonly Dictionary<string, ShopInventorySO> _shopDefs = new();
        private readonly List<RecipeDefinitionSO> _testRecipes = new();
        private readonly List<QuestDefinitionSO> _testQuests = new();
        private int _gold = 5000;
        private int _guildTokens = 200;
        private int _accordEssence;

        // ---- interaction prompt ----
        private GameObject _interactPrompt;
        private Text _interactPromptText;

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

        // ---- companion leveling ----
        private readonly Dictionary<string, int> _companionLevels = new(); // companionId → level (1-45)
        private int _playerLevel = 1;

        // ---- guild system ----
        private int _guildReputation = 0;
        private int _guildContractsCompleted = 0;
        private int _guildTab = 0; // 0=Info, 1=Contracts, 2=Active, 3=Turn In
        private readonly Dictionary<string, (int RequiredRank, int ReputationReward)> _guildContractMeta = new();

        // ---- weapon system ----
        private readonly Dictionary<string, WeaponDefinitionSO> _weaponDefs = new();
        private readonly HashSet<string> _ownedWeapons = new();
        private string _equippedWeaponId;

        // ---- fog of war ----
        private readonly HashSet<int> _revealedFogCells = new();
        private readonly HashSet<string> _visitedRegions = new();
        private const int FogGridRes = 20;
        private const float FogCellSize = 10f;       // 200 / 20
        private const float FogWorldMin = -100f;
        private const float FogRevealRadius = 18f;
        private RectTransform _mapBlipRt;
        private RectTransform _mapGlowRt;
        private Transform _mapFogLayer;
        private int _mapRenderedFogCount;

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
            _screenKeys[KeyCode.C] = ShowCrafting;
            _screenKeys[KeyCode.G] = ShowGacha;
            _screenKeys[KeyCode.P] = ShowCompanions;
            _screenKeys[KeyCode.M] = ShowMap;
            _screenKeys[KeyCode.Q] = ShowQuestLog;
            _screenKeys[KeyCode.K] = ShowSkills;
            _screenKeys[KeyCode.T] = ShowTitles;
            // E is handled specially in Update() (NPC shop vs Equipment)
            _screenKeys[KeyCode.BackQuote] = ShowCheatMenu;

            // Persistent HUD elements (event-driven, no manual wiring needed)
            _notificationUI = ItemNotificationUI.Create(_canvas, _font);
            _questTrackerUI = QuestTrackerUI.Create(_canvas, _font);

            // Quest objective tracker — bridges game events to QuestManager.UpdateObjective()
            var trackerGo = new GameObject("QuestObjectiveTracker");
            trackerGo.AddComponent<SoR.Systems.Quests.QuestObjectiveTracker>();
        }

        private void RevealFogAroundPlayer()
        {
            if (_sceneSetup == null || _sceneSetup.PlayerTransform == null) return;

            Vector3 pPos = _sceneSetup.PlayerTransform.position;
            float px = pPos.x;
            float pz = pPos.z;

            int centerCol = (int)((px - FogWorldMin) / FogCellSize);
            int centerRow = (int)((pz - FogWorldMin) / FogCellSize);
            int cellRadius = Mathf.CeilToInt(FogRevealRadius / FogCellSize);

            for (int r = centerRow - cellRadius; r <= centerRow + cellRadius; r++)
            {
                for (int c = centerCol - cellRadius; c <= centerCol + cellRadius; c++)
                {
                    if (r < 0 || r >= FogGridRes || c < 0 || c >= FogGridRes) continue;

                    // Cell center in world space
                    float cellCx = FogWorldMin + (c + 0.5f) * FogCellSize;
                    float cellCz = FogWorldMin + (r + 0.5f) * FogCellSize;
                    float dx = px - cellCx;
                    float dz = pz - cellCz;
                    if (dx * dx + dz * dz <= FogRevealRadius * FogRevealRadius)
                    {
                        _revealedFogCells.Add(r * FogGridRes + c);
                    }
                }
            }

            // Track visited biome regions
            var regionData = new[]
            {
                ("Greenreach Valley",   0f,   0f, 35f),
                ("The Ashen Steppe",  -55f, -55f, 30f),
                ("Gloomtide Marshes",  55f, -55f, 30f),
                ("Frosthollow Peaks", -55f,  55f, 30f),
                ("The Withered Heart", 55f,  55f, 30f),
            };
            foreach (var (name, cx, cz, half) in regionData)
            {
                if (px >= cx - half && px <= cx + half && pz >= cz - half && pz <= cz + half)
                    _visitedRegions.Add(name);
            }
        }

        private void RebuildFogLayer()
        {
            if (_mapFogLayer == null) return;

            // Destroy existing fog tiles
            for (int i = _mapFogLayer.childCount - 1; i >= 0; i--)
                Destroy(_mapFogLayer.GetChild(i).gameObject);

            _mapRenderedFogCount = _revealedFogCells.Count;

            for (int fogRow = 0; fogRow < FogGridRes; fogRow++)
            {
                for (int fogCol = 0; fogCol < FogGridRes; fogCol++)
                {
                    int cellKey = fogRow * FogGridRes + fogCol;
                    if (_revealedFogCells.Contains(cellKey)) continue;

                    // Check if this cell is adjacent to any revealed cell (edge fog)
                    bool isEdge = false;
                    for (int dr = -1; dr <= 1 && !isEdge; dr++)
                    {
                        for (int dc = -1; dc <= 1 && !isEdge; dc++)
                        {
                            if (dr == 0 && dc == 0) continue;
                            int nr = fogRow + dr;
                            int nc = fogCol + dc;
                            if (nr >= 0 && nr < FogGridRes && nc >= 0 && nc < FogGridRes)
                            {
                                if (_revealedFogCells.Contains(nr * FogGridRes + nc))
                                    isEdge = true;
                            }
                        }
                    }

                    float ancMinX = fogCol / (float)FogGridRes;
                    float ancMinY = fogRow / (float)FogGridRes;
                    float ancMaxX = (fogCol + 1) / (float)FogGridRes;
                    float ancMaxY = (fogRow + 1) / (float)FogGridRes;

                    var fogGo = new GameObject("Fog");
                    fogGo.transform.SetParent(_mapFogLayer, false);
                    var fogRt = fogGo.AddComponent<RectTransform>();
                    fogRt.anchorMin = new Vector2(ancMinX, ancMinY);
                    fogRt.anchorMax = new Vector2(ancMaxX, ancMaxY);
                    fogRt.offsetMin = Vector2.zero;
                    fogRt.offsetMax = Vector2.zero;
                    var fogImg = fogGo.AddComponent<Image>();
                    float fogAlpha = isEdge ? 0.7f : 1f;
                    fogImg.color = new Color(0.15f, 0.12f, 0.08f, fogAlpha);
                    fogImg.raycastTarget = true;
                }
            }
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

            // Update interaction prompt (always, even when a screen is open)
            UpdateInteractPrompt();

            // Reveal fog cells around the player as they move
            RevealFogAroundPlayer();

            // Live-update map elements when the map screen is open
            if (_mapBlipRt != null && _sceneSetup != null && _sceneSetup.PlayerTransform != null)
            {
                Vector3 pp = _sceneSetup.PlayerTransform.position;
                Vector2 pm = new Vector2((pp.x + 100f) / 200f, (pp.z + 100f) / 200f);
                _mapBlipRt.anchorMin = pm;
                _mapBlipRt.anchorMax = pm;
                if (_mapGlowRt != null) { _mapGlowRt.anchorMin = pm; _mapGlowRt.anchorMax = pm; }
            }
            if (_mapFogLayer != null && _revealedFogCells.Count != _mapRenderedFogCount)
                RebuildFogLayer();

            // Only open screens when nothing is open (prevents accidental toggling)
            if (_activeScreen != null) return;

            // E-key: context-sensitive — NPC shop if near an NPC, otherwise Equipment
            if (UnityEngine.Input.GetKeyDown(KeyCode.E))
            {
                if (_sceneSetup != null)
                {
                    var nearNpc = _sceneSetup.GetNearestNPC(3.5f);
                    if (nearNpc.HasValue)
                    {
                        if (nearNpc.Value.ShopId == "adventure_guild")
                            ShowGuild();
                        else
                            ShowShopForNPC(nearNpc.Value);
                        return;
                    }
                }
                ShowEquipment();
                return;
            }

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

            _titleSystem = new TitleSystem();
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

            // --- Weapon Crafting (Rare tier) ---
            _testRecipes.Add(CreateRecipe("Verdant Scythe", "verdant_scythe",
                CraftingDiscipline.Forging, 3,
                ("iron_ore", 10), ("ancient_seed", 2), ("verdance_shard", 3)));
            _testRecipes.Add(CreateRecipe("Blazeforged Hoe-Blade", "blazeforged_hoeblade",
                CraftingDiscipline.Forging, 4,
                ("iron_ore", 12), ("fire_crystal", 3)));
            _testRecipes.Add(CreateRecipe("Frost Sickle & Shield", "frost_sickle_shield",
                CraftingDiscipline.Forging, 3,
                ("iron_ore", 8), ("moonstone", 1), ("runic_dust", 4)));
            _testRecipes.Add(CreateRecipe("Runic Pitchfork", "runic_pitchfork",
                CraftingDiscipline.Runebinding, 4,
                ("iron_ore", 10), ("runic_dust", 6), ("moonstone", 1)));
            _testRecipes.Add(CreateRecipe("Elemental Seed Sling", "elemental_sling",
                CraftingDiscipline.Seedcraft, 3,
                ("silk_thread", 5), ("fire_crystal", 2), ("ancient_seed", 1)));
            _testRecipes.Add(CreateRecipe("Apprentice's Druid Staff", "apprentice_staff",
                CraftingDiscipline.Herbalism, 4,
                ("ancient_seed", 2), ("herb_bundle", 10), ("runic_dust", 3)));

            // --- Shops (all 6 GDD shops) ---
            SeedAllShops();

            // --- Gacha Banners (4 banners, GDD-compliant) ---
            var pityConfig = ScriptableObject.CreateInstance<PityConfigSO>();
            pityConfig.LegendarySoftPity = 50;
            pityConfig.LegendaryHardPity = 60;
            pityConfig.MythicHardPity = 999; // disabled
            pityConfig.SoftPityRateBoost = 0.06f;

            // All 32 companion IDs by tier
            var pool3Star = new List<string>
            {
                "companion_tomas", "companion_berta", "companion_pip", "companion_marsh",
                "companion_willow", "companion_emmet", "companion_lira", "companion_bruno",
                "companion_mira", "companion_sage"
            };
            var pool4Star = new List<string>
            {
                "companion_brynn", "companion_hale", "companion_nix", "companion_petra",
                "companion_elara", "companion_fenwick", "companion_maeven", "companion_rook",
                "companion_denna", "companion_cassius", "companion_wren", "companion_jareth"
            };
            var pool5Star = new List<string>
            {
                "companion_seraphine", "companion_korrath", "companion_yuki", "companion_theron",
                "companion_lyssara", "companion_vex", "companion_orin", "companion_zara",
                "companion_malachai", "companion_kaelen"
            };

            // Banner 1: Standard Verdant (no featured)
            var standardBanner = ScriptableObject.CreateInstance<BannerDefinitionSO>();
            standardBanner.BannerName = "Standard Verdant";
            standardBanner.BannerId = "standard_verdant";
            standardBanner.Description = "The standard summoning banner — all companions available.";
            standardBanner.CommonRate = 0.85f;
            standardBanner.RareRate = 0.12f;
            standardBanner.LegendaryRate = 0.03f;
            standardBanner.MythicRate = 0f;
            standardBanner.PityConfig = pityConfig;
            standardBanner.CommonPool = new List<string>(pool3Star);
            standardBanner.RarePool = new List<string>(pool4Star);
            standardBanner.LegendaryPool = new List<string>(pool5Star);
            standardBanner.MythicPool = new List<string>();
            standardBanner.FeaturedCompanionId = "";
            standardBanner.FeaturedRarity = Rarity.Common;
            _testBanners.Add(standardBanner);

            // Banner 2: Radiant Pyre (Featured: Seraphine Dawnveil)
            var featuredBanner = ScriptableObject.CreateInstance<BannerDefinitionSO>();
            featuredBanner.BannerName = "Radiant Pyre";
            featuredBanner.BannerId = "featured_seraphine";
            featuredBanner.Description = "Featured: Seraphine Dawnveil (5★ Magician / Pyro)";
            featuredBanner.CommonRate = 0.85f;
            featuredBanner.RareRate = 0.12f;
            featuredBanner.LegendaryRate = 0.03f;
            featuredBanner.MythicRate = 0f;
            featuredBanner.PityConfig = pityConfig;
            featuredBanner.CommonPool = new List<string>(pool3Star);
            featuredBanner.RarePool = new List<string>(pool4Star);
            featuredBanner.LegendaryPool = new List<string>(pool5Star);
            featuredBanner.MythicPool = new List<string>();
            featuredBanner.FeaturedCompanionId = "companion_seraphine";
            featuredBanner.FeaturedRarity = Rarity.Legendary;
            _testBanners.Add(featuredBanner);

            // Banner 3: Elemental Convergence (Pyro + Verdant only)
            var pyroVerdantCommon = pool3Star.FindAll(id =>
                CompanionRegistry.TryGetValue(id, out var info) && (info.Element == "Pyro" || info.Element == "Verdant"));
            var pyroVerdantRare = pool4Star.FindAll(id =>
                CompanionRegistry.TryGetValue(id, out var info) && (info.Element == "Pyro" || info.Element == "Verdant"));
            var pyroVerdantLeg = pool5Star.FindAll(id =>
                CompanionRegistry.TryGetValue(id, out var info) && (info.Element == "Pyro" || info.Element == "Verdant"));

            var elementalBanner = ScriptableObject.CreateInstance<BannerDefinitionSO>();
            elementalBanner.BannerName = "Elemental Convergence";
            elementalBanner.BannerId = "elemental_pyro_verdant";
            elementalBanner.Description = "Elemental focus: only Pyro & Verdant companions appear.";
            elementalBanner.CommonRate = 0.85f;
            elementalBanner.RareRate = 0.12f;
            elementalBanner.LegendaryRate = 0.03f;
            elementalBanner.MythicRate = 0f;
            elementalBanner.PityConfig = pityConfig;
            elementalBanner.CommonPool = pyroVerdantCommon;
            elementalBanner.RarePool = pyroVerdantRare;
            elementalBanner.LegendaryPool = pyroVerdantLeg;
            elementalBanner.MythicPool = new List<string>();
            elementalBanner.FeaturedCompanionId = "";
            elementalBanner.FeaturedRarity = Rarity.Common;
            _testBanners.Add(elementalBanner);

            // Banner 4: Legacy — Theron (Featured: Theron Ashblood)
            var legacyBanner = ScriptableObject.CreateInstance<BannerDefinitionSO>();
            legacyBanner.BannerName = "Legacy: Theron";
            legacyBanner.BannerId = "legacy_theron";
            legacyBanner.Description = "Featured: Theron Ashblood (5★ Necromancer / Umbral)";
            legacyBanner.CommonRate = 0.85f;
            legacyBanner.RareRate = 0.12f;
            legacyBanner.LegendaryRate = 0.03f;
            legacyBanner.MythicRate = 0f;
            legacyBanner.PityConfig = pityConfig;
            legacyBanner.CommonPool = new List<string>(pool3Star);
            legacyBanner.RarePool = new List<string>(pool4Star);
            legacyBanner.LegendaryPool = new List<string>(pool5Star);
            legacyBanner.MythicPool = new List<string>();
            legacyBanner.FeaturedCompanionId = "companion_theron";
            legacyBanner.FeaturedRarity = Rarity.Legendary;
            _testBanners.Add(legacyBanner);

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

            // --- Guild Contracts (10 total) ---
            _testQuests.Add(CreateGuildContract("The Forgemaster's Test", "quest_forge_01",
                "Craft an iron blade to prove your skill.",
                0, 25,
                new[] { ("Craft an Iron Blade", ObjectiveType.Craft, "iron_blade", 1) },
                new[] { ("fire_crystal", 2, 200, 100) }));

            _testQuests.Add(CreateGuildContract("Wolf Pelts Wanted", "guild_wolf_pelts",
                "The guild needs wolf pelts for its supply stores.",
                0, 25,
                new[] { ("Collect wolf pelts", ObjectiveType.Collect, "wolf_pelt", 5) },
                new[] { ("health_potion", 3, 80, 60) }));

            _testQuests.Add(CreateGuildContract("Scorched Earth Patrol", "guild_scorched_patrol",
                "Patrol the Ashen Steppe and eliminate scorched husks threatening travelers.",
                1, 50,
                new[] { ("Kill scorched husks", ObjectiveType.Kill, "scorched_husk", 6) },
                new[] { ("fire_crystal", 3, 200, 120) }));

            _testQuests.Add(CreateGuildContract("Fire Mineral Survey", "guild_fire_minerals",
                "Collect fire minerals from the volcanic vents in the steppe.",
                1, 50,
                new[] { ("Collect fire minerals", ObjectiveType.Collect, "fire_mineral", 8) },
                new[] { ("runic_dust", 5, 180, 100) }));

            _testQuests.Add(CreateGuildContract("Marsh Wraith Bounty", "guild_marsh_wraith",
                "Marsh wraiths are terrorizing the Gloomtide Marshes. Eliminate them.",
                2, 75,
                new[] { ("Kill marsh wraiths", ObjectiveType.Kill, "marsh_wraith", 8) },
                new[] { ("shadow_essence", 2, 300, 180) }));

            _testQuests.Add(CreateGuildContract("Fungal Specimen Collection", "guild_fungal_specimens",
                "Collect rare fungal specimens for the guild's research division.",
                2, 75,
                new[] { ("Collect fungal specimens", ObjectiveType.Collect, "fungal_specimen", 10) },
                new[] { ("fungal_extract", 4, 250, 150) }));

            _testQuests.Add(CreateGuildContract("Frost Sentinel Extermination", "guild_frost_sentinels",
                "Frost sentinels have overrun the mountain passes. Clear them out.",
                3, 100,
                new[] { ("Kill frost sentinels", ObjectiveType.Kill, "frost_sentinel", 10) },
                new[] { ("frost_shard", 4, 400, 250) }));

            _testQuests.Add(CreateGuildContract("Ancient Scroll Recovery", "guild_ancient_scrolls",
                "Recover ancient scrolls scattered across the Frosthollow Peaks.",
                3, 100,
                new[] { ("Collect ancient scrolls", ObjectiveType.Collect, "ancient_scroll", 5) },
                new[] { ("runic_core", 1, 450, 300) }));

            _testQuests.Add(CreateGuildContract("Blight Nexus Purge", "guild_blight_nexus",
                "Locate and purge blight nexus points before they spread further.",
                4, 150,
                new[] {
                    ("Explore blight nexus sites", ObjectiveType.Explore, "blight_nexus", 5),
                    ("Kill nexus guardians", ObjectiveType.Kill, "nexus_guardian", 8)
                },
                new[] { ("blight_ward", 1, 600, 400) }));

            _testQuests.Add(CreateGuildContract("The Wither Knight Hunt", "guild_wither_knight",
                "A legendary wither knight has been sighted. Only the guild's finest should attempt this.",
                4, 150,
                new[] { ("Kill wither knights", ObjectiveType.Kill, "wither_knight", 3) },
                new[] { ("dark_crystal", 2, 800, 500) }));

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

            // --- Weapons ---
            SeedWeapons();

            // --- Fog of War: pre-reveal spawn area around (0,0) ---
            RevealFogAroundPlayer();
            // Also manually reveal a small cluster around world origin
            for (int r = 8; r <= 11; r++)
                for (int c = 8; c <= 11; c++)
                    _revealedFogCells.Add(r * FogGridRes + c);
            _visitedRegions.Add("Greenreach Valley");
        }

        // ================================================================
        // Weapon definitions
        // ================================================================

        private WeaponDefinitionSO CreateWeaponDef(string id, string name, string desc,
            WeaponType wType, Rarity rarity, float damage, float stagger, float speed,
            float charged, float range, int combo, DamageType dmgType, Element element,
            string passive = null)
        {
            var w = ScriptableObject.CreateInstance<WeaponDefinitionSO>();
            w.WeaponName = name;
            w.Description = desc;
            w.WeaponType = wType;
            w.Rarity = rarity;
            w.BaseDamage = damage;
            w.BaseStagger = stagger;
            w.AttackSpeed = speed;
            w.ChargedMultiplier = charged;
            w.Range = range;
            w.MaxComboHits = combo;
            w.DamageType = dmgType;
            w.Element = element;
            w.UniquePassive = passive;
            _weaponDefs[id] = w;
            return w;
        }

        private void SeedWeapons()
        {
            // ---- COMMON (shop buyable) ----
            CreateWeaponDef("rusty_scythe", "Rusty Scythe", "A weathered scythe. Still cuts.",
                WeaponType.Scythe, Rarity.Common, 28f, 8f, 1.1f, 1.5f, 2.5f, 3, DamageType.Physical, Element.None);
            CreateWeaponDef("farmers_hoeblade", "Farmer's Hoe-Blade", "Repurposed farm tool with a sharp edge.",
                WeaponType.HoeBlade, Rarity.Common, 38f, 14f, 0.8f, 1.8f, 2f, 2, DamageType.Physical, Element.None);
            CreateWeaponDef("old_sickle_buckler", "Old Sickle & Buckler", "A bent sickle paired with a dented shield.",
                WeaponType.SickleShield, Rarity.Common, 20f, 5f, 1.5f, 1.3f, 1.5f, 4, DamageType.Physical, Element.None);
            CreateWeaponDef("worn_pitchfork", "Worn Pitchfork", "Three rusty prongs, still pointy.",
                WeaponType.PitchforkSpear, Rarity.Common, 30f, 9f, 1.0f, 1.5f, 3.5f, 3, DamageType.Physical, Element.None);
            CreateWeaponDef("basic_seed_sling", "Basic Seed Sling", "Launches seed bombs at short range.",
                WeaponType.SeedSling, Rarity.Common, 22f, 3f, 1.6f, 1.4f, 6f, 3, DamageType.Magical, Element.Verdant);

            // ---- UNCOMMON (shop buyable + some craftable) ----
            CreateWeaponDef("iron_scythe", "Iron Scythe", "Forged iron blade with balanced weight.",
                WeaponType.Scythe, Rarity.Uncommon, 42f, 12f, 1.2f, 1.5f, 2.5f, 3, DamageType.Physical, Element.None,
                "Attacks hit enemies in a wider arc.");
            CreateWeaponDef("steel_hoeblade", "Steel Hoe-Blade", "Bram's forge-tempered steel chopper.",
                WeaponType.HoeBlade, Rarity.Uncommon, 55f, 18f, 0.85f, 1.8f, 2f, 2, DamageType.Physical, Element.None,
                "+15% stagger damage.");
            CreateWeaponDef("guards_sickle", "Guard's Sickle & Shield", "Standard issue for town guards.",
                WeaponType.SickleShield, Rarity.Uncommon, 30f, 7f, 1.6f, 1.3f, 1.5f, 4, DamageType.Physical, Element.None,
                "Blocking reduces 10% more damage.");
            CreateWeaponDef("iron_pitchfork", "Iron Pitchfork Spear", "Reinforced prongs on a sturdy shaft.",
                WeaponType.PitchforkSpear, Rarity.Uncommon, 44f, 13f, 1.05f, 1.5f, 3.5f, 3, DamageType.Physical, Element.None,
                "Charge attack has extended reach.");
            CreateWeaponDef("reinforced_sling", "Reinforced Seed Sling", "Better pouch, better range.",
                WeaponType.SeedSling, Rarity.Uncommon, 34f, 5f, 1.7f, 1.4f, 7f, 3, DamageType.Magical, Element.Verdant,
                "+10% projectile speed.");

            // ---- RARE (craftable at Bram's Forge) ----
            CreateWeaponDef("verdant_scythe", "Verdant Scythe", "Infused with living vines that extend its reach.",
                WeaponType.Scythe, Rarity.Rare, 60f, 16f, 1.2f, 1.6f, 3f, 4, DamageType.Physical, Element.Verdant,
                "Attacks leave a verdant trail that heals 1% HP. AoE radius +20%.");
            CreateWeaponDef("blazeforged_hoeblade", "Blazeforged Hoe-Blade", "Forged in volcanic flame, glows red-hot.",
                WeaponType.HoeBlade, Rarity.Rare, 75f, 22f, 0.9f, 2.0f, 2f, 3, DamageType.Physical, Element.Pyro,
                "Charged attacks ignite targets. +25% guard break.");
            CreateWeaponDef("frost_sickle_shield", "Frost Sickle & Shield", "Ice-tempered blade with a frost-ward shield.",
                WeaponType.SickleShield, Rarity.Rare, 40f, 10f, 1.7f, 1.4f, 1.5f, 4, DamageType.Physical, Element.Cryo,
                "Perfect blocks freeze attacker for 1s. +15% block efficiency.");
            CreateWeaponDef("runic_pitchfork", "Runic Pitchfork", "Ancient runes glow along the shaft.",
                WeaponType.PitchforkSpear, Rarity.Rare, 58f, 17f, 1.1f, 1.6f, 4f, 3, DamageType.Physical, Element.Geo,
                "Thrusts create a shockwave at max range. Charge attack pins enemies.");
            CreateWeaponDef("elemental_sling", "Elemental Seed Sling", "Channels elemental energy into each shot.",
                WeaponType.SeedSling, Rarity.Rare, 48f, 7f, 1.8f, 1.5f, 8f, 4, DamageType.Magical, Element.Verdant,
                "Shots cycle through Pyro/Cryo/Volt elements. +20% projectile damage.");
            CreateWeaponDef("apprentice_staff", "Apprentice's Druid Staff", "A staff given to those who begin the druid path.",
                WeaponType.DruidStaff, Rarity.Rare, 65f, 8f, 0.75f, 2.0f, 3f, 2, DamageType.Magical, Element.Verdant,
                "Verdance skills cost 15% less. +10 Verdance stat.");

            // ---- EPIC (boss drops / hidden quests) ----
            CreateWeaponDef("reapers_crescent", "Reaper's Crescent", "A crescent blade that drinks the life of the fallen.",
                WeaponType.Scythe, Rarity.Epic, 85f, 20f, 1.3f, 1.7f, 3f, 4, DamageType.Physical, Element.Umbral,
                "Kills restore 5% max HP. AoE attacks apply Shadow Blight for 3s. +15% crit chance.");
            CreateWeaponDef("earthshatter", "Earthshatter", "Each swing cracks the earth beneath it.",
                WeaponType.HoeBlade, Rarity.Epic, 100f, 30f, 0.9f, 2.2f, 2.5f, 3, DamageType.Physical, Element.Geo,
                "Ground strikes create fissures dealing 30% bonus AoE. Staggered enemies take +25% damage. Guard break guaranteed.");
            CreateWeaponDef("aegis_of_thorns", "Aegis of Thorns", "Living thorns grow across shield and blade.",
                WeaponType.SickleShield, Rarity.Epic, 52f, 12f, 1.8f, 1.5f, 1.5f, 5, DamageType.Physical, Element.Verdant,
                "Blocking reflects 20% damage as thorns. Perfect block heals 3% HP. +20% block efficiency.");
            CreateWeaponDef("blight_piercer", "Blight Piercer", "A corrupted spear that drains the land's vitality.",
                WeaponType.PitchforkSpear, Rarity.Epic, 78f, 20f, 1.15f, 1.7f, 4.5f, 4, DamageType.Physical, Element.Umbral,
                "Thrusts apply Blight (DoT 2% per sec for 4s). Charge attack pierces through enemies. +20% range.");
            CreateWeaponDef("storm_sling", "Storm Sling", "Channels lightning through every seed.",
                WeaponType.SeedSling, Rarity.Epic, 65f, 10f, 2.0f, 1.6f, 9f, 4, DamageType.Magical, Element.Volt,
                "Shots chain lightning to 2 nearby enemies. Crits cause a thunderclap AoE. +25% attack speed.");
            CreateWeaponDef("elder_druid_staff", "Elder Druid Staff", "Carved from the heartwood of the World Tree.",
                WeaponType.DruidStaff, Rarity.Epic, 90f, 12f, 0.8f, 2.2f, 3.5f, 3, DamageType.Magical, Element.Verdant,
                "Verdance skills cost 25% less. Attacks summon healing spores. +20 Verdance stat. +15% magic damage.");

            // ---- LEGENDARY (final bosses / secret content) ----
            CreateWeaponDef("millhavens_memory", "Millhaven's Memory", "The scythe remembers every harvest, and gives back.",
                WeaponType.Scythe, Rarity.Legendary, 110f, 25f, 1.3f, 1.8f, 3.5f, 4, DamageType.Physical, Element.Verdant,
                "Attacks heal 3% of damage dealt. Glows with golden light. AoE +30%. On kill: burst heal 8% to nearby allies.");
            CreateWeaponDef("ashwoods_regret", "Ashwood's Regret", "A druid's remorse given form — vines and sorrow.",
                WeaponType.DruidStaff, Rarity.Legendary, 120f, 15f, 0.85f, 2.5f, 4f, 3, DamageType.Magical, Element.Verdant,
                "Verdance skills cost 30% less. Crits summon spectral vines that root enemies for 2s. +30 Verdance. +25% magic damage.");
            CreateWeaponDef("withered_throne", "The Withered Throne", "Power demands a price — your very life force.",
                WeaponType.HoeBlade, Rarity.Legendary, 140f, 35f, 0.95f, 2.5f, 2.5f, 3, DamageType.Physical, Element.Umbral,
                "+50% damage, but -1% max HP per hit. Dark corruption visual. Kills restore 3% max HP. Stagger damage doubled.");
            CreateWeaponDef("scarecrows_fang", "Scarecrow's Fang", "The crows still remember its terror.",
                WeaponType.SickleShield, Rarity.Legendary, 68f, 15f, 2.0f, 1.6f, 2f, 5, DamageType.Physical, Element.Umbral,
                "Perfect blocks terrify all nearby enemies for 3s. +30% block efficiency. Terrified enemies take +20% damage. Counter-attacks deal double.");
            CreateWeaponDef("primordial_root", "The Primordial Root", "Born from the first seed, older than memory.",
                WeaponType.PitchforkSpear, Rarity.Legendary, 95f, 25f, 1.2f, 1.8f, 5f, 4, DamageType.Physical, Element.Verdant,
                "Attacks plant seeds that explode after 2s for AoE damage. Charge attack summons root cage. +25% range. On kill: grow a healing flower.");

            // Give player starting weapons
            _ownedWeapons.Add("rusty_scythe");
            _ownedWeapons.Add("old_sickle_buckler");
            _equippedWeaponId = "rusty_scythe";

            // Apply starting weapon to scene
            if (_sceneSetup != null && _weaponDefs.TryGetValue(_equippedWeaponId, out var startWeapon))
                _sceneSetup.SetActiveWeapon(startWeapon);
        }

        // ================================================================
        // Shop definitions
        // ================================================================

        private void SeedAllShops()
        {
            CreateAndRegisterShop("general_store", "Maren's General Store", new[]
            {
                ("health_potion", 50, -1),
                ("verdance_shard", 120, 5),
                ("herb_bundle", 15, -1),
                ("antidote", 30, -1),
                ("travel_ration", 20, -1),
            });

            CreateAndRegisterShop("brams_forge", "Bram's Forge", new[]
            {
                ("iron_ore", 25, -1),
                ("repair_kit", 100, 5),
                ("reinforced_helm", 350, 2),
                // Common weapons
                ("rusty_scythe", 120, 3),
                ("farmers_hoeblade", 150, 3),
                ("old_sickle_buckler", 100, 3),
                ("worn_pitchfork", 130, 3),
                // Uncommon weapons
                ("iron_scythe", 400, 2),
                ("steel_hoeblade", 500, 2),
                ("guards_sickle", 350, 2),
                ("iron_pitchfork", 420, 2),
            });

            CreateAndRegisterShop("seed_merchant", "Silas's Curious Seeds", new[]
            {
                ("ancient_seed", 200, 3),
                ("blight_resistant_seed", 150, 5),
                ("verdant_bulb", 80, -1),
                ("growth_elixir", 120, 8),
                ("basic_seed_sling", 140, 2),
                ("reinforced_sling", 380, 1),
            });

            CreateAndRegisterShop("guild_quartermaster", "Guild Quartermaster", new[]
            {
                ("guild_badge", 50, 1),
                ("elite_potion", 80, 1),
                ("guild_map", 30, 1),
            });

            CreateAndRegisterShop("wandering_druid", "Druid Enna's Remedies", new[]
            {
                ("purify_charm", 180, 3),
                ("swamp_antidote", 60, -1),
                ("fungal_extract", 90, 10),
                ("blight_ward", 300, 1),
            });

            CreateAndRegisterShop("black_market", "The Whisperer's Wares", new[]
            {
                ("shadow_blade", 800, 1),
                ("forbidden_tome", 1200, 1),
                ("venom_vial", 150, 5),
                ("dark_crystal", 500, 2),
            });

            // --- Titles ---
            // Farming
            CreateTitle("seedling", "Seedling", "+5% XP from gathering",
                "Harvest 10 crops", new StatBlock { Harvest = 2 });
            CreateTitle("greenhand", "Greenhand", "+10% healing from consumables",
                "Use 25 consumables", new StatBlock { Vigor = 3 });
            CreateTitle("master_cultivator", "Master Cultivator", "+15% crafting quality",
                "Craft 50 items", new StatBlock { Harvest = 5 });
            CreateTitle("son_of_the_soil", "Son of the Soil", "+20% crit chance",
                "Reach Harvest level 10", new StatBlock { Agility = 6 });
            CreateTitle("last_farmer_of_millhaven", "Last Farmer of Millhaven", "+10% dmg vs Withered",
                "Complete the Millhaven questline", new StatBlock { Strength = 4 });

            // Combat
            CreateTitle("reluctant_blade", "Reluctant Blade", "+5% dodge distance",
                "Win your first combat encounter", new StatBlock { Agility = 2 });
            CreateTitle("scarecrow", "Scarecrow", "+10% intimidation",
                "Defeat 50 enemies", new StatBlock { Strength = 3, Resilience = 2 });
            CreateTitle("blight_reaper", "Blight Reaper", "+15% dmg vs corrupted",
                "Defeat 100 Blighted enemies", new StatBlock { Strength = 5 });
            CreateTitle("warden_of_the_green", "Warden of the Green", "+20% Verdance regen",
                "Restore 3 corrupted zones", new StatBlock { Verdance = 8 });
            CreateTitle("legend_of_eldrath", "Legend of Eldrath", "+25% all stats in NG+",
                "Complete the main story", new StatBlock { Vigor = 5, Strength = 5, Harvest = 5, Verdance = 5, Agility = 5, Resilience = 5 });

            // Exploration & Social
            CreateTitle("wanderer", "Wanderer", "+10% move speed on roads",
                "Discover 5 regions", new StatBlock { Agility = 4 });
            CreateTitle("lorekeep", "Lorekeep", "+15% XP from quests",
                "Complete 20 quests", new StatBlock { Vigor = 4 });
            CreateTitle("the_peoples_champion", "The People's Champion", "+20% shop discounts",
                "Reach max reputation with 3 NPCs", new StatBlock { Resilience = 5 });
            CreateTitle("herbalist_supreme", "Herbalist Supreme", "+25% potion potency",
                "Craft 30 potions", new StatBlock { Harvest = 6, Verdance = 4 });
            CreateTitle("accord_keeper", "Accord Keeper", "+15% Accord Essence gains",
                "Complete 5 Accord quests", new StatBlock { Resilience = 3, Vigor = 3 });

            // Starter unlocks (one per category)
            _titleSystem.UnlockTitle("seedling");
            _titleSystem.UnlockTitle("reluctant_blade");
            _titleSystem.UnlockTitle("wanderer");
        }

        private void CreateAndRegisterShop(string shopId, string shopName, (string itemId, int price, int stock)[] items)
        {
            var def = ScriptableObject.CreateInstance<ShopInventorySO>();
            def.ShopName = shopName;
            def.ShopId = shopId;
            def.Items = new List<ShopItem>();
            foreach (var (itemId, price, stock) in items)
                def.Items.Add(new ShopItem { ItemId = itemId, Price = price, Stock = stock });
            _shop.RegisterShop(def);
            _shopDefs[shopId] = def;
        }

        // ================================================================
        // NPC interaction
        // ================================================================

        private void UpdateInteractPrompt()
        {
            if (_sceneSetup == null || _activeScreen != null)
            {
                ClearInteractPrompt();
                return;
            }

            var nearNpc = _sceneSetup.GetNearestNPC(3.5f);
            if (nearNpc.HasValue)
                ShowInteractPrompt(nearNpc.Value.Name);
            else
                ClearInteractPrompt();
        }

        private void ShowInteractPrompt(string npcName)
        {
            if (_interactPrompt == null)
            {
                _interactPrompt = new GameObject("InteractPrompt");
                _interactPrompt.transform.SetParent(_canvas.transform, false);
                var rt = _interactPrompt.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(0f, 50f);
                rt.sizeDelta = new Vector2(320f, 40f);

                var bg = _interactPrompt.AddComponent<Image>();
                bg.color = new Color(0.05f, 0.05f, 0.1f, 0.85f);

                var textGo = new GameObject("Text");
                textGo.transform.SetParent(_interactPrompt.transform, false);
                _interactPromptText = textGo.AddComponent<Text>();
                _interactPromptText.font = _font;
                _interactPromptText.fontSize = 18;
                _interactPromptText.color = new Color(1f, 0.9f, 0.6f);
                _interactPromptText.alignment = TextAnchor.MiddleCenter;
                var tRt = textGo.GetComponent<RectTransform>();
                tRt.anchorMin = Vector2.zero;
                tRt.anchorMax = Vector2.one;
                tRt.offsetMin = Vector2.zero;
                tRt.offsetMax = Vector2.zero;
            }

            _interactPromptText.text = $"[E] Talk to {npcName}";
            _interactPrompt.SetActive(true);
        }

        private void ClearInteractPrompt()
        {
            if (_interactPrompt != null)
                _interactPrompt.SetActive(false);
        }

        private void ShowShopForNPC(TestSceneSetup.NpcEntry npc)
        {
            if (!_shopDefs.TryGetValue(npc.ShopId, out var shopDef))
            {
                Debug.LogWarning($"[Shop] No shop definition for {npc.ShopId}");
                return;
            }

            bool isGuildShop = npc.ShopId == "guild_quartermaster";
            string currencyLabel = isGuildShop ? "Guild Tokens" : "Gold";
            int currencyAmount = isGuildShop ? _guildTokens : _gold;

            var panel = CreateScreenPanel(shopDef.ShopName);
            var content = CreateScrollContent(panel.transform, new Vector2(0f, 0f), new Vector2(1f, 0.9f));

            // Currency header
            AddLabel(panel.transform, $"{currencyLabel}: {currencyAmount}", 18, TextAnchor.MiddleRight,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-20f, -60f), new Vector2(250f, 30f), new Color(1f, 0.85f, 0.3f));

            // If guild shop, also show gold
            if (isGuildShop)
            {
                AddLabel(panel.transform, $"Gold: {_gold}", 14, TextAnchor.MiddleRight,
                    new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                    new Vector2(-20f, -85f), new Vector2(200f, 24f), new Color(0.7f, 0.7f, 0.7f));
            }

            int row = 0;
            foreach (var item in shopDef.Items)
            {
                string stockStr = item.Stock < 0 ? "inf" : item.Stock.ToString();
                string priceTag = isGuildShop ? $"{item.Price} GT" : $"{item.Price}g";
                bool canAfford = isGuildShop
                    ? _guildTokens >= item.Price
                    : _shop.CanBuy(shopDef.ShopId, item.ItemId, _gold);
                bool inStock = item.Stock != 0;
                bool alreadyOwned = _weaponDefs.ContainsKey(item.ItemId) && _ownedWeapons.Contains(item.ItemId);
                bool canBuy = canAfford && inStock && !alreadyOwned;
                Color color = !canBuy ? new Color(0.5f, 0.5f, 0.5f)
                    : _weaponDefs.ContainsKey(item.ItemId) ? WeaponRarityColor(_weaponDefs[item.ItemId].Rarity)
                    : Color.white;

                bool isWeapon = _weaponDefs.TryGetValue(item.ItemId, out var shopWeapon);
                string ownedTag = alreadyOwned ? "  [OWNED]" : "";
                string weaponTag = isWeapon ? $"  [{WeaponTypeName(shopWeapon.WeaponType)}] DMG {shopWeapon.BaseDamage:F0}" : "";
                string line = $"  {FormatItemName(item.ItemId)}{weaponTag}   {priceTag}   Stock: {stockStr}{ownedTag}";
                AddRowLabel(content, line, row, color);

                string capturedShopId = shopDef.ShopId;
                string capturedItemId = item.ItemId;
                bool capturedIsGuild = isGuildShop;
                var capturedNpc = npc;
                AddButton(content, "Buy", row, () =>
                {
                    TryBuyFromShop(capturedShopId, capturedItemId, capturedIsGuild);
                    CloseActiveScreen();
                    ShowShopForNPC(capturedNpc);
                });

                row++;
            }

            SetContentHeight(content, row);
        }

        private void TryBuyFromShop(string shopId, string itemId, bool isGuildToken)
        {
            if (isGuildToken)
            {
                // Guild token shop: manually check price and stock, then use _shop.Buy with dummy gold
                if (!_shopDefs.TryGetValue(shopId, out var def)) return;
                ShopItem target = null;
                foreach (var si in def.Items)
                {
                    if (si.ItemId == itemId) { target = si; break; }
                }
                if (target == null) return;
                if (_guildTokens < target.Price) return;
                if (target.Stock == 0) return;

                // Use a temporary gold reserve to satisfy ShopSystem.Buy
                int tempGold = int.MaxValue / 2;
                if (_shop.Buy(shopId, itemId, ref tempGold))
                {
                    _guildTokens -= target.Price;
                    if (_weaponDefs.ContainsKey(itemId)) _ownedWeapons.Add(itemId);
                    Debug.Log($"[Shop] Bought {itemId} for {target.Price} Guild Tokens");
                }
            }
            else
            {
                if (_shop.Buy(shopId, itemId, ref _gold))
                {
                    if (_weaponDefs.ContainsKey(itemId)) _ownedWeapons.Add(itemId);
                    Debug.Log($"[Shop] Bought {itemId}");
                }
            }
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
            text.text = "[I] Inventory  [E] NPC/Equipment  [C] Crafting  [G] Gacha  [P] Companions  [M] Map  [Q] Quests  [K] Skills  [T] Titles  [`] Cheats  [ESC] Close";
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

            int row = 0;

            // ---- Equipped Weapon Detail ----
            AddRowLabel(content, "  --- Equipped Weapon ---", row, new Color(0.95f, 0.85f, 0.5f)); row++;

            if (!string.IsNullOrEmpty(_equippedWeaponId) && _weaponDefs.TryGetValue(_equippedWeaponId, out var equipped))
            {
                Color rarityCol = WeaponRarityColor(equipped.Rarity);
                AddRowLabel(content, $"  {equipped.WeaponName}", row, rarityCol); row++;
                AddRowLabel(content, $"    Type: {WeaponTypeName(equipped.WeaponType)}   Rarity: {WeaponRarityName(equipped.Rarity)}", row, new Color(0.7f, 0.7f, 0.7f)); row++;
                AddRowLabel(content, $"    DMG {equipped.BaseDamage:F0}  SPD {equipped.AttackSpeed:F1}  STG {equipped.BaseStagger:F0}  RNG {equipped.Range:F1}  Combo {equipped.MaxComboHits}", row, Color.white); row++;
                AddRowLabel(content, $"    Charged: {equipped.ChargedMultiplier:F1}x   {equipped.DamageType} / {equipped.Element}", row, Color.white); row++;
                if (!string.IsNullOrEmpty(equipped.UniquePassive))
                {
                    AddRowLabel(content, $"    {equipped.UniquePassive}", row, new Color(1f, 0.8f, 0.4f)); row++;
                }
            }
            else
            {
                AddRowLabel(content, "  (no weapon equipped)", row, Color.gray); row++;
            }

            AddRowLabel(content, "", row, Color.white); row++;

            // ---- Other Equipment Slots ----
            AddRowLabel(content, "  --- Armor & Accessories ---", row, new Color(0.95f, 0.85f, 0.5f)); row++;
            string[] armorSlotNames = { "Head", "Chest", "Legs", "Accessory" };
            EquipmentSlot[] armorSlots = {
                EquipmentSlot.Head, EquipmentSlot.Chest,
                EquipmentSlot.Legs, EquipmentSlot.Accessory
            };
            for (int i = 0; i < armorSlots.Length; i++)
            {
                string eq = _equipment.GetEquipped(armorSlots[i]);
                string display = string.IsNullOrEmpty(eq) ? "(empty)" : FormatItemName(eq);
                Color color = string.IsNullOrEmpty(eq) ? Color.gray : new Color(0.6f, 0.85f, 1f);
                AddRowLabel(content, $"  [{armorSlotNames[i]}]  {display}", row, color); row++;
            }

            // Stat bonuses
            var stats = _equipment.GetTotalStatBonuses();
            AddRowLabel(content, "", row, Color.white); row++;
            AddRowLabel(content, "  --- Stat Bonuses ---", row, new Color(0.95f, 0.85f, 0.5f)); row++;
            AddRowLabel(content, $"  VIG {stats.Vigor:+0;-0;0}  STR {stats.Strength:+0;-0;0}  HAR {stats.Harvest:+0;-0;0}", row, Color.white); row++;
            AddRowLabel(content, $"  VER {stats.Verdance:+0;-0;0}  AGI {stats.Agility:+0;-0;0}  RES {stats.Resilience:+0;-0;0}", row, Color.white); row++;

            AddRowLabel(content, "", row, Color.white); row++;

            // ---- Owned Weapons ----
            AddRowLabel(content, "  --- Owned Weapons ---", row, new Color(0.95f, 0.85f, 0.5f)); row++;

            // Sort owned weapons by rarity (ascending), then by type
            var sortedWeapons = new List<string>(_ownedWeapons);
            sortedWeapons.Sort((a, b) =>
            {
                var wa = _weaponDefs[a];
                var wb = _weaponDefs[b];
                int rarityOrder = WeaponRaritySortOrder(wa.Rarity).CompareTo(WeaponRaritySortOrder(wb.Rarity));
                if (rarityOrder != 0) return rarityOrder;
                return ((int)wa.WeaponType).CompareTo((int)wb.WeaponType);
            });

            foreach (var wId in sortedWeapons)
            {
                var w = _weaponDefs[wId];
                Color rc = WeaponRarityColor(w.Rarity);
                bool isEquipped = wId == _equippedWeaponId;
                string eqTag = isEquipped ? "  [EQUIPPED]" : "";
                string rarTag = WeaponRarityName(w.Rarity);

                AddRowLabel(content, $"  [{rarTag}] {w.WeaponName}  ({WeaponTypeName(w.WeaponType)})  DMG {w.BaseDamage:F0}  SPD {w.AttackSpeed:F1}{eqTag}", row, rc);

                if (!isEquipped)
                {
                    string capturedId = wId;
                    AddButton(content, "Equip", row, () =>
                    {
                        EquipWeapon(capturedId);
                        CloseActiveScreen();
                        ShowEquipment();
                    });
                }
                row++;

                // Show passive if present
                if (!string.IsNullOrEmpty(w.UniquePassive))
                {
                    AddRowLabel(content, $"    {w.UniquePassive}", row, new Color(0.7f, 0.65f, 0.45f)); row++;
                }
            }

            if (_ownedWeapons.Count == 0)
            {
                AddRowLabel(content, "  (no weapons owned)", row, Color.gray); row++;
            }

            SetContentHeight(content, row);
        }

        private void EquipWeapon(string weaponId)
        {
            if (!_weaponDefs.TryGetValue(weaponId, out var weapon)) return;
            _equippedWeaponId = weaponId;
            _equipment.Equip(weaponId, EquipmentSlot.Weapon);
            if (_sceneSetup != null)
                _sceneSetup.SetActiveWeapon(weapon);
            Debug.Log($"[Equipment] Equipped weapon: {weapon.WeaponName}");
        }

        private static string WeaponTypeName(WeaponType t) => t switch
        {
            WeaponType.Scythe => "Scythe",
            WeaponType.HoeBlade => "Hoe-Blade",
            WeaponType.SickleShield => "Sickle & Shield",
            WeaponType.PitchforkSpear => "Pitchfork Spear",
            WeaponType.SeedSling => "Seed Sling",
            WeaponType.DruidStaff => "Druid Staff",
            _ => t.ToString()
        };

        private static string WeaponRarityName(Rarity r) => r switch
        {
            Rarity.Common => "Common",
            Rarity.Uncommon => "Uncommon",
            Rarity.Rare => "Rare",
            Rarity.Epic => "Epic",
            Rarity.Legendary => "Legendary",
            _ => r.ToString()
        };

        private static Color WeaponRarityColor(Rarity r) => r switch
        {
            Rarity.Common => new Color(0.7f, 0.7f, 0.7f),       // White/gray
            Rarity.Uncommon => new Color(0.3f, 0.85f, 0.3f),    // Green
            Rarity.Rare => new Color(0.3f, 0.5f, 1f),           // Blue
            Rarity.Epic => new Color(0.7f, 0.3f, 0.9f),         // Purple
            Rarity.Legendary => new Color(1f, 0.6f, 0.1f),      // Orange
            _ => Color.white
        };

        private static int WeaponRaritySortOrder(Rarity r) => r switch
        {
            Rarity.Common => 0,
            Rarity.Uncommon => 1,
            Rarity.Rare => 2,
            Rarity.Epic => 3,
            Rarity.Legendary => 4,
            _ => 5
        };

        // ================================================================
        // SHOP SCREEN
        // ================================================================

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
                        if (_weaponDefs.ContainsKey(capturedRecipe.OutputItemId))
                            _ownedWeapons.Add(capturedRecipe.OutputItemId);
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
            if (_gachaSubScreen == 1) { ShowWishHistory(); return; }
            if (_gachaSubScreen == 2) { ShowRateDetails(); return; }

            if (_gachaBannerIndex >= _testBanners.Count) _gachaBannerIndex = 0;
            var banner = _testBanners[_gachaBannerIndex];

            var panel = CreateScreenPanel("Summoning");

            // ---- Banner Tabs (top bar) ----
            float tabWidth = 1f / _testBanners.Count;
            for (int i = 0; i < _testBanners.Count; i++)
            {
                int idx = i;
                bool active = (i == _gachaBannerIndex);
                string tabLabel = _testBanners[i].BannerName;
                if (!string.IsNullOrEmpty(_testBanners[i].FeaturedCompanionId))
                    tabLabel += "\u2605"; // star marker for featured

                var tabGo = new GameObject("Tab_" + i);
                tabGo.transform.SetParent(panel.transform, false);
                var tabRt = tabGo.AddComponent<RectTransform>();
                tabRt.anchorMin = new Vector2(i * tabWidth, 0.91f);
                tabRt.anchorMax = new Vector2((i + 1) * tabWidth, 0.96f);
                tabRt.offsetMin = new Vector2(2f, 0f);
                tabRt.offsetMax = new Vector2(-2f, 0f);

                var tabImg = tabGo.AddComponent<Image>();
                tabImg.color = active ? new Color(0.3f, 0.25f, 0.5f) : new Color(0.15f, 0.15f, 0.2f);

                var tabBtn = tabGo.AddComponent<Button>();
                tabBtn.targetGraphic = tabImg;
                tabBtn.onClick.AddListener(() =>
                {
                    _gachaBannerIndex = idx;
                    CloseActiveScreen(); ShowGacha();
                });

                var tabText = new GameObject("Text");
                tabText.transform.SetParent(tabGo.transform, false);
                var tt = tabText.AddComponent<Text>();
                tt.text = tabLabel;
                tt.font = _font;
                tt.fontSize = 13;
                tt.fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
                tt.color = active ? new Color(0.95f, 0.85f, 0.5f) : new Color(0.6f, 0.6f, 0.6f);
                tt.alignment = TextAnchor.MiddleCenter;
                var ttRt = tabText.GetComponent<RectTransform>();
                ttRt.anchorMin = Vector2.zero;
                ttRt.anchorMax = Vector2.one;
                ttRt.offsetMin = Vector2.zero;
                ttRt.offsetMax = Vector2.zero;
            }

            // ---- Banner Splash ----
            var splashArea = new GameObject("Splash");
            splashArea.transform.SetParent(panel.transform, false);
            var splashRt = splashArea.AddComponent<RectTransform>();
            splashRt.anchorMin = new Vector2(0f, 0.72f);
            splashRt.anchorMax = new Vector2(1f, 0.91f);
            splashRt.offsetMin = new Vector2(20f, 0f);
            splashRt.offsetMax = new Vector2(-20f, 0f);

            string bannerTitle = $"\u2726 {banner.BannerName.ToUpper()} \u2726";
            AddLabel(splashArea.transform, bannerTitle, 22, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, Vector2.zero, new Color(1f, 0.85f, 0.3f));

            string descLine = banner.Description;
            if (!string.IsNullOrEmpty(banner.FeaturedCompanionId) && CompanionRegistry.TryGetValue(banner.FeaturedCompanionId, out var featInfo))
                descLine = $"Featured: {featInfo.Name} (5\u2605 {featInfo.Class} / {featInfo.Element})";

            AddLabel(splashArea.transform, descLine, 16, TextAnchor.MiddleCenter,
                new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero,
                !string.IsNullOrEmpty(banner.FeaturedCompanionId) ? new Color(1f, 0.92f, 0.6f) : new Color(0.7f, 0.7f, 0.8f));

            // ---- Currency & Pity Display ----
            var pity = _gacha.GetPityTracker(banner.BannerId);
            int pityCount = pity.PullsSinceLegendary;
            int hardPity = banner.PityConfig != null ? banner.PityConfig.LegendaryHardPity : 60;
            float pityPct = Mathf.Clamp01((float)pityCount / hardPity);

            var infoArea = new GameObject("Info");
            infoArea.transform.SetParent(panel.transform, false);
            var infoRt = infoArea.AddComponent<RectTransform>();
            infoRt.anchorMin = new Vector2(0f, 0.58f);
            infoRt.anchorMax = new Vector2(1f, 0.72f);
            infoRt.offsetMin = new Vector2(20f, 0f);
            infoRt.offsetMax = new Vector2(-20f, 0f);

            string currencyLine = $"Starseeds: {_starseeds:N0}  \u2726          Accord Essence: {_accordEssence}";
            AddLabel(infoArea.transform, currencyLine, 16, TextAnchor.MiddleLeft,
                new Vector2(0f, 0.55f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(10f, 0f), Vector2.zero, new Color(0.6f, 0.9f, 1f));

            // Pity bar (text-based)
            int filledBlocks = Mathf.RoundToInt(pityPct * 20);
            string pityBar = new string('\u2593', filledBlocks) + new string('\u2591', 20 - filledBlocks);
            string fiftyFiftyStatus = !string.IsNullOrEmpty(banner.FeaturedCompanionId)
                ? (pity.LostLastFiftyFifty ? "Guaranteed" : "50/50")
                : "N/A";
            string pityLine = $"  {pityBar}  Pity: {pityCount}/{hardPity}    50/50: {fiftyFiftyStatus}    Total: {pity.TotalPulls}";
            Color pityColor = pityPct > 0.8f ? new Color(1f, 0.4f, 0.3f)
                            : pityPct > 0.5f ? new Color(1f, 0.85f, 0.3f)
                            : new Color(0.5f, 0.8f, 0.5f);
            AddLabel(infoArea.transform, pityLine, 14, TextAnchor.MiddleLeft,
                new Vector2(0f, 0f), new Vector2(1f, 0.55f), new Vector2(0f, 0.5f),
                Vector2.zero, Vector2.zero, pityColor);

            // ---- Pull Buttons ----
            bool canPull1 = CanAffordPull(1) && !_isGachaAnimating;
            bool canPull10 = CanAffordPull(10) && !_isGachaAnimating;

            string pull1Label = $"Pull x1 \u2014 {StarseedCostSingle} \u2726";
            string pull10Label = $"Pull x10 \u2014 {StarseedCost10Pull} \u2726";

            var btn1Go = new GameObject("Btn_Pull1");
            btn1Go.transform.SetParent(panel.transform, false);
            var btn1Rt = btn1Go.AddComponent<RectTransform>();
            btn1Rt.anchorMin = new Vector2(0.12f, 0.48f);
            btn1Rt.anchorMax = new Vector2(0.45f, 0.56f);
            btn1Rt.offsetMin = Vector2.zero;
            btn1Rt.offsetMax = Vector2.zero;
            var btn1Img = btn1Go.AddComponent<Image>();
            btn1Img.color = canPull1 ? new Color(0.25f, 0.4f, 0.6f) : new Color(0.2f, 0.2f, 0.25f);
            var btn1 = btn1Go.AddComponent<Button>();
            btn1.targetGraphic = btn1Img;
            btn1.interactable = canPull1;
            btn1.onClick.AddListener(() =>
            {
                if (_isGachaAnimating || !CanAffordPull(1)) return;
                SpendStarseeds(1);
                var result = _gacha.Pull(banner);
                RecordWishHistory(result, banner);
                LogPullResult(result);

                var wheelCompanions = BuildWheelCompanions(banner);
                int winIdx = FindCompanionIndex(wheelCompanions, result.CompanionId);
                _isGachaAnimating = true;
                _roulette = GachaRouletteUI.Create(_canvas, _font);
                _roulette.Spin(wheelCompanions, winIdx, () =>
                {
                    if (!result.IsDuplicate)
                        _unlockedCompanions.Add(result.CompanionId);
                    _roulette.Dismiss();
                    _roulette = null;
                    _isGachaAnimating = false;
                    CloseActiveScreen();
                    ShowGacha();
                });
            });
            AddTextChild(btn1Go.transform, pull1Label, 16, TextAnchor.MiddleCenter,
                canPull1 ? Color.white : new Color(0.5f, 0.5f, 0.5f));

            var btn10Go = new GameObject("Btn_Pull10");
            btn10Go.transform.SetParent(panel.transform, false);
            var btn10Rt = btn10Go.AddComponent<RectTransform>();
            btn10Rt.anchorMin = new Vector2(0.55f, 0.48f);
            btn10Rt.anchorMax = new Vector2(0.88f, 0.56f);
            btn10Rt.offsetMin = Vector2.zero;
            btn10Rt.offsetMax = Vector2.zero;
            var btn10Img = btn10Go.AddComponent<Image>();
            btn10Img.color = canPull10 ? new Color(0.35f, 0.3f, 0.55f) : new Color(0.2f, 0.2f, 0.25f);
            var btn10Btn = btn10Go.AddComponent<Button>();
            btn10Btn.targetGraphic = btn10Img;
            btn10Btn.interactable = canPull10;
            btn10Btn.onClick.AddListener(() =>
            {
                if (_isGachaAnimating || !CanAffordPull(10)) return;
                SpendStarseeds(10);
                var results = _gacha.Pull10(banner);

                // Find best result to feature in roulette
                var featured = results[0];
                foreach (var r in results)
                {
                    if (r.Rarity > featured.Rarity) featured = r;
                }

                foreach (var r in results)
                {
                    RecordWishHistory(r, banner);
                    LogPullResult(r);
                    if (!r.IsDuplicate)
                        _unlockedCompanions.Add(r.CompanionId);
                }

                var wheelCompanions = BuildWheelCompanions(banner);
                int winIdx = FindCompanionIndex(wheelCompanions, featured.CompanionId);
                _isGachaAnimating = true;
                _roulette = GachaRouletteUI.Create(_canvas, _font);
                _roulette.Spin(wheelCompanions, winIdx, () =>
                {
                    _roulette.Dismiss();
                    _roulette = null;
                    _isGachaAnimating = false;
                    CloseActiveScreen();
                    ShowGacha();
                });
            });
            AddTextChild(btn10Go.transform, pull10Label, 16, TextAnchor.MiddleCenter,
                canPull10 ? Color.white : new Color(0.5f, 0.5f, 0.5f));

            // ---- Sub-screen Buttons ----
            AddButtonAt(panel.transform, "Wish History", new Vector2(0.25f, 0.42f), new Vector2(160f, 32f), () =>
            {
                _gachaSubScreen = 1;
                CloseActiveScreen(); ShowGacha();
            });
            AddButtonAt(panel.transform, "Rate Details", new Vector2(0.75f, 0.42f), new Vector2(160f, 32f), () =>
            {
                _gachaSubScreen = 2;
                CloseActiveScreen(); ShowGacha();
            });

            // ---- Recent Summons Log ----
            var logHeader = new GameObject("LogHeader");
            logHeader.transform.SetParent(panel.transform, false);
            var logHeaderRt = logHeader.AddComponent<RectTransform>();
            logHeaderRt.anchorMin = new Vector2(0f, 0.35f);
            logHeaderRt.anchorMax = new Vector2(1f, 0.40f);
            logHeaderRt.offsetMin = new Vector2(20f, 0f);
            logHeaderRt.offsetMax = new Vector2(-20f, 0f);
            AddTextChild(logHeader.transform, "Recent Summons:", 14, TextAnchor.MiddleLeft, new Color(0.7f, 0.7f, 0.8f));

            var logContent = CreateScrollContent(panel.transform, new Vector2(0f, 0f), new Vector2(1f, 0.35f));
            for (int i = 0; i < _gachaLog.Count; i++)
            {
                Color c;
                if (_gachaLog[i].StartsWith("★★★★★"))
                    c = new Color(1f, 0.85f, 0.3f);   // 5★ gold
                else if (_gachaLog[i].StartsWith("★★★★"))
                    c = new Color(0.7f, 0.3f, 0.9f);   // 4★ purple
                else
                    c = new Color(0.4f, 0.65f, 1f);    // 3★ blue

                AddRowLabel(logContent, "  " + _gachaLog[i], i, c);
            }
            SetContentHeight(logContent, _gachaLog.Count);
        }

        // ================================================================
        // WISH HISTORY SUB-SCREEN
        // ================================================================

        private void ShowWishHistory()
        {
            var panel = CreateScreenPanel("Wish History");

            var content = CreateScrollContent(panel.transform, new Vector2(0f, 0f), new Vector2(1f, 0.88f));

            // Header row
            AddRowLabel(content, "  #     Stars           Companion                    Banner              Status", 0, new Color(0.7f, 0.7f, 0.8f));

            int row = 1;
            for (int i = 0; i < _wishHistory.Count; i++)
            {
                var (companionId, rarity, isDuplicate, bannerName, pullNumber) = _wishHistory[i];
                string stars = RarityStars(rarity);
                string name = GetCompanionDisplayName(companionId);
                string status = isDuplicate ? "(DUP)" : "NEW!";

                Color c = rarity switch
                {
                    Rarity.Legendary => new Color(1f, 0.85f, 0.3f),
                    Rarity.Rare => new Color(0.7f, 0.3f, 0.9f),
                    _ => new Color(0.4f, 0.65f, 1f)
                };

                string line = $"  {pullNumber,-5} {stars,-16} {name,-28} {bannerName,-20} {status}";
                AddRowLabel(content, line, row, c);
                row++;
            }

            if (_wishHistory.Count == 0)
            {
                AddRowLabel(content, "  No summons yet — try your luck!", 1, new Color(0.5f, 0.5f, 0.5f));
                row = 2;
            }

            SetContentHeight(content, row);

            // Back button
            AddButtonAt(panel.transform, "Back", new Vector2(0.5f, 0.94f), new Vector2(100f, 32f), () =>
            {
                _gachaSubScreen = 0;
                CloseActiveScreen(); ShowGacha();
            });
        }

        // ================================================================
        // RATE DETAILS SUB-SCREEN
        // ================================================================

        private void ShowRateDetails()
        {
            if (_gachaBannerIndex >= _testBanners.Count) _gachaBannerIndex = 0;
            var banner = _testBanners[_gachaBannerIndex];

            var panel = CreateScreenPanel("Rate Details — " + banner.BannerName);
            var content = CreateScrollContent(panel.transform, new Vector2(0f, 0f), new Vector2(1f, 0.88f));

            int row = 0;
            Color headerColor = new Color(0.95f, 0.85f, 0.5f);
            Color textColor = new Color(0.85f, 0.85f, 0.9f);
            Color dimColor = new Color(0.6f, 0.6f, 0.65f);

            // Base rates
            AddRowLabel(content, "  --- Base Rates ---", row, headerColor); row++;
            AddRowLabel(content, $"  5★ Legendary:  {banner.LegendaryRate * 100:F1}%", row, new Color(1f, 0.85f, 0.3f)); row++;
            AddRowLabel(content, $"  4★ Epic:       {banner.RareRate * 100:F1}%", row, new Color(0.7f, 0.3f, 0.9f)); row++;
            AddRowLabel(content, $"  3★ Rare:       {banner.CommonRate * 100:F1}%", row, new Color(0.4f, 0.65f, 1f)); row++;
            AddRowLabel(content, "", row, Color.white); row++;

            // Pity system
            AddRowLabel(content, "  --- Pity System ---", row, headerColor); row++;
            int softPity = banner.PityConfig != null ? banner.PityConfig.LegendarySoftPity : 50;
            int hardPity = banner.PityConfig != null ? banner.PityConfig.LegendaryHardPity : 60;
            float boost = banner.PityConfig != null ? banner.PityConfig.SoftPityRateBoost : 0.06f;
            AddRowLabel(content, $"  Soft pity begins at pull {softPity} (+{boost * 100:F0}% per pull)", row, textColor); row++;
            AddRowLabel(content, $"  Hard pity at pull {hardPity} (guaranteed 5★)", row, textColor); row++;
            AddRowLabel(content, "  10-pull guarantees at least one 4★ or higher", row, textColor); row++;
            AddRowLabel(content, "", row, Color.white); row++;

            // 50/50 rules
            if (!string.IsNullOrEmpty(banner.FeaturedCompanionId))
            {
                AddRowLabel(content, "  --- 50/50 Featured System ---", row, headerColor); row++;
                AddRowLabel(content, "  When you pull a 5★, there is a 50% chance it is the featured companion.", row, textColor); row++;
                AddRowLabel(content, "  If you lose the 50/50, your next 5★ is guaranteed to be the featured.", row, textColor); row++;
                AddRowLabel(content, "", row, Color.white); row++;
            }

            // Pool contents
            AddRowLabel(content, "  --- 5★ Pool ---", row, headerColor); row++;
            foreach (var id in banner.LegendaryPool)
            {
                string tag = id == banner.FeaturedCompanionId ? "  [RATE UP]" : "";
                Color c = id == banner.FeaturedCompanionId ? new Color(1f, 0.85f, 0.3f) : textColor;
                string cInfo = CompanionRegistry.TryGetValue(id, out var info)
                    ? $"  {info.Name} ({info.Class} / {info.Element}){tag}"
                    : $"  {FormatItemName(id)}{tag}";
                AddRowLabel(content, cInfo, row, c); row++;
            }
            AddRowLabel(content, "", row, Color.white); row++;

            AddRowLabel(content, "  --- 4★ Pool ---", row, headerColor); row++;
            foreach (var id in banner.RarePool)
            {
                string cInfo = CompanionRegistry.TryGetValue(id, out var info)
                    ? $"  {info.Name} ({info.Class} / {info.Element})"
                    : $"  {FormatItemName(id)}";
                AddRowLabel(content, cInfo, row, new Color(0.7f, 0.3f, 0.9f)); row++;
            }
            AddRowLabel(content, "", row, Color.white); row++;

            AddRowLabel(content, "  --- 3★ Pool ---", row, headerColor); row++;
            foreach (var id in banner.CommonPool)
            {
                string cInfo = CompanionRegistry.TryGetValue(id, out var info)
                    ? $"  {info.Name} ({info.Class} / {info.Element})"
                    : $"  {FormatItemName(id)}";
                AddRowLabel(content, cInfo, row, dimColor); row++;
            }

            SetContentHeight(content, row);

            // Back button
            AddButtonAt(panel.transform, "Back", new Vector2(0.5f, 0.94f), new Vector2(100f, 32f), () =>
            {
                _gachaSubScreen = 0;
                CloseActiveScreen(); ShowGacha();
            });
        }

        // ================================================================
        // COMPANIONS SCREEN
        // ================================================================

        private void ShowCompanions()
        {
            var panel = CreateScreenPanel("Companion Roster");

            // Build sorted companion list from registry (5★ first, then 4★, then 3★)
            var sortedCompanions = new List<(string id, string name, string cls, string element, Rarity rarity)>();
            foreach (var kvp in CompanionRegistry)
            {
                Rarity r = GetCompanionRarity(kvp.Key);
                sortedCompanions.Add((kvp.Key, kvp.Value.Name, kvp.Value.Class, kvp.Value.Element, r));
            }
            sortedCompanions.Sort((a, b) =>
            {
                int ra = a.rarity == Rarity.Legendary ? 0 : a.rarity == Rarity.Rare ? 1 : 2;
                int rb = b.rarity == Rarity.Legendary ? 0 : b.rarity == Rarity.Rare ? 1 : 2;
                return ra != rb ? ra.CompareTo(rb) : string.Compare(a.name, b.name, System.StringComparison.Ordinal);
            });

            // Helper to look up display info for a companion id
            string CompanionLine(string id)
            {
                if (!CompanionRegistry.TryGetValue(id, out var info)) return id;
                Rarity r = GetCompanionRarity(id);
                int lvl = GetCompanionLevel(id);
                return $"{RarityStars(r)} {info.Name}   {info.Element}  {info.Class}  Lv {lvl}";
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
                    if (_sceneSetup != null) _sceneSetup.SetPartyCompanion("Active", null, 1);
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
                    if (_sceneSetup != null) _sceneSetup.SetPartyCompanion("Support", null, 1);
                    CloseActiveScreen(); ShowCompanions();
                });

            // Clear Party button
            AddButton(headerContent, "Clear Party", 2, () => {
                _partyActiveId = null; _partySupportId = null;
                if (_sceneSetup != null)
                {
                    _sceneSetup.SetPartyCompanion("Active", null, 1);
                    _sceneSetup.SetPartyCompanion("Support", null, 1);
                }
                CloseActiveScreen(); ShowCompanions();
            });

            SetContentHeight(headerContent, 3);

            // --- B) Companion list (scrollable, below header) ---
            var content = CreateScrollContent(panel.transform, new Vector2(0f, 0f), new Vector2(1f, 0.77f));

            int row = 0;
            foreach (var (cId, cName, cls, element, rarity) in sortedCompanions)
            {
                bool owned = IsCompanionOwned(cId);
                string star = RarityStars(rarity);
                int lvl = GetCompanionLevel(cId);
                Color color = owned ? RarityToColor(rarity) : new Color(0.3f, 0.3f, 0.3f);

                if (!owned)
                {
                    string line = $"  {star} {cName}   {element}  {cls}  [LOCKED]";
                    AddRowLabel(content, line, row, color);
                }
                else if (cId == _partyActiveId)
                {
                    string line = $"  {star} {cName}   {element}  {cls}  Lv {lvl}  [ACTIVE]";
                    AddRowLabel(content, line, row, color);
                    string capturedId = cId;
                    AddUpgradeButton(content, row, capturedId);
                }
                else if (cId == _partySupportId)
                {
                    string line = $"  {star} {cName}   {element}  {cls}  Lv {lvl}  [SUPPORT]";
                    AddRowLabel(content, line, row, color);
                    string capturedId = cId;
                    AddUpgradeButton(content, row, capturedId);
                }
                else
                {
                    string line = $"  {star} {cName}   {element}  {cls}  Lv {lvl}";
                    AddRowLabel(content, line, row, color);
                    string capturedId = cId;
                    AddUpgradeButton(content, row, capturedId);
                    AddDualButtons(content, row,
                        "Active", () =>
                        {
                            if (_partySupportId == capturedId) _partySupportId = null;
                            _partyActiveId = capturedId;
                            if (_sceneSetup != null)
                            {
                                _sceneSetup.SetPartyCompanion("Support", _partySupportId, GetCompanionLevel(_partySupportId));
                                _sceneSetup.SetPartyCompanion("Active", _partyActiveId, GetCompanionLevel(_partyActiveId));
                            }
                            CloseActiveScreen(); ShowCompanions();
                        },
                        "Support", () =>
                        {
                            if (_partyActiveId == capturedId) _partyActiveId = null;
                            _partySupportId = capturedId;
                            if (_sceneSetup != null)
                            {
                                _sceneSetup.SetPartyCompanion("Active", _partyActiveId, GetCompanionLevel(_partyActiveId));
                                _sceneSetup.SetPartyCompanion("Support", _partySupportId, GetCompanionLevel(_partySupportId));
                            }
                            CloseActiveScreen(); ShowCompanions();
                        });
                }
                row++;
            }

            SetContentHeight(content, row);
        }

        private void AddUpgradeButton(Transform content, int row, string companionId)
        {
            // Place upgrade button to the left of the dual buttons area
            var go = new GameObject("Btn_Upgrade");
            go.transform.SetParent(content, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-15f - 70f - 4f - 70f - 4f, -row * 28f);
            rt.sizeDelta = new Vector2(70f, 26f);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.45f, 0.3f, 0.15f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            string capturedId = companionId;
            btn.onClick.AddListener(() => { CloseActiveScreen(); ShowCompanionUpgrade(capturedId); });

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var t = textGo.AddComponent<Text>();
            t.text = "Upgrade";
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

        // ================================================================
        // COMPANION UPGRADE SCREEN
        // ================================================================

        private void ShowCompanionUpgrade(string companionId)
        {
            string displayName = GetCompanionDisplayName(companionId);
            Rarity rarity = GetCompanionRarity(companionId);
            string element = CompanionRegistry.TryGetValue(companionId, out var info) ? info.Element : "None";
            string cls = CompanionRegistry.ContainsKey(companionId) ? CompanionRegistry[companionId].Class : "Unknown";
            string stars = RarityStars(rarity);

            int currentLevel = GetCompanionLevel(companionId);
            bool isMaxLevel = currentLevel >= 45;

            var panel = CreateScreenPanel($"Upgrade: {displayName}");
            var content = CreateScrollContent(panel.transform, new Vector2(0f, 0f), new Vector2(1f, 0.9f));

            int row = 0;

            // Row 1: Name / Rarity / Element / Class
            AddRowLabel(content, $"  {stars} {displayName}   {element}  {cls}", row, RarityToColor(rarity));
            row++;

            // Row 2: Current Level
            AddRowLabel(content, $"  Level: {currentLevel} / 45", row, Color.white);
            row++;

            // Row 3: Player Level
            if (!isMaxLevel)
            {
                int targetLevel = currentLevel + 1;
                string plvlNote = _playerLevel >= targetLevel ? "" : $"  (need Lv {targetLevel} for next)";
                AddRowLabel(content, $"  Player Level: {_playerLevel}{plvlNote}", row,
                    _playerLevel >= targetLevel ? Color.white : new Color(1f, 0.4f, 0.4f));
            }
            else
            {
                AddRowLabel(content, $"  Player Level: {_playerLevel}  (MAX LEVEL)", row, new Color(0.4f, 1f, 0.4f));
            }
            row++;

            AddRowLabel(content, "", row, Color.white); row++;

            if (!isMaxLevel)
            {
                int nextLevel = currentLevel + 1;

                // Stats at current → next
                float curDmg = TestSceneSetup.GetScaledDamage(companionId, currentLevel);
                float nextDmg = TestSceneSetup.GetScaledDamage(companionId, nextLevel);
                float curCd = TestSceneSetup.GetScaledCooldown(currentLevel);
                float nextCd = TestSceneSetup.GetScaledCooldown(nextLevel);
                float curSpd = TestSceneSetup.GetScaledMoveSpeed(currentLevel);
                float nextSpd = TestSceneSetup.GetScaledMoveSpeed(nextLevel);

                AddRowLabel(content, $"  --- Stats at Level {currentLevel} -> {nextLevel} ---", row,
                    new Color(0.95f, 0.85f, 0.5f));
                row++;
                AddRowLabel(content, $"  Damage:   {curDmg:F1} -> {nextDmg:F1}", row, Color.white); row++;
                AddRowLabel(content, $"  Cooldown: {curCd:F2}s -> {nextCd:F2}s", row, Color.white); row++;
                AddRowLabel(content, $"  Speed:    {curSpd:F2} -> {nextSpd:F2}", row, Color.white); row++;

                // Stat block comparison
                var curStats = TestSceneSetup.GetCompanionStatBlock(companionId, currentLevel);
                var nextStats = TestSceneSetup.GetCompanionStatBlock(companionId, nextLevel);
                AddRowLabel(content, $"  VIG {curStats.Vigor:F0} -> {nextStats.Vigor:F0}  STR {curStats.Strength:F0} -> {nextStats.Strength:F0}  HAR {curStats.Harvest:F0} -> {nextStats.Harvest:F0}", row, new Color(0.8f, 0.8f, 0.8f)); row++;
                AddRowLabel(content, $"  VER {curStats.Verdance:F0} -> {nextStats.Verdance:F0}  AGI {curStats.Agility:F0} -> {nextStats.Agility:F0}  RES {curStats.Resilience:F0} -> {nextStats.Resilience:F0}", row, new Color(0.8f, 0.8f, 0.8f)); row++;

                AddRowLabel(content, "", row, Color.white); row++;

                // Cost
                var cost = GetUpgradeCost(currentLevel, rarity);
                AddRowLabel(content, "  --- Cost ---", row, new Color(0.95f, 0.85f, 0.5f)); row++;

                bool goldOk = _gold >= cost.Gold;
                AddRowLabel(content, $"  Gold: {cost.Gold}  (have: {_gold}) {(goldOk ? "OK" : "X")}",
                    row, goldOk ? Color.white : new Color(1f, 0.4f, 0.4f)); row++;

                bool essOk = _accordEssence >= cost.Essence;
                AddRowLabel(content, $"  Essence: {cost.Essence}  (have: {_accordEssence}) {(essOk ? "OK" : "X")}",
                    row, essOk ? Color.white : new Color(1f, 0.4f, 0.4f)); row++;

                int matOwned = _inventory.GetItemCount(cost.MaterialId);
                bool matOk = matOwned >= cost.MaterialCount;
                AddRowLabel(content, $"  {cost.MaterialId} x{cost.MaterialCount}  (have: {matOwned}) {(matOk ? "OK" : "X")}",
                    row, matOk ? Color.white : new Color(1f, 0.4f, 0.4f)); row++;

                AddRowLabel(content, "", row, Color.white); row++;

                // Level Up button
                bool canUpgrade = goldOk && essOk && matOk && _playerLevel >= nextLevel;
                string capturedId = companionId;

                var lvlUpGo = new GameObject("Btn_LevelUp");
                lvlUpGo.transform.SetParent(content, false);
                var lvlRt = lvlUpGo.AddComponent<RectTransform>();
                lvlRt.anchorMin = new Vector2(0f, 1f);
                lvlRt.anchorMax = new Vector2(0f, 1f);
                lvlRt.pivot = new Vector2(0f, 1f);
                lvlRt.anchoredPosition = new Vector2(20f, -row * 28f);
                lvlRt.sizeDelta = new Vector2(100f, 28f);
                var lvlImg = lvlUpGo.AddComponent<Image>();
                lvlImg.color = canUpgrade ? new Color(0.2f, 0.5f, 0.2f) : new Color(0.3f, 0.3f, 0.3f);
                var lvlBtn = lvlUpGo.AddComponent<Button>();
                lvlBtn.targetGraphic = lvlImg;
                lvlBtn.interactable = canUpgrade;
                lvlBtn.onClick.AddListener(() =>
                {
                    DoUpgrade(capturedId);
                    CloseActiveScreen();
                    ShowCompanionUpgrade(capturedId);
                });
                var lvlTextGo = new GameObject("Text");
                lvlTextGo.transform.SetParent(lvlUpGo.transform, false);
                var lvlT = lvlTextGo.AddComponent<Text>();
                lvlT.text = "Level Up";
                lvlT.font = _font;
                lvlT.fontSize = 14;
                lvlT.color = Color.white;
                lvlT.alignment = TextAnchor.MiddleCenter;
                var lvlTRt = lvlTextGo.GetComponent<RectTransform>();
                lvlTRt.anchorMin = Vector2.zero;
                lvlTRt.anchorMax = Vector2.one;
                lvlTRt.offsetMin = Vector2.zero;
                lvlTRt.offsetMax = Vector2.zero;

                // Max Level button
                var maxGo = new GameObject("Btn_MaxLevel");
                maxGo.transform.SetParent(content, false);
                var maxRt = maxGo.AddComponent<RectTransform>();
                maxRt.anchorMin = new Vector2(0f, 1f);
                maxRt.anchorMax = new Vector2(0f, 1f);
                maxRt.pivot = new Vector2(0f, 1f);
                maxRt.anchoredPosition = new Vector2(130f, -row * 28f);
                maxRt.sizeDelta = new Vector2(100f, 28f);
                var maxImg = maxGo.AddComponent<Image>();
                maxImg.color = canUpgrade ? new Color(0.45f, 0.3f, 0.15f) : new Color(0.3f, 0.3f, 0.3f);
                var maxBtn = maxGo.AddComponent<Button>();
                maxBtn.targetGraphic = maxImg;
                maxBtn.interactable = canUpgrade;
                maxBtn.onClick.AddListener(() =>
                {
                    // Level up as many times as affordable
                    for (int loop = 0; loop < 44; loop++)
                    {
                        int cl = GetCompanionLevel(capturedId);
                        if (cl >= 45) break;
                        int tl = cl + 1;
                        if (_playerLevel < tl) break;
                        var c = GetUpgradeCost(cl, GetCompanionRarity(capturedId));
                        if (_gold < c.Gold || _accordEssence < c.Essence || !_inventory.HasItem(c.MaterialId, c.MaterialCount)) break;
                        DoUpgrade(capturedId);
                    }
                    CloseActiveScreen();
                    ShowCompanionUpgrade(capturedId);
                });
                var maxTextGo = new GameObject("Text");
                maxTextGo.transform.SetParent(maxGo.transform, false);
                var maxT = maxTextGo.AddComponent<Text>();
                maxT.text = "Max Level";
                maxT.font = _font;
                maxT.fontSize = 14;
                maxT.color = Color.white;
                maxT.alignment = TextAnchor.MiddleCenter;
                var maxTRt = maxTextGo.GetComponent<RectTransform>();
                maxTRt.anchorMin = Vector2.zero;
                maxTRt.anchorMax = Vector2.one;
                maxTRt.offsetMin = Vector2.zero;
                maxTRt.offsetMax = Vector2.zero;

                row++;

                // Status text
                if (!canUpgrade)
                {
                    string reason = _playerLevel < nextLevel ? $"Need player level {nextLevel}"
                        : !goldOk ? "Not enough gold"
                        : !essOk ? "Not enough essence"
                        : "Not enough materials";
                    AddRowLabel(content, $"  {reason}", row, new Color(1f, 0.4f, 0.4f));
                    row++;
                }
            }
            else
            {
                AddRowLabel(content, "  --- MAX LEVEL REACHED ---", row, new Color(0.4f, 1f, 0.4f)); row++;

                // Show current stats
                float curDmg = TestSceneSetup.GetScaledDamage(companionId, 45);
                float curCd = TestSceneSetup.GetScaledCooldown(45);
                float curSpd = TestSceneSetup.GetScaledMoveSpeed(45);
                AddRowLabel(content, $"  Damage: {curDmg:F1}   Cooldown: {curCd:F2}s   Speed: {curSpd:F2}", row, Color.white); row++;

                var stats = TestSceneSetup.GetCompanionStatBlock(companionId, 45);
                AddRowLabel(content, $"  VIG {stats.Vigor:F0}  STR {stats.Strength:F0}  HAR {stats.Harvest:F0}  VER {stats.Verdance:F0}  AGI {stats.Agility:F0}  RES {stats.Resilience:F0}", row, new Color(0.8f, 0.8f, 0.8f)); row++;
            }

            AddRowLabel(content, "", row, Color.white); row++;

            // Party Buff info
            if (rarity == Rarity.Mythic)
                AddRowLabel(content, "  Party Buff: +10% Player Damage (Mythic)", row, new Color(1f, 0.5f, 1f));
            else if (rarity == Rarity.Legendary)
                AddRowLabel(content, "  Party Buff: -0.1s Cooldown Reduction (Legendary)", row, new Color(1f, 0.85f, 0.3f));
            else
                AddRowLabel(content, "  Party Buff: (none — Rare/Common)", row, new Color(0.5f, 0.5f, 0.5f));
            row++;

            AddRowLabel(content, "", row, Color.white); row++;

            // Back button
            AddButton(content, "Back", row, () => { CloseActiveScreen(); ShowCompanions(); });
            row++;

            SetContentHeight(content, row);
        }

        // ================================================================
        // MAP SCREEN
        // ================================================================

        private string _selectedMapRegion;

        private void ShowMap()
        {
            var panel = CreateScreenPanel("World Map");

            // Biome data matching TestSceneSetup.InitBiomeZones
            var biomes = new[]
            {
                (name: "Greenreach Valley",  cx:   0f, cz:   0f, half: 35f, color: new Color(0.35f, 0.55f, 0.3f),  blight: 0.05f, elem: "Verdant", lvMin: 1,  lvMax: 10, hub: "Thornwall"),
                (name: "The Ashen Steppe",   cx: -55f, cz: -55f, half: 30f, color: new Color(0.55f, 0.35f, 0.2f),  blight: 0.25f, elem: "Pyro",    lvMin: 10, lvMax: 18, hub: "Dusthaven Outpost"),
                (name: "Gloomtide Marshes",  cx:  55f, cz: -55f, half: 30f, color: new Color(0.2f, 0.35f, 0.25f),  blight: 0.40f, elem: "Umbral",  lvMin: 16, lvMax: 24, hub: "Fenwick Village"),
                (name: "Frosthollow Peaks",  cx: -55f, cz:  55f, half: 30f, color: new Color(0.6f, 0.7f, 0.8f),   blight: 0.15f, elem: "Cryo",    lvMin: 22, lvMax: 32, hub: "Ironpeak Fortress"),
                (name: "The Withered Heart", cx:  55f, cz:  55f, half: 30f, color: new Color(0.3f, 0.15f, 0.2f),   blight: 0.80f, elem: "None",    lvMin: 30, lvMax: 40, hub: "Varek's Sanctum"),
            };

            // Detail data for info panel
            var regionEnemies = new Dictionary<string, (string name, int level, string tier)[]>
            {
                ["Greenreach Valley"] = new[] { ("Withered Wolf", 2, "T1"), ("Blight Beetle", 3, "T1"), ("Corrupted Farmhand", 5, "T2"), ("Wither Stag", 8, "Elite") },
                ["The Ashen Steppe"]  = new[] { ("Dustcrawler", 11, "T1"), ("Scorched Viper", 13, "T1"), ("Acolyte Ranger", 15, "T2"), ("Ashwalker Golem", 17, "Elite") },
                ["Gloomtide Marshes"] = new[] { ("Bogfiend", 17, "T1"), ("Sporecap Horror", 19, "T1"), ("Drowned Sentinel", 21, "T2"), ("The Mire Queen", 23, "Elite") },
                ["Frosthollow Peaks"] = new[] { ("Frostwight", 23, "T1"), ("Glacial Construct", 26, "T1"), ("Acolyte Warder", 28, "T2"), ("Avalanche Beast", 31, "Elite") },
                ["The Withered Heart"] = new[] { ("Hollow Shade", 32, "T1"), ("Rootwraith", 35, "T1"), ("Wither Knight", 37, "T2"), ("Blight Colossus", 39, "Elite") },
            };
            var regionBosses = new Dictionary<string, (string name, int level, bool optional)[]>
            {
                ["Greenreach Valley"] = new[] { ("The Rootmother", 10, false), ("The Scarecrow King", 12, true) },
                ["The Ashen Steppe"]  = new[] { ("Cindermaw", 18, false), ("Oasis Phantom", 20, true) },
                ["Gloomtide Marshes"] = new[] { ("The Mire Sovereign", 24, false), ("Grandmother Spore", 26, true) },
                ["Frosthollow Peaks"] = new[] { ("Frostfang, the Bound", 32, false), ("The Frozen Accord", 34, true) },
                ["The Withered Heart"] = new[] { ("Varek Ashwood", 38, false), ("The Hollow Mother", 40, true), ("Primordial Seed", 40, true) },
            };
            var regionNpcs = new Dictionary<string, (string name, string role)[]>
            {
                ["Greenreach Valley"] = new[] { ("Elder Mirren", "Village Elder"), ("Healer Maren", "Herbalist"), ("Warden Sable", "Forest Warden") },
                ["The Ashen Steppe"]  = new[] { ("Forgemaster Bram", "Crafting Master"), ("Watcher Sera", "Scout"), ("Lyra", "Companion") },
                ["Gloomtide Marshes"] = new[] { ("Oracle Nyx", "Marsh Seer"), ("Ranger Theron", "Guide"), ("Selene", "Companion") },
                ["Frosthollow Peaks"] = new[] { ("Scholar Veylin", "Researcher"), ("Sentinel Kaelos", "Guardian"), ("Thorne", "Companion") },
                ["The Withered Heart"] = new[] { ("The Blightcaller", "Antagonist"), ("Eldara", "Companion"), ("Echo of Mirren", "Guide") },
            };

            // ---- LAYOUT: Square map (left), info panel (right) ----
            float worldMin = -100f;
            float worldSize = 200f;

            // Helper: world coords to 0..1 normalized map position
            System.Func<float, float, Vector2> worldToMap = (wx, wz) =>
                new Vector2((wx - worldMin) / worldSize, (wz - worldMin) / worldSize);

            // == MAP CONTAINER (force square via AspectRatioFitter) ==
            var mapContainer = new GameObject("MapContainer");
            mapContainer.transform.SetParent(panel.transform, false);
            var mcRt = mapContainer.AddComponent<RectTransform>();
            mcRt.anchorMin = new Vector2(0f, 0f);
            mcRt.anchorMax = new Vector2(0.6f, 0.93f);
            mcRt.offsetMin = new Vector2(8f, 8f);
            mcRt.offsetMax = new Vector2(-4f, -4f);
            var arf = mapContainer.AddComponent<UnityEngine.UI.AspectRatioFitter>();
            arf.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            arf.aspectRatio = 1f;

            // Map background (dark wilderness)
            var mapBg = new GameObject("MapBg");
            mapBg.transform.SetParent(mapContainer.transform, false);
            var bgRt = mapBg.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            mapBg.AddComponent<Image>().color = new Color(0.15f, 0.12f, 0.08f);

            // Decorative border around map
            var mapBorder = new GameObject("MapBorder");
            mapBorder.transform.SetParent(mapContainer.transform, false);
            var mbRt = mapBorder.AddComponent<RectTransform>();
            mbRt.anchorMin = Vector2.zero;
            mbRt.anchorMax = Vector2.one;
            mbRt.offsetMin = new Vector2(-2f, -2f);
            mbRt.offsetMax = new Vector2(2f, 2f);
            var mbImg = mapBorder.AddComponent<Image>();
            mbImg.color = new Color(0.35f, 0.28f, 0.18f);
            mapBorder.transform.SetAsFirstSibling();

            // ---- Draw path lines connecting adjacent regions ----
            // Paths: Greenreach connects to all 4 outer zones; diagonal pairs
            var paths = new[]
            {
                (0f, 0f, -55f, -55f),   // Greenreach -> Ashen
                (0f, 0f, 55f, -55f),    // Greenreach -> Gloomtide
                (0f, 0f, -55f, 55f),    // Greenreach -> Frosthollow
                (0f, 0f, 55f, 55f),     // Greenreach -> Withered
                (-55f, -55f, -55f, 55f), // Ashen -> Frosthollow (west edge)
                (55f, -55f, 55f, 55f),   // Gloomtide -> Withered (east edge)
            };
            foreach (var (x1, z1, x2, z2) in paths)
            {
                DrawMapPath(mapContainer.transform, worldToMap(x1, z1), worldToMap(x2, z2),
                    new Color(0.35f, 0.3f, 0.2f, 0.5f));
            }

            // ---- Draw biome zones ----
            foreach (var b in biomes)
            {
                Vector2 bl = worldToMap(b.cx - b.half, b.cz - b.half);
                Vector2 tr = worldToMap(b.cx + b.half, b.cz + b.half);
                Color tinted = Color.Lerp(b.color, new Color(0.15f, 0.05f, 0.1f), b.blight * 0.5f);
                bool isSelected = _selectedMapRegion == b.name;

                // Outline (always visible, brighter if selected)
                var outlineGo = new GameObject("Outline_" + b.name);
                outlineGo.transform.SetParent(mapContainer.transform, false);
                var olRt = outlineGo.AddComponent<RectTransform>();
                olRt.anchorMin = bl;
                olRt.anchorMax = tr;
                olRt.offsetMin = new Vector2(-2f, -2f);
                olRt.offsetMax = new Vector2(2f, 2f);
                outlineGo.AddComponent<Image>().color = isSelected
                    ? new Color(1f, 0.85f, 0.3f, 0.9f)
                    : new Color(0.3f, 0.25f, 0.18f, 0.6f);

                // Zone fill
                var zoneGo = new GameObject("Zone_" + b.name);
                zoneGo.transform.SetParent(mapContainer.transform, false);
                var zoneRt = zoneGo.AddComponent<RectTransform>();
                zoneRt.anchorMin = bl;
                zoneRt.anchorMax = tr;
                zoneRt.offsetMin = Vector2.zero;
                zoneRt.offsetMax = Vector2.zero;
                var zoneImg = zoneGo.AddComponent<Image>();
                zoneImg.color = tinted;

                // Click handler
                var zoneBtn = zoneGo.AddComponent<Button>();
                zoneBtn.targetGraphic = zoneImg;
                string capName = b.name;
                zoneBtn.onClick.AddListener(() =>
                {
                    _selectedMapRegion = capName;
                    CloseActiveScreen();
                    ShowMap();
                });

                // Inner accent — lighter center stripe to give terrain texture
                var accentGo = new GameObject("Accent");
                accentGo.transform.SetParent(zoneGo.transform, false);
                var acRt = accentGo.AddComponent<RectTransform>();
                acRt.anchorMin = new Vector2(0.15f, 0.15f);
                acRt.anchorMax = new Vector2(0.85f, 0.85f);
                acRt.offsetMin = Vector2.zero;
                acRt.offsetMax = Vector2.zero;
                var acImg = accentGo.AddComponent<Image>();
                acImg.color = new Color(tinted.r + 0.06f, tinted.g + 0.06f, tinted.b + 0.06f, 0.4f);
                acImg.raycastTarget = false;

                // Hub marker (diamond)
                var hubGo = new GameObject("Hub");
                hubGo.transform.SetParent(zoneGo.transform, false);
                var hubRt = hubGo.AddComponent<RectTransform>();
                hubRt.anchorMin = new Vector2(0.5f, 0.38f);
                hubRt.anchorMax = new Vector2(0.5f, 0.38f);
                hubRt.sizeDelta = new Vector2(10f, 10f);
                hubRt.localRotation = Quaternion.Euler(0f, 0f, 45f);
                var hubImg = hubGo.AddComponent<Image>();
                hubImg.color = new Color(1f, 0.9f, 0.5f);
                hubImg.raycastTarget = false;

                // Hub name label
                var hubLabelGo = new GameObject("HubLabel");
                hubLabelGo.transform.SetParent(zoneGo.transform, false);
                var hlRt = hubLabelGo.AddComponent<RectTransform>();
                hlRt.anchorMin = new Vector2(0.05f, 0.2f);
                hlRt.anchorMax = new Vector2(0.95f, 0.38f);
                hlRt.offsetMin = Vector2.zero;
                hlRt.offsetMax = Vector2.zero;
                var hlText = hubLabelGo.AddComponent<Text>();
                hlText.text = b.hub;
                hlText.font = _font;
                hlText.fontSize = 9;
                hlText.color = new Color(1f, 0.9f, 0.6f, 0.85f);
                hlText.alignment = TextAnchor.UpperCenter;
                hlText.horizontalOverflow = HorizontalWrapMode.Overflow;
                hlText.raycastTarget = false;

                // Region name
                var nameGo = new GameObject("Name");
                nameGo.transform.SetParent(zoneGo.transform, false);
                var nRt = nameGo.AddComponent<RectTransform>();
                nRt.anchorMin = new Vector2(0.02f, 0.58f);
                nRt.anchorMax = new Vector2(0.98f, 0.92f);
                nRt.offsetMin = Vector2.zero;
                nRt.offsetMax = Vector2.zero;
                var nText = nameGo.AddComponent<Text>();
                nText.text = b.name;
                nText.font = _font;
                nText.fontSize = 13;
                nText.fontStyle = FontStyle.Bold;
                nText.color = Color.white;
                nText.alignment = TextAnchor.MiddleCenter;
                nText.horizontalOverflow = HorizontalWrapMode.Overflow;
                nText.raycastTarget = false;

                // Level + element tag
                var tagGo = new GameObject("Tag");
                tagGo.transform.SetParent(zoneGo.transform, false);
                var tRt = tagGo.AddComponent<RectTransform>();
                tRt.anchorMin = new Vector2(0.02f, 0.42f);
                tRt.anchorMax = new Vector2(0.98f, 0.58f);
                tRt.offsetMin = Vector2.zero;
                tRt.offsetMax = Vector2.zero;
                var tText = tagGo.AddComponent<Text>();
                tText.text = $"Lv {b.lvMin}-{b.lvMax}   [{b.elem}]";
                tText.font = _font;
                tText.fontSize = 10;
                tText.color = new Color(0.9f, 0.9f, 0.9f, 0.7f);
                tText.alignment = TextAnchor.MiddleCenter;
                tText.horizontalOverflow = HorizontalWrapMode.Overflow;
                tText.raycastTarget = false;

                // Blight indicator in corner
                if (b.blight > 0.1f)
                {
                    var blGo = new GameObject("BlightDot");
                    blGo.transform.SetParent(zoneGo.transform, false);
                    var blRt = blGo.AddComponent<RectTransform>();
                    blRt.anchorMin = new Vector2(0.88f, 0.85f);
                    blRt.anchorMax = new Vector2(0.88f, 0.85f);
                    float dotSize = 6f + b.blight * 10f;
                    blRt.sizeDelta = new Vector2(dotSize, dotSize);
                    Color blightDotColor = Color.Lerp(new Color(0.6f, 0.8f, 0.2f), new Color(0.9f, 0.15f, 0.15f), b.blight);
                    var blImg = blGo.AddComponent<Image>();
                    blImg.color = blightDotColor;
                    blImg.raycastTarget = false;
                }
            }

            // ---- Wilderness scatter dots (fill empty space) ----
            var rng = new System.Random(42);
            for (int i = 0; i < 30; i++)
            {
                float rx = (float)rng.NextDouble();
                float ry = (float)rng.NextDouble();
                // Skip dots that overlap biome zones
                bool inBiome = false;
                foreach (var b in biomes)
                {
                    Vector2 bbl = worldToMap(b.cx - b.half, b.cz - b.half);
                    Vector2 btr = worldToMap(b.cx + b.half, b.cz + b.half);
                    if (rx >= bbl.x && rx <= btr.x && ry >= bbl.y && ry <= btr.y) { inBiome = true; break; }
                }
                if (inBiome) continue;

                var dotGo = new GameObject("WildDot");
                dotGo.transform.SetParent(mapContainer.transform, false);
                var dotRt = dotGo.AddComponent<RectTransform>();
                dotRt.anchorMin = new Vector2(rx, ry);
                dotRt.anchorMax = new Vector2(rx, ry);
                float ds = 2f + (float)rng.NextDouble() * 3f;
                dotRt.sizeDelta = new Vector2(ds, ds);
                var dotImg = dotGo.AddComponent<Image>();
                float g = 0.12f + (float)rng.NextDouble() * 0.08f;
                dotImg.color = new Color(g, g * 0.9f, g * 0.7f, 0.4f);
                dotImg.raycastTarget = false;
            }

            // ---- Fog of War overlay (parented to a layer for live rebuild) ----
            var fogLayer = new GameObject("FogLayer");
            fogLayer.transform.SetParent(mapContainer.transform, false);
            var fogLayerRt = fogLayer.AddComponent<RectTransform>();
            fogLayerRt.anchorMin = Vector2.zero;
            fogLayerRt.anchorMax = Vector2.one;
            fogLayerRt.offsetMin = Vector2.zero;
            fogLayerRt.offsetMax = Vector2.zero;
            _mapFogLayer = fogLayer.transform;
            RebuildFogLayer();

            // ---- Player blip (tracked for live position updates) ----
            _mapBlipRt = null;
            _mapGlowRt = null;
            if (_sceneSetup != null && _sceneSetup.PlayerTransform != null)
            {
                Vector3 pPos = _sceneSetup.PlayerTransform.position;
                Vector2 pm = worldToMap(pPos.x, pPos.z);

                // Outer glow
                var glowGo = new GameObject("PlayerGlow");
                glowGo.transform.SetParent(mapContainer.transform, false);
                var glRt = glowGo.AddComponent<RectTransform>();
                glRt.anchorMin = pm;
                glRt.anchorMax = pm;
                glRt.sizeDelta = new Vector2(22f, 22f);
                var glImg = glowGo.AddComponent<Image>();
                glImg.color = new Color(0.2f, 0.8f, 1f, 0.25f);
                glImg.raycastTarget = false;
                _mapGlowRt = glRt;

                // Blip
                var blipGo = new GameObject("PlayerBlip");
                blipGo.transform.SetParent(mapContainer.transform, false);
                var bpRt = blipGo.AddComponent<RectTransform>();
                bpRt.anchorMin = pm;
                bpRt.anchorMax = pm;
                bpRt.sizeDelta = new Vector2(12f, 12f);
                var bpImg = blipGo.AddComponent<Image>();
                bpImg.color = new Color(0.2f, 0.9f, 1f);
                bpImg.raycastTarget = false;
                _mapBlipRt = bpRt;

                // White center
                var ctrGo = new GameObject("BlipCenter");
                ctrGo.transform.SetParent(blipGo.transform, false);
                var ctrRt = ctrGo.AddComponent<RectTransform>();
                ctrRt.anchorMin = new Vector2(0.25f, 0.25f);
                ctrRt.anchorMax = new Vector2(0.75f, 0.75f);
                ctrRt.offsetMin = Vector2.zero;
                ctrRt.offsetMax = Vector2.zero;
                var ctrImg = ctrGo.AddComponent<Image>();
                ctrImg.color = Color.white;
                ctrImg.raycastTarget = false;
            }

            // ---- Compass labels ----
            string[] dirs = { "N", "S", "W", "E" };
            Vector2[] dAnch = { new Vector2(0.5f, 0.97f), new Vector2(0.5f, 0.03f), new Vector2(0.03f, 0.5f), new Vector2(0.97f, 0.5f) };
            for (int d = 0; d < 4; d++)
            {
                var cGo = new GameObject("Compass_" + dirs[d]);
                cGo.transform.SetParent(mapContainer.transform, false);
                var cRt = cGo.AddComponent<RectTransform>();
                cRt.anchorMin = dAnch[d];
                cRt.anchorMax = dAnch[d];
                cRt.sizeDelta = new Vector2(20f, 18f);
                var cText = cGo.AddComponent<Text>();
                cText.text = dirs[d];
                cText.font = _font;
                cText.fontSize = 14;
                cText.fontStyle = d == 0 ? FontStyle.Bold : FontStyle.Normal;
                cText.color = d == 0 ? new Color(1f, 0.85f, 0.5f, 0.8f) : new Color(0.6f, 0.6f, 0.6f, 0.5f);
                cText.alignment = TextAnchor.MiddleCenter;
                cText.raycastTarget = false;
            }

            // ---- Legend bar at bottom of map ----
            var legendGo = new GameObject("Legend");
            legendGo.transform.SetParent(mapContainer.transform, false);
            var legRt = legendGo.AddComponent<RectTransform>();
            legRt.anchorMin = new Vector2(0.05f, 0f);
            legRt.anchorMax = new Vector2(0.95f, 0f);
            legRt.pivot = new Vector2(0.5f, 1f);
            legRt.anchoredPosition = new Vector2(0f, -4f);
            legRt.sizeDelta = new Vector2(0f, 16f);
            var legText = legendGo.AddComponent<Text>();
            legText.text = "[ ] = Region     <> = Hub     * = Player";
            legText.font = _font;
            legText.fontSize = 10;
            legText.color = new Color(0.5f, 0.5f, 0.5f, 0.7f);
            legText.alignment = TextAnchor.MiddleCenter;
            legText.raycastTarget = false;

            // == RIGHT: Region info panel ==
            var infoPanel = new GameObject("InfoPanel");
            infoPanel.transform.SetParent(panel.transform, false);
            var infoPanelRt = infoPanel.AddComponent<RectTransform>();
            infoPanelRt.anchorMin = new Vector2(0.61f, 0f);
            infoPanelRt.anchorMax = new Vector2(1f, 0.93f);
            infoPanelRt.offsetMin = new Vector2(4f, 8f);
            infoPanelRt.offsetMax = new Vector2(-8f, -4f);
            infoPanel.AddComponent<Image>().color = new Color(0.06f, 0.06f, 0.1f, 0.92f);

            var infoContent = CreateScrollContent(infoPanel.transform, new Vector2(0f, 0f), new Vector2(1f, 1f));
            int row = 0;

            if (string.IsNullOrEmpty(_selectedMapRegion))
            {
                AddRowLabel(infoContent, "", row, Color.white); row++;
                AddRowLabel(infoContent, "  Select a region", row, new Color(0.7f, 0.7f, 0.7f)); row++;
                AddRowLabel(infoContent, "  on the map to", row, new Color(0.7f, 0.7f, 0.7f)); row++;
                AddRowLabel(infoContent, "  view details.", row, new Color(0.7f, 0.7f, 0.7f)); row++;
                AddRowLabel(infoContent, "", row, Color.white); row++;
                AddRowLabel(infoContent, "  Player position", row, new Color(0.2f, 0.9f, 1f)); row++;
                if (_sceneSetup != null && _sceneSetup.PlayerTransform != null)
                {
                    var pp = _sceneSetup.PlayerTransform.position;
                    AddRowLabel(infoContent, $"  ({pp.x:F0}, {pp.z:F0})", row, new Color(0.2f, 0.9f, 1f)); row++;
                }
            }
            else
            {
                var sel = System.Array.Find(biomes, b => b.name == _selectedMapRegion);
                bool regionVisited = _visitedRegions.Contains(_selectedMapRegion);
                Color nameCol = sel.blight > 0.5f ? new Color(0.9f, 0.35f, 0.35f) : new Color(0.5f, 1f, 0.5f);

                if (!regionVisited)
                {
                    // Unexplored region — show minimal info
                    AddRowLabel(infoContent, $"  ??? -- Unexplored", row, new Color(0.5f, 0.5f, 0.5f)); row++;
                    AddRowLabel(infoContent, "", row, Color.white); row++;
                    AddRowLabel(infoContent, "  This region has not", row, new Color(0.6f, 0.6f, 0.6f)); row++;
                    AddRowLabel(infoContent, "  been visited yet.", row, new Color(0.6f, 0.6f, 0.6f)); row++;
                    AddRowLabel(infoContent, "", row, Color.white); row++;
                    AddRowLabel(infoContent, "  Travel there to", row, new Color(0.6f, 0.6f, 0.6f)); row++;
                    AddRowLabel(infoContent, "  reveal details.", row, new Color(0.6f, 0.6f, 0.6f)); row++;
                }
                else
                {
                    // Header
                    AddRowLabel(infoContent, $"  {sel.name}", row, nameCol); row++;
                    AddRowLabel(infoContent, $"  Lv {sel.lvMin}-{sel.lvMax}   [{sel.elem}]", row, new Color(0.75f, 0.75f, 0.75f)); row++;
                    AddRowLabel(infoContent, $"  Hub: {sel.hub}", row, new Color(0.8f, 0.85f, 1f)); row++;

                    // Blight bar
                    int bw = 15;
                    int bf = Mathf.RoundToInt(sel.blight * bw);
                    string bBar = "[" + new string('#', bf) + new string('-', bw - bf) + "]";
                    Color blCol = Color.Lerp(new Color(0.3f, 0.8f, 0.3f), new Color(0.9f, 0.2f, 0.2f), sel.blight);
                    AddRowLabel(infoContent, $"  Blight {bBar} {sel.blight * 100:F0}%", row, blCol); row++;

                    AddRowLabel(infoContent, "", row, Color.white); row++;

                    // Enemies
                    if (regionEnemies.TryGetValue(sel.name, out var enemies))
                    {
                        AddRowLabel(infoContent, "  -- Enemies --", row, new Color(0.95f, 0.85f, 0.5f)); row++;
                        foreach (var (eName, eLv, eTier) in enemies)
                        {
                            Color ec = eTier == "Elite" ? new Color(1f, 0.85f, 0.3f) : Color.white;
                            AddRowLabel(infoContent, $"  [{eTier}] {eName} Lv{eLv}", row, ec); row++;
                        }
                        AddRowLabel(infoContent, "", row, Color.white); row++;
                    }

                    // Bosses
                    if (regionBosses.TryGetValue(sel.name, out var bosses))
                    {
                        AddRowLabel(infoContent, "  -- Bosses --", row, new Color(0.95f, 0.85f, 0.5f)); row++;
                        foreach (var (bName, bLv, bOpt) in bosses)
                        {
                            Color bc = bOpt ? RarityToColor(Rarity.Mythic) : RarityToColor(Rarity.Legendary);
                            string tag = bOpt ? "[Opt]" : "[Main]";
                            AddRowLabel(infoContent, $"  {tag} {bName} Lv{bLv}", row, bc); row++;
                        }
                        AddRowLabel(infoContent, "", row, Color.white); row++;
                    }

                    // NPCs
                    if (regionNpcs.TryGetValue(sel.name, out var npcs))
                    {
                        AddRowLabel(infoContent, "  -- NPCs --", row, new Color(0.95f, 0.85f, 0.5f)); row++;
                        foreach (var (nName, nRole) in npcs)
                        {
                            bool isComp = nRole == "Companion";
                            Color nc = isComp ? new Color(1f, 0.85f, 0.3f) : new Color(0.85f, 0.75f, 1f);
                            string cTag = isComp ? " *" : "";
                            AddRowLabel(infoContent, $"  {nName}{cTag}", row, nc); row++;
                            AddRowLabel(infoContent, $"    {nRole}", row, new Color(0.55f, 0.55f, 0.55f)); row++;
                        }
                        AddRowLabel(infoContent, "", row, Color.white); row++;
                    }

                    // Warp button
                    if (_sceneSetup != null && _sceneSetup.PlayerTransform != null)
                    {
                        Vector3 warpTarget = new Vector3(sel.cx, 0f, sel.cz);
                        AddRowLabel(infoContent, "  Travel to region", row, new Color(0.6f, 0.85f, 1f));
                        AddButton(infoContent, "Warp", row, () =>
                        {
                            var cc = _sceneSetup.PlayerTransform.GetComponent<CharacterController>();
                            if (cc != null) { cc.enabled = false; _sceneSetup.PlayerTransform.position = warpTarget; cc.enabled = true; }
                            else _sceneSetup.PlayerTransform.position = warpTarget;
                            Debug.Log($"[Map] Warped to {sel.name}");
                            CloseActiveScreen();
                            ShowMap();
                        }); row++;
                    }
                }
            }

            SetContentHeight(infoContent, row);
        }

        private void DrawMapPath(Transform parent, Vector2 from, Vector2 to, Color color)
        {
            // Draw a line between two anchor-space points using a rotated thin rect
            var go = new GameObject("Path");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();

            // Midpoint in anchor space
            Vector2 mid = (from + to) * 0.5f;
            rt.anchorMin = mid;
            rt.anchorMax = mid;

            // We need pixel-space distance. Estimate from parent rect.
            // Since anchors are 0..1, we scale by parent size at layout time.
            // Use a trick: set anchors to endpoints and use a thin height.
            // Simpler approach: place at midpoint, rotate, set width to approximate distance.
            var parentRt = parent.GetComponent<RectTransform>();
            Vector2 pSize = parentRt.rect.size;
            if (pSize.x < 1f) pSize = new Vector2(500f, 500f); // fallback before layout

            Vector2 fromPx = from * pSize;
            Vector2 toPx = to * pSize;
            float dist = Vector2.Distance(fromPx, toPx);
            float angle = Mathf.Atan2(toPx.y - fromPx.y, toPx.x - fromPx.x) * Mathf.Rad2Deg;

            rt.sizeDelta = new Vector2(dist, 3f);
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);

            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        // ================================================================
        // ADVENTURE GUILD SCREEN
        // ================================================================

        private void ShowGuild()
        {
            var panel = CreateScreenPanel("Adventure Guild");
            var content = CreateScrollContent(panel.transform, new Vector2(0f, 0f), new Vector2(1f, 0.88f));

            // Tab buttons at top
            string[] tabNames = { "Info", "Contracts", "Active", "Turn In" };
            for (int i = 0; i < tabNames.Length; i++)
            {
                int tabIdx = i;
                float tabWidth = 1f / tabNames.Length;
                var tabGo = new GameObject("Tab_" + tabNames[i]);
                tabGo.transform.SetParent(panel.transform, false);
                var tabRt = tabGo.AddComponent<RectTransform>();
                tabRt.anchorMin = new Vector2(tabWidth * i, 0.88f);
                tabRt.anchorMax = new Vector2(tabWidth * (i + 1), 0.93f);
                tabRt.offsetMin = new Vector2(2f, 0f);
                tabRt.offsetMax = new Vector2(-2f, 0f);

                bool isActive = _guildTab == i;
                var tabImg = tabGo.AddComponent<Image>();
                tabImg.color = isActive ? new Color(0.25f, 0.35f, 0.5f) : new Color(0.15f, 0.15f, 0.2f);

                var tabBtn = tabGo.AddComponent<Button>();
                tabBtn.targetGraphic = tabImg;
                tabBtn.onClick.AddListener(() =>
                {
                    _guildTab = tabIdx;
                    CloseActiveScreen();
                    ShowGuild();
                });

                var tabTextGo = new GameObject("Text");
                tabTextGo.transform.SetParent(tabGo.transform, false);
                var tabText = tabTextGo.AddComponent<Text>();
                tabText.text = tabNames[i];
                tabText.font = _font;
                tabText.fontSize = 15;
                tabText.color = isActive ? Color.white : new Color(0.6f, 0.6f, 0.6f);
                tabText.alignment = TextAnchor.MiddleCenter;
                var ttRt = tabTextGo.GetComponent<RectTransform>();
                ttRt.anchorMin = Vector2.zero;
                ttRt.anchorMax = Vector2.one;
                ttRt.offsetMin = Vector2.zero;
                ttRt.offsetMax = Vector2.zero;
            }

            switch (_guildTab)
            {
                case 0: BuildGuildInfoTab(content); break;
                case 1: BuildGuildContractsTab(content); break;
                case 2: BuildGuildActiveTab(content); break;
                case 3: BuildGuildTurnInTab(content); break;
            }
        }

        private void BuildGuildInfoTab(Transform content)
        {
            int row = 0;
            string rankName = GetGuildRankName();
            Color rankColor = GetGuildRankColor();
            int rankIdx = GetGuildRankIndex();

            AddRowLabel(content, $"  Guild Rank: {rankName}", row, rankColor);
            row++;

            // Progress bar to next rank
            int nextThreshold = rankIdx < GuildRankThresholds.Length - 1
                ? GuildRankThresholds[rankIdx + 1]
                : GuildRankThresholds[rankIdx];
            int currentThreshold = GuildRankThresholds[rankIdx];
            float progress = rankIdx >= GuildRankThresholds.Length - 1
                ? 1f
                : (float)(_guildReputation - currentThreshold) / (nextThreshold - currentThreshold);

            string nextRankLabel = rankIdx < GuildRankNames.Length - 1
                ? GuildRankNames[rankIdx + 1]
                : "MAX";
            AddRowLabel(content, $"  Reputation: {_guildReputation} / {nextThreshold}  (next: {nextRankLabel})", row, Color.white);
            row++;

            // Visual progress bar
            string barFilled = new string('|', Mathf.Clamp((int)(progress * 20), 0, 20));
            string barEmpty = new string('.', 20 - barFilled.Length);
            AddRowLabel(content, $"  [{barFilled}{barEmpty}]  {(progress * 100f):F0}%", row, rankColor);
            row++;

            AddRowLabel(content, "", row, Color.white); row++;
            AddRowLabel(content, $"  Contracts Completed: {_guildContractsCompleted}", row, Color.white);
            row++;
            AddRowLabel(content, $"  Guild Tokens: {_guildTokens}", row, new Color(0.5f, 1f, 0.5f));
            row++;

            AddRowLabel(content, "", row, Color.white); row++;
            AddRowLabel(content, "  --- Rank Thresholds ---", row, new Color(0.95f, 0.85f, 0.5f));
            row++;

            for (int i = 0; i < GuildRankNames.Length; i++)
            {
                bool isCurrent = i == rankIdx;
                Color c = isCurrent ? GetGuildRankColor() : new Color(0.5f, 0.5f, 0.5f);
                string marker = isCurrent ? " <<" : "";
                AddRowLabel(content, $"    {GuildRankNames[i]}: {GuildRankThresholds[i]} rep{marker}", row, c);
                row++;
            }

            SetContentHeight(content, row);
        }

        private void BuildGuildContractsTab(Transform content)
        {
            int row = 0;
            int rankIdx = GetGuildRankIndex();

            AddRowLabel(content, "  --- Available Guild Contracts ---", row, new Color(0.95f, 0.85f, 0.5f));
            row++;

            var activeQuests = _questManager.GetActiveQuests();
            var completedIds = _questManager.GetCompletedQuestIds();
            var uncollected = _questManager.GetCompletedUncollectedQuests();

            foreach (var quest in _testQuests)
            {
                if (quest.Type != QuestType.GuildContract) continue;
                if (activeQuests.ContainsKey(quest.QuestId) || completedIds.Contains(quest.QuestId)
                    || uncollected.ContainsKey(quest.QuestId)) continue;

                if (!_guildContractMeta.TryGetValue(quest.QuestId, out var meta))
                    continue;

                bool locked = rankIdx < meta.RequiredRank;
                string rankReq = GuildRankNames[Mathf.Clamp(meta.RequiredRank, 0, GuildRankNames.Length - 1)];

                if (locked)
                {
                    AddRowLabel(content, $"  [Locked] {quest.QuestName}  (Requires: {rankReq})", row, new Color(0.4f, 0.4f, 0.4f));
                    row++;
                }
                else
                {
                    AddRowLabel(content, $"  {quest.QuestName}  (+{meta.ReputationReward} rep)", row, new Color(0.5f, 1f, 0.5f));

                    var capturedQuest = quest;
                    AddButton(content, "Accept", row, () =>
                    {
                        _questManager.AcceptQuest(capturedQuest);
                        CloseActiveScreen();
                        ShowGuild();
                    });
                    row++;

                    // Show objectives
                    foreach (var obj in quest.Objectives)
                    {
                        AddRowLabel(content, $"    - {obj.ObjectiveDescription} ({obj.RequiredCount})", row, new Color(0.7f, 0.7f, 0.7f));
                        row++;
                    }

                    // Show rewards
                    foreach (var reward in quest.Rewards)
                    {
                        string rewardText = "";
                        if (!string.IsNullOrEmpty(reward.ItemId))
                            rewardText += $"{FormatItemName(reward.ItemId)} x{reward.Quantity}  ";
                        if (reward.Gold > 0) rewardText += $"{reward.Gold} Gold  ";
                        AddRowLabel(content, $"    Reward: {rewardText.Trim()}", row, new Color(0.7f, 0.7f, 0.5f));
                        row++;
                    }
                }
            }

            SetContentHeight(content, row);
        }

        private void BuildGuildActiveTab(Transform content)
        {
            int row = 0;
            AddRowLabel(content, "  --- Active Guild Contracts ---", row, new Color(0.95f, 0.85f, 0.5f));
            row++;

            var activeQuests = _questManager.GetActiveQuests();
            bool anyFound = false;

            foreach (var kvp in activeQuests)
            {
                var state = kvp.Value;
                var def = state.Definition;
                if (def.Type != QuestType.GuildContract) continue;
                anyFound = true;

                AddRowLabel(content, $"  {def.QuestName}", row, new Color(0.5f, 1f, 0.5f));

                var capturedId = def.QuestId;
                AddButton(content, "Abandon", row, () =>
                {
                    _questManager.AbandonQuest(capturedId);
                    CloseActiveScreen();
                    ShowGuild();
                });
                row++;

                for (int i = 0; i < def.Objectives.Count; i++)
                {
                    var obj = def.Objectives[i];
                    int progress = state.ObjectiveProgress[i];
                    bool done = progress >= obj.RequiredCount;
                    string check = done ? "[X]" : "[ ]";
                    Color objColor = done ? new Color(0.5f, 1f, 0.5f) : Color.white;

                    // Progress bar
                    float pct = (float)progress / obj.RequiredCount;
                    int filled = Mathf.Clamp((int)(pct * 10), 0, 10);
                    string bar = new string('|', filled) + new string('.', 10 - filled);

                    AddRowLabel(content, $"    {check} {obj.ObjectiveDescription}  [{bar}] {progress}/{obj.RequiredCount}", row, objColor);
                    row++;
                }

                AddRowLabel(content, "", row, Color.white);
                row++;
            }

            if (!anyFound)
            {
                AddRowLabel(content, "    (no active guild contracts)", row, Color.gray);
                row++;
            }

            SetContentHeight(content, row);
        }

        private void BuildGuildTurnInTab(Transform content)
        {
            int row = 0;
            AddRowLabel(content, "  --- Completed Guild Contracts ---", row, new Color(0.95f, 0.85f, 0.5f));
            row++;

            var uncollected = _questManager.GetCompletedUncollectedQuests();
            bool anyFound = false;

            foreach (var kvp in uncollected)
            {
                var def = kvp.Value.Definition;
                if (def.Type != QuestType.GuildContract) continue;
                anyFound = true;

                _guildContractMeta.TryGetValue(def.QuestId, out var meta);

                AddRowLabel(content, $"  {def.QuestName}", row, new Color(0.4f, 0.9f, 0.4f));

                var capturedId = def.QuestId;
                int capturedRep = meta.ReputationReward;
                AddButton(content, "Collect", row, () =>
                {
                    _questManager.CollectGuildQuestRewards(capturedId);
                    _guildReputation += capturedRep;
                    _guildTokens += capturedRep / 2;
                    _guildContractsCompleted++;
                    Debug.Log($"[Guild] Collected rewards for {capturedId}: +{capturedRep} rep, +{capturedRep / 2} tokens");
                    CloseActiveScreen();
                    ShowGuild();
                });
                row++;

                // Show rewards preview
                foreach (var reward in def.Rewards)
                {
                    string rewardText = "";
                    if (!string.IsNullOrEmpty(reward.ItemId))
                        rewardText += $"{FormatItemName(reward.ItemId)} x{reward.Quantity}  ";
                    if (reward.Gold > 0) rewardText += $"{reward.Gold} Gold  ";
                    AddRowLabel(content, $"    Reward: {rewardText.Trim()}", row, new Color(0.7f, 0.7f, 0.5f));
                    row++;
                }

                AddRowLabel(content, $"    +{meta.ReputationReward} Reputation  +{meta.ReputationReward / 2} Guild Tokens", row, new Color(0.5f, 1f, 0.8f));
                row++;

                AddRowLabel(content, "", row, Color.white);
                row++;
            }

            if (!anyFound)
            {
                AddRowLabel(content, "    (no contracts ready for turn-in)", row, Color.gray);
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
        // TITLES SCREEN
        // ================================================================

        private void ShowTitles()
        {
            var panel = CreateScreenPanel("Titles");
            var content = CreateScrollContent(panel.transform, new Vector2(0f, 0f), new Vector2(1f, 0.9f));

            int row = 0;
            Color headerColor = new Color(1f, 0.85f, 0.4f);
            Color equippedColor = new Color(0.4f, 1f, 0.4f);
            Color lockedColor = new Color(0.5f, 0.5f, 0.5f);

            // ---- Equipped Title ----
            AddRowLabel(content, "  --- Equipped Title ---", row, headerColor); row++;
            string equippedId = _titleSystem.EquippedTitleId;
            if (!string.IsNullOrEmpty(equippedId))
            {
                var equipped = _testTitles.Find(t => t.TitleId == equippedId);
                if (equipped != null)
                {
                    AddRowLabel(content, $"  {equipped.TitleName}", row, equippedColor); row++;
                    AddRowLabel(content, $"    Passive: {equipped.Description}", row, new Color(0.8f, 0.8f, 0.8f)); row++;
                    AddRowLabel(content, $"    Stats: {FormatStatBonuses(equipped.StatBonuses)}", row, new Color(0.8f, 0.8f, 0.8f)); row++;
                    AddRowLabel(content, "", row, Color.white);
                    AddButton(content, "Unequip", row, () =>
                    {
                        _titleSystem.UnequipTitle();
                        CloseActiveScreen(); ShowTitles();
                    }); row++;
                }
            }
            else
            {
                AddRowLabel(content, "  (none)", row, lockedColor); row++;
            }
            AddRowLabel(content, "", row, Color.white); row++;

            // ---- Category sections ----
            var categories = new[]
            {
                ("Farming", new[] { "seedling", "greenhand", "master_cultivator", "son_of_the_soil", "last_farmer_of_millhaven" }),
                ("Combat", new[] { "reluctant_blade", "scarecrow", "blight_reaper", "warden_of_the_green", "legend_of_eldrath" }),
                ("Exploration & Social", new[] { "wanderer", "lorekeep", "the_peoples_champion", "herbalist_supreme", "accord_keeper" }),
            };

            foreach (var (catName, titleIds) in categories)
            {
                AddRowLabel(content, $"  --- {catName} ---", row, headerColor); row++;

                foreach (var titleId in titleIds)
                {
                    var def = _testTitles.Find(t => t.TitleId == titleId);
                    if (def == null) continue;

                    bool unlocked = _titleSystem.UnlockedTitleIds.Contains(titleId);
                    bool isEquipped = _titleSystem.EquippedTitleId == titleId;

                    string tag = isEquipped ? "  [EQUIPPED]" : !unlocked ? "  [LOCKED]" : "";
                    Color nameColor = isEquipped ? equippedColor : unlocked ? Color.white : lockedColor;

                    AddRowLabel(content, $"  {def.TitleName}{tag}", row, nameColor); row++;
                    AddRowLabel(content, $"    Passive: {def.Description}", row, unlocked ? new Color(0.8f, 0.8f, 0.8f) : lockedColor); row++;
                    AddRowLabel(content, $"    Stats: {FormatStatBonuses(def.StatBonuses)}", row, unlocked ? new Color(0.8f, 0.8f, 0.8f) : lockedColor); row++;
                    AddRowLabel(content, $"    Unlock: {def.UnlockCondition}", row, unlocked ? new Color(0.6f, 0.8f, 0.6f) : lockedColor); row++;

                    if (unlocked && !isEquipped)
                    {
                        string capturedId = titleId;
                        AddRowLabel(content, "", row, Color.white);
                        AddButton(content, "Equip", row, () =>
                        {
                            _titleSystem.EquipTitle(capturedId);
                            CloseActiveScreen(); ShowTitles();
                        }); row++;
                    }

                    AddRowLabel(content, "", row, Color.white); row++;
                }
            }

            SetContentHeight(content, row);
        }

        private string FormatStatBonuses(StatBlock stats)
        {
            var parts = new List<string>();
            if (stats.Vigor != 0) parts.Add($"VIG +{stats.Vigor:F0}");
            if (stats.Strength != 0) parts.Add($"STR +{stats.Strength:F0}");
            if (stats.Harvest != 0) parts.Add($"HAR +{stats.Harvest:F0}");
            if (stats.Verdance != 0) parts.Add($"VER +{stats.Verdance:F0}");
            if (stats.Agility != 0) parts.Add($"AGI +{stats.Agility:F0}");
            if (stats.Resilience != 0) parts.Add($"RES +{stats.Resilience:F0}");
            return parts.Count > 0 ? string.Join(", ", parts) : "None";
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

            // ---- GOLD / ESSENCE / GUILD TOKENS ----
            AddRowLabel(content, "  --- Currency ---", row, headerColor); row++;

            AddRowLabel(content, $"  Gold: {_gold}", row, cheatColor);
            AddCheatValueButtons(content, row, new[] { 1000f, 10000f, 100000f, 999999f }, v =>
            {
                _gold = (int)v;
                CloseActiveScreen(); ShowCheatMenu();
            }); row++;

            AddRowLabel(content, "  Add 1000 Gold", row, cheatColor);
            AddButton(content, "+1000", row, () =>
            {
                _gold += 1000;
                Debug.Log($"[Cheat] Added 1000 gold (now {_gold})");
                CloseActiveScreen(); ShowCheatMenu();
            }); row++;

            AddRowLabel(content, $"  Guild Tokens: {_guildTokens}", row, cheatColor);
            AddCheatValueButtons(content, row, new[] { 100f, 500f, 1000f, 9999f }, v =>
            {
                _guildTokens = (int)v;
                CloseActiveScreen(); ShowCheatMenu();
            }); row++;

            AddRowLabel(content, "  Add 100 Guild Tokens", row, cheatColor);
            AddButton(content, "+100", row, () =>
            {
                _guildTokens += 100;
                Debug.Log($"[Cheat] Added 100 guild tokens (now {_guildTokens})");
                CloseActiveScreen(); ShowCheatMenu();
            }); row++;

            AddRowLabel(content, $"  Guild Reputation: {_guildReputation}  ({GetGuildRankName()})", row, cheatColor);
            AddCheatValueButtons(content, row, new[] { 100f, 300f, 600f, 1500f }, v =>
            {
                _guildReputation = (int)v;
                CloseActiveScreen(); ShowCheatMenu();
            }); row++;

            AddRowLabel(content, "  Add 100 Guild Reputation", row, cheatColor);
            AddButton(content, "+100 GR", row, () =>
            {
                _guildReputation += 100;
                Debug.Log($"[Cheat] Added 100 guild reputation (now {_guildReputation})");
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

            // ---- WEAPONS ----
            AddRowLabel(content, "  --- Weapons ---", row, headerColor); row++;

            AddRowLabel(content, "  Unlock all Common & Uncommon", row, cheatColor);
            AddButton(content, "Unlock", row, () =>
            {
                foreach (var kvp in _weaponDefs)
                {
                    if (kvp.Value.Rarity == Rarity.Common || kvp.Value.Rarity == Rarity.Uncommon)
                        _ownedWeapons.Add(kvp.Key);
                }
                Debug.Log("[Cheat] Unlocked all Common & Uncommon weapons");
                CloseActiveScreen(); ShowCheatMenu();
            }); row++;

            AddRowLabel(content, "  Unlock all Rare & Epic", row, cheatColor);
            AddButton(content, "Unlock", row, () =>
            {
                foreach (var kvp in _weaponDefs)
                {
                    if (kvp.Value.Rarity == Rarity.Rare || kvp.Value.Rarity == Rarity.Epic)
                        _ownedWeapons.Add(kvp.Key);
                }
                Debug.Log("[Cheat] Unlocked all Rare & Epic weapons");
                CloseActiveScreen(); ShowCheatMenu();
            }); row++;

            AddRowLabel(content, "  Unlock all Legendary weapons", row, new Color(1f, 0.6f, 0.1f));
            AddButton(content, "Unlock", row, () =>
            {
                foreach (var kvp in _weaponDefs)
                {
                    if (kvp.Value.Rarity == Rarity.Legendary)
                        _ownedWeapons.Add(kvp.Key);
                }
                Debug.Log("[Cheat] Unlocked all Legendary weapons");
                CloseActiveScreen(); ShowCheatMenu();
            }); row++;

            AddRowLabel(content, "  Unlock ALL weapons", row, new Color(1f, 0.85f, 0.3f));
            AddButton(content, "All", row, () =>
            {
                foreach (var kvp in _weaponDefs)
                    _ownedWeapons.Add(kvp.Key);
                Debug.Log("[Cheat] Unlocked all weapons");
                CloseActiveScreen(); ShowCheatMenu();
            }); row++;

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
                foreach (var id in CompanionRegistry.Keys)
                {
                    _gacha.RegisterOwnedCompanion(id);
                    _unlockedCompanions.Add(id);
                    if (!_gachaLog.Exists(e => e.Contains(GetCompanionDisplayName(id))))
                        _gachaLog.Insert(0, $"{RarityStars(GetCompanionRarity(id))} {GetCompanionDisplayName(id)} NEW!");
                }
                Debug.Log("[Cheat] All companions unlocked");
                CloseActiveScreen(); ShowCheatMenu();
            }); row++;

            AddRowLabel(content, "", row, Color.white); row++;

            // ---- COMPANION LEVELS ----
            AddRowLabel(content, "  --- Companion Levels ---", row, headerColor); row++;

            AddRowLabel(content, $"  Player Level: {_playerLevel}", row, cheatColor);
            AddCheatValueButtons(content, row, new[] { 1f, 15f, 30f, 45f }, v =>
            {
                _playerLevel = (int)v;
                CloseActiveScreen(); ShowCheatMenu();
            }); row++;

            AddRowLabel(content, "  Level all companions to max (45)", row, cheatColor);
            AddButton(content, "Max All", row, () =>
            {
                foreach (var id in CompanionRegistry.Keys)
                    _companionLevels[id] = 45;
                // Respawn active companions at new level
                if (_sceneSetup != null)
                {
                    if (_partyActiveId != null) _sceneSetup.SetPartyCompanion("Active", _partyActiveId, 45);
                    if (_partySupportId != null) _sceneSetup.SetPartyCompanion("Support", _partySupportId, 45);
                }
                Debug.Log("[Cheat] All companions set to Lv 45");
                CloseActiveScreen(); ShowCheatMenu();
            }); row++;

            AddRowLabel(content, "  Reset all companions to Lv 1", row, new Color(1f, 0.3f, 0.3f));
            AddButton(content, "Reset", row, () =>
            {
                _companionLevels.Clear();
                // Respawn active companions at level 1
                if (_sceneSetup != null)
                {
                    if (_partyActiveId != null) _sceneSetup.SetPartyCompanion("Active", _partyActiveId, 1);
                    if (_partySupportId != null) _sceneSetup.SetPartyCompanion("Support", _partySupportId, 1);
                }
                Debug.Log("[Cheat] All companions reset to Lv 1");
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

            // ---- TITLES ----
            AddRowLabel(content, "  --- Titles ---", row, headerColor); row++;

            int unlockedCount = _titleSystem.UnlockedTitleIds.Count;
            AddRowLabel(content, $"  Unlocked: {unlockedCount} / {_testTitles.Count}", row, cheatColor); row++;

            AddRowLabel(content, "  Unlock all titles", row, cheatColor);
            AddButton(content, "Unlock All", row, () =>
            {
                foreach (var t in _testTitles)
                    _titleSystem.UnlockTitle(t.TitleId);
                Debug.Log("[Cheat] All titles unlocked");
                CloseActiveScreen(); ShowCheatMenu();
            }); row++;

            AddRowLabel(content, "  Unequip current title", row, cheatColor);
            AddButton(content, "Unequip", row, () =>
            {
                _titleSystem.UnequipTitle();
                Debug.Log("[Cheat] Title unequipped");
                CloseActiveScreen(); ShowCheatMenu();
            }); row++;

            AddRowLabel(content, "", row, Color.white); row++;

            // ---- PITY RESET ----
            AddRowLabel(content, "  --- Gacha ---", row, headerColor); row++;

            AddRowLabel(content, "  Reset pity counters (all banners)", row, cheatColor);
            AddButton(content, "Reset", row, () =>
            {
                foreach (var b in _testBanners)
                {
                    var p = _gacha.GetPityTracker(b.BannerId);
                    p.ResetLegendaryPity();
                    p.ResetMythicPity();
                    p.TotalPulls = 0;
                    p.LostLastFiftyFifty = false;
                }
                Debug.Log("[Cheat] Pity reset on all banners");
                CloseActiveScreen(); ShowCheatMenu();
            }); row++;

            AddRowLabel(content, "  Set pity to 59 (next = guaranteed 5★)", row, cheatColor);
            AddButton(content, "Set 59", row, () =>
            {
                foreach (var b in _testBanners)
                {
                    var p = _gacha.GetPityTracker(b.BannerId);
                    p.PullsSinceLegendary = 59;
                }
                Debug.Log("[Cheat] Pity set to 59 on all banners");
                CloseActiveScreen(); ShowCheatMenu();
            }); row++;

            AddRowLabel(content, $"  Starseeds: {_starseeds}", row, cheatColor);
            AddCheatValueButtons(content, row, new[] { 160f, 1440f, 5000f, 50000f }, v =>
            {
                _starseeds = (int)v;
                CloseActiveScreen(); ShowCheatMenu();
            }); row++;

            AddRowLabel(content, "  Add 5000 Starseeds", row, cheatColor);
            AddButton(content, "+5000", row, () =>
            {
                _starseeds += 5000;
                Debug.Log($"[Cheat] Added 5000 Starseeds (now {_starseeds})");
                CloseActiveScreen(); ShowCheatMenu();
            }); row++;

            AddRowLabel(content, "", row, Color.white); row++;

            // ---- ENEMIES ----
            AddRowLabel(content, "  --- Enemies ---", row, headerColor); row++;

            if (_sceneSetup != null)
            {
                AddRowLabel(content, "  Kill All Enemies", row, cheatColor);
                AddButton(content, "Kill All", row, () =>
                {
                    _sceneSetup.KillAllEnemies();
                    CloseActiveScreen(); ShowCheatMenu();
                }); row++;

                AddRowLabel(content, "  Respawn All Enemies (full HP)", row, cheatColor);
                AddButton(content, "Respawn", row, () =>
                {
                    _sceneSetup.RespawnAllEnemies();
                    CloseActiveScreen(); ShowCheatMenu();
                }); row++;
            }

            AddRowLabel(content, "", row, Color.white); row++;

            // ---- FOG OF WAR ----
            AddRowLabel(content, "  --- Fog of War ---", row, headerColor); row++;

            int revealedCount = _revealedFogCells.Count;
            int totalCells = FogGridRes * FogGridRes;
            AddRowLabel(content, $"  Revealed: {revealedCount}/{totalCells} cells", row, cheatColor); row++;
            AddRowLabel(content, $"  Visited regions: {_visitedRegions.Count}/5", row, cheatColor); row++;

            AddRowLabel(content, "  Reveal entire map", row, cheatColor);
            AddButton(content, "Reveal All", row, () =>
            {
                for (int i = 0; i < FogGridRes * FogGridRes; i++)
                    _revealedFogCells.Add(i);
                _visitedRegions.Add("Greenreach Valley");
                _visitedRegions.Add("The Ashen Steppe");
                _visitedRegions.Add("Gloomtide Marshes");
                _visitedRegions.Add("Frosthollow Peaks");
                _visitedRegions.Add("The Withered Heart");
                Debug.Log("[Cheat] Revealed all map cells and regions");
                CloseActiveScreen(); ShowCheatMenu();
            }); row++;

            AddRowLabel(content, "  Reset fog (re-hide map)", row, new Color(1f, 0.3f, 0.3f));
            AddButton(content, "Reset Fog", row, () =>
            {
                _revealedFogCells.Clear();
                _visitedRegions.Clear();
                RevealFogAroundPlayer();
                Debug.Log("[Cheat] Fog reset — only current position revealed");
                CloseActiveScreen(); ShowCheatMenu();
            }); row++;

            AddRowLabel(content, "", row, Color.white); row++;

            // ---- TELEPORT TO TOWN / NPC ----
            AddRowLabel(content, "  --- Teleport to Town / NPC ---", row, headerColor); row++;

            if (_sceneSetup != null && _sceneSetup.PlayerTransform != null)
            {
                // Teleport to town center (near the well)
                AddRowLabel(content, "  Teleport to Town Center", row, cheatColor);
                AddButton(content, "Warp", row, () =>
                {
                    Vector3 townCenter = new Vector3(0f, 0f, -12f);
                    var cc = _sceneSetup.PlayerTransform.GetComponent<CharacterController>();
                    if (cc != null)
                    {
                        cc.enabled = false;
                        _sceneSetup.PlayerTransform.position = townCenter;
                        cc.enabled = true;
                    }
                    else
                    {
                        _sceneSetup.PlayerTransform.position = townCenter;
                    }
                    Debug.Log("[Cheat] Teleported to Town Center");
                    CloseActiveScreen(); ShowCheatMenu();
                }); row++;

                // Teleport to Guild Hall
                AddRowLabel(content, "  Teleport to Guild", row, cheatColor);
                AddButton(content, "Warp", row, () =>
                {
                    Vector3 guildPos = new Vector3(2f, 0f, -2f);
                    var cc = _sceneSetup.PlayerTransform.GetComponent<CharacterController>();
                    if (cc != null)
                    {
                        cc.enabled = false;
                        _sceneSetup.PlayerTransform.position = guildPos;
                        cc.enabled = true;
                    }
                    else
                    {
                        _sceneSetup.PlayerTransform.position = guildPos;
                    }
                    Debug.Log("[Cheat] Teleported to Guild Hall");
                    CloseActiveScreen(); ShowCheatMenu();
                }); row++;

                // Individual NPC teleports (updated to town positions)
                var npcNames = new[] { "Silas", "Maren", "Bram", "Quartermaster Voss", "Druid Enna", "The Whisperer" };
                var npcPositions = new[]
                {
                    new Vector3(-6f, 0f, -7f),
                    new Vector3(6f, 0f, -7f),
                    new Vector3(-6f, 0f, -15f),
                    new Vector3(6f, 0f, -15f),
                    new Vector3(-8f, 0f, -22f),
                    new Vector3(8f, 0f, -22f),
                };

                for (int i = 0; i < npcNames.Length; i++)
                {
                    string npcName = npcNames[i];
                    Vector3 pos = npcPositions[i] + new Vector3(2f, 0f, 0f); // offset so player doesn't overlap NPC
                    AddRowLabel(content, $"  Teleport to {npcName}", row, cheatColor);
                    AddButton(content, "Warp", row, () =>
                    {
                        var cc = _sceneSetup.PlayerTransform.GetComponent<CharacterController>();
                        if (cc != null)
                        {
                            cc.enabled = false;
                            _sceneSetup.PlayerTransform.position = pos;
                            cc.enabled = true;
                        }
                        else
                        {
                            _sceneSetup.PlayerTransform.position = pos;
                        }
                        Debug.Log($"[Cheat] Teleported to {npcName}");
                        CloseActiveScreen(); ShowCheatMenu();
                    }); row++;
                }
            }

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

        // ================================================================
        // Companion Registry — all 32 GDD companions
        // ================================================================

        private static readonly Dictionary<string, (string Name, string Class, string Element)> CompanionRegistry = new()
        {
            // 5★ Legendary (10)
            { "companion_seraphine",   ("Seraphine Dawnveil",   "Magician",        "Pyro")    },
            { "companion_korrath",     ("Korrath the Unbroken", "Axe Warden",      "Geo")     },
            { "companion_yuki",        ("Yuki Frostwhisper",    "Archer",          "Frost")   },
            { "companion_theron",      ("Theron Ashblood",      "Necromancer",     "Umbral")  },
            { "companion_lyssara",     ("Lyssara Tidecaller",   "Cleric",          "Hydro")   },
            { "companion_vex",         ("Vex Stormfang",        "Duelist",         "Volt")    },
            { "companion_orin",        ("Orin Ironroot",        "Shieldbearer",    "Verdant") },
            { "companion_zara",        ("Zara Emberlance",      "Lancer",          "Pyro")    },
            { "companion_malachai",    ("Malachai Voidweaver",  "Magician",        "Umbral")  },
            { "companion_kaelen",      ("Kaelen Windrider",     "Crossbow Knight", "Volt")    },

            // 4★ Epic (12)
            { "companion_brynn",       ("Brynn Ashford",        "Swordsman",       "Pyro")    },
            { "companion_hale",        ("Hale Deeproot",        "Shieldbearer",    "Verdant") },
            { "companion_nix",         ("Nix Shadewalker",      "Duelist",         "Umbral")  },
            { "companion_petra",       ("Petra Stoneheart",     "Axe Warden",      "Geo")     },
            { "companion_elara",       ("Elara Mistbow",        "Archer",          "Hydro")   },
            { "companion_fenwick",     ("Fenwick Bonespark",    "Necromancer",     "Volt")    },
            { "companion_maeven",      ("Sister Maeven",        "Cleric",          "Frost")   },
            { "companion_rook",        ("Rook Galehammer",      "Lancer",          "Verdant") },
            { "companion_denna",       ("Denna Sparkshot",      "Crossbow Knight", "Pyro")    },
            { "companion_cassius",     ("Cassius Ironvow",      "Swordsman",       "Frost")   },
            { "companion_wren",        ("Wren Hollowgaze",      "Magician",        "Verdant") },
            { "companion_jareth",      ("Jareth Duskblade",     "Duelist",         "Pyro")    },

            // 3★ Rare (10)
            { "companion_tomas",       ("Tomas Fieldhand",      "Swordsman",       "Verdant") },
            { "companion_berta",       ("Berta Coalfist",       "Axe Warden",      "Pyro")    },
            { "companion_pip",         ("Pip Quickfingers",     "Duelist",         "Volt")    },
            { "companion_marsh",       ("Old Marsh",            "Shieldbearer",    "Hydro")   },
            { "companion_willow",      ("Willow Softbow",       "Archer",          "Verdant") },
            { "companion_emmet",       ("Emmet Rattlebones",    "Necromancer",     "Umbral")  },
            { "companion_lira",        ("Novice Lira",          "Cleric",          "Hydro")   },
            { "companion_bruno",       ("Bruno Ashpike",        "Lancer",          "Pyro")    },
            { "companion_mira",        ("Mira Boltstring",      "Crossbow Knight", "Volt")    },
            { "companion_sage",        ("Sage Dustweave",       "Magician",        "Umbral")  },
        };

        private static readonly HashSet<string> FiveStarIds = new()
        {
            "companion_seraphine", "companion_korrath", "companion_yuki", "companion_theron",
            "companion_lyssara", "companion_vex", "companion_orin", "companion_zara",
            "companion_malachai", "companion_kaelen"
        };

        private static readonly HashSet<string> FourStarIds = new()
        {
            "companion_brynn", "companion_hale", "companion_nix", "companion_petra",
            "companion_elara", "companion_fenwick", "companion_maeven", "companion_rook",
            "companion_denna", "companion_cassius", "companion_wren", "companion_jareth"
        };

        private static string GetCompanionDisplayName(string id)
        {
            if (CompanionRegistry.TryGetValue(id, out var info))
                return info.Name;
            return FormatItemName(id);
        }

        private bool CanAffordPull(int count)
        {
            int cost = count == 1 ? StarseedCostSingle : StarseedCost10Pull;
            return _starseeds >= cost;
        }

        private void SpendStarseeds(int count)
        {
            int cost = count == 1 ? StarseedCostSingle : StarseedCost10Pull;
            _starseeds -= cost;
        }

        private void RecordWishHistory(GachaPullResult result, BannerDefinitionSO banner)
        {
            int pullNum = _wishHistory.Count + 1;
            _wishHistory.Insert(0, (result.CompanionId, result.Rarity, result.IsDuplicate, banner.BannerName, pullNum));
        }

        private void LogPullResult(GachaPullResult result)
        {
            string star = RarityStars(result.Rarity);
            string name = GetCompanionDisplayName(result.CompanionId);
            string dup = result.IsDuplicate ? " (DUP)" : " NEW!";
            _gachaLog.Insert(0, $"{star} {name}{dup}");
            if (_gachaLog.Count > 30) _gachaLog.RemoveAt(30);
        }

        private List<(string id, string name, Rarity rarity)> BuildWheelCompanions(BannerDefinitionSO banner)
        {
            var list = new List<(string, string, Rarity)>();
            void AddPool(List<string> pool, Rarity rarity)
            {
                foreach (var id in pool)
                    list.Add((id, GetCompanionDisplayName(id), rarity));
            }
            AddPool(banner.CommonPool, Rarity.Common);
            AddPool(banner.RarePool, Rarity.Rare);
            AddPool(banner.LegendaryPool, Rarity.Legendary);
            AddPool(banner.MythicPool, Rarity.Mythic);
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
            if (FiveStarIds.Contains(id)) return Rarity.Legendary;
            if (FourStarIds.Contains(id)) return Rarity.Rare;
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
            Rarity.Common => "★★★",
            Rarity.Rare => "★★★★",
            Rarity.Legendary => "★★★★★",
            Rarity.Mythic => "★★★★★★",
            _ => "★★★"
        };

        private static Color RarityToColor(Rarity r) => r switch
        {
            Rarity.Common => new Color(0.7f, 0.7f, 0.7f),
            Rarity.Uncommon => new Color(0.3f, 0.85f, 0.3f),
            Rarity.Rare => new Color(0.5f, 0.7f, 1f),
            Rarity.Epic => new Color(0.7f, 0.3f, 0.9f),
            Rarity.Legendary => new Color(1f, 0.85f, 0.3f),
            Rarity.Mythic => new Color(1f, 0.5f, 1f),
            _ => Color.white
        };

        private Color GetRarityColor(string itemId)
        {
            // Check if it's a weapon with known rarity
            if (_weaponDefs.TryGetValue(itemId, out var weapon))
                return WeaponRarityColor(weapon.Rarity);
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
            // Check the wish history for any pull of this companion
            foreach (var entry in _wishHistory)
            {
                if (entry.companionId == companionId)
                    return true;
            }
            // Fallback: check the gacha log for any pull of this companion
            string displayName = GetCompanionDisplayName(companionId);
            foreach (var entry in _gachaLog)
            {
                if (entry.Contains(displayName))
                    return true;
            }
            return false;
        }

        // ================================================================
        // Companion leveling helpers
        // ================================================================

        private int GetCompanionLevel(string id) => id != null ? _companionLevels.GetValueOrDefault(id, 1) : 1;

        private struct UpgradeCost
        {
            public int Gold;
            public int Essence;
            public string MaterialId;
            public int MaterialCount;
        }

        private static UpgradeCost GetUpgradeCost(int currentLevel, Rarity rarity)
        {
            float rarityMult = rarity switch
            {
                Rarity.Common => 1f,
                Rarity.Rare => 1.5f,
                Rarity.Legendary => 2f,
                Rarity.Mythic => 3f,
                _ => 1f
            };

            int gold = Mathf.RoundToInt(currentLevel * 100 * rarityMult);
            int essence = Mathf.RoundToInt(currentLevel * 5 * rarityMult);

            string materialId;
            int materialCount;
            if (currentLevel <= 10)
            {
                materialId = "herb_bundle";
                materialCount = 1 + currentLevel / 5;
            }
            else if (currentLevel <= 20)
            {
                materialId = "iron_ore";
                materialCount = 1 + (currentLevel - 10) / 5;
            }
            else if (currentLevel <= 30)
            {
                materialId = "fire_crystal";
                materialCount = 1 + (currentLevel - 20) / 5;
            }
            else if (currentLevel <= 40)
            {
                materialId = "runic_dust";
                materialCount = 1 + (currentLevel - 30) / 5;
            }
            else
            {
                materialId = "moonstone";
                materialCount = 1 + (currentLevel - 40) / 5;
            }

            return new UpgradeCost { Gold = gold, Essence = essence, MaterialId = materialId, MaterialCount = materialCount };
        }

        private void DoUpgrade(string companionId)
        {
            int currentLevel = GetCompanionLevel(companionId);
            if (currentLevel >= 45) return;

            int targetLevel = currentLevel + 1;
            if (_playerLevel < targetLevel)
            {
                Debug.Log($"[Upgrade] Need player level {targetLevel}");
                return;
            }

            Rarity rarity = GetCompanionRarity(companionId);
            var cost = GetUpgradeCost(currentLevel, rarity);

            if (_gold < cost.Gold) { Debug.Log("[Upgrade] Not enough gold"); return; }
            if (_accordEssence < cost.Essence) { Debug.Log("[Upgrade] Not enough essence"); return; }
            if (!_inventory.HasItem(cost.MaterialId, cost.MaterialCount)) { Debug.Log("[Upgrade] Not enough materials"); return; }

            _gold -= cost.Gold;
            _accordEssence -= cost.Essence;
            _inventory.RemoveItem(cost.MaterialId, cost.MaterialCount);
            _companionLevels[companionId] = targetLevel;

            // Respawn active companions at new level
            if (_sceneSetup != null)
            {
                if (companionId == _partyActiveId)
                    _sceneSetup.SetPartyCompanion("Active", companionId, targetLevel);
                if (companionId == _partySupportId)
                    _sceneSetup.SetPartyCompanion("Support", companionId, targetLevel);
            }
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

        // ================================================================
        // Guild rank helpers
        // ================================================================

        private static readonly string[] GuildRankNames = { "Initiate", "Bronze", "Silver", "Gold", "Platinum", "Legendary" };
        private static readonly int[] GuildRankThresholds = { 0, 100, 300, 600, 1000, 1500 };

        private string GetGuildRankName()
        {
            int idx = GetGuildRankIndex();
            return GuildRankNames[idx];
        }

        private int GetGuildRankIndex()
        {
            for (int i = GuildRankThresholds.Length - 1; i >= 0; i--)
            {
                if (_guildReputation >= GuildRankThresholds[i])
                    return i;
            }
            return 0;
        }

        private Color GetGuildRankColor()
        {
            return GetGuildRankIndex() switch
            {
                0 => new Color(0.7f, 0.7f, 0.7f),      // Initiate — gray
                1 => new Color(0.8f, 0.55f, 0.2f),      // Bronze
                2 => new Color(0.75f, 0.75f, 0.8f),     // Silver
                3 => new Color(1f, 0.85f, 0.2f),        // Gold
                4 => new Color(0.6f, 0.9f, 1f),         // Platinum
                5 => new Color(1f, 0.5f, 0.2f),         // Legendary
                _ => Color.white
            };
        }

        private QuestDefinitionSO CreateGuildContract(string name, string id, string description,
            int requiredRank, int reputationReward,
            (string desc, ObjectiveType objType, string targetId, int count)[] objectives,
            (string itemId, int qty, int xp, int gold)[] rewards)
        {
            var quest = CreateQuest(name, id, QuestType.GuildContract, description, objectives, rewards);
            _guildContractMeta[id] = (requiredRank, reputationReward);
            return quest;
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

        private void CreateTitle(string id, string name, string passive, string unlockCondition, StatBlock stats)
        {
            var def = ScriptableObject.CreateInstance<TitleDefinitionSO>();
            def.TitleId = id;
            def.TitleName = name;
            def.Description = passive;
            def.UnlockCondition = unlockCondition;
            def.StatBonuses = stats;
            _testTitles.Add(def);
        }
    }
}
