using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Threading;
using Voyage.EarthquakeWarning.UI;

namespace Voyage.EarthquakeWarning.Services;

public sealed record ErrorReport(DateTime OccurredAt, string Type, string Message, string Source, string Detail, string Location, bool Fatal, string LogFile);

public static class ErrorReporter
{
    public const string IssuesUrl = "https://github.com/FengLinOvO/VoyageEarthquakeWarningForClassisland/issues";

    private const int FallbackContextLines = 6;
    private const int MaxContextLines = 80;

    private static readonly Dictionary<string, string> TypeNames = new()
    {
        ["OutOfMemoryException"] = "内存分配失败",
        ["InsufficientMemoryException"] = "内存分配失败",
        ["StackOverflowException"] = "堆栈溢出",
        ["AccessViolationException"] = "内存访问违规",
        ["SEHException"] = "系统级底层异常",
        ["InvalidOperationException"] = "调用状态无效",
        ["NullReferenceException"] = "空引用访问",
        ["ArgumentNullException"] = "必需参数为空",
        ["ArgumentOutOfRangeException"] = "参数超出允许范围",
        ["ArgumentException"] = "参数不合法",
        ["IndexOutOfRangeException"] = "数组索引越界",
        ["OverflowException"] = "数值溢出",
        ["DivideByZeroException"] = "除零错误",
        ["ArithmeticException"] = "算术运算错误",
        ["FormatException"] = "数据格式不正确",
        ["InvalidCastException"] = "类型转换失败",
        ["FileNotFoundException"] = "文件未找到",
        ["DirectoryNotFoundException"] = "目录未找到",
        ["DriveNotFoundException"] = "磁盘驱动器未找到",
        ["PathTooLongException"] = "文件路径过长",
        ["IOException"] = "文件读写失败",
        ["EndOfStreamException"] = "数据流意外结束",
        ["UnauthorizedAccessException"] = "文件或资源访问被拒绝",
        ["InvalidDataException"] = "数据内容无效",
        ["TimeoutException"] = "操作超时未完成",
        ["TaskCanceledException"] = "任务已被取消",
        ["OperationCanceledException"] = "操作已被取消",
        ["JsonException"] = "JSON 数据解析失败",
        ["XmlException"] = "XML 数据解析失败",
        ["HttpRequestException"] = "网络请求发送失败",
        ["SocketException"] = "网络连接失败",
        ["WebSocketException"] = "网络连接中断",
        ["COMException"] = "系统组件调用失败",
        ["ExternalException"] = "外部组件调用失败",
        ["Win32Exception"] = "系统接口调用失败",
        ["MmException"] = "音频设备播放失败",
        ["DllNotFoundException"] = "依赖库文件未找到",
        ["EntryPointNotFoundException"] = "依赖库入口未找到",
        ["BadImageFormatException"] = "程序集或依赖格式不正确",
        ["TypeLoadException"] = "类型加载失败",
        ["FileLoadException"] = "文件加载失败",
        ["TypeInitializationException"] = "类型初始化失败",
        ["NotSupportedException"] = "当前不支持此操作",
        ["PlatformNotSupportedException"] = "当前系统不支持此操作",
        ["NotImplementedException"] = "功能尚未实现",
        ["ObjectDisposedException"] = "对象已被释放",
        ["KeyNotFoundException"] = "未找到指定的键",
        ["MissingMethodException"] = "未找到指定的方法",
        ["MissingFieldException"] = "未找到指定的字段",
        ["TargetInvocationException"] = "反射调用的方法执行失败",
        ["SecurityException"] = "安全权限不足",
        ["SerializationException"] = "序列化失败",
        ["CryptographicException"] = "加密或解密失败",
        ["CryptographicUnexpectedOperationException"] = "加密操作出现意外结果",
        ["RegexMatchTimeoutException"] = "正则匹配超时",
        ["UriFormatException"] = "网址格式不正确",
        ["DecoderFallbackException"] = "字符解码失败",
        ["EncoderFallbackException"] = "字符编码失败",
        ["TaskSchedulerException"] = "任务调度失败",
        ["XamlLoadException"] = "界面布局文件加载失败",
        ["XamlParseException"] = "界面布局文件解析失败",
        ["ThreadAbortException"] = "线程被强制中止",
        ["ThreadInterruptedException"] = "线程等待被中断",
        ["ThreadStateException"] = "线程状态无效",
        ["ThreadStartException"] = "线程启动失败",
        ["MarshalDirectiveException"] = "托管与非托管数据转换失败",
        ["RuntimeWrappedException"] = "非托管代码引发的异常",
        ["AmbiguousMatchException"] = "成员匹配不唯一",
        ["MethodAccessException"] = "方法访问被拒绝",
        ["FieldAccessException"] = "字段访问被拒绝",
        ["MemberAccessException"] = "成员访问被拒绝",
        ["MissingMemberException"] = "未找到指定的成员",
        ["InvalidProgramException"] = "程序包含无效指令",
        ["VerificationException"] = "代码安全验证失败",
        ["CannotUnloadAppDomainException"] = "应用程序域卸载失败",
        ["ContextMarshalException"] = "上下文数据转换失败",
        ["InsufficientExecutionStackException"] = "可用调用栈空间不足",
        ["HostProtectionException"] = "宿主保护限制阻止了操作",
        ["InvalidTimeZoneException"] = "时区数据无效",
        ["ApplicationException"] = "应用程序运行错误",
        ["SystemException"] = "系统运行错误",
        ["DataException"] = "数据访问错误",
        ["ConstraintException"] = "数据约束冲突",
        ["DuplicateNameException"] = "名称重复",
        ["EvaluateException"] = "表达式求值失败",
        ["SyntaxErrorException"] = "数据语法错误",
        ["ReadOnlyException"] = "目标数据为只读",
        ["NoNullAllowedException"] = "该字段不允许为空",
        ["VersionNotFoundException"] = "未找到指定的数据版本",
        ["DeletedRowInaccessibleException"] = "访问了已删除的数据行",
        ["RowNotInTableException"] = "数据行不属于当前表",
        ["InRowChangingEventException"] = "在行变更事件中执行了无效操作",
        ["NotFiniteNumberException"] = "数值不是有限数",
        ["InvalidOleVariantTypeException"] = "OLE 变体类型无效",
        ["SafeArrayTypeMismatchException"] = "数组元素类型不匹配",
        ["SafeArrayRankMismatchException"] = "数组维数不匹配",
        ["RemotingException"] = "远程调用失败",
        ["HttpIOException"] = "网络数据读取失败",
        ["HttpProtocolException"] = "网络协议错误",
        ["XmlSchemaException"] = "XML 结构校验失败",
        ["ConfigurationErrorsException"] = "配置文件读取失败",
        ["ConfigurationException"] = "配置内容无效",
        ["SettingsPropertyNotFoundException"] = "未找到指定的配置项",
        ["AbandonedMutexException"] = "互斥体已被放弃",
        ["SemaphoreFullException"] = "信号量已达上限",
        ["WaitHandleCannotBeOpenedException"] = "无法打开指定的等待句柄",
        ["Exception"] = "未知异常"
    };

