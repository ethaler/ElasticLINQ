using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ElasticLinq.Request.Criteria
{
    internal class BoolQueryCriteria : ICriteria, IsQueryCriteria
    {
        private readonly List<ICriteria> mustCriteria;
        private readonly List<ICriteria> mustNotCriteria;
        private readonly List<ICriteria> shouldCriteria;

        public BoolQueryCriteria(IEnumerable<ICriteria> mustCriteria, IEnumerable<ICriteria> mustNotCriteria = null, IEnumerable<ICriteria> shouldCriteria = null)
        {
            this.mustCriteria = mustCriteria != null ? new List<ICriteria>(mustCriteria) : new List<ICriteria>();
            this.mustNotCriteria = mustNotCriteria != null ? new List<ICriteria>(mustNotCriteria) : new List<ICriteria>();
            this.shouldCriteria = shouldCriteria != null ? new List<ICriteria>(shouldCriteria) : new List<ICriteria>();
        }

        public static ICriteria CombineMustQueryCriteria(ICriteria rootCriteria, ICriteria newCriteria)
        {
            if (rootCriteria == null)
                return new BoolQueryCriteria(Enumerable.Repeat(newCriteria, 1));
            else
            {
                var rootBool = rootCriteria as BoolQueryCriteria;
                if (rootBool != null)
                {

                    var must = new List<ICriteria>(rootBool.MustCriteria);
                    must.Add(newCriteria);
                    return new BoolQueryCriteria(must, rootBool.mustNotCriteria, rootBool.ShouldCriteria);
                }
                else
                {
                    var must = new List<ICriteria>();
                    must.Add(rootCriteria);
                    must.Add(newCriteria);
                    return new BoolQueryCriteria(must);
                }
            }
        }

        public static ICriteria CombineShouldQueryCriteria(ICriteria rootCriteria, ICriteria newCriteria)
        {
            if (rootCriteria == null)
                return new BoolQueryCriteria(null,null,Enumerable.Repeat(newCriteria, 1));
            else
            {
                var rootBool = rootCriteria as BoolQueryCriteria;
                if (rootBool != null)
                {

                    var should = new List<ICriteria>(rootBool.ShouldCriteria);
                    should.Add(newCriteria);
                    return new BoolQueryCriteria(rootBool.MustCriteria, rootBool.mustNotCriteria, should);
                }
                else
                {
                    var should = new List<ICriteria>();
                    should.Add(rootCriteria);
                    should.Add(newCriteria);
                    return new BoolQueryCriteria(null,null,should);
                }
            }
        }

        public string Name
        {
            get { return "bool"; }
        }

        public IReadOnlyList<ICriteria> MustCriteria
        {
            get { return mustCriteria.AsReadOnly(); }
        }

        public IReadOnlyList<ICriteria> MustNotCriteria
        {
            get { return mustNotCriteria.AsReadOnly(); }
        }

        public IReadOnlyList<ICriteria> ShouldCriteria
        {
            get { return shouldCriteria.AsReadOnly(); }
        }
    }
}
