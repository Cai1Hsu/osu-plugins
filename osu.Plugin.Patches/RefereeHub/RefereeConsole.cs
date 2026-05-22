using System.Collections;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Serialization;
using osu.Framework.Allocation;
using osu.Framework.Extensions.TypeExtensions;
using osu.Framework.Graphics;
using osu.Game.Online.Chat;
using osu.Plugin.Patches.Referee;
using osu.Server.Spectator.Hubs.Referee.Models.Responses;

namespace osu.Plugin.Patches.RefereeHub;

public partial class RefereeConsole : Component
{
    private readonly RefereeClient client;

    public RefereeConsole(RefereeClient client)
    {
        this.client = client;
    }

    public event Action<Message?, object?>? CommandFired;

    public void HandleCommand(string command, object? tag)
    {
        if (!command.StartsWith("!mp", StringComparison.OrdinalIgnoreCase))
            return;

        var subCommand = command[3..].TrimStart();
        var parameters = string.Empty;

        var spaceIndex = subCommand.IndexOf(' ');

        if (spaceIndex != -1)
        {
            subCommand = subCommand[..spaceIndex].ToLowerInvariant();
            parameters = command[3..].TrimStart()[spaceIndex..].TrimStart();
        }

        if (subCommand is "help" or "h" or "?")
        {
            if (string.IsNullOrEmpty(parameters))
            {
                var helpMessages = generateHelpMessages();

                foreach (var message in helpMessages)
                    CommandFired?.Invoke(message, tag);
            }
            else if (commandHandlerCache.TryGetValue(parameters, out var handlerInfo))
            {
                var helpMessage = generateHelpMessageFor(handlerInfo, parameters);
                CommandFired?.Invoke(helpMessage, tag);
            }
            else
            {
                CommandFired?.Invoke(createErrorMessage($"Unknown command: {parameters}"), tag);
            }

            return;
        }

        if (commandHandlerCache.TryGetValue(subCommand, out var handler))
        {
            if (!client.IsConnected.Value)
            {
                CommandFired?.Invoke(createErrorMessage("Not connected to referee hub."), tag);
                return;
            }

            try
            {
                var result = handler.Invoke(parameters);

                if (result is Message message)
                    CommandFired?.Invoke(message, tag);
                else if (result?.ToString() is { } str)
                    CommandFired?.Invoke(createInfoMessage(str), tag);
                else
                    // sending null to simply indicate successful execution without a message
                    CommandFired?.Invoke(null, tag);
            }
            catch (Exception ex)
            {
                CommandFired?.Invoke(createErrorMessage($"Error executing command: {ex.Message}"), tag);
            }
        }
        else
        {
            CommandFired?.Invoke(createErrorMessage($"Unknown command: {subCommand}"), tag);
        }
    }

    private static InfoMessage createInfoMessage(string content) => createMessage(new InfoMessage(content));

    private static ErrorMessage createErrorMessage(string content) => createMessage(new ErrorMessage(content));

    private static T createMessage<T>(T message) where T : Message
    {
        message.Timestamp = DateTimeOffset.Now;
        return message; ;
    }

    private Message generateHelpMessageFor(HandlerInfo handlerInfo, string command)
    {
        var parameters = string.Join(' ', handlerInfo.FlattenedParameters.Select(p => $"<{p.Name}:{p.Type.ReadableName()}>"));

        return createInfoMessage($"!mp {command} {parameters}");
    }

    private Message[] generateHelpMessages()
    {
        List<Message> messages = new List<Message>(new[]
        {
            createInfoMessage("Available commands:"),
        });

        foreach (var (command, handlerInfo) in commandHandlerCache)
            messages.Add(generateHelpMessageFor(handlerInfo, command));

        return messages.ToArray();
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        registerHandler("ping", client.Ping);
        registerHandler("close", client.CloseRoom);
        registerHandler("invite", client.InvitePlayer);
        registerHandler("kick", client.KickPlayer);
        registerHandler("ban", client.BanUser);
        registerHandler("addref", client.AddReferee);
        registerHandler("removeref", client.RemoveReferee);
        registerHandler("settings", client.ChangeRoomSettings);
        registerHandler("roll", client.Roll);
        registerHandler("move", client.MoveUser);
        registerHandler("lock", client.SetLockState);
        registerHandler("start", client.StartMatch);
        registerHandler("stop", client.StopMatchCountdown);
        registerHandler("abort", client.AbortMatch);

        // we may want better name for these commands
        registerHandler("map", client.EditCurrentPlaylistItem);
        registerHandler("addmap", client.AddPlaylistItem);
        registerHandler("editmap", client.EditPlaylistItem);
        registerHandler("removemap", client.RemovePlaylistItem);

        // thses methods have return value, we should output something.
        registerHandler("make", client.MakeRoom, transformRoomJoinedResult);
        registerHandler("join", client.JoinRoom, transformRoomJoinedResult);
        registerHandler("list", client.ListRooms, transformListRoomsResult);
    }

