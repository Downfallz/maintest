namespace DA.Game.Infrastructure.Bootstrap
{
    public interface IGameCatalogCache
    {
        CatalogSnapshot? Get();
        void Set(CatalogSnapshot snapshot);
    }
}