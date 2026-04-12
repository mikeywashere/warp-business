using System.Text.Json.Serialization;

namespace WarpBusiness.Api.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UserRole
{
    User = 0,
    SystemAdministrator = 1
}
