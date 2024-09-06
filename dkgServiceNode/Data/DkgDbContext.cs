using dkgServiceNode.Models;
using Microsoft.EntityFrameworkCore;

namespace dkgServiceNode.Data
{
    public class DkgDbContext : DbContext
    {
        public DbSet<Node> Nodes { get; set; }
        public DbSet<Round> Rounds { get; set; }

        public DkgDbContext(DbContextOptions<DkgContext> options) : base(options)
        {
        }
    }
}
