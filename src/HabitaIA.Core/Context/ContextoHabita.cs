using HabitaIA.Business.Imovel.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HabitaIA.Core.Context
{
    public class ContextoHabita : DbContext
    {
        private readonly Guid? _tenantId;

        // ===== Runtime (API): com IHttpContextAccessor =====
        public ContextoHabita(DbContextOptions<ContextoHabita> options, IHttpContextAccessor http)
            : base(options)
        {
            var claim = http.HttpContext?.User?.FindFirst("tenant_id")?.Value
                        ?? http.HttpContext?.Request.Headers["X-Tenant-Id"].FirstOrDefault();
            if (Guid.TryParse(claim, out var t)) _tenantId = t;
        }

        // ===== Design-time (migrations): sem HttpContext =====
        public ContextoHabita(DbContextOptions<ContextoHabita> options)
            : base(options)
        {
            // sem tenant em design-time -> filtro não é aplicado
        }

        public DbSet<ImovelModel> Imoveis => Set<ImovelModel>();

        protected override void OnModelCreating(ModelBuilder mb)
        {
            mb.ApplyConfigurationsFromAssembly(typeof(ContextoHabita).Assembly);

            // Aplica o filtro somente se _tenantId tiver valor (runtime)
            if (_tenantId is Guid tid)
            {
                mb.Entity<ImovelModel>().HasQueryFilter(i => i.TenantId == tid);
            }

            base.OnModelCreating(mb);
        }
    }

}
