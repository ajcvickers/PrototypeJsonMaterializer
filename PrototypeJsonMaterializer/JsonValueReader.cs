using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Newtonsoft.Json;

namespace PrototypeJsonMaterializer;

public abstract class JsonValueReader<TValue>
{
    public abstract TValue FromJson(Stream stream, ref byte[] buffer, ref Utf8JsonReader reader);
}

public abstract class SimpleJsonValueReader<TValue> : JsonValueReader<TValue>
{
    public override TValue FromJson(Stream stream, ref byte[] buffer, ref Utf8JsonReader reader)
        => FromJson(ref reader);

    public abstract TValue FromJson(ref Utf8JsonReader reader);
}

public class IPAddressJsonValueReader : SimpleJsonValueReader<IPAddress?>
{
    public override IPAddress FromJson(ref Utf8JsonReader reader)
        => IPAddress.Parse(reader.GetString()!);
}

public class DateOnlyJsonValueReader : SimpleJsonValueReader<DateOnly>
{
    public override DateOnly FromJson(ref Utf8JsonReader reader)
        => DateOnly.Parse(reader.GetString()!);
}

public class PointJsonValueReader : SimpleJsonValueReader<Point>
{
    public override Point FromJson(ref Utf8JsonReader reader)
        => (Point)new WKTReader().Read(reader.GetString()!);
}

public class GeoJsonPointJsonValueReader : JsonValueReader<Point>
{
    public override Point FromJson(Stream stream, ref byte[] buffer, ref Utf8JsonReader reader)
    {
        string? type = null;
        var coordinates = new List<double>();
        string? tokenName = null;
        while (JsonColumnsSample.TryReadToken(stream, ref buffer, ref reader, ref tokenName))
        {
            switch (tokenName!)
            {
                case "coordinates":
                    coordinates.Add(JsonColumnsSample.MaterializeDoubleElement(stream, ref buffer, ref reader)!);
                    break;
                case "type":
                    type = reader.GetString();
                    break;
            }
        }

        Debug.Assert(type == "Point");
        return new Point(coordinates[0], coordinates[1]) { SRID = 4326 };
    }
}

public class GeoJsonPointJsonValueReader2 : JsonValueReader<Point>
{
    public override Point FromJson(Stream stream, ref byte[] buffer, ref Utf8JsonReader reader)
    {
        var builder = new StringBuilder("{");
        var depth = 1;
        while (depth > 0)
        {
            while (!reader.Read())
            {
                JsonColumnsSample.ReadBytes(stream, ref buffer, ref reader);
            }

            switch (reader.TokenType)
            {
                case JsonTokenType.EndObject:
                    depth--;
                    builder.Append('}');
                    break;
                case JsonTokenType.PropertyName:
                    builder.Append(@$"""{reader.GetString()}"":");
                    break;
                case JsonTokenType.StartObject:
                    depth++;
                    builder.Append('{');
                    break;
                case JsonTokenType.String:
                    builder.Append(@$"""{reader.GetString()}"",");
                    
                    break;
                case JsonTokenType.Number:
                    builder.Append(@$"{reader.GetDecimal()},");
                    break;
                case JsonTokenType.True:
                    builder.Append("true,");
                    break;
                case JsonTokenType.False:
                    builder.Append("false,");
                    break;
                case JsonTokenType.Null:
                    builder.Append("null,");
                    break;
                case JsonTokenType.StartArray:
                    builder.Append('[');
                    break;
                case JsonTokenType.EndArray:
                    builder.Append(']');
                    break;
            }
        }

        var serializer = GeoJsonSerializer.Create();
        using (var stringReader = new StringReader(builder.ToString()))
        using (var jsonReader = new JsonTextReader(stringReader))
        {
            return (Point)serializer.Deserialize<Geometry>(jsonReader);
        }
    }
}

// Extensions:

public static class TempExtensions
{
    public static object? GetJsonValueReader(this CoreTypeMapping typeMapping)
    {
        return typeMapping.ClrType.Name switch
        {
            nameof(IPAddress) => new IPAddressJsonValueReader(),
            nameof(DateOnly) => new DateOnlyJsonValueReader(),
            nameof(Geometry) => new PointJsonValueReader(),
            nameof(Point) => new GeoJsonPointJsonValueReader2(),
            _ => null
        };
    }
}
