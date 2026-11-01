#nullable enable
using System;
using System.IO;
using System.Text.Json;
namespace ZNQInterface.Services.Product
{
    public sealed class ProductDataOptions
    {
        public string ExcelDirectory { get; private init; } = "";
        public string Error { get; private init; } = "";
        public static ProductDataOptions Load(string path)
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                string directory = doc.RootElement.GetProperty("excelDirectory").GetString() ?? "";
                if (!Path.IsPathFullyQualified(directory)) throw new InvalidDataException("excelDirectory必须为绝对目录。");
                return new ProductDataOptions { ExcelDirectory = Path.GetFullPath(directory) };
            }
            catch (Exception e) { return new ProductDataOptions { Error = "产品目录配置错误：" + e.Message }; }
        }
        public void Validate()
        {
            if (Error.Length != 0) throw new InvalidDataException(Error);
            if (!Directory.Exists(ExcelDirectory)) throw new DirectoryNotFoundException("产品Excel目录不存在：" + ExcelDirectory);
        }
    }
}
