using UnityEngine;

[DisallowMultipleComponent]
public sealed class AgentVisualController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private string attackTrigger = "Attack";
    [SerializeField] private string celebrateTrigger = "Celebrate";
    [SerializeField] private string hitTrigger = "Hit";
    [SerializeField] private string defeatTrigger = "Defeat";
    [SerializeField] private float fullRunSpeed = 5f;
    [SerializeField] private float idleSpeedThreshold = 0.15f;
    [SerializeField] private float speedDampTime = 0.12f;
    [SerializeField] private Vector2 animationSpeedRange = new Vector2(0.92f, 1.08f);
    [SerializeField] private float initialPhaseOffset = 0.8f;

    private PreyAgent preyAgent;
    private PredatorAgent predatorAgent;
    private int speedParameterHash;
    private int attackTriggerHash;
    private int celebrateTriggerHash;
    private int hitTriggerHash;
    private int defeatTriggerHash;
    private bool hasSpeedParameter;
    private bool hasAttackTrigger;
    private bool hasCelebrateTrigger;
    private bool hasHitTrigger;
    private bool hasDefeatTrigger;
    private float baseAnimatorSpeed = 1f;

    private void Awake()
    {
        preyAgent = GetComponent<PreyAgent>();
        predatorAgent = GetComponent<PredatorAgent>();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        speedParameterHash = Animator.StringToHash(speedParameter);
        attackTriggerHash = Animator.StringToHash(attackTrigger);
        celebrateTriggerHash = Animator.StringToHash(celebrateTrigger);
        hitTriggerHash = Animator.StringToHash(hitTrigger);
        defeatTriggerHash = Animator.StringToHash(defeatTrigger);
        ConfigureAnimator();
    }

    private void OnValidate()
    {
        fullRunSpeed = Mathf.Max(0.1f, fullRunSpeed);
        idleSpeedThreshold = Mathf.Clamp(idleSpeedThreshold, 0f, fullRunSpeed);
        speedDampTime = Mathf.Max(0f, speedDampTime);
        initialPhaseOffset = Mathf.Max(0f, initialPhaseOffset);

        animationSpeedRange = new Vector2(
            Mathf.Max(0.05f, animationSpeedRange.x),
            Mathf.Max(Mathf.Max(0.05f, animationSpeedRange.x), animationSpeedRange.y));

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    public void TriggerAttack()
    {
        TrySetTrigger(attackTriggerHash, hasAttackTrigger);
    }

    public void TriggerCelebrate()
    {
        TrySetTrigger(celebrateTriggerHash, hasCelebrateTrigger);
    }

    public void TriggerHit()
    {
        TrySetTrigger(hitTriggerHash, hasHitTrigger);
    }

    public void TriggerDefeat()
    {
        TrySetTrigger(defeatTriggerHash, hasDefeatTrigger);
    }

    private void Update()
    {
        if (animator == null || !hasSpeedParameter)
        {
            return;
        }

        float speed = GetAgentSpeed();
        float normalizedSpeed = Mathf.InverseLerp(idleSpeedThreshold, fullRunSpeed, speed);
        animator.SetFloat(speedParameterHash, normalizedSpeed, speedDampTime, Time.deltaTime);
    }

    private float GetAgentSpeed()
    {
        if (preyAgent != null)
        {
            return preyAgent.Velocity.magnitude;
        }

        if (predatorAgent != null)
        {
            return predatorAgent.Velocity.magnitude;
        }

        return 0f;
    }

    private void ConfigureAnimator()
    {
        if (animator == null)
        {
            return;
        }

        animator.applyRootMotion = false;
        CacheAnimatorParameters();

        baseAnimatorSpeed = animator.speed;
        animator.speed = baseAnimatorSpeed * Random.Range(animationSpeedRange.x, animationSpeedRange.y);

        if (initialPhaseOffset > 0f)
        {
            animator.Update(Random.Range(0f, initialPhaseOffset));
        }
    }

    private void CacheAnimatorParameters()
    {
        hasSpeedParameter = false;
        hasAttackTrigger = false;
        hasCelebrateTrigger = false;
        hasHitTrigger = false;
        hasDefeatTrigger = false;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == AnimatorControllerParameterType.Float && parameter.nameHash == speedParameterHash)
            {
                hasSpeedParameter = true;
            }
            else if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.nameHash == attackTriggerHash)
            {
                hasAttackTrigger = true;
            }
            else if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.nameHash == celebrateTriggerHash)
            {
                hasCelebrateTrigger = true;
            }
            else if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.nameHash == hitTriggerHash)
            {
                hasHitTrigger = true;
            }
            else if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.nameHash == defeatTriggerHash)
            {
                hasDefeatTrigger = true;
            }
        }
    }

    private void TrySetTrigger(int triggerHash, bool hasTrigger)
    {
        if (animator == null || !hasTrigger)
        {
            return;
        }

        animator.SetTrigger(triggerHash);
    }
}
