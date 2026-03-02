using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SoR.AI;
using SoR.Combat;
using SoR.Core;
using SoR.Gameplay;
using SoR.Shared;

namespace SoR.Testing
{
    /// <summary>
    /// Attach to a single empty GameObject in a blank scene.
    /// Builds the entire playable test level at runtime:
    /// ground, player, enemies, camera, HUD, combat wiring, and floating damage numbers.
    /// </summary>
    public class TestSceneSetup : MonoBehaviour
    {
        // ---- runtime references ----
        private GameObject _player;
        private PlayerController _playerController;
        private PlayerStatsSO _playerStats;
        private WeaponDefinitionSO _testWeapon;
        private Camera _mainCamera;

        // ---- HUD ----
        private Image _healthFill;
        private Image _verdanceFill;

        // ---- player resources (tracked here; PlayerController has no IDamageable) ----
        private float _playerHealth;
        private float _playerMaxHealth;
        private float _playerVerdance;
        private float _playerMaxVerdance;

        // Public accessors for cheat menu
        public float PlayerHealth { get => _playerHealth; set => _playerHealth = Mathf.Clamp(value, 0f, _playerMaxHealth); }
        public float PlayerMaxHealth { get => _playerMaxHealth; set { _playerMaxHealth = value; _playerHealth = Mathf.Min(_playerHealth, value); } }
        public float PlayerVerdance { get => _playerVerdance; set => _playerVerdance = Mathf.Clamp(value, 0f, _playerMaxVerdance); }
        public float PlayerMaxVerdance { get => _playerMaxVerdance; set { _playerMaxVerdance = value; _playerVerdance = Mathf.Min(_playerVerdance, value); } }
        public PlayerStatsSO PlayerStats => _playerStats;
        public WeaponDefinitionSO TestWeapon => _testWeapon;
        public void SetActiveWeapon(WeaponDefinitionSO weapon) { if (weapon != null) _testWeapon = weapon; }
        public bool GodMode { get; set; }
        public bool OneHitKill { get; set; }
        public Transform PlayerTransform => _player != null ? _player.transform : null;

        // ---- enemies ----
        private readonly List<EnemyEntry> _enemies = new();

        /// <summary>Kill all currently alive enemies.</summary>
        public void KillAllEnemies()
        {
            foreach (var entry in _enemies)
            {
                if (entry.AI != null && entry.AI.IsAlive)
                {
                    var payload = new DamagePayload(entry.AI.CurrentHealth + 999f, DamageType.Physical, Element.None, false, 0f);
                    entry.AI.TakeDamage(payload, entry.AI.transform.position);
                }
            }
            Debug.Log("[Cheat] All enemies killed");
        }

        /// <summary>Reset all enemies to full health at their spawn positions.</summary>
        public void RespawnAllEnemies()
        {
            foreach (var entry in _enemies)
            {
                if (entry.AI != null)
                    entry.AI.ResetHealth();
            }
            Debug.Log("[Cheat] All enemies respawned");
        }

        // ---- companions ----
        private readonly List<CompanionEntry> _companions = new();
        private WeaponDefinitionSO _companionWeapon;

        // ---- attack hit detection ----
        private readonly HashSet<int> _hitThisSwing = new();
        private bool _wasAttacking;

        // ---- gravity ----
        private float _verticalVelocity;

        // ---- camera ----
        private readonly Vector3 _cameraOffset = new Vector3(0f, 18f, -12f);

        // ---- biome zones (name, center, halfExtent, blightLevel, groundColor) ----
        private struct BiomeZone
        {
            public string Name;
            public Vector3 Center;
            public float HalfExtent; // square zone: center ± halfExtent on X and Z
            public float Height;     // ground Y elevation
            public float Blight;     // 0..1
            public Color GroundColor;
        }

        private BiomeZone[] _biomes;

        // ---- blight HUD ----
        private Image _blightFill;
        private Text _blightLabel;
        private Text _blightZoneName;
        private float _currentBlight;
        private float _displayedBlight;  // smoothly lerps toward _currentBlight
        private string _currentZoneName = "Wilderness";

        // ---- cached font ----
        private Font _font;

        private struct EnemyEntry
        {
            public EnemyAIController AI;
            public Image HealthFill;
            public Transform HealthBarCanvas;
        }

        private struct CompanionEntry
        {
            public string CompanionId;
            public string Slot;
            public int Level;
            public GameObject Root;
            public TestCompanionBehavior Behavior;
        }

        // ---- NPCs (shop keepers) ----
        public struct NpcEntry
        {
            public string Name;
            public string ShopId;
            public string ShopRole;
            public Transform Root;
            public Transform NametagCanvas;
        }
        private readonly List<NpcEntry> _npcList = new();

        // ---- party passive buffs ----
        private float _partyDamageBonus = 1f;     // multiplier (1.0 = no bonus)
        private float _partyCooldownReduction = 0f; // seconds

        public float PartyDamageBonus => _partyDamageBonus;
        public float PartyCooldownReduction => _partyCooldownReduction;

        // ================================================================
        // Lifecycle
        // ================================================================

        private void Start()
        {
            _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (_font == null)
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            SetupCombatSystem();
            CreateGround();
            CreatePlayer();
            CreateEnemies();
            CreateTown();
            CreateNPCs();
            CreateCompanionWeapon();
            SetupCamera();
            CreateHUD();
            SubscribeEvents();

            // Attach menu UI system (all screens openable via keyboard)
            gameObject.AddComponent<TestMenuUI>();
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<DamageDealtEvent>(OnDamageDealt);
            EventBus.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);

            // Cleanup companion GOs
            foreach (var entry in _companions)
            {
                if (entry.Root != null)
                    Destroy(entry.Root);
            }
            _companions.Clear();
        }

        private void Update()
        {
            ApplyGravity();
            UpdateCamera();
            UpdateHUD();
            UpdateEnemyHealthBars();
            UpdateNPCNametags();
            CheckPlayerAttackHits();
        }

