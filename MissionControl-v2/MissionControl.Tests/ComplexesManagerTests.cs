using System;
using Microsoft.Extensions.Logging;
using Moq;
using Unity.ClusterDisplay.MissionControl.MissionControl.Library;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    public class ComplexesManagerTests
    {
        [SetUp]
        public void Setup()
        {
            m_LoggerMock.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            m_LoggerMock.VerifyNoOtherCalls();
        }

        [Test]
        public async Task AddRemove()
        {
            ComplexesManager manager = new(m_LoggerMock.Object);

            await manager.PutAsync(k_ComplexA);
            await manager.PutAsync(k_ComplexB);

            using (var locked = await manager.GetLockedReadOnlyAsync())
            {
                Assert.That(locked.Value.Count, Is.EqualTo(2));
            }

            await manager.RemoveAsync(k_ComplexA.Id);
            Assert.That(await manager.RemoveAsync(Guid.NewGuid()), Is.False);

            using (var locked = await manager.GetLockedReadOnlyAsync())
            {
                Assert.That(locked.Value.Count, Is.EqualTo(1));
                Assert.That(locked.Value.ContainsKey(k_ComplexB.Id));
            }
        }

        [Test]
        public async Task Update()
        {
            ComplexesManager manager = new(m_LoggerMock.Object);

            await manager.PutAsync(k_ComplexA);

            using (var locked = await manager.GetLockedReadOnlyAsync())
            {
                Assert.That(locked.Value[k_ComplexA.Id], Is.SameAs(k_ComplexA));
            }

            var complexAClone = k_ComplexA.DeepClone();
            Assert.That(complexAClone, Is.Not.SameAs(k_ComplexA));
            complexAClone.Name = "Improved A";
            await manager.PutAsync(complexAClone);

            using (var locked = await manager.GetLockedReadOnlyAsync())
            {
                Assert.That(locked.Value[k_ComplexA.Id], Is.SameAs(complexAClone));
            }
        }

        [Test]
        public async Task BadHangarBayId()
        {
            ComplexesManager manager = new(m_LoggerMock.Object);

            await manager.PutAsync(k_ComplexA);

            using (var locked = await manager.GetLockedReadOnlyAsync())
            {
                Assert.That(locked.Value[k_ComplexA.Id], Is.SameAs(k_ComplexA));
            }

            var complexAClone = k_ComplexA.DeepClone();
            complexAClone.HangarBay.Identifier = Guid.NewGuid();
            Assert.That(async () => await manager.PutAsync(complexAClone), Throws.TypeOf<ArgumentException>());

            using (var locked = await manager.GetLockedReadOnlyAsync())
            {
                Assert.That(locked.Value[k_ComplexA.Id], Is.SameAs(k_ComplexA));
            }
        }

        [Test]
        public async Task DetectsChangesFromTheOutside()
        {
            ComplexesManager manager = new(m_LoggerMock.Object);

            var complexAClone = k_ComplexA.DeepClone();
            await manager.PutAsync(complexAClone);

            complexAClone.HangarBay.Identifier = Guid.NewGuid();
            using (var lockedCollection = await manager.GetLockedReadOnlyAsync())
            {
                var hackedCollection = (IncrementalCollection<LaunchComplex>)lockedCollection.Value;
                Assert.Throws<InvalidOperationException>(() => complexAClone.SignalChanges(hackedCollection));
            }

            using var locked = await manager.GetLockedReadOnlyAsync();
            var collection = (IncrementalCollection<LaunchComplex>)locked.Value;
            Assert.Throws<InvalidOperationException>(() => collection[k_ComplexB.Id] = k_ComplexB);
        }

        [Test]
        public void DetectsInternalEndpointConflicts()
        {
            ComplexesManager manager = new(m_LoggerMock.Object);

            var complexAClone = k_ComplexA.DeepClone();
            complexAClone.LaunchPads.First().Endpoint = complexAClone.HangarBay.Endpoint;
            Assert.That(async () => await manager.PutAsync(complexAClone), Throws.TypeOf<ArgumentException>());

            complexAClone = k_ComplexA.DeepClone();
            complexAClone.LaunchPads.ElementAt(0).Endpoint = complexAClone.LaunchPads.ElementAt(1).Endpoint;
            Assert.That(async () => await manager.PutAsync(complexAClone), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void DetectsInternalIdentifierConflicts()
        {
            ComplexesManager manager = new(m_LoggerMock.Object);

            var complexAClone = k_ComplexA.DeepClone();
            complexAClone.LaunchPads.First().Identifier = complexAClone.Id;
            Assert.That(async () => await manager.PutAsync(complexAClone), Throws.TypeOf<ArgumentException>());

            complexAClone = k_ComplexA.DeepClone();
            complexAClone.LaunchPads.ElementAt(0).Identifier = complexAClone.LaunchPads.ElementAt(1).Identifier;
            Assert.That(async () => await manager.PutAsync(complexAClone), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public async Task DetectsExternalEndpointConflicts()
        {
            ComplexesManager manager = new(m_LoggerMock.Object);

            await manager.PutAsync(k_ComplexB);

            var complexAClone = k_ComplexA.DeepClone();
            complexAClone.HangarBay.Endpoint = k_ComplexB.HangarBay.Endpoint;
            Assert.That(async () => await manager.PutAsync(complexAClone), Throws.TypeOf<ArgumentException>());
            complexAClone.HangarBay.Endpoint = k_ComplexB.LaunchPads.First().Endpoint;
            Assert.That(async () => await manager.PutAsync(complexAClone), Throws.TypeOf<ArgumentException>());

            complexAClone = k_ComplexA.DeepClone();
            complexAClone.LaunchPads.First().Endpoint = k_ComplexB.HangarBay.Endpoint;
            Assert.That(async () => await manager.PutAsync(complexAClone), Throws.TypeOf<ArgumentException>());
            complexAClone.LaunchPads.First().Endpoint = k_ComplexB.LaunchPads.First().Endpoint;
            Assert.That(async () => await manager.PutAsync(complexAClone), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public async Task DetectsExternalIdentifierConflicts()
        {
            ComplexesManager manager = new(m_LoggerMock.Object);

            await manager.PutAsync(k_ComplexB);

            var complexAClone = k_ComplexA.DeepClone();
            complexAClone.HangarBay.Identifier = k_ComplexB.HangarBay.Identifier;
            Assert.That(async () => await manager.PutAsync(complexAClone), Throws.TypeOf<ArgumentException>());
            complexAClone.HangarBay.Identifier = k_ComplexB.LaunchPads.First().Identifier;
            Assert.That(async () => await manager.PutAsync(complexAClone), Throws.TypeOf<ArgumentException>());

            complexAClone = k_ComplexA.DeepClone();
            complexAClone.LaunchPads.First().Identifier = k_ComplexB.HangarBay.Identifier;
            Assert.That(async () => await manager.PutAsync(complexAClone), Throws.TypeOf<ArgumentException>());
            complexAClone.LaunchPads.First().Identifier = k_ComplexB.LaunchPads.First().Identifier;
            Assert.That(async () => await manager.PutAsync(complexAClone), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public async Task SaveLoad()
        {
            ComplexesManager manager = new(m_LoggerMock.Object);

            await manager.PutAsync(k_ComplexA);
            await manager.PutAsync(k_ComplexB);

            MemoryStream memoryStream = new();
            await manager.SaveAsync(memoryStream);

            manager = new(m_LoggerMock.Object);
            using (var locked = await manager.GetLockedReadOnlyAsync())
            {
                Assert.That(locked.Value, Is.Empty);
            }

            memoryStream.Position = 0;
            manager.Load(memoryStream);

            using (var locked = await manager.GetLockedReadOnlyAsync())
            {
                Assert.That(locked.Value.Count, Is.EqualTo(2));

                Assert.That(locked.Value.ContainsKey(k_ComplexA.Id), Is.True);
                Assert.That(locked.Value[k_ComplexA.Id], Is.EqualTo(k_ComplexA));

                Assert.That(locked.Value.ContainsKey(k_ComplexB.Id), Is.True);
                Assert.That(locked.Value[k_ComplexB.Id], Is.EqualTo(k_ComplexB));
            }
        }

        static readonly LaunchComplex k_ComplexA;
        static readonly LaunchComplex k_ComplexB;

        static ComplexesManagerTests()
        {
            Guid complexAId = Guid.NewGuid();
            k_ComplexA = new(complexAId)
            {
                Name = "Complex A",
                HangarBay = new()
                {
                    Identifier = complexAId,
                    Endpoint = new("http://127.0.0.1:8100")
                },
                LaunchPads = new[] {
                    new LaunchPad()
                    {
                        Identifier = Guid.NewGuid(),
                        Name = "A1",
                        Endpoint = new("http://127.0.0.1:8201"),
                        SuitableFor = new []{ "clusterNode" }
                    },
                    new LaunchPad()
                    {
                        Identifier = Guid.NewGuid(),
                        Name = "A2",
                        Endpoint = new("http://127.0.0.1:8202"),
                        SuitableFor = new []{ "clusterNode" }
                    }
                }
            };

            Guid complexBId = Guid.NewGuid();
            k_ComplexB = new(complexBId)
            {
                Name = "Complex B",
                HangarBay = new()
                {
                    Identifier = complexBId,
                    Endpoint = new("http://127.0.0.1:8101")
                },
                LaunchPads = new[] {
                    new LaunchPad()
                    {
                        Identifier = Guid.NewGuid(),
                        Name = "B1",
                        Endpoint = new("http://127.0.0.1:8203"),
                        SuitableFor = new []{ "clusterNode" }
                    }
                }
            };
        }

        Mock<ILogger> m_LoggerMock = new();
    }
}
