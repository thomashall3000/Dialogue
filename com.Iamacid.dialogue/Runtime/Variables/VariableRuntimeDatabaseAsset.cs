using UnityEngine;

[CreateAssetMenu(
    fileName = "VariableRuntimeDatabase",
    menuName = "Dialogue/Runtime Variable Database")]
public class VariableRuntimeDatabaseAsset : ScriptableObject
{
    [SerializeField]
    private TextAsset variableDatabaseJson;

    public TextAsset VariableDatabaseJson => variableDatabaseJson;
}