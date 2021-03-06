﻿// Licensed under the Apache 2.0 License. See LICENSE.txt in the project root for more information.

using ElasticLinq.Mapping;
using ElasticLinq.Request.Criteria;
using ElasticLinq.Request.Expressions;
using ElasticLinq.Request.Facets;
using ElasticLinq.Response.Materializers;
using ElasticLinq.Response.Model;
using ElasticLinq.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace ElasticLinq.Request.Visitors
{
    /// <summary>
    /// Expression visitor to translate a LINQ query into a <see cref="ElasticTranslateResult"/>
    /// that captures remote and local semantics.
    /// </summary>
    internal class ElasticQueryTranslator : CriteriaExpressionVisitor
    {

        private Type sourceType;
        private Type finalItemType;
        private Func<Hit, Object> itemProjector;
        private IElasticMaterializer materializer;

        private ElasticQueryTranslator(IElasticMapping mapping, string prefix)
            : base(mapping, prefix, new ElasticSearchRequest())
        {
        }

        internal static ElasticTranslateResult Translate(IElasticMapping mapping, string prefix, Expression e)
        {
            return new ElasticQueryTranslator(mapping, prefix).Translate(e);
        }

        private ElasticTranslateResult Translate(Expression e)
        {
            var evaluated = PartialEvaluator.Evaluate(e);
            //visit before Facet and  revisit later where necessary
            evaluated = Visit(evaluated);
            var aggregated = FacetExpressionVisitor.Rebind(Mapping, Prefix, evaluated);

            if (aggregated.Collected.Count > 0)
                CompleteFacetTranslation(aggregated);
            else
                CompleteHitTranslation(evaluated);

            return new ElasticTranslateResult(SearchRequest, materializer);
        }

        private void CompleteHitTranslation(Expression evaluated)
        {
            Visit(evaluated);
            SearchRequest.DocumentType = Mapping.GetDocumentType(sourceType);

            if (SearchRequest.Filter == null && SearchRequest.Query == null)
                SearchRequest.Filter = Mapping.GetTypeExistsCriteria(sourceType);

            if (materializer == null)
                materializer = new ElasticManyHitsMaterializer(itemProjector ?? DefaultItemProjector, finalItemType ?? sourceType);
        }

        private void CompleteFacetTranslation(RebindCollectionResult<IFacet> aggregated)
        {
            Visit(aggregated.Expression);
            SearchRequest.DocumentType = Mapping.GetDocumentType(sourceType);

            SearchRequest.Facets = aggregated.Collected.ToList();
            SearchRequest.SearchType = "count"; // We only want facets, not hits
            materializer = new ManyFacetsElasticMaterializer(r => aggregated.Projection.Compile().DynamicInvoke(r), aggregated.Projection.ReturnType);
        }

        protected override Expression VisitMethodCall(MethodCallExpression m)
        {
            if (m.Method.DeclaringType == typeof(string) &&
                string.Equals(m.Method.Name, "Contains", StringComparison.InvariantCultureIgnoreCase))
                return VisitStringContains(m);
            if (m.Method.DeclaringType == typeof(Queryable))
                return VisitQueryableMethodCall(m);

            if (m.Method.DeclaringType == typeof(ElasticQueryExtensions))
                return VisitElasticQueryExtensionsMethodCall(m);

            if (m.Method.DeclaringType == typeof(ElasticMethods))
                return VisitElasticMethodsMethodCall(m);

            if (m.Method.Name == "Create")
                return m;

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
            //SearchRequest.Query = ApplyQueryContainsCriteria(SearchRequest.Query, criteriaExpression.Criteria);
            return criteriaExpression;
        }

        internal Expression VisitElasticQueryExtensionsMethodCall(MethodCallExpression m)
        {
            switch (m.Method.Name)
            {
                case "Query":
                    if (m.Arguments.Count == 2)
                        return VisitQuery(m.Arguments[0], m.Arguments[1]);
                    break;

                case "QueryString":
                    if (m.Arguments.Count == 2)
                        return VisitQueryString(m.Arguments[0], m.Arguments[1]);
                    break;

                case "OrderByScore":
                case "OrderByScoreDescending":
                case "ThenByScore":
                case "ThenByScoreDescending":
                    if (m.Arguments.Count == 1)
                        return VisitOrderByScore(m.Arguments[0], !m.Method.Name.EndsWith("Descending"));
                    break;
            }

            throw new NotSupportedException(string.Format("The ElasticQuery method '{0}' is not supported", m.Method.Name));
        }

        private Expression VisitQueryString(Expression source, Expression queryExpression)
        {
            var constantQueryExpression = (ConstantExpression)queryExpression;
            var criteriaExpression = new CriteriaExpression(new QueryStringCriteria(constantQueryExpression.Value.ToString()));
            SearchRequest.Query = ApplyCriteria(SearchRequest.Query, criteriaExpression.Criteria);

            return Visit(source);
        }

        internal Expression VisitQueryableMethodCall(MethodCallExpression m)
        {
            switch (m.Method.Name)
            {
                case "GroupBy":
                    if (m.Arguments.Count == 2)
                        return Visit(m.Arguments[0]); // Already handled by aggregator
                    break;

                case "Select":
                    if (m.Arguments.Count == 2)
                        return VisitSelect(m.Arguments[0], m.Arguments[1]);
                    break;

                case "First":
                case "FirstOrDefault":
                case "Single":
                case "SingleOrDefault":
                    if (m.Arguments.Count == 1)
                        return VisitFirstOrSingle(m.Arguments[0], null, m.Method.Name);
                    if (m.Arguments.Count == 2)
                        return VisitFirstOrSingle(m.Arguments[0], m.Arguments[1], m.Method.Name);
                    break;

                case "Where":
                    if (m.Arguments.Count == 2)
                        return VisitWhere(m.Arguments[0], m.Arguments[1]);
                    break;
                case "Skip":
                    if (m.Arguments.Count == 2)
                        return VisitSkip(m.Arguments[0], m.Arguments[1]);
                    break;
                case "Take":
                    if (m.Arguments.Count == 2)
                        return VisitTake(m.Arguments[0], m.Arguments[1]);
                    break;
                case "OrderBy":
                case "OrderByDescending":
                    if (m.Arguments.Count == 2)
                        return VisitOrderBy(m.Arguments[0], m.Arguments[1], m.Method.Name == "OrderBy");
                    break;
                case "ThenBy":
                case "ThenByDescending":
                    if (m.Arguments.Count == 2)
                        return VisitOrderBy(m.Arguments[0], m.Arguments[1], m.Method.Name == "ThenBy");
                    break;

                case "Count":
                    return VisitCount(m.Arguments[0]);

                case "OfType":
                case "Cast":
                    return m.Arguments[0];

            }

            throw new NotSupportedException(string.Format("The Queryable method '{0}' is not supported", m.Method.Name));
        }

        private Expression VisitCount(Expression expression)
        {
            materializer = new CountElasticMaterializer();
            return Visit(expression);
        }

        private Expression VisitFirstOrSingle(Expression source, Expression predicate, string methodName)
        {
            var single = methodName.StartsWith("Single");
            var orDefault = methodName.EndsWith("OrDefault");

            SearchRequest.Size = single ? 2 : 1;
            finalItemType = source.Type;
            materializer = new OneHitElasticMaterializer(itemProjector ?? DefaultItemProjector, finalItemType, single, orDefault);

            return predicate != null
                ? VisitWhere(source, predicate)
                : Visit(source);
        }

        protected override Expression VisitConstant(ConstantExpression c)
        {
            if (c.Value is IQueryable)
                sourceType = ((IQueryable)c.Value).ElementType;

            return c;
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

        private Expression VisitQuery(Expression source, Expression predicate)
        {
            var lambda = predicate.GetLambda();
            var body = BooleanMemberAccessBecomesEquals(lambda.Body);

            var criteriaExpression = body as CriteriaExpression;
            if (criteriaExpression == null)
                throw new NotSupportedException(string.Format("Unknown Query predicate '{0}'", body));

            SearchRequest.Query = ApplyCriteria(SearchRequest.Query, criteriaExpression.Criteria);

            return Visit(source);
        }

        private Expression VisitWhere(Expression source, Expression predicate)
        {
            var lambda = predicate.GetLambda();

            var body = BooleanMemberAccessBecomesEquals(lambda.Body);

            var criteriaExpression = body as CriteriaExpression;
            if (criteriaExpression == null)
                throw new NotSupportedException(String.Format("Unknown Where predicate '{0}'", body));

            if (!(criteriaExpression.Criteria is NOPCriteria))
            {
                if (criteriaExpression.Criteria is IsQueryCriteria)
                    SearchRequest.Query = ApplyCriteria(SearchRequest.Query, criteriaExpression.Criteria);
                else
                    SearchRequest.Filter = ApplyCriteria(SearchRequest.Filter, criteriaExpression.Criteria);
            }

            return Visit(source);
        }

        private Expression VisitOrderBy(Expression source, Expression orderByExpression, bool ascending)
        {
            var lambda = orderByExpression.GetLambda();
            var final = Visit(lambda.Body) as MemberExpression;
            if (final != null)
            {
                var fieldName = Prefix;
                if (final.Member.DeclaringType == typeof(ElasticFields) && final.Member.Name == "Score")
                {
                    fieldName = Mapping.GetFieldName(Prefix, final.Member);

                }
                else
                {
                    LinkedList<Expression> ll = new LinkedList<Expression>();
                    MemberExpression root = final;
                    ll.AddFirst(final);

                    while (root != null && root.Expression is MemberExpression)
                    {
                        root = root.Expression as MemberExpression;
                        ll.AddFirst(root);
                    }
                    if (root != null && root.Expression.NodeType == ExpressionType.Parameter)
                    {

                        while (root != null)
                        {
                            fieldName = Mapping.GetFieldName(fieldName, root.Member);
                            ll.RemoveFirst();
                            if (ll.Count > 0)
                                root = ll.First.Value as MemberExpression;
                            else
                                root = null;

                        }
                    }
                }

                var ignoreUnmapped = final.Type.IsNullable(); // Consider a config switch?
                SearchRequest.SortOptions.Insert(0, new SortOption(fieldName, ascending, ignoreUnmapped));
            }

            return Visit(source);
        }

        private Expression VisitOrderByScore(Expression source, bool ascending)
        {
            SearchRequest.SortOptions.Insert(0, new SortOption("_score", ascending));
            return Visit(source);
        }

        private Expression VisitSelect(Expression source, Expression selectExpression)
        {
            var lambda = selectExpression.GetLambda();
            var selectBody = lambda.Body;

            if (selectBody is MemberExpression)
                RebindPropertiesAndElasticFields(selectBody);

            if (selectBody is NewExpression)
                RebindSelectBody(selectBody, ((NewExpression)selectBody).Arguments, lambda.Parameters);

            if (selectBody is MethodCallExpression)
                RebindSelectBody(selectBody, ((MethodCallExpression)selectBody).Arguments, lambda.Parameters);

            if (selectBody is MemberInitExpression)
                RebindPropertiesAndElasticFields(selectBody);

            finalItemType = selectBody.Type;

            return Visit(source);
        }

        private void RebindSelectBody(Expression selectExpression, IEnumerable<Expression> arguments, IEnumerable<ParameterExpression> parameters)
        {
            var entityParameter = arguments.SingleOrDefault(parameters.Contains) as ParameterExpression;
            if (entityParameter == null)
                RebindPropertiesAndElasticFields(selectExpression);
            else
                RebindElasticFieldsAndChainProjector(selectExpression, entityParameter);
        }

        /// <summary>
        /// We are using the whole entity in a new select projection. Re-bind any ElasticField references to JObject
        /// and ensure the entity parameter is a freshly materialized entity object from our default materializer.
        /// </summary>
        /// <param name="selectExpression">Select expression to re-bind.</param>
        /// <param name="entityParameter">Parameter that references the whole entity.</param>
        private void RebindElasticFieldsAndChainProjector(Expression selectExpression, ParameterExpression entityParameter)
        {
            var projection = ElasticFieldsExpressionVisitor.Rebind(Prefix, Mapping, selectExpression);
            var compiled = Expression.Lambda(projection.Item1, entityParameter, projection.Item2).Compile();
            itemProjector = h => compiled.DynamicInvoke(DefaultItemProjector(h), h);
        }

        /// <summary>
        /// We are using just some properties of the entity. Rewrite the properties as JObject field lookups and
        /// record all the field names used to ensure we only select those.
        /// </summary>
        /// <param name="selectExpression">Select expression to re-bind.</param>
        private void RebindPropertiesAndElasticFields(Expression selectExpression)
        {
            var projection = MemberProjectionExpressionVisitor.Rebind(Prefix, Mapping, selectExpression);
            var compiled = Expression.Lambda(projection.Expression, projection.Parameter).Compile();
            itemProjector = h => compiled.DynamicInvoke(h);
            SearchRequest.Fields.AddRange(projection.Collected);
        }

        private Expression VisitSkip(Expression source, Expression skipExpression)
        {
            var skipConstant = Visit(skipExpression) as ConstantExpression;
            if (skipConstant != null)
                SearchRequest.From = (int)skipConstant.Value;
            return Visit(source);
        }

        private Expression VisitTake(Expression source, Expression takeExpression)
        {
            var takeConstant = Visit(takeExpression) as ConstantExpression;
            if (takeConstant != null)
            {
                var takeValue = (int)takeConstant.Value;

                if (SearchRequest.Size.HasValue)
                    SearchRequest.Size = Math.Min(SearchRequest.Size.GetValueOrDefault(), takeValue);
                else
                    SearchRequest.Size = takeValue;
            }
            return Visit(source);
        }

        private Func<Hit, Object> DefaultItemProjector
        {
            get
            {
                return hit => hit._source
                                .SelectToken(Mapping.GetDocumentMappingPrefix(sourceType) ?? "")
                                .ToObject(sourceType);
            }
        }
    }
}
