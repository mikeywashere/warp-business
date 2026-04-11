namespace WarpBusiness.Api.Tests.Infrastructure;

[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<PostgreSqlFixture>
{
}
