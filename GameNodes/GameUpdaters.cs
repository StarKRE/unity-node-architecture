namespace GameNodes
{
    public interface IGameUpdater
    {
        void Update(float deltaTime);
    }

    public interface IGameFixedUpdater
    {
        void FixedUpdate(float deltaTime);
    }

    public interface IGameLateUpdater
    {
        void LateUpdate(float deltaTime);
    }
}