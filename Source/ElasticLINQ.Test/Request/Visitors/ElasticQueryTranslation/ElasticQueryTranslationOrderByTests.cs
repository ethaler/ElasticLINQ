// Licensed under the Apache 2.0 License. See LICENSE.txt in the project root for more information.

using System;
using ElasticLinq.Request.Visitors;
using System.Linq;
using Xunit;

namespace ElasticLinq.Test.Request.Visitors.ElasticQueryTranslation
{
    public class ElasticQueryTranslationOrderByTests : ElasticQueryTranslationTestsBase
    {
        [Fact]
        public void OrderByTranslatesToSortAscending()
        {
            var ordered = Robots.OrderBy(e => e.Id);
            var sortOptions = ElasticQueryTranslator.Translate(Mapping, "prefix", ordered.Expression).SearchRequest.SortOptions;

            Assert.Single(sortOptions, o => o.Ascending && o.Name == "prefix.id");
            Assert.Equal(1, sortOptions.Count);
        }

        [Fact]
        public void OrderByScoreFieldTranslatesToScoreSortAscending()
        {
            var ordered = Robots.OrderBy(e => ElasticFields.Score);
            var sortOptions = ElasticQueryTranslator.Translate(Mapping, "prefix", ordered.Expression).SearchRequest.SortOptions;

            Assert.Single(sortOptions, o => o.Ascending && o.Name == "_score");
            Assert.Equal(1, sortOptions.Count);
        }

        [Fact]
        public void OrderByDescendingTranslatesToSortDescending()
        {
            var ordered = Robots.OrderByDescending(e => e.Name);
            var sortOptions = ElasticQueryTranslator.Translate(Mapping, "prefix", ordered.Expression).SearchRequest.SortOptions;

            Assert.Single(sortOptions, o => !o.Ascending && o.Name == "prefix.name");
            Assert.Equal(1, sortOptions.Count);
        }

        [Fact]
        public void OrderByDescendingScoreFieldTranslatesToScoreSortDescending()
        {
            var ordered = Robots.OrderByDescending(e => ElasticFields.Score);
            var sortOptions = ElasticQueryTranslator.Translate(Mapping, "prefix", ordered.Expression).SearchRequest.SortOptions;

            Assert.Single(sortOptions, o => !o.Ascending && o.Name == "_score");
            Assert.Equal(1, sortOptions.Count);
        }

        [Fact]
        public void OrderByScoreTranslatesToScoreSortAscending()
        {
            var ordered = Robots.OrderByScore();
            var sortOptions = ElasticQueryTranslator.Translate(Mapping, "prefix", ordered.Expression).SearchRequest.SortOptions;

            Assert.Single(sortOptions, o => o.Ascending && o.Name == "_score");
            Assert.Equal(1, sortOptions.Count);
        }

        [Fact]
        public void OrderByScoreDescendingTranslatesToScoreSortDescending()
        {
            var ordered = Robots.OrderByScoreDescending();
            var sortOptions = ElasticQueryTranslator.Translate(Mapping, "prefix", ordered.Expression).SearchRequest.SortOptions;

            Assert.Single(sortOptions, o => !o.Ascending && o.Name == "_score");
            Assert.Equal(1, sortOptions.Count);
        }

        [Fact]
        public void ThenByTranslatesToSecondSortAscending()
        {
            var ordered = Robots.OrderBy(e => e.Id).ThenBy(e => e.Name);
            var sortOptions = ElasticQueryTranslator.Translate(Mapping, "prefix", ordered.Expression).SearchRequest.SortOptions;

            Assert.NotNull(sortOptions);
            Assert.True(sortOptions[0].Ascending && sortOptions[0].Name == "prefix.id");
            Assert.True(sortOptions[1].Ascending && sortOptions[1].Name == "prefix.name");
            Assert.Equal(2, sortOptions.Count);
        }

        [Fact]
        public void ThenByScoreFieldTranslatesToSecondScoreSortAscending()
        {
            var ordered = Robots.OrderBy(e => e.Id).ThenBy(e => ElasticFields.Score);
            var sortOptions = ElasticQueryTranslator.Translate(Mapping, "prefix", ordered.Expression).SearchRequest.SortOptions;

            Assert.NotNull(sortOptions);
            Assert.True(sortOptions[0].Ascending && sortOptions[0].Name == "prefix.id");
            Assert.True(sortOptions[1].Ascending && sortOptions[1].Name == "_score");
            Assert.Equal(2, sortOptions.Count);
        }

        [Fact]
        public void ThenByDescendingTranslatesToSecondSortDescending()
        {
            var ordered = Robots.OrderBy(e => e.Id).ThenByDescending(e => e.Name);
            var sortOptions = ElasticQueryTranslator.Translate(Mapping, "prefix", ordered.Expression).SearchRequest.SortOptions;

            Assert.NotNull(sortOptions);
            Assert.True(sortOptions[0].Ascending && sortOptions[0].Name == "prefix.id");
            Assert.True(!sortOptions[1].Ascending && sortOptions[1].Name == "prefix.name");
            Assert.Equal(2, sortOptions.Count);
        }

