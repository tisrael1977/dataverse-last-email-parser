using System;
using System.IO;
using System.Text;

/// <summary>
/// Generic demo runner: point it at any Dataverse email HTML file and it prints the
/// output of all four EmailThreadParser methods.
///
///   dotnet run -- sample ..\..\samples\outlook-thread.html
///   dotnet run -- sample ..\..\samples\gmail-thread.html
///
/// (Run without arguments to execute the regression corpus in EmailTestCorpus*.cs instead.)
/// </summary>
internal static class SampleRunner
{
    public static void Run(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        if (args.Length < 2)
        {
            Console.WriteLine("Usage: dotnet run -- sample <path-to-email.html>");
            return;
        }

        var path = args[1];
        if (!File.Exists(path))
        {
            Console.WriteLine($"File not found: {path}");
            return;
        }

        var html = File.ReadAllText(path);

        Console.WriteLine("===== ExtractLatestMessageText =====");
        Console.WriteLine(EmailThreadParser.ExtractLatestMessageText(html));

        Console.WriteLine("\n===== ExtractLatestMessageTextWithPlaceholders =====");
        Console.WriteLine(EmailThreadParser.ExtractLatestMessageTextWithPlaceholders(html));

        Console.WriteLine("\n===== ExtractLatestMessageAttachments =====");
        var atts = EmailThreadParser.ExtractLatestMessageAttachments(html);
        Console.WriteLine($"{atts.Count} embedded attachment(s) in the latest message:");
        foreach (var a in atts)
            Console.WriteLine("  " + a);

        Console.WriteLine("\n===== ExtractLatestMessageHtml =====");
        Console.WriteLine(EmailThreadParser.ExtractLatestMessageHtml(html));
    }
}
