using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Fleck;
using System.Timers;
using System.Security.Cryptography;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.File;
using Serilog.Formatting.Compact;
using System.Data;
using Timer = System.Threading.Timer;
using System.Text.Json;
using System.Reflection.PortableExecutable;
using System.Globalization;

namespace ChatGPTExecutor
{

    class CommandManager
    {
        private const int maxWords = 2400;
        private const int MaxMessageSize = 4000;
        private static List<string> outputParts = new List<string>();
        private static int currentPartIndex = 0;

        private static string Header = string.Empty ;
        private static string Tailor = string.Empty;
        private static string AskForNext = string.Empty;
        private static string receptionBuffer = ""; 

        private static bool firstOutputLine = true;
        private static List<string> pendingCommands = new List<string>();

        private static Process cmdProcess;
        private static StreamWriter cmdStreamWriter;
        private static StringBuilder normalOutput = new StringBuilder();
        private static StringBuilder errorOutput = new StringBuilder();
        private static ManualResetEvent outputEndEvent = new ManualResetEvent(false);
        private static ManualResetEvent questionAskedEvent = new ManualResetEvent(false);
        private static ManualResetEvent answerProvidedEvent = new ManualResetEvent(false);
        private static System.Timers.Timer outputTimeoutTimer;
        private static string firstPrompt;

        private static WebSocketServer server;
        private static CancellationTokenSource cancellationTokenSource= new CancellationTokenSource();
        private static  Task serviceTask;
        private static  Dictionary<string, string> authSentences = new Dictionary<string, string>();
        private static string funKey;
        private static bool acceptNewClients = false;
        private static bool alreadyHadClient = true;
        private const string ConfigFilePath = "Config.json";
        private static string FirstPromptFilePath;



        private void initfunKey()
        {
            funKey = loadFunKey();
            if (funKey == string.Empty)
            {
                Log.Information("Creating new key ...");
                funKey = GenerateRandomToken();
                savefunKey(funKey);
                acceptNewClients = true;
                alreadyHadClient = false;
                new Timer( (_) =>
                {
                    acceptNewClients = false;
                },
                null,
                600000,
                Timeout.Infinite
                );
            }
        }
        private void savefunKey(string funKey)
        {
            StreamWriter  output  = new StreamWriter("Stat");

            string key =  GenerateRandomToken();
            output.Write(key + EncryptStringAES(Convert.ToBase64String(Encoding.UTF8.GetBytes(funKey)),key));
            output.Flush();
            output.Close();
        }

        string  loadFunKey()
        {
            string filePath = "Stat";
            string text = "";

            if (File.Exists(filePath))
            {
                using (StreamReader input = new StreamReader(filePath))
                {
                    text = input.ReadToEnd();
                }
            }

            if (text.Length > 44)
            {
                return DecryptStringAES(text.Substring(44), text.Substring(0, 44));
            }
            else
            {
                return string.Empty;
            }           
  
        }


        static void resetCommunicationBuffers()
        {
            outputParts.Clear();
            currentPartIndex = 0;
            receptionBuffer = string.Empty;
            pendingCommands.Clear();
        }

       
        public void Stop()
        {
            Log.Information("Shutting down...");

            // Stop the cmd process
            if (!cmdProcess.HasExited)
            {
                cmdProcess.Kill();
            }

            // Dispose the WebSocket server
            cancellationTokenSource.Cancel();
            server.Dispose();
            serviceTask.Wait();

            Log.Information("WebSocket server stopped.");
        }
        
        public void Start()
        {
            serviceTask = Task.Run(() => StartServiceTask(), cancellationTokenSource.Token);
        }

        public static string GenerateRandomToken(int keySize = 256)
        {
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = keySize;
                aes.GenerateKey();
                return Convert.ToBase64String(aes.Key);
            }
        }


