using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EFCore.GlobalQueryFiltersSample
{
    [Table("Users")]
    public class User : IRemovable
    {
        [Key]
        public Guid UserId { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string Name { get; set; }
        public bool IsRemoved { get; set; }
    }

    public class SampleDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        private static readonly MethodInfo ConfigureGlobalFiltersMethodInfo = typeof(SampleDbContext).GetMethod(nameof(ConfigureGlobalFilters), BindingFlags.Instance | BindingFlags.NonPublic);

        public SampleDbContext(DbContextOptions<SampleDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Option 1 - Manual
            modelBuilder.Entity<User>().HasQueryFilter(u => !u.IsRemoved);

            // Option 2 - Dynamic
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                ConfigureGlobalFiltersMethodInfo
                    .MakeGenericMethod(entityType.ClrType)
                    .Invoke(this, new object[] { modelBuilder, entityType });
            }

            base.OnModelCreating(modelBuilder);
        }

        protected void ConfigureGlobalFilters<TEntity>(ModelBuilder modelBuilder, IMutableEntityType entityType) where TEntity : class
        {
            if (entityType.BaseType != null || !ShouldFilterEntity<TEntity>(entityType)) return;
            var filterExpression = CreateFilterExpression<TEntity>();
            if (filterExpression == null) return;
            if (entityType.IsQueryType)
                modelBuilder.Query<TEntity>().HasQueryFilter(filterExpression);
            else
                modelBuilder.Entity<TEntity>().HasQueryFilter(filterExpression);
        }

        protected virtual bool ShouldFilterEntity<TEntity>(IMutableEntityType entityType) where TEntity : class
        {
            return typeof(IRemovable).IsAssignableFrom(typeof(TEntity));
        }

        protected Expression<Func<TEntity, bool>> CreateFilterExpression<TEntity>() where TEntity : class
        {
            Expression<Func<TEntity, bool>> expression = null;

            if (typeof(IRemovable).IsAssignableFrom(typeof(TEntity)))
            {
                Expression<Func<TEntity, bool>> removedFilter = e => !((IRemovable)e).IsRemoved;
                expression = expression == null ? removedFilter : CombineExpressions(expression, removedFilter);
            }

            return expression;
        }

        protected Expression<Func<T, bool>> CombineExpressions<T>(Expression<Func<T, bool>> expression1, Expression<Func<T, bool>> expression2)
        {
            return ExpressionCombiner.Combine(expression1, expression2);
        }
    }
}