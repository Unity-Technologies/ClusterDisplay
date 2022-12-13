using System;
// ReSharper disable once RedundantUsingDirective
using System.Collections;
// ReSharper disable once RedundantUsingDirective
using System.Threading.Tasks;
// ReSharper disable once RedundantUsingDirective
using NUnit.Framework;

namespace Unity.ClusterDisplay.MissionControl
{
    public class AsyncConditionVariableTests
    {
        [Test]
        public void SingleAwait()
        {
            var condition = new AsyncConditionVariable();

            bool afterAwaitOnSignaledTask = false;
            var awaitingTask = Task.Run(async () => {
                await condition.SignaledTask;
                afterAwaitOnSignaledTask = true;
            });

            Task.Delay(100).Wait();

            Assert.That(awaitingTask.IsCompleted, Is.False);
            Assert.That(afterAwaitOnSignaledTask, Is.False);

            condition.Signal();

            awaitingTask.Wait();
            Assert.That(awaitingTask.IsCompleted, Is.True);
            Assert.That(afterAwaitOnSignaledTask, Is.True);
        }

        [Test]
        public void MultipleAwait()
        {
            var condition = new AsyncConditionVariable();

            bool afterAwaitOnSignaledTask1 = false;
            var awaitingTask1 = Task.Run(async () => {
                await condition.SignaledTask;
                afterAwaitOnSignaledTask1 = true;
            });
            bool afterAwaitOnSignaledTask2 = false;
            var awaitingTask2 = Task.Run(async () => {
                await condition.SignaledTask;
                afterAwaitOnSignaledTask2 = true;
            });

            Task.Delay(100).Wait();

            Assert.That(awaitingTask1.IsCompleted, Is.False);
            Assert.That(afterAwaitOnSignaledTask1, Is.False);
            Assert.That(awaitingTask2.IsCompleted, Is.False);
            Assert.That(afterAwaitOnSignaledTask2, Is.False);

            condition.Signal();

            awaitingTask1.Wait();
            Assert.That(awaitingTask1.IsCompleted, Is.True);
            Assert.That(afterAwaitOnSignaledTask1, Is.True);
            awaitingTask2.Wait();
            Assert.That(awaitingTask2.IsCompleted, Is.True);
            Assert.That(afterAwaitOnSignaledTask2, Is.True);
        }

        [Test]
        public void SingleWithoutWait()
        {
            var condition = new AsyncConditionVariable();

            condition.Signal();
            condition.Signal();
            condition.Signal();

            bool afterAwaitOnSignaledTask = false;
            var awaitingTask = Task.Run(async () => {
                await condition.SignaledTask;
                afterAwaitOnSignaledTask = true;
            });

            Task.Delay(100).Wait();

            Assert.That(awaitingTask.IsCompleted, Is.False);
            Assert.That(afterAwaitOnSignaledTask, Is.False);

            condition.Signal();

            awaitingTask.Wait();
            Assert.That(awaitingTask.IsCompleted, Is.True);
            Assert.That(afterAwaitOnSignaledTask, Is.True);
        }

        [Test]
        public void MultipleSignal()
        {
            var condition = new AsyncConditionVariable();

            bool afterAwaitOnSignaledTask = false;
            var awaitingTask = Task.Run(async () => {
                await condition.SignaledTask;
                afterAwaitOnSignaledTask = true;
            });

            Task.Delay(100).Wait();

            Assert.That(awaitingTask.IsCompleted, Is.False);
            Assert.That(afterAwaitOnSignaledTask, Is.False);

            condition.Signal();
            condition.Signal();
            condition.Signal();

            awaitingTask.Wait();
            Assert.That(awaitingTask.IsCompleted, Is.True);
            Assert.That(afterAwaitOnSignaledTask, Is.True);
        }

        [Test]
        public void Cancel()
        {
            var condition = new AsyncConditionVariable();

            bool afterAwaitOnSignaledTask = false;
            var awaitingTask = Task.Run(async () => {
                await condition.SignaledTask;
                afterAwaitOnSignaledTask = true;
            });

            Task.Delay(100).Wait();

            Assert.That(awaitingTask.IsCompleted, Is.False);
            Assert.That(afterAwaitOnSignaledTask, Is.False);

            condition.Cancel();

            Task.WhenAny(awaitingTask).Wait(); // WhenAny so that await does not throw
            Assert.That(awaitingTask.IsCompleted, Is.True);
            Assert.That(awaitingTask.IsCanceled, Is.True);
            Assert.That(afterAwaitOnSignaledTask, Is.False);
        }

        [Test]
        public void CancelBeforeAskingForTask()
        {
            var condition = new AsyncConditionVariable();

            condition.Cancel();

            var canceledTask = condition.SignaledTask;
            Assert.That(canceledTask.IsCompleted, Is.True);
            Assert.That(canceledTask.IsCanceled, Is.True);
        }

        [Test]
        public void SignalAfterCancel()
        {
            var condition = new AsyncConditionVariable();

            condition.Cancel();

            var canceledTask = condition.SignaledTask;
            Assert.That(canceledTask.IsCompleted, Is.True);
            Assert.That(canceledTask.IsCanceled, Is.True);

            condition.Signal();
            var newCanceledTask = condition.SignaledTask;
            Assert.That(newCanceledTask, Is.SameAs(canceledTask));
            Assert.That(newCanceledTask.IsCompleted, Is.True);
            Assert.That(newCanceledTask.IsCanceled, Is.True);
        }
    }
}
