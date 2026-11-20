using System.Linq.Expressions;

namespace Pancras.Application.Common;

/// <summary>Ghép hai điều kiện lọc thành một (cả hai cùng đúng), để database dịch được thành một mệnh đề WHERE.</summary>
public static class Predicates
{
    public static Expression<Func<T, bool>> And<T>(Expression<Func<T, bool>> left, Expression<Func<T, bool>> right)
    {
        var parameter = left.Parameters[0];
        var body = new Rebind(right.Parameters[0], parameter).Visit(right.Body);
        return Expression.Lambda<Func<T, bool>>(Expression.AndAlso(left.Body, body), parameter);
    }

    private sealed class Rebind(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }
}
