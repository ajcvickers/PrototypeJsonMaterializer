using System.Text.Json;

namespace PrototypeJsonMaterializer;

public class PostMetadataMaterializer
{
    public static PostMetadata MaterializePostMetadata(JsonReaderData data)
    {
        var manager = new Utf8JsonReaderManager(data);
        var entity = new PostMetadata();
        var tokenType = JsonTokenType.None;
        var depth = 0;
        while (depth > 0 || tokenType != JsonTokenType.EndObject)
        {
            tokenType = manager.MoveNext();

            switch (tokenType)
            {
                case JsonTokenType.PropertyName:
                    if (manager.CurrentReader.ValueTextEquals("Views"u8))
                    {
                        manager.MoveNext();
                        entity.Views = manager.CurrentReader.GetInt32();
                    }
                    else if (manager.CurrentReader.ValueTextEquals("SomeInts"u8))
                    {
                        manager.MoveNext();
                        tokenType = manager.MoveNext();
                        while (tokenType != JsonTokenType.EndArray)
                        {
                            entity.SomeInts.Add(manager.CurrentReader.GetInt32());
                            tokenType = manager.MoveNext();
                        }
                    }
                    else if (manager.CurrentReader.ValueTextEquals("Contact"u8))
                    {
                        manager.MoveNext();
                        manager.CaptureState();
                        entity.Contact = MaterializeContact(manager.Data);
                        manager = new Utf8JsonReaderManager(manager.Data);
                    }
                    else if (manager.CurrentReader.ValueTextEquals("Updates"u8))
                    {
                        manager.MoveNext();
                        tokenType = manager.MoveNext();
                        while (tokenType != JsonTokenType.EndArray)
                        {
                            manager.CaptureState();
                            entity.Updates.Add(MaterializePostUpdate(manager.Data));
                            manager = new Utf8JsonReaderManager(manager.Data);
                            tokenType = manager.MoveNext();
                        }
                    }
                    else if (manager.CurrentReader.ValueTextEquals("TopGeographies"u8))
                    {
                        manager.MoveNext();
                        tokenType = manager.MoveNext();
                        while (tokenType != JsonTokenType.EndArray)
                        {
                            manager.CaptureState();
                            entity.TopGeographies.Add(MaterializeVisits(manager.Data));
                            manager = new Utf8JsonReaderManager(manager.Data);
                            tokenType = manager.MoveNext();
                        }
                    }
                    else if (manager.CurrentReader.ValueTextEquals("TopSearches"u8))
                    {
                        manager.MoveNext();
                        tokenType = manager.MoveNext();
                        while (tokenType != JsonTokenType.EndArray)
                        {
                            manager.CaptureState();
                            entity.TopSearches.Add(MaterializeSearchTerm(manager.Data));
                            manager = new Utf8JsonReaderManager(manager.Data);
                            tokenType = manager.MoveNext();
                        }
                    }
                    break;

                case JsonTokenType.StartObject:
                case JsonTokenType.StartArray:
                    depth++;
                    break;
                case JsonTokenType.EndObject:
                case JsonTokenType.EndArray:
                    depth--;
                    break;
            }
        }

        manager.CaptureState();
        return entity;
    }

    public static Visits MaterializeVisits(JsonReaderData data)
    {
        var manager = new Utf8JsonReaderManager(data);
        var entity = new Visits();
        var tokenType = JsonTokenType.None;
        var depth = 0;
        while (depth > 0 || tokenType != JsonTokenType.EndObject)
        {
            tokenType = manager.MoveNext();

            switch (tokenType)
            {
                case JsonTokenType.PropertyName:
                    if (manager.CurrentReader.ValueTextEquals("Location"u8))
                    {
                        manager.MoveNext();
                        entity.Location = LocationJsonValueReader.FromJson(ref manager);
                    }
                    else if (manager.CurrentReader.ValueTextEquals("Browsers"u8))
                    {
                        manager.MoveNext();
                        tokenType = manager.MoveNext();
                        while (tokenType != JsonTokenType.EndArray)
                        {
                            entity.Browsers.Add(manager.CurrentReader.GetString()!);
                            tokenType = manager.MoveNext();
                        }
                    }
                    else if (manager.CurrentReader.ValueTextEquals("GeoJsonLocation"u8))
                    {
                        manager.MoveNext();
                        entity.GeoJsonLocation = GeoJsonLocationJsonValueReader2.FromJson(ref manager);
                    }
                    else if (manager.CurrentReader.ValueTextEquals("Count"u8))
                    {
                        manager.MoveNext();
                        entity.Count = manager.CurrentReader.GetInt32();
                    }
                    break;

                case JsonTokenType.StartObject:
                case JsonTokenType.StartArray:
                    depth++;
                    break;
                case JsonTokenType.EndObject:
                case JsonTokenType.EndArray:
                    depth--;
                    break;
            }
        }

        manager.CaptureState();
        return entity;
    }

