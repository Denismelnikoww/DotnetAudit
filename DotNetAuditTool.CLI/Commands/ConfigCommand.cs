using DotNetAuditTool.CLI.Services;
using System.CommandLine;

namespace DotNetAuditTool.CLI.Commands;

public static class ConfigCommand
{
    public static Command Create()
    {
        var command = new Command("config", "Manage persistent DotNetAuditTool configuration");

        command.AddCommand(CreateSetThresholdCommand());
        command.AddCommand(CreateShowCommand());

        return command;
    }

    private static Command CreateSetThresholdCommand()
    {
        var command = new Command("set-threshold", "Set persistent threshold values for secret scanning");
        var entropyThresholdOption = new Option<double>(new[] { "--entropy-threshold", "-e" }, "Entropy threshold for secret scanning")
        {
            IsRequired = true
        };

        command.AddOption(entropyThresholdOption);
        command.SetHandler(ExecuteSetThresholdCommand, entropyThresholdOption);

        return command;
    }

    private static async Task<int> ExecuteSetThresholdCommand(double entropyThreshold)
    {
        var console = new ConsoleOutputService();
        try
        {
            var configurationService = new ConfigurationService();
            var settings = configurationService.Load();
            settings.EntropyThreshold = entropyThreshold;
            configurationService.Save(settings);

            console.WriteSuccess($"Saved entropy threshold = {entropyThreshold:F1} to {configurationService.ConfigFilePath}");
            return await Task.FromResult(0);
        }
        catch (Exception ex)
        {
            console.WriteError($"Failed to save configuration: {ex.Message}");
            return await Task.FromResult(1);
        }
    }

    private static Command CreateShowCommand()
    {
        var command = new Command("show", "Display current persistent configuration");
        command.SetHandler(ExecuteShowCommand);
        return command;
    }

    private static async Task<int> ExecuteShowCommand()
    {
        var console = new ConsoleOutputService();
        try
        {
            var configurationService = new ConfigurationService();
            var settings = configurationService.Load();

            console.WriteHeader("DotNetAuditTool Configuration");
            console.WriteInfo($"Config file: {configurationService.ConfigFilePath}");
            console.WriteInfo($"Entropy threshold: {settings.EntropyThreshold:F1}");

            return await Task.FromResult(0);
        }
        catch (Exception ex)
        {
            console.WriteError($"Failed to load configuration: {ex.Message}");
            return await Task.FromResult(1);
        }
    }
}
