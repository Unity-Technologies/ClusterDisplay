using System.Threading.Tasks;
using NUnit.Framework;

namespace Unity.ClusterDisplay.MissionControl
{
    public class AsyncConditionVariableValueTaskTests
    {
        [Test]
        public void SingleAwait()
        {
            var condition = new AsyncConditionVariableValueTask();

            bool afterAwaitOnSignaledTask = false;
            var awaitingTask = Task.Run(async () => {
                await condition.SignaledValueTask;
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
        public void SingleWithoutWait()
        {
            var condition = new AsyncConditionVariableValueTask();

            condition.Signal();
            condition.Signal();
            condition.Signal();

            bool afterAwaitOnSignaledTask = false;
            var awaitingTask = Task.Run(async () => {
                await condition.SignaledValueTask;
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
            var condition = new AsyncConditionVariableValueTask();

            bool afterAwaitOnSignaledTask = false;
            var awaitingTask = Task.Run(async () => {
                await condition.SignaledValueTask;
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
            var condition = new AsyncConditionVariableValueTask();

            bool afterAwaitOnSignaledTask = false;
            var awaitingTask = Task.Run(async () => {
                await condition.SignaledValueTask;
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
            var condition = new AsyncConditionVariableValueTask();

            condition.Cancel();

            var canceledTask = condition.SignaledValueTask;
            Assert.That(canceledTask.IsCompleted, Is.True);
            Assert.That(canceledTask.IsCanceled, Is.True);
        }

        [Test]
        public void SignalAfterCancel()
        {
            var condition = new AsyncConditionVariableValueTask();

            condition.Cancel();

            var canceledTask = condition.SignaledValueTask;
            Assert.That(canceledTask.IsCompleted, Is.True);
            Assert.That(canceledTask.IsCanceled, Is.True);

            condition.Signal();

            var newCanceledTask = condition.SignaledValueTask;
            Assert.That(newCanceledTask.IsCompleted, Is.True);
            Assert.That(newCanceledTask.IsCanceled, Is.True);
        }
    }
}