    public static SearchTerm MaterializeSearchTerm(JsonReaderData data)
    {
        var manager = new Utf8JsonReaderManager(data);
        var entity = new SearchTerm();
        var tokenType = JsonTokenType.None;
        var depth = 0;
        while (depth > 0 || tokenType != JsonTokenType.EndObject)
        {
            tokenType = manager.MoveNext();

            switch (tokenType)
            {
                case JsonTokenType.PropertyName:
                    if (manager.CurrentReader.ValueTextEquals("Term"u8))
                    {
                        manager.MoveNext();
                        entity.Term = manager.CurrentReader.GetString()!;
                    }
                    else if (manager.CurrentReader.ValueTextEquals("Count"u8))
                    {
                        manager.MoveNext();
                        entity.Count = manager.CurrentReader.GetInt32()!;
                    }
                    break;

                case JsonTokenType.StartObject:
                case JsonTokenType.StartArray:
                    depth++;
                    break;
                case JsonTokenType.EndObject:
                case JsonTokenType.EndArray:
                    depth--;
                    break;
            }
        }

        manager.CaptureState();
        return entity;
    }

    public static PostUpdate MaterializePostUpdate(JsonReaderData data)
    {
        var manager = new Utf8JsonReaderManager(data);
        var entity = new PostUpdate();
        var tokenType = JsonTokenType.None;
        var depth = 0;
        while (depth > 0 || tokenType != JsonTokenType.EndObject)
        {
            tokenType = manager.MoveNext();

            switch (tokenType)
            {
                case JsonTokenType.PropertyName:
                    if (manager.CurrentReader.ValueTextEquals("PostedFrom"u8))
                    {
                        manager.MoveNext();
                        entity.PostedFrom = PostedFromJsonValueReader.FromJson(ref manager)!;
                    }
                    else if (manager.CurrentReader.ValueTextEquals("UpdatedBy"u8))
                    {
                        manager.MoveNext();
                        entity.UpdatedBy = manager.CurrentReader.GetString()!;
                    }
                    else if (manager.CurrentReader.ValueTextEquals("UpdatedOn"u8))
                    {
                        manager.MoveNext();
                        entity.UpdatedOn = UpdatedOnJsonValueReader.FromJson(ref manager)!;
                    }
                    else if (manager.CurrentReader.ValueTextEquals("Commits"u8))
                    {
                        manager.MoveNext();
                        tokenType = manager.MoveNext();
                        while (tokenType != JsonTokenType.EndArray)
                        {
                            manager.CaptureState();
                            entity.Commits.Add(MaterializeCommit(manager.Data));
                            manager = new Utf8JsonReaderManager(manager.Data);
                            tokenType = manager.MoveNext();
                        }
                    }
                    break;

                case JsonTokenType.StartObject:
                case JsonTokenType.StartArray:
                    depth++;
                    break;
                case JsonTokenType.EndObject:
                case JsonTokenType.EndArray:
                    depth--;
                    break;
            }
        }

        manager.CaptureState();
        return entity;
    }
    
