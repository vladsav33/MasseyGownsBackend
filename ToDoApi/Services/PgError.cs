using Npgsql;

namespace GownApi.Services
{
    public static class PgError
    {
        public static string Match(PostgresException pg) =>
            pg.SqlState switch
            {
                "23505" => "This value already exists",
                "23503" => "Foreign key constraint failed.",
                "23502" => "Null constraint violation",
                "23514" => "Check constraint failed",
                _ => pg.MessageText
            };
    }
}
