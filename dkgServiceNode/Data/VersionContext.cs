using dkgServiceNode.Models;
using Microsoft.EntityFrameworkCore;

namespace dkgServiceNode.Data
{
    public class VersionContext : DbContext
    {
            public VersionContext(DbContextOptions<VersionContext> options) : base(options) { }

            public DbSet<Models.Version> Versions { get; set; }

        }
    }
