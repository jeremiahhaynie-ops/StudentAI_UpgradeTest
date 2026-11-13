using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace StudentAI;

public record ChatMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

public sealed class SavedChat
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "New conversation";
    public DateTime Created { get; set; } = DateTime.Now;
    public List<ChatMessage> Messages { get; set; } = new();
}

public sealed class AxtrazzData
{
    public string Model { get; set; } = "llama3.2";
    public string Style { get; set; } = "Clear, friendly, and concise. Explain IT topics step by step when asked.";
    public float SpeechRate { get; set; } = 1f;
    public string VoiceId { get; set; } = "";
    public string Memory { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Tasks { get; set; } = "";
    public string ActiveId { get; set; } = "";
    public List<SavedChat> Chats { get; set; } = new();
    public Dictionary<string, string> CustomApps { get; set; } = new();
}

public static class AxtrazzStore
{
    static readonly SemaphoreSlim SaveGate = new(1, 1);
    public static string DataPath => Path.Combine(FileSystem.Current.AppDataDirectory, "axtrazz-v2.json");

    public static async Task<AxtrazzData> LoadAsync()
    {
        if (!File.Exists(DataPath)) return new AxtrazzData();
        string text = await File.ReadAllTextAsync(DataPath);
        var data = JsonSerializer.Deserialize<AxtrazzData>(text)
            ?? throw new InvalidDataException("The saved data file is empty or invalid.");
        if (data.Chats is null || data.CustomApps is null ||
            data.Chats.Exists(c => c is null || c.Messages is null))
            throw new InvalidDataException("The saved data structure is invalid.");
        return data;
    }

    public static async Task SaveAsync(AxtrazzData data)
    {
        await SaveGate.WaitAsync();
        try
        {
            string temp = DataPath + ".tmp";
            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(temp, json);
            if (File.Exists(DataPath)) File.Copy(DataPath, DataPath + ".bak", true);
            File.Move(temp, DataPath, true);
        }
        finally { SaveGate.Release(); }
    }
}
