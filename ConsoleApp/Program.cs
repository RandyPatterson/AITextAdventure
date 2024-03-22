using Humanizer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenTelemetry.Logs;
using System.Diagnostics;
using System.Text.Json;
using Kernel = Microsoft.SemanticKernel.Kernel;
using Spectre.Console;

namespace ChatOpenAIConsole
{
    internal class Program
    {
        /*
        ToDo:
        - Make Program class an instance
        - Add System message as field and add when loading a game, currently load games use default system message
        */

        private IChatCompletionService _historyChatService;
        private IChatCompletionService _gameChatService;
        private Kernel _kernel;
        private OpenAIPromptExecutionSettings _gamePromptConfig;
        private OpenAIPromptExecutionSettings _historyPromptConfig;
        private ChatHistory _history;

        private static async Task Main(string[] args)
        {

            await new Program().StartGameLoop(args);

        }

        public Program() { }



        /// <summary>
        /// Builds the configuration for the application and initializes necessary services.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        private void Build(string[] args)
        {
            // Create a new configuration builder
            var config = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory) // Set the base path to the current domain's base directory
                .AddJsonFile("appsettings.json") // Add the appsettings.json file
                .AddUserSecrets<Program>() // Add user secrets of the Program class
                .AddEnvironmentVariables() // Add environment variables
                .AddCommandLine(args) // Add command line arguments
                .Build(); // Build the configuration

            Console.ForegroundColor = ConsoleColor.White;

            // Retrieve necessary configurations
            string gameModel = config["gameModel"];         // Model used to manage game state and chat history
            string historyModel = config["historyModel"];   // Model used to compress chat history
            string endpoint = config["endpoint"];           // Endpoint for the OpenAI API
            string apikey = config["apikey"];               // API key for the OpenAI API

