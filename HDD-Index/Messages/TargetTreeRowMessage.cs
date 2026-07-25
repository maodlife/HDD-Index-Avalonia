namespace HDD_Index.Messages;

public class TargetTreeRowMessage
{
    public string TreeName { get; set; } = string.Empty;

    public TargetTreeRowMessage(string treeName)
    {
        TreeName = treeName;
    }
}
