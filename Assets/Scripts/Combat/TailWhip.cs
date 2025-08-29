using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using WhiskerKing.Core;
using WhiskerKing.Player;

namespace WhiskerKing.Combat
{
    /// <summary>
    /// Tail Whip combat system for Whisker King
    /// Implements precise timing system with windup, active, and recovery phases
    /// Based on PRD specifications: 0.1s windup, 0.18s active, 0.12s recovery
    /// </summary>
    public class TailWhip : MonoBehaviour
    {
        public enum AttackState
        {
            Idle,
            Windup,
            Active,
            Recovery
        }

        [Header("Configuration")]
        [SerializeField] private bool useGameConfiguration = true;
        [SerializeField] private bool debugMode = false;

        [Header("Timing Settings (Override if not using GameConfiguration)")]
        [SerializeField] private float windupTime = 0.1f;      // 100ms
        [SerializeField] private float activeTime = 0.18f;     // 180ms
        [SerializeField] private float recoveryTime = 0.12f;   // 120ms

        [Header("Combat Parameters")]
        [SerializeField] private float damage = 25f;
        [SerializeField] private float stunDuration = 1.5f;
        [SerializeField] private float range = 2.5f;
        [SerializeField] private float angleDegreess = 270f;

        [Header("Visual Settings")]
        [SerializeField] private bool showAttackRange = true;
        [SerializeField] private Color windupColor = Color.yellow;
        [SerializeField] private Color activeColor = Color.red;
        [SerializeField] private Color recoveryColor = Color.blue;

        // Components
        private PlayerController playerController;
        
        // Attack state
        private AttackState currentState = AttackState.Idle;
        private float stateStartTime;
        private float totalAttackTime;
        private bool attackQueued = false;
        private bool canCombo = false;
        private int comboCount = 0;
        private float lastAttackTime = -1f;

        // Hit detection
        private HashSet<Collider> hitTargets = new HashSet<Collider>();
        private List<Collider> validTargets = new List<Collider>();

        // Configuration cache
        private TailWhipData combatConfig;

        // Performance tracking
        private Vector3 attackDirection;
        private Vector3 attackOrigin;
        private float debugCurrentDamage;
        private int debugHitCount;

        // Events
        public System.Action OnAttackStart;
        public System.Action OnAttackHit;
        public System.Action OnAttackComplete;
        public System.Action<AttackState> OnStateChanged;

        #region Unity Lifecycle

        private void Awake()
        {
            // Get required components
            playerController = GetComponent<PlayerController>();
            if (playerController == null)
            {
                playerController = GetComponentInParent<PlayerController>();
                if (playerController == null)
                {
                    Debug.LogError("TailWhip: Could not find PlayerController component!", this);
                }
            }
        }

        private void Start()
        {
            LoadConfiguration();
            InitializeCombatSystem();
        }

