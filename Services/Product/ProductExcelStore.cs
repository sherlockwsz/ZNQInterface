#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using ZNQInterface.Models.Product;

namespace ZNQInterface.Services.Product
{
    // Called by one storage worker; Excel is the only persistent product authority.
    public sealed class ProductExcelStore
    {
        private readonly ProductDataOptions _options;
        private static readonly string[] Headers = {
            "扫码时间", "产品编号", "初始X偏差(mm)", "初始Y偏差(mm)", "初始同轴度(mm)",
            "最终X偏差(mm)", "最终Y偏差(mm)", "最终同轴度(mm)", "调整次数", "调整时间(s)",
            "是否合格", "NG原因", "检测完成时间", "SessionId", "RequestId", "完成序号" };
        public ProductExcelStore(ProductDataOptions options) { _options = options; }
        private IEnumerable<string> Files()
        {
            _options.Validate();
            return Directory.EnumerateFiles(_options.ExcelDirectory, "*.xlsx")
                .Where(p => DateTime.TryParseExact(Path.GetFileNameWithoutExtension(p), "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out _)).OrderBy(p => p).ToArray();
        }
        private static FileStream OpenRead(string path) => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        private static IXLWorksheet Sheet(XLWorkbook book)
        {
            if (book.Worksheets.Count != 1) throw new InvalidDataException("产品Excel工作表数量错误。");
            var sheet = book.Worksheet(1);
            for (int c = 1; c <= Headers.Length; c++)
                if (sheet.Cell(1, c).GetString() != Headers[c - 1]) throw new InvalidDataException("产品Excel表头不匹配，禁止覆盖。");
            if (sheet.LastColumnUsed()?.ColumnNumber() != Headers.Length
                || Enumerable.Range(1, 13).Any(c => sheet.Column(c).IsHidden)
                || Enumerable.Range(14, 3).Any(c => !sheet.Column(c).IsHidden))
                throw new InvalidDataException("产品Excel列布局不匹配，禁止覆盖。");
            return sheet;
        }
        private static double? Number(IXLCell cell)
        {
            if (cell.IsEmpty()) return null;
            if (cell.HasFormula || cell.DataType != XLDataType.Number || !double.IsFinite(cell.GetDouble()))
                throw new InvalidDataException("产品Excel测量单元格无效。");
            return cell.GetDouble();
        }
        private static ProductDataItem ReadRow(IXLWorksheet s, int r)
        {
            var row = new ProductDataItem {
                DetectionTime = s.Cell(r, 1).GetDateTime(), ProductNumber = s.Cell(r, 2).GetString(),
                InitialX = Number(s.Cell(r, 3)), InitialY = Number(s.Cell(r, 4)), InitialCoaxiality = Number(s.Cell(r, 5)),
                FinalX = Number(s.Cell(r, 6)), FinalY = Number(s.Cell(r, 7)), Coaxiality = Number(s.Cell(r, 8)),
                TotalAdjustCount = checked((uint)s.Cell(r, 9).GetDouble()), AdjustmentSeconds = s.Cell(r, 10).GetDouble(),
                IsQualified = s.Cell(r, 11).GetString() == "合格", NgReason = s.Cell(r, 12).GetString(),
                CompletionTime = s.Cell(r, 13).GetDateTime(),
                SessionId = ulong.Parse(s.Cell(r, 14).GetString(), CultureInfo.InvariantCulture),
                RequestId = uint.Parse(s.Cell(r, 15).GetString(), CultureInfo.InvariantCulture),
                CompleteSeq = uint.Parse(s.Cell(r, 16).GetString(), CultureInfo.InvariantCulture) };
            if (row.SessionId == 0 || row.RequestId == 0 || row.CompleteSeq == 0 || string.IsNullOrWhiteSpace(row.ProductNumber)
                || !double.IsFinite(row.AdjustmentSeconds) || row.AdjustmentSeconds < 0
                || (s.Cell(r, 11).GetString() != "合格" && s.Cell(r, 11).GetString() != "不合格")
                || s.Cell(r, 2).DataType != XLDataType.Text || s.Cell(r, 2).HasFormula)
                throw new InvalidDataException("产品Excel身份或结果无效，禁止覆盖。");
            return row;
        }
        public IReadOnlyList<ProductDataItem> LoadAll()
        {
            var result = new Dictionary<string, ProductDataItem>();
            foreach (string file in Files())
            {
                using var stream = OpenRead(file);
                using var book = new XLWorkbook(stream);
                var sheet = Sheet(book);
                for (int r = 2; r <= (sheet.LastRowUsed()?.RowNumber() ?? 1); r++)
                {
                    var item = ReadRow(sheet, r);
                    if (!result.TryAdd(item.Key, item)) throw new InvalidDataException("Excel存在重复工件身份：" + item.Key);
                }
            }
            return result.Values.OrderByDescending(i => i.CompletionTime).ToArray();
        }
        private static void SetNumber(IXLCell c, double? value)
        { if (value.HasValue) c.Value = value.Value; }
        private static void WriteRow(IXLWorksheet s, int r, ProductDataItem p)
        {
            s.Cell(r, 1).Value = p.DetectionTime;
            s.Cell(r, 2).Style.NumberFormat.Format = "@";
            s.Cell(r, 2).Value = p.ProductNumber;
            SetNumber(s.Cell(r, 3), p.InitialX); SetNumber(s.Cell(r, 4), p.InitialY); SetNumber(s.Cell(r, 5), p.InitialCoaxiality);
            SetNumber(s.Cell(r, 6), p.FinalX); SetNumber(s.Cell(r, 7), p.FinalY); SetNumber(s.Cell(r, 8), p.Coaxiality);
            s.Cell(r, 9).Value = p.TotalAdjustCount; s.Cell(r, 10).Value = p.AdjustmentSeconds;
            s.Cell(r, 11).Value = p.QualificationText; s.Cell(r, 12).Value = p.NgReason; s.Cell(r, 13).Value = p.CompletionTime;
            for (int c = 14; c <= 16; c++) s.Cell(r, c).Style.NumberFormat.Format = "@";
            s.Cell(r, 14).Value = p.SessionId.ToString(CultureInfo.InvariantCulture);
            s.Cell(r, 15).Value = p.RequestId.ToString(CultureInfo.InvariantCulture);
            s.Cell(r, 16).Value = p.CompleteSeq.ToString(CultureInfo.InvariantCulture);
            s.Cell(r, 1).Style.DateFormat.Format = s.Cell(r, 13).Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
            s.Range(r, 3, r, 8).Style.NumberFormat.Format = "0.000000";
            s.Cell(r, 10).Style.NumberFormat.Format = "0.000";
        }
        // Returns only once Excel has the row (including a previously committed duplicate).
        public ProductDataItem Save(ProductDataItem item)
        {
            _options.Validate();
            string path = Path.Combine(_options.ExcelDirectory, item.CompletionTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".xlsx");
            // Deny other writers, including Excel; allow atomic replacement while this handle is open.
            using var guard = File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read | FileShare.Delete) : null;
            using var book = guard == null ? new XLWorkbook() : new XLWorkbook(guard);
            IXLWorksheet sheet;
            if (guard == null)
            {
                sheet = book.AddWorksheet("产品数据");
                for (int c = 1; c <= Headers.Length; c++) sheet.Cell(1, c).Value = Headers[c - 1];
                sheet.Row(1).Style.Font.Bold = true;
                sheet.Columns(1, 13).Width = 22;
                sheet.Columns(14, 16).Hide();
            }
            else sheet = Sheet(book);
            // Cross-day identity checks use the worker's loaded, durable mirror.
            // Still validate every existing row and duplicate identity in this target file.
            var rows = new Dictionary<string, ProductDataItem>();
            for (int r = 2; r <= (sheet.LastRowUsed()?.RowNumber() ?? 1); r++)
            {
                var row = ReadRow(sheet, r);
                if (!rows.TryAdd(row.Key, row)) throw new InvalidDataException("Excel存在重复工件身份：" + row.Key);
            }
            if (rows.TryGetValue(item.Key, out var existing))
            {
                if (existing.ProductNumber != item.ProductNumber || existing.CompleteSeq != item.CompleteSeq)
                    throw new InvalidDataException("同一工件身份的完成载荷不一致，禁止重复追加。");
                return existing;
            }
            WriteRow(sheet, (sheet.LastRowUsed()?.RowNumber() ?? 1) + 1, item);
            string temporary = Path.Combine(_options.ExcelDirectory, ".product-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                { book.SaveAs(stream); stream.Flush(true); }
                using (var stream = OpenRead(temporary))
                using (var verify = new XLWorkbook(stream))
                {
                    var check = Sheet(verify);
                    if (ReadRow(check, check.LastRowUsed()!.RowNumber()).Key != item.Key)
                        throw new InvalidDataException("临时Excel验证失败。");
                }
                if (guard == null) File.Move(temporary, path); // never overwrite a concurrently created workbook
                else File.Replace(temporary, path, null);
                return item;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
