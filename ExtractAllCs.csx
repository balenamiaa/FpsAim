using System.IO;
using System.Text;

string srcFolderPath = "src";
string outputFilePath = "extracted_code.txt";

if (!Directory.Exists(srcFolderPath))
{
    Console.WriteLine($"Error: Source folder not found: {srcFolderPath}");
    return;
}

StringBuilder outputBuilder = new StringBuilder();

foreach (string filePath in Directory.EnumerateFiles(srcFolderPath, "*.cs", SearchOption.AllDirectories))
{
    string fileName = Path.GetFileName(filePath);
    string fileContent = File.ReadAllText(filePath);

    outputBuilder.AppendLine($"<{fileName}>");
    outputBuilder.AppendLine(fileContent);
    outputBuilder.AppendLine($"</{fileName}>");
}

File.WriteAllText(outputFilePath, outputBuilder.ToString());

Console.WriteLine($"Code extraction complete. Output file: {outputFilePath}");