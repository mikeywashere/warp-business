using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WarpBusiness.MobileApp.Services;
using WarpBusiness.Shared.TimeTracking;

namespace WarpBusiness.MobileApp.ViewModels;

[QueryProperty(nameof(TimeEntryId), "id")]
public partial class TimeEntryDetailViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty] private string? _timeEntryId;
    [ObservableProperty] private TimeEntryDetailDto? _timeEntry;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public TimeEntryDetailViewModel(ApiClient api) => _api = api;

    partial void OnTimeEntryIdChanged(string? value) { if (Guid.TryParse(value, out _)) LoadCommand.Execute(null); }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (!Guid.TryParse(TimeEntryId, out var id)) return;
        IsBusy = true;
        try { TimeEntry = await _api.GetTimeEntryAsync(id); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand] private async Task EditAsync() => await Shell.Current.GoToAsync($"timeEntryEdit?id={TimeEntryId}");

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (!Guid.TryParse(TimeEntryId, out var id)) return;
        try { await _api.SubmitTimeEntryAsync(id); await LoadAsync(); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task ApproveAsync()
    {
        if (!Guid.TryParse(TimeEntryId, out var id)) return;
        try { await _api.ApproveTimeEntryAsync(id); await LoadAsync(); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task RejectAsync()
    {
        if (!Guid.TryParse(TimeEntryId, out var id)) return;
        try { await _api.RejectTimeEntryAsync(id); await LoadAsync(); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (!Guid.TryParse(TimeEntryId, out var id)) return;
        if (!await Shell.Current.DisplayAlertAsync("Delete", "Delete this time entry?", "Yes", "No")) return;
        try { await _api.DeleteTimeEntryAsync(id); await Shell.Current.GoToAsync(".."); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }
}
