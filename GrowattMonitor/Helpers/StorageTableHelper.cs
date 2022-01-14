﻿using Azure.Data.Tables;


namespace GrowattMonitor;

public class StorageTableHelper
{

    private readonly string? _storageConnectionString;

    public StorageTableHelper() { }

    public StorageTableHelper(string storageConnectionString)
    {
        _storageConnectionString = storageConnectionString;
    }


    public async Task<TableClient> GetTableAsync(string tablename)
    {
        Console.Write("Checking storage...");

        var serviceClient = new TableServiceClient(_storageConnectionString);

        var result = await serviceClient.CreateTableIfNotExistsAsync(tablename);

        if (result != null)
        {
            Console.WriteLine("table '{0}' created", tablename);
        }
        else
        {
            Console.WriteLine("table '{0}' exists", tablename);
        }

        var table = serviceClient.GetTableClient(tablename);



        return table;
    }
}