    private static object? transformListRoomsResult(object? o)
    {
        if (o is ListRoomsResponse listResponse)
            return $"Rooms: [{string.Join(", ", listResponse.RoomIDs.Select(id => id))}]";

        return o?.ToString();
    }

    private static object? transformRoomJoinedResult(object? o)
    {
        if (o is RoomJoinedResponse response)
            return $"Joined room {response.Name} ({response.RoomId}) Password: {response.Password}, Players: [{string.Join(", ", response.Players.Select(p => p.UserId))}], Referees: [{string.Join(", ", response.Referees.Select(r => r.UserId))}], Playlist items: [{string.Join(", ", response.Playlist.OrderBy(r => r.Order).Select(i => i.BeatmapId))}]";

        return o?.ToString();
    }

    private readonly Dictionary<string, HandlerInfo> commandHandlerCache = new Dictionary<string, HandlerInfo>();

    private partial class HandlerInfo
    {
        public Delegate Handler { get; }
        public ParameterInfo[] FlattenedParameters { get; }
        public Func<string, object?> Invoke { get; }

        public HandlerInfo(Delegate handler, ParameterInfo[] flattenedParameters, Func<string, object?> invoke)
        {
            Handler = handler;
            FlattenedParameters = flattenedParameters;
            Invoke = invoke;
        }
    }

    private void registerHandler(string name, Delegate handler, Func<object?, object?>? transform = null)
    {
        var flattened = createFattenedParameterList(handler);

        var parameters = handler.Method.GetParameters();

        Func<string, object?> invoke = input =>
        {
            var args = new EnumerableArguments(input).GetEnumerator();

            object?[] invokeArgs = new object?[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                var type = parameters[i].ParameterType;

                if (!parameterParsers.TryGetValue(type, out var parserInfo))
                    throw new InvalidOperationException($"No parser found for type {type}.");

                invokeArgs[i] = parserInfo.Parser(args);
            }

            var result = handler.DynamicInvoke(invokeArgs);

            // FIXME: this blocks thread
            if (result is Task t)
            {
                t.Wait();

                if (t.GetType().IsGenericType)
                    result = t.GetType().GetProperty("Result")!.GetValue(t);
                else
                    result = null;
            }

            if (transform != null)
                return transform(result);

            return result;
        };

        commandHandlerCache[name] = new HandlerInfo(handler, flattened, invoke);
    }

    private ParameterInfo[] createFattenedParameterList(Delegate @delegate)
    {
        var method = @delegate.GetMethodInfo();
        var parameters = method.GetParameters();

        return parameters.SelectMany(p =>
        {
            if (ensurePrimitiveType(p.ParameterType))
            {
                return new[] { new ParameterInfo(p.Name!, p.ParameterType) };
            }
            else
            {
                return createParameterInfoForType(p.ParameterType);
            }
        }).ToArray();
    }

    private static readonly FrozenDictionary<Type, TypeParserInfo> primitiveTypeParsers = new Dictionary<Type, TypeParserInfo>
    {
        [typeof(int)] = new TypeParserInfo(typeof(int), args => { args.MoveNext(); return int.Parse(args.Current); }),
        [typeof(uint)] = new TypeParserInfo(typeof(uint), args => { args.MoveNext(); return uint.Parse(args.Current); }),
        [typeof(string)] = new TypeParserInfo(typeof(string), args => { args.MoveNext(); return args.Current; }),
        [typeof(bool)] = new TypeParserInfo(typeof(bool), args => { args.MoveNext(); return bool.Parse(args.Current); }),
        [typeof(double)] = new TypeParserInfo(typeof(double), args => { args.MoveNext(); return double.Parse(args.Current); }),
        [typeof(float)] = new TypeParserInfo(typeof(float), args => { args.MoveNext(); return float.Parse(args.Current); }),
        [typeof(long)] = new TypeParserInfo(typeof(long), args => { args.MoveNext(); return long.Parse(args.Current); }),
        [typeof(ulong)] = new TypeParserInfo(typeof(ulong), args => { args.MoveNext(); return ulong.Parse(args.Current); }),
        [typeof(short)] = new TypeParserInfo(typeof(short), args => { args.MoveNext(); return short.Parse(args.Current); }),
        [typeof(ushort)] = new TypeParserInfo(typeof(ushort), args => { args.MoveNext(); return ushort.Parse(args.Current); }),
        [typeof(sbyte)] = new TypeParserInfo(typeof(sbyte), args => { args.MoveNext(); return sbyte.Parse(args.Current); }),
        [typeof(byte)] = new TypeParserInfo(typeof(byte), args => { args.MoveNext(); return byte.Parse(args.Current); }),
        [typeof(char)] = new TypeParserInfo(typeof(char), args => { args.MoveNext(); return char.Parse(args.Current); }),
        [typeof(decimal)] = new TypeParserInfo(typeof(decimal), args => { args.MoveNext(); return decimal.Parse(args.Current); }),
    }.ToFrozenDictionary();

