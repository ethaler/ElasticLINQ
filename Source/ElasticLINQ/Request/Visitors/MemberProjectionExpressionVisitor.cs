// Licensed under the Apache 2.0 License. See LICENSE.txt in the project root for more information.

using ElasticLinq.Mapping;
using ElasticLinq.Response.Model;
using ElasticLinq.Utility;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace ElasticLinq.Request.Visitors
{
    /// <summary>
    /// Rebinds select projection entity member accesses to JObject fields
    /// recording the specific field names required for selection.
    /// </summary>
    internal class MemberProjectionExpressionVisitor : ElasticFieldsExpressionVisitor
    {
        protected static readonly MethodInfo GetDictionaryValueMethod = typeof(MemberProjectionExpressionVisitor)
            .GetMethod("GetDictionaryValueOrDefault", BindingFlags.Static | BindingFlags.NonPublic);

        private readonly HashSet<string> fieldNames = new HashSet<string>();

        private MemberProjectionExpressionVisitor(string prefix, ParameterExpression bindingParameter, IElasticMapping mapping)
            : base(prefix, bindingParameter, mapping)
        {
        }

        internal static new RebindCollectionResult<string> Rebind(string prefix, IElasticMapping mapping, Expression selector)
        {
            var parameter = Expression.Parameter(typeof(Hit), "h");
            var visitor = new MemberProjectionExpressionVisitor(prefix, parameter, mapping);
            Argument.EnsureNotNull("selector", selector);
            var materializer = visitor.Visit(selector);
            return new RebindCollectionResult<string>(materializer, visitor.fieldNames, parameter, null);
        }

        protected override Expression VisitMember(MemberExpression m)
        {
            if (m.Expression != null && m.Expression.NodeType == ExpressionType.Parameter)
                return VisitFieldSelection(m);

            if (m.Expression != null && m.Expression.NodeType == ExpressionType.MemberAccess)
                return VisitReducingParameterMemberAccessFieldSelection(m);
            
            return base.VisitMember(m);
        }

        protected internal Expression VisitReducingParameterMemberAccessFieldSelection(MemberExpression m)
        {
            LinkedList<Expression> ll = new LinkedList<Expression>();
            MemberExpression root = m;
            ll.AddFirst(m);
            while (root != null && root.Expression is MemberExpression)
            {
                root = root.Expression as MemberExpression;
                ll.AddFirst(root);
            }
            if (root != null && root.Expression.NodeType == ExpressionType.Parameter)
            {
                var fieldName = Prefix;
                while (root != null)
                {
                    fieldName = Mapping.GetFieldName(fieldName, root.Member);
                    ll.RemoveFirst();
                    if (ll.Count > 0)
                        root = ll.First.Value as MemberExpression;
                    else
                        root = null;
                    
                }
                fieldNames.Add(fieldName);
                var getFieldExpression = Expression.Call(null, GetDictionaryValueMethod,
                    Expression.PropertyOrField(BindingParameter, "fields"), Expression.Constant(fieldName),
                    Expression.Constant(m.Type));
                return Expression.Convert(getFieldExpression, m.Type);
            }
            //couldn't do anything
            return m;
        }

        protected override Expression VisitElasticField(MemberExpression m)
        {
            var member = base.VisitElasticField(m);
            fieldNames.Add("_" + m.Member.Name.ToLowerInvariant());
            return member;
        }

        private Expression VisitFieldSelection(MemberExpression m)
        {
            var fieldName = Mapping.GetFieldName(Prefix, m.Member);
            fieldNames.Add(fieldName);
            var getFieldExpression = Expression.Call(null, GetDictionaryValueMethod, Expression.PropertyOrField(BindingParameter, "fields"), Expression.Constant(fieldName), Expression.Constant(m.Type));
            return Expression.Convert(getFieldExpression, m.Type);
        }

        internal static object GetDictionaryValueOrDefault(IDictionary<string, JToken> dictionary, string key, Type expectedType)
        {
            JToken token;
            if (dictionary.TryGetValue(key, out token))
            {
                //workaround elastic delivers arrays as results in certain queries ... not 100% sure if it's right or wrong
                if (token.Type == JTokenType.Array && (expectedType.IsValueType || expectedType == typeof(string) ))
                    return ((JArray) token)[0].ToObject(expectedType);
                return token.ToObject(expectedType);
            }

            return expectedType.IsValueType
                ? Activator.CreateInstance(expectedType)
                : null;
        }
    }
}