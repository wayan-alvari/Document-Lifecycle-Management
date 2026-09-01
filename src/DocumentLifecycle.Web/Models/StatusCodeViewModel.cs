namespace DocumentLifecycle.Web.Models;

public sealed record StatusCodeViewModel(
    int StatusCode,
    string Title,
    string Message);
