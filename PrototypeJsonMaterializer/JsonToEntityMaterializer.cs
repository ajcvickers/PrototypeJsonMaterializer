using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace PrototypeJsonMaterializer;

public class JsonToEntityMaterializer
{
    private static readonly MethodInfo MoveNextMethod =
        typeof(Utf8JsonReaderManager).GetMethod(nameof(Utf8JsonReaderManager.MoveNext))!;

    private static readonly MethodInfo ValueTextEqualsMethod =
        typeof(JsonToEntityMaterializer).GetMethod(nameof(ValueTextEquals))!;

    private static readonly FieldInfo CurrentReaderField =
        typeof(Utf8JsonReaderManager).GetField(nameof(Utf8JsonReaderManager.CurrentReader))!;

    private static readonly Dictionary<Type, MethodInfo> PrimitiveMethods
        = new()
        {
            { typeof(int), typeof(Utf8JsonReader).GetMethod(nameof(Utf8JsonReader.GetInt32))! },
            { typeof(string), typeof(Utf8JsonReader).GetMethod(nameof(Utf8JsonReader.GetString))! },
            { typeof(double), typeof(Utf8JsonReader).GetMethod(nameof(Utf8JsonReader.GetDouble))! },
        };

    private static readonly Dictionary<IEntityType, LambdaExpression> JsonToEntityMaterializers = new();

    public static Expression<Func<JsonReaderData, TEntity>> CreateJsonMaterializer<TEntity>(IEntityType entityType)
        => (Expression<Func<JsonReaderData, TEntity>>)GetOrCreateMaterializer(entityType);

    public static bool ValueTextEquals(ref Utf8JsonReaderManager manager, JsonEncodedText json)
        => manager.CurrentReader.ValueTextEquals(json.EncodedUtf8Bytes);