    private readonly Dictionary<Type, TypeParserInfo> parameterParsers = new Dictionary<Type, TypeParserInfo>(primitiveTypeParsers);

    // flatten json classes into a single level of parameters
    private IEnumerable<ParameterInfo> createParameterInfoForType(Type type)
    {
        // probably we made a mistake if we are trying to flatten a value type, as they are usually simple types that should be directly parsable.
        Debug.Assert(!type.IsValueType);

        var member = type.GetMembers(BindingFlags.Public | BindingFlags.Instance)
            .Select(m => (m, m.GetCustomAttribute<JsonPropertyNameAttribute>()))
            .Where(t => t.Item2 != null);

        List<ParameterInfo> parameters = new List<ParameterInfo>();

        foreach (var (m, jsonAttribute) in member)
        {
            var fieldName = jsonAttribute!.Name;
            var fieldType = m switch
            {
                FieldInfo f => f.FieldType,
                PropertyInfo p => p.PropertyType,
                _ => throw new InvalidOperationException($"Unsupported member type: {m.MemberType}")
            };

            // unwrap nullable types
            if (Nullable.GetUnderlyingType(fieldType) is Type underlyingType)
                fieldType = underlyingType;

            // The member may also a complex type, so we need to recursively flatten it as well.
            if (ensurePrimitiveType(fieldType))
            {
                parameters.Add(new ParameterInfo(fieldName, fieldType, m));
            }
            else
            {
                parameters.AddRange(createParameterInfoForType(fieldType));
            }
        }

        var parameterList = parameters.ToArray();

        parameterParsers[type] = new TypeParserInfo(type, args =>
        {
            var obj = Activator.CreateInstance(type)!;

            for (int i = 0; i < parameterList.Length; i++)
            {
                var parameterInfo = parameterList[i];

                if (!parameterParsers.TryGetValue(parameterInfo.Type, out var parserInfo))
                    throw new InvalidOperationException($"No parser found for type {parameterInfo.Type}.");

                var parsedValue = parserInfo.Parser(args);

                if (Nullable.GetUnderlyingType(parameterInfo.Type) is Type)
                    parsedValue = Activator.CreateInstance(parameterInfo.Type, parsedValue);

                switch (parameterInfo.Member)
                {
                    case FieldInfo f:
                        f.SetValue(obj, parsedValue);
                        break;
                    case PropertyInfo p:
                        p.SetValue(obj, parsedValue);
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported member type: {parameterInfo.Member?.MemberType}");
                }
            }

            return obj;
        }, parameterList);

        return parameters;
    }

    private bool ensurePrimitiveType(Type type)
    {
        if (primitiveTypeParsers.ContainsKey(type))
            return true;

        if (type.IsEnum)
        {
            parameterParsers[type] = new TypeParserInfo(type, args => { args.MoveNext(); return Enum.Parse(type, args.Current); });
            return true;
        }

        return false;
    }

    private delegate object ParameterParserDelegate(IEnumerator<string> args);

    private record struct TypeParserInfo(Type Type, ParameterParserDelegate Parser, ParameterInfo[]? flattened = null);

    private record struct ParameterInfo(string Name, Type Type, MemberInfo? Member = null);

    public partial class EnumerableArguments : IEnumerable<string>
    {
        private readonly string input;
        public EnumerableArguments(string input)
        {
            this.input = input;
        }

        public IEnumerator<string> GetEnumerator() => new Enumerator(input);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private partial class Enumerator : IEnumerator<string>
        {
            public string Current => input[startPosition..position].Trim('"');

            object IEnumerator.Current => Current;

            private string input;
            private int position;
            private int startPosition;

            public Enumerator(string input)
            {
                this.input = input;
                this.startPosition = 0;
                this.position = 0;
            }

            public bool MoveNext()
            {
                // simple policy: split by space, but ignore spaces inside quotes
                // e.g. `!mp invite "John Doe"` should treat "John Doe" as a single parameter

                bool inQuotes = false;

                startPosition = position;

                // skip trailing spaces of previous parameter
                while (startPosition < input.Length && input[startPosition] is ' ') ++startPosition;

                position = startPosition;

                while (position < input.Length)
                {
                    if (input[position] is '"')
                        inQuotes = !inQuotes;
                    else if (input[position] is ' ' && !inQuotes)
                        break;

                    position++;
                }

                return startPosition < input.Length && position < input.Length;
            }

            public void Reset()
            {
                position = 0;
                startPosition = 0;
            }

            public void Dispose()
            {
            }
        }
    }
}
