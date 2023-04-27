using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using OpenAI.GPT3;
using OpenAI.GPT3.Managers;
using OpenAI.GPT3.ObjectModels;
using OpenAI.GPT3.ObjectModels.RequestModels;
using pinecone;
using pinecone.Models;
using System.Diagnostics;

namespace sharpagi
{
    public class Sharpagi
    {
        private readonly Action<string, ConsoleColor?> printOutput;

        public string objective = string.Empty;
        public string openaiApiKey = string.Empty;
        public string openaiApiModel = string.Empty;
        public IPineconeProvider pinecone;
        public string pineconeApiKey = string.Empty;
        public string pineconeEnvironment = string.Empty;
        public Queue<Dictionary<string, string>> taskList = new();
        public string yourTableName = string.Empty;
        public string initialTask = string.Empty;
        public string pineconeProjectName = string.Empty;

        public Sharpagi(Action<string, ConsoleColor?> outputCallback)
        {
            printOutput = outputCallback;
        }

        public Sharpagi(Action<string> outputCallback)
        : this((output, color) => outputCallback(output))
        {
        }

        public async Task Agent(IConfiguration configuration)
        {

            openaiApiKey = configuration["OPENAI_API_KEY"];
            openaiApiModel = configuration["OPENAI_API_MODEL"];
            pineconeApiKey = configuration["PINECONE_API_KEY"];
            pineconeEnvironment = configuration["PINECONE_ENVIRONMENT"];
            yourTableName = configuration["TABLE_NAME"];
            objective = configuration["OBJECTIVE"];
            initialTask = configuration["INITIAL_TASK"] ?? configuration["FIRST_TASK"];


            if (pinecone == null)
            {
                pinecone = new PineconeProvider(pineconeApiKey, pineconeEnvironment);
            }


            if(string.IsNullOrEmpty(openaiApiKey)) throw new ArgumentException("OPENAI_API_KEY environment variable is missing from UserSecrets");
            if(string.IsNullOrEmpty(openaiApiModel)) throw new ArgumentException("OPENAI_API_MODEL environment variable is missing from UserSecrets");
            if (openaiApiModel.ToLower().Contains("gpt-4"))
            {
                printOutput("*****USING GPT-4. POTENTIALLY EXPENSIVE. MONITOR YOUR COSTS*****", ConsoleColor.Red);
            }

            // Print OBJECTIVE
            printOutput("\n*****OBJECTIVE*****\n", ConsoleColor.Blue);
            printOutput(objective, null);

            printOutput("\nInitial task: " + initialTask, ConsoleColor.Yellow);

            // Create Pinecone index
            var indexes = await pinecone.ListIndexes();
            if (indexes.Any(i => i != yourTableName))
            {
                await pinecone.CreateIndex(new CreateRequest
                {
                    Name = yourTableName,
                    Dimension = 1536,
                    Metric = "cosine",
                    PodType = "p1"
                });
            }
            //Get Pinecone project for Index processing.
            pineconeProjectName = await pinecone.GetProjectName();

            // Add the first task
            var firstTask = new Dictionary<string, string>
            {
                ["task_id"] = "1",
                ["task_name"] = initialTask
            };

            AddTask(firstTask);

            // Main loop
            var taskIdCounter = 1;
            while (true)
            {
                if (taskList.Count > 0)
                {
                    // Print the task list
                    printOutput("\n*****TASK LIST*****\n", ConsoleColor.Magenta);

                    foreach (var t in taskList)
                    {
                        printOutput(t["task_id"] + ": " + t["task_name"], null);
                    }

                    // Step 1: Pull the first task
                    var task = taskList.Dequeue();
                    printOutput("\n*****NEXT TASK*****\n", ConsoleColor.Green);
                    printOutput(task["task_id"] + ": " + task["task_name"], null);

                    // Send to execution function to complete the task based on the context
                    var result = await ExecutionAgent(objective, task["task_name"]);
                    var thisTaskId = int.Parse(task["task_id"]);
                    printOutput("\n*****TASK RESULT*****\n", ConsoleColor.Yellow);
                    printOutput(result?.ToString(), null);


                    // Step 2: Enrich result and store in Pinecone
                    var enrichedResult = new Dictionary<string, object>
                    {
                        ["data"] = result
                    };
                    var resultId = $"result_{task["task_id"]}";
                    var vector = await GetAdaEmbedding(enrichedResult["data"]);
                    await pinecone.Upsert(yourTableName, pineconeProjectName, new UpsertRequest
                    {
                        Vectors = new List<Vector>(){new Vector
                    {
                        Id = resultId,
                        Values = vector.ToList(),
                        Metadata = new Dictionary<string, string>
                        {
                            ["task"] = task["task_name"],
                            ["result"] = result.ToString()
                        }
                    } },
                        Namespace = objective
                    });

                    // Step 3: Create new tasks and reprioritize task list
                    var newTasks = await TaskCreationAgent(
                                   objective,
                                   enrichedResult,
                                   task["task_name"],
                                   taskList.Select(t => t["task_name"]).ToList()
                               );

                    foreach (var newTask in newTasks)
                    {
                        taskIdCounter += 1;
                        newTask["task_id"] = taskIdCounter.ToString();
                        AddTask(newTask);
                    }
                    await PrioritizationAgent(thisTaskId);
                }
                Thread.Sleep(1000);  // Sleep before checking the task list again
            }
        }

