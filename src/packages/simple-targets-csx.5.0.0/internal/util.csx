using System.Collections.Generic;
using System.Linq;
using System.Text;
using static SimpleTargets;

public static class SimpleTargetsUtil
{
    private static string Default          (bool color) => color ? "\x1b[0m"   : "";
    private static string Green            (bool color) => color ? "\x1b[32m"  : "";
    private static string Cyan             (bool color) => color ? "\x1b[36m"  : "";
    private static string White            (bool color) => color ? "\x1b[37m"  : "";
    private static string BrightRed        (bool color) => color ? "\x1b[91m"  : "";
    private static string BrightYellow     (bool color) => color ? "\x1b[93m"  : "";
    private static string BrightMagenta    (bool color) => color ? "\x1b[95m"  : "";

    private static readonly Dictionary<MessageType, Func<bool,string>> Colors = new Dictionary<MessageType, Func<bool,string>>
    {
        { MessageType.Start,    color => White(color) },
        { MessageType.Success,  color => Green(color) },
        { MessageType.Failure,  color => BrightRed(color) },
    };

    public static string GetUsage(bool color) =>
$@"{Cyan(color)}Usage: {BrightYellow(color)}<script-runner> {Default(color)}<script-file> {White(color)}[<options>] {Default(color)}[<targets>]

{Cyan(color)}script-runner: {Default(color)}A C# script runner. E.g. {BrightYellow(color)}csi.exe{Default(color)}.

{Cyan(color)}script-file: {Default(color)}Path to a script. E.g. build.csx.

{Cyan(color)}options:{Default(color)}
 {White(color)}-D          {Default(color)}Display the targets and dependencies, then exit
 {White(color)}-T          {Default(color)}Display the targets, then exit
 {White(color)}-n          {Default(color)}Do a dry run without executing actions
 {White(color)}--no-color  {Default(color)}Disable colored output

{Cyan(color)}targets: {Default(color)}A list of targets to run. If not specified, 'Default(color)' target will be run.

{Cyan(color)}Examples:{Default(color)}
  {BrightYellow(color)}csi.exe {Default(color)}build.csx
  {BrightYellow(color)}csi.exe {Default(color)}build.csx {White(color)}-T{Default(color)}
  {BrightYellow(color)}csi.exe {Default(color)}build.csx test pack
  {BrightYellow(color)}csi.exe {Default(color)}build.csx {White(color)}-n {Default(color)}build
";

    public static string GetList(IDictionary<string, Target> targets)
    {
        var value = new StringBuilder();
        foreach (var target in targets.OrderBy(pair => pair.Key))
        {
            value.AppendLine(target.Key);
        }

        return value.ToString();
    }

    public static string GetDependencies(IDictionary<string, Target> targets, bool color)
    {
        var value = new StringBuilder();
        foreach (var target in targets.OrderBy(pair => pair.Key))
        {
            value.AppendLine(target.Key);
            foreach (var dependency in target.Value.Dependencies)
            {
                value.AppendLine($"  {White(color)}{dependency}{Default(color)}");
            }
        }

        return value.ToString();
    }

    public enum MessageType
    {
        Start,
        Success,
        Failure,
    }

    public static string Message(MessageType messageType, string text, bool dryRun, bool color) =>
        $"{GetPrefix(color)}{Colors[messageType](color)}{text}{GetSuffix(dryRun, color)}{Default(color)}";

    public static string Message(MessageType messageType, string text, string targetName, bool color) =>
        $"{GetPrefix(targetName, color)}{Colors[messageType](color)}{text}{Default(color)}";

    private static string GetPrefix(bool color) =>
        $"{Cyan(color)}simple-targets{White(color)}: ";

    private static string GetPrefix(string targetName, bool color) =>
        $"{Cyan(color)}simple-targets{White(color)}/{Cyan(color)}{targetName.Replace(": ", ":: ")}{White(color)}: ";

    private static string GetSuffix(bool dryRun, bool color) => dryRun ? $"{BrightMagenta(color)} (dry run)" : "";
}
