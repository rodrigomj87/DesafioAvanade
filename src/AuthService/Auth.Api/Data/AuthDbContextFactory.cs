using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Auth.Api.Data;

public sealed class AuthDbContextFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
    public AuthDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AuthDbContext>();
        optionsBuilder.UseSqlServer("Server=localhost,1433;Database=AuthDb;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;");
        return new AuthDbContext(optionsBuilder.Options);
    }
}
