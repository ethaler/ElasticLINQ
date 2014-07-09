using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ElasticLinq.Request.Criteria
{
    /// <summary>
    /// Criteria that specifies the value that a
    /// field must match in order to select a document. Supported wildcard's are ?*
    /// </summary>
    internal class WildcardCriteria : ICriteria
    {
        private readonly string search;
        private readonly string field;
        private readonly MemberInfo member;

        public WildcardCriteria(string field,MemberInfo member, string search)
        {
            //wildcard search only works with lowercase criteria
            this.field = field;
            this.member = member;
            this.search = search.ToLower();
        }

        public string Field
        {
            get { return field; }
        }

        public MemberInfo Member
        {
            get { return member; }
        }

        public string Search
        {
            get { return search; }
        }

        public string Name
        {
            get { return "wildcard"; }
        }

        public override string ToString()
        {
            return base.ToString() + Search;
        }
    }
}