    private static readonly string[] FatalNames =
    [
        "OutOfMemoryException",
        "InsufficientMemoryException",
        "StackOverflowException",
        "AccessViolationException",
        "SEHException"
    ];

    private static readonly Regex FrameRegex = new(@"in\s+(.+?):line\s+(\d+)", RegexOptions.Compiled);
    private static readonly object Gate = new();
    private static readonly HashSet<string> Suppressed = [];
    private static string[]? _sourceNames;
    private static DateTime _lastShownAt = DateTime.MinValue;
    private static string _lastSignature = "";

    public static string FatalText =>
        "VoyageEarthquakeWarnning（地震预警）插件在运行中遇到致命错误！";

    public static string FatalEmphasisText =>
        "该错误已导致程序进程崩溃";

    public static string NonFatalText =>
        "VoyageEarthquakeWarnning（地震预警）插件在运行中遇到非致命错误！";

    public static string NonFatalEmphasisText =>
        "该错误已被安全策略忽略，将不影响程序其余部分运行";

    public static string FeedbackText(string logFile) =>
        $"错误日志已保存在\"{logFile}\"，请前往插件Github仓库的Issues页携带错误日志内容提交issues。";

    public static string LogFolder =>
        Plugin.Current is null ? "classisland的插件配置目录下的log" : Path.Combine(Plugin.Current.ConfigDirectory, "log");

    public static void Report(Exception ex, string source, bool fromPlugin = false)
    {
        if (!ShouldReport(ex, fromPlugin)) return;

        Handle(Create(ex, source, IsSystemLevel(ex)));
    }

    public static void ReportCrash(Exception ex, string source, bool fromPlugin = false)
    {
        if (!ShouldReport(ex, fromPlugin)) return;

        Handle(Create(ex, source, true));
    }

    public static void Suppress(ErrorReport report)
    {
        lock (Gate) Suppressed.Add(Key(report));
    }