        [Fact]
        public void ThenByDescendingScoreFieldTranslatesToSecondScoreSortDescending()
        {
            var ordered = Robots.OrderBy(e => e.Id).ThenByDescending(e => ElasticFields.Score);
            var sortOptions = ElasticQueryTranslator.Translate(Mapping, "prefix", ordered.Expression).SearchRequest.SortOptions;

            Assert.NotNull(sortOptions);
            Assert.True(sortOptions[0].Ascending && sortOptions[0].Name == "prefix.id");
            Assert.True(!sortOptions[1].Ascending && sortOptions[1].Name == "_score");
            Assert.Equal(2, sortOptions.Count);
        }

        [Fact]
        public void ThenByScoreTranslatesToSortAscending()
        {
            var ordered = Robots.OrderBy(o => o.Id).ThenByScore();
            var sortOptions = ElasticQueryTranslator.Translate(Mapping, "prefix", ordered.Expression).SearchRequest.SortOptions;

            Assert.True(sortOptions[0].Ascending && sortOptions[0].Name == "prefix.id");
            Assert.True(sortOptions[1].Ascending && sortOptions[1].Name == "_score");
            Assert.Equal(2, sortOptions.Count);
        }

        [Fact]
        public void ThenByScoreDescendingTranslatesToSortDescending()
        {
            var ordered = Robots.OrderBy(o => o.Id).ThenByScoreDescending();
            var sortOptions = ElasticQueryTranslator.Translate(Mapping, "prefix", ordered.Expression).SearchRequest.SortOptions;

            Assert.True(sortOptions[0].Ascending && sortOptions[0].Name == "prefix.id");
            Assert.True(!sortOptions[1].Ascending && sortOptions[1].Name == "_score");
            Assert.Equal(2, sortOptions.Count);
        }

        [Fact]
        public void OrderByWithThenByTwiceTranslatesToThreeSorts()
        {
            var ordered = Robots.OrderBy(e => e.Id).ThenByScore().ThenBy(e => e.Cost);
            var sortOptions = ElasticQueryTranslator.Translate(Mapping, "prefix", ordered.Expression).SearchRequest.SortOptions;

            Assert.NotNull(sortOptions);
            Assert.True(sortOptions[0].Ascending && sortOptions[0].Name == "prefix.id");
            Assert.True(sortOptions[1].Ascending && sortOptions[1].Name == "_score");
            Assert.True(sortOptions[2].Ascending && sortOptions[2].Name == "prefix.cost");
            Assert.Equal(3, sortOptions.Count);
        }

        [Fact]
        public void OrderByStringSpecifiesIgnoreUnmappedSortOption()
        {
            var ordered = Robots.OrderBy(e => e.Name);
            var sortOptions = ElasticQueryTranslator.Translate(Mapping, "prefix", ordered.Expression).SearchRequest.SortOptions;

            Assert.NotNull(sortOptions);
            Assert.Equal(1, sortOptions.Count);
            Assert.True(sortOptions[0].IgnoreUnmapped);
        }

        [Fact]
        public void OrderByDecimalDoesNotSpecifyIgnoreUnmappedSortOption()
        {
            var ordered = Robots.OrderBy(e => e.EnergyUse);
            var sortOptions = ElasticQueryTranslator.Translate(Mapping, "prefix", ordered.Expression).SearchRequest.SortOptions;

            Assert.NotNull(sortOptions);
            Assert.Equal(1, sortOptions.Count);
            Assert.False(sortOptions[0].IgnoreUnmapped);
        }

        [Fact]
        public void OrderByNullableIntSpecifiesIgnoreUnmappedSortOption()
        {
            var ordered = Robots.OrderBy(e => e.Zone);
            var sortOptions = ElasticQueryTranslator.Translate(Mapping, "prefix", ordered.Expression).SearchRequest.SortOptions;

            Assert.NotNull(sortOptions);
            Assert.Equal(1, sortOptions.Count);
            Assert.True(sortOptions[0].IgnoreUnmapped);
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

        [Fact]
        public void OrderBySubProperty()
        {
            var queryable = new ElasticQuery<Data>(SharedProvider);
            var ordered = queryable.OrderBy(d => d.Subdata.Name);

            var sortOptions = ElasticQueryTranslator.Translate(Mapping, "prefix", ordered.Expression).SearchRequest.SortOptions;
            Assert.Equal(1, sortOptions.Count);

            var sortOption = sortOptions[0];
            Assert.Equal("prefix.subdata.name", sortOption.Name);
        }

        [Fact]
        public void OrderBySubSubProperty()
        {
            var queryable = new ElasticQuery<Data>(SharedProvider);
            var ordered = queryable.OrderBy(d => d.Subdata.Child.Name);

            var sortOptions = ElasticQueryTranslator.Translate(Mapping, "prefix", ordered.Expression).SearchRequest.SortOptions;
            Assert.Equal(1, sortOptions.Count);

            var sortOption = sortOptions[0];
            Assert.Equal("prefix.subdata.child.name", sortOption.Name);
        }
    }
}
