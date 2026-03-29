using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WarpBusiness.MobileApp.Services;
using WarpBusiness.Shared.Crm;

namespace WarpBusiness.MobileApp.ViewModels;

[QueryProperty(nameof(DealId), "id")]
public partial class DealDetailViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty] private string? _dealId;
    [ObservableProperty] private DealDto? _deal;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public DealDetailViewModel(ApiClient api) => _api = api;

    partial void OnDealIdChanged(string? value) { if (Guid.TryParse(value, out _)) LoadCommand.Execute(null); }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (!Guid.TryParse(DealId, out var id)) return;
        IsBusy = true;
        try { Deal = await _api.GetDealAsync(id); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand] private async Task EditAsync() => await Shell.Current.GoToAsync($"dealEdit?id={DealId}");

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (!Guid.TryParse(DealId, out var id)) return;
        if (!await Shell.Current.DisplayAlertAsync("Delete", "Delete this deal?", "Yes", "No")) return;
        try { await _api.DeleteDealAsync(id); await Shell.Current.GoToAsync(".."); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }
}
