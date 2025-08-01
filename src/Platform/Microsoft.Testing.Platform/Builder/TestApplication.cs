﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHostControllers;

namespace Microsoft.Testing.Platform.Builder;

/// <summary>
/// Represents a test application.
/// </summary>
public sealed class TestApplication : ITestApplication
#if NETCOREAPP
#pragma warning disable SA1001 // Commas should be spaced correctly
    , IAsyncDisposable
#pragma warning restore SA1001 // Commas should be spaced correctly
#endif
{
    private readonly IHost _host;
    private static UnhandledExceptionHandler? s_unhandledExceptionHandler;

    static TestApplication() =>
        // Capture system console soon as possible to avoid any other code from changing it.
        // This is important for the console display system to work properly.
        _ = new SystemConsole();

    internal TestApplication(IHost host) => _host = host;

    // This cast looks like incorrect assumption.
    // This property is currently accessed in unit tests only.
    internal IServiceProvider ServiceProvider => ((CommonHost)_host).ServiceProvider;

    /// <summary>
    /// Creates a server mode builder asynchronously.
    /// </summary>
    /// <param name="args">The command line arguments.</param>
    /// <param name="testApplicationOptions">The test application options.</param>
    /// <returns>The task representing the asynchronous operation.</returns>
    public static Task<ITestApplicationBuilder> CreateServerModeBuilderAsync(string[] args, TestApplicationOptions? testApplicationOptions = null)
    {
        if (args.Contains($"--{PlatformCommandLineProvider.ServerOptionKey}") || args.Contains($"-{PlatformCommandLineProvider.ServerOptionKey}"))
        {
            // Remove the --server option from the args so that the builder can be created.
            args = [.. args.Where(arg => arg.Trim('-') != PlatformCommandLineProvider.ServerOptionKey)];
        }

        return CreateBuilderAsync([.. args, $"--{PlatformCommandLineProvider.ServerOptionKey}"], testApplicationOptions);
    }

    /// <summary>
    /// Creates a builder asynchronously.
    /// </summary>
    /// <param name="args">The command line arguments.</param>
    /// <param name="testApplicationOptions">The test application options.</param>
    /// <returns>The task representing the asynchronous operation.</returns>
    public static async Task<ITestApplicationBuilder> CreateBuilderAsync(string[] args, TestApplicationOptions? testApplicationOptions = null)
    {
        SystemEnvironment systemEnvironment = new();

        // See AB#2304879.
        UILanguageOverride.SetCultureSpecifiedByUser(systemEnvironment);

        // We get the time to save it in the logs for testcontrollers troubleshooting.
        SystemClock systemClock = new();
        DateTimeOffset createBuilderStart = systemClock.UtcNow;
        string createBuilderEntryTime = createBuilderStart.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
        testApplicationOptions ??= new TestApplicationOptions();

        SystemConsole systemConsole = new();
        SystemProcessHandler systemProcess = new();
        AttachDebuggerIfNeeded(systemEnvironment, systemConsole, systemProcess);

        // First step is to parse the command line from where we get the second input layer.
        // The first one should be the env vars handled autonomously by extensions and part of the test platform.
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, systemEnvironment);
        TestHostControllerInfo testHostControllerInfo = new(parseResult);
        CurrentTestApplicationModuleInfo testApplicationModuleInfo = new(systemEnvironment, systemProcess);

        // Create the UnhandledExceptionHandler that will be set inside the TestHostBuilder.
        LazyInitializer.EnsureInitialized(ref s_unhandledExceptionHandler, () => new UnhandledExceptionHandler(systemEnvironment, systemConsole, parseResult.IsOptionSet(PlatformCommandLineProvider.TestHostControllerPIDOptionKey)));
        ApplicationStateGuard.Ensure(s_unhandledExceptionHandler is not null);

        // First task is to setup the logger if enabled and we take the info from the command line or env vars.
        ApplicationLoggingState loggingState = CreateFileLoggerIfDiagnosticIsEnabled(parseResult, testApplicationModuleInfo, systemClock, systemEnvironment, new SystemTask(), new SystemConsole());

        if (loggingState.FileLoggerProvider is not null)
        {
            ILogger logger = loggingState.FileLoggerProvider.CreateLogger(typeof(TestApplication).ToString());
            s_unhandledExceptionHandler.SetLogger(logger);
            await LogInformationAsync(logger, testApplicationModuleInfo, testHostControllerInfo, systemEnvironment, createBuilderEntryTime, loggingState.IsSynchronousWrite, loggingState.LogLevel, args).ConfigureAwait(false);
        }

        // All checks are fine, create the TestApplication.
        return new TestApplicationBuilder(loggingState, createBuilderStart, testApplicationOptions, s_unhandledExceptionHandler);
    }

    private static async Task LogInformationAsync(
        ILogger logger,
        CurrentTestApplicationModuleInfo testApplicationModuleInfo,
        TestHostControllerInfo testHostControllerInfo,
        SystemEnvironment environment,
        string createBuilderEntryTime,
        bool syncWrite,
        LogLevel loggerLevel,
        string[] args)
    {
        // Log useful information
        AssemblyInformationalVersionAttribute? version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (version is not null)
        {
            await logger.LogInformationAsync($"Version: {version.InformationalVersion}").ConfigureAwait(false);
        }
        else
        {
            await logger.LogInformationAsync("Version attribute not found").ConfigureAwait(false);
        }

        await logger.LogInformationAsync("Logging mode: " + (syncWrite ? "synchronous" : "asynchronous")).ConfigureAwait(false);
        await logger.LogInformationAsync($"Logging level: {loggerLevel}").ConfigureAwait(false);
        await logger.LogInformationAsync($"CreateBuilderAsync entry time: {createBuilderEntryTime}").ConfigureAwait(false);
        await logger.LogInformationAsync($"PID: {environment.ProcessId}").ConfigureAwait(false);

#if NETCOREAPP
        string runtimeInformation = $"{RuntimeInformation.RuntimeIdentifier} - {RuntimeInformation.FrameworkDescription}";
#else
        string runtimeInformation = $"{RuntimeInformation.ProcessArchitecture} - {RuntimeInformation.FrameworkDescription}";
#endif
        await logger.LogInformationAsync($"Runtime information: {runtimeInformation}").ConfigureAwait(false);

#if NETCOREAPP
        if (RuntimeFeature.IsDynamicCodeSupported)
        {
#pragma warning disable IL3000 // Avoid accessing Assembly file path when publishing as a single file
            string? runtimeLocation = typeof(object).Assembly.Location;
#pragma warning restore IL3000 // Avoid accessing Assembly file path when publishing as a single file
            if (runtimeLocation is not null)
            {
                await logger.LogInformationAsync($"Runtime location: {runtimeLocation}").ConfigureAwait(false);
            }
            else
            {
                await logger.LogInformationAsync("Runtime location not found.").ConfigureAwait(false);
            }
        }
#else
#pragma warning disable IL3000 // Avoid accessing Assembly file path when publishing as a single file, this branch run only on .NET Framework
        string? runtimeLocation = typeof(object).Assembly?.Location;
#pragma warning restore IL3000 // Avoid accessing Assembly file path when publishing as a single file
        if (runtimeLocation is not null)
        {
            await logger.LogInformationAsync($"Runtime location: {runtimeLocation}").ConfigureAwait(false);
        }
        else
        {
            await logger.LogInformationAsync($"Runtime location not found.").ConfigureAwait(false);
        }
#endif

        bool isDynamicCodeSupported = false;
#if NETCOREAPP
        isDynamicCodeSupported = RuntimeFeature.IsDynamicCodeSupported;
#endif
        await logger.LogInformationAsync($"IsDynamicCodeSupported: {isDynamicCodeSupported}").ConfigureAwait(false);

        string moduleName = testApplicationModuleInfo.GetDisplayName();
        await logger.LogInformationAsync($"Test module: {moduleName}").ConfigureAwait(false);
        await logger.LogInformationAsync($"Command line arguments: '{(args.Length == 0 ? string.Empty : args.Aggregate((a, b) => $"{a} {b}"))}'").ConfigureAwait(false);

        StringBuilder machineInfo = new();
#pragma warning disable RS0030 // Do not use banned APIs
        machineInfo.AppendLine(CultureInfo.InvariantCulture, $"Machine name: {Environment.MachineName}");
        machineInfo.AppendLine(CultureInfo.InvariantCulture, $"OSVersion: {Environment.OSVersion}");
        machineInfo.AppendLine(CultureInfo.InvariantCulture, $"ProcessorCount: {Environment.ProcessorCount}");
        machineInfo.AppendLine(CultureInfo.InvariantCulture, $"Is64BitOperatingSystem: {Environment.Is64BitOperatingSystem}");
#pragma warning restore RS0030 // Do not use banned APIs
#if NETCOREAPP
        machineInfo.AppendLine(CultureInfo.InvariantCulture, $"TotalAvailableMemoryBytes(GB): {GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1_000_000_000}");
#endif
        await logger.LogDebugAsync($"Machine info:\n{machineInfo}").ConfigureAwait(false);

        if (testHostControllerInfo.HasTestHostController)
        {
            int? testHostControllerPID = testHostControllerInfo.GetTestHostControllerPID();

            await LogVariableAsync(EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_CORRELATIONID).ConfigureAwait(false);
            await LogVariableAsync(EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_PARENTPID).ConfigureAwait(false);
            await LogVariableAsync(EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_TESTHOSTPROCESSSTARTTIME).ConfigureAwait(false);

            async Task LogVariableAsync(string key)
            {
                string? value;
                key = $"{key}_{testHostControllerPID}";
                if ((value = environment.GetEnvironmentVariable(key)) is not null)
                {
                    await logger.LogDebugAsync($"{key} '{value}'").ConfigureAwait(false);
                }
            }
        }

        await logger.LogInformationAsync($"{EnvironmentVariableConstants.TESTINGPLATFORM_DEFAULT_HANG_TIMEOUT}: '{environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DEFAULT_HANG_TIMEOUT)}'").ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
        => (_host as IDisposable)?.Dispose();

#if NETCOREAPP
    /// <inheritdoc />
    public ValueTask DisposeAsync()
        => _host is IAsyncDisposable asyncDisposable
            ? asyncDisposable.DisposeAsync()
            : ValueTask.CompletedTask;
#endif

    /// <inheritdoc />
    public async Task<int> RunAsync()
        => await _host.RunAsync().ConfigureAwait(false);

    private static void AttachDebuggerIfNeeded(SystemEnvironment environment, SystemConsole console, SystemProcessHandler systemProcess)
    {
        if (environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_LAUNCH_ATTACH_DEBUGGER) == "1")
        {
            Debugger.Launch();
        }

        if (environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_WAIT_ATTACH_DEBUGGER) == "1")
        {
            using IProcess currentProcess = systemProcess.GetCurrentProcess();
            console.WriteLine($"Waiting for debugger to attach... Process Id: {environment.ProcessId}, Name: {currentProcess.Name}");

            while (!Debugger.IsAttached)
            {
                Thread.Sleep(1000);
            }

            Debugger.Break();
        }
    }

    /*
     The expected order for the final logs directory is (most to least important):
     1 Environment variable
     2 Command line
     3 TA settings(json)
     4 Default(TestResults in the current working folder)
    */
    private static ApplicationLoggingState CreateFileLoggerIfDiagnosticIsEnabled(
        CommandLineParseResult result, CurrentTestApplicationModuleInfo testApplicationModuleInfo, SystemClock clock,
        SystemEnvironment environment, SystemTask task, SystemConsole console)
    {
        LogLevel logLevel = LogLevel.None;

        if (result.HasError)
        {
            return new(logLevel, result);
        }

        string? environmentVariable = environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC);
        if (!result.IsOptionSet(PlatformCommandLineProvider.DiagnosticOptionKey))
        {
            // Environment variable is set, but the command line option is not set
            if (environmentVariable != "1")
            {
                return new(logLevel, result);
            }
        }

        // Environment variable is set to 0 and takes precedence over the command line option
        if (environmentVariable == "0")
        {
            return new(logLevel, result);
        }

        logLevel = LogLevel.Trace;

        if (result.TryGetOptionArgumentList(PlatformCommandLineProvider.DiagnosticVerbosityOptionKey, out string[]? verbosity))
        {
            logLevel = Enum.Parse<LogLevel>(verbosity[0], true);
        }

        // Override the log level if the environment variable is set
        string? environmentLogLevel = environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC_VERBOSITY);
        if (!RoslynString.IsNullOrEmpty(environmentLogLevel))
        {
            if (!Enum.TryParse(environmentLogLevel, out LogLevel parsedLogLevel))
            {
                throw new NotSupportedException($"Invalid environment value '{nameof(EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC_VERBOSITY)}', was expecting 'Trace', 'Debug', 'Information', 'Warning', 'Error', or 'Critical' but got '{environmentLogLevel}'.");
            }

            logLevel = parsedLogLevel;
        }

        // Set the directory to the default test result directory
        string directory = Path.Combine(testApplicationModuleInfo.GetCurrentTestApplicationDirectory(), AggregatedConfiguration.DefaultTestResultFolderName);
        bool customDirectory = false;

        if (result.TryGetOptionArgumentList(PlatformCommandLineProvider.ResultDirectoryOptionKey, out string[]? resultDirectoryArg))
        {
            directory = resultDirectoryArg[0];
            customDirectory = true;
        }

        if (result.TryGetOptionArgumentList(PlatformCommandLineProvider.DiagnosticOutputDirectoryOptionKey, out string[]? directoryArg))
        {
            directory = directoryArg[0];
            customDirectory = true;
        }

        // Override the output directory
        string? environmentOutputDirectory = environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC_OUTPUT_DIRECTORY);
        if (!RoslynString.IsNullOrEmpty(environmentOutputDirectory))
        {
            directory = environmentOutputDirectory;
            customDirectory = true;
        }

        // Finally create the directory
        Directory.CreateDirectory(directory);

        string prefixName = "log";
        if (result.TryGetOptionArgumentList(PlatformCommandLineProvider.DiagnosticOutputFilePrefixOptionKey, out string[]? prefixNameArg))
        {
            prefixName = prefixNameArg[0];
        }

        // Override the prefix name
        string? environmentFilePrefix = environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC_OUTPUT_FILEPREFIX);
        if (!RoslynString.IsNullOrEmpty(environmentFilePrefix))
        {
            prefixName = environmentFilePrefix;
        }

        bool synchronousWrite = result.IsOptionSet(PlatformCommandLineProvider.DiagnosticFileLoggerSynchronousWriteOptionKey);

        // Override the synchronous write
        string? environmentSynchronousWrite = environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC_FILELOGGER_SYNCHRONOUSWRITE);
        if (!RoslynString.IsNullOrEmpty(environmentSynchronousWrite))
        {
            synchronousWrite = environmentSynchronousWrite == "1";
        }

        return new(
            logLevel,
            result,
            new(
                new FileLoggerOptions(
                    directory,
                    prefixName,
                    fileName: null,
                    synchronousWrite),
                logLevel,
                customDirectory,
                clock,
                task,
                console,
                new SystemFileSystem(),
                new SystemFileStreamFactory()),
            synchronousWrite);
    }
}
