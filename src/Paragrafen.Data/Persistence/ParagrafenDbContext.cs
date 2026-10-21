using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

using Paragrafen.Data.Identity;

namespace Paragrafen.Data.Persistence;

public sealed class ParagrafenDbContext(DbContextOptions<ParagrafenDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        // this needs to be first since it creates the AspNet* tables
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(ParagrafenDbContext).Assembly);
    }
}
