using System;
using System.Collections.Generic;
using UnityEngine;
using Supabase;
using System.Threading.Tasks;
using Supabase.Postgrest.Attributes;

[Table("test")]
public class Item : Supabase.Postgrest.Models.BaseModel
{
    [PrimaryKey("id")]
    public int Id { get; set; }

    [Column("created_at")]
    public string CreatedAt { get; set; }
}

public class SupabaseManager : MonoBehaviour
{
    private Supabase.Client _client;
    public string SupabaseUrl = "YOUR_SUPABASE_URL"; 
    public string SupabaseKey = "YOUR_SUPABASE_ANON_KEY"; 

    async void Start()
    {
        Debug.Log("SupabaseManager Start method called");

        if (string.IsNullOrEmpty(SupabaseUrl) || string.IsNullOrEmpty(SupabaseKey))
        {
            Debug.LogError("Supabase URL or Key is not set");
            return;
        }

        Debug.Log("Initializing Supabase...");
        var options = new SupabaseOptions
        {
            AutoRefreshToken = true,
            // PersistSession = true,
        };

        _client = new Supabase.Client(SupabaseUrl, SupabaseKey, options);
        await _client.InitializeAsync();

        Debug.Log("Supabase Initialized");

        await FetchItems();
    }

    async Task FetchItems()
    {
        try
        {
            Debug.Log("Fetching items from Supabase...");
            var response = await _client
                .From<Item>()
                .Select("*")
                .Get();

            if (response.ResponseMessage.IsSuccessStatusCode)
            {
                List<Item> items = response.Models;
                foreach (var item in items)
                {
                    Debug.Log($"Item CreatedAt: {item.CreatedAt}, ID: {item.Id}"); 
                }
            }
            else
            {
                Debug.LogError($"Error fetching items: {response.ResponseMessage.ReasonPhrase}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error: {e.Message}");
        }
    }
}