using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace Nexus.Ingest.Backlog.Azdo;

public class AzdoService
{
    private readonly VssConnection connection;
    private readonly WorkItemTrackingHttpClient client;

    public AzdoService(string organizationUrl, VssCredentials credentials)
    {
        
        connection = new VssConnection(new Uri(organizationUrl), credentials);

       
        client = connection.GetClient<WorkItemTrackingHttpClient>();
    }

    public async Task<IEnumerable<WorkItem>> GetNewBugsAsync(string projectName)
    {
        try
        {
            
            var queryHierarchyItems = await client.GetQueriesAsync(projectName, depth: 2);

            
            var myQueriesFolder = queryHierarchyItems
                .FirstOrDefault(qhi => qhi.Name.Equals("My Queries", StringComparison.OrdinalIgnoreCase));

            if (myQueriesFolder == null)
            {
                throw new InvalidOperationException("'My Queries' folder not found in project.");
            }

            const string queryName = "New Bugs Query";

            
            var existingQuery = myQueriesFolder.Children?
                .FirstOrDefault(qhi => qhi.Name.Equals(queryName, StringComparison.OrdinalIgnoreCase));

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
                          AND [System.WorkItemType] = 'Bug' 
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

            // Execute query and get results
            var queryResult = await client.QueryByIdAsync(query.Id);

            if (!queryResult.WorkItems.Any())
            {
                return Enumerable.Empty<WorkItem>();
            }

            // Batch process work items for efficiency
            const int batchSize = 100;
            var allWorkItems = new List<WorkItem>();

            for (int skip = 0; skip < queryResult.WorkItems.Count(); skip += batchSize)
            {
                var batch = queryResult.WorkItems.Skip(skip).Take(batchSize);
                var workItemIds = batch.Select(wir => wir.Id).ToArray();

                // Get detailed work item information
                var workItems = await client.GetWorkItemsAsync(
                    ids: workItemIds,
                    fields: new[] { "System.Id", "System.Title", "System.State",
                                   "System.AssignedTo", "System.CreatedDate" });

                allWorkItems.AddRange(workItems);
            }

            return allWorkItems;
        }
        catch (Exception ex)
        {
            // Log error appropriately in real applications
            throw new InvalidOperationException($"Failed to retrieve work items: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Properly dispose of resources
    /// </summary>
    public void Dispose()
    {
        client?.Dispose();
        connection?.Dispose();
    }
}
