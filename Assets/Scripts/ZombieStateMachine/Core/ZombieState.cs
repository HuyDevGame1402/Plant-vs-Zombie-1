using UnityEngine;

public abstract class ZombieState
{
    protected BaseZombieReanim zombie;
    protected ZombieStateMachine stateMachine;

    protected ZombieState(
        BaseZombieReanim zombie,
        ZombieStateMachine stateMachine)
    {
        this.zombie = zombie;
        this.stateMachine = stateMachine;
    }

    // Được gọi khi bắt đầu State
    public virtual void Enter() { }

    // Được gọi mỗi frame
    public virtual void Update() { }

    // Được gọi khi thoát State
    public virtual void Exit() { }
}