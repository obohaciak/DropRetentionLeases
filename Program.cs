// Source and copyright: https://www.nimy.se/blog/azure-devops-release-retention-lock

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Text;
using Utility.CommandLine;

internal class Program
{
    //true, if you only want to get the retention leases and not delete them
    [Argument('d', "DryRun", "Dry run only; does not delete anything")]
    private static bool DryRun { get; set; }

    //The Private Access Token to use to authenticate to Azure Devops. To create one, follow this article: https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=azure-devops&tabs=Windows
    [Argument('t', "Pat", "Personal Acces Token")]
    private static string? Pat { get; set; }

    //The name of your organization, e.g. msazure
    [Argument('o', "Organization", "Organization name, e.g. msazure")]
    private static string? Organization { get; set; }

    //The name of the project your build pipeline resides in, e.g. One
    [Argument('p', "Project", "Project name, e.g. One")]
    private static string? Project { get; set; }

    //The definition Id of the build pipeline you want to 77696 the locks from
    [Argument('b', "BuildId", "Build Definition Id, e.g. 77696")]
    private static int BuildDefinitionId { get; set; }

    private static async Task<int> Main(string[] args)
    {
        Arguments.Populate();

        if (Pat == default || Organization == default || Project == default || BuildDefinitionId == default)
        {
            Console.WriteLine("One or more required arguments missing. Expected format is:");
            Console.WriteLine("DropRetentionLeases --Organization msazure --Project One --BuildId 77696 --Pat gku3xaljkljwtsqkcuy4djtlycll7gvqyzb3rkvt5hgxbdh3ufq");
            Console.WriteLine("");
            Console.WriteLine("Supported arguments:");
            Console.WriteLine("Short\tLong\t\tFunction");
            Console.WriteLine("-----\t----\t\t--------");

            foreach (ArgumentInfo? item in Arguments.GetArgumentInfo(typeof(Program)))
            {
                Console.WriteLine((string?)$"{item.ShortName}\t{item.LongName}\t\t{item.HelpText}");
            }
            return 1;
        }

        using HttpClient client = new HttpClient();

        //Encode your personal access token
        string credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{""}:{Pat}"));

        client.BaseAddress = new Uri($"https://dev.azure.com/{Organization}/{Project}/");
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        //Check that the build pipeline we are targeting exists
        HttpResponseMessage response = await client.GetAsync($"_apis/build/builds?definitions={BuildDefinitionId}&statusFilter=completed&api-version=7.0");

        if (response.IsSuccessStatusCode)
        {
            //Build pipeline exists. Go ahead and get all the leases connected to this pipeline
            string leasesUrl = $"_apis/build/retention/leases?api-version=6.1-preview&definitionId={BuildDefinitionId}";
            HttpResponseMessage leases = await client.GetAsync(leasesUrl);

            //Parse the resulting leases id to be used for additional API calls
            JObject? leasesObject = JsonConvert.DeserializeObject<JObject>(await leases.Content.ReadAsStringAsync());

            List<string> leaseIds = (from p in leasesObject["value"] select (string)p["leaseId"]).ToList();

            Console.WriteLine($"Found {leaseIds.Count} leases. Proceding to delete: {!DryRun}");

            if (!DryRun)
            {
                int batch = 0;
                int batchSize = 100;

                while (leaseIds.Count > 0)
                {
                    //Join the lease Ids to a string for use in the delete call
                    string slice = string.Join(",", leaseIds.Skip(batch * batchSize).Take(batchSize));

                    //Delete all the leases in one call
                    string deleteUrl = $"_apis/build/retention/leases?ids={slice}&api-version=7.0";
                    HttpResponseMessage deleteResponse = await client.DeleteAsync(deleteUrl);

                    if (deleteResponse.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"Deleted {batchSize} leases in batch {batch + 1}.");
                    }
                    else
                    {
                        Console.WriteLine($"Deleting leases failed {deleteResponse.StatusCode}");
                    }

                    batch++;
                }

                Console.WriteLine($"All retention leases deleted. Try to remove the build pipeline now.");
            }
        }

        Console.WriteLine($"No build pipeline found with id {BuildDefinitionId}");

        return 0;
    }
}