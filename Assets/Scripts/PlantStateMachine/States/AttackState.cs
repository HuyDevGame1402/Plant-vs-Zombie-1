public class AttackState : IPlantState
{
    private readonly Plant plant;
    private readonly IAttack attack;

    public AttackState(Plant plant, IAttack attack)
    {
        this.plant = plant;
        this.attack = attack;
    }

    public void Enter()
    {
        attack.OnAttackEnter();
    }

    public void Update()
    {
        attack.OnAttackUpdate();
    }

    public void Exit()
    {
        attack.OnAttackExit();
    }
}