        private void Update()
        {
            UpdateAttackState();
            UpdateHitDetection();
            
            if (debugMode)
            {
                UpdateDebugInfo();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (debugMode && showAttackRange)
            {
                DrawAttackGizmos();
            }
        }

        #endregion

        #region Configuration

        private void LoadConfiguration()
        {
            if (useGameConfiguration && GameConfiguration.Instance != null && GameConfiguration.Instance.Config != null)
            {
                var playerMovement = GameConfiguration.Instance.GetPlayerMovement();
                
                if (playerMovement?.combat?.tailWhip != null)
                {
                    combatConfig = playerMovement.combat.tailWhip;
                    ApplyConfiguration();
                    Debug.Log("TailWhip: Configuration loaded from GameConfig");
                }
                else
                {
                    Debug.LogWarning("TailWhip: GameConfiguration not available, using default values");
                    UseDefaultConfiguration();
                }
            }
            else
            {
                Debug.Log("TailWhip: Using Inspector values (GameConfiguration disabled)");
                UseDefaultConfiguration();
            }
        }

        private void ApplyConfiguration()
        {
            // Apply timing settings (convert from ms to seconds)
            windupTime = combatConfig.windupTimeMS / 1000f;
            activeTime = combatConfig.activeTimeMS / 1000f;
            recoveryTime = combatConfig.recoveryTimeMS / 1000f;

            // Apply combat parameters
            damage = combatConfig.damage;
            stunDuration = combatConfig.stunDuration;
            range = combatConfig.range;
            angleDegreess = combatConfig.angleDegreess;
        }

        private void UseDefaultConfiguration()
        {
            // Create default configuration based on PRD values
            combatConfig = new TailWhipData
            {
                windupTimeMS = 100,    // 0.1s
                activeTimeMS = 180,    // 0.18s
                recoveryTimeMS = 120,  // 0.12s
                damage = 25f,
                stunDuration = 1.5f,
                range = 2.5f,
                angleDegreess = 270f
            };

            ApplyConfiguration();
        }

        #endregion

        #region Initialization

        private void InitializeCombatSystem()
        {
            // Calculate total attack time
            totalAttackTime = windupTime + activeTime + recoveryTime;
            
            // Initialize state
            currentState = AttackState.Idle;
            stateStartTime = 0f;
            hitTargets.Clear();

            Debug.Log($"TailWhip initialized - Total time: {totalAttackTime * 1000:F0}ms, Damage: {damage}, Range: {range}m");
        }

        #endregion

        #region Attack State Management

        private void UpdateAttackState()
        {
            if (currentState == AttackState.Idle) return;

            float stateElapsed = Time.time - stateStartTime;

            switch (currentState)
            {
                case AttackState.Windup:
                    if (stateElapsed >= windupTime)
                    {
                        StartActivePhase();
                    }
                    break;

                case AttackState.Active:
                    if (stateElapsed >= activeTime)
                    {
                        StartRecoveryPhase();
                    }
                    break;

                case AttackState.Recovery:
                    if (stateElapsed >= recoveryTime)
                    {
                        CompleteAttack();
                    }
                    break;
            }
        }

        private void StartWindupPhase()
        {
            currentState = AttackState.Windup;
            stateStartTime = Time.time;
            
            // Clear hit targets for new attack
            hitTargets.Clear();
            
            // Calculate attack direction and origin
            UpdateAttackGeometry();
            
            // Trigger windup effects
            OnWindupStarted();
            
            if (debugMode)
            {
                Debug.Log($"Tail whip windup started - Duration: {windupTime * 1000:F0}ms");
            }
        }

        private void StartActivePhase()
        {
            currentState = AttackState.Active;
            stateStartTime = Time.time;
            
            // Update attack geometry for active phase
            UpdateAttackGeometry();
            
            // Trigger active effects
            OnActiveStarted();
            
            if (debugMode)
            {
                Debug.Log($"Tail whip active started - Duration: {activeTime * 1000:F0}ms, Range: {range}m");
            }
        }

        private void StartRecoveryPhase()
        {
            currentState = AttackState.Recovery;
            stateStartTime = Time.time;
            
            // Trigger recovery effects
            OnRecoveryStarted();
            
            if (debugMode)
            {
                Debug.Log($"Tail whip recovery started - Duration: {recoveryTime * 1000:F0}ms");
            }
        }

        private void CompleteAttack()
        {
            currentState = AttackState.Idle;
            lastAttackTime = Time.time;
            
            // Check for queued attacks
            if (attackQueued)
            {
                attackQueued = false;
                TryStartAttack();
            }
            
            // Trigger completion events
            OnAttackCompleted();
            
            if (debugMode)
            {
                Debug.Log($"Tail whip completed - Total hits: {debugHitCount}");
            }
        }

        private void UpdateAttackGeometry()
        {
            if (playerController != null)
            {
                attackOrigin = transform.position;
                
                // Use player's movement direction, or forward if not moving
                Vector3 playerDirection = playerController.GetMovementDirection();
                if (playerDirection.magnitude < 0.1f)
                {
                    attackDirection = transform.forward;
                }
                else
                {
                    attackDirection = playerDirection.normalized;
                }
            }
            else
            {
                attackOrigin = transform.position;
                attackDirection = transform.forward;
            }
        }

        #endregion

        #region Hit Detection

        private void UpdateHitDetection()
        {
            if (currentState != AttackState.Active) return;

            // Find potential targets in range
            FindTargetsInRange();
            
            // Check each target for valid hit
            foreach (var target in validTargets)
            {
                if (!hitTargets.Contains(target) && IsValidHitTarget(target))
                {
                    ProcessHit(target);
                    hitTargets.Add(target);
                    debugHitCount++;
                }
            }
        }

        private void FindTargetsInRange()
        {
            validTargets.Clear();
            
            // Use sphere overlap to find potential targets
            Collider[] nearbyColliders = Physics.OverlapSphere(attackOrigin, range);
            
            foreach (var collider in nearbyColliders)
            {
                // Skip self and player collider
                if (collider.transform == transform || 
                    collider.transform == playerController?.transform)
                    continue;
                
                // Check if target is within attack angle
                if (IsWithinAttackAngle(collider.transform.position))
                {
                    validTargets.Add(collider);
                }
            }
        }

        private bool IsWithinAttackAngle(Vector3 targetPosition)
        {
            Vector3 directionToTarget = (targetPosition - attackOrigin).normalized;
            float angleToTarget = Vector3.Angle(attackDirection, directionToTarget);
            
            // Check if within attack angle (270° means 135° on each side)
            float halfAngle = angleDegreess * 0.5f;
            return angleToTarget <= halfAngle;
        }

        private bool IsValidHitTarget(Collider target)
        {
            // Check distance
            float distance = Vector3.Distance(attackOrigin, target.transform.position);
            if (distance > range) return false;

            // Check for valid target tags/components
            if (target.CompareTag("Enemy") || target.CompareTag("Crate") || target.CompareTag("Destructible"))
                return true;

            // Check for specific components that indicate hittable objects
            if (target.GetComponent<IDamageable>() != null)
                return true;

            return false;
        }

        private void ProcessHit(Collider target)
        {
            // Calculate hit information
            Vector3 hitPoint = target.ClosestPoint(attackOrigin);
            Vector3 hitDirection = (target.transform.position - attackOrigin).normalized;
            
            // Apply damage if target can take damage
            var damageable = target.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage, hitPoint, hitDirection, stunDuration);
            }

