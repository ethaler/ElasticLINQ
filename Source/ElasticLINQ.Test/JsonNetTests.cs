﻿// Licensed under the Apache 2.0 License. See LICENSE.txt in the project root for more information.

using ElasticLinq.Mapping;
using ElasticLinq.Test.TestSupport;
using System;
using System.Globalization;
using System.Linq;
using Xunit;

namespace ElasticLinq.Test
{
    public class JsonNetTests
    {
        class MyCustomMapping : ElasticMapping
        {
            public override string GetDocumentMappingPrefix(Type type)
            {
                return String.Format("docWrapper.{0}", type.Name.ToCamelCase(CultureInfo.CurrentCulture));
            }
        }

        [Fact]
        public static void CustomTypes_Term()
        {
            var context = new TestableElasticContext(new MyCustomMapping());
            var helloIdentifier = new Identifier("Hello");

            var query = context.Query<ClassWithIdentifier>().Where(x => x.id == helloIdentifier).ToElasticSearchQuery();

            // Also verifies that any value which gets JSON converted into a string gets lower-cased
            Assert.Equal(@"{""filter"":{""term"":{""docWrapper.classWithIdentifier.id"":""hello!!""}}}", query);
        }

        [Fact]
        public static void CustomTypes_Terms()
        {
            var context = new TestableElasticContext();
            var identifiers = new[] { new Identifier("vALue1"), new Identifier("ValuE2") };

            var query = context.Query<ClassWithIdentifier>().Where(x => identifiers.Contains(x.id)).ToElasticSearchQuery();

            // Also verifies that any value which gets JSON converted into a string gets lower-cased
            Assert.Equal(@"{""filter"":{""terms"":{""id"":[""value1!!"",""value2!!""]}}}", query);
        }

        class ClassWithIdentifier
        {
            public Identifier id { get; set; }
        }
    }
}
