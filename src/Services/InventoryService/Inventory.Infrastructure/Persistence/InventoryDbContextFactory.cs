using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Inventory.Infrastructure.Persistence;

public sealed class InventoryDbContextFactory : IDesignTimeDbContextFactory<InventoryDbContext>
{
    public InventoryDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<InventoryDbContext>();
        optionsBuilder.UseSqlServer("Server=localhost,1433;Database=InventoryDb;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;");

        return new InventoryDbContext(optionsBuilder.Options);
    }
}
