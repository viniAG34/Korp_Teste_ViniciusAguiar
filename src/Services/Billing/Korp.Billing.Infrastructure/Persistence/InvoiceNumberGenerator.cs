using System.Data;
using Korp.Billing.Application.Common;
using Korp.Billing.Application.Invoices;
using Microsoft.EntityFrameworkCore;

namespace Korp.Billing.Infrastructure.Persistence;

public sealed class InvoiceNumberGenerator(BillingDbContext context) : IInvoiceNumberGenerator
{
    public async Task<long> GetNextAsync(CancellationToken cancellationToken)
    {
        try
        {
            var connection = context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT nextval('invoice_number_seq')";
            var value = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (DatabaseErrorClassifier.IsUnavailable(exception))
        { throw new BillingServiceUnavailableException(exception); }
    }
}
