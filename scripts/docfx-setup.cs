#! /usr/bin/env dotnet

using System.Text;

const string tocFormat = """
- name: {0}
  href: {0}
  homepage: ./{0}/index.md
""";

string[] folders = ["./2024", "./2025"];

ReadmeToIndex(Environment.CurrentDirectory);
var rootTocPath = "./toc.yml";
if (!File.Exists(rootTocPath))
{
    var navLines = folders.Select(dir => string.Format(tocFormat, new DirectoryInfo(dir).Name));
    await File.WriteAllLinesAsync(rootTocPath, navLines, Encoding.UTF8);
}

foreach(var folder in folders)
{
    ReadmeToIndex(folder);
    var dirList = new List<string>();
    foreach (var dir in Directory.GetDirectories(folder))
    {
        dirList.Add(new DirectoryInfo(dir).Name);
        ReadmeToIndex(dir);
	}
    // generate toc.yml
    var tocPath = Path.Combine(folder, "toc.yml");
    var lines = dirList.Order().Select(dir => string.Format(tocFormat, dir));	
    await File.WriteAllLinesAsync(tocPath, lines, Encoding.UTF8);
}

// replace READ.md => index.md
static void ReadmeToIndex(string folder)
{
    var readmePath = Path.Combine(folder, "README.md");
    var indexPath = Path.Combine(folder, "index.md");
    if (File.Exists(readmePath) && !File.Exists(indexPath))
    {
        File.Move(readmePath, indexPath);
    }
}
