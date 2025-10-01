public class TradeProcessor
{
    private IEnumerable<string> GetLinesFromStream(System.IO.Stream stream)
    {
        var lines = new List<string>();
        using (var reader = new System.IO.StreamReader(stream))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                lines.Add(line);
            }
        }
        return lines;
    }

    private void LogWarning(string message)
    {
        Console.WriteLine($"WARN: {message}");
    }

    private void LogInformation(string message)
    {
        Console.WriteLine($"INFO: {message}");
    }

    private TradeRecord? ParseTrade(string line, int lineNumber)
    {
        int ExpectedFieldCount = 3;
        int CurrencyCodeLength = 6;
        float LotSize = 100000f;

        var fields = line.Split(',');

        if (fields.Length != ExpectedFieldCount)
        {
            LogWarning($"Line {lineNumber} malformed. Only {fields.Length} field(s) found.");
            return null;
        }

        if (fields[0].Length != CurrencyCodeLength)
        {
            LogWarning($"Trade currencies on line {lineNumber} malformed: '{fields[0]}'");
            return null;
        }

        if (!int.TryParse(fields[1], out var tradeAmount))
        {
            LogWarning($"Trade amount on line {lineNumber} not a valid integer: '{fields[1]}'");
            return null;
        }

        if (!decimal.TryParse(fields[2], out var tradePrice))
        {
            LogWarning($"Trade price on line {lineNumber} not a valid decimal: '{fields[2]}'");
            return null;
        }

        return new TradeRecord
        {
            SourceCurrency = fields[0].Substring(0, 3),
            DestinationCurrency = fields[0].Substring(3, 3),
            Lots = tradeAmount / LotSize,
            Price = tradePrice
        };
    }

    private void SaveTrades(IEnumerable<TradeRecord> trades)
    {
        using (
            var connection = new System.Data.SqlClient.SqlConnection(
                "Data Source=(local); Initial Catalog=TradeDatabase; Integrated Security=True"
            )
        )
        {
            connection.Open();
            using (var transaction = connection.BeginTransaction())
            {
                foreach (var trade in trades)
                {
                    var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandType = System.Data.CommandType.StoredProcedure;
                    command.CommandText = "dbo.insert_trade";
                    command.Parameters.AddWithValue("@sourceCurrency", trade.SourceCurrency);
                    command.Parameters.AddWithValue(
                        "@destinationCurrency",
                        trade.DestinationCurrency
                    );
                    command.Parameters.AddWithValue("@lots", trade.Lots);
                    command.Parameters.AddWithValue("@price", trade.Price);

                    command.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            connection.Close();
        }
    }

    public void ProcessTrades(System.IO.Stream stream)
    {
        var lines = GetLinesFromStream(stream);
        var trades = new List<TradeRecord>();

        var lineCount = 1;
        foreach (var line in lines)
        {
            var trade = ParseTrade(line, lineCount);
            if (trade != null)
            {
                trades.Add(trade);
            }
            lineCount++;
        }

        SaveTrades(trades);

        LogInformation($"{trades.Count} trades processed");
    }
}

public class TradeRecord
{
    public string SourceCurrency { get; set; }

    public string DestinationCurrency { get; set; }

    public float Lots { get; set; }

    public decimal Price { get; set; }
}
