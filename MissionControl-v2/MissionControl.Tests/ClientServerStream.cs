using System;
using System.IO.Pipes;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Tests
{
    class ClientServerStream: IDisposable
    {
        public ClientServerStream()
        {
            AnonymousPipeServerStream writeStream = new();
            WriteStream = writeStream;
            ReadStream = new AnonymousPipeClientStream(PipeDirection.In, writeStream.ClientSafePipeHandle);
        }

        public Stream WriteStream { get; }
        public Stream ReadStream { get; }

        public void Dispose()
        {
            ReadStream.Dispose();
            WriteStream.Dispose();
        }
    }
}
