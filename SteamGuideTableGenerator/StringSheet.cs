public class StringSheet
{
    public StringSheet(CsvHelper.CsvReader csvReader)
    {
        if (!csvReader.Read())
        {
            throw new InvalidDataException("Cannot read csv stream.");
        }
        csvReader.ReadHeader();

        if (csvReader.HeaderRecord == null)
        {
            throw new InvalidDataException("csv stream contains no header.");
        }

        _header = csvReader.HeaderRecord;
        int rowNumber = 0;
        while (csvReader.Read())
        {
            ++rowNumber;
            string[]? row = new string[_header.Length];
            for (int columnIndex = 0; columnIndex < _header.Length; columnIndex++)
            {
                row[columnIndex] = csvReader.GetField(columnIndex) ?? string.Empty;
            }
            if (row == null)
            {
                throw new InvalidDataException($"Unable to read row #{rowNumber}");
            }
            _rows.Add(row);
        }
    }

    public IReadOnlyList<string> header => _header;
    public IReadOnlyList<IReadOnlyList<string>> rows => _rows;
    public int columnCount => _header.Length;

    private string[] _header = Array.Empty<string>();
    private List<string[]> _rows = new();
}