            // Apply knockback if target has rigidbody
            var rb = target.GetComponent<Rigidbody>();
            if (rb != null)
            {
                float knockbackForce = damage * 2f; // Scale knockback with damage
                rb.AddForce(hitDirection * knockbackForce, ForceMode.Impulse);
            }

            // Trigger hit events
            OnHitTarget(target, hitPoint, hitDirection);
            
            if (debugMode)
            {
                Debug.Log($"Tail whip hit: {target.name} - Damage: {damage}, Distance: {Vector3.Distance(attackOrigin, target.transform.position):F2}m");
            }
        }

        #endregion

        #region Public Interface

        /// <summary>
        /// Attempt to start a tail whip attack
        /// </summary>
        public bool TryStartAttack()
        {
            if (currentState != AttackState.Idle)
            {
                // Queue attack if in recovery phase
                if (currentState == AttackState.Recovery && canCombo)
                {
                    attackQueued = true;
                    return true;
                }
                return false;
            }

            StartWindupPhase();
            return true;
        }

        /// <summary>
        /// Force cancel current attack
        /// </summary>
        public void CancelAttack()
        {
            if (currentState != AttackState.Idle)
            {
                currentState = AttackState.Idle;
                attackQueued = false;
                OnAttackCanceled();
                
                if (debugMode)
                {
                    Debug.Log("Tail whip attack canceled");
                }
            }
        }

        /// <summary>
        /// Check if attack is available
        /// </summary>
        public bool CanAttack()
        {
            return currentState == AttackState.Idle || (currentState == AttackState.Recovery && canCombo);
        }

        /// <summary>
        /// Get current attack state
        /// </summary>
        public AttackState GetCurrentState()
        {
            return currentState;
        }

        /// <summary>
        /// Get attack progress (0-1)
        /// </summary>
        public float GetAttackProgress()
        {
            if (currentState == AttackState.Idle) return 0f;

            float totalElapsed = Time.time - (stateStartTime - GetPreviousStatesTime());
            return Mathf.Clamp01(totalElapsed / totalAttackTime);
        }

        /// <summary>
        /// Get current state progress (0-1)
        /// </summary>
        public float GetStateProgress()
        {
            if (currentState == AttackState.Idle) return 0f;

            float stateElapsed = Time.time - stateStartTime;
            float stateDuration = GetCurrentStateDuration();
            
            return Mathf.Clamp01(stateElapsed / stateDuration);
        }

        private float GetPreviousStatesTime()
        {
            switch (currentState)
            {
                case AttackState.Windup: return 0f;
                case AttackState.Active: return windupTime;
                case AttackState.Recovery: return windupTime + activeTime;
                default: return 0f;
            }
        }

        private float GetCurrentStateDuration()
        {
            switch (currentState)
            {
                case AttackState.Windup: return windupTime;
                case AttackState.Active: return activeTime;
                case AttackState.Recovery: return recoveryTime;
                default: return 0f;
            }
        }

        /// <summary>
        /// Get attack configuration
        /// </summary>
        public TailWhipData GetConfiguration()
        {
            return combatConfig;
        }

        /// <summary>
        /// Enable/disable combo system
        /// </summary>
        public void SetComboEnabled(bool enabled)
        {
            canCombo = enabled;
        }

        #endregion

        #region Events

