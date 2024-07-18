using System;
using System.Collections.Generic;
using System.Linq;
using Xpense.Services.Entities;
using Xpense.Services.Enums;
using Xpense.Services.ValueObjects;
namespace Xpense.API.Models.Responses;

public class TransactionResponse(
    int id,
    Money amount,
    long? createdOn,
    long? lastUpdated,
    AccountResponse account,
    TransactionType type,
    CategoryResponse category,
    MerchantResponse merchant,
    IEnumerable<TagResponse> tags)
{
    public int Id { get; set; } = id;
    public Money Amount { get; set; } = amount;
    public long? CreatedOn { get; set; } = createdOn;
    public long? LastUpdated { get; set; } = lastUpdated;
    public TransactionType Type => type;
    public CategoryResponse Category { get; set; } = category;
    public AccountResponse Account { get; set; } = account;
    public MerchantResponse Merchant { get; set; } = merchant;
    public IEnumerable<TagResponse> Tags { get; set; } = tags;

    public static TransactionResponse Of(Transaction transaction)
    {
        var amount = Money.OfCents(transaction.Amount, transaction.Currency);
        var createdOn = new DateTimeOffset(transaction.CreatedOn).ToUnixTimeSeconds();
        long? lastUpdated = transaction.LastUpdated.HasValue
            ? new DateTimeOffset(transaction.LastUpdated.Value).ToUnixTimeSeconds()
            : null;
        var merchant = MerchantResponse.Of(transaction.Merchant);
        var tags = transaction.Tags?.Select(TagResponse.Of);
        var account = AccountResponse.Of(transaction.Account);
        var category = CategoryResponse.Of(transaction.Category);

        return new TransactionResponse(
            transaction.Id,
            amount,
            createdOn,
            lastUpdated,
            account,
            transaction.TransactionType,
            category,
            merchant,
            tags
        );
    }
}