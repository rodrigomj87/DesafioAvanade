using Inventory.Integration.Tests.Fixtures;

namespace Inventory.Integration.Tests;

[CollectionDefinition("Integration")]
public sealed class IntegrationTestCollection : ICollectionFixture<IntegrationTestFixture>
{
}