        private void OnWindupStarted()
        {
            OnStateChanged?.Invoke(AttackState.Windup);
            // Hook for visual/audio effects during windup
        }

        private void OnActiveStarted()
        {
            OnStateChanged?.Invoke(AttackState.Active);
            OnAttackStart?.Invoke();
            // Hook for visual/audio effects during active phase
        }

        private void OnRecoveryStarted()
        {
            OnStateChanged?.Invoke(AttackState.Recovery);
            // Hook for visual/audio effects during recovery
        }

        private void OnAttackCompleted()
        {
            OnAttackComplete?.Invoke();
            // Hook for completion effects
        }

        private void OnAttackCanceled()
        {
            // Hook for cancellation effects
        }

        private void OnHitTarget(Collider target, Vector3 hitPoint, Vector3 hitDirection)
        {
            OnAttackHit?.Invoke();
            // Hook for hit effects (particles, sound, screen shake)
        }

        #endregion

        #region Debug

        private void UpdateDebugInfo()
        {
            debugCurrentDamage = damage;
        }

        private void DrawAttackGizmos()
        {
            // Draw attack range and angle
            Vector3 center = attackOrigin;
            
            // Choose color based on current state
            Color gizmoColor = currentState switch
            {
                AttackState.Windup => windupColor,
                AttackState.Active => activeColor,
                AttackState.Recovery => recoveryColor,
                _ => Color.white
            };
            
            Gizmos.color = gizmoColor;
            
            // Draw attack range circle
            Gizmos.DrawWireCircle(center, range);
            
            // Draw attack angle arc
            DrawAttackArc(center, attackDirection, angleDegreess, range);
            
            // Draw attack direction
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(center, attackDirection * range);
        }

        private void DrawAttackArc(Vector3 center, Vector3 direction, float angle, float radius)
        {
            float halfAngle = angle * 0.5f;
            int segments = 20;
            float segmentAngle = angle / segments;
            
            Vector3 leftBound = Quaternion.AngleAxis(-halfAngle, Vector3.up) * direction;
            Vector3 rightBound = Quaternion.AngleAxis(halfAngle, Vector3.up) * direction;
            
            // Draw arc segments
            for (int i = 0; i < segments; i++)
            {
                float currentAngle = -halfAngle + (segmentAngle * i);
                float nextAngle = -halfAngle + (segmentAngle * (i + 1));
                
                Vector3 currentDir = Quaternion.AngleAxis(currentAngle, Vector3.up) * direction;
                Vector3 nextDir = Quaternion.AngleAxis(nextAngle, Vector3.up) * direction;
                
                Gizmos.DrawLine(center + currentDir * radius, center + nextDir * radius);
            }
            
            // Draw bounds
            Gizmos.DrawLine(center, center + leftBound * radius);
            Gizmos.DrawLine(center, center + rightBound * radius);
        }

        private void OnGUI()
        {
            if (debugMode)
            {
                GUILayout.BeginArea(new Rect(680, 10, 300, 200));
                
                GUILayout.Label("=== TAIL WHIP DEBUG ===");
                GUILayout.Label($"State: {currentState}");
                
                if (currentState != AttackState.Idle)
                {
                    GUILayout.Label($"State Progress: {GetStateProgress() * 100:F0}%");
                    GUILayout.Label($"Total Progress: {GetAttackProgress() * 100:F0}%");
                }
                
                GUILayout.Space(5);
                GUILayout.Label("=== COMBAT STATS ===");
                GUILayout.Label($"Damage: {debugCurrentDamage:F0}");
                GUILayout.Label($"Range: {range:F1}m");
                GUILayout.Label($"Angle: {angleDegreess:F0}°");
                GUILayout.Label($"Total Time: {totalAttackTime * 1000:F0}ms");
                
                GUILayout.Space(5);
                GUILayout.Label("=== TIMING ===");
                GUILayout.Label($"Windup: {windupTime * 1000:F0}ms");
                GUILayout.Label($"Active: {activeTime * 1000:F0}ms");
                GUILayout.Label($"Recovery: {recoveryTime * 1000:F0}ms");
                
                GUILayout.Space(5);
                GUILayout.Label("=== HIT INFO ===");
                GUILayout.Label($"Targets Hit: {debugHitCount}");
                GUILayout.Label($"Valid Targets: {validTargets.Count}");
                GUILayout.Label($"Can Attack: {CanAttack()}");
                
                GUILayout.EndArea();
            }
        }

        #endregion
    }

    /// <summary>
    /// Interface for objects that can take damage
    /// </summary>
    public interface IDamageable
    {
        void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitDirection, float stunDuration = 0f);
    }
}
