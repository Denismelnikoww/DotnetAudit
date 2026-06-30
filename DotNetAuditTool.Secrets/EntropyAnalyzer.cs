using DotNetAuditTool.Core.Models;
using System.Text.RegularExpressions;

namespace DotNetAuditTool.Secrets;

public class EntropyAnalyzer
{
    /// <summary>
    /// Вычисляет энтропию Шеннона для строки (0-8 bits per char)
    /// Высокая энтропия (> 4.5) может указывать на зашифрованное или случайное значение
    /// </summary>
    public static double CalculateShannonEntropy(string input)
    {
        if (string.IsNullOrEmpty(input))
            return 0;

        var frequency = new Dictionary<char, int>();

        foreach (char c in input)
        {
            if (frequency.ContainsKey(c))
                frequency[c]++;
            else
                frequency[c] = 1;
        }

        double entropy = 0;
        int length = input.Length;

        foreach (var count in frequency.Values)
        {
            double probability = (double)count / length;
            entropy -= probability * Math.Log2(probability);
        }

        return entropy;
    }

    private static bool IsLikelyPathOrUrl(string value)
    {
        var patterns = new[]
        {
            @"^[a-zA-Z]:\\",           // Windows path
            @"^/[\w/.-]+",             // Unix path
            @"^https?://",             // URL
            @"^\.\.?/",                // Relative path
            @"\.(json|xml|yaml|yml|txt|log|cs|vb|fs)$"  // File extension
        };

        return patterns.Any(p => Regex.IsMatch(value, p, RegexOptions.IgnoreCase));
    }

    private static bool IsAlphanumeric(string value)
    {
        return value.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');
    }

    private static bool HasSpecialCharacters(string value)
    {
        return value.Any(c => !char.IsLetterOrDigit(c) && c != '-' && c != '_' && c != '.');
    }

    /// <summary>
    /// Проверяет наличие повторяющихся паттернов (признак не случайной строки)
    /// </summary>
    public static bool HasRepeatPatterns(string value)
    {
        var patterns = new[]
        {
            @"(.{4,})\1{2,}",     // Повторяющиеся подстроки
            @"^(.)\1{5,}",        // Много одинаковых символов в начале
            @"123456|qwerty|abcdef|password"  // Общие паттерны
        };

        return patterns.Any(p => Regex.IsMatch(value, p, RegexOptions.IgnoreCase));
    }
}