        public static (string firstPromptFilePAth, string header, string tail, string askForNext, string wsPort) InitProtocolConfig()
        {
            string header = string.Empty;
            string tail = string.Empty;
            string askForNext = string.Empty;
            string firstPromptFilePath = string.Empty;
            string wsPort = string.Empty;

            if (File.Exists(ConfigFilePath))
            {
                string jsonContent = File.ReadAllText(ConfigFilePath);

                Dictionary<string, string> config = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonContent);

                if (config.ContainsKey("Header") && config.ContainsKey("Tailor") && config.ContainsKey("AskForNext") && config.ContainsKey("WsPort") )
                {
                    header = config["Header"];
                    tail = config["Tailor"];
                    askForNext = config["AskForNext"];
                    wsPort = config["WsPort"];

                    if(config.ContainsKey("FirstPromptFilePath"))
                    {
                        firstPromptFilePath = config["FirstPromptFilePath"];
                    }
                                 
                }
                else
                {
                    Log.Error("Invalid config file. Missing 'Header' or 'Tailor' key.");
                }
            }
            else
            {
                Log.Error("Config file not found.");
            }

            if( string.IsNullOrEmpty(header) || string.IsNullOrEmpty(tail) || string.IsNullOrEmpty(askForNext) ||  string.IsNullOrEmpty(wsPort))           {
                throw new Exception("Bad Protocol Config file");
            }

