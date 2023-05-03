using System.Text.Json;

namespace PrototypeJsonMaterializer;

public ref struct Utf8JsonReaderManager
{
    public readonly JsonReaderData Data;
    public Utf8JsonReader CurrentReader;

    public Utf8JsonReaderManager(JsonReaderData data)
    {
        Data = data;
        CurrentReader = data.CreateReader();
    }

    public void MoveNext()
    {
        while (!CurrentReader.Read())
        {
            Data.ReadBytes((int)CurrentReader.BytesConsumed);
            Data.ReaderState = CurrentReader.CurrentState;
            CurrentReader = Data.CreateReader();
        }
    }

    public bool TryReadToken(ref string? tokenName)
    {
        while (true)
        {
            MoveNext();

            switch (CurrentReader.TokenType)
            {
                case JsonTokenType.EndObject:
                    return false;
                case JsonTokenType.PropertyName:
                    tokenName = CurrentReader.GetString();
                    break;
                case JsonTokenType.StartObject:
                case JsonTokenType.String:
                case JsonTokenType.Number:
                case JsonTokenType.True:
                case JsonTokenType.False:
                case JsonTokenType.Null:
                    return true;
            }
        }
    }
    
    public void AdvanceToFirstElement()
    {
        if (CurrentReader.TokenType == JsonTokenType.PropertyName)
        {
            string? _ = null;
            TryReadToken(ref _);
        }
    }
}