        public void AddTask(Dictionary<string, string> task)
        {
            taskList.Enqueue(task);

        }
        public async Task<List<string>> ContextAgent(string query, int n)
        {
            var queryEmbedding = await GetAdaEmbedding(query);
            var results = await pinecone.GetQuery(yourTableName, pineconeProjectName, new QueryRequest { Vector = queryEmbedding.ToList(), TopK = n, IncludeMetadata = true, Namespace = objective });
            var sortedResults = results.Matches.OrderByDescending(m => m.Score);
            return sortedResults.Select(m => m.Metadata["task"].ToString()).ToList();
        }

        public async Task<object> ExecutionAgent(string objective, string task)
        {
            var context = await ContextAgent(query: objective, n: 5);
            var prompt = $@"
            You are an AI who performs one task based on the following objective: {objective}.
            Take into account these previously completed tasks: {context}.
            Your task: {task}
            Response:";
            return await openai_call(prompt, temperature: 0.7f, maxTokens: 2000);

        }

        public async Task<double[]> GetAdaEmbedding(object data)
        {
            var openAiService = new OpenAIService(new OpenAiOptions()
            {
                ApiKey = openaiApiKey
            });
            var embeddingResult = await openAiService.Embeddings.CreateEmbedding(new EmbeddingCreateRequest()
            {
                Input = JsonConvert.SerializeObject(data),
                Model = Models.TextSearchAdaDocV1
            });
            if (embeddingResult.Successful)
            {
                return embeddingResult.Data.FirstOrDefault().Embedding.ToArray();
            }
            else
            {
                if (embeddingResult.Error == null)
                {
                    throw new Exception("Unknown Error");
                }
                printOutput($"{embeddingResult.Error.Code}: {embeddingResult.Error.Message}", null);
            }
            return null;
        }

