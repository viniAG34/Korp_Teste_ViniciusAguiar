using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Korp.Inventory.Infrastructure.Persistence;

internal static class DatabaseErrorClassifier
{
    public static bool IsUnavailable(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbException or TimeoutException
                || current is DbUpdateException { InnerException: DbException })
            {
                return true;
            }
        }

        return false;
    }
}
