# 🎮 AI Text Adventure

A console-based text adventure game powered by AI, built as a showcase of the **Microsoft Agent Framework (MAF)** and the .NET AI ecosystem.

![.NET 10](https://img.shields.io/badge/.NET-8.0-purple)
![Microsoft Agent Framework](https://img.shields.io/badge/Microsoft%20Agent%20Framework-1.1.0-blue)

## Overview

AI Text Adventure is an interactive fiction game—inspired by classics like **Zork**—where the entire game world, narrative, and puzzle logic are generated in real-time by an AI agent. Players navigate rooms, collect items, and solve mysteries using natural-language commands, all driven by an `AIAgent` from the Microsoft Agent Framework.

## Microsoft Agent Framework Showcase

This project demonstrates key capabilities of the [Microsoft Agent Framework](https://github.com/microsoft/Agents-for-net):

| Feature | How It's Used |
|---|---|
| **`AIAgent` creation** | An `AIAgent` is configured with a system prompt that defines the game's behavior, then created via `AsAIAgent()` from an `IChatClient`. |
| **`AgentSession` management** | Game state is maintained through an `AgentSession`, preserving conversation history and context across turns. |
| **Streaming responses** | `RunStreamingAsync` delivers the AI-generated narrative token-by-token for a responsive console experience. |
| **Session serialization** | `SerializeSessionAsync` / `DeserializeSessionAsync` enable save and load functionality by persisting session state to JSON. |
| **Microsoft.Extensions.AI integration** | The OpenAI chat client is adapted to `IChatClient` via `AsIChatClient()`, demonstrating MAF's interoperability with the broader .NET AI abstractions. |

## Features

- 🗺️ **AI-Generated Worlds** — Each playthrough creates a unique adventure with rooms, items, and mysteries.
- 🎭 **Theme Selection** — Choose from mystery, fantasy, sci-fi, time travel, dungeon crawler, or create your own theme.
- 💾 **Save / Load** — Save your progress at any time and resume later.
- 🧭 **Classic Navigation** — Use directional commands (north, south, east, west, up, down) with abbreviation support.
- 📜 **Journal** — Review your adventure history when loading a saved game.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- An **Azure OpenAI** resource with a deployed chat model (e.g., `gpt-4`)

## Getting Started

### 1. Clone the repository

```bash
git clone https://github.com/RandyPatterson/AITextAdventure.git
cd AITextAdventure
```

### 2. Configure your Azure OpenAI credentials

The app reads configuration from `appsettings.json`, **user secrets**, environment variables, or command-line arguments (in that order of precedence).

**Option A — User Secrets (recommended for local development):**

```bash
cd AIAdventure
dotnet user-secrets set "ENDPOINT" "https://<your-resource>.openai.azure.com/openai/v1/"
dotnet user-secrets set "GAMEMODEL" "<your-deployed-model-name>"
dotnet user-secrets set "APIKEY" "<your-api-key>"
```

**Option B — Environment Variables:**

```bash
export ENDPOINT="https://<your-resource>.openai.azure.com/openai/v1/"
export GAMEMODEL="<your-deployed-model-name>"
export APIKEY="<your-api-key>"
```

> **Note:** The endpoint URL must include the `/openai/v1/` path, as required when using the OpenAI SDK to access Azure Foundry models.

### 3. Run the application

```bash
dotnet run --project AIAdventure
```

## How to Play

1. Choose a theme when prompted (or press Enter for the default *dungeon crawler*).
2. Read the AI-generated scene description.
3. Type commands to interact with the world:
   - **Navigation:** `north`, `south`, `east`, `west`, `up`, `down` (or `n`, `s`, `e`, `w`)
   - **Actions:** `look`, `take`, `open`, `use`, `talk`, etc.
   - **Save/Load:** Type `save` to save your progress or `load` to restore a previous game.

## Tech Stack

| Package | Purpose |
|---|---|
| [Microsoft.Agents.AI.Foundry](https://www.nuget.org/packages/Microsoft.Agents.AI.Foundry) | Microsoft Agent Framework — agent creation, sessions, and serialization |
| [Microsoft.Extensions.AI.OpenAI](https://www.nuget.org/packages/Microsoft.Extensions.AI.OpenAI) | .NET AI abstractions for OpenAI-compatible models |
| [Azure.AI.OpenAI](https://www.nuget.org/packages/Azure.AI.OpenAI) | Azure OpenAI client SDK |
| [Azure.Identity](https://www.nuget.org/packages/Azure.Identity) | Azure credential management |
| [Spectre.Console](https://spectreconsole.net/) | Rich console output and ASCII art banner |
| [Humanizer](https://github.com/Humanizr/Humanizer) | String formatting utilities |
| [OpenTelemetry](https://opentelemetry.io/) | Structured logging and observability |

## Project Structure

```
AITextAdventure/
├── AIAdventure/
│   ├── Program.cs          # Application entry point, game loop, and agent setup
│   ├── AIAdventure.csproj  # Project file and dependencies
│   └── appsettings.json    # Configuration template
└── README.md
```

## License

This project is provided as a sample / showcase application. See the repository for license details.