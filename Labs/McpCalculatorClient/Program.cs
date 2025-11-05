using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>();

//var clientTransport = new StdioClientTransport(new()
//{
//    Name = "MCP Calculator Server",
//    Command = "dotnet",
//    Arguments = ["run", "--project", "..\\..\\..\\..\\McpCalculatorServer\\McpCalculatorServer.csproj"],
//});
var clientTransport = new StdioClientTransport(new()
{
    Name = "MCP Calculator Server",
    Command = "..\\..\\..\\..\\McpCalculatorServer\\bin\\Debug\\net9.0\\McpCalculatorServer.exe"
});

await using var mcpClient = await McpClient.CreateAsync(clientTransport);

foreach (var tool in await mcpClient.ListToolsAsync())
{
    Console.WriteLine($"{tool.Name} ({tool.Description})");
}

var result = await mcpClient.CallToolAsync(
    "add",
    new Dictionary<string, object?>() { ["a"] = 1, ["b"] = 3 },
    cancellationToken: CancellationToken.None);


if (result.Content.FirstOrDefault(c => c.Type == "text") is TextContentBlock textBlock && textBlock.Text is not null)
{
    Console.WriteLine(textBlock.Text);
}
else
{
    Console.WriteLine("<no text>");
}