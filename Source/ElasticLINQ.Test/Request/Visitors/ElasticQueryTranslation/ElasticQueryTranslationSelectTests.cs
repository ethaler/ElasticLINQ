// Licensed under the Apache 2.0 License. See LICENSE.txt in the project root for more information.
using ElasticLinq.Request.Visitors;
using System;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using Xunit;

namespace ElasticLinq.Test.Request.Visitors.ElasticQueryTranslation
{
    public class ElasticQueryTranslationSelectTests : ElasticQueryTranslationTestsBase
    {
        [Fact]
        public void SelectAnonymousProjectionTranslatesToFields()
        {
            var selected = Robots.Select(r => new { r.Id, r.Cost });
            var fields = ElasticQueryTranslator.Translate(Mapping, "prefix", selected.Expression).SearchRequest.Fields;

            Assert.NotNull(fields);
            Assert.Contains("prefix.id", fields);
            Assert.Contains("prefix.cost", fields);
            Assert.Equal(2, fields.Count);
        }

        [Fact]
        public void SelectAnonymousProjectionWithSomeIdentifiersTranslatesToFields()
        {
            var selected = Robots.Select(r => new { First = r.Id, Second = r.Started, r.Cost });
            var fields = ElasticQueryTranslator.Translate(Mapping, "prefix", selected.Expression).SearchRequest.Fields;

            Assert.NotNull(fields);
            Assert.Contains("prefix.id", fields);
            Assert.Contains("prefix.started", fields);
            Assert.Contains("prefix.cost", fields);
            Assert.Equal(3, fields.Count);
        }

        [Fact]
        public void SelectTupleProjectionWithIdentifiersTranslatesToFields()
        {
            var selected = Robots.Select(r => Tuple.Create(r.Id, r.Name));
            var fields = ElasticQueryTranslator.Translate(Mapping, "prefix", selected.Expression).SearchRequest.Fields;

            Assert.NotNull(fields);
            Assert.Contains("prefix.id", fields);
            Assert.Contains("prefix.name", fields);
            Assert.Equal(2, fields.Count);
        }

        [Fact]
        public void SelectSingleFieldTranslatesToField()
        {
            var selected = Robots.Select(r => r.Id);
            var fields = ElasticQueryTranslator.Translate(Mapping, "prefix", selected.Expression).SearchRequest.Fields;

            Assert.NotNull(fields);
            Assert.Contains("prefix.id", fields);
            Assert.Equal(1, fields.Count);
        }

        [Fact]
        public void SelectJustScoreTranslatesToField()
        {
            var selected = Robots.Select(r => ElasticFields.Score);
            var fields = ElasticQueryTranslator.Translate(Mapping, "prefix", selected.Expression).SearchRequest.Fields;

            Assert.NotNull(fields);
            Assert.Contains("_score", fields);
            Assert.Equal(1, fields.Count);
        }

        [Fact]
        public void SelectAnonymousScoreAndIdTranslatesToFields()
        {
            var selected = Robots.Select(r => new { ElasticFields.Id, ElasticFields.Score });
            var fields = ElasticQueryTranslator.Translate(Mapping, "prefix", selected.Expression).SearchRequest.Fields;

            Assert.NotNull(fields);
            Assert.Contains("_score", fields);
            Assert.Contains("_id", fields);
            Assert.Equal(2, fields.Count);
        }

        [Fact]
        public void SelectTupleScoreAndIdTranslatesToFields()
        {
            var selected = Robots.Select(r => Tuple.Create(ElasticFields.Id, ElasticFields.Score));
            var fields = ElasticQueryTranslator.Translate(Mapping, "prefix", selected.Expression).SearchRequest.Fields;

            Assert.NotNull(fields);
            Assert.Contains("_score", fields);
            Assert.Contains("_id", fields);
            Assert.Equal(2, fields.Count);
        }

        [Fact]
        public void SelectAnonymousEntityDoesNotTranslateToFields()
        {
            var selected = Robots.Select(r => new { Robot = r, ElasticFields.Score });
            var translation = ElasticQueryTranslator.Translate(Mapping, "prefix", selected.Expression);

            Assert.Empty(translation.SearchRequest.Fields);
        }

        [Fact]
        public void SelectTupleEntityDoesNotTranslateToFields()
        {
            var selected = Robots.Select(r => Tuple.Create(r, ElasticFields.Score));
            var translation = ElasticQueryTranslator.Translate(Mapping, "prefix", selected.Expression);

            Assert.Empty(translation.SearchRequest.Fields);
        }

        private class SubSubdata
        {
            public int Id { get; set; }
            public string Name { get; set; }

            public Subdata Parent { get; set; }
        }
        private class Subdata
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public DateTime DateTime { get; set; }

            public SubSubdata Child { get; set; }
        }
        private class Data
        {
            public int Id { get; set; }
            public string Name { get; set; }

            public Subdata Subdata { get; set; }
        }

        private class DataProjection
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public DateTime Y { get; set; }

            public string C1 { get; set; }

            public string P1 { get; set; }
        }

        [Fact]
        public void SelectAnonymousEntityWithObjectPropertyAccessTranslatesToFields()
        {
            var queryable = new ElasticQuery<Data>(SharedProvider);
            var selected = queryable.Select(r => new { Y = r.Subdata.DateTime });
            var translation = ElasticQueryTranslator.Translate(Mapping, "prefix", selected.Expression);

            Assert.Equal("prefix.subdata.dateTime", translation.SearchRequest.Fields[0]);
        }


        [Fact]
        public void SelectNewClassWithEntitySubpropertyTranslatesToFields()
        {
            var queryable = new ElasticQuery<Data>(SharedProvider);
            var selected = queryable.Select(r => new DataProjection { Id = r.Id, Name = r.Name, Y = r.Subdata.DateTime, C1 = r.Subdata.Child.Name, P1 = r.Subdata.Child.Parent.Name });
            var translation = ElasticQueryTranslator.Translate(Mapping, "prefix", selected.Expression);

            Assert.Equal("prefix.id", translation.SearchRequest.Fields[0]);
            Assert.Equal("prefix.name", translation.SearchRequest.Fields[1]);
            Assert.Equal("prefix.subdata.dateTime", translation.SearchRequest.Fields[2]);
            Assert.Equal("prefix.subdata.child.name", translation.SearchRequest.Fields[3]);
            Assert.Equal("prefix.subdata.child.parent.name", translation.SearchRequest.Fields[4]);
        }

        [Fact]
        public void SelectObjectTranslatesToFields()
        {
            var queryable = new ElasticQuery<Data>(SharedProvider);
            var selected = queryable.Select(r => new{r.Subdata.Id, r.Subdata.Name, r.Subdata.DateTime});
            var translation = ElasticQueryTranslator.Translate(Mapping, "prefix", selected.Expression);

            Assert.True(translation.SearchRequest.Fields.Contains("prefix.subdata.id"));
            Assert.True(translation.SearchRequest.Fields.Contains("prefix.subdata.name"));
            Assert.True(translation.SearchRequest.Fields.Contains("prefix.subdata.dateTime"));
        }
    }
}