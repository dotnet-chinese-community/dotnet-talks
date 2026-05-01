#!/usr/bin/env dotnet

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

var options = GeneratorOptions.Parse(args);
var repositoryRoot = Directory.GetCurrentDirectory();
var topicsPath = Path.GetFullPath(options.TopicsPath, repositoryRoot);
var templatePath = Path.GetFullPath(options.TemplatePath, repositoryRoot);

if (!File.Exists(topicsPath))
{
    Console.Error.WriteLine($"Topics file not found: {topicsPath}");
    return 1;
}

if (!File.Exists(templatePath))
{
    Console.Error.WriteLine($"Template file not found: {templatePath}");
    return 1;
}

var jsonOptions = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true
};

var topicsJson = await File.ReadAllTextAsync(topicsPath, Encoding.UTF8);
var topics = JsonSerializer.Deserialize(topicsJson, new TopicJsonContext(jsonOptions).ListTopic) ?? [];
var template = await File.ReadAllTextAsync(templatePath, Encoding.UTF8);

var selectedTopics = topics
    .Where(topic => options.Date is null || topic.Date == options.Date)
    .OrderBy(topic => topic.Date)
    .ToArray();

if (selectedTopics.Length == 0)
{
    Console.Error.WriteLine(options.Date is null
        ? "No topics found."
        : $"No topic found for date: {options.Date}");
    return 1;
}

var generatedCount = 0;
var skippedCount = 0;
var yearReadmeGeneratedCount = 0;
var yearReadmeUnchangedCount = 0;
var selectedYears = new SortedSet<int>();

