using System.Text.Json;

namespace PrototypeJsonMaterializer;

public ref struct Utf8JsonReaderManager
{
    private readonly Stream? _stream;
    private byte[] _buffer;
    public Utf8JsonReader CurrentReader;

    public Utf8JsonReaderManager(byte[] buffer)
    {
        _buffer = buffer;
        CurrentReader = new Utf8JsonReader(buffer.AsSpan(0), isFinalBlock: true, state: default);
        ReadBytes();
    }

    public Utf8JsonReaderManager(Stream stream)
    {
        _stream = stream;
        _buffer = new byte[1];
        CurrentReader = new Utf8JsonReader(_buffer.AsSpan(0), isFinalBlock: false, state: default);
        ReadBytes();
    }

    public void MoveNext()
    {
        while (!CurrentReader.Read())
        {
            ReadBytes();
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
    
    private void ReadBytes()
    {
        if (_stream == null)
        {
            return;
        }

        int bytesAvailable;
        if (CurrentReader.TokenType != JsonTokenType.None && CurrentReader.BytesConsumed < _buffer.Length)
        {
            var leftover = _buffer.AsSpan((int)CurrentReader.BytesConsumed);

            if (leftover.Length == _buffer.Length)
            {
                Array.Resize(ref _buffer, _buffer.Length * 2);
            }

            leftover.CopyTo(_buffer);
            bytesAvailable = _stream.Read(_buffer.AsSpan(leftover.Length)) + leftover.Length;
        }
        else
        {
            bytesAvailable = _stream.Read(_buffer);
        }

        CurrentReader = new Utf8JsonReader(_buffer.AsSpan(0, bytesAvailable), isFinalBlock: bytesAvailable != _buffer.Length,
            CurrentReader.CurrentState);
    }
}