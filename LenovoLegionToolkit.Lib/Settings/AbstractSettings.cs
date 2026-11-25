using System;
using System.IO;
using LenovoLegionToolkit.Lib.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace LenovoLegionToolkit.Lib.Settings;

public abstract class AbstractSettings<T> where T : class, new()
{
    private static readonly ISerializationBinder SafeTypeBinder = new AllowedTypesSerializationBinder();

    protected readonly JsonSerializerSettings JsonSerializerSettings;
    private readonly string _settingsStorePath;
    private readonly string _fileName;
    private volatile T? _store;
    private readonly object _saveLock = new();

    protected virtual T Default => new();

    public T Store
    {
        get
        {
            var store = _store;
            if (store is not null)
                return store;

            lock (_saveLock)
            {
                return _store ??= LoadStoreCore() ?? Default;
            }
        }
        protected set
        {
            lock (_saveLock)
            {
                _store = value;
            }
        }
    }

    protected AbstractSettings(string filename)
    {
        JsonSerializerSettings = new()
        {
            Formatting = Formatting.Indented,
            TypeNameHandling = TypeNameHandling.Auto,
            SerializationBinder = SafeTypeBinder,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            Converters = { new StringEnumConverter() }
        };

        _fileName = filename;
        _settingsStorePath = Path.Combine(Folders.AppData, _fileName);
    }

    public void Save()
    {
        lock (_saveLock)
        {
            try
            {
                var settingsSerialized = JsonConvert.SerializeObject(Store, JsonSerializerSettings);
                Folders.EnsureParentDirectoryExists(_settingsStorePath);
                WriteAllTextAtomic(settingsSerialized);
            }
            catch (Exception ex)
            {
                Log.Instance.Trace($"Unable to save {_fileName}", ex);
            }
        }
    }

    public void EnsureFileExists()
    {
        if (!File.Exists(_settingsStorePath))
        {
            Save();
        }
    }

    public void Reset()
    {
        lock (_saveLock)
        {
            _store = null;
        }
    }

    public virtual T? LoadStore()
    {
        lock (_saveLock)
        {
            return LoadStoreCore();
        }
    }

    private T? LoadStoreCore()
    {
        T? store = null;
        try
        {
            if (!File.Exists(_settingsStorePath)) return null;

            var settingsSerialized = File.ReadAllText(_settingsStorePath);
            store = JsonConvert.DeserializeObject<T>(settingsSerialized, JsonSerializerSettings);

            if (store is null)
                TryBackup();
        }
        catch
        {
            TryBackup();
        }

        return store;
    }

    public void SynchronizeStore()
    {
        var storeSnapshot = Store;
        lock (_saveLock)
        {
            try
            {
                var settingsSerialized = JsonConvert.SerializeObject(storeSnapshot, JsonSerializerSettings);
                Folders.EnsureParentDirectoryExists(_settingsStorePath);
                WriteAllTextAtomic(settingsSerialized);
            }
            catch (Exception ex)
            {
                Log.Instance.Trace($"Unable to synchronize {_fileName}", ex);
            }
        }
    }

    private void WriteAllTextAtomic(string contents)
    {
        var tempPath = $"{_settingsStorePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(tempPath, contents);
            File.Move(tempPath, _settingsStorePath, overwrite: true);
        }
        catch
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { /* Ignore */ }
            throw;
        }
    }

    private sealed class AllowedTypesSerializationBinder : ISerializationBinder
    {
        private static readonly DefaultSerializationBinder Inner = new();

        public Type BindToType(string? assemblyName, string typeName)
        {
            if (!IsAllowed(assemblyName, typeName))
                throw new JsonSerializationException($"Disallowed type in settings JSON: '{typeName}' ({assemblyName ?? "no assembly"}).");

            var type = Inner.BindToType(assemblyName, typeName);
            if (!IsAllowedType(type))
                throw new JsonSerializationException($"Disallowed resolved type in settings JSON: '{type.FullName}'.");

            return type;
        }

        public void BindToName(Type serializedType, out string? assemblyName, out string? typeName) =>
            Inner.BindToName(serializedType, out assemblyName, out typeName);

        private static bool IsAllowed(string? assemblyName, string typeName)
        {
            if (IsToolkitAssembly(assemblyName) || typeName.StartsWith("LenovoLegionToolkit.", StringComparison.Ordinal))
                return true;

            return typeName.StartsWith("System.Collections.Generic.", StringComparison.Ordinal)
                   || typeName.StartsWith("System.Drawing.", StringComparison.Ordinal)
                   || typeName is "System.String" or "System.Guid" or "System.TimeSpan" or "System.DateTime"
                       or "System.DateOnly" or "System.TimeOnly" or "System.DayOfWeek" or "System.Version"
                       or "System.Decimal" or "System.Int32" or "System.Int64" or "System.Double"
                       or "System.Single" or "System.Boolean" or "System.Byte" or "System.UInt32";
        }

        private static bool IsAllowedType(Type type)
        {
            if (type.IsArray)
                return type.GetElementType() is { } elem && IsAllowedType(elem);

            if (type.IsGenericType)
            {
                var def = type.GetGenericTypeDefinition();
                var defName = def.FullName ?? string.Empty;
                var allowedGeneric = defName.StartsWith("LenovoLegionToolkit.", StringComparison.Ordinal)
                                     || defName.StartsWith("System.Collections.Generic.", StringComparison.Ordinal)
                                     || def == typeof(Nullable<>);
                return allowedGeneric && Array.TrueForAll(type.GetGenericArguments(), IsAllowedType);
            }

            if (type.IsPrimitive || type.IsEnum)
                return true;

            if (IsToolkitAssembly(type.Assembly.GetName().Name))
                return true;

            if (type.Namespace == "System.Drawing" && type.IsValueType)
                return true;

            return type == typeof(string) || type == typeof(Guid) || type == typeof(TimeSpan)
                   || type == typeof(DateTime) || type == typeof(DateOnly) || type == typeof(TimeOnly)
                   || type == typeof(Version) || type == typeof(decimal);
        }

        private static bool IsToolkitAssembly(string? assemblyName) =>
            !string.IsNullOrEmpty(assemblyName) &&
            (assemblyName.StartsWith("LenovoLegionToolkit", StringComparison.Ordinal)
             || assemblyName.StartsWith("Lenovo Legion Toolkit", StringComparison.Ordinal));
    }

    private void TryBackup()
    {
        try
        {
            if (!File.Exists(_settingsStorePath))
                return;

            var backupFileName = $"{Path.GetFileNameWithoutExtension(_fileName)}_backup_{DateTime.UtcNow:yyyyMMddHHmmss}{Path.GetExtension(_fileName)}";
            var backupFilePath = Path.Combine(Folders.AppData, backupFileName);
            Folders.EnsureParentDirectoryExists(backupFilePath);
            File.Copy(_settingsStorePath, backupFilePath);
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"Unable to create backup for {_fileName}", ex);
        }
    }
}
