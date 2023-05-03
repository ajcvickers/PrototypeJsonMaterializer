using System.Diagnostics;
using System.Text.Json;

namespace PrototypeJsonMaterializer;

public class JsonReaderData
{
    private readonly Stream? _stream;
    private byte[] _buffer;
    private int _positionInBuffer;
    private int _bytesAvailable;

    public JsonReaderData(byte[] buffer)
    {
        _buffer = buffer;
        _bytesAvailable = buffer.Length;
    }

    public JsonReaderData(Stream stream)
    {
        _stream = stream;
        _buffer = new byte[1];
        ReadBytes(0);
    }

    public JsonReaderState ReaderState { get; set; }

    public void CaptureState(ref Utf8JsonReaderManager manager)
    {
        _positionInBuffer += (int)manager.CurrentReader.BytesConsumed;
        ReaderState = manager.CurrentReader.CurrentState;
    }

    public void ReadBytes(int bytesConsumed)
    {
        Debug.Assert(_stream != null);
        
        var buffer = _buffer;
        var totalConsumed = bytesConsumed + _positionInBuffer;
        if (_bytesAvailable != 0 && totalConsumed < buffer.Length)
        {
            var leftover = buffer.AsSpan(totalConsumed);

            if (leftover.Length == buffer.Length)
            {
                Array.Resize(ref buffer, buffer.Length * 2);
            }

            leftover.CopyTo(buffer);
            _bytesAvailable = _stream.Read(buffer.AsSpan(leftover.Length)) + leftover.Length;
        }
        else
        {
            _bytesAvailable = _stream.Read(buffer);
        }

        _buffer = buffer;
        _positionInBuffer = 0;
    }

    public Utf8JsonReader CreateReader() =>
        new(_buffer.AsSpan(_positionInBuffer), isFinalBlock: _bytesAvailable != _buffer.Length, ReaderState);
}