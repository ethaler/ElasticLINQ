﻿// Licensed under the Apache 2.0 License. See LICENSE.txt in the project root for more information.

using ElasticLinq.Mapping;
using ElasticLinq.Request.Criteria;
using ElasticLinq.Request.Expressions;
using ElasticLinq.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace ElasticLinq.Request.Visitors
{
    /// <summary>
    /// Expression visitor to translate predicate expressions to criteria expressions.
    /// Used by Where, Query, Single, First, Count etc.
    /// </summary>
    internal class CriteriaExpressionVisitor : ExpressionVisitor
    {
        protected readonly IElasticMapping Mapping;
        protected readonly string Prefix;
        private ElasticSearchRequest _searchRequest;

        protected CriteriaExpressionVisitor(IElasticMapping mapping, string prefix, ElasticSearchRequest searchRequest = null)
        {
            Mapping = new ElasticFieldsMappingWrapper(mapping);
            Prefix = prefix;
            _searchRequest = searchRequest ?? new ElasticSearchRequest();
        }

        public ElasticSearchRequest SearchRequest
        {
            get { return _searchRequest; }
        }

        protected override Expression VisitMethodCall(MethodCallExpression m)
        {
            if (m.Method.DeclaringType == typeof(Enumerable))
                return VisitEnumerableMethodCall(m);

            if (m.Method.DeclaringType == typeof(ElasticMethods))
                return VisitElasticMethodsMethodCall(m);

            switch (m.Method.Name)
            {
                case "Equals":
                    if (m.Arguments.Count == 1)
                        return VisitEquals(Visit(m.Object), Visit(m.Arguments[0]));
                    if (m.Arguments.Count == 2)
                        return VisitEquals(Visit(m.Arguments[0]), Visit(m.Arguments[1]));
                    break;

                case "Contains":
                    if (m.Method.DeclaringType == typeof(string))
                        return VisitStringContains(m);
                    if (TypeHelper.FindIEnumerable(m.Method.DeclaringType) != null)
                        return VisitEnumerableContainsMethodCall(m.Object, m.Arguments[0]);
                    break;
            }

            return base.VisitMethodCall(m);
        }

        internal Expression VisitStringContains(MethodCallExpression m)
        {
            var memberExp = m.Object as MemberExpression;
            if (memberExp == null)
                throw new NotSupportedException("Contains is only supported as Member access e.g. obj.prop.Contains(\"a\") ");

            var searchArg = m.Arguments.FirstOrDefault();
            var searchString = "*";
            if (searchArg != null)
            {
                var search = searchArg.ToString().TrimStart(new[] { '\"' }).TrimEnd(new[] { '\"' });

                if (!string.IsNullOrWhiteSpace(search))
                    searchString += search + "*";
            }

            var criteriaExpression = new CriteriaExpression(new WildcardCriteria(Mapping.GetFieldName(Prefix, memberExp), memberExp.Member, searchString));
            return criteriaExpression;
        }
        protected static ICriteria ApplyQueryMustContainCriteria(ICriteria currentRoot, ICriteria newCriteria)
        {
            return BoolQueryCriteria.CombineMustQueryCriteria(currentRoot, newCriteria);
        }

        protected static ICriteria ApplyQueryShouldContainCriteria(ICriteria currentRoot, ICriteria newCriteria)
        {
            return BoolQueryCriteria.CombineShouldQueryCriteria(currentRoot, newCriteria);
        }

        protected static ICriteria ApplyCriteria(ICriteria currentRoot, ICriteria newCriteria)
        {
            return currentRoot == null
                ? newCriteria
                : AndCriteria.Combine(currentRoot, newCriteria);
        }

        protected Expression VisitElasticMethodsMethodCall(MethodCallExpression m)
        {
            switch (m.Method.Name)
            {
                case "ContainsAny":
                    if (m.Arguments.Count == 2)
                        return VisitContains("ContainsAny", m.Arguments[0], m.Arguments[1], TermsExecutionMode.@bool);
                    break;

                case "ContainsAll":
                    if (m.Arguments.Count == 2)
                        return VisitContains("ContainsAll", m.Arguments[0], m.Arguments[1], TermsExecutionMode.and);
                    break;

                case "Regexp":
                    if (m.Arguments.Count == 2)
                        return VisitRegexp(m.Arguments[0], m.Arguments[1]);
                    break;

                case "Prefix":
                    if (m.Arguments.Count == 2)
                        return VisitPrefix(m.Arguments[0], m.Arguments[1]);
                    break;
            }

            throw new NotSupportedException(string.Format("The ElasticMethods method '{0}' is not supported", m.Method.Name));
        }

        protected Expression VisitEnumerableMethodCall(MethodCallExpression m)
        {
            switch (m.Method.Name)
            {
                case "Contains":
                    if (m.Arguments.Count == 2)
                        return VisitEnumerableContainsMethodCall(m.Arguments[0], m.Arguments[1]);
                    break;

                case "Any":
                    if (m.Arguments.Count == 1)
                        return CreateExists(new ConstantMemberPair(ConstantExpression.Constant(true), (MemberExpression)m.Arguments[0]), true);
                    if (m.Arguments.Count == 2)
                        return VisitContains("ContainsAny", m.Arguments[0], m.Arguments[1], TermsExecutionMode.@bool);
                    break;
            }

            throw new NotSupportedException(string.Format("The Enumerable method '{0}' is not supported", m.Method.Name));
        }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            switch (node.NodeType)
            {
                case ExpressionType.Convert:
                    return node.Operand;

                case ExpressionType.Not:
                    {
                        var subExpression = Visit(node.Operand) as CriteriaExpression;
                        if (subExpression != null)
                            return new CriteriaExpression(NotCriteria.Create(subExpression.Criteria));
                        break;
                    }
            }

            return base.VisitUnary(node);
        }

        protected override Expression VisitMember(MemberExpression m)
        {
            //why?
            //if (m.Member.DeclaringType == typeof(ElasticFields))
            //    return m;

            //switch (m.Expression.NodeType)
            //{
            //    case ExpressionType.Parameter:
            //        return m;

            //    case ExpressionType.MemberAccess:
            //        if (m.Member.Name == "HasValue" && m.Member.DeclaringType.IsGenericOf(typeof(Nullable<>)))
            //            return m;
            //        break;
            //}

            //throw new NotSupportedException(string.Format("The MemberInfo '{0}' is not supported", m.Member.Name));

            return m;
        }

        protected override Expression VisitBinary(BinaryExpression b)
        {
            switch (b.NodeType)
            {
                case ExpressionType.OrElse:
                    return VisitOrElse(b);

                case ExpressionType.AndAlso:
                    return VisitAndAlso(b);

                case ExpressionType.Equal:
                case ExpressionType.NotEqual:
                case ExpressionType.GreaterThan:
                case ExpressionType.GreaterThanOrEqual:
                case ExpressionType.LessThan:
                case ExpressionType.LessThanOrEqual:
                    return VisitComparisonBinary(b);

                default:
                    throw new NotSupportedException(string.Format("The binary expression '{0}' is not supported", b.NodeType));
            }
        }

        protected Expression BooleanMemberAccessBecomesEquals(Expression e)
        {
            if (e is CriteriaExpression)
                return e;

            e = Visit(e);

            var wasNegative = e.NodeType == ExpressionType.Not;

            if (e is UnaryExpression)
                e = Visit(((UnaryExpression)e).Operand);

            if (e is MemberExpression && e.Type == typeof(bool))
                return Visit(Expression.Equal(e, Expression.Constant(!wasNegative)));

            return e;
        }

        private Expression VisitPrefix(Expression left, Expression right)
        {
            var cm = ConstantMemberPair.Create(left, right);

            if (cm != null)
                return new CriteriaExpression(new PrefixCriteria(Mapping.GetFieldName(Prefix, cm.MemberExpression), cm.ConstantExpression.Value.ToString()));

            throw new NotSupportedException("Prefix must be between a Member and a Constant");
        }

        private Expression VisitRegexp(Expression left, Expression right)
        {
            var cm = ConstantMemberPair.Create(left, right);

            if (cm != null)
                return new CriteriaExpression(new RegexpCriteria(Mapping.GetFieldName(Prefix, cm.MemberExpression), cm.ConstantExpression.Value.ToString()));

            throw new NotSupportedException("Regexp must be between a Member and a Constant");
        }

        private Expression VisitEnumerableContainsMethodCall(Expression source, Expression match)
        {
            var matched = Visit(match);

            // Where(x => constantsList.Contains(x.Property))
            if (source is ConstantExpression && matched is MemberExpression)
            {
                var member = ((MemberExpression)matched).Member;
                var field = Mapping.GetFieldName(Prefix, member);
                var containsSource = ((IEnumerable)((ConstantExpression)source).Value);

                // If criteria contains a null create an Or criteria with Terms on one
                // side and Missing on the other.
                var values = containsSource.Cast<object>().Distinct().ToList();
                var nonNullValues = values.Where(v => v != null).ToList();

                ICriteria criteria = TermsCriteria.Build(field, member, nonNullValues);
                if (values.Count != nonNullValues.Count)
                    criteria = OrCriteria.Combine(criteria, new MissingCriteria(field));

                return new CriteriaExpression(criteria);
            }

            // Where(x => x.SomeList.Contains(constantValue))
            if (source is MemberExpression && matched is ConstantExpression)
            {
                var member = ((MemberExpression)source).Member;
                var field = Mapping.GetFieldName(Prefix, member);
                var value = ((ConstantExpression)matched).Value;
                return new CriteriaExpression(TermsCriteria.Build(field, member, value));
            }

            throw new NotSupportedException(string.Format("Unknown source '{0}' for Contains operation", source));
        }

        private Expression VisitAndAlso(BinaryExpression b)
        {
            var vLIsQueryCriteria = false;
            var vRIsQueryCriteria = false;

            var visitedL = Visit(b.Left);
            var visitedR = Visit(b.Right);

            var vLCritExpr = visitedL as CriteriaExpression;
            var vRCritExpr = visitedR as CriteriaExpression;

            if (vLCritExpr != null && vLCritExpr.Criteria is IsQueryCriteria)
                vLIsQueryCriteria = true;
            if (vRCritExpr != null && vRCritExpr.Criteria is IsQueryCriteria)
                vRIsQueryCriteria = true;

            ////Query Expression like wildcard are handled by the Query and therefore should not end in the filter part as the AndCriteria would
            if (vLIsQueryCriteria || vRIsQueryCriteria)
            {
                if(vLIsQueryCriteria)
                    SearchRequest.Query = ApplyQueryMustContainCriteria(SearchRequest.Query, vLCritExpr.Criteria);
                
                if(vRIsQueryCriteria)
                    SearchRequest.Query = ApplyQueryMustContainCriteria(SearchRequest.Query, vRCritExpr.Criteria);

                //return the side which isn't a query criteria
                if (!vLIsQueryCriteria)
                    return visitedL;
                if (!vRIsQueryCriteria)
                    return visitedR;
                //both are query criteria return A NOP 
                return new CriteriaExpression(new NOPCriteria(vLCritExpr.Criteria, vRCritExpr.Criteria));
            }

            //habdle if one of the parts is a NOP 
            if (vLCritExpr != null && vLCritExpr.Criteria is NOPCriteria)
                return vRCritExpr;
            if (vRCritExpr != null && vRCritExpr.Criteria is NOPCriteria)
                return vLCritExpr;

            return new CriteriaExpression(
                AndCriteria.Combine(AssertExpressionsOfType<CriteriaExpression>(visitedL, visitedR).Select(f => f.Criteria).ToArray()));
        }

        private Expression VisitOrElse(BinaryExpression b)
        {
            var vLIsQueryCriteria = false;
            var vRIsQueryCriteria = false;

            var visitedL = Visit(b.Left);
            var visitedR = Visit(b.Right);

            var vLCritExpr = visitedL as CriteriaExpression;
            var vRCritExpr = visitedR as CriteriaExpression;

            if (vLCritExpr != null && vLCritExpr.Criteria is IsQueryCriteria)
                vLIsQueryCriteria = true;
            if (vRCritExpr != null && vRCritExpr.Criteria is IsQueryCriteria)
                vRIsQueryCriteria = true;

            //Query Expression like wildcard are handled by the Query and therefore should not end in the filter part as the OrCriteria would
            if (vLIsQueryCriteria || vRIsQueryCriteria)
            {
                if (vLIsQueryCriteria)
                    SearchRequest.Query = ApplyQueryShouldContainCriteria(SearchRequest.Query, vLCritExpr.Criteria);

                if (vRIsQueryCriteria)
                    SearchRequest.Query = ApplyQueryShouldContainCriteria(SearchRequest.Query, vRCritExpr.Criteria);

                //return the side which isn't a query criteria
                if (!vLIsQueryCriteria)
                    return visitedL;
                if (!vRIsQueryCriteria)
                    return visitedR;
                //both are query criteria return A NOP 
                return new CriteriaExpression(new NOPCriteria(vLCritExpr.Criteria, vRCritExpr.Criteria));
            }

            //habdle if one of the parts is a NOP 
            if (vLCritExpr != null && vLCritExpr.Criteria is NOPCriteria)
                return vRCritExpr;
            if (vRCritExpr != null && vRCritExpr.Criteria is NOPCriteria)
                return vLCritExpr;

            return new CriteriaExpression(
                OrCriteria.Combine(AssertExpressionsOfType<CriteriaExpression>(visitedL, visitedR).Select(f => f.Criteria).ToArray()));
        }

        private IEnumerable<T> AssertExpressionsOfType<T>(params Expression[] expressions) where T : Expression
        {
            foreach (var expression in expressions.Select(BooleanMemberAccessBecomesEquals))
            {
                if ((expression as T) == null)
                    throw new NotSupportedException(string.Format("Unexpected binary expression '{0}'", expression));

                yield return (T)expression;
            }
        }

        private Expression VisitComparisonBinary(BinaryExpression b)
        {
            var left = Visit(b.Left);
            var right = Visit(b.Right);

            switch (b.NodeType)
            {
                case ExpressionType.Equal:
                    return VisitEquals(left, right);

                case ExpressionType.NotEqual:
                    return VisitNotEqual(left, right);

                case ExpressionType.GreaterThan:
                case ExpressionType.GreaterThanOrEqual:
                case ExpressionType.LessThan:
                case ExpressionType.LessThanOrEqual:
                    return VisitRange(b.NodeType, left, right);

                default:
                    throw new NotSupportedException(string.Format("The binary expression '{0}' is not supported", b.NodeType));
            }
        }

        private Expression VisitContains(string methodName, Expression left, Expression right, TermsExecutionMode executionMode)
        {
            var cm = ConstantMemberPair.Create(left, right);

            if (cm != null)
            {
                var values = ((IEnumerable)cm.ConstantExpression.Value).Cast<object>().ToArray();
                return new CriteriaExpression(TermsCriteria.Build(executionMode, Mapping.GetFieldName(Prefix, cm.MemberExpression), cm.MemberExpression.Member, values));
            }

            throw new NotSupportedException(methodName + " must be between a Member and a Constant");
        }

        private Expression CreateExists(ConstantMemberPair cm, bool positiveTest)
        {
            var fieldName = Mapping.GetFieldName(Prefix, UnwrapNullableMethodExpression(cm.MemberExpression));

            var value = cm.ConstantExpression.Value ?? false;

            if (value.Equals(positiveTest))
                return new CriteriaExpression(new ExistsCriteria(fieldName));

            if (value.Equals(!positiveTest))
                return new CriteriaExpression(new MissingCriteria(fieldName));

            throw new NotSupportedException("A null test Expression must have a member being compared to a bool or null");
        }

        private Expression VisitEquals(Expression left, Expression right)
        {
            var cm = ConstantMemberPair.Create(left, right);

            if (cm != null)
                return cm.IsNullTest
                    ? CreateExists(cm, true)
                    : new CriteriaExpression(new TermCriteria(Mapping.GetFieldName(Prefix, cm.MemberExpression), cm.MemberExpression.Member, cm.ConstantExpression.Value));

            throw new NotSupportedException("Equality must be between a Member and a Constant");
        }

        private static MemberInfo UnwrapNullableMethodExpression(MemberExpression m)
        {
            if (m.Expression is MemberExpression)
                return ((MemberExpression)(m.Expression)).Member;

            return m.Member;
        }

        private Expression VisitNotEqual(Expression left, Expression right)
        {
            var cm = ConstantMemberPair.Create(left, right);

            if (cm != null)
                return cm.IsNullTest
                    ? CreateExists(cm, false)
                    : new CriteriaExpression(NotCriteria.Create(new TermCriteria(Mapping.GetFieldName(Prefix, cm.MemberExpression), cm.MemberExpression.Member, cm.ConstantExpression.Value)));

            throw new NotSupportedException("A not-equal expression must consist of a constant and a member");
        }

        private Expression VisitRange(ExpressionType t, Expression left, Expression right)
        {
            var cm = ConstantMemberPair.Create(left, right);

            if (cm != null)
            {
                var field = Mapping.GetFieldName(Prefix, cm.MemberExpression);
                return new CriteriaExpression(new RangeCriteria(field, cm.MemberExpression.Member, ExpressionTypeToRangeType(t), cm.ConstantExpression.Value));
            }

            throw new NotSupportedException("A range must consist of a constant and a member");
        }

        private static RangeComparison ExpressionTypeToRangeType(ExpressionType expressionType)
        {
            switch (expressionType)
            {
                case ExpressionType.GreaterThan:
                    return RangeComparison.GreaterThan;
                case ExpressionType.GreaterThanOrEqual:
                    return RangeComparison.GreaterThanOrEqual;
                case ExpressionType.LessThan:
                    return RangeComparison.LessThan;
                case ExpressionType.LessThanOrEqual:
                    return RangeComparison.LessThanOrEqual;
            }

            throw new ArgumentOutOfRangeException("expressionType");
        }
    }
}