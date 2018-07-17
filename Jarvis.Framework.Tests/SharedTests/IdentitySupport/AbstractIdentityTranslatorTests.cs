﻿using Castle.Core.Logging;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Tests.EngineTests;
using Jarvis.Framework.Tests.Support;
using MongoDB.Driver;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;

namespace Jarvis.Framework.Tests.SharedTests.IdentitySupport
{
    [TestFixture]
    public class AbstractIdentityTranslatorTests
    {
        protected IMongoDatabase _db;
        private IdentityManager _identityManager;
        protected ILogger testLogger = NullLogger.Instance;

        [OneTimeSetUp]
        public void TestFixtureSetUp()
        {
            TestHelper.RegisterSerializerForFlatId<TestId>();
            TestHelper.RegisterSerializerForFlatId<TestFlatId>();
            _db = TestHelper.CreateNew(ConfigurationManager.ConnectionStrings["system"].ConnectionString);
            _identityManager = new IdentityManager(new CounterService(_db));
            _identityManager.RegisterIdentitiesFromAssembly(Assembly.GetExecutingAssembly());
        }

        [Test]
        public void Verify_basic_translation()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            String key = Guid.NewGuid().ToString();
            var id = sut.MapWithAutCreate(key);
            var secondCall = sut.MapWithAutCreate(key);
            Assert.That(id, Is.EqualTo(secondCall));
        }

        [Test]
        public void Verify_reverse_translation()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            String key = Guid.NewGuid().ToString();
            var id = sut.MapWithAutCreate(key);
            var reversed = sut.ReverseMap(id);
            Assert.That(reversed, Is.EqualTo(key));
        }

        [Test]
        public void Verify_reverse_translation_multiple()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            String key1 = Guid.NewGuid().ToString();
            String key2 = Guid.NewGuid().ToString();
            String key3 = Guid.NewGuid().ToString();

            var id1 = sut.MapWithAutCreate(key1);
            var id2 = sut.MapWithAutCreate(key2);
            var id3 = sut.MapWithAutCreate(key3);

            var reversed = sut.ReverseMap(id1,id2, id3);
            Assert.That(reversed[id1], Is.EqualTo(key1));
            Assert.That(reversed[id2], Is.EqualTo(key2));
            Assert.That(reversed[id3], Is.EqualTo(key3));
        }

        [Test]
        public void Verify_reverse_translation_is_resilient_to_missing_id()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            String key1 = Guid.NewGuid().ToString();
            String key2 = Guid.NewGuid().ToString();

            var id1 = sut.MapWithAutCreate(key1);
            var id2 = sut.MapWithAutCreate(key2);
            var id3 = new SampleAggregateId(100000);

            var reversed = sut.ReverseMap(id1, id2, id3);
            Assert.That(reversed[id1], Is.EqualTo(key1));
            Assert.That(reversed[id2], Is.EqualTo(key2));
            Assert.That(reversed.ContainsKey(id3), Is.False);
        }

        [Test]
        public void Verify_reverse_translation_is_resilient_to_empty_list()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            var reversed = sut.ReverseMap(new SampleAggregateId[] { });
            Assert.That(reversed.Count, Is.EqualTo(0));

            reversed = sut.ReverseMap(( SampleAggregateId[]) null);
            Assert.That(reversed.Count, Is.EqualTo(0));
        }

        private class TestMapper : AbstractIdentityTranslator<SampleAggregateId>
        {
            public TestMapper(IMongoDatabase systemDB, IIdentityGenerator identityGenerator) : base(systemDB, identityGenerator)
            {
            }

            public SampleAggregateId MapWithAutCreate(String key)
            {
                return base.Translate(key, true);
            }

            public string ReverseMap(SampleAggregateId id)
            {
                return base.GetAlias(id);
            }

            public IDictionary<SampleAggregateId, String> ReverseMap(params SampleAggregateId[] ids)
            {
                return base.GetAliases(ids);
            }
        }
    }
}
