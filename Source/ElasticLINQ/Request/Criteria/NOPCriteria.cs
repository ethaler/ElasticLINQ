using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ElasticLinq.Request.Criteria
{
    /// <summary>
    /// Criteria that represents a NO OPERATION criteria, substituted criteria can be accessed by Criteria property 
    /// </summary>
    internal class NOPCriteria : CompoundCriteria
    {
        public NOPCriteria(params ICriteria[] criteria)
            : base(criteria)
        {
            
        }

        public override string Name
        {
            get { return "NOP"; }
        }

    }
}
