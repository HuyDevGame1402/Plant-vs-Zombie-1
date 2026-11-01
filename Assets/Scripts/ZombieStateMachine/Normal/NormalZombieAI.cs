using UnityEngine;

public class NormalZombieAI : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private BaseZombieReanim zombie;

    private ZombieStateMachine stateMachine;

    private bool initialized;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 0.3f;

    private void Awake()
    {
        if (zombie == null)
            zombie = GetComponent<BaseZombieReanim>();

        stateMachine = new ZombieStateMachine();
    }

    private void Update()
    {
        // Chờ Reanim khởi tạo xong
        if (!initialized)
        {
            if (zombie == null || !zombie.IsReady)
                return;

            InitializeStateMachine();
            return;
        }

        stateMachine.Update();
    }

    private void InitializeStateMachine()
    {
        stateMachine.Initialize(
            new ZombieWalkState(zombie, stateMachine, moveSpeed)
        );

        initialized = true;
    }
}