using UnityEngine;

public class ZombieWalkState : ZombieState
{
    private float moveSpeed;

    public ZombieWalkState(
        BaseZombieReanim zombie,
        ZombieStateMachine stateMachine,
        float moveSpeed)
        : base(zombie, stateMachine)
    {
        this.moveSpeed = moveSpeed;
    }

    public override void Enter()
    {
        zombie.Walk();
    }

    public override void Update()
    {
        zombie.transform.position +=
            Vector3.left * moveSpeed * Time.deltaTime;
    }
}