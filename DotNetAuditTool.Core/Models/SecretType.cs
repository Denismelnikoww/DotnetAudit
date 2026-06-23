namespace DotNetAuditTool.Core.Models;

public enum SecretType
{
    ApiKey,
    Password,
    ConnectionString,
    PrivateKey,
    JwtToken,
    AccessToken,
    AwsKey,
    AzureKey,
}