    private static LambdaExpression GetOrCreateMaterializer(IEntityType entityType)
    {
        if (JsonToEntityMaterializers.TryGetValue(entityType, out var materializer))
        {
            return materializer;
        }

        var dataParameter = Expression.Parameter(typeof(JsonReaderData), "data");
        var clrType = entityType.ClrType;
        var entityVariable = Expression.Variable(clrType, "entity");
        var tokenTypeVariable = Expression.Variable(typeof(JsonTokenType), "tokenType");
        var depthVariable = Expression.Variable(typeof(int), "depth");
        var readDoneLabel = Expression.Label("readDone");
        var managerVariable = Expression.Variable(typeof(Utf8JsonReaderManager), "manager");

        var testExpressions = new List<Expression>();
        var readExpressions = new List<Expression>();

        foreach (var property in entityType.GetProperties().Where(p => !p.IsShadowProperty()))
        {
            testExpressions.Add(
                Expression.Call(
                    ValueTextEqualsMethod,
                    managerVariable,
                    Expression.Constant(JsonEncodedText.Encode(property.GetJsonPropertyName()!))));

            var typeMapping = property.GetTypeMapping();

            if (typeMapping.ElementTypeMapping != null)
            {
                var jsonValueReader = typeMapping.ElementTypeMapping.GetJsonValueReader();
                var arrayDoneLabel = Expression.Label("arrayDoneLabel");

                readExpressions.Add(
                    Expression.Block(
                        Expression.Call(managerVariable, MoveNextMethod),
                        Expression.Assign(tokenTypeVariable, Expression.Call(managerVariable, MoveNextMethod)),
                        Expression.Loop(
                            Expression.IfThenElse(
                                Expression.NotEqual(tokenTypeVariable, Expression.Constant(JsonTokenType.EndArray)),
                                Expression.Block(
                                    Expression.Call(
                                        Expression.MakeMemberAccess(entityVariable,
                                            clrType.GetProperty(property.Name)!),
                                        property.ClrType.GetMethod("Add")!,
                                        jsonValueReader == null
                                            ? Expression.Call(
                                                Expression.Field(managerVariable, CurrentReaderField),
                                                PrimitiveMethods[typeMapping.ElementTypeMapping.ClrType])
                                            : Expression.Call(
                                                Expression.Constant(jsonValueReader),
                                                jsonValueReader.GetType().GetMethod("FromJson")!,
                                                managerVariable)),
                                    Expression.Assign(tokenTypeVariable,
                                        Expression.Call(managerVariable, MoveNextMethod))),
                                Expression.Break(arrayDoneLabel)),
                            arrayDoneLabel)));
            }
            else
            {
                var jsonValueReader = typeMapping.GetJsonValueReader();
                readExpressions.Add(
                    Expression.Block(
                        Expression.Call(managerVariable, MoveNextMethod),
                        Expression.Assign(
                            Expression.MakeMemberAccess(entityVariable, clrType.GetProperty(property.Name)!),
                            jsonValueReader == null
                                ? Expression.Call(
                                    Expression.Field(managerVariable, CurrentReaderField),
                                    PrimitiveMethods[typeMapping.ClrType])
                                : Expression.Call(
                                    Expression.Constant(jsonValueReader),
                                    jsonValueReader.GetType().GetMethod("FromJson")!,
                                    managerVariable))));
            }
        }

        foreach (var navigation in entityType.GetNavigations().Where(n => !n.IsOnDependent))
        {
            testExpressions.Add(
                Expression.Call(
                    ValueTextEqualsMethod,
                    managerVariable,
                    Expression.Constant(JsonEncodedText.Encode(navigation.Name))));

            var arrayDoneLabel = Expression.Label("arrayDoneLabel");

            if (navigation.IsCollection)
            {
                readExpressions.Add(
                    Expression.Block(
                        Expression.Call(managerVariable, MoveNextMethod),
                        Expression.Assign(tokenTypeVariable, Expression.Call(managerVariable, MoveNextMethod)),
                        Expression.Loop(
                            Expression.IfThenElse(
                                Expression.NotEqual(tokenTypeVariable, Expression.Constant(JsonTokenType.EndArray)),
                                Expression.Block(
                                    Expression.Block(
                                        Expression.Call(
                                            managerVariable,
                                            typeof(Utf8JsonReaderManager).GetMethod(
                                                nameof(Utf8JsonReaderManager.CaptureState))!),
                                        Expression.Call(
                                            Expression.MakeMemberAccess(entityVariable,
                                                clrType.GetProperty(navigation.Name)!),
                                            navigation.ClrType.GetMethod("Add")!,
                                            Expression.Invoke(
                                                GetOrCreateMaterializer(navigation.TargetEntityType),
                                                dataParameter)),
                                        Expression.Assign(managerVariable,
                                            Expression.New(
                                                typeof(Utf8JsonReaderManager).GetConstructor(new[]
                                                    { typeof(JsonReaderData) })!,
                                                dataParameter)),
                                        Expression.Assign(tokenTypeVariable,
                                            Expression.Call(managerVariable, MoveNextMethod)))),
                                Expression.Break(arrayDoneLabel)),
                            arrayDoneLabel)));
            }
            else
            {
                readExpressions.Add(
                    Expression.Block(
                        Expression.Call(managerVariable, MoveNextMethod),
                        Expression.Call(
                            managerVariable,
                            typeof(Utf8JsonReaderManager).GetMethod(
                                nameof(Utf8JsonReaderManager.CaptureState))!),
                        Expression.Assign(
                            Expression.MakeMemberAccess(entityVariable, clrType.GetProperty(navigation.Name)!),
                            Expression.Invoke(
                                GetOrCreateMaterializer(navigation.TargetEntityType),
                                dataParameter)),
                        Expression.Assign(managerVariable,
                            Expression.New(
                                typeof(Utf8JsonReaderManager).GetConstructor(new[]
                                    { typeof(JsonReaderData) })!,
                                dataParameter))));
            }
        }

        var testsCount = testExpressions.Count;
        var testExpression = Expression.IfThen(
            testExpressions[testsCount - 1],
            readExpressions[testsCount - 1]);

        for (var i = testsCount - 2; i >= 0; i--)
        {
            testExpression = Expression.IfThenElse(
                testExpressions[i],
                readExpressions[i],
                testExpression);
        }

        var tokenTypeCases = new List<SwitchCase>
        {
            Expression.SwitchCase(
                Expression.Block(
                    testExpression,
                    Expression.Empty()),
                Expression.Constant(JsonTokenType.PropertyName)),
            Expression.SwitchCase(
                Expression.Block(
                    Expression.Assign(depthVariable, Expression.Increment(depthVariable)),
                    Expression.Empty()),
                Expression.Constant(JsonTokenType.StartObject),
                Expression.Constant(JsonTokenType.StartArray)),
            Expression.SwitchCase(
                Expression.Block(
                    Expression.Assign(depthVariable, Expression.Decrement(depthVariable)),
                    Expression.Empty()),
                Expression.Constant(JsonTokenType.EndObject),
                Expression.Constant(JsonTokenType.EndArray)),
        };

        var lambdaExpression = Expression.Lambda(
            typeof(Func<,>).MakeGenericType(typeof(JsonReaderData), clrType),
            Expression.Block(
                new[] { managerVariable, entityVariable, tokenTypeVariable, depthVariable },
                Expression.Assign(managerVariable,
                    Expression.New(
                        typeof(Utf8JsonReaderManager).GetConstructor(new[] { typeof(JsonReaderData) })!,
                        dataParameter)),
                Expression.Assign(entityVariable, Expression.New(clrType.GetConstructor(Type.EmptyTypes)!)),
                Expression.Assign(tokenTypeVariable, Expression.Constant(JsonTokenType.None)),
                Expression.Assign(depthVariable, Expression.Constant(0)),
                Expression.Loop(
                    Expression.IfThenElse(
                        Expression.OrElse(
                            Expression.GreaterThan(depthVariable, Expression.Constant(0)),
                            Expression.NotEqual(tokenTypeVariable, Expression.Constant(JsonTokenType.EndObject))),
                        Expression.Block(
                            Expression.Assign(tokenTypeVariable, Expression.Call(managerVariable, MoveNextMethod)),
                            Expression.Switch(tokenTypeVariable, null, null, tokenTypeCases)),
                        Expression.Break(readDoneLabel)),
                    readDoneLabel),
                Expression.Call(
                    managerVariable,
                    typeof(Utf8JsonReaderManager).GetMethod(nameof(Utf8JsonReaderManager.CaptureState))!),
                entityVariable),
            dataParameter);

        materializer = lambdaExpression;

        JsonToEntityMaterializers[entityType] = materializer;
        return materializer;
    }
}