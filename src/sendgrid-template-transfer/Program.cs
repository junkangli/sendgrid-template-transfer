using Newtonsoft.Json;
using SendGrid;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace SendgridTemplateTransfer
{
    public class Program
    {
        private static SendGridSettings Settings;

        public static void Main(string[] args)
        {
            Configure();

            var templates = GetTemplates().Result;
            Console.WriteLine("\n\nExecuting this process will wipe out of all templates before the transfer takes place.");
            Console.WriteLine("Are you sure you want to continue? (Y/n)");
            var response = Console.ReadLine();
            if (response == "Y")
            {
                CleanUp(templates).Wait();
                Execute().Wait();
            }
            Console.WriteLine("\n\nPress <Enter> to QUIT.");
            Console.ReadLine();
        }

        private static void Configure()
        {
            var builder = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .AddJsonFile("settings.json", optional: true)
                .AddUserSecrets("aspnet-sendgrid-template-transfer-20170330040601");
            var configuration = builder.Build();
            Settings = new SendGridSettings
            {
                SourceAccountApiKey = configuration.GetValue<string>("SendGrid:SourceAccountApiKey"),
                TargetAccountApiKey = configuration.GetValue<string>("SendGrid:TargetAccountApiKey")
            };
        }

        private static async Task Execute()
        {
            var apiKey = Settings.SourceAccountApiKey;
            var sourceAccountClient = new SendGridClient(apiKey);

            // Step 1. Retrieve all available templates from Account1
            Console.WriteLine("Retrieving all templates");
            var result = await GetRequest<TemplatesModel>(sourceAccountClient, "templates");
            var templates = new List<Template>();
            // Step 2. Recursively retrieve each template from Account1
            foreach (var item in result.Templates)
            {
                Console.WriteLine($"\nRetrieving {item.Name}: {item.Id}");
                var template = await GetRequest<Template>(sourceAccountClient, $"templates/{item.Id}");

                // Step 3.Backup templates to an external file
                var version = template.Versions.First();
                File.WriteAllText($"Templates\\{item.Name}.json", JsonConvert.SerializeObject(version, Formatting.Indented));
            }
            Highlight($"\nTemplates saved in {Directory.GetCurrentDirectory()}\\Templates directory");

            var output = new List<string>();

            apiKey = Settings.TargetAccountApiKey;
            var targetAccountClient = new SendGridClient(apiKey);
            // Step 4. Create an empty template in Account2
            foreach (var fileFullPath in Directory.GetFiles($"{Directory.GetCurrentDirectory()}\\Templates"))
            {
                var fileName = Path.GetFileNameWithoutExtension(fileFullPath);
                Console.WriteLine($"\nCreating template {fileName}");
                var template = await PostRequest<Template>(targetAccountClient, "templates", new Template { Name = $"{DateTime.Now:yyyyMMddhhmm}-{fileName}" });

                // Step 5. Populate template
                Console.WriteLine($"Populating template");
                var json = File.ReadAllText(fileFullPath);
                var version = await PostRequest<Version>(targetAccountClient, $"templates/{template.Id}/versions", json);
                Console.WriteLine($" Version {version.Name}: {version.Id}");

                output.Add($"\"{template.Name}\": \"{template.Id}\"");
            }
            Highlight("\nOutput:");
            foreach (var item in output)
            {
                Console.WriteLine(item);
            }
        }

        private static async Task<TemplatesModel> GetTemplates()
        {
            var apiKey = Settings.TargetAccountApiKey;
            var targetAccountClient = new SendGridClient(apiKey);
            Console.WriteLine("Listing target templates");
            var result = await GetRequest<TemplatesModel>(targetAccountClient, "templates");
            foreach (var item in result.Templates)
            {
                Console.WriteLine($"Found {item.Name}: {item.Id}");
            }
            return result;
        }

        private static async Task CleanUp(TemplatesModel model)
        {
            var apiKey = Settings.TargetAccountApiKey;
            var targetAccountClient = new SendGridClient(apiKey);
            Console.WriteLine("Cleaning Up");
            foreach (var item in model.Templates)
            {
                Console.WriteLine($"\nRemoving {item.Name}: {item.Id}");
                foreach (var version in item.Versions)
                {
                    await targetAccountClient.RequestAsync(SendGridClient.Method.DELETE, urlPath: $"templates/{item.Id}/versions/{version.Id}");
                }
                await targetAccountClient.RequestAsync(SendGridClient.Method.DELETE, urlPath: $"templates/{item.Id}");
            }
            Console.WriteLine("\nClean Up Complete");
        }

        private static async Task<TResponse> GetRequest<TResponse>(SendGridClient client, string urlPath)
        {
            var response = await client.RequestAsync(SendGridClient.Method.GET, urlPath: urlPath);
            var content = await response.Body.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<TResponse>(content);
        }

        private static async Task<TResponse> PostRequest<TResponse>(SendGridClient client, string urlPath, object request)
        {
            string json = null;
            if (request is string)
            {
                json = request as string;
            }
            else
            {
                json = JsonConvert.SerializeObject(request);
            }
            var response = await client.RequestAsync(SendGridClient.Method.POST, urlPath: urlPath, requestBody: json);
            EnsureSuccessStatusCode(response);
            var content = await response.Body.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<TResponse>(content);
        }

        private static void EnsureSuccessStatusCode(Response response)
        {
            var statusCode = response.StatusCode;
            if (!((int)statusCode >= 200) && ((int)statusCode <= 299))
            {
                var content = response.Body.ReadAsStringAsync().Result;
                throw new Exception(content);
            }
        }

        private static void Highlight(string value)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(value);
            Console.ResetColor();
        }
    }
}
