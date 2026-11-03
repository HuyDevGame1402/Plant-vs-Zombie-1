public class IdleState : IPlantState
{
    private readonly Plant plant;
    private readonly IIdle idle;

    public IdleState(Plant plant, IIdle idle)
    {
        this.plant = plant;
        this.idle = idle;
    }

    public void Enter()
    {
        idle.OnIdleEnter();
    }

    public void Update()
    {
        idle.OnIdleUpdate();
    }

    public void Exit()
    {
        idle.OnIdleExit();
    }
}