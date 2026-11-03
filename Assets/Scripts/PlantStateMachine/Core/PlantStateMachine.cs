public class PlantStateMachine
{
    public IPlantState CurrentState { get; private set; }

    public void ChangeState(IPlantState newState)
    {
        if (newState == null) return;

        CurrentState?.Exit();

        CurrentState = newState;

        CurrentState.Enter();
    }

    public void Update()
    {
        CurrentState?.Update();
    }
}