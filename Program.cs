using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Fleck;

namespace ChatCommandExecutor
{
    class Program
    {
        private const int maxWords = 4000;
        private static List<string> outputParts = new List<string>();
        private static int currentPartIndex = 0;
        private const string Header = "CHATGPT<< ";
        private const string Tailor = " >>CHATGPT";
        private static string receptionBuffer = ""; private static Process cmdProcess;
        private static StreamWriter cmdStreamWriter;
        private static StreamReader cmdStandardOutput;
        private static StreamReader cmdStandardError;
        private static StringBuilder normalOutput = new StringBuilder();
        private static StringBuilder errorOutput = new StringBuilder();
        private static bool firstOutputLine = true;
        private static ManualResetEvent outputEndEvent = new ManualResetEvent(false);


        static void Main(string[] args)
        {


            bool isNewInstance;
            using (Mutex mutex = new Mutex(true, "ChatCommandExecutor", out isNewInstance))
            {
                if (isNewInstance)
                {
                    // Initialize cmd process
                    InitializeCmdProcess();
                    Console.WriteLine("starting");
                    Console.WriteLine("-------------------------------------------------------");
                    Console.WriteLine(ExecuteCommand($"cd \"C:\\Users\\Bilel_Alstom\\Desktop\\codeSource\\talk-to-chatgpt-main\\chrome-extension\" && dir"));
                    Console.WriteLine("-------------------------------------------------------");
                    Console.WriteLine(ExecuteCommand($"type content.js"));
                    Console.WriteLine("-------------------------------------------------------");


                    var server = new WebSocketServer("ws://127.0.0.1:8181");
                    var cancellationTokenSource = new CancellationTokenSource();
                    server.Start(socket =>
                    {
                        socket.OnOpen = () => Console.WriteLine("WebSocket connection opened.");
                        socket.OnClose = () => Console.WriteLine("WebSocket connection closed.");
                        socket.OnMessage = message =>
                        {
                            //Console.WriteLine($"Received Message: {message}");

                            string command = ExtractMMIMessage( message);
                            //send back continu while buffering 
                            if(receptionBuffer != string.Empty)
                            {
                                Console.WriteLine($" command not complete , continue");
                                socket.Send("Continue");
                            }
                            else if (command == "NEXT")
                            {
                                Console.Write($"NEXT:");
                                SendNextPart(socket);
                            }
                            else if (command != string.Empty)
                            {
                                //Console.WriteLine($"Extracted command: {command}");

                                // Execute the command and get the output
                                string output = ExecuteCommand(command);
                                //Console.WriteLine($"Command Execution output: {output}");
                                output = Header + output + Tailor;
                                // Divide the output into parts and send the first part
                                DivideOutput(output);                                
                                SendNextPart(socket);
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

       

        private static void CmdProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                if (e.Data == "end")
                {
                    outputEndEvent.Set();
                }
                else
                {
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

        static void DivideOutput(string output)
        {
            outputParts.Clear();
            currentPartIndex = 0;

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

                if (position < outputLength)
                {
                    partBuilder.Append("<<ASK_FOR_NEXT>>");
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
                socket.Send( outputParts[currentPartIndex]);
                currentPartIndex++;
            }
        }

        static string ExtractMMIMessage(string message)
        {
            string headerKeyword = "MMI<<";
            string tailorKeyword = ">>MMI";

            int startIndex = message.IndexOf(headerKeyword);
            int endIndex = message.IndexOf(tailorKeyword);

            if (startIndex == -1 && endIndex == -1)
            {
                if (receptionBuffer != string.Empty)
                {
                    receptionBuffer += message;
                }
                return string.Empty;
            }
            else if (startIndex != -1 && endIndex == -1)
            {
                receptionBuffer += message.Substring(startIndex + headerKeyword.Length);
                return string.Empty;
            }
            else if (startIndex == -1 && endIndex != -1)
            {
                receptionBuffer += message.Substring(0, endIndex);
                string result = receptionBuffer.Trim();
                receptionBuffer = "";
                return result;
            }
            else // both startIndex and endIndex are not -1
            {
                return message.Substring(startIndex + headerKeyword.Length, endIndex - (startIndex + headerKeyword.Length)).Trim();
            }
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
        }

        private static void CmdProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                errorOutput.AppendLine(e.Data);
            }
        }


        static string ExecuteCommand(string command)
        {
            normalOutput.Clear();
            errorOutput.Clear();

            outputEndEvent.Reset();
            firstOutputLine = true;

            cmdStreamWriter.WriteLine(command);
            cmdStreamWriter.WriteLine("echo end");
            cmdStreamWriter.Flush();

            outputEndEvent.WaitOne();

            string output = normalOutput.ToString();
            string error = errorOutput.ToString();

            if (!string.IsNullOrEmpty(error))
            {
                return $"Error: {error}";
            }

            return output;
        }
    }
}
