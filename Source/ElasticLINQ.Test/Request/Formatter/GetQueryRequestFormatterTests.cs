﻿// Copyright (c) Tier 3 Inc. All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using ElasticLinq;
using ElasticLinq.Request;
using ElasticLinq.Request.Formatter;
using System;
using System.Collections.Generic;
using Xunit;

namespace ElasticLINQ.Test.Request.Formatter
{
    public class GetQueryRequestFormatterTests
    {
        [Fact]
        public void UriAddsQueryParameters()
        {
            var connection = MakeConnection();
            var request = MakeRequest();

            var actual = new GetQueryRequestFormatter(connection, request).Uri;

            Assert.Contains("sort1", actual.Query);
        }

        private static ElasticConnection MakeConnection()
        {
            return new ElasticConnection(new Uri("http://a.b.com:9000/d"), TimeSpan.FromSeconds(10), "index", true);
        }

        private static ElasticSearchRequest MakeRequest()
        {
            return new ElasticSearchRequest("type1", 20, 10,
                new List<string> { "field1" },
                new List<SortOption> { new SortOption("sort1", false) },
                new Dictionary<string, IReadOnlyList<object>> {
                    { "term1", new List<object> { "criteria1" }.AsReadOnly() }
                });
        }
    }
}