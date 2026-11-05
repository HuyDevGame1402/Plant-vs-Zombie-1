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
        zombie.SetWalkMotionSpeed(moveSpeed);
        zombie.Walk();
    }

    public override void Update()
    {
        if (zombie.HasGroundMotion)
            return;

        zombie.transform.position +=
            Vector3.left * moveSpeed * Time.deltaTime;
    }
}
