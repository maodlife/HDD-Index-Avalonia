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
    private bool _isCurrentMatchActive;

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

    public string MatchCounterText => TotalMatchCount == 0
        ? "0/0"
        : $"{(IsCurrentMatchActive ? CurrentMatchNumber.ToString() : "?")}/{TotalMatchCount}";

    public bool HasMatches => TotalMatchCount > 0;

    public bool IsCurrentMatchActive
    {
        get => _isCurrentMatchActive;
        private set
        {
            if (EqualityComparer<bool>.Default.Equals(_isCurrentMatchActive, value))
                return;

            this.RaiseAndSetIfChanged(ref _isCurrentMatchActive, value);
            RaiseCounterStateChanged();
        }
    }

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
            IsCurrentMatchActive = false;
            RaiseMatchStateChanged();
            return;
        }

        _matches = TreeNavigationService.FindRepoNodeVmsByNameContains(root, SearchText);
        CurrentMatchIndex = GetInitialMatchIndex(preferredNode);
        IsCurrentMatchActive = CurrentMatchIndex >= 0;
        RaiseMatchStateChanged();
    }

    public void DeactivateCurrentMatch()
    {
        if (_matches.Count == 0)
            return;

        IsCurrentMatchActive = false;
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

        var oldMatchIndex = CurrentMatchIndex;
        CurrentMatchIndex = CurrentMatchIndex <= 0
            ? _matches.Count - 1
            : CurrentMatchIndex - 1;
        IsCurrentMatchActive = true;
        if (CurrentMatchIndex == oldMatchIndex)
            this.RaisePropertyChanged(nameof(CurrentMatch));
    }

    private void SelectNextMatch()
    {
        if (_matches.Count == 0)
            return;

        var oldMatchIndex = CurrentMatchIndex;
        CurrentMatchIndex = CurrentMatchIndex >= _matches.Count - 1
            ? 0
            : CurrentMatchIndex + 1;
        IsCurrentMatchActive = true;
        if (CurrentMatchIndex == oldMatchIndex)
            this.RaisePropertyChanged(nameof(CurrentMatch));
    }

    private void RaiseMatchStateChanged()
    {
        RaiseCounterStateChanged();
        this.RaisePropertyChanged(nameof(CurrentMatch));
    }

    private void RaiseCounterStateChanged()
    {
        this.RaisePropertyChanged(nameof(CurrentMatchNumber));
        this.RaisePropertyChanged(nameof(TotalMatchCount));
        this.RaisePropertyChanged(nameof(MatchCounterText));
        this.RaisePropertyChanged(nameof(HasMatches));
        this.RaisePropertyChanged(nameof(IsCurrentMatchActive));
    }
}
