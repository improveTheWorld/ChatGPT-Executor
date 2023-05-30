using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Topshelf;
using System.IO;

namespace ChatGPTExecutor
{

    internal class WebSocketService
    {
        private CommandManager cmdManager;

        public WebSocketService()
        {
            this.cmdManager = new CommandManager();
        }

        public void Start()
        {
            this.cmdManager.Start();
        }

        public void Stop()
        {
            this.cmdManager.Stop();
        }

    }

    public class ChatGPTExecutorService
    {
        static void Main()
        {
            HostFactory.Run(x =>
            {
                x.Service<WebSocketService>(s =>
                {
                    s.ConstructUsing(name => new WebSocketService());
                    s.WhenStarted(tc => tc.Start());
                    s.WhenStopped(tc => tc.Stop());
                });
                x.RunAsLocalSystem();

                x.SetDescription("Ce service gère une connexion WebSocket pour exécuter des commandes windows");
                x.SetDisplayName("ChatGPT-Executor");
                x.SetServiceName("ChatGPT-Executor");
            });
        }
    }
}
