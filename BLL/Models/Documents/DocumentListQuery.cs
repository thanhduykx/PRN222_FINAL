namespace PRN222_FINAL.BLL.Models;

public sealed record DocumentListQuery(
    string? Query = null,
    string? SubjectFilter = null,
    string? StatusFilter = null);
