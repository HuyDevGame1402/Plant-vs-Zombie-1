public class ProduceState : IPlantState
{
    private readonly Plant plant;
    private readonly IProduce produce;

    public ProduceState(Plant plant, IProduce produce)
    {
        this.plant = plant;
        this.produce = produce;
    }

    public void Enter() { produce.OnProduceEnter(); }
    public void Update() { produce.OnProduceUpdate(); }
    public void Exit() { produce.OnProduceExit(); }
}