using System.Globalization;

namespace Loja.SharedKernel;

/// <summary>
/// An amount of money in a given currency. Immutable; always rounded to 2 decimal places.
/// </summary>
public sealed record Money
{
    public const string DefaultCurrency = "BRL";

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    /// <summary>ISO 4217 code, e.g. "BRL".</summary>
    public string Currency { get; }

    public static Money Of(decimal amount, string currency = DefaultCurrency)
    {
        if (currency is not { Length: 3 } || !currency.All(char.IsAsciiLetterUpper))
        {
            throw new ArgumentException($"'{currency}' is not an ISO 4217 currency code.", nameof(currency));
        }

        // Commercial rounding: 0.005 goes up to 0.01.
        return new Money(Math.Round(amount, 2, MidpointRounding.AwayFromZero), currency);
    }

    public static Money Zero(string currency = DefaultCurrency) => Of(0m, currency);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return Of(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return Of(Amount - other.Amount, Currency);
    }

    public Money Multiply(int quantity) => Of(Amount * quantity, Currency);

    public static Money operator +(Money left, Money right) => left.Add(right);

    public static Money operator -(Money left, Money right) => left.Subtract(right);

    public static Money operator *(Money money, int quantity) => money.Multiply(quantity);

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Amount:0.00} {Currency}");

    // Mixing currencies is a programming error, not an expected business outcome.
    private void EnsureSameCurrency(Money other)
    {
        if (other.Currency != Currency)
        {
            throw new InvalidOperationException($"Cannot combine {Currency} with {other.Currency}.");
        }
    }
}