    public static void MarkRecovered(string source)
    {
        lock (Gate) Suppressed.RemoveWhere(key => key.StartsWith($"{source}|", StringComparison.Ordinal));
    }

    private static string Key(ErrorReport report) => $"{report.Source}|{report.Type}";

    private static bool ShouldReport(Exception ex, bool fromPlugin) => fromPlugin || IsSystemLevel(ex) || IsOurs(ex);

    private static bool IsOurs(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            var frames = new StackTrace(current, false).GetFrames();

            if (frames is null) continue;

            foreach (var frame in frames)
                if (frame.GetMethod()?.DeclaringType?.Assembly == typeof(ErrorReporter).Assembly) return true;
        }

        return false;
    }

    private static ErrorReport Create(Exception ex, string source, bool fatal)
    {
        var occurred = DateTime.Now;
        var type = TypeName(ex);

        return new ErrorReport(occurred, type, ex.Message, source, ex.ToString(), SourceLocation(ex.StackTrace), fatal, LogFilePath(occurred, type));
    }

    private static string LogFilePath(DateTime occurred, string type) =>
        Path.Combine(LogFolder, $"Error_{occurred:yyyyMMdd_HHmmss}_{Sanitize(type)}.log");

    private static void Handle(ErrorReport report)
    {
        WriteLogFile(report, Format(report));

        var signature = $"{report.Type}|{report.Message}|{report.Source}";

        lock (Gate)
        {
            if (!report.Fatal && Suppressed.Contains(Key(report))) return;

            if (signature == _lastSignature && (DateTime.UtcNow - _lastShownAt).TotalSeconds < 5) return;

            _lastSignature = signature;
            _lastShownAt = DateTime.UtcNow;
        }

        PlayAlertSound();

        Dispatcher.UIThread.Post(() =>
        {
            try { ErrorWindow.ShowReport(report); } catch { }
        });
    }

    [DllImport("user32.dll")]
    private static extern bool MessageBeep(uint uType);

    private static void PlayAlertSound() { try { MessageBeep(0x00000010); } catch { } }

    private static bool IsSystemLevel(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            for (var type = current.GetType(); type is not null; type = type.BaseType)
            {
                if (Array.IndexOf(FatalNames, type.Name) >= 0) return true;
            }
        }

        return false;
    }

    private static string TypeName(Exception ex)
    {
        if (ex is AggregateException aggregate)
        {
            var names = aggregate.Flatten().InnerExceptions.Select(TypeName).Distinct().ToList();

            return names.Count > 0 ? string.Join("；", names) : "任务执行失败";
        }

        for (var inner = ex.InnerException; inner is not null; inner = inner.InnerException)
        {
            if (inner is TimeoutException) return "操作超时未完成";
        }

        if (ex is TaskCanceledException or OperationCanceledException && ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase)) return "操作超时未完成";

        for (var type = ex.GetType(); type is not null; type = type.BaseType)
        {
            if (TypeNames.TryGetValue(type.Name, out var name)) return Refine(type.Name, name, ex.Message);
        }

        return ex.GetType().Name;
    }

    private static string Refine(string key, string name, string message)
    {
        if (IsAudio(message))
        {
            return key switch
            {
                "FileNotFoundException" => "音频文件未找到",
                "DirectoryNotFoundException" => "音频目录未找到",
                "IOException" => "音频文件读取失败",
                "InvalidDataException" => "音频文件已损坏",
                "UnauthorizedAccessException" => "音频文件访问被拒绝",
                _ => name
            };
        }

        if (IsImage(message) && key is "FileNotFoundException" or "IOException") return "图标资源读取失败";

        return name;
    }

    private static bool IsAudio(string message) =>
        message.Contains("audio", StringComparison.OrdinalIgnoreCase) ||
        message.Contains(".mp3", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Assets\\Audio", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Assets/Audio", StringComparison.OrdinalIgnoreCase);

    private static bool IsImage(string message) =>
        message.Contains(".png", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Assets\\Images", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Assets/Images", StringComparison.OrdinalIgnoreCase);

    private static void WriteLogFile(ErrorReport report, string text)
    {
        try
        {
            Directory.CreateDirectory(LogFolder);
            File.WriteAllText(report.LogFile, text, Encoding.UTF8);
        }
        catch { }
    }

    private static string Sanitize(string value)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');

        return value;
    }

    private static string Format(ErrorReport report) => string.Join(Environment.NewLine,
    [
        "VoyageEarthquakeWarning错误报告",
        $"报告时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}",
        $"发生时间：{report.OccurredAt:yyyy-MM-dd HH:mm:ss}",
        $"错误级别：{(report.Fatal ? "致命（崩溃）" : "非致命")}",
        $"版本：{PluginVersion()}",
        $"ClassIsland：{ClassIslandVersion()}",
        $"系统版本：{SystemVersion()}",
        $"异常类型：{report.Type}",
        $"错误信息：{report.Message}",
        $"发生于：{(report.Location.Length > 0 ? report.Location : report.Source)}",
        report.Detail,
        new string('-', 32)
    ]);

    private static string SourceLocation(string? stackTrace)
    {
        if (string.IsNullOrWhiteSpace(stackTrace)) return "";

        foreach (Match match in FrameRegex.Matches(stackTrace))
        {
            var path = match.Groups[1].Value.Trim();

            if (!int.TryParse(match.Groups[2].Value, out var line)) continue;

            var snippet = ReadSourceSnippet(path, line);

            if (snippet.Length == 0) continue;

            return $"{path} 第 {line} 行{Environment.NewLine}{Environment.NewLine}{snippet}";
        }

        return "";
    }

    private static string ReadSourceSnippet(string path, int lineNumber)
    {
        var fileName = Path.GetFileName(path);

        using var stream = OpenSource(fileName);

        if (stream is null) return "";

        using var reader = new StreamReader(stream, Encoding.UTF8);

        var lines = reader.ReadToEnd().Replace("\r\n", "\n").Split('\n');

        if (lines.Length == 0) return "";

        var index = Math.Clamp(lineNumber - 1, 0, lines.Length - 1);
        var (start, end) = EnclosingBlock(lines, index);

        var builder = new StringBuilder();

        for (var i = start; i <= end; i++) builder.AppendLine($"{i + 1}| {lines[i].TrimEnd()}");

        return builder.ToString().TrimEnd();
    }

    private static Stream? OpenSource(string fileName)
    {
        try
        {
            var assembly = typeof(ErrorReporter).Assembly;

            _sourceNames ??= assembly.GetManifestResourceNames();

            var name = _sourceNames.FirstOrDefault(n => n.EndsWith($".{fileName}", StringComparison.OrdinalIgnoreCase));

            return name is null ? null : assembly.GetManifestResourceStream(name);
        }
        catch
        {
            return null;
        }
    }

    private static (int Start, int End) EnclosingBlock(string[] lines, int index)
    {
        var start = -1;
        var depth = 0;

        for (var i = index; i >= 0 && start < 0; i--)
        {
            var text = lines[i];

            for (var c = text.Length - 1; c >= 0; c--)
            {
                if (text[c] == '}') depth++;
                else if (text[c] == '{')
                {
                    if (depth == 0)
                    {
                        start = i;
                        break;
                    }

                    depth--;
                }
            }
        }

        if (start < 0) return Fallback(lines, index);

        depth = 0;
        var end = -1;

        for (var i = start; i < lines.Length && end < 0; i++)
        {
            var text = lines[i];

            for (var c = 0; c < text.Length; c++)
            {
                if (text[c] == '{') depth++;
                else if (text[c] == '}')
                {
                    depth--;

                    if (depth == 0)
                    {
                        end = i;
                        break;
                    }
                }
            }
        }

        if (end < 0) return Fallback(lines, index);
        if (end - start > MaxContextLines) return Fallback(lines, index);

        return (Math.Max(0, start - 3), Math.Min(lines.Length - 1, end + 1));
    }

    private static (int Start, int End) Fallback(string[] lines, int index) =>
        (Math.Max(0, index - FallbackContextLines), Math.Min(lines.Length - 1, index + FallbackContextLines));

    private static string PluginVersion()
    {
        try
        {
            var directory = Path.GetDirectoryName(typeof(ErrorReporter).Assembly.Location) ?? AppContext.BaseDirectory;
            var path = Path.Combine(directory, "manifest.yml");

            if (File.Exists(path))
            {
                foreach (var line in File.ReadAllLines(path))
                {
                    var value = line.Trim();

                    if (value.StartsWith("version:", StringComparison.OrdinalIgnoreCase))
                        return value["version:".Length..].Trim();
                }
            }
        }
        catch { }

        return "未知";
    }

    private static string ClassIslandVersion()
    {
        try
        {
            var version = Assembly.Load("ClassIsland")?.GetName().Version;

            if (version is not null) return $"{version.Major}.{version.Minor}";
        }
        catch { }

        return "未知";
    }

    private static string SystemVersion()
    {
        try
        {
            var version = Environment.OSVersion.Version;

            return $"Windows {version.Major}.{version.Minor}.{version.Build}";
        }
        catch { }

        return "未知";
    }
}
