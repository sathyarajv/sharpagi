# SharpAGI

## Description

SharpAGI is a C# library for creating and using Task-Driven Autonomous Agents in dotnet. 

## Quickstart

To create a Sharpagi client to run the agent in console application

```csharp
   Sharpagi sharpagi = new Sharpagi((output,consolecolor) =>
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
```

To cerate a Sharpagi client in other application types
```csharp
  StringBuilder printOutput = new StringBuilder();
  Sharpagi sharpagi = new Sharpagi((output) =>
  {
     printOutput.AppendLine(output);
  });
  await sharpagi.Agent(configuration);
```

Use sharpagi_test console application to test the agi agent with this user secret

```csharp
{
  "OPENAI_API_KEY": "<Your openai api key>",
  "OPENAI_API_MODEL": "gpt-3.5-turbo", // gpt-3.5-turbo, gpt-4, text-davinci-003, etc
  "OPENAI_TEMPERATURE": "0.7",
 
  "PINECONE_API_KEY": "<pinecone api key>",
  "PINECONE_ENVIRONMENT": "<pinecone environment>", //us-central1-gcp
  "TABLE_NAME": "<pinecone index name>",
  
  "OBJECTIVE": "Create a story with image prompts to create a book for 6 years old.",
  "INITIAL_TASK": "Develop a task list"
}
```

## License
This plugin is based on the BabyAGI project by @yoheinakajima (https://github.com/yoheinakajima). Please refer to their repository for licensing information.

## Acknowledgments
This plugin is based on the BabyAGI project by [@yoheinakajima] A big thank you to the author for their original work.