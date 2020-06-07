using DOTW.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DOTW.Data
{
    public class DotwDbContext : DbContext
    {
        public DbSet<DOTW.Models.Room> Room { get; set; }
        public DbSet<DOTW.Models.Row> Row { get; set; }
        public DbSet<DOTW.Models.Team> Team { get; set; }
        public DbSet<DOTW.Models.Chair> Chair { get; set; }
        public DbSet<DOTW.Models.Player> Player { get; set; }

        public DotwDbContext(DbContextOptions<DotwDbContext> options)
        : base(options)
        {
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Player>().ToTable("Player");
            modelBuilder.Entity<Chair>().ToTable("Chair");
            modelBuilder.Entity<Team>().ToTable("Team");
            modelBuilder.Entity<Row>().ToTable("Row");
            modelBuilder.Entity<Room>().ToTable("Room");
        }
    }
}
