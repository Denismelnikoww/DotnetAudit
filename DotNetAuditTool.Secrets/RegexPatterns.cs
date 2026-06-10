namespace DotNetAuditTool.Secrets;

using DotNetAuditTool.Core.Models;
using System.Text.RegularExpressions;

public static class RegexPatterns
{
    /// <summary>
    /// API Keys и Tokens
    /// </summary>
    public static readonly Regex ApiKeyPattern = new(
        @"(?i)(api[_-]?key|apikey|api_token|accesstoken|accesstoken|bearer_token)\s*[:=]\s*['""]?([a-zA-Z0-9\-_]{20,50})['""]?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    /// <summary>
    /// JWT Tokens
    /// </summary>
    public static readonly Regex JwtPattern = new(
        @"eyJ[a-zA-Z0-9\-_]+\.eyJ[a-zA-Z0-9\-_]+\.[a-zA-Z0-9\-_]+",
        RegexOptions.Compiled
    );

    /// <summary>
    /// AWS Keys
    /// </summary>
    public static readonly Regex AwsKeyPattern = new(
        @"(?i)(AKIA|ASIA)[0-9A-Z]{16}",
        RegexOptions.Compiled
    );

    /// <summary>
    /// Azure Keys
    /// </summary>
    public static readonly Regex AzureKeyPattern = new(
        @"(?i)([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})",
        RegexOptions.Compiled
    );

    /// <summary>
    /// GitHub Tokens
    /// </summary>
    public static readonly Regex GithubTokenPattern = new(
        @"(ghp|gho|ghu|ghs|ghr)_[a-zA-Z0-9]{36}",
        RegexOptions.Compiled
    );

    /// <summary>
    /// Generic Password patterns
    /// </summary>
    public static readonly Regex PasswordPattern = new(
        @"(?i)(password|passwd|pwd)\s*[:=]\s*['""]?([^'""\s]{8,50})['""]?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    /// <summary>
    /// Connection Strings
    /// </summary>
    public static readonly Regex ConnectionStringPattern = new(
        @"(?i)(connection[_\s]?string|conn[_\s]?str)\s*[:=]\s*['""]?([^'""]{10,200})['""]?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    /// <summary>
    /// Private Keys (PEM format)
    /// </summary>
    public static readonly Regex PrivateKeyPattern = new(
        @"-----BEGIN (RSA|DSA|EC|OPENSSH) PRIVATE KEY-----[\s\S]+?-----END \1 PRIVATE KEY-----",
        RegexOptions.Compiled
    );


    public static readonly List<(Regex Pattern, SecretType Type, string Name)> All = new()
    {
        (ApiKeyPattern, SecretType.ApiKey, "Generic API Key"),
        (JwtPattern, SecretType.JwtToken, "JWT Token"),
        (AwsKeyPattern, SecretType.AwsKey, "AWS Access Key"),
        (AzureKeyPattern, SecretType.AzureKey, "Azure Key"),
        (GithubTokenPattern, SecretType.AccessToken, "GitHub Token"),
        (PasswordPattern, SecretType.Password, "Password"),
        (ConnectionStringPattern, SecretType.ConnectionString, "Connection String"),
        (PrivateKeyPattern, SecretType.PrivateKey, "Private Key"),
    };
}