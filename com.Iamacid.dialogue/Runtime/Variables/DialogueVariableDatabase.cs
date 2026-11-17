using System.Collections.Generic;

public class DialogueVariableDatabase
{

    public const int CurrentSchemaVersion = 1;
    private int schemaVersion = CurrentSchemaVersion;

    private List<DialogueVariableDefinition> variables = new List<DialogueVariableDefinition>();

    public IReadOnlyList<DialogueVariableDefinition> Variables => variables;
    public int SchemaVersion => schemaVersion;

    public DialogueVariableDatabase()
    {

    }
    internal DialogueVariableDatabase(int schemaVersion, IEnumerable<DialogueVariableDefinition> variables)
    {
        this.schemaVersion = schemaVersion;
        this.variables = variables != null ? new List<DialogueVariableDefinition>(variables) : new List<DialogueVariableDefinition>();
    }

    public bool AddVariable(DialogueVariableDefinition variable)
    {

        if (variable == null)
        {
            return false;
        }

        variables.Add(variable);
        return true;
    }
}
