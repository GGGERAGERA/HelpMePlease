public static class AICommentGenerator
{
    public static string GetComment(bool victory)
    {
        return LocalizationService.EnsureExists().Get(
            victory ? "result.comment.victory" : "result.comment.defeat");
    }
}
