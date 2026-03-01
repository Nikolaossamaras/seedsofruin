using SoR.Combat;
using SoR.Core;
using UnityEngine;

namespace SoR.Gameplay
{
    public class PlayerController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private PlayerStatsSO _baseData;

        [Header("References")]
        [SerializeField] private PlayerMovement _movement;
        [SerializeField] private PlayerView _view;

        private StateMachine _stateMachine;
        private CombatSystem _combatSystem;

        // States accessible by state classes.
        public PlayerIdleState IdleState { get; private set; }
        public PlayerMoveState MoveState { get; private set; }
        public PlayerAttackState AttackState { get; private set; }
        public PlayerDodgeState DodgeState { get; private set; }
        public PlayerSkillState SkillState { get; private set; }
        public PlayerDeathState DeathState { get; private set; }

        // Public accessors for state classes.
        public PlayerStatsSO BaseData => _baseData;
        public PlayerMovement Movement => _movement;
        public PlayerView View => _view;
        public StateMachine StateMachine => _stateMachine;

        // Input direction driven by the input system.
        public Vector3 InputDirection { get; set; }
        public bool AttackInputPressed { get; set; }
        public bool DodgeInputPressed { get; set; }
        public int SkillInputIndex { get; set; } = -1;

        // Invincibility flag used during dodge.
        public bool IsInvincible { get; set; }

        private void Awake()
        {
            _stateMachine = new StateMachine();
            InitializeStates();
        }

        private void Start()
        {
            if (ServiceLocator.TryResolve<CombatSystem>(out var combat))
                _combatSystem = combat;

            _stateMachine.ChangeState(IdleState);
        }

        /// <summary>
        /// Allows runtime-created players to wire serialized references from code.
        /// </summary>
        public void SetupReferences(PlayerStatsSO data, PlayerMovement movement, PlayerView view)
        {
            _baseData = data;
            _movement = movement;
            _view = view;
        }

        private void Update()
        {
            _stateMachine.Update();
        }

        private void InitializeStates()
        {
            IdleState = new PlayerIdleState(this);
            MoveState = new PlayerMoveState(this);
            AttackState = new PlayerAttackState(this);
            DodgeState = new PlayerDodgeState(this);
            SkillState = new PlayerSkillState(this);
            DeathState = new PlayerDeathState(this);
        }

        public void HandleAttackInput()
        {
            AttackInputPressed = true;
        }

        public void HandleDodgeInput()
        {
            DodgeInputPressed = true;
        }

        public void HandleSkillInput(int skillIndex)
        {
            SkillInputIndex = skillIndex;
        }

        public CombatSystem GetCombatSystem()
        {
            return _combatSystem;
        }
    }
}