foreach (var topic in selectedTopics)
{
    if (!DateOnly.TryParseExact(topic.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
    {
        Console.Error.WriteLine($"Invalid topic date '{topic.Date}' for topic '{topic.TopicTitle}'.");
        return 1;
    }

    selectedYears.Add(date.Year);

    var topicDirectory = Path.Combine(repositoryRoot, date.Year.ToString(CultureInfo.InvariantCulture), topic.Date);
    if (!Directory.Exists(topicDirectory) && !options.DryRun)
    {
        Directory.CreateDirectory(topicDirectory);
    }
    var readmePath = Path.Combine(topicDirectory, "README.md");
    var readmeContent = RenderReadme(template, topic, topicDirectory, repositoryRoot);

    if (File.Exists(readmePath) && !options.Force)
    {
        Console.WriteLine($"Skip existing: {Path.GetRelativePath(repositoryRoot, readmePath)}");
        skippedCount++;
        continue;
    }

    Console.WriteLine($"{(options.DryRun ? "Would write" : "Write")}: {Path.GetRelativePath(repositoryRoot, readmePath)}");
    generatedCount++;

    if (!options.DryRun)
    {
        Directory.CreateDirectory(topicDirectory);
        await File.WriteAllTextAsync(readmePath, readmeContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}

foreach (var year in selectedYears)
{
    var yearDirectory = Path.Combine(repositoryRoot, year.ToString(CultureInfo.InvariantCulture));
    var yearReadmePath = Path.Combine(yearDirectory, "README.md");
    var yearReadmeContent = RenderYearReadme(topics, year);

    if (File.Exists(yearReadmePath))
    {
        var existingContent = await File.ReadAllTextAsync(yearReadmePath, Encoding.UTF8);
        if (string.Equals(existingContent.ReplaceLineEndings("\n"), yearReadmeContent.ReplaceLineEndings("\n"), StringComparison.Ordinal))
        {
            Console.WriteLine($"Unchanged: {Path.GetRelativePath(repositoryRoot, yearReadmePath)}");
            yearReadmeUnchangedCount++;
            continue;
        }
    }

    Console.WriteLine($"{(options.DryRun ? "Would write" : "Write")}: {Path.GetRelativePath(repositoryRoot, yearReadmePath)}");
    yearReadmeGeneratedCount++;

    if (!options.DryRun)
    {
        Directory.CreateDirectory(yearDirectory);
        await File.WriteAllTextAsync(yearReadmePath, yearReadmeContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}

Console.WriteLine($"Done. Topic READMEs generated: {generatedCount}, skipped: {skippedCount}. Year READMEs generated: {yearReadmeGeneratedCount}, unchanged: {yearReadmeUnchangedCount}.");
return 0;

static string RenderReadme(string template, Topic topic, string topicDirectory, string repositoryRoot)
{
    var values = new Dictionary<string, string>
    {
        ["Date"] = topic.Date,
        ["Topic"] = topic.TopicTitle,
        ["Description"] = NormalizeMarkdownParagraph(topic.Description),
        ["SpeakerName"] = topic.SpeakerName,
        ["SpeakerTitle"] = topic.SpeakerTitle,
        ["SpeakerDescription"] = NormalizeMarkdownParagraph(topic.SpeakerDescription),
        ["SpeakerAvatar"] = RenderSpeakerAvatar(topic, topicDirectory, repositoryRoot),
        ["Tags"] = RenderTags(topic),
        ["Links"] = RenderLinks(topic)
    };

    foreach (var (key, value) in values)
    {
        template = template.Replace("{{" + key + "}}", value.Trim(), StringComparison.Ordinal);
    }

    return CollapseExcessBlankLines(template.TrimEnd()) + Environment.NewLine;
}

static string RenderSpeakerAvatar(Topic topic, string topicDirectory, string repositoryRoot)
{
    if (string.IsNullOrWhiteSpace(topic.SpeakerAvatar))
    {
        return string.Empty;
    }

    var avatarPath = topic.SpeakerAvatar.Replace('\\', '/');
    var absoluteAvatarPath = Path.GetFullPath(avatarPath, repositoryRoot);
    var relativeAvatarPath = Path.GetRelativePath(topicDirectory, absoluteAvatarPath).Replace('\\', '/');
    var avatarAlt = Path.GetFileNameWithoutExtension(avatarPath);

    return $"![{avatarAlt}]({relativeAvatarPath})";
}

static string RenderTags(Topic topic)
{
    if (topic.Tags is null || topic.Tags.Length == 0)
    {
        return string.Empty;
    }

    return "## 标签" + Environment.NewLine + $"{Environment.NewLine}- " + string.Join($"{Environment.NewLine}- ", topic.Tags.Select(tag => $"{tag}"));
}

static string RenderLinks(Topic topic)
{
    var links = new List<TopicLink>();

    if (!string.IsNullOrWhiteSpace(topic.IntroUrl))
    {
        links.Add(new TopicLink("活动介绍", topic.IntroUrl));
    }
    if (!string.IsNullOrWhiteSpace(topic.RecordingUrl))
    {
        links.Add(new TopicLink("视频回放", topic.RecordingUrl));
    }
    if (!string.IsNullOrWhiteSpace(topic.SpeakerGithub))
    {
        links.Add(new TopicLink("讲师 Github", topic.SpeakerGithub));
    }
    if (!string.IsNullOrWhiteSpace(topic.SlideFileName))
    {
        links.Add(new TopicLink("PPT", $"./{topic.SlideFileName}"));
    }

    if (topic.Links is { Length: > 0 })
    {
        links.AddRange(topic.Links);
    }

    var deduplicatedLinks = links
        .Where(link => !string.IsNullOrWhiteSpace(link.Title) && !string.IsNullOrWhiteSpace(link.Url))
        .DistinctBy(link => link.Url)
        .Select(link => $"- [{link.Title}]({link.Url})");

    return string.Join(Environment.NewLine, deduplicatedLinks);
}

static string RenderYearReadme(IEnumerable<Topic> topics, int year)
{
    var yearPrefix = year.ToString(CultureInfo.InvariantCulture) + "-";
    var entries = topics
        .Where(topic => topic.Date.StartsWith(yearPrefix, StringComparison.Ordinal))
        .Select(topic =>
        {
            if (!DateOnly.TryParseExact(topic.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                throw new InvalidOperationException($"Invalid topic date '{topic.Date}' for topic '{topic.TopicTitle}'.");
            }

            return (Topic: topic, Date: date);
        })
        .Where(entry => entry.Date.Year == year)
        .OrderBy(entry => entry.Date)
        .Select(entry => $"- [{entry.Topic.Date}](./{entry.Topic.Date}/)");

    var builder = new StringBuilder();
    builder.AppendLine($"# {year}");
    builder.AppendLine();

    foreach (var entry in entries)
    {
        builder.AppendLine(entry);
    }

    return builder.ToString().TrimEnd().ReplaceLineEndings(Environment.NewLine) + Environment.NewLine;
}

static string NormalizeMarkdownParagraph(string? value)
{
    return string.IsNullOrWhiteSpace(value)
        ? string.Empty
        : value.Trim().ReplaceLineEndings(Environment.NewLine);
}

static string CollapseExcessBlankLines(string value)
{
    var lines = value.ReplaceLineEndings("\n").Split('\n');
    var result = new StringBuilder();
    var blankCount = 0;

    foreach (var line in lines)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            blankCount++;
            if (blankCount > 2)
            {
                continue;
            }
        }
        else
        {
            blankCount = 0;
        }

        result.AppendLine(line);
    }

    return result.ToString().TrimEnd().ReplaceLineEndings(Environment.NewLine);
}

sealed record GeneratorOptions(string TopicsPath, string TemplatePath, string? Date, bool Force, bool DryRun)
{
    public static GeneratorOptions Parse(string[] args)
    {
        var topicsPath = "topics.json";
        var templatePath = Path.Combine("scripts", "templates", "topic-readme-template.md");
        string? date = null;
        var force = false;
        var dryRun = false;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--topics":
                    topicsPath = ReadRequiredValue(args, ref index, "--topics");
                    break;
                case "--template":
                    templatePath = ReadRequiredValue(args, ref index, "--template");
                    break;
                case "--date":
                    date = ReadRequiredValue(args, ref index, "--date");
                    break;
                case "--force":
                    force = true;
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "-h":
                case "--help":
                    PrintUsage();
                    Environment.Exit(0);
                    break;
                default:
                    Console.Error.WriteLine($"Unknown argument: {args[index]}");
                    PrintUsage();
                    Environment.Exit(1);
                    break;
            }
        }

        return new GeneratorOptions(topicsPath, templatePath, date, force, dryRun);
    }

    static string ReadRequiredValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length || args[index + 1].StartsWith('-'))
        {
            Console.Error.WriteLine($"Missing value for {optionName}.");
            Environment.Exit(1);
        }

        index++;
        return args[index];
    }

    static void PrintUsage()
    {
        Console.WriteLine("""
        Usage:
          dotnet scripts/generate-from-topics.cs -- [options]

        Examples:
          dotnet scripts/generate-from-topics.cs -- --dry-run
          dotnet scripts/generate-from-topics.cs -- --force
          dotnet scripts/generate-from-topics.cs -- --date 2026-04-22 --force

        Options:
          --topics <path>     Topics JSON path. Defaults to topics.json.
          --template <path>   README template path. Defaults to scripts/templates/topic-readme-template.md.
          --date <yyyy-MM-dd> Optional. Generate one topic README; omit to generate all topics.
          --force             Overwrite existing README.md files.
          --dry-run           Print planned writes without changing files.
          -h, --help          Show help.
        """);
    }
}

sealed record Topic
{
    [JsonPropertyName("topic")]
    public string TopicTitle { get; init; } = "";

    public string Description { get; init; } = "";

    public string Date { get; init; } = "";

    public string SpeakerName { get; init; } = "";

    public string SpeakerTitle { get; init; } = "";

    public string? SpeakerAvatar { get; init; }

    public string? SpeakerDescription { get; init; }

    public string? SpeakerGithub { get; init; }

    public string? SlideFileName { get; init; }

    public string? IntroUrl { get; init; }
    
    public string? RecordingUrl { get; init; }

    public TopicLink[]? Links { get; init; }

    public string[]? Tags { get; init; }
}

sealed record TopicLink(string Title, string Url);

[JsonSerializable(typeof(List<Topic>))]
sealed partial class TopicJsonContext : JsonSerializerContext;
