using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

public static bool Inspect(string inputFile)
{
    var colors = new Dictionary<string, ConsoleColor>
    {
        { "SUGGESTION", ConsoleColor.Green },
        { "WARNING", ConsoleColor.Yellow },
        { "ERROR", ConsoleColor.Red },
    };

    var doc = new XmlDocument();
    doc.Load(inputFile);
    
    var issueTypeSeverities = doc.DocumentElement.SelectNodes("/Report/IssueTypes/IssueType").Cast<XmlElement>()
        .ToDictionary(i => i.GetAttribute("Id"), i => i.GetAttribute("Severity"));
    
    var hasError = false;
    foreach (var issue in doc.DocumentElement.SelectNodes("/Report/Issues/Project/Issue").Cast<XmlElement>())
    {
        var file = issue.GetAttribute("File");
        var line = issue.GetAttribute("Line");
        var severity = issueTypeSeverities[issue.GetAttribute("TypeId")];
        var message = issue.GetAttribute("Message");
        
        hasError = hasError || severity == "ERROR";

        var color = ConsoleColor.Cyan;
        colors.TryGetValue(severity, out color);
        var originalColor = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = color;
            Console.WriteLine($"{file}({line}): {severity}: {message}");
        }
        finally
        {
            Console.ForegroundColor = originalColor;
        }
    }

    return !hasError;
}
