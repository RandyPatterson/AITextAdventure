using Azure;
using Azure.AI.Projects;
using Humanizer;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using Spectre.Console;
using System.Text.Json;

namespace ChatOpenAIConsole
{
    internal class Program
    {
        /*
        ToDo:
        - Make Program class an instance
        - Add System message as field and add when loading a game, currently load games use default system message
        */

        private AIAgent _agent;
        private AgentSession _session;

        private static async Task Main(string[] args)
        {

            await new Program().StartGameLoop(args);

        }

        public Program() { }



        /// <summary>
        /// Builds the configuration for the application and initializes necessary services.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        private async Task Build(string[] args)
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

            // Ensure that you have the "/openai/v1/" path in the URL, since this is required when using the OpenAI SDK to access Azure Foundry models.
            var endpoint = config["ENDPOINT"]
                ?? throw new InvalidOperationException("Missing ENDPOINT");
            var gameModel = config["GAMEMODEL"]
                ?? throw new InvalidOperationException("Missing GAMEMODEL");
            var apiKey = config["APIKEY"]
                ?? throw new InvalidOperationException("Missing APIKEY");


            // Create a logger factory with OpenTelemetry as a logging provider
            using var loggerFactory = LoggerFactory.Create(builder => {
                builder.AddOpenTelemetry(options => {
                    options.IncludeFormattedMessage = true; // Format log messages
                });
                builder.SetMinimumLevel(LogLevel.Trace); // Set the minimum log level to Trace
            });


            // Define the game system prompt
            string gameSystemPrompt = $"""
                    you are a text adventure AI that can generate complex text adventures similiar to Zork.  
                    let the user enter commands to navigate and interact with the environment.
                    Use north, south, east,west, up and down as navigation commands and descriptions.
                    user can navigate using abreviations, e.g. n for North.
                    You generate an adventure that requires a mystery to be solved and can lead to character failing.
                    You keep track of the users inventory and where they have been.
                    Respond using descriptive language appropriate for the theme.
                    Sometimes be snarky and humorous in your responses, especially when the user fails or is lost.                    
                    """
                    ;

            var clientOptions = new OpenAIClientOptions() { Endpoint=new Uri(endpoint) };
            _agent = new OpenAIClient(new AzureKeyCredential(apiKey), clientOptions)
                .GetChatClient(gameModel)
                .AsIChatClient()
                .AsAIAgent(instructions: gameSystemPrompt, name: "GameAgent");

            _session = await _agent.CreateSessionAsync();
        }


        public async Task StartGameLoop(string[] args)
        {
            await Build(args);

            var loadedFromSave = false;
            if (File.Exists("save.json"))
            {
                Console.Write("Would you like to load a saved game? (y/n)");
                string input = Console.ReadLine();
                if (input.StartsWith("y", StringComparison.CurrentCultureIgnoreCase))
                {
                    _session = await Load();
                    loadedFromSave= true;
                }
            }

            if (!loadedFromSave)
            {
                string theme = GetTheme();
                // Get the chat completions
                await foreach (var update in _agent.RunStreamingAsync(theme, _session))
                    Console.Write(update);
                Console.WriteLine();
            }

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("Command: ");
                string? prompt = Console.ReadLine();
                Console.ResetColor();

                if (prompt.ToLower() == "save")
                    await Save(_session);
                else if (prompt.ToLower() == "load")
                    _session = await Load();
                else
                {
                    await foreach (var update in _agent.RunStreamingAsync(prompt, _session))
                        Console.Write(update);
                    Console.WriteLine();
                    Console.WriteLine(new string('-', 20));
                }

            }

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




        /// <summary>
        /// Prints the history of chat messages to the console.
        /// </summary>
        /// <param name="history">The history of chat messages.</param>
        private void PrintHistory(AgentSession history)
        {
            Console.WriteLine("Your Journal:");
            if (history.TryGetInMemoryChatHistory(out var messages))
            {
                foreach (var message in messages)
                {
                    if (message.Role == ChatRole.User)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"\nCommand: {message.Text}");
                        Console.ResetColor();
                    } else if (message.Role == ChatRole.Assistant)
                    {
                        Console.WriteLine(message.Text);
                    }
                }
            }
        }

        private async Task Save(AgentSession session)
        {
            Console.WriteLine("Saving progress to file...");
            var jsonElement = await _agent.SerializeSessionAsync(session);
            string jsonString = JsonSerializer.Serialize(jsonElement, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(@"save.json", jsonString);
            Console.WriteLine("Success");
        }

        private async Task<AgentSession?> Load()
        {
            // Check if the file exists
            if (!File.Exists(@"save.json"))
            {
                Console.WriteLine("No saved progress found.");
                return null;
            }

            Console.Clear();
            // Read the JSON string from a file
            Console.WriteLine("Loading progress from file...");
            string jsonString = File.ReadAllText(@"save.json");

            // Deserialize the JSON string to a JsonElement, then restore the AgentSession
            var jsonElement = JsonSerializer.Deserialize<JsonElement>(jsonString);
            var session = await _agent.DeserializeSessionAsync(jsonElement);

            PrintHistory(session);
            return session;
        }
    }
}