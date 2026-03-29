using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WarpBusiness.MobileApp.Services;
using WarpBusiness.Shared.Crm;

namespace WarpBusiness.MobileApp.ViewModels;

[QueryProperty(nameof(ActivityId), "id")]
public partial class ActivityDetailViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty] private string? _activityId;
    [ObservableProperty] private ActivityDto? _activity;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public ActivityDetailViewModel(ApiClient api) => _api = api;

    partial void OnActivityIdChanged(string? value) { if (Guid.TryParse(value, out _)) LoadCommand.Execute(null); }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (!Guid.TryParse(ActivityId, out var id)) return;
        IsBusy = true;
        try { Activity = await _api.GetActivityAsync(id); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand] private async Task EditAsync() => await Shell.Current.GoToAsync($"activityEdit?id={ActivityId}");

    [RelayCommand]
    private async Task CompleteAsync()
    {
        if (!Guid.TryParse(ActivityId, out var id)) return;
        try { await _api.CompleteActivityAsync(id); await LoadAsync(); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (!Guid.TryParse(ActivityId, out var id)) return;
        if (!await Shell.Current.DisplayAlertAsync("Delete", "Delete this activity?", "Yes", "No")) return;
        try { await _api.DeleteActivityAsync(id); await Shell.Current.GoToAsync(".."); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }
}