            // Create a logger factory with OpenTelemetry as a logging provider
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddOpenTelemetry(options =>
                {
                    options.IncludeFormattedMessage = true; // Format log messages
                });
                builder.SetMinimumLevel(LogLevel.Trace); // Set the minimum log level to Trace
            });

            // Create the kernel
            var builder = Kernel.CreateBuilder();
            builder.Services.AddLogging(c => c.SetMinimumLevel(LogLevel.Trace)); // Add logging service with minimum log level set to Trace
            builder.Services.ConfigureHttpClientDefaults(c =>
            {
                // Use a standard resiliency policy, augmented to retry on 429 Rate Limit Exceeded
                c.AddStandardResilienceHandler().Configure(o =>
                {
                    o.Retry.DelayGenerator = null;
                    o.Retry.Delay = new TimeSpan(0, 0, 5); // Set the delay between retries to 5 seconds
                    o.Retry.MaxRetryAttempts = 5; // Set the maximum number of retry attempts to 5
                });
            });

            // Add Azure OpenAI Chat Completion services
            builder.Services.AddAzureOpenAIChatCompletion(gameModel, endpoint, apikey, "gameChat");
            builder.Services.AddAzureOpenAIChatCompletion(historyModel, endpoint, apikey, "historyChat");
            _kernel = builder.Build(); // Build the kernel

            // Retrieve the chat completion service from the kernel
            _gameChatService = _kernel.GetRequiredService<IChatCompletionService>("gameChat");
            _historyChatService = _kernel.GetRequiredService<IChatCompletionService>("historyChat");

            _history = null; // Initialize the chat history to null

            // Define the game system prompt
            string gameSystemPrompt = $"""
                    you are a text adventure AI that can generate complex text adventures similiar to Zork.  
                    let the user enter commands to navigate and interact with the environment 
                    Use north, south, east,west, up and down as navigation commands and descriptions
                    user can navigate using abreviations, e.g. n for North 
                    You generate an adventure that requires a mystery to be solved and can lead to character failing 
                    You keep track of the users inventory and where they have been
                    Respond using descriptive language appropriate for the theme 
                    """
                    ;

            // Define the game prompt configuration
            _gamePromptConfig = new()
            {
                ChatSystemPrompt = gameSystemPrompt,
                Temperature = 0.9,
                MaxTokens = 1500,
            };

            // Define the history prompt configuration
            _historyPromptConfig = new()
            {
                Temperature = 0.2,
                MaxTokens = 800
            };
        }


        public async Task StartGameLoop(string[] args)
        {
            Build(args);

            if (File.Exists("save.json"))
            {
                Console.Write("Would you like to load a saved game? (y/n)");
                string input = Console.ReadLine();
                if (input.StartsWith("y", StringComparison.CurrentCultureIgnoreCase))
                    _history = Load();

            }

            if (_history == null)
            {
                string theme = GetTheme();

                //collection to store converstaion history and initialize with a persona
                _history = new ChatHistory();
                // Get the chat completions
                streamResponse(_gamePromptConfig, $"Generate a text adventure world using the theme {theme}").Wait();
                Console.WriteLine();
            }

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("Command: ");
                string? prompt = Console.ReadLine();
                Console.ResetColor();

                if (prompt.ToLower() == "save")
                {
                    Save(_history);
                }
                else if (prompt.ToLower() == "load")
                {
                    _history = Load();
                }
                else
                {
                    await streamResponse(_gamePromptConfig, prompt);
                    Console.WriteLine();
                    Console.WriteLine(new string('-', 20));
                }

            }

        }


        private async Task streamResponse(OpenAIPromptExecutionSettings openAIPromptExecutionSettings, string? prompt)
        {
            _history.AddUserMessage(prompt);
            var response = _gameChatService.GetStreamingChatMessageContentsAsync(_history, executionSettings: openAIPromptExecutionSettings, kernel: _kernel);

            // Stream the results
            string fullMessage = "";
            await foreach (var content in response)
            {   
                Console.Write(content.Content);
                fullMessage += content?.Content;
            }
            // Add the message from the agent to the chat history
            _history.AddAssistantMessage(await CompressHistory(fullMessage));
        }

        private string GetTheme()
        {
            AnsiConsole.Write(
                new FigletText("AI Text Adventure")
                .Centered()
                .Color(Color.Green)); 

            Console.WriteLine("Welcome to the AI generated Text Adventure. \nYou interact with the AI to try and solve the mystery\n\n");
            Console.Write("What theme would you like to play? \nFor Example: mystery, fantasy, sci-fi, time travel, dungeon crawler or make up your own: ");
            string input = Console.ReadLine();
            string theme = string.IsNullOrWhiteSpace(input) ? "dungeon crawler" : input;

            Console.WriteLine("You have chosen to play a " + theme.Humanize(LetterCasing.Title) + " game.");
            Console.WriteLine("\n\nGenerating the game world...\n\n");

            Console.WriteLine("""
                ***********************************************
                * To save your game at any time, type 'save'. *
                * to load a saved game, type 'load'.          *
                ***********************************************

                """);
            return theme;

        }




        /// Compresses the chat history by removing irrelevant information.
        /// </summary>
        /// <param name="prompt">The chat history to be compressed.</param>
        /// <returns>A compressed version of the chat history.</returns>
        private async Task<string> CompressHistory(string prompt)
        {


            // Request the chat service to compress the chat history
            var historyPrompt = $"update the following text to reduce the number of tokens for LLM History. Do not lose fidelity but remove any irrelevant information not needed for history storage\n\n{prompt}";
            var historyResponse = await _historyChatService.GetChatMessageContentsAsync(historyPrompt, _historyPromptConfig);
            if (historyResponse?.Count == 0)
                return prompt;

            Debug.Write("optimized history:");
            Debug.WriteLine(historyResponse[0].ToString());
            return historyResponse[0].ToString();
        }

        /// <summary>
        /// Prints the history of chat messages to the console.
        /// </summary>
        /// <param name="history">The history of chat messages.</param>
        private static void PrintHistory(ChatHistory history)
        {
            Console.WriteLine("Your Journal:");
            foreach (var message in history)
            {
                if (message.Role == AuthorRole.User)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"\nCommand: {message.Content}");
                    Console.ForegroundColor = ConsoleColor.White;
                }
                else if (message.Role == AuthorRole.Assistant)
                {
                    Console.WriteLine($"{message.Content}");
                }

            }
        }

        private static void Save(ChatHistory history)
        {
            Console.WriteLine("Saving progress to file...");
            // Convert the ChatHistory object to a JSON string
            string jsonString = JsonSerializer.Serialize(history);

            // Save the JSON string to a file
            File.WriteAllText(@"save.json", jsonString);
            Console.WriteLine("Success");
        }

        private static ChatHistory? Load()
        {
            // Check if the file exists
            if (!File.Exists(@"save.json"))
            {
                Console.WriteLine("No saved progress found.");
            }

            Console.Clear();
            // Read the JSON string from a file
            Console.WriteLine("Loading progress from file...");
            string jsonString = File.ReadAllText(@"save.json");

            // Convert the JSON string to a ChatHistory object
            var chatMessages = JsonSerializer.Deserialize<ChatHistory>(jsonString);


            PrintHistory(chatMessages);
            return chatMessages;
        }
    }
}