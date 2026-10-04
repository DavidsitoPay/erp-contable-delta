namespace DeltaERP.Tests.Support;

[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
