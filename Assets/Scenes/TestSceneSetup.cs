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

        // ---- enemies ----
        private readonly List<EnemyEntry> _enemies = new();

        // ---- attack hit detection ----
        private readonly HashSet<int> _hitThisSwing = new();
        private bool _wasAttacking;

        // ---- camera ----
        private readonly Vector3 _cameraOffset = new Vector3(0f, 12f, -8f);

        // ---- cached font ----
        private Font _font;

        private struct EnemyEntry
        {
            public EnemyAIController AI;
            public Image HealthFill;
            public Transform HealthBarCanvas;
        }

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
            SetupCamera();
            CreateHUD();
            SubscribeEvents();
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<DamageDealtEvent>(OnDamageDealt);
            EventBus.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);
        }

        private void Update()
        {
            UpdateCamera();
            UpdateHUD();
            UpdateEnemyHealthBars();
            CheckPlayerAttackHits();
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
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(5f, 1f, 5f); // 50x50
            ground.GetComponent<Renderer>().material = CreateMaterial(new Color(0.35f, 0.55f, 0.3f));
        }

        private void CreatePlayer()
        {
            // Root
            _player = new GameObject("Player");
            _player.transform.position = Vector3.zero;

            // Capsule visual
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "PlayerModel";
            capsule.transform.SetParent(_player.transform, false);
            capsule.transform.localPosition = new Vector3(0f, 1f, 0f);
            capsule.GetComponent<Renderer>().material = CreateMaterial(new Color(0.2f, 0.4f, 0.9f));
            Object.Destroy(capsule.GetComponent<CapsuleCollider>());

            // CharacterController
            var cc = _player.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.radius = 0.5f;
            cc.center = new Vector3(0f, 1f, 0f);

            // PlayerMovement
            var movement = _player.AddComponent<PlayerMovement>();
            movement.SetCharacterController(cc);

            // PlayerView (animator stays null — all methods already null-check it)
            var view = _player.AddComponent<PlayerView>();

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
            Vector3[] positions =
            {
                new Vector3(5f, 0f, 5f),
                new Vector3(-4f, 0f, 6f),
                new Vector3(0f, 0f, -7f)
            };

            string[] names = { "Enemy_A", "Enemy_B", "Enemy_C" };

            for (int i = 0; i < 3; i++)
                _enemies.Add(CreateEnemy(names[i], positions[i]));
        }

        private EnemyEntry CreateEnemy(string enemyName, Vector3 position)
        {
            // Root
            var go = new GameObject(enemyName);
            go.transform.position = position;

            // Cube visual
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "EnemyModel";
            cube.transform.SetParent(go.transform, false);
            cube.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            cube.transform.localScale = new Vector3(1f, 1.5f, 1f);
            cube.GetComponent<Renderer>().material = CreateMaterial(new Color(0.9f, 0.2f, 0.2f));
            Object.Destroy(cube.GetComponent<BoxCollider>());

            // Collider on root for hit detection
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.75f, 0f);
            col.size = new Vector3(1f, 1.5f, 1f);

            // Definition
            var def = ScriptableObject.CreateInstance<EnemyDefinitionSO>();
            def.EnemyName = enemyName;
            def.EnemyId = enemyName.ToLower();
            def.MaxHealth = 200f;
            def.MaxStagger = 50f;
            def.Element = Element.None;
            def.Tier = Rarity.Common;
            def.BaseStats = new StatBlock
            {
                Vigor = 5f,
                Strength = 8f,
                Harvest = 3f,
                Verdance = 0f,
                Agility = 4f,
                Resilience = 3f
            };

            // AI controller (Awake fires — _definition is null so it does nothing)
            var ai = go.AddComponent<EnemyAIController>();
            ai.SetDefinition(def);
            ai.Target = _player.transform;

            // Test behavior
            var behavior = go.AddComponent<TestEnemyBehavior>();
            behavior.SetTarget(_player.transform);
            behavior.OnAttackHit += OnEnemyAttackPlayer;

            // World-space health bar
            var (fill, canvasTransform) = CreateEnemyHealthBar(go.transform);

            return new EnemyEntry { AI = ai, HealthFill = fill, HealthBarCanvas = canvasTransform };
        }

        private (Image fill, Transform canvasTransform) CreateEnemyHealthBar(Transform parent)
        {
            var canvasGo = new GameObject("HealthBarCanvas");
            canvasGo.transform.SetParent(parent, false);
            canvasGo.transform.localPosition = new Vector3(0f, 2.2f, 0f);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGo.AddComponent<CanvasScaler>();

            var rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(120f, 15f);
            rt.localScale = Vector3.one * 0.01f;

            // Background
            var bgGo = new GameObject("Bg");
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            // Fill
            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(canvasGo.transform, false);
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = new Color(0.9f, 0.15f, 0.15f, 1f);
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = 1f;
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
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

            // Fill
            var fill = new GameObject("Fill");
            fill.transform.SetParent(container.transform, false);
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = fillColor;
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = 1f;
            var fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
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

            // Skip if player is invincible (dodging)
            if (_playerController.IsInvincible) return;

            _playerHealth = Mathf.Max(0f, _playerHealth - damage);

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
                _healthFill.fillAmount = _playerMaxHealth > 0f ? _playerHealth / _playerMaxHealth : 0f;
            if (_verdanceFill != null)
                _verdanceFill.fillAmount = _playerMaxVerdance > 0f ? _playerVerdance / _playerMaxVerdance : 0f;
        }

        private void UpdateEnemyHealthBars()
        {
            if (_mainCamera == null) return;

            foreach (var entry in _enemies)
            {
                if (entry.AI == null || entry.HealthFill == null) continue;

                float fill = entry.AI.MaxHealth > 0f ? entry.AI.CurrentHealth / entry.AI.MaxHealth : 0f;
                entry.HealthFill.fillAmount = Mathf.Max(0f, fill);

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

                combat.ProcessAttack(
                    _player,
                    ai.gameObject,
                    _testWeapon,
                    _playerStats.BaseStats,
                    ai.Definition != null ? ai.Definition.BaseStats : default,
                    1f,
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
        // Helpers
        // ================================================================

        private static Material CreateMaterial(Color color)
        {
            // URP Lit needs keyword/property setup to avoid magenta.
            // URP Unlit just works with _BaseColor — perfect for placeholder visuals.
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");

            var mat = new Material(shader);

            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);

            return mat;
        }
    }
}
