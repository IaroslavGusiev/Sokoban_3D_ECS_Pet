namespace Sokoban.Presentation.Ecs.Components
{
    public readonly struct SkinChangeRequest
    {
        public readonly SkinId Id;
        public SkinChangeRequest(SkinId id) => Id = id;
    }
}
