using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Octokit;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace csharpfunctions.DataProcessing
{
    public static class DataProcessing_2
    {
        //http://localhost:7071/admin/functions/DataProcessing_2
        [FunctionName("DataProcessing_2")]
        public static async Task Run([TimerTrigger("0 0 1 */1 * *")]TimerInfo myTimer,
            [CosmosDB(
                databaseName: "GithubStats",
                collectionName: "DocsRepo",
                ConnectionStringSetting = "CosmosDBConnection")]  IAsyncCollector<IssueData> outputData,
            ILogger log)
        {
            log.LogInformation($"C# DataProcessing_2 function executed at: {DateTime.Now}");

            var client = new GitHubClient(new ProductHeaderValue("fsharpfunctions"));
            var tokenAuth = new Credentials(Environment.GetEnvironmentVariable("GithubToken")); // NOTE: not real token
            client.Credentials = tokenAuth;

            log.LogInformation("Retrieving Github Issues");

            var request = new SearchIssuesRequest()
            {
                PerPage = 100,
                Page = 1,
                Type = IssueTypeQualifier.Issue,
                State = ItemState.Open
            };
            request.Repos.Add("MicrosoftDocs", "azure-docs");
            var openIssues = await client.Search.SearchIssues(request);
            log.LogInformation($"Retrieved {openIssues.Items.Count()} Issues"); ;

            log.LogInformation(client.GetLastApiInfo().RateLimit.Remaining.ToString());

            var byPriority = openIssues.Items
                .SelectMany(o => o.Labels)
                .Where(l => l.Name.Contains("Pri"))
                .GroupBy(p => p.Name)
                .ToDictionary(g => g.Key, g => g.Count());
            
            var document = new IssueData()
            {
                id = Guid.NewGuid().ToString(),
                Source = "CSharp",
                EntryType = "ByPriority",
                Timestamp = DateTime.UtcNow,
                TotalOpenIssues = openIssues.Items.Count(),
                MissingTags = openIssues.Items.Count(o => o.Labels.Count() == 0),
                CountByPriority = byPriority

            };

            await outputData.AddAsync(document);
        }
    }
}
