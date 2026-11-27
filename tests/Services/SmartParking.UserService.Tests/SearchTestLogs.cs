using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using SmartParking.OperationService.API;
using SmartParking.OperationService.Application.Discovery;
using SmartParking.OperationService.Domain;

namespace SmartParking.UserService.Tests;

internal sealed class SearchTestLogs : ILoggerProvider, ILogger
{
    public readonly System.Collections.Concurrent.ConcurrentBag<string> Messages = [];
    public ILogger CreateLogger(string categoryName) => this;
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel level) => true;
    public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> format) =>
        Messages.Add(format(state, error));
    public void Dispose() { }
}
