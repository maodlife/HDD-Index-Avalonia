using System;
using System.Collections.Generic;
using HDD_Index.Application.Persistence;

namespace HDD_Index.Application.Startup;

public enum ApplicationStartupState
{
    Ready,
    FirstRun,
    Blocked,
}

public sealed class ApplicationStartupResult
{
    private ApplicationStartupResult(
        ApplicationStartupState state,
        string configFilePath,
        ApplicationSession? session,
        SessionLoadIssue? blockingIssue,
        IReadOnlyList<SessionLoadIssue> warnings)
    {
        State = state;
        ConfigFilePath = configFilePath;
        Session = session;
        BlockingIssue = blockingIssue;
        Warnings = warnings;
    }

    public ApplicationStartupState State { get; }

    public string ConfigFilePath { get; }

    public ApplicationSession? Session { get; }

    public SessionLoadIssue? BlockingIssue { get; }

    public IReadOnlyList<SessionLoadIssue> Warnings { get; }

    public static ApplicationStartupResult Ready(
        string configFilePath,
        ApplicationSession session,
        IReadOnlyList<SessionLoadIssue>? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new ApplicationStartupResult(
            ApplicationStartupState.Ready,
            configFilePath,
            session,
            null,
            warnings ?? Array.Empty<SessionLoadIssue>());
    }

    public static ApplicationStartupResult FirstRun(
        string configFilePath,
        SessionLoadIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        return new ApplicationStartupResult(
            ApplicationStartupState.FirstRun,
            configFilePath,
            null,
            issue,
            Array.Empty<SessionLoadIssue>());
    }

    public static ApplicationStartupResult Blocked(
        string configFilePath,
        SessionLoadIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        return new ApplicationStartupResult(
            ApplicationStartupState.Blocked,
            configFilePath,
            null,
            issue,
            Array.Empty<SessionLoadIssue>());
    }
}

public interface IApplicationStartupService
{
    ApplicationStartupResult LoadDefault();

    ApplicationStartupResult Load(string configFilePath);

    ApplicationStartupResult CreateDefault(string dataDirectoryPath);

    ApplicationStartupResult RepairDataDirectory(
        string configFilePath,
        string dataDirectoryPath);
}
