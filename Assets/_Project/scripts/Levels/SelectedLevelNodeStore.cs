public static class SelectedLevelNodeStore
{
    public static LevelNodeData SelectedNode { get; private set; }

    public static bool HasSelectedNode => SelectedNode != null;

    public static void Set(LevelNodeData node)
    {
        SelectedNode = node;
    }

    public static void Clear()
    {
        SelectedNode = null;
    }
}