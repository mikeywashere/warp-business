using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WarpBusiness.MobileApp.Services;
using WarpBusiness.Shared.Crm;

namespace WarpBusiness.MobileApp.ViewModels;

[QueryProperty(nameof(ActivityId), "id")]
public partial class ActivityEditViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty] private string? _activityId;
    [ObservableProperty] private bool _isNew = true;
    [ObservableProperty] private string _type = "Call";
    [ObservableProperty] private string? _subject;
    [ObservableProperty] private string? _description;
    [ObservableProperty] private DateTime _dueDate = DateTime.Today;
    [ObservableProperty] private TimeSpan _dueTime = TimeSpan.FromHours(9);
    [ObservableProperty] private bool _isCompleted;
    [ObservableProperty] private Guid? _contactId;
    [ObservableProperty] private Guid? _dealId;
    [ObservableProperty] private ObservableCollection<ContactDto> _contacts = [];
    [ObservableProperty] private ObservableCollection<DealDto> _deals = [];
    [ObservableProperty] private ContactDto? _selectedContact;
    [ObservableProperty] private DealDto? _selectedDeal;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public static List<string> ActivityTypes => ["Call", "Email", "Meeting", "Task", "Note"];

    public ActivityEditViewModel(ApiClient api) => _api = api;

    partial void OnActivityIdChanged(string? value) { if (Guid.TryParse(value, out _)) { IsNew = false; LoadCommand.Execute(null); } }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var contactsTask = _api.GetContactsAsync(1, 100);
            var dealsTask = _api.GetDealsAsync(1, 100);
            await Task.WhenAll(contactsTask, dealsTask);

            if (contactsTask.Result != null) Contacts = new ObservableCollection<ContactDto>(contactsTask.Result.Items);
            if (dealsTask.Result != null) Deals = new ObservableCollection<DealDto>(dealsTask.Result.Items);

            if (!IsNew && Guid.TryParse(ActivityId, out var id))
            {
                var a = await _api.GetActivityAsync(id);
                if (a != null)
                {
                    Type = a.Type; Subject = a.Subject; Description = a.Description;
                    DueDate = a.DueDate.LocalDateTime.Date;
                    DueTime = a.DueDate.LocalDateTime.TimeOfDay;
                    IsCompleted = a.IsCompleted;
                    ContactId = a.ContactId; DealId = a.DealId;
                    SelectedContact = Contacts.FirstOrDefault(x => x.Id == a.ContactId);
                    SelectedDeal = Deals.FirstOrDefault(x => x.Id == a.DealId);
                }
            }
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Subject)) { ErrorMessage = "Subject is required"; return; }
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var selectedContactId = SelectedContact?.Id ?? ContactId;
            var selectedDealId = SelectedDeal?.Id ?? DealId;
            var dueDateTimeOffset = new DateTimeOffset(DueDate.Add(DueTime));

            if (IsNew)
            {
                await _api.CreateActivityAsync(new CreateActivityRequest(
                    Type, Subject!, Description, dueDateTimeOffset, selectedContactId, selectedDealId));
            }
            else if (Guid.TryParse(ActivityId, out var id))
            {
                await _api.UpdateActivityAsync(id, new UpdateActivityRequest(
                    Type, Subject!, Description, dueDateTimeOffset, IsCompleted));
            }
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex) { ErrorMessage = $"Save failed: {ex.Message}"; }
        finally { IsBusy = false; }
    }
}
