# ![ChatGPT-Executor Logo](./Images/logo.jpg) **ChatGPT-Executor**

**ChatGPT-Executor is a server application that empowers ChatGPT to execute Windows commands, unlocking a wide range of applications and capabilities.**

ChatGPT-Executor is part of the ChatGPT-Driver system, designed to seamlessly integrate ChatGPT with third-party software and enable it to execute Windows commands. To use ChatGPT-Executor, you'll also need to install the [ChatGPT-Bridge](https://github.com/improveTheWorld/ChatGPT-Bridge) browser plugin.

## 🌟 Features

* 💬 Real-time execution of Windows commands generated by ChatGPT
* ⚡ Efficient WebSocket server
* 🔄 Bidirectional communication with ChatGPT and the command execution environment
* 📦 Easy integration with ChatGPT web plugin
* 📄 Enable ChatGPT to read large files that exceed the size of a single prompt

## 🚀 Getting Started

### Prerequisites

* Windows operating system

### Compilation

* **Using command-line**

  Follow these instructions to compile the ChatGPT-Executor using command-line tools:

  1. Ensure you have .NET 6.0 SDK installed on your system. If not, download it from the [.NET downloads page](https://dotnet.microsoft.com/download/dotnet/6.0).
  2. Open a command prompt or terminal window.
  3. If you have Git installed, you can use the following command:

  `git clone https://github.com/improveTheWorld/ChatGPT-Executor.git`

  Alternatively, download the repository as a ZIP file to your local machine from [here](https://github.com/improveTheWorld/ChatGPT-Executor/archive/refs/heads/master.zip) and extract it to a folder on your local machine.

  4. Change your working directory to the ChatGPT-Executor folder:

  `cd ChatGPT-Executor`

  5. Build the solution using the `dotnet build` command:

  `dotnet build`

  6. Once the build is successful, you can find the compiled executable in the "bin" folder of the project directory, under the "Release" folder.

  Congratulations! 🎉 ChatGPT-Executor is now up. Luanch it on your local machine. You need now to install the [ChatGPT-Bridge plugin](https://github.com/improveTheWorld/ChatGPT-Bridge) to see it in action.
* **Using Microsoft visual Studio**

1. Clone the repository:

   git clone [https://github.com/improveTheWorld/ChatGPT-Executor.git](https://github.com/improveTheWorld/ChatGPT-Executor.git)
2. `To compile the ChatGPT-Executor, which is written in C# using Microsoft Visual Studio 2022 and targeting .NET 6.0, follow these steps:

   1. Ensure you have Microsoft Visual Studio 2022 installed on your system. If not, download it from the [official Microsoft website](https://visualstudio.microsoft.com/vs/).
   2. Install the .NET 6.0 SDK from the[.NET downloads page](https://dotnet.microsoft.com/download/dotnet/6.0).
   3. download the repository as a ZIP file to your local machine from [here](https://github.com/improveTheWorld/ChatGPT-Executor/archive/refs/heads/master.zip) and extract it to a folder on your local machine.
   4. Open the solution file (ChatGPT-Executor.sln) in Visual Studio 2022.
   5. Build the solution by selecting "Build" from the menu, then "Build Solution" or use the shortcut "Ctrl+Shift+B".
   6. Once the build is successful, you can find the compiled executable in the "bin" folder of the project directory, under the appropriate configuration folder (e.g., "Debug" or "Release").

   Congratulations! 🎉 ChatGPT-Executor is now up. Luanch it on your local machine. You need now to install the [ChatGPT-Bridge plugin](https://github.com/improveTheWorld/ChatGPT-Bridge) to see it in action.

## 🛠️ Usage

After running the ChatGPT-Executor server application, you need to use it in conjunction with ChatGPT-Bridge to enable it to execute Windows commands generated by CHATGPT. For detailed usage instructions, please refer to the [ChatGPT-Bridge GitHub Repository](https://github.com/improveTheWorld/ChatGPT-Bridge).

<!-- 📚 Documentation
----------------

For more detailed information on how to use ChatGPT-Executor, please refer to the [Wiki](https://github.com/improveTheWorld/ChatGPT-Executor/wiki).
📧 Contributing
---------------

We welcome contributions! If you'd like to contribute, please follow the steps outlined in our [Contributing Guidelines](./CONTRIBUTING.md). -->

## 🔐 License

* This project is licensed under the Apache V2.0 for free software use - see the [LICENSE](./LICENSE-APACHE.txt) file for details.
* For commercial software use, see the [LICENSE\_NOTICE](./LICENSE_NOTICE.md) file.

## 📬 Contact

If you have any questions or suggestions, please feel free to reach out to us:

* [Tec-Net](mailto:tecnet.paris@gmail.com)

<!-- * Project Link -->

**Together with ChatGPT-Bridge, ChatGPT-Executor unlocks the unlimited power and potential of ChatGPT. Join us on this exciting journey to revolutionize the way we interact with AI-powered language models!**
