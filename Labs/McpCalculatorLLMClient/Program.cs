using Azure;
using Azure.AI.Inference;
using Azure.Identity;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

var endpoint = "https://models.inference.ai.azure.com";
var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN"); // Your GitHub Access Token
var client = new ChatCompletionsClient(new Uri(endpoint), new AzureKeyCredential(token));
var chatHistory = new List<ChatRequestMessage>
{
    new ChatRequestSystemMessage("You are a helpful assistant that knows about AI")
};

var clientTransport = new StdioClientTransport(new()
{
    Name = "MCP Calculator Server",
    Command = "..\\..\\..\\..\\McpCalculatorServer\\bin\\Debug\\net9.0\\McpCalculatorServer.exe"
});
// var clientTransport = new StdioClientTransport(new()
// {
//    Name = "MCP Calculator Server",
//    Command = "dotnet",
//    Arguments = ["run", "--project", "..\\..\\..\\..\\McpCalculatorServer\\McpCalculatorServer.csproj"],
//});

Console.WriteLine("🚀 Setting up stdio transport");

await using var mcpClient = await McpClient.CreateAsync(clientTransport);

ChatCompletionsToolDefinition ConvertFrom(string name, string description, JsonElement jsonElement)
{ 
    // convert the tool to a function definition
    FunctionDefinition functionDefinition = new FunctionDefinition(name)
    {
        Description = description,
        Parameters = BinaryData.FromObjectAsJson(new
        {
            Type = "object",
            Properties = jsonElement
        },
        new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
    };

    // create a tool definition
    ChatCompletionsToolDefinition toolDefinition = new ChatCompletionsToolDefinition(functionDefinition);
    return toolDefinition;
}

async Task<List<ChatCompletionsToolDefinition>> GetMcpTools()
{
    Console.WriteLine("🛠️ Listing tools");
    var tools = await mcpClient.ListToolsAsync();

    List<ChatCompletionsToolDefinition> toolDefinitions = new List<ChatCompletionsToolDefinition>();

    foreach (var tool in tools)
    {
        Console.WriteLine($"🔗 Connected to server with tools: {tool.Name}");
        Console.WriteLine($"📄 Tool description: {tool.Description}");
        Console.WriteLine($"📝 Tool parameters: {tool.JsonSchema}");

        JsonElement propertiesElement;
        tool.JsonSchema.TryGetProperty("properties", out propertiesElement);

        var def = ConvertFrom(tool.Name, tool.Description, propertiesElement);
        Console.WriteLine($"🧰 Tool definition: {def}");
        toolDefinitions.Add(def);

        Console.WriteLine($"🔍 Properties: {propertiesElement}");        
    }

    return toolDefinitions;
}

// 1. List tools on mcp server
var tools = await GetMcpTools();
for (int i = 0; i < tools.Count; i++)
{
    var tool = tools[i];
    Console.WriteLine($"🛠️ MCP Tools def: {i}: {tool}");
}

// 2. Define the chat history and the user message
//var userMessage = "add 2 and 4";
var userMessage = "Can you add two and four for me?";

chatHistory.Add(new ChatRequestUserMessage(userMessage));


// 3. Define options, including the tools
var options = new ChatCompletionsOptions(chatHistory)
{
    Model = "gpt-4o-mini",
    Tools = { tools[0] }
};

// 4. Call the model  
ChatCompletions? response = await client.CompleteAsync(options);
var content = response.Content;

// 5. Check if the response contains a function call
ChatCompletionsToolCall? calls = response.ToolCalls.FirstOrDefault();
for (int i = 0; i < response.ToolCalls.Count; i++)
{
    var call = response.ToolCalls[i];
    Console.WriteLine($"🤖 Tool call {i}: {call.Name} with arguments {call.Arguments}");
    //Tool call 0: add with arguments {"a":2,"b":4}

    var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(call.Arguments);
    var result = await mcpClient.CallToolAsync(
        call.Name,
        dict!,
        cancellationToken: CancellationToken.None
    );

    if (result.Content.FirstOrDefault(c => c.Type == "text") is TextContentBlock textBlock && textBlock.Text is not null)
    {
        Console.WriteLine($"📢 {textBlock.Text}");
    }
    else
    {
        Console.WriteLine($"📢 <no text>");
    }
}

// 5. Print the generic response
Console.WriteLine($"💬 Assistant response: {content}");