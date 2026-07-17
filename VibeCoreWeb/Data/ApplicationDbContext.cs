using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VibeCore.Models;

namespace VibeCore.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext(options), IDataProtectionKeyContext
{
    public DbSet<TodoItem> Todos { get; set; }
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }
}
