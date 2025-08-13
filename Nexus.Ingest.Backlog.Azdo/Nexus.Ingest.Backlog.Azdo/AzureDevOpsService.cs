using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace Nexus.Ingest.Backlog.Azdo;

public class AzureDevOpsService
{
    private readonly VssConnection connection;
    private readonly WorkItemTrackingHttpClient client;

    public AzureDevOpsService(string organizationUrl, VssCredentials credentials)
    {
        connection = new VssConnection(new Uri(organizationUrl), credentials);
        client = connection.GetClient<WorkItemTrackingHttpClient>();
    }

    public async Task<IEnumerable<WorkItem>> GetNewFeaturesAsync(string projectName)
    {
        try
        {
            var queryHierarchyItems = await client.GetQueriesAsync(projectName, depth: 2);

            //TODO: make this an arg or config
            var myQueriesFolder = queryHierarchyItems
                .FirstOrDefault(i => i.Name.Equals("My Queries", StringComparison.OrdinalIgnoreCase));

            if (myQueriesFolder == null)
            {
                throw new InvalidOperationException("'My Queries' folder not found in project.");
            }

            const string queryName = "New Features Query"; //TODO: make this an arg or config

            var existingQuery = myQueriesFolder.Children?
                .FirstOrDefault(i => i.Name.Equals(queryName, StringComparison.OrdinalIgnoreCase));


            QueryHierarchyItem query;

            if (existingQuery == null)
            {
                query = new QueryHierarchyItem
                {
                    Name = queryName,
                    Wiql = @"
                        SELECT [System.Id], [System.WorkItemType], [System.Title], 
                               [System.AssignedTo], [System.State], [System.Tags] 
                        FROM WorkItems 
                        WHERE [System.TeamProject] = @project 
                          AND [System.WorkItemType] = 'Feature' 
                          AND [System.State] = 'New'
                        ORDER BY [System.CreatedDate] DESC",
                    IsFolder = false
                };

                query = await client.CreateQueryAsync(query, projectName, myQueriesFolder.Name);
            }
            else
            {
                query = existingQuery;
            }

            var result = await client.QueryByIdAsync(query.Id);

            if (!result.WorkItems.Any())
            {
                return Enumerable.Empty<WorkItem>();
            }

            const int batchSize = 100;
            var allWorkItems = new List<WorkItem>();

            for (int skip = 0; skip < result.WorkItems.Count(); skip += batchSize)
            {
                var batch = result.WorkItems.Skip(skip).Take(batchSize);
                var workItemIds = batch.Select(i => i.Id).ToArray();

                var workItems = await client.GetWorkItemsAsync(
                    ids: workItemIds,
                    fields: new[] { "System.Id", "System.Title", "System.State",
                                   "System.AssignedTo", "System.CreatedDate" });
                allWorkItems.AddRange(workItems);
            }
            return allWorkItems;
        }
        catch(Exception ex) 
        {
            throw new InvalidOperationException($"Failed to retrive work items: {ex.Message}", ex);
        }
    }

    public void Dispose()
    {
        client?.Dispose();
        connection?.Dispose();
    }
}
