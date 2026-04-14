namespace WarpBusiness.Crm.Tests.Infrastructure;

[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<PostgreSqlFixture>
{
}
