#load "internal/runner.csx"

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

public static class SimpleTargets
{
    public static string[] DependsOn(params string[] dependencies) => dependencies;

    public static void Run(IList<string> args, IDictionary<string, SimpleTargets.Target> targets) =>
        SimpleTargetsRunner.Run(args, targets, Console.Out);

    public class Target
    {
        public Target(IEnumerable<string> dependencies)
            : this(dependencies, null)
        {
        }

        public Target(Action action)
            : this(null, action)
        {
        }

        public Target(IEnumerable<string> dependencies, Action action)
        {
            this.Dependencies = new ReadOnlyCollection<string>(dependencies?.ToList() ?? new List<string>());
            this.Action = action;
        }

        public IReadOnlyList<string> Dependencies { get; }

        public Action Action { get; }
    }

    public class TargetDictionary : Dictionary<string, Target>
    {
        public void Add(string name, IEnumerable<string> dependencies, Action action) =>
            this.Add(name, new Target(dependencies, action));

        public void Add(string name, IEnumerable<string> dependencies) =>
            this.Add(name, new Target(dependencies, null));

        public void Add(string name, Action action) =>
            this.Add(name, new Target(null, action));
    }
}
