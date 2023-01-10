using System;

namespace Unity.ClusterDisplay.MissionControl
{
    public class TaskGroupTests
    {
        [Test]
        public async Task AllSucceed()
        {
            TaskGroup taskGroup = new();

            bool task1Completed = false;
            async Task Task1Func() {
                await Task.Delay(50, taskGroup.CancellationToken);
                task1Completed = true;
            }
            var task1 = Task1Func();
            taskGroup.Add(task1);

            bool task2Completed = false;
            async Task Task2Func() {
                await Task.Delay(100, taskGroup.CancellationToken);
                task2Completed = true;
            }
            var task2 = Task2Func();
            taskGroup.Add(task2);

            await Task.WhenAll(taskGroup.ToWaitOn);
            Assert.That(task1.IsCompleted, Is.True);
            Assert.That(task1Completed, Is.True);
            Assert.That(task2.IsCompleted, Is.True);
            Assert.That(task2Completed, Is.True);
        }

        class MyCustomException: Exception { }

        [Test]
        public void OneFailCancelOthers()
        {
            TaskGroup taskGroup = new();

            bool task1Completed = false;
            async Task Task1Func() {
                await Task.Delay(50, taskGroup.CancellationToken);
                task1Completed = true;
            }
            var task1 = Task1Func();
            taskGroup.Add(task1);

            bool task2Completed = false;
            async Task Task2Func() {
                await Task.Delay(100, taskGroup.CancellationToken);
                throw new MyCustomException();
#pragma warning disable CS0162 // Unreachable code detected
                task2Completed = true;
#pragma warning restore CS0162 // Unreachable code detected
            }
            var task2 = Task2Func();
            taskGroup.Add(task2);

            bool task3Completed = false;
            async Task Task3Func() {
                await Task.Delay(5000, taskGroup.CancellationToken);
                task3Completed = true;
            }
            var task3 = Task3Func();
            taskGroup.Add(task3);

            Assert.That(async () => await Task.WhenAll(taskGroup.ToWaitOn), Throws.TypeOf<MyCustomException>());
            Assert.That(task1.IsCompleted, Is.True);
            Assert.That(task1Completed, Is.True);
            Assert.That(task2.IsCompleted, Is.True);
            Assert.That(task2.IsFaulted, Is.True);
            Assert.That(task2Completed, Is.False);
            Assert.That(task3.IsCompleted, Is.True);
            Assert.That(task3.IsCanceled, Is.True);
            Assert.That(task3Completed, Is.False);
        }

        [Test]
        public async Task ForceCancel()
        {
            TaskGroup taskGroup = new();

            bool task1Completed = false;
            async Task Task1Func() {
                await Task.Delay(50, taskGroup.CancellationToken);
                task1Completed = true;
            }
            var task1 = Task1Func();
            taskGroup.Add(task1);

            bool task2Completed = false;
            async Task Task2Func() {
                await Task.Delay(5000, taskGroup.CancellationToken);
                task2Completed = true;
            }
            var task2 = Task2Func();
            taskGroup.Add(task2);

            await task1;
            taskGroup.Cancel();

            Assert.That(async () => await Task.WhenAll(taskGroup.ToWaitOn), Throws.TypeOf<TaskCanceledException>());
            Assert.That(task1.IsCompleted, Is.True);
            Assert.That(task1Completed, Is.True);
            Assert.That(task2.IsCompleted, Is.True);
            Assert.That(task2.IsCanceled, Is.True);
            Assert.That(task2Completed, Is.False);
        }
    }
}
