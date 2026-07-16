using System.Text.Json;
using System.Text.Json.Serialization;

namespace MedicineReminder.Serialization;

/// <summary>
/// Serializes/deserializes <see cref="DateOnly"/> using the strict "yyyy-MM-dd"
/// format, used for the ReminderDate field in reminders.json.
/// </summary>
public sealed class DateOnlyJsonConverter : JsonConverter<DateOnly>
{
    private const string Format = "yyyy-MM-dd";

    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException("ReminderDate must be a non-empty date string in 'yyyy-MM-dd' format.");
        }

        if (!DateOnly.TryParseExact(value, Format, out DateOnly result))
        {
            throw new JsonException($"Invalid ReminderDate '{value}'. Expected format '{Format}' (e.g. 2026-08-10).");
        }

        return result;
    }

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(Format));
    }
}
