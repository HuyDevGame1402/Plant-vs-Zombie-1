using UnityEngine;

public abstract class Plant : MonoBehaviour
{
    [Header("Lane Settings")]
    [SerializeField] protected int laneIndex = 2;
    public int LaneIndex => laneIndex;

    [SerializeField] private int eatPriority = 0;
    public int EatPriority => eatPriority;

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