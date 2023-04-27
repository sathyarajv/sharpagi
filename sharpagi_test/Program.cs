// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Configuration;
using sharpagi;

Console.WriteLine("Hello, World!");
// Load environment variables from UserSecrets in Visual Studio
var builder = new ConfigurationBuilder()
    .AddUserSecrets<Program>();
var configuration = builder.Build();

Sharpagi sharpagi = new Sharpagi((output, consolecolor) =>
{
    if (consolecolor.HasValue)
    {
        Console.ForegroundColor = consolecolor.Value;
    }
    Console.WriteLine(output);
    if (consolecolor.HasValue)
    {
        Console.ResetColor();
    }
});
await sharpagi.Agent(configuration);
