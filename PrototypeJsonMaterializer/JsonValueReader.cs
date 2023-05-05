using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Newtonsoft.Json;

namespace PrototypeJsonMaterializer;

public interface IJsonValueReader<out TValue>
{
    TValue FromJson(ref Utf8JsonReaderManager manager);
}

public sealed class IpAddressJsonValueReader : IJsonValueReader<IPAddress?>
{
    public IPAddress FromJson(ref Utf8JsonReaderManager manager)
        => IPAddress.Parse(manager.CurrentReader.GetString()!);
}

public sealed class DateOnlyJsonValueReader : IJsonValueReader<DateOnly>
{
    public DateOnly FromJson(ref Utf8JsonReaderManager manager)
        => DateOnly.Parse(manager.CurrentReader.GetString()!);
}

public sealed class PointJsonValueReader : IJsonValueReader<Point>
{
    public Point FromJson(ref Utf8JsonReaderManager manager)
        => (Point)new WKTReader().Read(manager.CurrentReader.GetString()!);
}

public sealed class GeoJsonPointJsonValueReader3 : IJsonValueReader<Point>
{
    public Point FromJson(ref Utf8JsonReaderManager manager)
    {
        string? type = null;
        var coordinates = new List<double>();
        var tokenType = JsonTokenType.None; 
        while (tokenType != JsonTokenType.EndObject)
        {
            tokenType = manager.MoveNext();

            switch (tokenType)
            {
                case JsonTokenType.PropertyName:
                    if (manager.CurrentReader.ValueTextEquals("type"u8))
                    {
                        manager.MoveNext();
                        type = manager.CurrentReader.GetString();
                    }
                    else if (manager.CurrentReader.ValueTextEquals("coordinates"u8))
                    {
                        manager.MoveNext();
                        tokenType = manager.MoveNext();
                        while (tokenType != JsonTokenType.EndArray)
                        {
                            coordinates.Add(manager.CurrentReader.GetDouble());
                            tokenType = manager.MoveNext();
                        }
                    }
                    break;
            }
        }

        Debug.Assert(type == "Point");
        return new Point(coordinates[0], coordinates[1]) { SRID = 4326 };
    }
}

public sealed class GeoJsonPointJsonValueReader4 : IJsonValueReader<Point>
{
    public Point FromJson(ref Utf8JsonReaderManager manager)
    {
        var builder = new StringBuilder("{");
        var depth = 1;
        while (depth > 0)
        {
            manager.MoveNext();

            switch (manager.CurrentReader.TokenType)
            {
                case JsonTokenType.EndObject:
                    depth--;
                    builder.Append('}');
                    break;
                case JsonTokenType.PropertyName:
                    builder.Append(@$"""{manager.CurrentReader.GetString()}"":");
                    break;
                case JsonTokenType.StartObject:
                    depth++;
                    builder.Append('{');
                    break;
                case JsonTokenType.String:
                    builder.Append(@$"""{manager.CurrentReader.GetString()}"",");
                    break;
                case JsonTokenType.Number:
                    builder.Append(@$"{manager.CurrentReader.GetDecimal()},");
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
