using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;

namespace JNPF.Logging;

/// <summary>
/// 常量、公共方法配置类
/// </summary>
internal static class Penetrates
{
    /// <summary>
    /// 异常分隔符
    /// </summary>
    private const string EXCEPTION_SEPARATOR = "++++++++++++++++++++++++++++++++++++++++++++++++++++++++";

    /// <summary>
    /// 从配置文件中加载配置并创建文件日志记录器提供程序
    /// </summary>
    /// <param name="configuraionKey">获取配置文件对应的 Key</param>
    /// <param name="configure">文件日志记录器配置选项委托</param>
    /// <returns><see cref="FileLoggerProvider"/></returns>
    internal static FileLoggerProvider CreateFromConfiguration(Func<string> configuraionKey, Action<FileLoggerOptions> configure = default)
    {
        // 检查 Key 是否存在
        var key = configuraionKey?.Invoke();
        if (string.IsNullOrWhiteSpace(key)) return new FileLoggerProvider("application.log", new FileLoggerOptions());

        // 加载配置文件中指定节点
        var fileLoggerSettings = App.GetConfig<FileLoggerSettings>(key)
            ?? new FileLoggerSettings();

        // 创建文件日志记录器配置选项
        var fileLoggerOptions = new FileLoggerOptions
        {
            Append = fileLoggerSettings.Append,
            FileSizeLimitBytes = fileLoggerSettings.FileSizeLimitBytes,
            MaxRollingFiles = fileLoggerSettings.MaxRollingFiles,
            MinimumLevel = fileLoggerSettings.MinimumLevel,
            UseUtcTimestamp = fileLoggerSettings.UseUtcTimestamp,
            DateFormat = fileLoggerSettings.DateFormat,
            IncludeScopes = fileLoggerSettings.IncludeScopes,
            WithTraceId = fileLoggerSettings.WithTraceId,
            WithStackFrame = fileLoggerSettings.WithStackFrame
        };

        // 处理自定义配置
        configure?.Invoke(fileLoggerOptions);

        // 创建文件日志记录器提供程序
        return new FileLoggerProvider(fileLoggerSettings.FileName ?? "application.log", fileLoggerOptions);
    }

    /// <summary>
    /// 从配置文件中加载配置并创建数据库日志记录器提供程序
    /// </summary>
    /// <param name="configuraionKey">获取配置文件对应的 Key</param>
    /// <param name="configure">数据库日志记录器配置选项委托</param>
    /// <returns><see cref="DatabaseLoggerProvider"/></returns>
    internal static DatabaseLoggerProvider CreateFromConfiguration(Func<string> configuraionKey, Action<DatabaseLoggerOptions> configure = default)
    {
        // 检查 Key 是否存在
        var key = configuraionKey?.Invoke();
        if (string.IsNullOrWhiteSpace(key)) return new DatabaseLoggerProvider(new DatabaseLoggerOptions());

        // 加载配置文件中指定节点
        var databaseLoggerSettings = App.GetConfig<DatabaseLoggerSettings>(key)
            ?? new DatabaseLoggerSettings();

        // 创建数据库日志记录器配置选项
        var databaseLoggerOptions = new DatabaseLoggerOptions
        {
            MinimumLevel = databaseLoggerSettings.MinimumLevel,
            UseUtcTimestamp = databaseLoggerSettings.UseUtcTimestamp,
            DateFormat = databaseLoggerSettings.DateFormat,
            IncludeScopes = databaseLoggerSettings.IncludeScopes,
            IgnoreReferenceLoop = databaseLoggerSettings.IgnoreReferenceLoop,
            WithTraceId = databaseLoggerSettings.WithTraceId,
            WithStackFrame = databaseLoggerSettings.WithStackFrame
        };

        // 处理自定义配置
        configure?.Invoke(databaseLoggerOptions);

        // 创建数据库日志记录器提供程序
        return new DatabaseLoggerProvider(databaseLoggerOptions);
    }

