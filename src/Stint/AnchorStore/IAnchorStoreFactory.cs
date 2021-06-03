namespace Stint
{
    public interface IAnchorStoreFactory
    {
        IAnchorStore GetAnchorStore(string name);
    }
}
