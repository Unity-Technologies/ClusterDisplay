using System;

namespace Unity.ClusterDisplay.MissionControl.LaunchPad.Tests
{
    public class HealthTests
    {
        [TearDown]
        public void TearDown()
        {
            m_ProcessHelper.Dispose();

            foreach (string folder in m_TestTempFolders)
            {
                try
                {
                    Directory.Delete(folder, true);
                }
                catch
                {
                    // ignored
                }
            }
        }

        [Test]
        public async Task Get()
        {
            await m_ProcessHelper.Start(GetTestTempFolder());

            // Start some background processing to use all available processing power
            CancellationTokenSource cancelBackgroundProcess = new();
            List<Thread> backgroundThreads = new();
            long someValue = 0;
            for (int i = 0; i < Environment.ProcessorCount; ++i)
            {
                backgroundThreads.Add(new Thread(() => {
                    Random random = new Random(Guid.NewGuid().GetHashCode());
                    int threadSum = 0;
                    while (!cancelBackgroundProcess.IsCancellationRequested)
                    {
                        threadSum += random.Next(0, 10);
                    }
                    if ((threadSum & 1) == 0)
                    {
                        Interlocked.Increment(ref someValue);
                    }
                }));
                backgroundThreads.Last().Priority = ThreadPriority.Lowest;
                backgroundThreads.Last().Start();
            }

            try
            {
                // Wait a little bit so that CPU usage has the time to increase and the launch pad sample CPU usage often
                // enough.
                await Task.Delay(5000);

                // Get health
                var health = await m_ProcessHelper.GetHealth();
                Assert.That(health, Is.Not.Null);
                Assert.That(health!.CpuUtilization, Is.GreaterThan(0.75f));
                Assert.That(health.MemoryUsage, Is.GreaterThan(0));
                Assert.That(health.MemoryAvailable, Is.GreaterThan(0));
            }
            finally
            {
                cancelBackgroundProcess.Cancel();
                foreach (var thread in backgroundThreads)
                {
                    thread.Join();
                }
            }
        }

        string GetTestTempFolder()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), "HealthTests_" + Guid.NewGuid().ToString());
            m_TestTempFolders.Add(folderPath);
            return folderPath;
        }

        LaunchPadProcessHelper m_ProcessHelper = new();
        List<string> m_TestTempFolders = new();
    }
}
