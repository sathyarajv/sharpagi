# SharpAGI

## Description

SharpAGI is a C# library based on the BabyAGI project by [@yoheinakajima]. 

## Quickstart

Use these settings in user secrets to run the code.

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

License
This plugin is based on the BabyAGI project by @yoheinakajima (https://github.com/yoheinakajima). Please refer to their repository for licensing information.

Acknowledgments
This plugin is based on the BabyAGI project by [@yoheinakajima] A big thank you to the author for their original work.