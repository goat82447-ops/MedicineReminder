using System.Text.Json;
using System.Text.Json.Serialization;

namespace MedicineReminder.Serialization;

/// <summary>
/// Serializes/deserializes <see cref="TimeOnly"/> using the strict "HH:mm"
/// format, used for the ReminderTime field in reminders.json.
/// </summary>
public sealed class TimeOnlyJsonConverter : JsonConverter<TimeOnly>
{
    private const string Format = "HH:mm";

    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException("ReminderTime must be a non-empty time string in 'HH:mm' format.");
        }

        if (!TimeOnly.TryParseExact(value, Format, out TimeOnly result))
        {
            throw new JsonException($"Invalid ReminderTime '{value}'. Expected format '{Format}' (e.g. 09:00).");
        }

        return result;
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(Format));
    }
}
