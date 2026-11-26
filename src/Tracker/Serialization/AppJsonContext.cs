using System.Text.Json.Serialization;
using Tracker.Models;

namespace Tracker.Serialization;

/// <summary>Source-generated JSON metadata: trim-safe for WebAssembly publish and avoids runtime reflection.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(UserPreferences))]
[JsonSerializable(typeof(HydrationHistory))]
[JsonSerializable(typeof(ReminderState))]
[JsonSerializable(typeof(TrackerBackup))]
[JsonSerializable(typeof(EmailJsRequest))]
internal partial class AppJsonContext : JsonSerializerContext;
