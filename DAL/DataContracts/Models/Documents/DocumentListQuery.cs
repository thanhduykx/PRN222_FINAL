namespace PRN222_FINAL.DAL.Models;

public sealed record DocumentListQuery(
    string? Query = null,
    string? SubjectFilter = null,
    string? StatusFilter = null);
