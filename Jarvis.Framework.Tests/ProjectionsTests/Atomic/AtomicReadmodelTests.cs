﻿using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Tests.EngineTests;
using Jarvis.Framework.Tests.ProjectionsTests.Atomic.Support;
using NUnit.Framework;
using System;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionsTests.Atomic
{
    [TestFixture]
    public class AtomicReadmodelTests : AtomicProjectionEngineTestBase
    {
        [Test]
        public async Task Verify_basic_dispatch_of_changeset()
        {
            var cs = await GenerateCreatedEvent(false).ConfigureAwait(false);
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            rm.ProcessChangeset(cs);

            Assert.That(rm.ProjectedPosition, Is.EqualTo(cs.GetChunkPosition()));
        }

        [Test]
        public async Task Verify_correctly_return_of_handled_event()
        {
            var cs = await GenerateCreatedEvent(false).ConfigureAwait(false);
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            rm.ProcessChangeset(cs);

            Assert.IsTrue(rm.Created);
        }

        [Test]
        public async Task Verify_cache_does_not_dispatch_wrong_event()
        {
            //ok process a created event
            var cs = await GenerateCreatedEvent(false).ConfigureAwait(false);
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            rm.ProcessChangeset(cs);

            //then take a different readmodel that does not consume that element
            ++_aggregateIdSeed;
            var rm2 = new SimpleAtomicAggregateReadModel(new AtomicAggregateId(_aggregateIdSeed));
            var processed = rm2.ProcessChangeset(cs);

            Assert.IsFalse(processed);
        }

        [Test]
        public async Task Verify_cache_does_not_dispatch_wrong_event_to_reamodel_of_same_aggregate()
        {
            //ok process a created event
            var cs = await GenerateCreatedEvent(false).ConfigureAwait(false);
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            rm.ProcessChangeset(cs);

            //then take a different readmodel that does not consume that element
            ++_aggregateIdSeed;
            var rm2 = new AnotherSimpleTestAtomicReadModel(new AtomicAggregateId(_aggregateIdSeed));
            var processed = rm2.ProcessChangeset(cs);

            Assert.IsTrue(processed);
            Assert.That(rm2.Created, Is.True);
        }

        [Test]
        public async Task Verify_correctly_return_of_handled_event_even_if_a_single_Event_is_handled()
        {
            var cs = await GenerateCreatedEvent(false).ConfigureAwait(false);
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            rm.ProcessChangeset(cs);

            var evta = new SampleAggregateDerived1();
            var evtb = new SampleAggregateTouched();
            var cs2 = await ProcessEvents(new DomainEvent[] { evta, evtb }, p => new AtomicAggregateId(p));
            Assert.IsTrue(rm.ProcessChangeset(cs2));
        }

        [Test]
        public async Task Verify_correctly_return_false_of_Unhandled_event()
        {
            var cs = await GenerateSampleAggregateDerived1(false).ConfigureAwait(false);
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            Assert.IsFalse(rm.ProcessChangeset(cs));
        }

        [Test]
        public async Task Verify_basic_dispatch_of_two_changeset()
        {
            var cs = await GenerateAtomicAggregateCreatedEvent(false).ConfigureAwait(false);
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            rm.ProcessChangeset(cs);
            var touch = await GenerateTouchedEvent(false).ConfigureAwait(false);
            rm.ProcessChangeset(touch);

            Assert.That(rm.ProjectedPosition, Is.EqualTo(touch.GetChunkPosition()));
        }

        [Test]
        public async Task Verify_idempotence_of_changeest_processing()
        {
            var cs = await GenerateAtomicAggregateCreatedEvent(false).ConfigureAwait(false);
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            rm.ProcessChangeset(cs);
            var touch = await GenerateTouchedEvent(false).ConfigureAwait(false);
            rm.ProcessChangeset(touch);

            Assert.That(rm.TouchCount, Is.EqualTo(1));

            rm.ProcessChangeset(touch);

            Assert.That(rm.TouchCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Verify_idempotence_of_changeest_processing_past_event()
        {
            var cs = await GenerateAtomicAggregateCreatedEvent(false).ConfigureAwait(false);
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            rm.ProcessChangeset(cs);
            var touch = await GenerateTouchedEvent(false).ConfigureAwait(false);
            rm.ProcessChangeset(touch);

            Assert.That(rm.TouchCount, Is.EqualTo(1));

            var touch2 = await GenerateTouchedEvent(false).ConfigureAwait(false);
            rm.ProcessChangeset(touch2);
            rm.ProcessChangeset(touch);

            Assert.That(rm.TouchCount, Is.EqualTo(2));
        }

        [Test]
        public async Task Verify_basic_properties()
        {
            var cs = await GenerateCreatedEvent(false, issuedBy : "admin").ConfigureAwait(false);
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            rm.ProcessChangeset(cs);

            Assert.That(rm.CreationUser, Is.EqualTo("admin"));
            Assert.That(rm.LastModificationUser, Is.EqualTo("admin"));
            Assert.That(rm.LastModify, Is.EqualTo(((DomainEvent)cs.Events[0]).CommitStamp));
            Assert.That(rm.AggregateVersion, Is.EqualTo(cs.AggregateVersion));

            cs = await GenerateTouchedEvent(false, issuedBy: "admin2").ConfigureAwait(false);
            rm.ProcessChangeset(cs);

            Assert.That(rm.CreationUser, Is.EqualTo("admin"));
            Assert.That(rm.LastModificationUser, Is.EqualTo("admin2"));
            Assert.That(rm.LastModify, Is.EqualTo(((DomainEvent)cs.Events[0]).CommitStamp));
            Assert.That(rm.AggregateVersion, Is.EqualTo(cs.AggregateVersion));
        }
    }
}
