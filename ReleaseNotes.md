*   Optimize the maximum word usage.
*   When the Executor asks ChatGPT to continue, it should specify the last received line and request that ChatGPT not resend previously received content.
*   *   Move the management of the initial prompt to the server side to minimize dependency on the plugin protocol and ensure ongoing compatibility between the initial prompt version and the server-implemented version. To accomplish this, add a config parameter on the plugin side that requests an initial prompt when new communication is detected. The plugin should send a command to the server, which should be independent of the protocol, "_SART_NEW".