        public async Task<string> openai_call(string prompt, string model = "gpt-3.5-turbo", float temperature = 0.5f, int maxTokens = 100)
        {
            var openAiService = new OpenAIService(new OpenAiOptions()
            {
                ApiKey = openaiApiKey
            });

            while (true)
            {
                try
                {
                    if (model.StartsWith("llama"))
                    {
                        // Spawn a subprocess to run llama.cpp
                        var cmd = new List<string> { "llama/main", "-p", prompt };
                        var result = await RunCommandAsync(string.Join(" ", cmd));
                        return result.stdout.Trim();
                    }
                    else if (!model.StartsWith("gpt-"))
                    {
                        // Use completion API
                        var response = await openAiService.Completions.CreateCompletion(new CompletionCreateRequest()
                        {
                            Model = model,
                            Prompt = prompt,
                            Temperature = temperature,
                            MaxTokens = maxTokens,
                            TopP = 1,
                            FrequencyPenalty = 0,
                            PresencePenalty = 0
                        });
                        return response.Choices.FirstOrDefault()?.Text.Trim() ?? string.Empty;
                    }
                    else
                    {
                        // Use chat completion API
                        var messages = new List<ChatMessage> { new ChatMessage("system", prompt) };
                        var response = await openAiService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest()
                        {
                            Model = model,
                            Messages = messages,
                            Temperature = temperature,
                            MaxTokens = maxTokens,
                            N = 1,
                            Stop = null
                        });
                        if (response.Successful)
                            return response.Choices.FirstOrDefault()?.Message.Content.Trim() ?? string.Empty;
                        else
                        {
                            if (response.Error.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
                                await Task.Delay(20000);
                        }
                    }
                }
                catch (Exception ex) when (ex.Message.Contains("rate limit"))
                {
                    printOutput("The OpenAI API rate limit has been exceeded. Waiting 10 seconds and trying again.", null);
                    await Task.Delay(20000); // Wait 10 seconds and try again
                }
            }
        }

        public async Task PrioritizationAgent(int thisTaskId)
        {
            var taskNames = taskList.Select(t => t["task_name"]).ToList();
            var nextTaskId = thisTaskId + 1;
            var prompt = $@"
            You are a task prioritization AI tasked with cleaning the formatting of and reprioritizing the following tasks: {string.Join(", ", taskNames)}.
            Consider the ultimate objective of your team:{objective}.
            Do not remove any tasks. Return the result as a numbered list, like:
            #. First task
            #. Second task
            Start the task list with number {nextTaskId}.";
            var response = await openai_call(prompt);
            var newTasks = response.Split('\n');
            taskList = new Queue<Dictionary<string, string>>(newTasks.Select((t, index) => new Dictionary<string, string> { { "task_id", (index + nextTaskId).ToString() }, { "task_name", (t.Trim().Length >= 2) ? t.Trim().Substring(2) : t.Trim() } }));
        }

        public async Task<(string stdout, string stderr)> RunCommandAsync(string command)
        {
            var psi = new ProcessStartInfo("bash", $"-c \"{command}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            var process = Process.Start(psi);
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await Task.WhenAll(stdoutTask, stderrTask);
            process.WaitForExit();
            return (stdoutTask.Result, stderrTask.Result);
        }

        public async Task<List<Dictionary<string, string>>> TaskCreationAgent(string objective, Dictionary<string, object> result, string taskDescription, List<string> taskList)
        {
            var prompt = $@"
            You are a task creation AI that uses the result of an execution agent to create new tasks with the following objective: {objective},
            The last completed task has the result: {result}.
            This result was based on this task description: {taskDescription}. These are incomplete tasks: {string.Join(", ", taskList)}.
            Based on the result, create new tasks to be completed by the AI system that do not overlap with incomplete tasks.
            Return the tasks as an array.";
            var response = await openai_call(prompt);
            var newTasks = response.Split('\n');
            return newTasks.Select(t => new Dictionary<string, string> { { "task_name", t.Trim() } }).ToList();
        }

        // Method to check if the input is safe
        //public bool IsInputSafe(string input)
        //{
        //    // Check for blacklisted words in the input
        //    foreach (var word in blacklistedWords)
        //    {
        //        if (input.ToLowerInvariant().Contains(word.ToLowerInvariant()))
        //        {
        //            return false; // Return false if a blacklisted word is found
        //        }
        //    }
        //    return true; // Return true if input is safe
        //}

        // Method to check if the output is safe
        //public bool IsOutputSafe(string output)
        //{
        //    // Check for blacklisted words in the output
        //    foreach (var word in blacklistedWords)
        //    {
        //        if (output.ToLowerInvariant().Contains(word.ToLowerInvariant()))
        //        {
        //            return false; // Return false if a blacklisted word is found
        //        }
        //    }
        //    return true; // Return true if output is safe
        //}
    }
}