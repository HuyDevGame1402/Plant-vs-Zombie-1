public interface IIdle
{
    void OnIdleEnter();
    void OnIdleUpdate();
    void OnIdleExit();
}

public interface IAttack
{
    void OnAttackEnter();
    void OnAttackUpdate();
    void OnAttackExit();
}

public interface ISleep
{
    void OnSleepEnter();
    void OnSleepUpdate();
    void OnSleepExit();
}

public interface IProduce
{
    void OnProduceEnter();
    void OnProduceUpdate();
    void OnProduceExit();
}

public interface IExplode
{
    void OnExplodeEnter();
    void OnExplodeUpdate();
    void OnExplodeExit();
}

public interface IPlantState
{
    void Enter();
    void Update();
    void Exit();
}