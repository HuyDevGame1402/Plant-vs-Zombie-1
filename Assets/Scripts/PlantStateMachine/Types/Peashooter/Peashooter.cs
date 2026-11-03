using UnityEngine;
using PlantReanimation.Controllers.Peashooter;

public class Peashooter : Plant, IIdle, IAttack
{
    [SerializeField] private PeashooterController peashooterControllerAnimation;

    private IdleState idleState;
    private AttackState attackState;

    protected override void InitializeStates()
    {
        idleState = new IdleState(this, this);
        attackState = new AttackState(this, this);

        stateMachine.ChangeState(idleState);
    }

    public void OnIdleEnter()
    {
        peashooterControllerAnimation.gameObject.SetActive(true);
        peashooterControllerAnimation.PlayAnimationIdle();
    }

    public void OnIdleUpdate()
    {
    }

    public void OnIdleExit()
    {
    }

    public void OnAttackEnter()
    {
        //animator.Play("Attack");
    }

    public void OnAttackUpdate()
    {
    }

    public void OnAttackExit()
    {
    }
}