using UnityEngine;

public class NormalZombieAI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BaseZombieReanim zombie;
    [SerializeField] private TriggerAttackZombie attackTrigger;

    private ZombieStateMachine stateMachine;
    private bool initialized;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 0.3f;

    private void Awake()
    {
        if (zombie == null)
            zombie = GetComponent<BaseZombieReanim>();

        if (attackTrigger == null)
            attackTrigger = GetComponentInChildren<TriggerAttackZombie>();

        stateMachine = new ZombieStateMachine();
    }

    private void Update()
    {
        if (!initialized)
        {
            if (zombie == null || !zombie.IsReady || attackTrigger == null)
                return;

            InitializeStateMachine();
            return;
        }

        if (zombie.IsDeadOrDying)
            return;

        stateMachine.Update();
    }

    private void InitializeStateMachine()
    {
        stateMachine.Initialize(
            new ZombieWalkState(zombie, stateMachine, attackTrigger, moveSpeed)
        );

        initialized = true;
    }
}