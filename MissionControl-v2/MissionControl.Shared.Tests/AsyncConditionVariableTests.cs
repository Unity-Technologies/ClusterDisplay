using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unity.ClusterDisplay.MissionControl
{
    public class AsyncConditionVariableTests
    {
        [Test]
        public async Task SingleAwait()
        {
            var condition = new AsyncConditionVariable();

            bool afterAwaitOnSignaledTask = false;
            var awaitingTask = Task.Run(async () => {
                await condition.SignaledTask;
                afterAwaitOnSignaledTask = true;
            });

            await Task.Delay(100);

            Assert.That(awaitingTask.IsCompleted, Is.False);
            Assert.That(afterAwaitOnSignaledTask, Is.False);

            condition.Signal();

            await awaitingTask;
            Assert.That(awaitingTask.IsCompleted, Is.True);
            Assert.That(afterAwaitOnSignaledTask, Is.True);
        }

        [Test]
        public async Task MultipleAwait()
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

            await Task.Delay(100);

            Assert.That(awaitingTask1.IsCompleted, Is.False);
            Assert.That(afterAwaitOnSignaledTask1, Is.False);
            Assert.That(awaitingTask2.IsCompleted, Is.False);
            Assert.That(afterAwaitOnSignaledTask2, Is.False);

            condition.Signal();

            await awaitingTask1;
            Assert.That(awaitingTask1.IsCompleted, Is.True);
            Assert.That(afterAwaitOnSignaledTask1, Is.True);
            await awaitingTask2;
            Assert.That(awaitingTask2.IsCompleted, Is.True);
            Assert.That(afterAwaitOnSignaledTask2, Is.True);
        }

        [Test]
        public async Task SingleWithoutWait()
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

            await Task.Delay(100);

            Assert.That(awaitingTask.IsCompleted, Is.False);
            Assert.That(afterAwaitOnSignaledTask, Is.False);

            condition.Signal();

            await awaitingTask;
            Assert.That(awaitingTask.IsCompleted, Is.True);
            Assert.That(afterAwaitOnSignaledTask, Is.True);
        }

        [Test]
        public async Task MultipleSignal()
        {
            var condition = new AsyncConditionVariable();

            bool afterAwaitOnSignaledTask = false;
            var awaitingTask = Task.Run(async () => {
                await condition.SignaledTask;
                afterAwaitOnSignaledTask = true;
            });

            await Task.Delay(100);

            Assert.That(awaitingTask.IsCompleted, Is.False);
            Assert.That(afterAwaitOnSignaledTask, Is.False);

            condition.Signal();
            condition.Signal();
            condition.Signal();

            await awaitingTask;
            Assert.That(awaitingTask.IsCompleted, Is.True);
            Assert.That(afterAwaitOnSignaledTask, Is.True);
        }

        [Test]
        public async Task Cancel()
        {
            var condition = new AsyncConditionVariable();

            bool afterAwaitOnSignaledTask = false;
            var awaitingTask = Task.Run(async () => {
                await condition.SignaledTask;
                afterAwaitOnSignaledTask = true;
            });

            await Task.Delay(100);

            Assert.That(awaitingTask.IsCompleted, Is.False);
            Assert.That(afterAwaitOnSignaledTask, Is.False);

            condition.Cancel();

            await Task.WhenAny(awaitingTask); // WhenAny so that await does not throw
            Assert.That(awaitingTask.IsCompleted, Is.True);
            Assert.That(awaitingTask.IsCanceled, Is.True);
            Assert.That(afterAwaitOnSignaledTask, Is.False);
        }
    }
}
