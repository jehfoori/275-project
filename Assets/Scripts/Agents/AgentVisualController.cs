using UnityEngine;

[DisallowMultipleComponent]
public sealed class AgentVisualController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private float fullRunSpeed = 5f;
    [SerializeField] private float idleSpeedThreshold = 0.15f;
    [SerializeField] private float speedDampTime = 0.12f;
    [SerializeField] private Vector2 animationSpeedRange = new Vector2(0.92f, 1.08f);
    [SerializeField] private float initialPhaseOffset = 0.8f;

    private PreyAgent preyAgent;
    private PredatorAgent predatorAgent;
    private int speedParameterHash;
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

    private void Update()
    {
        if (animator == null || string.IsNullOrEmpty(speedParameter))
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

        baseAnimatorSpeed = animator.speed;
        animator.speed = baseAnimatorSpeed * Random.Range(animationSpeedRange.x, animationSpeedRange.y);

        if (initialPhaseOffset > 0f)
        {
            animator.Update(Random.Range(0f, initialPhaseOffset));
        }
    }
}
