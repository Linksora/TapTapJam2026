#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TapJam.EditorTools
{
    /// <summary>
    /// 表头按列名索引的 CSV 表。支持双引号包裹、引号内逗号/换行、"" 转义、UTF-8 BOM。
    /// </summary>
    public sealed class CsvTable
    {
        private readonly Dictionary<string, int> columns;
        private readonly List<string[]> rows;

        public int RowCount => rows.Count;

        public int ColumnCount => columns.Count;

        public CsvTable(Dictionary<string, int> columnIndex, List<string[]> dataRows)
        {
            columns = columnIndex;
            rows = dataRows;
        }

        public int GetRowLength(int row)
        {
            return row >= 0 && row < rows.Count ? rows[row].Length : 0;
        }

        public string Get(int row, string column)
        {
            if (row < 0 || row >= rows.Count || !columns.TryGetValue(column, out int index))
            {
                return string.Empty;
            }

            return index < rows[row].Length ? rows[row][index] : string.Empty;
        }
    }

    public static class CsvParser
    {
        public static CsvTable Parse(string text)
        {
            text = text.TrimStart('\uFEFF');

            var records = new List<List<string>>();
            var current = new List<string>();
            var field = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        field.Append(c);
                    }
                }
                else if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    current.Add(field.ToString());
                    field.Clear();
                }
                else if (c == '\n')
                {
                    current.Add(field.ToString());
                    field.Clear();
                    records.Add(current);
                    current = new List<string>();
                }
                else if (c != '\r')
                {
                    field.Append(c);
                }
            }

            if (field.Length > 0 || current.Count > 0)
            {
                current.Add(field.ToString());
                records.Add(current);
            }

            if (records.Count == 0)
            {
                return new CsvTable(new Dictionary<string, int>(), new List<string[]>());
            }

            var columnIndex = new Dictionary<string, int>();
            string[] header = records[0].ToArray();
            for (int i = 0; i < header.Length; i++)
            {
                string name = header[i].Trim();
                if (!string.IsNullOrEmpty(name))
                {
                    columnIndex[name] = i;
                }
            }

            var rows = new List<string[]>();
            for (int i = 1; i < records.Count; i++)
            {
                // 跳过纯空行（Excel 常见尾部空行）。
                bool empty = true;
                foreach (string value in records[i])
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        empty = false;
                        break;
                    }
                }

                if (!empty)
                {
                    rows.Add(records[i].ToArray());
                }
            }

            return new CsvTable(columnIndex, rows);
        }
    }
}
#endif