        private void ApplyGravity()
        {
            if (_player == null) return;
            var cc = _player.GetComponent<CharacterController>();
            if (cc == null) return;

            if (cc.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -0.5f; // small downward to stay grounded
            else
                _verticalVelocity -= 20f * Time.deltaTime; // gravity

            cc.Move(new Vector3(0f, _verticalVelocity * Time.deltaTime, 0f));
        }

        // ================================================================
        // Asset loading helpers
        // ================================================================

        private static T TryLoadAsset<T>(string path) where T : Object
        {
            return Resources.Load<T>(path);
        }

        /// <summary>
        /// Strips all colliders from a GameObject hierarchy so loaded prefabs
        /// don't conflict with the CharacterController / BoxCollider added at runtime.
        /// </summary>
        private static void StripColliders(GameObject go)
        {
            foreach (var col in go.GetComponentsInChildren<Collider>())
                Object.Destroy(col);
        }

        // ================================================================
        // Scene construction
        // ================================================================

        private void SetupCombatSystem()
        {
            var go = new GameObject("CombatSystem");
            go.AddComponent<CombatSystem>(); // Registers via ServiceLocator in Awake
        }

        private void CreateGround()
        {
            // Try custom prefab first
            var groundPrefab = TryLoadAsset<GameObject>("TestAssets/Ground/GroundPrefab");
            if (groundPrefab != null)
            {
                var ground = Instantiate(groundPrefab);
                ground.name = "Ground";
                ground.transform.position = Vector3.zero;
                InitBiomeZones();
                return;
            }

            InitBiomeZones();

            // Create a large base plane (200x200)
            var basePlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            basePlane.name = "Ground";
            basePlane.transform.position = Vector3.zero;
            basePlane.transform.localScale = new Vector3(20f, 1f, 20f); // 200x200
            basePlane.GetComponent<Renderer>().material = CreateMaterial(new Color(0.25f, 0.2f, 0.15f)); // dark earth base

            // Create colored biome planes at their respective heights
            foreach (var biome in _biomes)
            {
                var biomePlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
                biomePlane.name = $"Biome_{biome.Name.Replace(" ", "_")}";
                biomePlane.transform.position = new Vector3(biome.Center.x, biome.Height, biome.Center.z);
                // Each biome zone is 2*halfExtent wide; plane default is 10 units, so scale = halfExtent*2/10
                float planeScale = biome.HalfExtent * 2f / 10f;
                biomePlane.transform.localScale = new Vector3(planeScale, 1f, planeScale);

                // Tint biome color darker with blight
                Color tinted = Color.Lerp(biome.GroundColor, new Color(0.15f, 0.05f, 0.1f), biome.Blight * 0.6f);
                biomePlane.GetComponent<Renderer>().material = CreateMaterial(tinted);
            }
        }

        private void InitBiomeZones()
        {
            // Layout on 200x200 map:
            //   Greenreach Valley  — center (player spawn area)
            //   Ashen Steppe       — southwest
            //   Gloomtide Marshes  — southeast
            //   Frosthollow Peaks  — northwest
            //   Withered Heart     — north (far, dangerous)
            _biomes = new[]
            {
                new BiomeZone { Name = "Greenreach Valley",  Center = new Vector3(0f, 0f, 0f),     HalfExtent = 35f, Height = 0f, Blight = 0.05f, GroundColor = new Color(0.35f, 0.55f, 0.3f) },
                new BiomeZone { Name = "The Ashen Steppe",   Center = new Vector3(-55f, 0f, -55f),  HalfExtent = 30f, Height = 0f, Blight = 0.25f, GroundColor = new Color(0.55f, 0.35f, 0.2f) },
                new BiomeZone { Name = "Gloomtide Marshes",  Center = new Vector3(55f, 0f, -55f),   HalfExtent = 30f, Height = 0f, Blight = 0.40f, GroundColor = new Color(0.2f, 0.35f, 0.25f) },
                new BiomeZone { Name = "Frosthollow Peaks",  Center = new Vector3(-55f, 0f, 55f),   HalfExtent = 30f, Height = 0f, Blight = 0.15f, GroundColor = new Color(0.6f, 0.7f, 0.8f) },
                new BiomeZone { Name = "The Withered Heart",  Center = new Vector3(55f, 0f, 55f),    HalfExtent = 30f, Height = 0f, Blight = 0.80f, GroundColor = new Color(0.3f, 0.15f, 0.2f) },
            };
        }

        /// <summary>Returns the blight level (0..1) for the biome the given position is in.</summary>
        private float GetBlightAtPosition(Vector3 pos)
        {
            if (_biomes == null) return 0f;
            foreach (var b in _biomes)
            {
                if (Mathf.Abs(pos.x - b.Center.x) <= b.HalfExtent &&
                    Mathf.Abs(pos.z - b.Center.z) <= b.HalfExtent)
                {
                    _currentZoneName = b.Name;
                    return b.Blight;
                }
            }
            _currentZoneName = "Wilderness";
            return 0f;
        }

        // ================================================================
        // Town (market area in Greenreach Valley)
        // ================================================================

        private void CreateTown()
        {
            CreateTownDecor();
            CreateGuildHall();

            // Stalls for each NPC — position is the NPC's position, stall is built behind them
            // Row 1 (north): Silas (left, facing right), Maren (right, facing left)
            CreateTownStall(new Vector3(-6f, 0f, -7f), true,
                new Color(0.25f, 0.55f, 0.2f), new Color(0.35f, 0.65f, 0.3f), "Silas_Stall");
            CreateTownStall(new Vector3(6f, 0f, -7f), false,
                new Color(0.45f, 0.65f, 0.35f), new Color(0.55f, 0.75f, 0.45f), "Maren_Stall");

            // Row 2 (middle): Bram (left, facing right), Voss (right, facing left)
            CreateTownStall(new Vector3(-6f, 0f, -15f), true,
                new Color(0.6f, 0.35f, 0.15f), new Color(0.7f, 0.45f, 0.2f), "Bram_Stall");
            CreateTownStall(new Vector3(6f, 0f, -15f), false,
                new Color(0.35f, 0.5f, 0.7f), new Color(0.45f, 0.6f, 0.8f), "Voss_Stall");

            // Row 3 (south): Enna (left, facing right), Whisperer (right, facing left)
            CreateTownStall(new Vector3(-8f, 0f, -22f), true,
                new Color(0.2f, 0.55f, 0.5f), new Color(0.3f, 0.65f, 0.6f), "Enna_Stall");
            CreateTownStall(new Vector3(8f, 0f, -22f), false,
                new Color(0.35f, 0.15f, 0.45f), new Color(0.45f, 0.2f, 0.55f), "Whisperer_Stall");
        }

        private void CreateGuildHall()
        {
            var parent = new GameObject("GuildHall");
            parent.transform.position = new Vector3(0f, 0f, -2f);

            // Back wall (north)
            var backWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backWall.name = "BackWall";
            backWall.transform.SetParent(parent.transform, false);
            backWall.transform.localPosition = new Vector3(0f, 2f, 2.5f);
            backWall.transform.localScale = new Vector3(8f, 4f, 0.4f);
            backWall.GetComponent<Renderer>().material = CreateMaterial(new Color(0.45f, 0.45f, 0.48f)); // stone gray

            // Left wall
            var leftWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftWall.name = "LeftWall";
            leftWall.transform.SetParent(parent.transform, false);
            leftWall.transform.localPosition = new Vector3(-3.8f, 2f, 0.5f);
            leftWall.transform.localScale = new Vector3(0.4f, 4f, 4.5f);
            leftWall.GetComponent<Renderer>().material = CreateMaterial(new Color(0.45f, 0.45f, 0.48f));

            // Right wall
            var rightWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightWall.name = "RightWall";
            rightWall.transform.SetParent(parent.transform, false);
            rightWall.transform.localPosition = new Vector3(3.8f, 2f, 0.5f);
            rightWall.transform.localScale = new Vector3(0.4f, 4f, 4.5f);
            rightWall.GetComponent<Renderer>().material = CreateMaterial(new Color(0.45f, 0.45f, 0.48f));

            // Roof
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Roof";
            roof.transform.SetParent(parent.transform, false);
            roof.transform.localPosition = new Vector3(0f, 4.2f, 0.5f);
            roof.transform.localScale = new Vector3(9f, 0.3f, 5.5f);
            roof.GetComponent<Renderer>().material = CreateMaterial(new Color(0.4f, 0.25f, 0.12f)); // brown

            // Floor
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(parent.transform, false);
            floor.transform.localPosition = new Vector3(0f, 0.05f, 0.5f);
            floor.transform.localScale = new Vector3(7.6f, 0.1f, 4.5f);
            floor.GetComponent<Renderer>().material = CreateMaterial(new Color(0.3f, 0.2f, 0.1f)); // dark wood

            // Entrance pillars
            var pillarMat = CreateMaterial(new Color(0.5f, 0.5f, 0.52f));
            var leftPillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftPillar.name = "LeftPillar";
            leftPillar.transform.SetParent(parent.transform, false);
            leftPillar.transform.localPosition = new Vector3(-2.5f, 2f, -1.5f);
            leftPillar.transform.localScale = new Vector3(0.5f, 4f, 0.5f);
            leftPillar.GetComponent<Renderer>().material = pillarMat;

            var rightPillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightPillar.name = "RightPillar";
            rightPillar.transform.SetParent(parent.transform, false);
            rightPillar.transform.localPosition = new Vector3(2.5f, 2f, -1.5f);
            rightPillar.transform.localScale = new Vector3(0.5f, 4f, 0.5f);
            rightPillar.GetComponent<Renderer>().material = pillarMat;

            // Banner/sign above entrance
            var banner = GameObject.CreatePrimitive(PrimitiveType.Cube);
            banner.name = "GuildBanner";
            banner.transform.SetParent(parent.transform, false);
            banner.transform.localPosition = new Vector3(0f, 3.5f, -1.5f);
            banner.transform.localScale = new Vector3(2f, 1f, 0.1f);
            banner.GetComponent<Renderer>().material = CreateMaterial(new Color(0.85f, 0.7f, 0.2f)); // gold
        }

        /// <summary>
        /// Creates a single shop stall: back wall, counter, and awning.
        /// facingRight means the stall opens toward +X (NPC faces +X).
        /// </summary>
        private void CreateTownStall(Vector3 npcPos, bool facingRight, Color wallColor, Color roofColor, string name)
        {
            var parent = new GameObject(name);
            parent.transform.position = npcPos;

            // Direction multiplier: stall structure is behind the NPC
            float dir = facingRight ? -1f : 1f;

            // Back wall — behind the NPC
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
            wall.transform.SetParent(parent.transform, false);
            wall.transform.localPosition = new Vector3(dir * 1.5f, 1.25f, 0f);
            wall.transform.localScale = new Vector3(0.3f, 2.5f, 3f);
            wall.GetComponent<Renderer>().material = CreateMaterial(wallColor);

            // Counter/table — in front of the wall, between NPC and player
            var counter = GameObject.CreatePrimitive(PrimitiveType.Cube);
            counter.name = "Counter";
            counter.transform.SetParent(parent.transform, false);
            counter.transform.localPosition = new Vector3(dir * -0.5f, 0.4f, 0f);
            counter.transform.localScale = new Vector3(1f, 0.8f, 2.5f);
            counter.GetComponent<Renderer>().material = CreateMaterial(Color.Lerp(wallColor, Color.white, 0.2f));

            // Awning/roof — above the stall
            var awning = GameObject.CreatePrimitive(PrimitiveType.Cube);
            awning.name = "Awning";
            awning.transform.SetParent(parent.transform, false);
            awning.transform.localPosition = new Vector3(0f, 2.8f, 0f);
            awning.transform.localScale = new Vector3(2f, 0.1f, 3.5f);
            awning.GetComponent<Renderer>().material = CreateMaterial(roofColor);
        }

        /// <summary>
        /// Creates town decorations: ground tile, central well, corner posts.
        /// </summary>
        private void CreateTownDecor()
        {
            // Town square ground tile — lighter cobblestone color (extended north to cover guild hall)
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "TownSquareGround";
            ground.transform.position = new Vector3(0f, 0.01f, -12f);
            ground.transform.localScale = new Vector3(2.4f, 1f, 2.4f); // 24x24 units (Z=0 to Z=-24)
            ground.GetComponent<Renderer>().material = CreateMaterial(new Color(0.55f, 0.5f, 0.4f));

            // Central well/fountain
            var well = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            well.name = "TownWell";
            well.transform.position = new Vector3(0f, 0.3f, -12f);
            well.transform.localScale = new Vector3(1.5f, 0.6f, 1.5f);
            well.GetComponent<Renderer>().material = CreateMaterial(new Color(0.5f, 0.5f, 0.5f));

            // Well rim — slightly larger cylinder on top
            var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rim.name = "WellRim";
            rim.transform.position = new Vector3(0f, 0.6f, -12f);
            rim.transform.localScale = new Vector3(1.7f, 0.1f, 1.7f);
            rim.GetComponent<Renderer>().material = CreateMaterial(new Color(0.4f, 0.4f, 0.42f));

            // Corner posts at the 6 corners of the market area (including north for guild hall)
            var postColor = CreateMaterial(new Color(0.45f, 0.35f, 0.25f));
            Vector3[] postPositions =
            {
                new Vector3(-11f, 0.75f, 1f),   // north-left
                new Vector3(11f, 0.75f, 1f),    // north-right
                new Vector3(-11f, 0.75f, -4f),
                new Vector3(11f, 0.75f, -4f),
                new Vector3(-11f, 0.75f, -25f),
                new Vector3(11f, 0.75f, -25f),
            };
            for (int i = 0; i < postPositions.Length; i++)
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                post.name = $"CornerPost_{i}";
                post.transform.position = postPositions[i];
                post.transform.localScale = new Vector3(0.4f, 1.5f, 0.4f);
                post.GetComponent<Renderer>().material = postColor;
            }
        }

