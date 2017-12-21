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

        public static async Task Main(string[] args)
        {
            Configure();

            string templatePrefix = null;

            if (args.Length == 1)
            {
                templatePrefix = args[0];
                Console.WriteLine($"Template prefix set to [{templatePrefix}]");
            }
            else
            {
                Console.Write("Do you want to filter the templates based on prefix (Y/n)? ");
                var response = Console.ReadLine();
                if (response == "Y")
                {
                    Console.Write("Template prefix set to: ");
                    templatePrefix = Console.ReadLine();
                }
            }

            var templates = await GetMatchingTemplates(templatePrefix);
            await Execute(templates);

            Console.WriteLine("\n\nPress <Enter> to QUIT.");
            Console.ReadLine();
        }

        private static async Task<TemplatesModel> GetMatchingTemplates(string prefix = null)
        {
            var apiKey = Settings.SourceAccountApiKey;
            var sourceAccountClient = new SendGridClient(apiKey);

            Console.WriteLine("Getting templates from Source Account");
            var result = await GetRequest<TemplatesModel>(sourceAccountClient, "templates");
            var filteredResult = (prefix != null) ? result.Templates.Where(t => t.Name.StartsWith(prefix)) : result.Templates;
            foreach (var item in filteredResult)
            {
                Console.WriteLine($"Found {item.Name}: {item.Id}");
            }
            return new TemplatesModel { Templates = filteredResult.ToList() };
        }

        private static void Configure()
        {
            var builder = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .AddJsonFile("settings.json", optional: true)
                .AddUserSecrets<Program>();
            var configuration = builder.Build();
            Settings = new SendGridSettings
            {
                SourceAccountApiKey = configuration.GetValue<string>("SendGrid:SourceAccountApiKey"),
                TargetAccountApiKey = configuration.GetValue<string>("SendGrid:TargetAccountApiKey")
            };
        }

        private static async Task Execute(TemplatesModel model)
        {
            var apiKey = Settings.SourceAccountApiKey;
            var sourceAccountClient = new SendGridClient(apiKey);

            var runId = $"{DateTime.Now:yyyyMMddHHmm}";
            var savedDirectory = $"{Directory.GetCurrentDirectory()}\\Templates\\{runId}";
            Directory.CreateDirectory(savedDirectory);
            
            // Step 2. Recursively retrieve each template
            foreach (var item in model.Templates)
            {
                Console.WriteLine($"\nRetrieving {item.Name}: {item.Id}");
                var template = await GetRequest<Template>(sourceAccountClient, $"templates/{item.Id}");

                // Step 3.Backup templates to an external file
                var activeTemplate = template.Versions.First(v => v.Active == 1);
                File.WriteAllText($"{savedDirectory}\\{item.Name}.json", JsonConvert.SerializeObject(activeTemplate, Formatting.Indented));
            }
            Highlight($"\nTemplates saved in {savedDirectory} directory");

            var output = new List<string>();

            apiKey = Settings.TargetAccountApiKey;
            var targetAccountClient = new SendGridClient(apiKey);
            // Step 4. Create an empty template if don't exist in Target Account
            var templatesInTarget = await GetRequest<TemplatesModel>(targetAccountClient, "templates");
            foreach (var fileFullPath in Directory.GetFiles($"{savedDirectory}"))
            {
                var fileName = Path.GetFileNameWithoutExtension(fileFullPath);
                var template = templatesInTarget.Templates.SingleOrDefault(t => t.Name == fileName);

                if (template == null)
                {
                    Console.WriteLine($"\nCreating template {fileName}");
                    template = await PostRequest<Template>(targetAccountClient, "templates", new Template { Name = fileName });
                }

                // Step 5. Populate template
                Console.WriteLine($"Populating template {fileName}");
                var json = JsonConvert.DeserializeObject<Version>(File.ReadAllText(fileFullPath));
                var templateReference = json.Name;
                json.Name = $"{templateReference}-{runId}";
                var version = await PostRequest<Version>(targetAccountClient, $"templates/{template.Id}/versions", json);
                Console.WriteLine($" Version {version.Name}: {version.Id}");

                output.Add($"\"{templateReference}\": \"{template.Id}\"");
            }
            Highlight("\nOutput:");
            foreach (var item in output)
            {
                Console.WriteLine(item);
            }
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
            var integerStatusCode = (int)statusCode;
            var isSuccessStatusCode = ((integerStatusCode >= 200) && (integerStatusCode <= 299));
            if (!isSuccessStatusCode)
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
