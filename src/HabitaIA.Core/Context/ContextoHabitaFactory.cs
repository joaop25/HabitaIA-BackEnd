using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;


namespace HabitaIA.Core.Context
{
    public class ContextoHabitaFactory : IDesignTimeDbContextFactory<ContextoHabita>
    {
        public ContextoHabita CreateDbContext(string[] args)
        {
            // Lê o appsettings da API para garantir mesma string
            var basePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "HabitaIA.API"));
            var cfg = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var conn = cfg.GetConnectionString("pg");

            var options = new DbContextOptionsBuilder<ContextoHabita>()
                .UseNpgsql(conn)
                .Options;

            return new ContextoHabita(options); // construtor sem IHttpContext
        }
    }
}
