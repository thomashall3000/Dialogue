using UnityEngine;

public static class DialogueVariableRuntime
{
    private const string RuntimeDatabasePath = "Dialogue/Generated/VariableRuntimeDatabase";

    private static DialogueVariableRegistry registry;
    private static DialogueVariableDatabase database;

    public static DialogueVariableRegistry Registry => registry;
    public static DialogueVariableDatabase Database => database;

    public static bool IsInitialized => registry != null && database != null;

    public static bool TryInitialize(out string error)
    {
        error = null;

        if (IsInitialized)
            return true;

        VariableRuntimeDatabaseAsset runtimeAsset = Resources.Load<VariableRuntimeDatabaseAsset>(RuntimeDatabasePath);

        if (runtimeAsset == null)
        {
            error = "Could not find the runtime variable database asset.";
            return false;
        }

        if (runtimeAsset.VariableDatabaseJson == null)
        {
            error = "The runtime variable database asset has no JSON reference.";
            return false;
        }

        if (!DialogueVariableDatabaseSerializer.TryDeserialize(runtimeAsset.VariableDatabaseJson.text, out DialogueVariableDatabase loadedDatabase, out error))
        {
            return false;
        }

        if (!DialogueVariableRegistryInitializer.TryCreate(loadedDatabase, out DialogueVariableRegistry loadedRegistry, out error))
        {
            return false;
        }

        database = loadedDatabase;
        registry = loadedRegistry;

        return true;
    }
}