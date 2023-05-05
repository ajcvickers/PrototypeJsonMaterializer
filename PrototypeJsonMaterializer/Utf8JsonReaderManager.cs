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

    public JsonTokenType MoveNext()
    {
        while (!CurrentReader.Read())
        {
            CurrentReader = Data.ReadBytes((int)CurrentReader.BytesConsumed, CurrentReader.CurrentState);
        }

        return CurrentReader.TokenType;
    }
    
    public void CaptureState() => Data.CaptureState(ref this);
}