        // ================================================================
        // NPCs (shop keepers)
        // ================================================================

        private void CreateNPCs()
        {
            // Guildmaster inside the Guild Hall
            SpawnNPC("Guildmaster Aldric",  "adventure_guild",     "Guildmaster",          new Vector3(0f, 0f, 0f),     new Color(0.7f, 0.55f, 0.2f));

            // All NPCs now in Greenreach Valley town market
            SpawnNPC("Silas",               "seed_merchant",       "Seed Merchant",        new Vector3(-6f, 0f, -7f),   new Color(0.3f, 0.75f, 0.3f));
            SpawnNPC("Maren",               "general_store",       "General Store",        new Vector3(6f, 0f, -7f),    new Color(0.6f, 0.9f, 0.5f));
            SpawnNPC("Bram",                "brams_forge",         "Blacksmith",           new Vector3(-6f, 0f, -15f),  new Color(0.8f, 0.5f, 0.25f));
            SpawnNPC("Quartermaster Voss",  "guild_quartermaster", "Guild Quartermaster",  new Vector3(6f, 0f, -15f),   new Color(0.45f, 0.6f, 0.8f));
            SpawnNPC("Druid Enna",          "wandering_druid",     "Wandering Druid",      new Vector3(-8f, 0f, -22f),  new Color(0.3f, 0.7f, 0.65f));
            SpawnNPC("The Whisperer",       "black_market",        "Black Market",         new Vector3(8f, 0f, -22f),   new Color(0.45f, 0.2f, 0.55f));
        }

        private void SpawnNPC(string npcName, string shopId, string shopRole, Vector3 position, Color color)
        {
            var go = new GameObject($"NPC_{npcName}");
            go.transform.position = position;

            // Visual — capsule (distinct from cube enemies)
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "NPCModel";
            visual.transform.SetParent(go.transform, false);
            visual.transform.localPosition = new Vector3(0f, 1f, 0f);
            visual.transform.localScale = new Vector3(1f, 1f, 1f); // 2 units tall (capsule default height = 2)
            Object.Destroy(visual.GetComponent<CapsuleCollider>());
            visual.GetComponent<Renderer>().material = CreateMaterial(color);

            // Collider for interaction detection
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1f, 0f);
            col.size = new Vector3(1.2f, 2.2f, 1.2f);
            col.isTrigger = true;

            // Nametag
            var nametagCanvas = CreateNPCNametag(go.transform, npcName, shopRole, color);

