using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace PrototypeJsonMaterializer;

public static class JsonColumnsSample
{
    public static void Main()
    {
        using var context = new BlogsContext();

        var json = @"{
  ""SomeInts"": [0,1,2],
  ""Views"": 6187,
  ""TopGeographies"": [
    {
      ""Browsers"": [""Firefox"", ""Netscape""],
      ""Count"": 966,
      ""Location"": ""POINT (109.793 43.2431)"",
      ""GeoJsonLocation"": {
        ""type"": ""Point"",
        ""coordinates"": [133.793,47.2431]
      },
      ""Latitude"": 134.793,
      ""Longitude"": 35.2431
    }
  ],
  ""TopSearches"": [
    {
      ""Count"": 9647,
      ""Term"": ""Search #1""
    }
  ],
  ""Updates"": [
    {
      ""PostedFrom"": ""127.0.0.1"",
      ""UpdatedBy"": ""Admin"",
      ""UpdatedOn"": ""1998-04-16"",
      ""Commits"": [
        {
          ""Comment"": ""Commit #1"",
          ""CommittedOn"": ""2023-04-30""
        }
      ]
    },
    {
      ""PostedFrom"": ""127.0.0.1"",
      ""UpdatedBy"": ""Admin"",
      ""UpdatedOn"": ""2015-02-11"",
      ""Commits"": [
        {
          ""Comment"": ""Commit #1"",
          ""CommittedOn"": ""2023-04-30""
        },
        {
          ""Comment"": ""Commit #2"",
          ""CommittedOn"": ""2023-04-30""
        }
      ]
    },
    {
      ""PostedFrom"": ""127.0.0.1"",
      ""UpdatedBy"": ""Admin"",
      ""UpdatedOn"": ""2007-02-10"",
      ""Commits"": [
        {
          ""Comment"": ""Commit #1"",
          ""CommittedOn"": ""2023-04-30""
        }
      ]
    }
  ]
}";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var buffer = new byte[1];

        var materializer = CreateJsonMaterializer<PostMetadata>(
            context.Model.FindEntityType("PrototypeJsonMaterializer.PostMetadata")!);
        var entity = materializer(stream, buffer);

        // var reader = new Utf8JsonReader(buffer.AsSpan(0), isFinalBlock: false, state: default);
        // ReadBytes(stream, ref buffer, ref reader);
        // var entity = MaterializePostMetadata(stream, ref buffer, ref reader);

