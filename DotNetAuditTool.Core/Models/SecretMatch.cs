namespace DotNetAuditTool.Core.Models;

public class SecretMatch
{
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string LineContent { get; set; } = string.Empty;
    public SecretType Type { get; set; }
    public string MatchedValue { get; set; } = string.Empty;
    public double Entropy { get; set; }
    public string Rule { get; set; } = string.Empty;
}