            _npcList.Add(new NpcEntry
            {
                Name = npcName,
                ShopId = shopId,
                ShopRole = shopRole,
                Root = go.transform,
                NametagCanvas = nametagCanvas
            });
        }

        private Transform CreateNPCNametag(Transform parent, string npcName, string shopRole, Color color)
        {
            float barY = 2.8f;

            var canvasGo = new GameObject("NPCNametag");
            canvasGo.transform.SetParent(parent, false);
            canvasGo.transform.localPosition = new Vector3(0f, barY, 0f);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGo.AddComponent<CanvasScaler>();

            var rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(200f, 45f);
            rt.localScale = Vector3.one * 0.01f;

            // Name text (top)
            var nameGo = new GameObject("Name");
            nameGo.transform.SetParent(canvasGo.transform, false);
            var nameText = nameGo.AddComponent<Text>();
            nameText.text = npcName;
            nameText.font = _font;
            nameText.fontSize = 15;
            nameText.color = Color.white;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
            var nameRt = nameGo.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0f, 0.5f);
            nameRt.anchorMax = Vector2.one;
            nameRt.offsetMin = Vector2.zero;
            nameRt.offsetMax = Vector2.zero;

            // Role text (bottom)
            var roleGo = new GameObject("Role");
            roleGo.transform.SetParent(canvasGo.transform, false);
            var roleText = roleGo.AddComponent<Text>();
            roleText.text = $"[{shopRole}]";
            roleText.font = _font;
            roleText.fontSize = 12;
            roleText.color = new Color(1f, 0.85f, 0.3f);
            roleText.alignment = TextAnchor.MiddleCenter;
            roleText.horizontalOverflow = HorizontalWrapMode.Overflow;
            var roleRt = roleGo.GetComponent<RectTransform>();
            roleRt.anchorMin = Vector2.zero;
            roleRt.anchorMax = new Vector2(1f, 0.5f);
            roleRt.offsetMin = Vector2.zero;
            roleRt.offsetMax = Vector2.zero;

            return canvasGo.transform;
        }

        private void UpdateNPCNametags()
        {
            if (_mainCamera == null) return;
            foreach (var npc in _npcList)
            {
                if (npc.NametagCanvas != null)
                    npc.NametagCanvas.forward = _mainCamera.transform.forward;
            }
        }

        /// <summary>
        /// Returns the nearest NPC within maxRange of the player, or null if none.
        /// </summary>
        public NpcEntry? GetNearestNPC(float maxRange)
        {
            if (_player == null) return null;
            Vector3 playerPos = _player.transform.position;
            NpcEntry? nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var npc in _npcList)
            {
                if (npc.Root == null) continue;
                float dist = Vector3.Distance(playerPos, npc.Root.position);
                if (dist <= maxRange && dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = npc;
                }
            }
            return nearest;
        }

        private void CreatePlayer()
        {
            // Root
            _player = new GameObject("Player");
            _player.transform.position = Vector3.zero;

            // Visual — try custom prefab, fall back to capsule
            GameObject playerVisual;
            var playerPrefab = TryLoadAsset<GameObject>("TestAssets/Player/PlayerModel");
            if (playerPrefab != null)
            {
                playerVisual = Instantiate(playerPrefab, _player.transform, false);
                playerVisual.name = "PlayerModel";
                playerVisual.transform.localPosition = new Vector3(0f, 1f, 0f);
                StripColliders(playerVisual);
            }
            else
            {
                playerVisual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                playerVisual.name = "PlayerModel";
                playerVisual.transform.SetParent(_player.transform, false);
                playerVisual.transform.localPosition = new Vector3(0f, 1f, 0f);
                Object.Destroy(playerVisual.GetComponent<CapsuleCollider>());
            }

            // Material — try custom, else default blue
            var playerMat = TryLoadAsset<Material>("TestAssets/Player/PlayerMaterial");
            var renderer = playerVisual.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                renderer.material = playerMat != null
                    ? playerMat
                    : CreateMaterial(new Color(0.2f, 0.4f, 0.9f));
            }

            // Weapon model — try loading and parent to WeaponMount or model root
            var weaponPrefab = TryLoadAsset<GameObject>("TestAssets/Weapons/WeaponModel");
            if (weaponPrefab != null)
            {
                var mountPoint = playerVisual.transform.Find("WeaponMount") ?? playerVisual.transform;
                var weapon = Instantiate(weaponPrefab, mountPoint, false);
                weapon.name = "WeaponModel";
                StripColliders(weapon);
            }

            // CharacterController
            var cc = _player.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.radius = 0.5f;
            cc.center = new Vector3(0f, 1f, 0f);

            // PlayerMovement
            var movement = _player.AddComponent<PlayerMovement>();
            movement.SetCharacterController(cc);

            // PlayerView (animator stays null — ProceduralAnimator handles visuals)
            var view = _player.AddComponent<PlayerView>();

            // Procedural animator
            var procAnim = playerVisual.AddComponent<ProceduralAnimator>();
            view.SetProceduralAnimator(procAnim);

            // PlayerStatsSO
            _playerStats = ScriptableObject.CreateInstance<PlayerStatsSO>();
            _playerStats.CharacterName = "TestPlayer";
            _playerStats.BaseHealth = 1000f;
            _playerStats.BaseVerdance = 100f;
            _playerStats.MoveSpeed = 6f;
            _playerStats.DodgeSpeed = 14f;
            _playerStats.DodgeDistance = 4f;
            _playerStats.BaseStats = new StatBlock
            {
                Vigor = 10f,
                Strength = 15f,
                Harvest = 8f,
                Verdance = 12f,
                Agility = 10f,
                Resilience = 5f
            };

            // PlayerController (Awake fires: creates StateMachine + states)
            _playerController = _player.AddComponent<PlayerController>();
            _playerController.SetupReferences(_playerStats, movement, view);
            movement.Initialize(_playerStats.MoveSpeed, _playerStats.DodgeSpeed, _playerStats.DodgeDistance);

            // Input handler
            _player.AddComponent<SimpleInputHandler>();

            // Player resources
            _playerMaxHealth = _playerStats.BaseHealth;
            _playerHealth = _playerMaxHealth;
            _playerMaxVerdance = _playerStats.BaseVerdance;
            _playerVerdance = _playerMaxVerdance;

            // Weapon
            _testWeapon = ScriptableObject.CreateInstance<WeaponDefinitionSO>();
            _testWeapon.WeaponName = "Test Sword";
            _testWeapon.BaseDamage = 40f;
            _testWeapon.BaseStagger = 10f;
            _testWeapon.AttackSpeed = 1.2f;
            _testWeapon.ChargedMultiplier = 1.5f;
            _testWeapon.DamageType = DamageType.Physical;
            _testWeapon.Element = Element.None;
            _testWeapon.MaxComboHits = 3;
        }

        private void CreateEnemies()
        {
            // GDD-accurate enemy roster — each enemy in its proper biome zone on the 200x200 map

            // Greenreach Valley (center: 0,0)
            SpawnEnemy("Withered Wolf",       2,  Element.None,    Rarity.Common, "Withered Beast",   new Vector3(12f, 0f, 8f));
            SpawnEnemy("Withered Wolf",       2,  Element.None,    Rarity.Common, "Withered Beast",   new Vector3(-10f, 0f, 14f));
            SpawnEnemy("Blight Beetle",       3,  Element.Verdant, Rarity.Common, "Blight Spawn",     new Vector3(20f, 0f, -5f));
            SpawnEnemy("Blight Beetle",       3,  Element.Verdant, Rarity.Common, "Blight Spawn",     new Vector3(-18f, 0f, -8f));
            SpawnEnemy("Corrupted Farmhand",  5,  Element.None,    Rarity.Common, "Corrupted Human",  new Vector3(5f, 0f, 22f));
            SpawnEnemy("Wither Stag",         8,  Element.Verdant, Rarity.Rare,   "Withered Beast",   new Vector3(-8f, 0f, 28f));

            // The Ashen Steppe (center: -55, -55)
            SpawnEnemy("Dustcrawler",         11, Element.Pyro,    Rarity.Common, "Withered Beast",   new Vector3(-45f, 0f, -45f));
            SpawnEnemy("Scorched Viper",      13, Element.Pyro,    Rarity.Common, "The Untamed",      new Vector3(-60f, 0f, -50f));
            SpawnEnemy("Acolyte Ranger",      15, Element.None,    Rarity.Common, "Varek's Acolytes", new Vector3(-50f, 0f, -65f));
            SpawnEnemy("Ashwalker Golem",     17, Element.Pyro,    Rarity.Rare,   "Constructs",       new Vector3(-65f, 0f, -70f));

            // Gloomtide Marshes (center: 55, -55)
            SpawnEnemy("Bogfiend",            17, Element.Umbral,  Rarity.Common, "Blight Spawn",     new Vector3(45f, 0f, -45f));
            SpawnEnemy("Sporecap Horror",     19, Element.Verdant, Rarity.Common, "Blight Spawn",     new Vector3(60f, 0f, -50f));
            SpawnEnemy("Drowned Sentinel",    21, Element.Umbral,  Rarity.Common, "Corrupted Human",  new Vector3(50f, 0f, -65f));
            SpawnEnemy("The Mire Queen",      23, Element.Umbral,  Rarity.Rare,   "Blight Spawn",     new Vector3(65f, 0f, -70f));

            // Frosthollow Peaks (center: -55, 55)
            SpawnEnemy("Frostwight",          23, Element.Cryo,    Rarity.Common, "Withered Beast",   new Vector3(-45f, 0f, 45f));
            SpawnEnemy("Glacial Construct",   26, Element.Cryo,    Rarity.Common, "Constructs",       new Vector3(-60f, 0f, 55f));
            SpawnEnemy("Acolyte Warder",      28, Element.None,    Rarity.Common, "Varek's Acolytes", new Vector3(-50f, 0f, 65f));
            SpawnEnemy("Avalanche Beast",     31, Element.Cryo,    Rarity.Rare,   "Withered Beast",   new Vector3(-65f, 0f, 70f));

            // The Withered Heart (center: 55, 55)
            SpawnEnemy("Hollow Shade",        32, Element.None,    Rarity.Common, "Blight Spawn",     new Vector3(45f, 0f, 45f));
            SpawnEnemy("Rootwraith",          35, Element.Verdant, Rarity.Common, "Withered Beast",   new Vector3(60f, 0f, 55f));
            SpawnEnemy("Wither Knight",       37, Element.None,    Rarity.Common, "Corrupted Human",  new Vector3(50f, 0f, 65f));
            SpawnEnemy("Blight Colossus",     39, Element.None,    Rarity.Rare,   "Blight Spawn",     new Vector3(65f, 0f, 70f));
        }

        private void SpawnEnemy(string enemyName, int level, Element element, Rarity tier, string category, Vector3 position)
        {
            _enemies.Add(CreateEnemyFromTemplate(enemyName, level, element, tier, category, position));
        }

        private static Color EnemyCategoryColor(string category) => category switch
        {
            "Withered Beast"   => new Color(0.6f, 0.3f, 0.15f),  // Dark brown
            "Blight Spawn"     => new Color(0.5f, 0.8f, 0.2f),   // Sickly green
            "Corrupted Human"  => new Color(0.7f, 0.5f, 0.7f),   // Muted purple
            "The Untamed"      => new Color(0.8f, 0.7f, 0.4f),   // Tawny/natural
            "Varek's Acolytes" => new Color(0.9f, 0.3f, 0.3f),   // Crimson
            "Constructs"       => new Color(0.5f, 0.6f, 0.7f),   // Steel gray
            _ => new Color(0.7f, 0.7f, 0.7f),
        };

        private EnemyEntry CreateEnemyFromTemplate(string enemyName, int level, Element element, Rarity tier, string category, Vector3 position)
        {
            // --- Stat formulas per tier ---
            //   Base HP = 50 + level * 15
            //   Tier multipliers: Common 1x, Rare(Elite) 2.5x, Legendary 5x, Mythic 10x
            float baseHp = 50f + level * 15f;
            float baseStagger = 30f + level * 5f;

            float hpMult = tier switch { Rarity.Common => 1f, Rarity.Rare => 2.5f, Rarity.Legendary => 5f, Rarity.Mythic => 10f, _ => 1f };
            float staggerMult = tier switch { Rarity.Common => 1f, Rarity.Rare => 2f, Rarity.Legendary => 3f, Rarity.Mythic => 5f, _ => 1f };
            float damage = tier switch { Rarity.Common => 10f + level, Rarity.Rare => 15f + level * 2f, Rarity.Legendary => 20f + level * 3f, Rarity.Mythic => 25f + level * 4f, _ => 10f + level };
            float speed = tier switch { Rarity.Common => 2.5f, Rarity.Rare => 3f, Rarity.Legendary => 2f, Rarity.Mythic => 1.5f, _ => 2.5f };
            int xpReward = tier switch { Rarity.Common => level * 10, Rarity.Rare => level * 30, Rarity.Legendary => level * 60, Rarity.Mythic => level * 100, _ => level * 10 };
            int goldReward = tier switch { Rarity.Common => level * 5, Rarity.Rare => level * 15, Rarity.Legendary => level * 30, Rarity.Mythic => level * 50, _ => level * 5 };

            float finalHp = baseHp * hpMult;
            float finalStagger = baseStagger * staggerMult;

            // --- Visual scale: elites are larger ---
            float visualScale = tier switch { Rarity.Common => 1f, Rarity.Rare => 1.3f, Rarity.Legendary => 1.6f, Rarity.Mythic => 2f, _ => 1f };

            // --- Color: element-based or category-based, tinted by tier ---
            Color baseColor = element != Element.None
                ? ElementToColor(element)
                : EnemyCategoryColor(category);
            float tierBrightness = tier switch { Rarity.Common => 0.7f, Rarity.Rare => 1f, Rarity.Legendary => 1.2f, Rarity.Mythic => 1.4f, _ => 0.7f };
            Color finalColor = new Color(
                Mathf.Min(baseColor.r * tierBrightness, 1f),
                Mathf.Min(baseColor.g * tierBrightness, 1f),
                Mathf.Min(baseColor.b * tierBrightness, 1f),
                1f);

            // --- Root ---
            var go = new GameObject(enemyName);
            go.transform.position = position;

            // Visual — try custom prefab, fall back to cube
            var enemyPrefab = TryLoadAsset<GameObject>("TestAssets/Enemies/EnemyModel");
            GameObject enemyVisual;
            if (enemyPrefab != null)
            {
                enemyVisual = Instantiate(enemyPrefab, go.transform, false);
                enemyVisual.name = "EnemyModel";
                enemyVisual.transform.localPosition = new Vector3(0f, 0.75f * visualScale, 0f);
                enemyVisual.transform.localScale = Vector3.one * visualScale;
                StripColliders(enemyVisual);
            }
            else
            {
                enemyVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                enemyVisual.name = "EnemyModel";
                enemyVisual.transform.SetParent(go.transform, false);
                enemyVisual.transform.localPosition = new Vector3(0f, 0.75f * visualScale, 0f);
                enemyVisual.transform.localScale = new Vector3(1f * visualScale, 1.5f * visualScale, 1f * visualScale);
                Object.Destroy(enemyVisual.GetComponent<BoxCollider>());
            }

            // Material — element/tier colored
            var enemyMat = TryLoadAsset<Material>("TestAssets/Enemies/EnemyMaterial");
            var enemyRenderer = enemyVisual.GetComponentInChildren<Renderer>();
            if (enemyRenderer != null)
            {
                enemyRenderer.material = enemyMat != null
                    ? new Material(enemyMat) { color = finalColor }
                    : CreateMaterial(finalColor);
            }

            // Collider on root for hit detection (scaled)
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.75f * visualScale, 0f);
            col.size = new Vector3(1f * visualScale, 1.5f * visualScale, 1f * visualScale);

            // Definition
            var def = ScriptableObject.CreateInstance<EnemyDefinitionSO>();
            def.EnemyName = enemyName;
            def.EnemyId = enemyName.ToLower().Replace(" ", "_");
            def.MaxHealth = finalHp;
            def.MaxStagger = finalStagger;
            def.Element = element;
            def.Tier = tier;
            def.XPReward = xpReward;
            def.GoldReward = goldReward;
            def.BaseStats = new StatBlock
            {
                Vigor = 3f + level * 0.5f,
                Strength = 5f + level * 0.8f,
                Harvest = 2f + level * 0.2f,
                Verdance = element != Element.None ? 3f + level * 0.3f : 0f,
                Agility = 3f + level * 0.4f,
                Resilience = 2f + level * 0.3f,
            };

            // AI controller
            var ai = go.AddComponent<EnemyAIController>();
            ai.SetDefinition(def);
            ai.Target = _player.transform;

            // Test behavior with per-enemy stats
            var behavior = go.AddComponent<TestEnemyBehavior>();
            behavior.SetTarget(_player.transform);
            behavior.AttackDamage = damage;
            behavior.MoveSpeed = speed;
            behavior.DetectRange = 10f + level * 0.15f;
            behavior.AttackCooldown = tier == Rarity.Rare ? 1.2f : 1.5f;
            behavior.OnAttackHit += OnEnemyAttackPlayer;

            // World-space nametag + health bar
            var (fill, canvasTransform) = CreateEnemyHealthBar(go.transform, enemyName, level, tier, finalColor, visualScale);

            return new EnemyEntry { AI = ai, HealthFill = fill, HealthBarCanvas = canvasTransform };
        }

        private (Image fill, Transform canvasTransform) CreateEnemyHealthBar(
            Transform parent, string enemyName, int level, Rarity tier, Color enemyColor, float visualScale)
        {
            float barY = 2.2f * visualScale;

            var canvasGo = new GameObject("HealthBarCanvas");
            canvasGo.transform.SetParent(parent, false);
            canvasGo.transform.localPosition = new Vector3(0f, barY, 0f);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGo.AddComponent<CanvasScaler>();

            var rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(160f, 38f); // taller to fit name + bar
            rt.localScale = Vector3.one * 0.01f;

            // --- Nametag (top half) ---
            var nameGo = new GameObject("Name");
            nameGo.transform.SetParent(canvasGo.transform, false);
            var nameText = nameGo.AddComponent<Text>();

            // Color name by tier
            Color nameColor = tier switch
            {
                Rarity.Rare => new Color(1f, 0.85f, 0.3f),      // gold for elites
                Rarity.Legendary => new Color(1f, 0.5f, 0.2f),   // orange
                Rarity.Mythic => new Color(1f, 0.4f, 1f),        // pink-purple
                _ => Color.white,                                  // common = white
            };

            nameText.text = $"{enemyName}  Lv{level}";
            nameText.font = _font;
            nameText.fontSize = 13;
            nameText.color = nameColor;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
            var nameRt = nameGo.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0f, 0.45f);
            nameRt.anchorMax = Vector2.one;
            nameRt.offsetMin = Vector2.zero;
            nameRt.offsetMax = Vector2.zero;

            // --- Health bar (bottom half) ---
            // Bar background
            var bgGo = new GameObject("Bg");
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.12f, 0.12f, 0.12f, 0.9f);
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = new Vector2(1f, 0.4f);
            bgRt.offsetMin = new Vector2(8f, 0f);
            bgRt.offsetMax = new Vector2(-8f, 0f);

            // Bar fill
            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(bgGo.transform, false);
            var fillImg = fillGo.AddComponent<Image>();
            // Tint health bar color by tier
            fillImg.color = tier switch
            {
                Rarity.Rare => new Color(1f, 0.6f, 0.15f, 1f),   // orange for elites
                Rarity.Legendary => new Color(0.9f, 0.3f, 0.1f),
                Rarity.Mythic => new Color(0.8f, 0.15f, 0.8f),
                _ => new Color(0.85f, 0.15f, 0.15f, 1f),          // red for common
            };
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one; // anchorMax.x driven in UpdateEnemyHealthBars
            fillRt.offsetMin = new Vector2(2f, 2f);
            fillRt.offsetMax = new Vector2(-2f, -2f);

            return (fillImg, canvasGo.transform);
        }

        private void SetupCamera()
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                var camGo = new GameObject("MainCamera");
                camGo.tag = "MainCamera";
                _mainCamera = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }

            _mainCamera.clearFlags = CameraClearFlags.SolidColor;
            _mainCamera.backgroundColor = new Color(0.15f, 0.15f, 0.2f);
            UpdateCamera();
        }

        private void CreateHUD()
        {
            var canvasGo = new GameObject("HUDCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            _healthFill = CreateHUDBar(canvasGo.transform, "HealthBar",
                new Vector2(20f, -20f), new Color(0.85f, 0.15f, 0.15f), "HP");

            _verdanceFill = CreateHUDBar(canvasGo.transform, "VerdanceBar",
                new Vector2(20f, -55f), new Color(0.2f, 0.75f, 0.3f), "VP");

            CreateBlightMeter(canvasGo.transform);
        }

        private void CreateBlightMeter(Transform hudParent)
        {
            // Container — anchored to right side, tall vertical bar
            var container = new GameObject("BlightMeter");
            container.transform.SetParent(hudParent, false);
            var cRt = container.AddComponent<RectTransform>();
            cRt.anchorMin = new Vector2(1f, 0.2f);  // right side, 20% from bottom
            cRt.anchorMax = new Vector2(1f, 0.8f);   // up to 80%
            cRt.pivot = new Vector2(1f, 0f);
            cRt.anchoredPosition = new Vector2(-20f, 0f);
            cRt.sizeDelta = new Vector2(30f, 0f);    // 30px wide, height from anchors

            // Background
            var bg = new GameObject("Bg");
            bg.transform.SetParent(container.transform, false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            // Fill — grows upward (anchorMax.y driven by blight level)
            var fill = new GameObject("Fill");
            fill.transform.SetParent(container.transform, false);
            _blightFill = fill.AddComponent<Image>();
            _blightFill.color = new Color(0.5f, 0.1f, 0.5f, 0.9f);
            var fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(1f, 0f); // starts empty, anchorMax.y set in update
            fillRt.offsetMin = new Vector2(2f, 2f);
            fillRt.offsetMax = new Vector2(-2f, -2f);

            // Label "BLIGHT" at top
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(container.transform, false);
            var titleText = titleGo.AddComponent<Text>();
            titleText.text = "BLIGHT";
            titleText.font = _font;
            titleText.fontSize = 11;
            titleText.color = new Color(0.8f, 0.5f, 0.8f);
            titleText.alignment = TextAnchor.UpperCenter;
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 0f);
            titleRt.anchoredPosition = new Vector2(0f, 4f);
            titleRt.sizeDelta = new Vector2(0f, 20f);

            // Percentage label at bottom
            var pctGo = new GameObject("Pct");
            pctGo.transform.SetParent(container.transform, false);
            _blightLabel = pctGo.AddComponent<Text>();
            _blightLabel.text = "0%";
            _blightLabel.font = _font;
            _blightLabel.fontSize = 12;
            _blightLabel.color = Color.white;
            _blightLabel.alignment = TextAnchor.LowerCenter;
            var pctRt = pctGo.GetComponent<RectTransform>();
            pctRt.anchorMin = Vector2.zero;
            pctRt.anchorMax = new Vector2(1f, 0f);
            pctRt.pivot = new Vector2(0.5f, 1f);
            pctRt.anchoredPosition = new Vector2(0f, -4f);
            pctRt.sizeDelta = new Vector2(0f, 18f);

            // Zone name label (above the bar)
            var zoneGo = new GameObject("ZoneName");
            zoneGo.transform.SetParent(container.transform, false);
            _blightZoneName = zoneGo.AddComponent<Text>();
            _blightZoneName.text = "";
            _blightZoneName.font = _font;
            _blightZoneName.fontSize = 12;
            _blightZoneName.color = new Color(0.9f, 0.8f, 0.6f);
            _blightZoneName.alignment = TextAnchor.LowerCenter;
            var zoneRt = zoneGo.GetComponent<RectTransform>();
            zoneRt.anchorMin = new Vector2(0f, 1f);
            zoneRt.anchorMax = new Vector2(1f, 1f);
            zoneRt.pivot = new Vector2(0.5f, 0f);
            zoneRt.anchoredPosition = new Vector2(0f, 22f);
            zoneRt.sizeDelta = new Vector2(80f, 18f);
        }

        private Image CreateHUDBar(Transform parent, string barName, Vector2 position,
                                    Color fillColor, string label)
        {
            // Container
            var container = new GameObject(barName);
            container.transform.SetParent(parent, false);
            var containerRt = container.AddComponent<RectTransform>();
            containerRt.anchorMin = new Vector2(0f, 1f);
            containerRt.anchorMax = new Vector2(0f, 1f);
            containerRt.pivot = new Vector2(0f, 1f);
            containerRt.anchoredPosition = position;
            containerRt.sizeDelta = new Vector2(300f, 28f);

            // Background
            var bg = new GameObject("Bg");
            bg.transform.SetParent(container.transform, false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.12f, 0.12f, 0.12f, 0.85f);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            // Fill — use anchor-driven width (no sprite needed)
            var fill = new GameObject("Fill");
            fill.transform.SetParent(container.transform, false);
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = fillColor;
            var fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one; // anchorMax.x will be driven in UpdateHUD
            fillRt.offsetMin = new Vector2(2f, 2f);
            fillRt.offsetMax = new Vector2(-2f, -2f);

            // Label
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(container.transform, false);
            var text = labelGo.AddComponent<Text>();
            text.text = label;
            text.font = _font;
            text.fontSize = 14;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            var textRt = labelGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(6f, 0f);
            textRt.offsetMax = Vector2.zero;

            return fillImg;
        }

        // ================================================================
        // Events
        // ================================================================

        private void SubscribeEvents()
        {
            EventBus.Subscribe<DamageDealtEvent>(OnDamageDealt);
            EventBus.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
        }

        private void OnDamageDealt(DamageDealtEvent evt)
        {
            SpawnDamageNumber(evt.HitPoint, evt.Amount, evt.IsCrit);
        }

        private void OnEnemyKilled(EnemyKilledEvent evt)
        {
            Debug.Log($"[TestScene] Enemy killed: {evt.EnemyDefinitionId} at {evt.Position}");
        }

        private void OnEnemyAttackPlayer(float damage)
        {
            if (_playerController == null) return;

            // Skip if player is invincible (dodging) or god mode
            if (_playerController.IsInvincible || GodMode) return;

            // Blight makes enemies stronger
            float blightedDamage = damage * BlightEnemyMultiplier;
            _playerHealth = Mathf.Max(0f, _playerHealth - blightedDamage);

            // Blight also caps effective max health
            float effectiveMax = _playerMaxHealth * BlightPlayerMultiplier;
            _playerHealth = Mathf.Min(_playerHealth, effectiveMax);

            if (_playerHealth <= 0f)
                _playerController.StateMachine.ChangeState(_playerController.DeathState);
        }

        // ================================================================
        // Per-frame update helpers
        // ================================================================

        private void UpdateCamera()
        {
            if (_player == null || _mainCamera == null) return;
            _mainCamera.transform.position = _player.transform.position + _cameraOffset;
            _mainCamera.transform.LookAt(_player.transform.position + Vector3.up);
        }

        private void UpdateHUD()
        {
            if (_healthFill != null)
            {
                // Show health relative to blight-reduced effective max
                float effectiveMax = _playerMaxHealth * BlightPlayerMultiplier;
                _playerHealth = Mathf.Min(_playerHealth, effectiveMax);
                float pct = effectiveMax > 0f ? _playerHealth / effectiveMax : 0f;
                SetBarFill(_healthFill.rectTransform, pct);
            }
            if (_verdanceFill != null)
            {
                float pct = _playerMaxVerdance > 0f ? _playerVerdance / _playerMaxVerdance : 0f;
                SetBarFill(_verdanceFill.rectTransform, pct);
            }
            UpdateBlightMeter();
        }

        private void UpdateBlightMeter()
        {
            if (_player == null) return;

            _currentBlight = GetBlightAtPosition(_player.transform.position);

            // Smoothly animate the displayed blight toward the actual value
            float lerpSpeed = 0.6f; // units per second — takes ~1.3s to go from 0 to 0.8
            _displayedBlight = Mathf.MoveTowards(_displayedBlight, _currentBlight, lerpSpeed * Time.deltaTime);

            if (_blightFill != null)
            {
                // Fill upward: anchorMax.y = smoothed blight level
                var fillRt = _blightFill.rectTransform;
                var max = fillRt.anchorMax;
                max.y = _displayedBlight;
                fillRt.anchorMax = max;
                fillRt.offsetMax = new Vector2(-2f, 0f);

                // Color lerp: low blight = dim purple, high blight = angry red
                _blightFill.color = Color.Lerp(
                    new Color(0.4f, 0.15f, 0.5f, 0.9f),
                    new Color(0.9f, 0.1f, 0.15f, 0.95f),
                    _displayedBlight);
            }

            if (_blightLabel != null)
                _blightLabel.text = $"{_displayedBlight * 100f:F0}%";

            if (_blightZoneName != null)
                _blightZoneName.text = _currentZoneName;
        }

        /// <summary>
        /// Blight penalty multiplier for player/companion damage and health.
        /// At 0% blight: 1.0 (no penalty). At 80% blight: 0.6 (40% weaker).
        /// Formula: 1 - blight * 0.5
        /// </summary>
        public float BlightPlayerMultiplier => 1f - _currentBlight * 0.5f;

        /// <summary>
        /// Blight bonus multiplier for enemy damage.
        /// At 0% blight: 1.0 (no bonus). At 80% blight: 1.4 (40% stronger).
        /// Formula: 1 + blight * 0.5
        /// </summary>
        public float BlightEnemyMultiplier => 1f + _currentBlight * 0.5f;

        private static void SetBarFill(RectTransform fillRt, float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            var max = fillRt.anchorMax;
            max.x = fraction;
            fillRt.anchorMax = max;
            // Keep right offset at 0 so the bar edge aligns with the anchor
            fillRt.offsetMax = new Vector2(0f, fillRt.offsetMax.y);
        }

        private void UpdateEnemyHealthBars()
        {
            if (_mainCamera == null) return;

            foreach (var entry in _enemies)
            {
                if (entry.AI == null || entry.HealthFill == null) continue;

                float fill = entry.AI.MaxHealth > 0f ? entry.AI.CurrentHealth / entry.AI.MaxHealth : 0f;
                SetBarFill(entry.HealthFill.rectTransform, Mathf.Max(0f, fill));

                // Billboard — face same direction as camera
                if (entry.HealthBarCanvas != null)
                    entry.HealthBarCanvas.forward = _mainCamera.transform.forward;
            }
        }

        private void CheckPlayerAttackHits()
        {
            if (_playerController == null) return;

            bool isAttacking =
                _playerController.StateMachine.CurrentState == _playerController.AttackState
                && _playerController.AttackState.HitboxActive;

            // Reset hit tracking when a new swing starts
            if (isAttacking && !_wasAttacking)
                _hitThisSwing.Clear();

            _wasAttacking = isAttacking;
            if (!isAttacking) return;

            // Sphere overlap in front of the player
            float attackRadius = 2.5f;
            Vector3 origin = _player.transform.position
                           + _player.transform.forward * 1.5f
                           + Vector3.up;

            var hits = Physics.OverlapSphere(origin, attackRadius);
            var combat = _playerController.GetCombatSystem();
            if (combat == null) return;

            foreach (var hit in hits)
            {
                var ai = hit.GetComponent<EnemyAIController>();
                if (ai == null || !ai.IsAlive) continue;

                int id = ai.GetInstanceID();
                if (_hitThisSwing.Contains(id)) continue;
                _hitThisSwing.Add(id);

                // Blight weakens player damage
                float damageMultiplier = OneHitKill ? 9999f : _partyDamageBonus * BlightPlayerMultiplier;
                combat.ProcessAttack(
                    _player,
                    ai.gameObject,
                    _testWeapon,
                    _playerStats.BaseStats,
                    ai.Definition != null ? ai.Definition.BaseStats : default,
                    damageMultiplier,
                    false);
            }
        }

        // ================================================================
        // Floating damage numbers
        // ================================================================

        private void SpawnDamageNumber(Vector3 worldPos, float amount, bool isCrit)
        {
            StartCoroutine(DamageNumberRoutine(worldPos, amount, isCrit));
        }

        private IEnumerator DamageNumberRoutine(Vector3 worldPos, float amount, bool isCrit)
        {
            // World-space canvas at hit point
            var go = new GameObject("DmgNum");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            go.AddComponent<CanvasScaler>();

            var rt = go.GetComponent<RectTransform>();
            rt.position = worldPos + Vector3.up * 0.5f;
            rt.sizeDelta = new Vector2(200f, 50f);
            rt.localScale = Vector3.one * 0.01f;

            // Text
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.AddComponent<Text>();
            text.text = Mathf.RoundToInt(amount).ToString();
            text.font = _font;
            text.fontSize = 36;
            text.color = isCrit ? Color.yellow : Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            if (isCrit)
            {
                text.fontStyle = FontStyle.Bold;
                rt.localScale *= 1.4f;
            }

            // Float up and fade out
            float duration = 1f;
            float elapsed = 0f;
            Vector3 startPos = rt.position;
            Color startColor = text.color;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                rt.position = startPos + Vector3.up * t * 1.5f;

                // Billboard
                if (_mainCamera != null)
                    rt.forward = _mainCamera.transform.forward;

                // Fade
                text.color = new Color(startColor.r, startColor.g, startColor.b, 1f - t);

                yield return null;
            }

            Destroy(go);
        }

        // ================================================================
        // Companions
        // ================================================================

        private static readonly string[] CompanionIds =
        {
            "companion_villager", "companion_farmer", "companion_scout", "companion_apprentice",
            "companion_knight", "companion_pyromancer", "companion_ranger", "companion_priest",
            "companion_lyra", "companion_thorne", "companion_selene", "companion_eldara"
        };

        private static readonly Element[] CompanionElements =
        {
            Element.None, Element.Verdant, Element.Volt, Element.Hydro,
            Element.Geo, Element.Pyro, Element.Verdant, Element.Hydro,
            Element.Verdant, Element.Umbral, Element.Cryo, Element.Pyro
        };

        private static readonly Rarity[] CompanionRarities =
        {
            Rarity.Common, Rarity.Common, Rarity.Common, Rarity.Common,
            Rarity.Rare, Rarity.Rare, Rarity.Rare, Rarity.Rare,
            Rarity.Legendary, Rarity.Legendary, Rarity.Legendary, Rarity.Mythic
        };

        private void CreateCompanionWeapon()
        {
            _companionWeapon = ScriptableObject.CreateInstance<WeaponDefinitionSO>();
            _companionWeapon.WeaponName = "Companion Strike";
            _companionWeapon.BaseDamage = 25f;
            _companionWeapon.BaseStagger = 5f;
            _companionWeapon.AttackSpeed = 1f;
            _companionWeapon.ChargedMultiplier = 1f;
            _companionWeapon.DamageType = DamageType.Physical;
            _companionWeapon.Element = Element.None;
            _companionWeapon.MaxComboHits = 1;
        }

        /// <summary>
        /// Called by TestMenuUI when the player assigns or removes a companion from a party slot.
        /// </summary>
        public void SetPartyCompanion(string slot, string companionId, int level = 1)
        {
            DespawnCompanion(slot);

            if (!string.IsNullOrEmpty(companionId))
                SpawnCompanion(slot, companionId, level);

            RecalculatePartyBuffs();
        }

        private void SpawnCompanion(string slot, string companionId, int level = 1)
        {
            // Offset: Active = left-behind, Support = right-behind
            Vector3 followOffset = slot == "Active"
                ? new Vector3(-1.5f, 0f, -1.5f)
                : new Vector3(1.5f, 0f, -1.5f);

            Vector3 spawnPos = _player.transform.position
                + _player.transform.TransformDirection(followOffset);

            // Root
            var root = new GameObject($"Companion_{companionId}_{slot}");
            root.transform.position = spawnPos;

            // Visual — try per-companion prefab, then generic fallback, then capsule
            GameObject visual;
            var companionPrefab = TryLoadAsset<GameObject>($"TestAssets/Companions/{companionId}/CompanionModel");
            if (companionPrefab == null)
                companionPrefab = TryLoadAsset<GameObject>("TestAssets/Companions/CompanionModel");

            if (companionPrefab != null)
            {
                visual = Instantiate(companionPrefab, root.transform, false);
                visual.name = "CompanionModel";
                visual.transform.localPosition = new Vector3(0f, 0.8f, 0f);
                StripColliders(visual);
            }
            else
            {
                visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visual.name = "CompanionModel";
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = new Vector3(0f, 0.8f, 0f);
                visual.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
                Object.Destroy(visual.GetComponent<CapsuleCollider>());
            }

            // Material — try per-companion, then generic, then element color fallback
            var companionMat = TryLoadAsset<Material>($"TestAssets/Companions/{companionId}/CompanionMaterial");
            if (companionMat == null)
                companionMat = TryLoadAsset<Material>("TestAssets/Companions/CompanionMaterial");

            var renderer = visual.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                renderer.material = companionMat != null
                    ? companionMat
                    : CreateMaterial(GetCompanionElementColor(companionId));
            }

            // Procedural animator on visual child
            var animator = visual.AddComponent<CompanionProceduralAnimator>();

            // Build enemy AI list for the behavior to scan
            var enemyAIs = new List<EnemyAIController>();
            foreach (var entry in _enemies)
            {
                if (entry.AI != null)
                    enemyAIs.Add(entry.AI);
            }

            // Behavior on root
            var behavior = root.AddComponent<TestCompanionBehavior>();
            behavior.FollowOffset = followOffset;
            behavior.Initialize(_player.transform, enemyAIs, animator);

            // Apply level-scaled stats
            behavior.AttackDamage = GetScaledDamage(companionId, level);
            behavior.AttackCooldown = GetScaledCooldown(level);
            behavior.MoveSpeed = GetScaledMoveSpeed(level);
            behavior.DetectRange = 10f + level * 0.1f;

            // Wire attack callback
            string capturedId = companionId;
            behavior.OnAttackEnemy += (enemyAI) => OnCompanionAttackEnemy(root, capturedId, enemyAI);

            _companions.Add(new CompanionEntry
            {
                CompanionId = companionId,
                Slot = slot,
                Level = level,
                Root = root,
                Behavior = behavior
            });
        }

        private void DespawnCompanion(string slot)
        {
            for (int i = _companions.Count - 1; i >= 0; i--)
            {
                if (_companions[i].Slot == slot)
                {
                    if (_companions[i].Root != null)
                        Destroy(_companions[i].Root);
                    _companions.RemoveAt(i);
                }
            }
        }

        private void OnCompanionAttackEnemy(GameObject companionGo, string companionId, EnemyAIController enemyAI)
        {
            if (enemyAI == null || !enemyAI.IsAlive) return;

            var combat = ServiceLocator.TryResolve<CombatSystem>(out var cs) ? cs : null;
            if (combat == null) return;

            // Set weapon element per companion
            Element companionElement = GetCompanionElement(companionId);
            _companionWeapon.Element = companionElement;

            // Look up companion entry to get level
            int level = 1;
            foreach (var entry in _companions)
            {
                if (entry.CompanionId == companionId && entry.Root == companionGo)
                {
                    level = entry.Level;
                    break;
                }
            }

            StatBlock attackerStats = GetCompanionStatBlock(companionId, level);
            StatBlock defenderStats = enemyAI.Definition != null ? enemyAI.Definition.BaseStats : default;

            // Blight weakens companion damage too
            combat.ProcessAttack(
                companionGo,
                enemyAI.gameObject,
                _companionWeapon,
                attackerStats,
                defenderStats,
                BlightPlayerMultiplier,
                false);
        }

        private static Element GetCompanionElement(string companionId)
        {
            int idx = System.Array.IndexOf(CompanionIds, companionId);
            return idx >= 0 ? CompanionElements[idx] : Element.None;
        }

        private static Color GetCompanionElementColor(string companionId)
        {
            return ElementToColor(GetCompanionElement(companionId));
        }

        public static StatBlock GetCompanionStatBlock(string companionId, int level = 1)
        {
            int idx = System.Array.IndexOf(CompanionIds, companionId);
            Rarity rarity = idx >= 0 ? CompanionRarities[idx] : Rarity.Common;

            float multiplier = rarity switch
            {
                Rarity.Common => 1f,
                Rarity.Rare => 1.5f,
                _ => 2f // Legendary, Mythic
            };

            float levelScale = 1f + (level - 1) * 0.04f;

            return new StatBlock
            {
                Vigor = 8f * multiplier * levelScale,
                Strength = 12f * multiplier * levelScale,
                Harvest = 5f * multiplier * levelScale,
                Verdance = 8f * multiplier * levelScale,
                Agility = 8f * multiplier * levelScale,
                Resilience = 4f * multiplier * levelScale
            };
        }

        // ================================================================
        // Level-scaling helpers (public static for UI access)
        // ================================================================

        private static float RarityMultiplier(string companionId)
        {
            int idx = System.Array.IndexOf(CompanionIds, companionId);
            Rarity rarity = idx >= 0 ? CompanionRarities[idx] : Rarity.Common;
            return rarity switch
            {
                Rarity.Common => 1f,
                Rarity.Rare => 1.5f,
                Rarity.Legendary => 2f,
                Rarity.Mythic => 2f,
                _ => 1f
            };
        }

        public static float GetScaledDamage(string companionId, int level)
        {
            return 25f * RarityMultiplier(companionId) * (1f + (level - 1) * 0.04f);
        }

        public static float GetScaledCooldown(int level)
        {
            return Mathf.Max(0.5f, 2f - (level - 1) * 0.025f);
        }

        public static float GetScaledMoveSpeed(int level)
        {
            return 5f + (level - 1) * 0.05f;
        }

        // ================================================================
        // Party passive buff system
        // ================================================================

        private void RecalculatePartyBuffs()
        {
            _partyDamageBonus = 1f;
            _partyCooldownReduction = 0f;

            foreach (var entry in _companions)
            {
                int idx = System.Array.IndexOf(CompanionIds, entry.CompanionId);
                if (idx < 0) continue;
                Rarity rarity = CompanionRarities[idx];

                if (rarity == Rarity.Mythic)
                    _partyDamageBonus += 0.10f;
                if (rarity == Rarity.Legendary)
                    _partyCooldownReduction += 0.1f;
            }

            // Apply cooldown reduction to weapon attack speed
            if (_testWeapon != null)
                _testWeapon.AttackSpeed = 1.2f + _partyCooldownReduction;
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

        // ================================================================
        // Helpers
        // ================================================================

        private static Material CreateMaterial(Color color)
        {
            // Clone the material Unity assigns to primitives — guaranteed to
            // use the correct shader for whatever render pipeline is active.
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var mat = new Material(temp.GetComponent<Renderer>().sharedMaterial);
            Object.Destroy(temp);

            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);

            return mat;
        }
    }
}