    /// <summary>
    /// 输出标准日志消息
    /// </summary>
    /// <param name="logMsg"></param>
    /// <param name="dateFormat"></param>
    /// <param name="disableColors"></param>
    /// <param name="isConsole"></param>
    /// <param name="withTraceId"></param>
    /// <param name="withStackFrame"></param>
    /// <returns></returns>
    internal static string OutputStandardMessage(LogMessage logMsg
        , string dateFormat = "yyyy-MM-dd HH:mm:ss.fffffff zzz dddd"
        , bool isConsole = false
        , bool disableColors = true
        , bool withTraceId = false
        , bool withStackFrame = false)
    {
        // 空检查
        if (logMsg.Message is null) return null;

        // 创建默认日志格式化模板
        var formatString = new StringBuilder();

        // 获取日志级别对应控制台的颜色
        var disableConsoleColor = !isConsole || disableColors;
        var logLevelColors = GetLogLevelConsoleColors(logMsg.LogLevel, disableConsoleColor);

        _ = AppendWithColor(formatString, GetLogLevelString(logMsg.LogLevel), logLevelColors);
        formatString.Append(": ");
        formatString.Append(logMsg.LogDateTime.ToString(dateFormat));
        formatString.Append(' ');
        formatString.Append(logMsg.UseUtcTimestamp ? "U" : "L");
        formatString.Append(' ');
        _ = AppendWithColor(formatString, logMsg.LogName, disableConsoleColor
            ? new ConsoleColors(null, null)
            : new ConsoleColors(ConsoleColor.Cyan, ConsoleColor.DarkCyan));
        formatString.Append('[');
        formatString.Append(logMsg.EventId.Id);
        formatString.Append(']');
        formatString.Append(' ');
        formatString.Append($"#{logMsg.ThreadId}");
        if (withTraceId && !string.IsNullOrWhiteSpace(logMsg.TraceId))
        {
            formatString.Append(' ');
            _ = AppendWithColor(formatString, $"'{logMsg.TraceId}'", disableConsoleColor
                ? new ConsoleColors(null, null)
                : new ConsoleColors(ConsoleColor.Gray, ConsoleColor.Black));
        }
        formatString.AppendLine();

        // 输出日志输出所在方法，类型，程序集
        if (withStackFrame)
        {
            var stackTraces = EnhancedStackTrace.Current();
            var pos = isConsole ? 6 : 5;
            if (stackTraces.FrameCount > pos)
            {
                var targetMethod = stackTraces.Where((u, i) => i == pos).FirstOrDefault()?.MethodInfo;
                var targetAssembly = targetMethod?.DeclaringType?.Assembly;

                if (targetAssembly != null)
                {
                    formatString.Append(PadLeftAlign($"[{targetAssembly.GetName().Name}.dll] {targetMethod}"));
                    formatString.AppendLine();
                }
            }
        }

        // 对日志内容进行缩进对齐处理
        formatString.Append(PadLeftAlign(logMsg.Message));

        // 如果包含异常信息，则创建新一行写入
        if (logMsg.Exception != null)
        {
            var EXCEPTION_SEPARATOR_WITHCOLOR = AppendWithColor(default, EXCEPTION_SEPARATOR, logLevelColors).ToString();
            var exceptionMessage = $"{Environment.NewLine}{EXCEPTION_SEPARATOR_WITHCOLOR}{Environment.NewLine}{logMsg.Exception}{Environment.NewLine}{EXCEPTION_SEPARATOR_WITHCOLOR}";

            formatString.Append(PadLeftAlign(exceptionMessage));
        }

        // 返回日志消息模板
        return formatString.ToString();
    }

    /// <summary>
    /// 将日志内容进行对齐
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    private static string PadLeftAlign(string message)
    {
        var newMessage = string.Join(Environment.NewLine, message.Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.None)
                    .Select(line => string.Empty.PadLeft(6, ' ') + line));