    public static Commit MaterializeCommit(JsonReaderData data)
    {
        var manager = new Utf8JsonReaderManager(data);
        var entity = new Commit();
        var tokenType = JsonTokenType.None;
        var depth = 0;
        while (depth > 0 || tokenType != JsonTokenType.EndObject)
        {
            tokenType = manager.MoveNext();

            switch (tokenType)
            {
                case JsonTokenType.PropertyName:
                    if (manager.CurrentReader.ValueTextEquals("Comment"u8))
                    {
                        manager.MoveNext();
                        entity.Comment = manager.CurrentReader.GetString()!;
                    }
                    else if (manager.CurrentReader.ValueTextEquals("CommittedOn"u8))
                    {
                        manager.MoveNext();
                        entity.CommittedOn = CommittedOnJsonValueReader.FromJson(ref manager);
                    }
                    break;

                case JsonTokenType.StartObject:
                case JsonTokenType.StartArray:
                    depth++;
                    break;
                case JsonTokenType.EndObject:
                case JsonTokenType.EndArray:
                    depth--;
                    break;
            }
        }

        manager.CaptureState();
        return entity;
    }

    public static ContactDetails MaterializeContact(JsonReaderData data)
    {
        var manager = new Utf8JsonReaderManager(data);
        var entity = new ContactDetails();
        var tokenType = JsonTokenType.None;
        var depth = 0;
        while (depth > 0 || tokenType != JsonTokenType.EndObject)
        {
            tokenType = manager.MoveNext();

            switch (tokenType)
            {
                case JsonTokenType.PropertyName:
                    if (manager.CurrentReader.ValueTextEquals("Address"u8))
                    {
                        manager.MoveNext();
                        manager.CaptureState();
                        entity.Address = MaterializeAddress(manager.Data);
                        manager = new Utf8JsonReaderManager(manager.Data);
                    }
                    else if (manager.CurrentReader.ValueTextEquals("Phone"u8))
                    {
                        manager.MoveNext();
                        entity.Phone = manager.CurrentReader.GetString()!;
                    }
                    break;

                case JsonTokenType.StartObject:
                case JsonTokenType.StartArray:
                    depth++;
                    break;
                case JsonTokenType.EndObject:
                case JsonTokenType.EndArray:
                    depth--;
                    break;
            }
        }

        manager.CaptureState();
        return entity;
    }

    public static Address MaterializeAddress(JsonReaderData data)
    {
        var manager = new Utf8JsonReaderManager(data);
        var entity = new Address();
        var tokenType = JsonTokenType.None;
        var depth = 0;
        while (depth > 0 || tokenType != JsonTokenType.EndObject)
        {
            tokenType = manager.MoveNext();

            switch (tokenType)
            {
                case JsonTokenType.PropertyName:
                    if (manager.CurrentReader.ValueTextEquals("Street"u8))
                    {
                        manager.MoveNext();
                        entity.Street = manager.CurrentReader.GetString()!;
                    }
                    else if (manager.CurrentReader.ValueTextEquals("City"u8))
                    {
                        manager.MoveNext();
                        entity.City = manager.CurrentReader.GetString()!;
                    }
                    else if (manager.CurrentReader.ValueTextEquals("Postcode"u8))
                    {
                        manager.MoveNext();
                        entity.Postcode = manager.CurrentReader.GetString()!;
                    }
                    else if (manager.CurrentReader.ValueTextEquals("Country"u8))
                    {
                        manager.MoveNext();
                        entity.Country = manager.CurrentReader.GetString()!;
                    }
                    break;

                case JsonTokenType.StartObject:
                case JsonTokenType.StartArray:
                    depth++;
                    break;
                case JsonTokenType.EndObject:
                case JsonTokenType.EndArray:
                    depth--;
                    break;
            }
        }

        manager.CaptureState();
        return entity;
    }

    private static readonly DateOnlyJsonValueReader CommittedOnJsonValueReader = new();
    private static readonly IpAddressJsonValueReader PostedFromJsonValueReader = new();
    private static readonly DateOnlyJsonValueReader UpdatedOnJsonValueReader = new();
    private static readonly PointJsonValueReader LocationJsonValueReader = new();
    private static readonly GeoJsonPointJsonValueReader4 GeoJsonLocationJsonValueReader2 = new();
}