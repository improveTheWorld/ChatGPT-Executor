using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Fleck;
using System.Timers;
using System.ServiceProcess;


namespace ChatGPTExecutor
{
    class CommandManager
    {
        private const int maxWords = 2400;
        private const int MaxMessageSize = 4000;
        private static List<string> outputParts = new List<string>();
        private static int currentPartIndex = 0;

        private const string Header = "CHATGPT<< \r\n";
        private const string Tailor = "\r\n >>CHATGPT";
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

        static void resetCommunicationBuffers()
        {
            outputParts.Clear();
            currentPartIndex = 0;
            receptionBuffer = string.Empty;
            pendingCommands.Clear();
        }

       

        public void Stop()
        {
            Console.WriteLine("Shutting down...");

            // Stop the cmd process
            if (!cmdProcess.HasExited)
            {
                cmdProcess.Kill();
            }

            // Dispose the WebSocket server
            cancellationTokenSource.Cancel();
            server.Dispose();
            serviceTask.Wait();

            Console.WriteLine("WebSocket server stopped.");
        }
        public void Start()
        {
            serviceTask = Task.Run(() => StartServiceTask(), cancellationTokenSource.Token);
        }
        public void StartServiceTask()
        {
            bool isNewInstance;
            firstPrompt = File.ReadAllText("firstPrompt.md");
            using (Mutex mutex = new Mutex(true, "ChatGPTExecutor", out isNewInstance))
            {
                if (isNewInstance)
                {
                    // Initialize cmd process
                    InitializeCmdProcess();
                    server = new WebSocketServer("ws://127.0.0.1:8181");
                    cancellationTokenSource = new CancellationTokenSource();
                    server.Start(socket =>
                    {
                        socket.OnOpen = () => Console.WriteLine("WebSocket connection opened.");
                        socket.OnClose = () => Console.WriteLine("WebSocket connection closed.");
                        socket.OnMessage = message =>
                        {
                            if(message=="_STOP_")
                            {
                                resetCommunicationBuffers();
                                return;
                            }
                            else if (message == "_START_NEW")
                            {
                                resetCommunicationBuffers();
                                socket.Send(firstPrompt);
                                return;
                            }
                            else
                            {
                                Console.WriteLine("New message received");
                                ExtractWindowsCommands(message);


                                if (receptionBuffer != string.Empty)
                                {
                                    Console.WriteLine($"Last Command Uncomplete. Continue");
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
                    });

                    Console.WriteLine("WebSocket server started. Press CTRL+C to exit...");
                    Console.CancelKeyPress += (sender, e) =>
                    { 
                        Console.WriteLine("Shutting down...");
                        e.Cancel = true;
                        cancellationTokenSource.Cancel();
                    };

                    WaitHandle.WaitAny(new[] { cancellationTokenSource.Token.WaitHandle });

                    server.Dispose();
                }
                else
                {
                    Console.WriteLine("An instance of this program is already running.");
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
                    part = output.Substring(position, length) + "<<ASK_FOR_NEXT>>";
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
        //            partBuilder.Append("<<ASK_FOR_NEXT>>");
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
                        partBuilder.Append("<<ASK_FOR_NEXT>>");
                    }
                    else
                    {
                        partBuilder.Append("\r\n<<ASK_FOR_NEXT>>");
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
                Console.WriteLine(outputParts[currentPartIndex]);
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

            outputTimeoutTimer = new System.Timers.Timer(1000); // 1 second timeout
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

