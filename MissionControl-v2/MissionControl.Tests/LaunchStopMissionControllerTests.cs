using System.Diagnostics;
using System.Net;
using static Unity.ClusterDisplay.MissionControl.MissionControl.Helpers;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    public class LaunchStopMissionControllerTests
    {
        [SetUp]
        public async Task SetUp()
        {
            await Task.WhenAll(m_HangarBayProcessHelper.Start(GetTestTempFolder()),
                m_ProcessHelper.Start(GetTestTempFolder()));
        }

        [TearDown]
        public void TearDown()
        {
            m_ProcessHelper.Dispose();
            foreach (var launchPadProcessHelper in m_LaunchPadsProcessHelper)
            {
                launchPadProcessHelper.Dispose();
            }
            m_LaunchPadsProcessHelper.Clear();
            m_HangarBayProcessHelper.Dispose();

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
        public async Task Launch()
        {
            await PrepareMissionControl(1, k_LaunchCatalog, k_LaunchCatalogFilesContent);

            await m_ProcessHelper.PostCommand(new LaunchMissionCommand(), HttpStatusCode.Accepted);

            await GetLaunchedProcess(m_LaunchPadsProcessHelper.First());
        }

        [Test]
        public async Task LaunchAndStop()
        {
            await PrepareMissionControl(1, k_LaunchCatalog, k_LaunchCatalogFilesContent);

            await m_ProcessHelper.PostCommand(new LaunchMissionCommand(), HttpStatusCode.Accepted);

            var process = await GetLaunchedProcess(m_LaunchPadsProcessHelper.First());
            Assert.That(process.HasExited, Is.False);

            // While at it, try to double launch, it should fail
            await m_ProcessHelper.PostCommand(new LaunchMissionCommand(), HttpStatusCode.Conflict);

            // But we should still be able to stop it
            await m_ProcessHelper.PostCommand(new StopMissionCommand(), HttpStatusCode.Accepted);

            await m_ProcessHelper.WaitForState(State.Idle);
            Assert.That(process.HasExited, Is.True);

            // While at it, try to double stop, it should return an immediate success (nothing to stop)
            await m_ProcessHelper.PostCommand(new StopMissionCommand());
        }

        [Test]
        public async Task LaunchNonIdle()
        {
            await PrepareMissionControl(1, k_LaunchCatalog, k_LaunchCatalogFilesContent);

            await m_ProcessHelper.PostCommand(new LaunchMissionCommand(), HttpStatusCode.Accepted);

            var launchedProcess = await GetLaunchedProcess(m_LaunchPadsProcessHelper.First());

            // Kill mission control (not stop, otherwise it will stop the launch pad, we want the launchpad to stay
            // running).
            string missionControlPath = m_ProcessHelper.ConfigPath;
            m_ProcessHelper.Kill();

            await Task.Delay(100); // So that we let the time to launchedProcess to die if it has to...
            Assert.That(launchedProcess.HasExited, Is.False);

            // Start it back
            await m_ProcessHelper.Start(missionControlPath);

            await Task.Delay(100); // So that we let the time to launchedProcess to die if it has to...
            Assert.That(launchedProcess.HasExited, Is.False);

            // Launch the mission
            await m_ProcessHelper.PostCommand(new LaunchMissionCommand(), HttpStatusCode.Accepted);

            // Wait for everything to successfully launch
            await m_ProcessHelper.WaitForState(State.Launched);

            // But the old process should have been killed
            Assert.That(launchedProcess.HasExited, Is.True);
        }

        [Test]
        public async Task InvalidLaunchPadFeedbackTimeoutSec()
        {
            var config = await m_ProcessHelper.GetConfig();

            config.LaunchPadFeedbackTimeoutSec = 0;
            Assert.That((await m_ProcessHelper.PutConfigWithResponse(config)).StatusCode,
                Is.EqualTo(HttpStatusCode.BadRequest));

            config.LaunchPadFeedbackTimeoutSec = -1;
            Assert.That((await m_ProcessHelper.PutConfigWithResponse(config)).StatusCode,
                Is.EqualTo(HttpStatusCode.BadRequest));

            config.LaunchPadFeedbackTimeoutSec = 0.25f;
            await m_ProcessHelper.PutConfig(config);
        }

        [Test]
        public async Task FailedLaunchPad()
        {
            await PrepareMissionControl(2, k_LaunchCatalog, k_LaunchCatalogFilesContent);

            await m_ProcessHelper.PostCommand(new LaunchMissionCommand(), HttpStatusCode.Accepted);

            var process1 = await GetLaunchedProcess(m_LaunchPadsProcessHelper.ElementAt(0));
            Assert.That(process1.HasExited, Is.False);
            var process2 = await GetLaunchedProcess(m_LaunchPadsProcessHelper.ElementAt(1));
            Assert.That(process2.HasExited, Is.False);

            var status = await m_ProcessHelper.GetStatus();
            Assert.That(status.State, Is.EqualTo(State.Launched));

            // Terminate one of the two processes
            process2.Kill();

            // Should change the state of mission control to failed
            await m_ProcessHelper.WaitForState(State.Failure);

            // While at it, try to double launch, it should fail
            await m_ProcessHelper.PostCommand(new LaunchMissionCommand(), HttpStatusCode.Conflict);

            // Kill the other process
            process1.Kill();

            // Should restore everything to idle
            await m_ProcessHelper.WaitForState(State.Idle);

            // And now we should be able to launch again
            await m_ProcessHelper.PostCommand(new LaunchMissionCommand(), HttpStatusCode.Accepted);
            await m_ProcessHelper.WaitForState(State.Launched);
        }

        [Test]
        public async Task NoLaunchPad()
        {
            await PrepareMissionControl(1, k_LaunchCatalog, k_LaunchCatalogFilesContent);

            var complex = (await m_ProcessHelper.GetComplexes()).First();
            complex.LaunchPads.First().Identifier = Guid.NewGuid();
            await m_ProcessHelper.PutLaunchComplex(complex);

            // It reports the command as ok the sense that launching has been fully completed.  However it launched and
            // immediately finished since there was no launch pad configured...
            await m_ProcessHelper.PostCommand(new LaunchMissionCommand());

            var status = await m_ProcessHelper.GetStatus();
            Assert.That(status.State, Is.EqualTo(State.Idle));
        }

        [Test]
        public async Task FailPreparingLaunchPad()
        {
            await PrepareMissionControl(1, k_FailPreLaunchCatalog, k_FailPreLaunchCatalogFilesContent);

            await m_ProcessHelper.PostCommand(new LaunchMissionCommand(), HttpStatusCode.Accepted);

            Stopwatch stopwatch = Stopwatch.StartNew();
            bool isOver = false;
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(10) && !isOver)
            {
                var launchpadStatuses = await m_ProcessHelper.GetLaunchPadsStatus();
                Assert.That(launchpadStatuses.Count, Is.EqualTo(1));
                if (launchpadStatuses[0].IsDefined &&
                    launchpadStatuses[0].State == ClusterDisplay.MissionControl.LaunchPad.State.Over)
                {
                    isOver = true;
                }
            }
            Assert.That(isOver, Is.True);

            await m_ProcessHelper.WaitForState(State.Idle);
        }

        [Test]
        public async Task Land()
        {
            await PrepareMissionControl(1, k_LandCatalog, k_LandCatalogFilesContent);

            await m_ProcessHelper.PostCommand(new LaunchMissionCommand(), HttpStatusCode.Accepted);

            var launchPadProcessHelper = m_LaunchPadsProcessHelper.First();
            var launchedProcess = await GetLaunchedProcess(launchPadProcessHelper);

            // Initiate landing
            Assert.That(launchedProcess.HasExited, Is.False);
            var landedPath = Path.Combine(launchPadProcessHelper.LaunchFolder, "landed.txt");
            Assert.That(File.Exists(landedPath), Is.False);
            await m_ProcessHelper.PostCommand(new StopMissionCommand(), HttpStatusCode.Accepted);

            // Wait for the payload to land (graceful shutdown)
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var launchedProcessCompletedTask = launchedProcess.WaitForExitAsync();
            var awaitTask = await Task.WhenAny(launchedProcessCompletedTask, timeoutTask);
            Assert.That(awaitTask, Is.SameAs(launchedProcessCompletedTask)); // Or else timed out
            Assert.That(File.Exists(landedPath), Is.True);

            // Process not running anymore, but internal state of MissionControl might not be updated yet, wait for the
            // state to be idle ready to restart.
            await m_ProcessHelper.WaitForState(State.Idle);

            // While at it validate we can re-launch
            ClearPreviousLaunchPidTxt(launchPadProcessHelper);
            await m_ProcessHelper.PostCommand(new LaunchMissionCommand(), HttpStatusCode.Accepted);
            launchedProcess = await GetLaunchedProcess(launchPadProcessHelper);
            Assert.That(launchedProcess.HasExited, Is.False);
            Assert.That(File.Exists(landedPath), Is.False);
        }

        [Test]
        public async Task GradualLanding()
        {
            await PrepareMissionControl(2, k_ControlledLandingCatalog, k_ControlledLandingCatalogFilesContent);

            await m_ProcessHelper.PostCommand(new LaunchMissionCommand(), HttpStatusCode.Accepted);

            var launchPadProcessHelper1 = m_LaunchPadsProcessHelper.ElementAt(0);
            var launchedProcess1 = await GetLaunchedProcess(launchPadProcessHelper1);
            var landedPath1 = Path.Combine(launchPadProcessHelper1.LaunchFolder, "landed.txt");

            var launchPadProcessHelper2 = m_LaunchPadsProcessHelper.ElementAt(1);
            var launchedProcess2 = await GetLaunchedProcess(launchPadProcessHelper2);
            var landedPath2 = Path.Combine(launchPadProcessHelper2.LaunchFolder, "landed.txt");

            // Initiate landing
            await m_ProcessHelper.PostCommand(new StopMissionCommand(), HttpStatusCode.Accepted);

            // This is a special test where no process should land before first authorized
            await Task.Delay(TimeSpan.FromMilliseconds(250));
            Assert.That(launchedProcess1.HasExited, Is.False);
            Assert.That(File.Exists(landedPath1), Is.False);
            Assert.That(launchedProcess2.HasExited, Is.False);
            Assert.That(File.Exists(landedPath2), Is.False);

            // Allow launchpad1 to land
            await File.WriteAllTextAsync(Path.Combine(launchPadProcessHelper1.LaunchFolder, "landingAllowed.txt"), "Go");
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var launchedProcessCompletedTask = launchedProcess1.WaitForExitAsync();
            var awaitTask = await Task.WhenAny(launchedProcessCompletedTask, timeoutTask);
            Assert.That(awaitTask, Is.SameAs(launchedProcessCompletedTask)); // Or else timed out
            Assert.That(File.Exists(landedPath1), Is.True);

            // Stopping should trigger a failure state
            await Task.Delay(TimeSpan.FromMilliseconds(250));
            Assert.That(m_ProcessHelper.GetStatus().Result.State, Is.EqualTo(State.Launched));

            // Allow launchpad2 to land
            Assert.That(launchedProcess2.HasExited, Is.False);
            Assert.That(File.Exists(landedPath2), Is.False);
            await File.WriteAllTextAsync(Path.Combine(launchPadProcessHelper2.LaunchFolder, "landingAllowed.txt"), "Go");
            timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            launchedProcessCompletedTask = launchedProcess2.WaitForExitAsync();
            awaitTask = await Task.WhenAny(launchedProcessCompletedTask, timeoutTask);
            Assert.That(awaitTask, Is.SameAs(launchedProcessCompletedTask)); // Or else timed out
            Assert.That(File.Exists(landedPath2), Is.True);

            // Process not running anymore, but internal state of MissionControl might not be updated yet, wait for the
            // state to be idle ready to restart.
            await m_ProcessHelper.WaitForState(State.Idle);
        }

        static void ClearPreviousLaunchPidTxt(LaunchPadProcessHelper launchpadProcessHelper)
        {
            var pidTxtPath = Path.Combine(launchpadProcessHelper.LaunchFolder, "pid.txt");
            if (File.Exists(pidTxtPath))
            {
                File.Delete(pidTxtPath);
            }
        }

        static async Task<Process> GetLaunchedProcess(LaunchPadProcessHelper launchpadProcessHelper)
        {
            var pidTxtPath = Path.Combine(launchpadProcessHelper.LaunchFolder, "pid.txt");

            var elapsedTime = Stopwatch.StartNew();
            while (elapsedTime.Elapsed < TimeSpan.FromSeconds(15))
            {
                try
                {
                    var pidString = await File.ReadAllTextAsync(pidTxtPath);
                    int pid = Convert.ToInt32(pidString);
                    return Process.GetProcessById(pid);
                }
                catch
                {
                    // Just ignore, this is normal, process is not launched yet.  Take a small break and try again
                    await Task.Delay(25);
                }
            }
            Assert.Fail("No process found for launchpad");
            return Process.GetCurrentProcess(); // Should not be reached but otherwise the compiler complains...
        }

        async Task PrepareMissionControl(int nbrLaunchPads, LaunchCatalog.Catalog catalog,
            Dictionary<string, MemoryStream> filesContent)
        {
            List<Task> launchPads = new();
            for (int launchPadIdx = 0; launchPadIdx < nbrLaunchPads; ++launchPadIdx)
            {
                var processHelper = new LaunchPadProcessHelper();
                launchPads.Add(processHelper.Start(GetTestTempFolder(), 8200 + launchPadIdx));
                m_LaunchPadsProcessHelper.Add(processHelper);
            }
            await Task.WhenAll(launchPads);

            string assetUrl = await CreateAsset(GetTestTempFolder(), catalog, new(), filesContent);
            AssetPost assetPost = new()
            {
                Name = "Test asset",
                Url = assetUrl
            };
            var assetId = await m_ProcessHelper.PostAsset(assetPost);

            LaunchComplex launchComplex = new(Guid.NewGuid());
            launchComplex.LaunchPads = m_LaunchPadsProcessHelper.Select(lpph => new LaunchPad() {
                Identifier = lpph.Id,
                Endpoint = lpph.EndPoint,
                SuitableFor = new[] { "clusterNode" }
            }).ToList();
            await m_ProcessHelper.PutLaunchComplex(launchComplex);

            LaunchConfiguration launchConfiguration = new() {
                AssetId = assetId,
                LaunchComplexes = new[]
                {
                    new LaunchComplexConfiguration()
                    {
                        Identifier = launchComplex.Id,
                        LaunchPads = m_LaunchPadsProcessHelper.Select(
                            lpph => new LaunchPadConfiguration() { Identifier = lpph.Id, LaunchableName = "Cluster Node" }).ToList()
                    }
                }
            };
            await m_ProcessHelper.PutLaunchConfiguration(launchConfiguration);
        }

        static readonly LaunchCatalog.Catalog k_LaunchCatalog = new()
        {
            Payloads = new[] {
                new LaunchCatalog.Payload()
                {
                    Name = "Payload",
                    Files = new []
                    {
                        new LaunchCatalog.PayloadFile() { Path = "launch.ps1" }
                    }
                }
            },
            Launchables = new[] {
                new LaunchCatalog.Launchable()
                {
                    Name = "Cluster Node",
                    Type = "clusterNode",
                    Payloads = new [] { "Payload" },
                    LaunchPath = "launch.ps1"
                }
            }
        };
        static readonly Dictionary<string, MemoryStream> k_LaunchCatalogFilesContent = new() {
            { "launch.ps1", MemoryStreamFromString(
                "$pid | Out-File \"pid.txt\"    \n" +
                "while ( $true )                \n" +
                "{                              \n" +
                "    Start-Sleep -Seconds 60    \n" +
                "}") }
        };

        static readonly LaunchCatalog.Catalog k_FailPreLaunchCatalog = new()
        {
            Payloads = new[] {
                new LaunchCatalog.Payload()
                {
                    Name = "Payload",
                    Files = new []
                    {
                        new LaunchCatalog.PayloadFile() { Path = "prelaunch.ps1" },
                        new LaunchCatalog.PayloadFile() { Path = "launch.ps1" }
                    }
                }
            },
            Launchables = new[] {
                new LaunchCatalog.Launchable()
                {
                    Name = "Cluster Node",
                    Type = "clusterNode",
                    Payloads = new [] { "Payload" },
                    PreLaunchPath = "prelaunch.ps1",
                    LaunchPath = "launch.ps1"
                }
            }
        };
        static readonly Dictionary<string, MemoryStream> k_FailPreLaunchCatalogFilesContent = new() {
            { "launch.ps1", MemoryStreamFromString(
                "$pid | Out-File \"pid.txt\"    \n" +
                "while ( $true )                \n" +
                "{                              \n" +
                "    Start-Sleep -Seconds 60    \n" +
                "}") },
            { "prelaunch.ps1", MemoryStreamFromString("exit 1") }
        };

        static readonly LaunchCatalog.Catalog k_LandCatalog = new()
        {
            Payloads = new[] {
                new LaunchCatalog.Payload()
                {
                    Name = "Payload",
                    Files = new []
                    {
                        new LaunchCatalog.PayloadFile() { Path = "launch.ps1" }
                    }
                }
            },
            Launchables = new[] {
                new LaunchCatalog.Launchable()
                {
                    Name = "Cluster Node",
                    Type = "clusterNode",
                    Payloads = new [] { "Payload" },
                    LaunchPath = "launch.ps1",
                    LandingTimeSec = 2.5f
                }
            }
        };
        static readonly Dictionary<string, MemoryStream> k_LandCatalogFilesContent = new() {
            { "launch.ps1", MemoryStreamFromString(
                "$missionControlUri = $env:MISSIONCONTROL_ENTRY             \n" +
                "$pid | Out-File \"pid.txt\"                                \n" +
                "$proceedWithLanding = $false                               \n" +
                "while (-not $proceedWithLanding)                           \n" +
                "{                                                          \n" +
                "    $uplink = Invoke-RestMethod -Uri \"$($missionControlUri)api/v1/capcomUplink\"\n" +
                "    $proceedWithLanding = $uplink.proceedWithLanding       \n" +
                "    if (-not $proceedWithLanding)                          \n" +
                "    {                                                      \n" +
                "        Start-Sleep -Milliseconds 5                        \n" +
                "    }                                                      \n" +
                "}                                                          \n" +
                "Out-File \"landed.txt\" -InputObject \"Over\"") }
        };

        static readonly LaunchCatalog.Catalog k_ControlledLandingCatalog = new()
        {
            Payloads = new[] {
                new LaunchCatalog.Payload()
                {
                    Name = "Payload",
                    Files = new []
                    {
                        new LaunchCatalog.PayloadFile() { Path = "launch.ps1" }
                    }
                }
            },
            Launchables = new[] {
                new LaunchCatalog.Launchable()
                {
                    Name = "Cluster Node",
                    Type = "clusterNode",
                    Payloads = new [] { "Payload" },
                    LaunchPath = "launch.ps1",
                    LandingTimeSec = 10.0f
                }
            }
        };
        static readonly Dictionary<string, MemoryStream> k_ControlledLandingCatalogFilesContent = new() {
            { "launch.ps1", MemoryStreamFromString(
                "$missionControlUri = $env:MISSIONCONTROL_ENTRY                    \n" +
                "$pid | Out-File \"pid.txt\"                                       \n" +
                "$waitToCheckLanding = $true                                       \n" +
                "while ($waitToCheckLanding)                                       \n" +
                "{                                                                 \n" +
                "    $waitToCheckLanding = -not (Test-Path \"landingAllowed.txt\") \n" +
                "    if ($waitToCheckLanding)                                      \n" +
                "    {                                                             \n" +
                "        Start-Sleep -Milliseconds 5                               \n" +
                "    }                                                             \n" +
                "}                                                                 \n" +
                "$proceedWithLanding = $false                                      \n" +
                "while (-not $proceedWithLanding)                                  \n" +
                "{                                                                 \n" +
                "    $uplink = Invoke-RestMethod -Uri \"$($missionControlUri)api/v1/capcomUplink\"\n" +
                "    $proceedWithLanding = $uplink.proceedWithLanding              \n" +
                "    if (-not $proceedWithLanding)                                 \n" +
                "    {                                                             \n" +
                "        Start-Sleep -Milliseconds 5                               \n" +
                "    }                                                             \n" +
                "}                                                                 \n" +
                "Out-File \"landed.txt\" -InputObject \"Over\"") }
        };

        string GetTestTempFolder()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), "LaunchStopMissionControllerTests_" + Guid.NewGuid().ToString());
            m_TestTempFolders.Add(folderPath);
            return folderPath;
        }

        HangarBayProcessHelper m_HangarBayProcessHelper = new();
        List<LaunchPadProcessHelper> m_LaunchPadsProcessHelper = new();
        MissionControlProcessHelper m_ProcessHelper = new();
        List<string> m_TestTempFolders = new();
    }
}
