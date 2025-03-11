using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ECommerce.Shared.Infrastructure.Outbox;

/// <summary>
/// Factory for creating the OutboxContext.
/// Because we are using a class library and don’t have a Program class, we need to implement the IDesignTimeDbContextFactory in the same folder to let EF Core know how to create an instance of our OutboxContext.
/// </summary>
internal class OutboxContextFactory : IDesignTimeDbContextFactory<OutboxContext>
{
    public OutboxContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<OutboxContext>();
        optionsBuilder.UseSqlServer();

        return new OutboxContext(optionsBuilder.Options);
    }
}