            return (firstPromptFilePath, header, tail, askForNext, wsPort);
        }



        public static string EncryptStringAES(string b64Text, string b64Key)
        {
            byte[] keyBytes = Convert.FromBase64String(b64Key);
            byte[] plainBytes = Convert.FromBase64String(b64Text);

            using (Aes aes = Aes.Create())
            {
                aes.Key = keyBytes;
                aes.GenerateIV();  // Generate a new IV for each encryption
                aes.Padding = PaddingMode.PKCS7; // Set the padding mode to PKCS7

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        cs.Write(plainBytes, 0, plainBytes.Length);
                        cs.FlushFinalBlock();

                        // Prepend the IV to the cipherText, so it's available for decryption
                        byte[] cipherBytes = new byte[aes.IV.Length + ms.Length];
                        Array.Copy(aes.IV, 0, cipherBytes, 0, aes.IV.Length);
                        Array.Copy(ms.ToArray(), 0, cipherBytes, aes.IV.Length, ms.Length);
                        var b64encrypted = Convert.ToBase64String(cipherBytes);
                        return b64encrypted;
                    }
                }
            }
        }

        public static string DecryptStringAES(string b64CipherText, string b64Key)
        {
            byte[] keyBytes = Convert.FromBase64String(b64Key);
            byte[] encryptedBytes = Convert.FromBase64String(b64CipherText);
            byte[] iv = new byte[16];
            Array.Copy(encryptedBytes, 0, iv, 0, iv.Length);
            byte[] cipherBytes = new byte[encryptedBytes.Length - iv.Length];
            Array.Copy(encryptedBytes, iv.Length, cipherBytes, 0, cipherBytes.Length);

            using (Aes aes = Aes.Create())
            {
                aes.Key = keyBytes;
                aes.IV = iv;
                aes.Padding = PaddingMode.PKCS7; // Set the padding mode to PKCS7

                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                using (MemoryStream ms = new MemoryStream(cipherBytes))
                using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (StreamReader reader = new StreamReader(cs))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        public static string ComputeSha256Hash(string rawData)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        public void connect(WebSocketServer server)
        {
            server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    var cookies = socket.ConnectionInfo.Cookies;
                    Log.Information($"Socket {socket.ConnectionInfo.Id}: Connection Attempt, Authentificate...");

                    if (!cookies.ContainsKey("authenticated"))
                    {
                        cookies["authenticated"] = "False";
                    }

                    if(acceptNewClients || !alreadyHadClient)
                    {
                        Log.Information(" New client registred");
                        socket.Send("KEY:"+ funKey);
                        alreadyHadClient = true;
                        cookies["authenticated"] = "True";
                    }
                    else if (cookies["authenticated"] == "False")
                    {
                        Log.Information($"Socket {socket.ConnectionInfo.Id} : Authetification question sent.");
                        var token = GenerateRandomToken();
                        cookies["token"] = ComputeSha256Hash(token);
                        socket.Send(token);
                    }
                };


                socket.OnClose = () =>
                {
                    Log.Information($"Socket {socket.ConnectionInfo.Id} :Connection closed.");

                    // Reconnect when the connection is closed
                    if (!cancellationTokenSource.IsCancellationRequested)
                    {
                        Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(_ => connect(server));
                    }
                };
                socket.OnMessage = message =>
                {
                    var cookies = socket.ConnectionInfo.Cookies;

                    if (cookies["authenticated"] == "False")
                    {
                        if (DecryptStringAES(message, funKey) == cookies["token"])
                        {
                            cookies["authenticated"] = "True";
                            Log.Information($"Socket {socket.ConnectionInfo.Id} : Correct Authentification answer. authenticated.");
                        }
                        else
                        {
                            Log.Error($"Socket {socket.ConnectionInfo.Id} :Wrong Authentification answer, Failed.");
                        }

                    }

                    if (cookies["authenticated"] == "True")
                    {
                        if (message == "_STOP_")
                        {
                            resetCommunicationBuffers();
                            return;
                        }
                        else if (message == "_START_NEW")
                        {
                            resetCommunicationBuffers();
                            socket.Send(firstPrompt);
                            Log.Information("First Prompt sent");
                            return;
                        }
                        else
                        {
                            Log.Information("New message received");
                            ExtractWindowsCommands(message);


                            if (receptionBuffer != string.Empty)
                            {
                                Log.Information($"Last Command Uncomplete. Continue");
                                socket.Send($"{Header} The previous command was incomplete. Please do not repeat any words that I have already received. Instead, continue from where you left off. The last line I received was '{GetLastLine(receptionBuffer)}'.  {Tailor}");
                                return;
                            }

                            if (pendingCommands.Count != 0)
                            {
                                bool multiCommands = false;
                                if (pendingCommands.Count > 1)
                                {
                                    multiCommands = true;
                                }

                                StringBuilder concatenatedOutput = new StringBuilder();
                                int commandIndex = 0;
                                foreach (string command in pendingCommands)
                                {
                                    if (command == "NEXT")
                                    {
                                        Console.Write("Will sent NEXT Part:");
                                    }
                                    else
                                    {
                                        //reset output parts list
                                        outputParts.Clear();
                                        currentPartIndex = 0;

                                        commandIndex++;
                                        string commandOutput = ExecuteCommand(command);
                                        if (multiCommands && commandOutput.Trim() != string.Empty)
                                        {
                                            concatenatedOutput.Append($"## Command {commandIndex} Feedback \r\n");
                                            concatenatedOutput.Append(commandOutput);
                                            concatenatedOutput.Append($"## End command {commandIndex} Feedback \r\n");
                                        }
                                        else
                                        {
                                            concatenatedOutput.Append(commandOutput);
                                        }
                                    }
                                }
                                pendingCommands.Clear();


                                processOutput(concatenatedOutput.ToString());
                                SendNextPart(socket);
                            }

                        }
                    };
                };

            });

        }
        public void StartServiceTask()
        {
            bool isNewInstance;

            string akey = "0123456789123456";
            string atext = "hello world !";


            var b64key = Convert.ToBase64String(Encoding.UTF8.GetBytes(akey));
            var b64text = Convert.ToBase64String(Encoding.UTF8.GetBytes(atext));

            string b64encrypted = EncryptStringAES(b64text, b64key);
            string decripted = DecryptStringAES(b64encrypted, b64key);
            //  status : decripted==  "hello world !"

            using (Mutex mutex = new Mutex(true, "ChatGPTExecutor", out isNewInstance))
            {
                if (isNewInstance)
                {



                    Log.Logger = new LoggerConfiguration()
                        .WriteTo.Console()
                        .WriteTo.File(
                            new CompactJsonFormatter(),
                            "logs/log-.txt",
                            rollingInterval: RollingInterval.Day,
                            fileSizeLimitBytes: 1_000_000_000,
                            rollOnFileSizeLimit: true,
                            retainedFileCountLimit: null,
                            shared: true,
                            flushToDiskInterval: TimeSpan.FromSeconds(1))
                        .CreateLogger();

                    Log.Information("Logging initialized.............");



                    initfunKey();
                    // Initialize cmd process
                    InitializeCmdProcess();
                    string wsPort = string.Empty;

                    (FirstPromptFilePath ,Header, Tailor, AskForNext, wsPort) = InitProtocolConfig();

                    if(File.Exists(FirstPromptFilePath))
                    {
                        firstPrompt = File.ReadAllText(FirstPromptFilePath);
                    }
                                      
                    server = new WebSocketServer($"ws://127.0.0.1:{wsPort}");
                    cancellationTokenSource = new CancellationTokenSource();
                    connect(server);


                    Log.Information("WebSocket server started. Press CTRL+C to exit...");
                    Console.CancelKeyPress += (sender, e) =>
                    {
                        Log.Information("Shutting down...");
                        e.Cancel = true;
                        cancellationTokenSource.Cancel();
                    };

                    WaitHandle.WaitAny(new[] { cancellationTokenSource.Token.WaitHandle });

                    server.Dispose();

                    Log.CloseAndFlush();
                }
                else
                {
                    Log.Information("An instance of this program is already running.");
                }
            }
        }

        public static string GetLastLine(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            int lastNewLineIndex = text.LastIndexOfAny(new char[] { '\r', '\n' });
            if (lastNewLineIndex == -1)
            {
                return text;
            }

            if (lastNewLineIndex > 0 && text[lastNewLineIndex - 1] == '\r' && text[lastNewLineIndex] == '\n')
            {
                lastNewLineIndex--;
            }

            return text.Substring(lastNewLineIndex + 1);
        }

        static void processOutput(string output)
        {
            //Console.WriteLine($"Command Execution output: {output}");
            output = Header + output + Tailor;
            // Divide the output into parts and send the first part
            DivideOutput(output);
        }

       

        static void DivideOutputOnSize(string output)
        {
 
            int outputLength = output.Length;
            int position = 0;
            //bool firstPart = true;

            while (position < outputLength)
            {
                int length = Math.Min(MaxMessageSize, outputLength - position);
                string part;

                //if (firstPart)
                //{
                //    part = "BEGIN<" + output.Substring(position, length);
                //    firstPart = false;
                //}
                //else
                if (position + length >= outputLength)
                {
                    part = output.Substring(position, length);
                }
                else
                {
                    part = output.Substring(position, length) + AskForNext;
                }

                outputParts.Add(part);
                position += length;
            }
        }

        //static void DivideOutput(string output)
        //{
        //    outputParts.Clear();
        //    currentPartIndex = 0;

        //    int outputLength = output.Length;
        //    int position = 0;


        //    while (position < outputLength)
        //    {
        //        int wordsProcessed = 0;
        //        StringBuilder partBuilder = new StringBuilder();

        //        while (wordsProcessed < maxWords && position < outputLength)
        //        {
        //            partBuilder.Append(output[position]);
        //            if (char.IsWhiteSpace(output[position]))
        //            {
        //                wordsProcessed++;
        //            }
        //            position++;
        //        }

        //        if (position < outputLength)
        //        {
        //            partBuilder.Append(AskForNext);
        //        }

        //        outputParts.Add(partBuilder.ToString());
        //    }
        //}


        static void DivideOutput(string output)
        {
            int outputLength = output.Length;
            int position = 0;

            while (position < outputLength)
            {
                int wordsProcessed = 0;
                StringBuilder partBuilder = new StringBuilder();

                while (wordsProcessed < maxWords && position < outputLength)
                {
                    partBuilder.Append(output[position]);
                    if (char.IsWhiteSpace(output[position]))
                    {
                        wordsProcessed++;
                    }
                    position++;
                }

                // If the current position is not the end of the output,
                // search for the last newline character in the block
                if (position < outputLength)
                {
                    int lastNewline = partBuilder.ToString().LastIndexOf('\n');
                    if (lastNewline != -1)
                    {
                        position -= (partBuilder.Length - lastNewline - 1);
                        partBuilder.Length = lastNewline + 1;
                        partBuilder.Append(AskForNext);
                    }
                    else
                    {
                        partBuilder.Append("\r\n"+ AskForNext);
                    }
                }
                outputParts.Add(partBuilder.ToString());
            }
        }


        static int GetIndexOfNthWord(int n,int startIndex, string text)
        {
            if (n < 1 || string.IsNullOrEmpty(text))
            {
                return -1;
            }

            int wordCount = 0;
            bool isPreviousCharSpace = true;

            for (int i = startIndex; i < text.Length; i++)
            {
                if (char.IsWhiteSpace(text[i]))
                {
                    isPreviousCharSpace = true;
                }
                else if (isPreviousCharSpace)
                {
                    wordCount++;
                    if (wordCount == n)
                    {
                        return i;
                    }
                    isPreviousCharSpace = false;
                }
            }

            return -1;
        }

        static void SendNextPart(IWebSocketConnection socket)
        {
            if (currentPartIndex < outputParts.Count)
            {
                Log.Information(outputParts[currentPartIndex]);
                int size = outputParts[currentPartIndex].Length;
                socket.Send( outputParts[currentPartIndex]);
                currentPartIndex++;
            }
        }

        static List<string> ExtractWindowsCommands(string message)
        {
            string headerKeyword = "MMI<<";
            string tailorKeyword = ">>MMI";

            int startIndex = -1;
            int endIndex = -1;

            receptionBuffer += message;

            do
            {
                startIndex = receptionBuffer.IndexOf(headerKeyword, startIndex + 1);
                if (startIndex != -1)
                {
                    endIndex = receptionBuffer.IndexOf(tailorKeyword, startIndex + headerKeyword.Length);
                    if (endIndex != -1)
                    {
                        string command = receptionBuffer.Substring(startIndex + headerKeyword.Length, endIndex - startIndex - headerKeyword.Length).Trim();
                        pendingCommands.Add(command);
                    }
                    else
                    {
                        receptionBuffer = receptionBuffer.Substring(startIndex);
                        break;
                    }
                }
                else
                {
                    receptionBuffer = "";
                }
            } while (startIndex != -1 && endIndex != -1);

            return pendingCommands;
        }


        static void InitializeCmdProcess()
        {
            cmdProcess = new Process();
            cmdProcess.StartInfo.FileName = "cmd.exe";
            cmdProcess.StartInfo.UseShellExecute = false;
            cmdProcess.StartInfo.RedirectStandardOutput = true;
            cmdProcess.StartInfo.RedirectStandardInput = true;
            cmdProcess.StartInfo.RedirectStandardError = true;
            cmdProcess.StartInfo.CreateNoWindow = true;

            cmdProcess.OutputDataReceived += CmdProcess_OutputDataReceived;
            cmdProcess.ErrorDataReceived += CmdProcess_ErrorDataReceived;

            cmdProcess.Start();

            cmdStreamWriter = cmdProcess.StandardInput;

            cmdProcess.BeginOutputReadLine();
            cmdProcess.BeginErrorReadLine();

            outputTimeoutTimer = new System.Timers.Timer(3000); // 3 second timeout
            outputTimeoutTimer.Elapsed += OutputTimeoutTimer_Elapsed;
            outputTimeoutTimer.AutoReset = false;
        }





        public static string ExecuteCommand(string command)
        {
            normalOutput.Clear();
            errorOutput.Clear();

            outputEndEvent.Reset();
            questionAskedEvent.Reset();

            cmdStreamWriter.WriteLine(command);
 
            cmdStreamWriter.Flush();

            outputTimeoutTimer.Start();

            WaitHandle.WaitAny(new[] { outputEndEvent, questionAskedEvent });

            outputTimeoutTimer.Stop();

            string output = normalOutput.ToString();
            string error = errorOutput.ToString();

            if (!string.IsNullOrEmpty(error))
            {
                return $"Error: {error}";
            }

            if (questionAskedEvent.WaitOne(0))
            {
                return $"Question: {output}";
            }

            return output;
        }


        public static void ProvideAnswer(string answer)
        {
            cmdStreamWriter.WriteLine(answer);
            cmdStreamWriter.Flush();
            answerProvidedEvent.Set();
        }

        static void CmdProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                outputTimeoutTimer.Stop();
                outputTimeoutTimer.Start();

                if (e.Data == "end")
                {
                    outputEndEvent.Set();
                }
                else if (e.Data.EndsWith("?"))
                {
                    normalOutput.AppendLine(e.Data);
                    questionAskedEvent.Set();
                }
                else if (e.Data.EndsWith("echo end"))
                {
                    outputEndEvent.Set();
                    // Ignore the "echo end" line
                }
                else
                {//ignore the first line when creating a cmd window:
                   // (c)Microsoft Corporation.Tous droits réservés.
                    if (firstOutputLine)
                    {
                        firstOutputLine = false;
                    }
                    else
                    {
                        normalOutput.AppendLine(e.Data);
                        
                    }
                }
            }
        }

        private static void CmdProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null) return;

            errorOutput.AppendLine(e.Data);
        }

        private static void OutputTimeoutTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            outputEndEvent.Set();
        }

    }
}

