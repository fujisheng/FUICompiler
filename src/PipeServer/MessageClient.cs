using System.IO.Pipes;

namespace FUICompiler
{
    internal class MessageClient : IDisposable
    {
        const string PipeName = "FUICompilerMessage";
        const int BufferSize = 1024;
        readonly NamedPipeClientStream pipeClient;
        readonly Queue<byte[]> sendQueue = new Queue<byte[]>();
        readonly CancellationTokenSource cts = new CancellationTokenSource();

        public event Action<byte[]> MessageReceived;
        public event Action Connected;
        public event Action Disconnected;

        static MessageClient instance;

        public static MessageClient Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new MessageClient();
                }

                return instance;
            }
        }

        MessageClient()
        {
            pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        }

        public async Task ConnectAsync(int timeoutMs = 5000)
        {
            if (IsConnected)
            {
                return;
            }

            try
            {
                await pipeClient.ConnectAsync(timeoutMs, cts.Token);
                Connected?.Invoke();

                // 启动读写任务
                _ = ReadMessagesAsync();
                _ = ProcessSendQueueAsync();
            }
            catch (Exception)
            {
                throw;
            }
        }

        public void Send(byte[] bytes)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("未连接到服务器");
            }

            lock (sendQueue)
            {
                sendQueue.Enqueue(bytes);
            }
        }

        async Task ReadMessagesAsync()
        {
            var buffer = new byte[BufferSize];

            try
            {
                while (IsConnected && !cts.Token.IsCancellationRequested)
                {
                    var message = new List<byte>();
                    do
                    {
                        var bytesRead = await pipeClient.ReadAsync(buffer, 0, buffer.Length, cts.Token);
                        if (bytesRead == 0)
                        {
                            break;
                        }

                        for (int i = 0; i < bytesRead; i++)
                        {
                            message.Add(buffer[i]);
                        }
                    }
                    while (!pipeClient.IsMessageComplete);

                    MessageReceived?.Invoke(message.ToArray());
                }
            }
            catch (IOException)
            {
                // 服务端断开连接
                HandleDisconnection();
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
        }

        async Task ProcessSendQueueAsync()
        {
            try
            {
                while (IsConnected && !cts.Token.IsCancellationRequested)
                {
                    byte[] message = null;

                    lock (sendQueue)
                    {
                        if (sendQueue.Count > 0)
                        {
                            message = sendQueue.Dequeue();
                        }
                    }

                    if (message != null)
                    {
                        await pipeClient.WriteAsync(message, 0, message.Length, cts.Token);
                        await pipeClient.FlushAsync(cts.Token);
                    }
                    else
                    {
                        await Task.Delay(100, cts.Token); // 避免空轮询
                    }
                }
            }
            catch (IOException)
            {
                // 服务端断开连接
                HandleDisconnection();
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
        }

        void HandleDisconnection()
        {
            if (!IsConnected)
            {
                return;
            }

            Disconnected?.Invoke();
        }

        public void Disconnect()
        {
            if (!IsConnected)
            {
                return;
            }

            cts.Cancel();
            pipeClient.Close();
            Disconnected?.Invoke();
        }

        public void Dispose()
        {
            Disconnect();
            cts.Dispose();
            pipeClient.Dispose();
        }

        public bool IsConnected => pipeClient.IsConnected;
    }
}