using Rambler.Control;

//Stops fighting and moving; Health plays the death itself
public class EnemyDead : IState
{
    readonly EnemyAI ai;

    public EnemyDead(EnemyAI ai) => this.ai = ai;

    public void OnEnter()
    {
        ai.HoldFire();
        if (ai.Cover != null) ai.Cover.Leave();
        if (ai.Thrower != null) ai.Thrower.Cancel();
        ai.Stop();
    }

    public void OnExit() { }

    public void Tick() { }
}
