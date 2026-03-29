using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WarpBusiness.MobileApp.Services;
using WarpBusiness.Shared.Crm;

namespace WarpBusiness.MobileApp.ViewModels;

[QueryProperty(nameof(ContactId), "id")]
public partial class ContactDetailViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty] private string? _contactId;
    [ObservableProperty] private ContactDto? _contact;
    [ObservableProperty] private ObservableCollection<CustomFieldValueDto> _customFields = [];
    [ObservableProperty] private ObservableCollection<ContactEmployeeRelationshipDto> _relationships = [];
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public ContactDetailViewModel(ApiClient api) => _api = api;

    partial void OnContactIdChanged(string? value) { if (Guid.TryParse(value, out _)) LoadCommand.Execute(null); }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (!Guid.TryParse(ContactId, out var id)) return;
        IsBusy = true;
        try
        {
            Contact = await _api.GetContactAsync(id);
            if (Contact != null)
            {
                CustomFields = new ObservableCollection<CustomFieldValueDto>(Contact.CustomFields ?? []);
                Relationships = new ObservableCollection<ContactEmployeeRelationshipDto>(Contact.Relationships ?? []);
            }
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand] private async Task EditAsync() => await Shell.Current.GoToAsync($"contactEdit?id={ContactId}");

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (!Guid.TryParse(ContactId, out var id)) return;
        if (!await Shell.Current.DisplayAlertAsync("Delete", "Delete this contact?", "Yes", "No")) return;
        try { await _api.DeleteContactAsync(id); await Shell.Current.GoToAsync(".."); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }
}
