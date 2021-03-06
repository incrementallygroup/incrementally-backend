using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Database.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace incrementally.Services
{
    public class CosmosDbService
    {
        private Dictionary<string, Container> _containers;

        public CosmosDbService(
          CosmosClient dbClient,
          string databaseName,
          List<string> containerNames)
        {
            _containers = new Dictionary<string, Container>();
            foreach (var containerName in containerNames)
            {
                _containers.Add(containerName, dbClient.GetContainer(databaseName, containerName));
            }
        }

        public async Task AddRecordingAsync(RecordingEntry recordingEntry, RecordingMetadata recordingMetadata)
        {
            await _containers["recordings"].CreateItemAsync(recordingEntry, new PartitionKey(recordingEntry.Id));
            await _containers["recording_metadata"].CreateItemAsync(recordingMetadata, new PartitionKey(recordingEntry.Id));
        }

        public async Task<List<RecordingEntry>> GetRecordings(string id)
        {
            var query = new QueryDefinition("SELECT * FROM c");
            if (id != null)
            {
                query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id")
                  .WithParameter("@id", id);
            }
            var resultSetIterator = _containers["recordings"].GetItemQueryIterator<RecordingEntry>(query);
            var results = new List<RecordingEntry>();
            while (resultSetIterator.HasMoreResults)
            {
                results.AddRange((await resultSetIterator.ReadNextAsync()));
            }
            return results;
        }

        public async Task<RecordingEntry> GetItemAsync(string id)
        {
            ItemResponse<RecordingEntry> response = await this._containers["recordings"].ReadItemAsync<RecordingEntry>(id, new PartitionKey(id));
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            return response.Resource;
        }

        public async Task<IEnumerable<RecordingEntry>> GetItemsAsync(string queryString)
        {
            var query = this._containers["recordings"].GetItemQueryIterator<RecordingEntry>(new QueryDefinition(queryString));
            List<RecordingEntry> results = new List<RecordingEntry>();
            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();

                results.AddRange(response.ToList());
            }

            return results;
        }

        public async Task<IEnumerable<RecordingMetadata>> GetTopRecordingMetadata()
        {
            var query = new QueryDefinition("SELECT * FROM c ORDER BY c.createdAt DESC OFFSET 0 LIMIT 10");
            var resultSetIterator = _containers["recording_metadata"].GetItemQueryIterator<RecordingMetadata>(query);
            var results = new List<RecordingMetadata>();
            while (resultSetIterator.HasMoreResults)
            {
                results.AddRange(await resultSetIterator.ReadNextAsync());
            }
            return results;
        }

        public async Task<IEnumerable<RecordingMetadata>> GetRecordingMetadata(string id)
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id")
                .WithParameter("@id", id);
            var resultSetIterator = _containers["recording_metadata"].GetItemQueryIterator<RecordingMetadata>(query);
            var results = new List<RecordingMetadata>();
            while (resultSetIterator.HasMoreResults)
            {
                results.AddRange((await resultSetIterator.ReadNextAsync()));
            }
            return results;
        }

        /*
    public async Task<IEnumerable<RecordingMetadata>> GetRecordingMetadata(string userId, string id)
    {
      var query = new QueryDefinition("SELECT * FROM c");
      if (userId != null) {
        query = new QueryDefinition("SELECT * FROM c WHERE c.createdBy = @userId")
          .WithParameter("@userId", userId);
      }
      if (id != null) {
        query = new QueryDefinition("SELECT * FROM c WHERE c.createdBy = @userId AND c.id = @id")
          .WithParameter("@userId", userId)
          .WithParameter("@id", id);
      }
      var resultSetIterator = _containers["recording_metadata"].GetItemQueryIterator<RecordingMetadata>(query);
      var results = new List<RecordingMetadata>();
      while (resultSetIterator.HasMoreResults)
      {
        results.AddRange((await resultSetIterator.ReadNextAsync()));
      }
      return results;
    }
    */
    }
}