        Console.WriteLine($"{entity.GetType()}:");
        Console.WriteLine($"  Views: {entity.Views}");
        Console.WriteLine($"  SomeInts: {string.Join(", ", entity.SomeInts)}");
        Console.WriteLine($"  TopGeographies:");
        for (var i = 0; i < entity.TopGeographies.Count; i++)
        {
            Console.WriteLine($"    Geography {i}:");
            var geography = entity.TopGeographies[i];
            Console.WriteLine($"      Count: {geography.Count}");
            Console.WriteLine($"      Location: {geography.Location}");
            Console.WriteLine($"      GeoLocation: {geography.GeoJsonLocation}");
            Console.WriteLine($"      Browsers: {string.Join(", ", geography.Browsers)}");
        }
        Console.WriteLine($"  TopSearches:");
        for (var i = 0; i < entity.TopSearches.Count; i++)
        {
            Console.WriteLine($"    Search {i}:");
            var searchTerm = entity.TopSearches[i];
            Console.WriteLine($"      Term: {searchTerm.Term}");
            Console.WriteLine($"      Count: {searchTerm.Count}");
        }
        Console.WriteLine($"  Updates:");
        for (var i = 0; i < entity.Updates.Count; i++)
        {
            Console.WriteLine($"    Update {i}:");
            var update = entity.Updates[i];
            Console.WriteLine($"      PostedFrom: {update.PostedFrom}");
            Console.WriteLine($"      UpdatedBy: {update.UpdatedBy}");
            Console.WriteLine($"      UpdatedOn: {update.UpdatedOn}");
            Console.WriteLine($"      Commits:");
            for (var j = 0; j < update.Commits.Count(); j++)
            {
                Console.WriteLine($"        Commit {j}:");
                var commit = update.Commits[j];
                Console.WriteLine($"          CommittedOn: {commit.CommittedOn}");
                Console.WriteLine($"          Comment: {commit.Comment}");
            }
        }
    }

    // Expression-based materializer:
    
    private static readonly MethodInfo ReadBytesMethod = typeof(JsonColumnsSample).GetMethod(nameof(ReadBytes))!;
    private static readonly MethodInfo TryReadTokenMethod = typeof(JsonColumnsSample).GetMethod(nameof(TryReadToken))!;
    private static readonly MethodInfo AdvanceToFirstElementMethod = typeof(JsonColumnsSample).GetMethod(nameof(AdvanceToFirstElement))!;
    private static readonly ConstructorInfo ReadOnlySpanConstructor = typeof(ReadOnlySpan<byte>).GetConstructors()
        .Single(c => c.GetParameters().Length == 1 && c.GetParameters()[0].ParameterType == typeof(byte[]));

    private static readonly Dictionary<Type, MethodInfo> PrimitiveMethods
        = new()
        {
            { typeof(int), typeof(Utf8JsonReader).GetMethod(nameof(Utf8JsonReader.GetInt32))! },
            { typeof(string), typeof(Utf8JsonReader).GetMethod(nameof(Utf8JsonReader.GetString))! },
            { typeof(double), typeof(Utf8JsonReader).GetMethod(nameof(Utf8JsonReader.GetDouble))! },
        };

    public static Func<Stream, byte[], TEntity> CreateJsonMaterializer<TEntity>(IEntityType entityType)
    {
        var streamParameter = Expression.Parameter(typeof(Stream), "stream");
        var bufferParameter = Expression.Parameter(typeof(byte[]), "buffer");
        var readerVariable = Expression.Variable(typeof(Utf8JsonReader), "reader");

        var topBlock = Expression.Block(
            new[] { readerVariable },
            Expression.Assign(
                readerVariable, Expression.New(
                    typeof(Utf8JsonReader).GetConstructor(new[] { typeof(ReadOnlySpan<byte>), typeof(bool), typeof(JsonReaderState) })!,
                    Expression.New(ReadOnlySpanConstructor, bufferParameter),
                    Expression.Constant(false),
                    Expression.Constant(default(JsonReaderState)))),
            Expression.Call(ReadBytesMethod, streamParameter, bufferParameter, readerVariable),
            CreateMaterializeBlock(streamParameter, bufferParameter, readerVariable, entityType));

        var materializeLambda = Expression.Lambda<Func<Stream, byte[], TEntity>>(
            topBlock, streamParameter, bufferParameter);

        return materializeLambda.Compile();
    }

    private static BlockExpression CreateMaterializeBlock(
        ParameterExpression streamParameter,
        ParameterExpression bufferParameter,
        ParameterExpression readerVariable,
        IEntityType entityType)
    {
        var clrType = entityType.ClrType;
        var entityVariable = Expression.Variable(clrType, "entity");
        var tokenNameVariable = Expression.Variable(typeof(string), "tokenName");
        var readDoneLabel = Expression.Label("readDone");

        var propertyCases = new List<SwitchCase>();

        foreach (var property in entityType.GetProperties().Where(p => !p.IsShadowProperty()))
        {
            var typeMapping = property.GetTypeMapping();
            if (typeMapping.ElementTypeMapping != null)
            {
                var jsonValueReader = typeMapping.ElementTypeMapping.GetJsonValueReader();

                var readerExpression = Expression.Block(
                    Expression.Call(AdvanceToFirstElementMethod, streamParameter, bufferParameter, readerVariable),
                    jsonValueReader == null
                        ? Expression.Call(readerVariable, PrimitiveMethods[typeMapping.ElementTypeMapping.ClrType])
                        : Expression.Call(
                            Expression.Constant(jsonValueReader),
                            jsonValueReader.GetType().GetMethods().Single(m => m.Name == "FromJson" && m.GetParameters().Length == 3),
                            streamParameter, bufferParameter, readerVariable));

                propertyCases.Add(
                    Expression.SwitchCase(
                        Expression.Block(
                            Expression.Call(
                                Expression.MakeMemberAccess(entityVariable, clrType.GetProperty(property.Name)!),
                                property.ClrType.GetMethod("Add")!,
                                readerExpression),
                            Expression.Empty()),
                        Expression.Constant(property.GetJsonPropertyName())));
            }
            else
            {
                var jsonValueReader = typeMapping.GetJsonValueReader();
                propertyCases.Add(
                    Expression.SwitchCase(
                        Expression.Block(
                            Expression.Assign(
                                Expression.MakeMemberAccess(entityVariable, clrType.GetProperty(property.Name)!),
                                jsonValueReader == null
                                    ? Expression.Call(readerVariable, PrimitiveMethods[typeMapping.ClrType])
                                    : Expression.Call(
                                        Expression.Constant(jsonValueReader),
                                        jsonValueReader.GetType().GetMethods()
                                            .Single(m => m.Name == "FromJson" && m.GetParameters().Length == 3),
                                        streamParameter, bufferParameter, readerVariable)),
                            Expression.Empty()),
                        Expression.Constant(property.GetJsonPropertyName())));
            }
        }

        foreach (var navigation in entityType.GetNavigations().Where(n => !n.IsOnDependent))
        {
            propertyCases.Add(
                Expression.SwitchCase(
                    Expression.Block(
                        Expression.Call(
                            Expression.MakeMemberAccess(entityVariable, clrType.GetProperty(navigation.Name)!),
                            navigation.ClrType.GetMethod("Add")!,
                            CreateMaterializeBlock(streamParameter, bufferParameter, readerVariable, navigation.TargetEntityType)),
                        Expression.Empty()),
                    Expression.Constant(navigation.Name)));
        }

        return Expression.Block(
            new[] { entityVariable, tokenNameVariable },
            Expression.Assign(entityVariable, Expression.New(clrType.GetConstructor(Type.EmptyTypes)!)),
            Expression.Assign(tokenNameVariable, Expression.Constant(null, typeof(string))),
            Expression.Loop(
                Expression.IfThenElse(
                    Expression.Call(TryReadTokenMethod, streamParameter, bufferParameter, readerVariable, tokenNameVariable),
                    Expression.Block(
                        Expression.Switch(tokenNameVariable, null, null, propertyCases)),
                    Expression.Break(readDoneLabel)),
                readDoneLabel),
            entityVariable);
    }

    // Helpers:
    
    public static void AdvanceToFirstElement(Stream stream, ref byte[] buffer, ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.PropertyName)
        {
            string? _ = null;
            TryReadToken(stream, ref buffer, ref reader, ref _);
        }
    }

    public static bool TryReadToken(Stream stream, ref byte[] buffer, ref Utf8JsonReader reader, ref string? tokenName)
    {
        while (true)
        {
            while (!reader.Read())
            {
                ReadBytes(stream, ref buffer, ref reader);
            }

            switch (reader.TokenType)
            {
                case JsonTokenType.EndObject:
                    return false;
                case JsonTokenType.PropertyName:
                    tokenName = reader.GetString();
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

    public static void ReadBytes(Stream stream, ref byte[] buffer, ref Utf8JsonReader reader)
    {
        int bytesAvailable;
        if (reader.TokenType != JsonTokenType.None && reader.BytesConsumed < buffer.Length)
        {
            var leftover = buffer.AsSpan((int)reader.BytesConsumed);

            if (leftover.Length == buffer.Length)
            {
                Array.Resize(ref buffer, buffer.Length * 2);
            }

            leftover.CopyTo(buffer);
            bytesAvailable = stream.Read(buffer.AsSpan(leftover.Length)) + leftover.Length;
        }
        else
        {
            bytesAvailable = stream.Read(buffer);
        }

        reader = new Utf8JsonReader(buffer.AsSpan(0, bytesAvailable), isFinalBlock : bytesAvailable != buffer.Length, reader.CurrentState);
    }

    // Static materializer: 
    
    public static PostMetadata MaterializePostMetadata(Stream stream, ref byte[] buffer, ref Utf8JsonReader reader)
    {
        var entity = new PostMetadata();
        string? tokenName = null;
        while (TryReadToken(stream, ref buffer, ref reader, ref tokenName))
        {
            switch (tokenName!)
            {
                case "Views":
                    entity.Views = reader.GetInt32();
                    break;
                case "SomeInts":
                    entity.SomeInts.Add(MaterializeIntElement(stream, ref buffer, ref reader));
                    break;
                case "TopGeographies":
                    entity.TopGeographies.Add(MaterializeVisits(stream, ref buffer, ref reader));
                    break;
                case "TopSearches":
                    entity.TopSearches.Add(MaterializeSearchTerm(stream, ref buffer, ref reader));
                    break;
                case "Updates":
                    entity.Updates.Add(MaterializePostUpdate(stream, ref buffer, ref reader));
                    break;
            }
        }

        return entity;
    }

    private static readonly PointJsonValueReader LocationJsonValueReader = new();
    private static readonly GeoJsonPointJsonValueReader GeoJsonLocationJsonValueReader = new();

    public static Visits MaterializeVisits(Stream stream, ref byte[] buffer, ref Utf8JsonReader reader)
    {
        var entity = new Visits();
        string? tokenName = null;
        while (TryReadToken(stream, ref buffer, ref reader, ref tokenName))
        {
            switch (tokenName!)
            {
                case "Browsers":
                    entity.Browsers.Add(MaterializeStringElement(stream, ref buffer, ref reader)!);
                    break;
                case "Location":
                    entity.Location = LocationJsonValueReader.FromJson(stream, ref buffer, ref reader);
                    break;
                case "GeoJsonLocation":
                    entity.GeoJsonLocation = GeoJsonLocationJsonValueReader.FromJson(stream, ref buffer, ref reader);
                    break;
                case "Count":
                    entity.Count = reader.GetInt32();
                    break;
            }
        }

        return entity;
    }

    public static SearchTerm MaterializeSearchTerm(Stream stream, ref byte[] buffer, ref Utf8JsonReader reader)
    {
        var entity = new SearchTerm();
        string? tokenName = null;
        while (TryReadToken(stream, ref buffer, ref reader, ref tokenName))
        {
            switch (tokenName!)
            {
                case "Term":
                    entity.Term = reader.GetString()!;
                    break;
                case "Count":
                    entity.Count = reader.GetInt32();
                    break;
            }
        }

        return entity;
    }

    private static readonly IPAddressJsonValueReader PostedFromJsonValueReader = new();
    private static readonly DateOnlyJsonValueReader UpdatedOnJsonValueReader = new();

    public static PostUpdate MaterializePostUpdate(Stream stream, ref byte[] buffer, ref Utf8JsonReader reader)
    {
        var entity = new PostUpdate();
        string? tokenName = null;
        while (TryReadToken(stream, ref buffer, ref reader, ref tokenName))
        {
            switch (tokenName!)
            {
                case "PostedFrom":
                    entity.PostedFrom = PostedFromJsonValueReader.FromJson(stream, ref buffer, ref reader)!;
                    break;
                case "UpdatedBy":
                    entity.UpdatedBy = reader.GetString();
                    break;
                case "UpdatedOn":
                    entity.UpdatedOn = UpdatedOnJsonValueReader.FromJson(stream, ref buffer, ref reader)!;
                    break;
                case "Commits":
                    entity.Commits.Add(MaterializeCommit(stream, ref buffer, ref reader));
                    break;
            }
        }

        return entity;
    }

    private static readonly DateOnlyJsonValueReader CommittedOnJsonValueReader = new();

    public static Commit MaterializeCommit(Stream stream, ref byte[] buffer, ref Utf8JsonReader reader)
    {
        var entity = new Commit();
        string? tokenName = null;
        while (TryReadToken(stream, ref buffer, ref reader, ref tokenName))
        {
            switch (tokenName!)
            {
                case "Comment":
                    entity.Comment = reader.GetString()!;
                    break;
                case "CommittedOn":
                    entity.CommittedOn = CommittedOnJsonValueReader.FromJson(stream, ref buffer, ref reader);
                    break;
            }
        }

        return entity;
    }

    public static int MaterializeIntElement(Stream stream, ref byte[] buffer, ref Utf8JsonReader reader)
    {
        AdvanceToFirstElement(stream, ref buffer, ref reader);
        return reader.GetInt32();
    }

    public static string? MaterializeStringElement(Stream stream, ref byte[] buffer, ref Utf8JsonReader reader)
    {
        AdvanceToFirstElement(stream, ref buffer, ref reader);
        return reader.GetString();
    }

    public static double MaterializeDoubleElement(Stream stream, ref byte[] buffer, ref Utf8JsonReader reader)
    {
        AdvanceToFirstElement(stream, ref buffer, ref reader);
        return reader.GetDouble();
    }
}
