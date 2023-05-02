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

        // Stream/dynamic
        
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var materializer = CreateJsonStreamMaterializer<PostMetadata>(
            context.Model.FindEntityType("PrototypeJsonMaterializer.PostMetadata")!);
        var entity = materializer(stream);

        // Buffer/dynamic
        
        // var materializer = CreateJsonBufferMaterializer<PostMetadata>(
        //     context.Model.FindEntityType("PrototypeJsonMaterializer.PostMetadata")!);
        // var entity = materializer(Encoding.UTF8.GetBytes(json));

        // Stream/static
        
        // using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        // var readerManager = new Utf8JsonReaderManager(stream);
        // var entity = MaterializePostMetadata(ref readerManager);

        // Buffer/static
        
        // var readerManager = new Utf8JsonReaderManager(Encoding.UTF8.GetBytes(json));
        // var entity = MaterializePostMetadata(ref readerManager);

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

    private static readonly MethodInfo TryReadTokenMethod = typeof(Utf8JsonReaderManager).GetMethod(nameof(Utf8JsonReaderManager.TryReadToken))!;
    private static readonly MethodInfo AdvanceToFirstElementMethod = typeof(Utf8JsonReaderManager).GetMethod(nameof(Utf8JsonReaderManager.AdvanceToFirstElement))!;
    private static readonly FieldInfo CurrentReaderField = typeof(Utf8JsonReaderManager).GetField(nameof(Utf8JsonReaderManager.CurrentReader))!;

    private static readonly Dictionary<Type, MethodInfo> PrimitiveMethods
        = new()
        {
            { typeof(int), typeof(Utf8JsonReader).GetMethod(nameof(Utf8JsonReader.GetInt32))! },
            { typeof(string), typeof(Utf8JsonReader).GetMethod(nameof(Utf8JsonReader.GetString))! },
            { typeof(double), typeof(Utf8JsonReader).GetMethod(nameof(Utf8JsonReader.GetDouble))! },
        };

    public static Func<byte[], TEntity> CreateJsonBufferMaterializer<TEntity>(IEntityType entityType)
    {
        var bufferParameter = Expression.Parameter(typeof(byte[]), "buffer");
        var managerVariable = Expression.Variable(typeof(Utf8JsonReaderManager), "manager");

        var topBlock = Expression.Block(
            new[] { managerVariable },
            Expression.Assign(
                managerVariable, Expression.New(
                    typeof(Utf8JsonReaderManager).GetConstructor(new[] { typeof(byte[]) })!,
                    bufferParameter)),
            CreateMaterializeBlock(managerVariable, entityType));

        return Expression.Lambda<Func<byte[], TEntity>>(topBlock, bufferParameter).Compile();
    }

    public static Func<Stream, TEntity> CreateJsonStreamMaterializer<TEntity>(IEntityType entityType)
    {
        var streamParameter = Expression.Parameter(typeof(Stream), "stream");
        var managerVariable = Expression.Variable(typeof(Utf8JsonReaderManager), "manager");
    
        var topBlock = Expression.Block(
            new[] { managerVariable },
            Expression.Assign(
                managerVariable, Expression.New(
                    typeof(Utf8JsonReaderManager).GetConstructor(new[] { typeof(Stream) })!,
                    streamParameter)),
            CreateMaterializeBlock(managerVariable, entityType));

        return Expression.Lambda<Func<Stream, TEntity>>(topBlock, streamParameter).Compile();
    }
    
    private static BlockExpression CreateMaterializeBlock(
        ParameterExpression managerParameter,
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
                    Expression.Call(managerParameter, AdvanceToFirstElementMethod),
                    jsonValueReader == null
                        ? Expression.Call(
                            Expression.Field(managerParameter, CurrentReaderField),
                            PrimitiveMethods[typeMapping.ElementTypeMapping.ClrType])
                        : Expression.Call(
                            Expression.Constant(jsonValueReader),
                            jsonValueReader.GetType().GetMethod("FromJson")!,
                            managerParameter));

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
                                    ? Expression.Call(
                                        Expression.Field(managerParameter, CurrentReaderField),
                                        PrimitiveMethods[typeMapping.ClrType])
                                    : Expression.Call(
                                        Expression.Constant(jsonValueReader),
                                        jsonValueReader.GetType().GetMethod("FromJson")!,
                                        managerParameter)),
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
                            CreateMaterializeBlock(managerParameter, navigation.TargetEntityType)),
                        Expression.Empty()),
                    Expression.Constant(navigation.Name)));
        }

        return Expression.Block(
            new[] { entityVariable, tokenNameVariable },
            Expression.Assign(entityVariable, Expression.New(clrType.GetConstructor(Type.EmptyTypes)!)),
            Expression.Assign(tokenNameVariable, Expression.Constant(null, typeof(string))),
            Expression.Loop(
                Expression.IfThenElse(
                    Expression.Call(managerParameter, TryReadTokenMethod, tokenNameVariable),
                    Expression.Block(Expression.Switch(tokenNameVariable, null, null, propertyCases)),
                    Expression.Break(readDoneLabel)),
                readDoneLabel),
            entityVariable);
    }

    // Static materializer with manager: 

    public static PostMetadata MaterializePostMetadata(ref Utf8JsonReaderManager manager)
    {
        var entity = new PostMetadata();
        string? tokenName = null;
        while (manager.TryReadToken(ref tokenName))
        {
            switch (tokenName!)
            {
                case "Views":
                    entity.Views = manager.CurrentReader.GetInt32();
                    break;
                case "SomeInts":
                    manager.AdvanceToFirstElement();
                    entity.SomeInts.Add(manager.CurrentReader.GetInt32());
                    break;
                case "TopGeographies":
                    entity.TopGeographies.Add(MaterializeVisits(ref manager));
                    break;
                case "TopSearches":
                    entity.TopSearches.Add(MaterializeSearchTerm(ref manager));
                    break;
                case "Updates":
                    entity.Updates.Add(MaterializePostUpdate(ref manager));
                    break;
            }
        }

        return entity;
    }

    public static Visits MaterializeVisits(ref Utf8JsonReaderManager manager)
    {
        var entity = new Visits();
        string? tokenName = null;
        while (manager.TryReadToken(ref tokenName))
        {
            switch (tokenName!)
            {
                case "Browsers":
                    manager.AdvanceToFirstElement();
                    entity.Browsers.Add(manager.CurrentReader.GetString()!);
                    break;
                case "Location":
                    entity.Location = LocationJsonValueReader.FromJson(ref manager);
                    break;
                case "GeoJsonLocation":
                    entity.GeoJsonLocation = GeoJsonLocationJsonValueReader2.FromJson(ref manager);
                    break;
                case "Count":
                    entity.Count = manager.CurrentReader.GetInt32();
                    break;
            }
        }

        return entity;
    }
    
    public static SearchTerm MaterializeSearchTerm(ref Utf8JsonReaderManager manager)
    {
        var entity = new SearchTerm();
        string? tokenName = null;
        while (manager.TryReadToken(ref tokenName))
        {
            switch (tokenName!)
            {
                case "Term":
                    entity.Term = manager.CurrentReader.GetString()!;
                    break;
                case "Count":
                    entity.Count = manager.CurrentReader.GetInt32();
                    break;
            }
        }

        return entity;
    }

    public static PostUpdate MaterializePostUpdate(ref Utf8JsonReaderManager manager)
    {
        var entity = new PostUpdate();
        string? tokenName = null;
        while (manager.TryReadToken(ref tokenName))
        {
            switch (tokenName!)
            {
                case "PostedFrom":
                    entity.PostedFrom = PostedFromJsonValueReader.FromJson(ref manager)!;
                    break;
                case "UpdatedBy":
                    entity.UpdatedBy = manager.CurrentReader.GetString();
                    break;
                case "UpdatedOn":
                    entity.UpdatedOn = UpdatedOnJsonValueReader.FromJson(ref manager)!;
                    break;
                case "Commits":
                    entity.Commits.Add(MaterializeCommit(ref manager));
                    break;
            }
        }

        return entity;
    }

    public static Commit MaterializeCommit(ref Utf8JsonReaderManager manager)
    {
        var entity = new Commit();
        string? tokenName = null;
        while (manager.TryReadToken(ref tokenName))
        {
            switch (tokenName!)
            {
                case "Comment":
                    entity.Comment = manager.CurrentReader.GetString()!;
                    break;
                case "CommittedOn":
                    entity.CommittedOn = CommittedOnJsonValueReader.FromJson(ref manager);
                    break;
            }
        }

        return entity;
    }

    private static readonly DateOnlyJsonValueReader CommittedOnJsonValueReader = new();
    private static readonly IpAddressJsonValueReader PostedFromJsonValueReader = new();
    private static readonly DateOnlyJsonValueReader UpdatedOnJsonValueReader = new();
    private static readonly PointJsonValueReader LocationJsonValueReader = new();
    private static readonly GeoJsonPointJsonValueReader4 GeoJsonLocationJsonValueReader2 = new();
}
    
