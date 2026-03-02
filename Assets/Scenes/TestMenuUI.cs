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
            // E is handled specially in Update() (NPC shop vs Equipment)
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

            // Update interaction prompt (always, even when a screen is open)
            UpdateInteractPrompt();

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
            text.text = "[I] Inventory  [E] NPC/Equipment  [C] Crafting  [G] Gacha  [P] Companions  [M] Map  [Q] Quests  [K] Skills  [`] Cheats  [ESC] Close";
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
                int lvl = GetCompanionLevel(id);
                return $"{RarityStars(rarities[idx])} {FormatItemName(id)}   {elements[idx]}  {classes[idx]}  Lv {lvl}";
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
            for (int i = 0; i < companionIds.Length; i++)
            {
                string cId = companionIds[i];
                bool owned = IsCompanionOwned(cId);
                string star = RarityStars(rarities[i]);
                int lvl = GetCompanionLevel(cId);
                Color color = owned ? RarityToColor(rarities[i]) : new Color(0.3f, 0.3f, 0.3f);

                if (!owned)
                {
                    string line = $"  {star} {FormatItemName(cId)}   {elements[i]}  {classes[i]}  [LOCKED]";
                    AddRowLabel(content, line, row, color);
                }
                else if (cId == _partyActiveId)
                {
                    string line = $"  {star} {FormatItemName(cId)}   {elements[i]}  {classes[i]}  Lv {lvl}  [ACTIVE]";
                    AddRowLabel(content, line, row, color);
                    string capturedId = cId;
                    AddUpgradeButton(content, row, capturedId);
                }
                else if (cId == _partySupportId)
                {
                    string line = $"  {star} {FormatItemName(cId)}   {elements[i]}  {classes[i]}  Lv {lvl}  [SUPPORT]";
                    AddRowLabel(content, line, row, color);
                    string capturedId = cId;
                    AddUpgradeButton(content, row, capturedId);
                }
                else
                {
                    string line = $"  {star} {FormatItemName(cId)}   {elements[i]}  {classes[i]}  Lv {lvl}";
                    AddRowLabel(content, line, row, color);
                    string capturedId = cId;
                    // Upgrade button at a fixed position (left of Active/Support)
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

            int idx = System.Array.IndexOf(companionIds, companionId);
            string displayName = FormatItemName(companionId);
            Rarity rarity = idx >= 0 ? rarities[idx] : Rarity.Common;
            string element = idx >= 0 ? elements[idx] : "None";
            string cls = idx >= 0 ? classes[idx] : "Unknown";
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

        private void ShowMap()
        {
            var panel = CreateScreenPanel("World Map");
            var content = CreateScrollContent(panel.transform, new Vector2(0f, 0f), new Vector2(1f, 0.9f));

            // Full GDD region data: Name, Biome, Element, LevelMin, LevelMax, Hub, Theme, Blight
            var regions = new[]
            {
                (name: "Greenreach Valley",   biome: "Temperate farmland / forest",       element: "Verdant", lvMin: 1,  lvMax: 10, hub: "Thornwall",         theme: "Loss, beginnings, the farming past",    blight: 0.05f),
                (name: "The Ashen Steppe",    biome: "Scorched plains / dried riverbeds", element: "Pyro",    lvMin: 10, lvMax: 18, hub: "Dusthaven Outpost", theme: "Drought, survival",                     blight: 0.25f),
                (name: "Gloomtide Marshes",   biome: "Swampland / bioluminescent fungi",  element: "Umbral",  lvMin: 16, lvMax: 24, hub: "Fenwick Village",   theme: "Decay, hidden beauty",                  blight: 0.40f),
                (name: "Frosthollow Peaks",   biome: "Frozen mountains / alpine tundra",  element: "Cryo",    lvMin: 22, lvMax: 32, hub: "Ironpeak Fortress", theme: "Isolation, endurance",                  blight: 0.15f),
                (name: "The Withered Heart",  biome: "Corrupted wasteland / twisted",     element: "None",    lvMin: 30, lvMax: 40, hub: "Varek's Sanctum",   theme: "Culmination, sacrifice",                blight: 0.80f),
            };

            // GDD 8.2 — Enemies per region: (name, level, category, tier, element)
            var regionEnemies = new Dictionary<string, (string name, int level, string category, string tier, string element)[]>
            {
                ["Greenreach Valley"] = new[]
                {
                    ("Withered Wolf",      2,  "Withered Beast",   "Tier 1", "None"),
                    ("Blight Beetle",      3,  "Blight Spawn",     "Tier 1", "Verdant"),
                    ("Corrupted Farmhand", 5,  "Corrupted Human",  "Tier 2", "None"),
                    ("Wither Stag",        8,  "Withered Beast",   "Elite",  "Verdant"),
                },
                ["The Ashen Steppe"] = new[]
                {
                    ("Dustcrawler",        11, "Withered Beast",   "Tier 1", "Pyro"),
                    ("Scorched Viper",     13, "The Untamed",      "Tier 1", "Pyro"),
                    ("Acolyte Ranger",     15, "Varek's Acolytes", "Tier 2", "None"),
                    ("Ashwalker Golem",    17, "Constructs",       "Elite",  "Pyro"),
                },
                ["Gloomtide Marshes"] = new[]
                {
                    ("Bogfiend",           17, "Blight Spawn",     "Tier 1", "Umbral"),
                    ("Sporecap Horror",    19, "Blight Spawn",     "Tier 1", "Verdant"),
                    ("Drowned Sentinel",   21, "Corrupted Human",  "Tier 2", "Umbral"),
                    ("The Mire Queen",     23, "Blight Spawn",     "Elite",  "Umbral"),
                },
                ["Frosthollow Peaks"] = new[]
                {
                    ("Frostwight",         23, "Withered Beast",   "Tier 1", "Cryo"),
                    ("Glacial Construct",  26, "Constructs",       "Tier 1", "Cryo"),
                    ("Acolyte Warder",     28, "Varek's Acolytes", "Tier 2", "None"),
                    ("Avalanche Beast",    31, "Withered Beast",   "Elite",  "Cryo"),
                },
                ["The Withered Heart"] = new[]
                {
                    ("Hollow Shade",       32, "Blight Spawn",     "Tier 1", "None"),
                    ("Rootwraith",         35, "Withered Beast",   "Tier 1", "Verdant"),
                    ("Wither Knight",      37, "Corrupted Human",  "Tier 2", "None"),
                    ("Blight Colossus",    39, "Blight Spawn",     "Elite",  "None"),
                },
            };

            // GDD 9 — Bosses per region: (name, level, phases, mechanic, isOptional)
            var regionBosses = new Dictionary<string, (string name, int level, int phases, string mechanic, bool optional)[]>
            {
                ["Greenreach Valley"] = new[]
                {
                    ("The Rootmother",     10, 2, "Vine traps; burn root nodes in P2",                  false),
                    ("The Scarecrow King", 12, 2, "Decoy copies; real one identified by shadow",        true),
                },
                ["The Ashen Steppe"] = new[]
                {
                    ("Cindermaw",          18, 2, "Fire AoE shrinks arena; douse with water",           false),
                    ("Oasis Phantom",      20, 2, "Mirage attacks; illusions vanish when approached",   true),
                },
                ["Gloomtide Marshes"] = new[]
                {
                    ("The Mire Sovereign", 24, 3, "Submerges; drain water via valves",                  false),
                    ("Grandmother Spore",  26, 3, "Toxic clouds; wind mechanics for safe zones",        true),
                },
                ["Frosthollow Peaks"] = new[]
                {
                    ("Frostfang, the Bound", 32, 3, "Chained beast; breaking chains changes moveset",  false),
                    ("The Frozen Accord",    34, 3, "Immune to magic P1, physical P2, vulnerable P3",   true),
                },
                ["The Withered Heart"] = new[]
                {
                    ("Varek Ashwood",      38, 3, "Mirrors player abilities",                           false),
                    ("The Hollow Mother",  40, 3, "Reality-warping arena",                              true),
                    ("Primordial Seed",    40, 3, "Secret boss — reality-warping arena",                true),
                },
            };

            // GDD 6.1 — Exploration features: (harvest, restZone, hiddenGrove)
            var regionExploration = new Dictionary<string, (string harvest, string restZone, string hiddenGrove)>
            {
                ["Greenreach Valley"]  = ("Herb patches, Ancient seeds",          "Thornwall Inn Clearing",       "Requires Root Sense — Eldergrove Hollow"),
                ["The Ashen Steppe"]   = ("Fire minerals, Charred bark",          "Dusthaven Camp",              "Requires Flame Walk — Ember Hollow"),
                ["Gloomtide Marshes"]  = ("Luminescent fungi, Swamp resin",       "Fenwick Lantern Rest",        "Requires Mire Sight — Glowspore Cavern"),
                ["Frosthollow Peaks"]  = ("Froststone ore, Alpine lichen",        "Ironpeak Base Camp",          "Requires Frost Step — Crystal Grotto"),
                ["The Withered Heart"] = ("Blightite shards, Withered essence",   "Sanctum Threshold",           "Requires Verdant Bloom — Heart's Memory"),
            };

            // Main storyline NPCs per region
            var regionNpcs = new Dictionary<string, (string name, string role)[]>
            {
                ["Greenreach Valley"] = new[]
                {
                    ("Elder Mirren",   "Village Elder — quest giver"),
                    ("Healer Maren",   "Village Healer — herbalist"),
                    ("Captain Aldric", "Militia Captain — blight investigation"),
                    ("Warden Sable",   "Forest Warden — patrols the border"),
                },
                ["The Ashen Steppe"] = new[]
                {
                    ("Forgemaster Bram", "Guild Forgemaster — crafting master"),
                    ("Watcher Sera",     "Outpost Scout — tracks blight spread"),
                    ("Lyra",             "Forest Warden — memory fragments"),
                },
                ["Gloomtide Marshes"] = new[]
                {
                    ("Oracle Nyx",     "Marsh Seer — blight origin visions"),
                    ("Ranger Theron",  "Marsh Guide — safe passage"),
                    ("Selene",         "Moonlight Priestess — purification rites"),
                },
                ["Frosthollow Peaks"] = new[]
                {
                    ("Scholar Veylin",   "Blight Researcher — ancient texts"),
                    ("Sentinel Kaelos",  "Mountain Guardian — peak passage"),
                    ("Thorne",           "Wandering Sellsword — hired blade"),
                },
                ["The Withered Heart"] = new[]
                {
                    ("The Blightcaller", "Source of the corruption"),
                    ("Eldara",           "Spirit of the Verdant — ancient guardian"),
                    ("Echo of Mirren",   "Spectral guide — final trial"),
                },
            };

            // Category colors for enemy types
            Color CategoryColor(string category) => category switch
            {
                "Withered Beast"   => new Color(0.6f, 0.3f, 0.15f),  // Dark brown
                "Blight Spawn"     => new Color(0.5f, 0.8f, 0.2f),   // Sickly green
                "Corrupted Human"  => new Color(0.7f, 0.5f, 0.7f),   // Muted purple
                "The Untamed"      => new Color(0.8f, 0.7f, 0.4f),   // Tawny/natural
                "Varek's Acolytes" => new Color(0.9f, 0.3f, 0.3f),   // Crimson
                "Constructs"       => new Color(0.5f, 0.6f, 0.7f),   // Steel gray
                _ => Color.white,
            };

            Color npcColor = new Color(0.85f, 0.75f, 1f);
            Color companionNpcColor = new Color(1f, 0.85f, 0.3f);
            Color headerColor = new Color(0.95f, 0.85f, 0.5f);
            Color dimColor = new Color(0.55f, 0.55f, 0.55f);
            Color bossMainColor = RarityToColor(Rarity.Legendary);   // Gold
            Color bossOptColor = RarityToColor(Rarity.Mythic);       // Mythic purple

            int row = 0;
            foreach (var r in regions)
            {
                // Region header
                Color nameColor = r.blight > 0.5f ? new Color(0.8f, 0.3f, 0.3f) : new Color(0.5f, 1f, 0.5f);
                AddRowLabel(content, $"  {r.name}   (Lv {r.lvMin}-{r.lvMax})   [{r.element}]", row, nameColor);
                row++;
                AddRowLabel(content, $"    {r.biome}", row, new Color(0.7f, 0.7f, 0.7f));
                row++;
                AddRowLabel(content, $"    Hub: {r.hub}   |   Theme: {r.theme}", row, dimColor);
                row++;

                // Blight bar
                int barWidth = 20;
                int filled = Mathf.RoundToInt(r.blight * barWidth);
                string bar = "[" + new string('#', filled) + new string('-', barWidth - filled) + "]";
                Color blightColor = Color.Lerp(new Color(0.3f, 0.8f, 0.3f), new Color(0.9f, 0.2f, 0.2f), r.blight);
                AddRowLabel(content, $"    Blight: {bar} {r.blight * 100:F0}%", row, blightColor);
                row++;

                AddRowLabel(content, "", row, Color.white); row++;

                // --- Enemies ---
                if (regionEnemies.TryGetValue(r.name, out var enemies))
                {
                    AddRowLabel(content, "    --- Enemies ---", row, headerColor); row++;
                    foreach (var (eName, eLv, eCat, eTier, eElem) in enemies)
                    {
                        Color enemyColor = CategoryColor(eCat);
                        if (eTier == "Elite") enemyColor = Color.Lerp(enemyColor, Color.white, 0.3f);
                        string elemTag = eElem != "None" ? $"  [{eElem}]" : "";
                        AddRowLabel(content, $"    [{eTier}] {eName} (Lv {eLv})       {eCat}{elemTag}", row, enemyColor);
                        row++;
                    }
                    AddRowLabel(content, "", row, Color.white); row++;
                }

                // --- Bosses ---
                if (regionBosses.TryGetValue(r.name, out var bosses))
                {
                    AddRowLabel(content, "    --- Bosses ---", row, headerColor); row++;
                    foreach (var (bName, bLv, bPhases, bMech, bOpt) in bosses)
                    {
                        Color bColor = bOpt ? bossOptColor : bossMainColor;
                        string tag = bOpt ? "[Optional]" : "[Main]";
                        AddRowLabel(content, $"    {tag}  {bName} (Lv {bLv})  {bPhases} Phases — {bMech}", row, bColor);
                        row++;
                    }
                    AddRowLabel(content, "", row, Color.white); row++;
                }

                // --- Exploration ---
                if (regionExploration.TryGetValue(r.name, out var explore))
                {
                    AddRowLabel(content, "    --- Exploration ---", row, headerColor); row++;
                    AddRowLabel(content, $"    Harvest: {explore.harvest}", row, new Color(0.6f, 0.85f, 0.5f)); row++;
                    AddRowLabel(content, $"    Rest Zone: {explore.restZone}", row, new Color(0.7f, 0.8f, 1f)); row++;
                    AddRowLabel(content, $"    Hidden Grove: {explore.hiddenGrove}", row, new Color(0.9f, 0.7f, 1f)); row++;
                    AddRowLabel(content, "", row, Color.white); row++;
                }

                // --- NPCs ---
                if (regionNpcs.TryGetValue(r.name, out var npcs))
                {
                    AddRowLabel(content, "    --- NPCs ---", row, headerColor); row++;
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

                // Spacer between regions
                AddRowLabel(content, "", row, Color.white); row++;
                AddRowLabel(content, "  ────────────────────────────────────────────", row, dimColor); row++;
                AddRowLabel(content, "", row, Color.white); row++;
            }

            SetContentHeight(content, row);
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
                string[] allCompanions = {
                    "companion_villager", "companion_farmer", "companion_scout", "companion_apprentice",
                    "companion_knight", "companion_pyromancer", "companion_ranger", "companion_priest",
                    "companion_lyra", "companion_thorne", "companion_selene", "companion_eldara"
                };
                foreach (var id in allCompanions)
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
            // Check the gacha log for any pull of this companion
            foreach (var entry in _gachaLog)
            {
                if (entry.Contains(FormatItemName(companionId)))
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
    }
}
