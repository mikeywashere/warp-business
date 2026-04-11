namespace WarpBusiness.Web.Services;

public class TenantStateService
{
    public Guid? SelectedTenantId { get; private set; }
    public string? SelectedTenantName { get; private set; }

    public event Action? OnTenantChanged;

    public void SetTenant(Guid tenantId, string tenantName)
    {
        SelectedTenantId = tenantId;
        SelectedTenantName = tenantName;
        OnTenantChanged?.Invoke();
    }

    public void ClearTenant()
    {
        SelectedTenantId = null;
        SelectedTenantName = null;
        OnTenantChanged?.Invoke();
    }
}
