using Azure;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;

namespace GrowattMonitor.Helpers;

public class StorageTableHelper
{

    private readonly string _storageConnectionString;

    public StorageTableHelper() { }

    public StorageTableHelper(string storageConnectionString)
    {
        _storageConnectionString = storageConnectionString;
    }


    public async Task<TableClient> GetTableAsync(string tablename)
    {
        Console.Write("Checking storage...");

        TableServiceClient serviceClient = new (_storageConnectionString);

        Response<TableItem> result = await serviceClient.CreateTableIfNotExistsAsync(tablename);

        if (result != null)
        {
            Console.WriteLine("table '{0}' created", tablename);
        }
        else
        {
            Console.WriteLine("table '{0}' exists", tablename);
        }

        TableClient table = serviceClient.GetTableClient(tablename);


        return table;
    }
}
