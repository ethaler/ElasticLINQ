using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ElasticLinq.Request.Criteria
{
    /// <summary>
    /// Criteria that specifies the value that a
    /// field must match in order to select a document. Supported wildcard's are ?*
    /// </summary>
    internal class WildcardCriteria : SingleFieldCriteria
    {
        private readonly string search;

        public WildcardCriteria(string field, string search)
            : base(field)
        {
            //wildcard search only works with lowercase criteria
            this.search = search.ToLower();
        }

        public string Search
        {
            get { return search; }
        }

        public override string Name
        {
            get { return "wildcard"; }
        }

        public override string ToString()
        {
            return base.ToString() + "\"" + Search + "\"";
        }
    }
}
