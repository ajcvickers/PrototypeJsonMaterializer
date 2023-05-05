using System.Text;

namespace PrototypeJsonMaterializer;

public static class JsonColumnsSample
{
    public static void Main()
    {
        using var context = new BlogsContext();

        var json = """
            {
              "SomeInts": [0,1,2],
              "Views": 6187,
              "TopGeographies": [
                {
                  "Browsers": ["Firefox", "Netscape"],
                  "Count": 966,
                  "Location": "POINT (109.793 43.2431)",
                  "GeoJsonLocation": {
                    "type": "Point",
                    "coordinates": [133.793,47.2431]
                  },
                  "Latitude": 134.793,
                  "Longitude": 35.2431
                }
              ],
                  "UnknownObject": {
                    "type": "Point",
                    "coordinates": [133.793,47.2431]
                  },
              "Contact": {
                  "Address": {
                      "Street": "11 Meadow Drive",
                      "City": "Healing",
                      "Postcode": "DN37 7RU",
                      "Country": "UK"
                  },
                  "Phone": "(555) 555-5555"
              },
              "TopSearches": [
                {
                  "Count": 9647,
                  "Term": "Search #1"
                }
              ],
              "UnknownArray": [
                {
                  "Count": 9647,
                  "Term": "Search #1"
                }
              ],
              "Updates": [
                {
                  "PostedFrom": "127.0.0.1",
                  "UpdatedBy": "Admin",
                  "UpdatedOn": "1998-04-16",
                  "Commits": [
                    {
                      "Comment": "Commit #1",
                      "CommittedOn": "2023-04-30"
                    }
                  ]
                },
                {
                  "PostedFrom": "127.0.0.1",
                  "UpdatedBy": "Admin",
                  "UpdatedOn": "2015-02-11",
                  "Commits": [
                    {
                      "Comment": "Commit #1",
                      "CommittedOn": "2023-04-30"
                    },
                    {
                      "Comment": "Commit #2",
                      "CommittedOn": "2023-04-30"
                    }
                  ]
                },
                {
                  "PostedFrom": "127.0.0.1",
                  "UpdatedBy": "Admin",
                  "UpdatedOn": "2007-02-10",
                  "Commits": [
                    {
                      "Comment": "Commit #1",
                      "CommittedOn": "2023-04-30"
                    }
                  ]
                }
              ]
            }
            """;

        // Stream/dynamic

        // using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        // var materializer = JsonToEntityMaterializer.CreateJsonMaterializer<PostMetadata>(
        //     context.Model.FindEntityType("PrototypeJsonMaterializer.PostMetadata")!);
        // var entity = materializer(new JsonReaderData(stream));

        // Buffer/dynamic

        var materializer = JsonToEntityMaterializer.CreateJsonMaterializer<PostMetadata>(
            context.Model.FindEntityType("PrototypeJsonMaterializer.PostMetadata")!).Compile();
        var entity = materializer(new JsonReaderData(Encoding.UTF8.GetBytes(json)));

        // Stream/static

        // using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        // var entity = PostMetadataMaterializer.MaterializePostMetadata(new JsonReaderData(stream));

        // Buffer/static

        // var entity = PostMetadataMaterializer.MaterializePostMetadata(new JsonReaderData(Encoding.UTF8.GetBytes(json)));

        Console.WriteLine($"{entity.GetType()}:");
        Console.WriteLine($"  Views: {entity.Views}");
        Console.WriteLine($"  SomeInts: {string.Join(", ", entity.SomeInts)}");
        Console.WriteLine($"  Contact:");
        Console.WriteLine($"      Street: {entity.Contact?.Address.Street}");
        Console.WriteLine($"      City: {entity.Contact?.Address.City}");
        Console.WriteLine($"      Postcode: {entity.Contact?.Address.Postcode}");
        Console.WriteLine($"      Country: {entity.Contact?.Address.Country}");
        Console.WriteLine($"      Phone: {entity.Contact?.Phone}");
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
}