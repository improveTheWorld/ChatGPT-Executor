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
        private const int MaxMessageSize = 16000;
        private static List<string> outputParts = new List<string>();
        private static int currentPartIndex = 0;
        private const string Header = "CHATGPT<< ";
        private const string Tailor = " >>CHATGPT";
        private static string receptionBuffer = ""; private static Process cmdProcess;
        private static StreamWriter cmdStreamWriter;
        private static StreamReader cmdStandardOutput;
        private static StreamReader cmdStandardError; 

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
                    Console.WriteLine(ExecuteCommand($"cd \" C:\\Users\\Bilel_Alstom\\Desktop\\codeSource\\talk-to-chatgpt-main\\chrome-extension            string error = cmdStandardError.ReadToEnd();\r\n\r\n \r\n\r\n            if (!string.IsNullOrEmpty(error))\r\n            {{\r\n                return $\"Error: {{error}}\";\r\n            }}"));
                    Console.WriteLine("-------------------------------------------------------");
                    Console.WriteLine(ExecuteCommand($"type \"content.js\""));
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

                                //Reset output as new command executing will start
                                outputParts = new List<string>();
                                currentPartIndex = 0;

                                // Execute the command and get the output
                                string output = ExecuteCommand(command);
                                //Console.WriteLine($"Command Execution output: {output}");
                                output = Header + output + Tailor;
                                // Divide the output into parts and send the first part
                                if (output.Length>MaxMessageSize)
                                {
                                    DivideOutput(output);
                                    SendNextPart(socket);
                                }
                                else
                                {
                                    Console.Write($"Output:"+ output);
                                    socket.Send( output );
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





        static void DivideOutput(string output)
        {
            outputParts.Clear();
            currentPartIndex = 0;

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
                    part = output.Substring(position, length) +"<<ASK_FOR_NEXT>>";
                }

                outputParts.Add(part);
                position += length;
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
            
            int startIndex = message.IndexOf(headerKeyword);
            

            if (startIndex == -1)
            {
                if(receptionBuffer != string.Empty)
                {
                    receptionBuffer += message;
                    //send back "continue"
                }
                return string.Empty;
            }

            
            string MMIMessage = message.Substring(startIndex + headerKeyword.Length);

            string tailorKeyword = " >>MMI";
            int endIndex = MMIMessage.IndexOf(tailorKeyword);

            if (endIndex == -1)
            {
                receptionBuffer+= MMIMessage;
                return string.Empty;
                //sendback "continue"
            }
            else
            {
                receptionBuffer = "";
                return MMIMessage.Substring(0, endIndex).Trim();
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
            cmdProcess.Start();

            cmdStreamWriter = cmdProcess.StandardInput;
            cmdStandardOutput = cmdProcess.StandardOutput;
            cmdStandardError = cmdProcess.StandardError;
            
    }

    static string ExecuteCommand(string command)
        {
            cmdStreamWriter.WriteLine(command);
            cmdStreamWriter.WriteLine("echo end");
            cmdStreamWriter.Flush();

            string line;
            string output = string.Empty;

            while ((line = cmdStandardOutput.ReadLine()) != null)
            {
                if (line == "end")
                    break;

                output += line + Environment.NewLine;
            }

            //string output = cmdStandardOutput.ReadToEnd();
            //string error = cmdStandardError.ReadToEnd();
 

            //if (!string.IsNullOrEmpty(error))
            //{
            //    return $"Error: {error}";
            //}

            return output;

        }

        //static string ExecuteCommand(string command)
        //{
        //    Process process = new Process();
        //    process.StartInfo.FileName = "cmd.exe";
        //    process.StartInfo.Arguments = "/c " + command;
        //    process.StartInfo.RedirectStandardOutput = true;
        //    process.StartInfo.RedirectStandardError = true;
        //    process.StartInfo.UseShellExecute = false;
        //    process.StartInfo.CreateNoWindow = true;

        //    process.Start();

        //    string output = process.StandardOutput.ReadToEnd();
        //    string error = process.StandardError.ReadToEnd();

        //    process.WaitForExit();

        //    if (!string.IsNullOrEmpty(error))
        //    {
        //        return $"Error: {error}";
        //    }

        //    return output;
        //}
    }
}
