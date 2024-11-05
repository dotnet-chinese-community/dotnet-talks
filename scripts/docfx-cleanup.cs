var distPath = "./dist";
// s => https://dotnet-chinese-community.github.io/dotnet-talks/2024/2024-10-28/%E5%AE%A2%E6%88%B7%E7%AB%AF%E5%BA%94%E7%94%A8%E6%8A%80%E6%9C%AF%E6%96%B0%E7%89%B9%E6%80%A7.pdf
// d => https://github.com/dotnet-chinese-community/dotnet-talks/2024/2024-10-28/%E5%AE%A2%E6%88%B7%E7%AB%AF%E5%BA%94%E7%94%A8%E6%8A%80%E6%9C%AF%E6%96%B0%E7%89%B9%E6%80%A7.pdf
string[] htmlFiles = Directory.GetFiles(distPath, "*.html", SearchOption.AllDirectories);
foreach (string file in htmlFiles)
{
    // replace pdf links
    // var text = File.ReadAllText(file);
}
