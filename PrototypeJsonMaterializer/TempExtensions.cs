using System.Net;
using Microsoft.EntityFrameworkCore.Storage;
using NetTopologySuite.Geometries;

namespace PrototypeJsonMaterializer;

public static class TempExtensions
{
    public static object? GetJsonValueReader(this CoreTypeMapping typeMapping)
    {
        return typeMapping.ClrType.Name switch
        {
            nameof(IPAddress) => new IpAddressJsonValueReader(),
            nameof(DateOnly) => new DateOnlyJsonValueReader(),
            nameof(Geometry) => new PointJsonValueReader(),
            nameof(Point) => new GeoJsonPointJsonValueReader3(),
            _ => null
        };
    }
}