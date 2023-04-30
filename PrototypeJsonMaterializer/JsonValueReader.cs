using System.Text.Json;

namespace PrototypeJsonMaterializer;

public abstract class JsonValueReader<TValue>
{
    public abstract TValue FromJson(Stream stream, ref byte[] buffer, ref Utf8JsonReader reader);
}