        return newMessage;
    }

    /// <summary>
    /// 获取日志级别短名称
    /// </summary>
    /// <param name="logLevel">日志级别</param>
    /// <returns></returns>
    internal static string GetLogLevelString(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => "trce",
            LogLevel.Debug => "dbug",
            LogLevel.Information => "info",
            LogLevel.Warning => "warn",
            LogLevel.Error => "fail",
            LogLevel.Critical => "crit",
            _ => throw new ArgumentOutOfRangeException(nameof(logLevel)),
        };
    }

    /// <summary>
    /// 获取日志上下文
    /// </summary>
    /// <param name="scopeProvider"></param>
    /// <param name="includeScopes"></param>
    /// <returns></returns>
    internal static LogContext SetLogContext(IExternalScopeProvider scopeProvider, bool includeScopes)
    {
        // 空检查
        if (!includeScopes || scopeProvider == null) return null;

        var contexts = new List<LogContext>();
        var scopes = new List<object>();

        // 解析日志上下文数据
        scopeProvider.ForEachScope<object>((scope, ctx) =>
        {
            if (scope != null)
            {
                if (scope is LogContext context) contexts.Add(context);
                else scopes.Add(scope);
            }
        }, null);

        if (contexts.Count == 0 && scopes.Count == 0) return null;

        // 构建日志上下文
        var logConext = new LogContext
        {
            Properties = contexts.SelectMany(p => p.Properties)
                .GroupBy(p => p.Key)
                .ToDictionary(u => u.Key, u => u.FirstOrDefault().Value),
            Scopes = scopes
        };

        return logConext;
    }

    /// <summary>
    /// 拓展 StringBuilder 增加带颜色写入
    /// </summary>
    /// <param name="message"></param>
    /// <param name="colors"></param>
    /// <param name="formatString"></param>
    /// <returns></returns>
    private static StringBuilder AppendWithColor(StringBuilder formatString, string message, ConsoleColors colors)
    {
        formatString ??= new();

        // 输出控制台前景色和背景色
        if (colors.Background.HasValue) formatString.Append(GetBackgroundColorEscapeCode(colors.Background.Value));
        if (colors.Foreground.HasValue) formatString.Append(GetForegroundColorEscapeCode(colors.Foreground.Value));

        formatString.Append(message);

        // 输出控制台前景色和背景色
        if (colors.Background.HasValue) formatString.Append("\u001b[39m\u001b[22m");
        if (colors.Foreground.HasValue) formatString.Append("\u001b[49m");

        return formatString;
    }

    /// <summary>
    /// 输出控制台字体颜色 UniCode 码
    /// </summary>
    /// <param name="color"></param>
    /// <returns></returns>
    private static string GetForegroundColorEscapeCode(ConsoleColor color)
    {
        return color switch
        {
            ConsoleColor.Black => "\u001b[30m",
            ConsoleColor.DarkRed => "\u001b[31m",
            ConsoleColor.DarkGreen => "\u001b[32m",
            ConsoleColor.DarkYellow => "\u001b[33m",
            ConsoleColor.DarkBlue => "\u001b[34m",
            ConsoleColor.DarkMagenta => "\u001b[35m",
            ConsoleColor.DarkCyan => "\u001b[36m",
            ConsoleColor.Gray => "\u001b[37m",
            ConsoleColor.Red => "\u001b[1m\u001b[31m",
            ConsoleColor.Green => "\u001b[1m\u001b[32m",
            ConsoleColor.Yellow => "\u001b[1m\u001b[33m",
            ConsoleColor.Blue => "\u001b[1m\u001b[34m",
            ConsoleColor.Magenta => "\u001b[1m\u001b[35m",
            ConsoleColor.Cyan => "\u001b[1m\u001b[36m",
            ConsoleColor.White => "\u001b[1m\u001b[37m",
            _ => "\u001b[39m\u001b[22m",
        };
    }

    /// <summary>
    /// 输出控制台背景颜色 UniCode 码
    /// </summary>
    /// <param name="color"></param>
    /// <returns></returns>
    private static string GetBackgroundColorEscapeCode(ConsoleColor color)
    {
        return color switch
        {
            ConsoleColor.Black => "\u001b[40m",
            ConsoleColor.Red => "\u001b[41m",
            ConsoleColor.Green => "\u001b[42m",
            ConsoleColor.Yellow => "\u001b[43m",
            ConsoleColor.Blue => "\u001b[44m",
            ConsoleColor.Magenta => "\u001b[45m",
            ConsoleColor.Cyan => "\u001b[46m",
            ConsoleColor.White => "\u001b[47m",
            _ => "\u001b[49m",
        };
    }

    /// <summary>
    /// 获取控制台日志级别对应的颜色
    /// </summary>
    /// <param name="logLevel"></param>
    /// <param name="disableColors"></param>
    /// <returns></returns>
    private static ConsoleColors GetLogLevelConsoleColors(LogLevel logLevel, bool disableColors = false)
    {
        if (disableColors)
        {
            return new ConsoleColors(null, null);
        }

        return logLevel switch
        {
            LogLevel.Critical => new ConsoleColors(ConsoleColor.White, ConsoleColor.Red),
            LogLevel.Error => new ConsoleColors(ConsoleColor.Black, ConsoleColor.Red),
            LogLevel.Warning => new ConsoleColors(ConsoleColor.Yellow, ConsoleColor.Black),
            LogLevel.Information => new ConsoleColors(ConsoleColor.DarkGreen, ConsoleColor.Black),
            LogLevel.Debug => new ConsoleColors(ConsoleColor.Gray, ConsoleColor.Black),
            LogLevel.Trace => new ConsoleColors(ConsoleColor.Gray, ConsoleColor.Black),
            _ => new ConsoleColors(null, background: null),
        };
    }
}