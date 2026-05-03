using System;
using System.Collections.Generic;
using System.Reactive;
using HDD_Index.Services;
using ReactiveUI;

namespace HDD_Index.ViewModels;

public class RepoNodeSearchViewModel : ViewModelBase
{
    private IReadOnlyList<RepoNodeSearchMatch> _matches =
        Array.Empty<RepoNodeSearchMatch>();

    private string _searchText = string.Empty;
    private int _currentMatchIndex = -1;

    public string SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }

    public int CurrentMatchIndex
    {
        get => _currentMatchIndex;
        private set
        {
            if (EqualityComparer<int>.Default.Equals(_currentMatchIndex, value))
                return;

            this.RaiseAndSetIfChanged(ref _currentMatchIndex, value);
            RaiseMatchStateChanged();
        }
    }

    public int CurrentMatchNumber => CurrentMatchIndex >= 0
        ? CurrentMatchIndex + 1
        : 0;

    public int TotalMatchCount => _matches.Count;

    public string MatchCounterText => $"{CurrentMatchNumber}/{TotalMatchCount}";

    public bool HasMatches => TotalMatchCount > 0;

    public RepoNodeSearchMatch? CurrentMatch =>
        CurrentMatchIndex >= 0 && CurrentMatchIndex < _matches.Count
            ? _matches[CurrentMatchIndex]
            : null;

    public ReactiveCommand<Unit, Unit> SearchPreviousCommand { get; }

    public ReactiveCommand<Unit, Unit> SearchNextCommand { get; }

    public RepoNodeSearchViewModel()
    {
        SearchPreviousCommand = ReactiveCommand.Create(SelectPreviousMatch);
        SearchNextCommand = ReactiveCommand.Create(SelectNextMatch);
    }

    public void RefreshMatches(RepoNodeVM root, RepoNodeVM? preferredNode = null)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            _matches = Array.Empty<RepoNodeSearchMatch>();
            CurrentMatchIndex = -1;
            RaiseMatchStateChanged();
            return;
        }

        _matches = TreeNavigationService.FindRepoNodeVmsByNameContains(root, SearchText);
        CurrentMatchIndex = GetInitialMatchIndex(preferredNode);
        RaiseMatchStateChanged();
    }

    private int GetInitialMatchIndex(RepoNodeVM? preferredNode)
    {
        if (_matches.Count == 0)
            return -1;

        if (preferredNode != null)
        {
            for (var i = 0; i < _matches.Count; i++)
            {
                if (ReferenceEquals(_matches[i].Node, preferredNode))
                    return i;
            }
        }

        return 0;
    }

    private void SelectPreviousMatch()
    {
        if (_matches.Count == 0)
            return;

        CurrentMatchIndex = CurrentMatchIndex <= 0
            ? _matches.Count - 1
            : CurrentMatchIndex - 1;
    }

    private void SelectNextMatch()
    {
        if (_matches.Count == 0)
            return;

        CurrentMatchIndex = CurrentMatchIndex >= _matches.Count - 1
            ? 0
            : CurrentMatchIndex + 1;
    }

    private void RaiseMatchStateChanged()
    {
        this.RaisePropertyChanged(nameof(CurrentMatchNumber));
        this.RaisePropertyChanged(nameof(TotalMatchCount));
        this.RaisePropertyChanged(nameof(MatchCounterText));
        this.RaisePropertyChanged(nameof(HasMatches));
        this.RaisePropertyChanged(nameof(CurrentMatch));
    }
}
