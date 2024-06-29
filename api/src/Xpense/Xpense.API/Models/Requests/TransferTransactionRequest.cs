namespace Xpense.API.Models.Requests;

public class TransferTransactionRequest
{
    public decimal Amount { get; set; }
    public string Reason { get; set; }
    public string ToAccount { get; set; }
    public string FromAccount { get; set; }
    public int Category { get; set; }
    public int?[] Tags { get; set; } = null;
}