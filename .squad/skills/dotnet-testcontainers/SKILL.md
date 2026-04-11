# SKILL: .NET Testcontainers PostgreSQL Testing

## When to Use

When testing .NET applications that use PostgreSQL with EF Core, especially for migration testing, unique constraint testing, and FK cascade behavior that InMemory provider cannot verify.

## Pattern

### 1. Fixture with Shared Container

```csharp
public class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    // xUnit 2.x uses Task; xUnit 3.x uses ValueTask
    public async Task InitializeAsync() => await _container.StartAsync();
    public async Task DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<PostgreSqlFixture> { }
```

### 2. Test Class Using the Fixture

```csharp
[Collection("Database")]
public class MyTests
{
    private readonly PostgreSqlFixture _fixture;

    public MyTests(PostgreSqlFixture fixture) => _fixture = fixture;

    private WarpBusinessDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<WarpBusinessDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;
        return new WarpBusinessDbContext(options);
    }
}
```

### 3. Clean State Pattern

Always clean tables before each test for isolation:
```csharp
db.UserTenantMemberships.RemoveRange(db.UserTenantMemberships);
db.Users.RemoveRange(db.Users);
db.Tenants.RemoveRange(db.Tenants);
await db.SaveChangesAsync();
```

## Gotchas

- **xUnit 2.x vs 3.x**: `IAsyncLifetime` returns `Task` in v2, `ValueTask` in v3
- **Container startup**: ~20s first time, Docker must be running
- **NSubstitute + non-virtual methods**: Use `FakeHttpMessageHandler` instead of mocking concrete services
- **Anonymous types in Results**: `Results.Conflict(new { message = "..." })` produces `Conflict<AnonymousType>`, not `Conflict<object>`. Assert via reflection on `StatusCode` property
