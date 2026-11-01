public class ZombieStateMachine
{
    public ZombieState CurrentState { get; private set; }

    public void Initialize(ZombieState startingState)
    {
        CurrentState = startingState;
        CurrentState.Enter();
    }

    public void ChangeState(ZombieState newState)
    {
        if (newState == null || newState == CurrentState)
            return;

        CurrentState?.Exit();

        CurrentState = newState;

        CurrentState.Enter();
    }

    public void Update()
    {
        CurrentState?.Update();
    }
}