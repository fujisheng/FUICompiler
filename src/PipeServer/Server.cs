using System.IO.Pipes;
using System.Security.Principal;

namespace FUICompiler
{
    internal class Server
    {
        void Start()
        {
            // 服务端
            using (NamedPipeServerStream pipeServer = new NamedPipeServerStream("testpipe", PipeDirection.InOut, 1))
            {
                Console.WriteLine("Named pipe server is running...");
                pipeServer.WaitForConnection();
                using (StreamWriter sw = new StreamWriter(pipeServer))
                {
                    sw.WriteLine("Hello, client!");
                }
            }

            // 客户端
            using (NamedPipeClientStream pipeClient =  new NamedPipeClientStream(".", "testpipe", PipeDirection.InOut, PipeOptions.None, TokenImpersonationLevel.None))
            {
                pipeClient.Connect();
                using (StreamReader sr = new StreamReader(pipeClient))
                {
                    Console.WriteLine(sr.ReadToEnd());
                }
            }
        }
    }
}
