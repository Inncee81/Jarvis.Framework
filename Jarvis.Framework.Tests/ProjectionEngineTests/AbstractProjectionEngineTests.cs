using System;
using System.Collections.Generic;
using System.Linq;
using Castle.Core.Logging;
using CommonDomain.Core;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Kernel.ProjectionEngine;
using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using Jarvis.Framework.Kernel.Store;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.MultitenantSupport;
using Jarvis.Framework.Shared.ReadModel;
using Jarvis.NEventStoreEx.CommonDomainEx.Persistence.EventStore;
using MongoDB.Driver;
using NEventStore;
using NSubstitute;
using NUnit.Framework;

namespace Jarvis.Framework.Tests.ProjectionEngineTests
{
    public abstract class AbstractProjectionEngineTests
    {
        protected ConcurrentProjectionsEngine Engine;
        string _eventStoreConnectionString;
        IdentityManager _identityConverter;
        protected RepositoryEx Repository;
        IStoreEvents _eventStore;
        protected MongoDatabase Database;
        RebuildContext _rebuildContext;
        protected MongoStorageFactory StorageFactory;

        [TestFixtureSetUp]
        public virtual void TestFixtureSetUp()
        {
            _eventStoreConnectionString = GetConnectionString();

            DropDb();

            _identityConverter = new IdentityManager(new CounterService(Database));
            RegisterIdentities(_identityConverter);
            CollectionNames.Customize = name => "rm." + name;

            ConfigureEventStore();
            ConfigureProjectionEngine();
        }

        protected abstract void RegisterIdentities(IdentityManager identityConverter);

        protected abstract string GetConnectionString();

        void DropDb()
        {
            var url = new MongoUrl(_eventStoreConnectionString);
            var client = new MongoClient(url);
            Database = client.GetServer().GetDatabase(url.DatabaseName);
            Database.Drop();
        }

        [TestFixtureTearDown]
        public virtual void TestFixtureTearDown()
        {
            _eventStore.Dispose();
            Engine.Stop();
            CollectionNames.Customize = name => name;
        }

        void ConfigureEventStore()
        {
            var loggerFactory = Substitute.For<ILoggerFactory>();
            loggerFactory.Create(Arg.Any<Type>()).Returns(NullLogger.Instance);
            var factory = new EventStoreFactory(loggerFactory);
            _eventStore = factory.BuildEventStore(_eventStoreConnectionString);
            Repository = new RepositoryEx(
                _eventStore,
                new AggregateFactory(),
                new ConflictDetector(),
                _identityConverter
                );
        }

        void ConfigureProjectionEngine()
        {
            var tracker = new ConcurrentCheckpointTracker(Database);
            var tenantId = new TenantId("engine");

            var config = new ProjectionEngineConfig()
            {
                Slots = new[] { "*" },
                EventStoreConnectionString = _eventStoreConnectionString,
                TenantId = tenantId
            };

            _rebuildContext = new RebuildContext(false);
            StorageFactory = new MongoStorageFactory(Database, _rebuildContext);

            Engine = new ConcurrentProjectionsEngine(
                tracker,
                BuildProjections().ToArray(),
                new PollingClientWrapper(new CommitEnhancer(_identityConverter), true, tracker),
                new NullHouseKeeper(),
                _rebuildContext,
                new NullNotifyCommitHandled(),
                config
                );
            Engine.LoggerFactory = Substitute.For<ILoggerFactory>();
            Engine.LoggerFactory.Create(Arg.Any<Type>()).Returns(NullLogger.Instance);
            Engine.StartWithManualPoll();
        }

        protected abstract IEnumerable<IProjection> BuildProjections();
    }
}