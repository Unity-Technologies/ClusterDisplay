using System;
using Microsoft.Extensions.Logging;
using Moq;
using Unity.ClusterDisplay.MissionControl.MissionControl.Library;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Tests
{
    public class MissionsManagerTests
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
        public async Task Store()
        {
            MissionsManager manager = new(m_LoggerMock.Object);

            // Add a new mission
            MissionDetails toStore = new()
            {
                Identifier = Guid.NewGuid(),
                Name = "toStore",
                Description = "Description of toStore",
                LaunchConfiguration = new() { AssetId = Guid.NewGuid() }
            };
            var timeBeforeStore = DateTime.Now;
            await manager.StoreAsync(toStore);
            var timeAfterStore = DateTime.Now;

            var fromManager = await manager.GetAsync(toStore.Identifier);
            Assert.That(fromManager, Is.Not.SameAs(toStore));
            Assert.That(fromManager, Is.EqualTo(toStore));

            using (var lockedList = await manager.GetLockedReadOnlyAsync())
            {
                var summary = lockedList.Value[toStore.Identifier];
                Assert.That(summary.Name, Is.EqualTo(toStore.Name));
                Assert.That(summary.Description, Is.EqualTo(toStore.Description));
                Assert.That(summary.SaveTime, Is.InRange(timeBeforeStore, timeAfterStore));
                Assert.That(summary.AssetId, Is.EqualTo(toStore.LaunchConfiguration.AssetId));
            }

            // Update it
            toStore.Name += " updated";
            toStore.Description += " updated";
            toStore.LaunchConfiguration.AssetId = Guid.NewGuid();

            timeBeforeStore = DateTime.Now;
            await manager.StoreAsync(toStore);
            timeAfterStore = DateTime.Now;

            fromManager = await manager.GetAsync(toStore.Identifier);
            Assert.That(fromManager, Is.Not.SameAs(toStore));
            Assert.That(fromManager, Is.EqualTo(toStore));

            using (var lockedList = await manager.GetLockedReadOnlyAsync())
            {
                var summary = lockedList.Value[toStore.Identifier];
                Assert.That(summary.Name, Is.EqualTo(toStore.Name));
                Assert.That(summary.Description, Is.EqualTo(toStore.Description));
                Assert.That(summary.SaveTime, Is.InRange(timeBeforeStore, timeAfterStore));
                Assert.That(summary.AssetId, Is.EqualTo(toStore.LaunchConfiguration.AssetId));
            }
        }

        [Test]
        public async Task SkipUselessStore()
        {
            MissionsManager manager = new(m_LoggerMock.Object);

            int changesCounter = 0;
            using (var lockedList = await manager.GetLockedReadOnlyAsync())
            {
                lockedList.Value.OnSomethingChanged += _ => changesCounter++;
            }

            // Add a new mission
            MissionDetails toStore = new()
            {
                Identifier = Guid.NewGuid(),
                Name = "toStore",
                Description = "Description of toStore",
                LaunchConfiguration = new() { AssetId = Guid.NewGuid() }
            };
            Assert.That(changesCounter, Is.EqualTo(0));
            await manager.StoreAsync(toStore);
            Assert.That(changesCounter, Is.EqualTo(1));

            // Add it again, shouldn't receive notifications about a change
            await manager.StoreAsync(toStore);
            Assert.That(changesCounter, Is.EqualTo(1));
            await manager.StoreAsync(toStore.DeepClone());
            Assert.That(changesCounter, Is.EqualTo(1));
        }

        [Test]
        public async Task Delete()
        {
            MissionsManager manager = new(m_LoggerMock.Object);

            // Add two new missions
            MissionDetails toStore1 = new()
            {
                Identifier = Guid.NewGuid(),
                Name = "toStore 1",
                Description = "Description of toStore 1",
                LaunchConfiguration = new() { AssetId = Guid.NewGuid() }
            };
            await manager.StoreAsync(toStore1);
            MissionDetails toStore2 = new()
            {
                Identifier = Guid.NewGuid(),
                Name = "toStore 2",
                Description = "Description of toStore 2",
                LaunchConfiguration = new() { AssetId = Guid.NewGuid() }
            };
            await manager.StoreAsync(toStore2);

            var fromManager = await manager.GetAsync(toStore1.Identifier);
            Assert.That(fromManager, Is.EqualTo(toStore1));
            fromManager = await manager.GetAsync(toStore2.Identifier);
            Assert.That(fromManager, Is.EqualTo(toStore2));

            using (var lockedList = await manager.GetLockedReadOnlyAsync())
            {
                Assert.That(lockedList.Value.ContainsKey(toStore1.Identifier), Is.True);
                Assert.That(lockedList.Value.ContainsKey(toStore2.Identifier), Is.True);
            }

            // Remove toStore1
            Assert.That(await manager.DeleteAsync(toStore1.Identifier), Is.True);

            Assert.ThrowsAsync<KeyNotFoundException>(() => manager.GetAsync(toStore1.Identifier));
            fromManager = await manager.GetAsync(toStore2.Identifier);
            Assert.That(fromManager, Is.EqualTo(toStore2));

            using (var lockedList = await manager.GetLockedReadOnlyAsync())
            {
                Assert.That(lockedList.Value.ContainsKey(toStore1.Identifier), Is.False);
                Assert.That(lockedList.Value.ContainsKey(toStore2.Identifier), Is.True);
            }

            // Remove something not in the manager, shouldn't change anything
            Assert.That(await manager.DeleteAsync(Guid.NewGuid()), Is.False);

            Assert.ThrowsAsync<KeyNotFoundException>(() => manager.GetAsync(toStore1.Identifier));
            fromManager = await manager.GetAsync(toStore2.Identifier);
            Assert.That(fromManager, Is.EqualTo(toStore2));

            using (var lockedList = await manager.GetLockedReadOnlyAsync())
            {
                Assert.That(lockedList.Value.ContainsKey(toStore1.Identifier), Is.False);
                Assert.That(lockedList.Value.ContainsKey(toStore2.Identifier), Is.True);
            }
        }

        [Test]
        public async Task SaveLoad()
        {
            MissionsManager manager = new(m_LoggerMock.Object);

            // Add two new missions
            MissionDetails toStore1 = new()
            {
                Identifier = Guid.NewGuid(),
                Name = "toStore 1",
                Description = "Description of toStore 1",
                LaunchConfiguration = new() { AssetId = Guid.NewGuid() }
            };
            await manager.StoreAsync(toStore1);
            MissionDetails toStore2 = new()
            {
                Identifier = Guid.NewGuid(),
                Name = "toStore 2",
                Description = "Description of toStore 2",
                LaunchConfiguration = new() { AssetId = Guid.NewGuid() }
            };
            await manager.StoreAsync(toStore2);

            SavedMissionSummary toStore1Summary;
            SavedMissionSummary toStore2Summary;
            using (var lockedList = await manager.GetLockedReadOnlyAsync())
            {
                toStore1Summary = lockedList.Value[toStore1.Identifier];
                toStore2Summary = lockedList.Value[toStore2.Identifier];
            }

            MemoryStream saveStream = new();
            await manager.SaveAsync(saveStream);

            // Create a new manager and load from the stream
            manager = new(m_LoggerMock.Object);
            saveStream.Position = 0;
            manager.Load(saveStream);

            Assert.That(await manager.GetAsync(toStore1.Identifier), Is.EqualTo(toStore1));
            Assert.That(await manager.GetAsync(toStore2.Identifier), Is.EqualTo(toStore2));

            using (var lockedList = await manager.GetLockedReadOnlyAsync())
            {
                Assert.That(lockedList.Value.Count, Is.EqualTo(2));
                Assert.That(lockedList.Value[toStore1.Identifier], Is.EqualTo(toStore1Summary));
                Assert.That(lockedList.Value[toStore2.Identifier], Is.EqualTo(toStore2Summary));
            }

            // Try to load again (in a manager that contains something), should fail
            saveStream.Position = 0;
            Assert.Throws<InvalidOperationException>(() => manager.Load(saveStream));
        }

        [Test]
        public async Task AssetInUse()
        {
            MissionsManager manager = new(m_LoggerMock.Object);

            // Add two new missions
            MissionDetails toStore1 = new()
            {
                Identifier = Guid.NewGuid(),
                Name = "toStore 1",
                Description = "Description of toStore 1",
                LaunchConfiguration = new() { AssetId = Guid.NewGuid() }
            };
            await manager.StoreAsync(toStore1);
            MissionDetails toStore2 = new()
            {
                Identifier = Guid.NewGuid(),
                Name = "toStore 2",
                Description = "Description of toStore 2",
                LaunchConfiguration = new() { AssetId = Guid.NewGuid() }
            };
            await manager.StoreAsync(toStore2);

            Assert.That(await manager.IsAssetInUseAsync(toStore1.LaunchConfiguration.AssetId), Is.True);
            Assert.That(await manager.IsAssetInUseAsync(toStore2.LaunchConfiguration.AssetId), Is.True);
            var thirdAssetId = Guid.NewGuid();
            Assert.That(await manager.IsAssetInUseAsync(thirdAssetId), Is.False);
        }

        Mock<ILogger> m_LoggerMock = new();
    }
}
