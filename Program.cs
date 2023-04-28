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
        private const int maxWords = 2000;
        private const int MaxMessageSize = 4000;
        private static List<string> outputParts = new List<string>();
        private static int currentPartIndex = 0;
        private const string Header = "CHATGPT<< \r\n";
        private const string Tailor = "\r\n >>CHATGPT";
        private static string receptionBuffer = ""; 
        private static Process cmdProcess;
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
                    var server = new WebSocketServer("ws://127.0.0.1:8181");
                    var cancellationTokenSource = new CancellationTokenSource();
                    server.Start(socket =>
                    {
                        socket.OnOpen = () => Console.WriteLine("WebSocket connection opened.");
                        socket.OnClose = () => Console.WriteLine("WebSocket connection closed.");
                        socket.OnMessage = message =>
                        {
                            List<string> commands = ExtractWindowsCommands(message);

                           
                            if (commands.Count != 0)
                            {   
                                bool multiCommands = false;
                                if(commands.Count > 1)
                                {
                                    multiCommands = true;
                                }

                                StringBuilder concatenatedOutput = new StringBuilder();
                                int commandIndex = 0;
                                foreach (string command in commands)
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
                                        if(multiCommands && commandOutput.Trim()!= string.Empty)
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

                                if (receptionBuffer != string.Empty)
                                {
                                    Console.WriteLine($"Last Command Uncomplete. Continue");
                                    concatenatedOutput.Append("Last Command Uncomplete. Continue from where you stopped");
                                }

                                processOutput(concatenatedOutput.ToString());
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

        static void processOutput(string output)
        {
            //Console.WriteLine($"Command Execution output: {output}");
            output = Header + output + Tailor;
            // Divide the output into parts and send the first part
            DivideOutput(output);
        }

        static void CmdProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                if (e.Data == "end")
                {
                    outputEndEvent.Set();
                }
                else if (e.Data.EndsWith("echo end"))
                {
                    // Ignore the "echo end" line
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
            string headerKeyword = "MMI<< ";
            string tailorKeyword = " >>MMI";

            List<string> commands = new List<string>();
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
                        commands.Add(command);
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

            return commands;
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
