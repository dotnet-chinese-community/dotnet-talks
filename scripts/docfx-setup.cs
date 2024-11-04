string[] folders = ["./2024"];

ReadmeToIndex(Environment.CurrentDirectory);
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
	var lines = dirList.Select(dir => $"""
- name: {dir}
  href: ./{dir}/
""");
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
