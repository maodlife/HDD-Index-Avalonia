using System;
using System.Collections.Generic;

namespace HDD_Index.Application.Persistence;

public enum SessionLoadIssueKind
{
    ConfigurationMissing,
    ConfigurationInvalid,
    ConfigurationUnreadable,
    DataDirectoryMissing,
    DataDirectoryUnreadable,
    RepositoryMissing,
    RepositoryInvalid,
    RepositoryUnreadable,
    FileIndexConfigurationInvalid,
    FileIndexMissing,
    FileIndexInvalid,
    FileIndexUnreadable,
    InitializationConflict,
    InitializationFailed,
}

public sealed record SessionLoadIssue(
    SessionLoadIssueKind Kind,
    string FilePath,
    string Message,
    string? DiskLabel = null);

public sealed class SessionLoadResult
{
    private SessionLoadResult(
        ApplicationSession? session,
        SessionLoadIssue? blockingIssue,
        IReadOnlyList<SessionLoadIssue> warnings)
    {
        Session = session;
        BlockingIssue = blockingIssue;
        Warnings = warnings;
    }

    public ApplicationSession? Session { get; }

    public SessionLoadIssue? BlockingIssue { get; }

    public IReadOnlyList<SessionLoadIssue> Warnings { get; }

    public bool Succeeded => Session != null && BlockingIssue == null;

    public static SessionLoadResult Success(
        ApplicationSession session,
        IReadOnlyList<SessionLoadIssue>? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new SessionLoadResult(
            session,
            null,
            warnings ?? Array.Empty<SessionLoadIssue>());
    }

    public static SessionLoadResult Blocked(SessionLoadIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        return new SessionLoadResult(
            null,
            issue,
            Array.Empty<SessionLoadIssue>());
    }
}
