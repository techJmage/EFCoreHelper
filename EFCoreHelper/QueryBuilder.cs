using LinqKit;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace EFCoreHelper;
public record struct QuerySettings(int Skip, int? Take, bool Forkable);
public enum ComposeOption { AND, OR }

public static class QueryBuilder
{
    #region Compile
    public static Func<DbContext, IEnumerable<DM>> Build<DM>(this IQueryable<DM> dm, int skip = 0, int? limit = null, IReadOnlyList<Expression<Func<DM, bool>>>? predicates = null, IReadOnlyList<(Expression<Func<DM, dynamic>> Expression, bool IsAscending)>? sortExpressions = null)
    where DM : class
    {
        Expression<Func<DbContext, IEnumerable<DM>>> expr = (DbContext db) => dm.Compose(skip, limit, false, predicates, sortExpressions).AsEnumerable();
        return EF.CompileQuery(expr);
    }

    public static Func<DbContext, IEnumerable<DM>> Build<DM>(this DbSet<DM> dm, int skip = 0, int? limit = null, params Expression<Func<DM, bool>>[] predicates)
        where DM : class
        => dm.AsQueryable().Build(skip, limit, predicates);

    public static Func<DbContext, IAsyncEnumerable<DM>> BuildAsync<DM>(this IQueryable<DM> dq, int skip = 0, int? limit = null, IReadOnlyList<Expression<Func<DM, bool>>>? predicates = null, IReadOnlyList<(Expression<Func<DM, dynamic>> Expression, bool IsAscending)>? sortExpressions = null)
        where DM : class
    {
        Expression<Func<DbContext, IQueryable<DM>>> expr = (DbContext db) => dq.Compose(skip, limit, false, predicates, sortExpressions);
        return EF.CompileAsyncQuery(expr);
    }

    public static Func<DbContext, IAsyncEnumerable<DM>> BuildAsync<DM>(this DbSet<DM> dm, int skip = 0, int? limit = null, params Expression<Func<DM, bool>>[] predicates) where DM : class
        => dm.AsQueryable().BuildAsync(skip, limit, predicates);

    #endregion
    #region Compose
    public static IQueryable<DM> Compose<DM>(this IQueryable<DM> q,
        QuerySettings qs = new(), IReadOnlyList<Expression<Func<DM, bool>>>? predicates = null, IReadOnlyList<(Expression<Func<DM, dynamic>> Expression, bool IsAscending)>? sortExpressions = null)
    where DM : class
    {
        if (predicates?.Count > 0)
            q = q.BuildPredicate(predicates);
        if (sortExpressions?.Count > 0)
            q = q.BuildOrderedQuery(sortExpressions);
        return qs.ApplyTo(q);
    }
    public static IQueryable<DM> Compose<DM>(this IQueryable<DM> q,
       QuerySettings qs = new(), IReadOnlyList<(Expression<Func<DM, bool>> expr, ComposeOption opt)>? predicates = null, IReadOnlyList<(Expression<Func<DM, dynamic>> Expression, bool IsAscending)>? sortExpressions = null)
       where DM : class
    {
        if (predicates?.Count > 0)
            q = q.BuildPredicate(predicates);
        if (sortExpressions?.Count > 0)
            q = q.BuildOrderedQuery(sortExpressions);
        return qs.ApplyTo(q);
    }
    public static IQueryable<DM> Compose<DM>(this IQueryable<DM> q, int skip = 0, int? limit = null, bool forkable = false, Expression<Func<DM, bool>>? predicates = null)
        where DM : class => q.Compose(new QuerySettings(skip, limit, forkable), predicates == null ? null : [predicates]);
    public static IQueryable<DM> Compose<DM>(this DbSet<DM> q, int skip = 0, int? limit = null, bool forkable = false, Expression<Func<DM, bool>>? predicates = null)
        where DM : class => q.Compose(new QuerySettings(skip, limit, forkable), predicates == null ? null : [predicates]);

    public static IQueryable<DM> Compose<DM>(this IQueryable<DM> q, int skip = 0, int? limit = null, bool forkable = false, IReadOnlyList<Expression<Func<DM, bool>>>? predicates = null, IReadOnlyList<(Expression<Func<DM, dynamic>> Expression, bool IsAscending)>? sortExpressions = null)
        where DM : class => q.Compose(new QuerySettings(skip, limit, forkable), predicates, sortExpressions);

    public static IQueryable<DM> Compose<DM>(this DbSet<DM> dm, int skip = 0, int? limit = null, bool forkable = false, IReadOnlyList<Expression<Func<DM, bool>>>? predicates = null, IReadOnlyList<(Expression<Func<DM, dynamic>> Expression, bool IsAscending)>? sortExpressions = null) where DM : class
        => dm.AsQueryable().Compose(skip, limit, forkable, predicates, sortExpressions);

    #endregion
    private static IQueryable<DM> ApplyTo<DM>(this QuerySettings qs, IQueryable<DM> q) where DM : class
    {
        if (qs.Skip > 0)
            q = q.Skip(qs.Skip);
        if (qs.Take.HasValue)
            q = q.Take(qs.Take.Value);
        q = q.AsNoTracking();
        return qs.Forkable ? q.AsSplitQuery() : q;
    }
    private static IQueryable<DM> BuildPredicate<DM>(this IQueryable<DM> q, IReadOnlyList<(Expression<Func<DM, bool>> expr, ComposeOption opt)> predicates) where DM : class
    {
        var p = PredicateBuilder.New<DM>();
        foreach ((var expr, var opt) in predicates)
            p = opt == ComposeOption.AND ? p.And(expr) : p.Or(expr);
        return q.Where(p);
    }
    private static IQueryable<DM> BuildPredicate<DM>(this IQueryable<DM> q, IReadOnlyList<Expression<Func<DM, bool>>> predicates) where DM : class
    {
        var p = PredicateBuilder.New<DM>();
        for (int i = 0; i < predicates.Count; i++)
            p = p.And(predicates[i]);
        return q.Where(p);
    }

    private static IQueryable<DM> BuildOrderedQuery<DM>(this IQueryable<DM> q, IReadOnlyList<(Expression<Func<DM, dynamic>> Expression, bool IsAscending)> sortExpressions)
    {
        var firstItem = true;
        foreach (var expr in sortExpressions)
        {
            if (firstItem)
                q = expr.IsAscending ? q.OrderBy(expr.Expression) : q.OrderByDescending(expr.Expression);
            else
            {
                var orderedQ = (IOrderedQueryable<DM>)q;
                q = expr.IsAscending ? orderedQ.ThenBy(expr.Expression) : orderedQ.ThenByDescending(expr.Expression);
                q = orderedQ;
            }
            firstItem = false;
        }
        return q;
    }
}
