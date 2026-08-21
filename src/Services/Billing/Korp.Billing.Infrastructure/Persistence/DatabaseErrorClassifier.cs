using Npgsql;

namespace Korp.Billing.Infrastructure.Persistence;

internal static class DatabaseErrorClassifier
{
    public static bool IsUnavailable(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
        {
            if (current is NpgsqlException or TimeoutException) return true;
        }

        return false;
    }
}
