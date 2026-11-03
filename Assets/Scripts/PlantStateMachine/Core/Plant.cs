using UnityEngine;

public abstract class Plant : MonoBehaviour
{
    protected PlantStateMachine stateMachine;

    public PlantData plantData;

    protected virtual void Awake()
    {
        stateMachine = new PlantStateMachine();
    }

    protected virtual void Start()
    {
        InitializeStates();
    }

    protected virtual void Update()
    {
        stateMachine.Update();
    }

    protected abstract void InitializeStates();
}