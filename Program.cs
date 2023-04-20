// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using OpenAI.GPT3;
using OpenAI.GPT3.Managers;
using OpenAI.GPT3.ObjectModels;
using OpenAI.GPT3.ObjectModels.RequestModels;
using pinecone;
using pinecone.Models;
using System.Diagnostics;
using System.Security.AccessControl;
using Vector = pinecone.Models.Vector;

namespace sharpagi;
public class Program
{

    public static async Task Main(string[] args)
    {
        Console.WriteLine("Hello, World!");


        // Load environment variables from UserSecrets in Visual Studio
        var builder = new ConfigurationBuilder()
            .AddUserSecrets<Program>();
        var configuration = builder.Build();

        Sharpagi sharpagi = new Sharpagi();
        await sharpagi.Main(configuration);

    }
}
