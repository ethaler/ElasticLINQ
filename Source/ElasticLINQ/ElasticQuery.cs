﻿// Licensed under the Apache 2.0 License. See LICENSE.txt in the project root for more information.

using ElasticLinq.Request.Formatters;
using ElasticLinq.Request.Visitors;
using ElasticLinq.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace ElasticLinq
{
    /// <summary>
    /// ElasticSearch query object used to facilitate LINQ query syntax.
    /// </summary>
    /// <typeparam name="T">Element type being queried.</typeparam>
    public class ElasticQuery<T> : IElasticQuery<T>
    {
        private readonly ElasticQueryProvider provider;
        private readonly Expression expression;

        public ElasticQuery(ElasticQueryProvider provider)
        {
            Argument.EnsureNotNull("provider", provider);

            this.provider = provider;
            expression = Expression.Constant(this);
        }

        public ElasticQuery(ElasticQueryProvider provider, Expression expression)
        {
            Argument.EnsureNotNull("provider", provider);
            Argument.EnsureNotNull("expression", expression);

            if (!typeof(IQueryable<T>).IsAssignableFrom(expression.Type))
                throw new ArgumentOutOfRangeException("expression");

            this.provider = provider;
            this.expression = expression;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return ((IEnumerable<T>)provider.Execute(expression)).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)provider.Execute(expression)).GetEnumerator();
        }

        public Type ElementType
        {
            get { return typeof(T); }
        }

        public Expression Expression
        {
            get { return expression; }
        }

        public IQueryProvider Provider
        {
            get { return provider; }
        }

        /// <summary>
        /// Shows the query that would be issued to ElasticSearch
        /// </summary>
        public string ToElasticSearchQuery()
        {
            var request = ElasticQueryTranslator.Translate(provider.Mapping, provider.Prefix, Expression);
            var formatter = new PostBodyRequestFormatter(provider.Connection, provider.Mapping, request.SearchRequest);
            return formatter.Body;
        }
    }
}