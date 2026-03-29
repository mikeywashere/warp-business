using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WarpBusiness.MobileApp.Services;
using WarpBusiness.Shared.TimeTracking;

namespace WarpBusiness.MobileApp.ViewModels;

public partial class TimeEntryTypesViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty] private ObservableCollection<TimeEntryTypeDto> _entryTypes = [];
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    // Edit form fields
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private Guid? _editingId;
    [ObservableProperty] private string? _name;
    [ObservableProperty] private string? _description;
    [ObservableProperty] private int _displayOrder;
    [ObservableProperty] private bool _isActive = true;
    [ObservableProperty] private bool _isBillable;

    public TimeEntryTypesViewModel(ApiClient api) => _api = api;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var types = await _api.GetTimeEntryTypesAsync(includeInactive: true);
            EntryTypes = new ObservableCollection<TimeEntryTypeDto>(types ?? []);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void StartCreate()
    {
        EditingId = null; Name = null; Description = null;
        DisplayOrder = 0; IsActive = true; IsBillable = false;
        IsEditing = true;
    }

    [RelayCommand]
    private void StartEdit(TimeEntryTypeDto type)
    {
        EditingId = type.Id; Name = type.Name; Description = type.Description;
        DisplayOrder = type.DisplayOrder; IsActive = type.IsActive; IsBillable = type.IsBillable;
        IsEditing = true;
    }

    [RelayCommand]
    private void CancelEdit() => IsEditing = false;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name)) { ErrorMessage = "Name is required"; return; }
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            if (EditingId == null)
            {
                await _api.CreateTimeEntryTypeAsync(new CreateTimeEntryTypeRequest(
                    Name!, Description, DisplayOrder, IsActive, IsBillable));
            }
            else
            {
                await _api.UpdateTimeEntryTypeAsync(EditingId.Value, new UpdateTimeEntryTypeRequest(
                    Name!, Description, DisplayOrder, IsActive, IsBillable));
            }
            IsEditing = false;
            await LoadAsync();
        }
        catch (Exception ex) { ErrorMessage = $"Save failed: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DeleteAsync(TimeEntryTypeDto type)
    {
        if (!await Shell.Current.DisplayAlertAsync("Delete", $"Delete type '{type.Name}'?", "Yes", "No")) return;
        try { await _api.DeleteTimeEntryTypeAsync(type.Id); await LoadAsync(); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }
}
