*   Update the protocol to directly handle writing text to a file as generated for the user.
    
*   Enhance the protocol to support updating existing files with a patch generated from ChatGPT. This reduces the number of tokens required to modify an existing file (as ChatGPT has limited token memory). Add the ability to apply patches and save the result in a new file.
    
*   Introduce a configuration file to set the maximum word count for a prompt.
    
*   Optimize the maximum word usage.
    
*   When ChatGPT is asked to read a file, start by providing the total number of lines.
    
*   When the Executor asks ChatGPT to continue, it should specify the last received line and request that ChatGPT not resend previously received content.
    
*   Ensure compatibility with the Linux shell.
    
*   Implement the application as a background service.
    
*   Move the management of the initial prompt to the server side to minimize dependency on the plugin protocol and ensure ongoing compatibility between the initial prompt version and the server-implemented version. To accomplish this, add a config parameter on the plugin side that requests an initial prompt when new communication is detected. The plugin should send a command to the server, which should be independent of the protocol, such as "Send\_First\_Prompt".
    
