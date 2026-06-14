namespace PhongKham.Data;

public class DatabaseRuntimeState
{
    public bool IsAvailable { get; private set; }

    public string? LastError { get; private set; }

    public void MarkAvailable()
    {
        IsAvailable = true;
        LastError = null;
    }

    public void MarkUnavailable(string? error)
    {
        IsAvailable = false;
        LastError = error;
    }
}
