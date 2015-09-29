using Dapper;
using Depmon.Server.Domain.Model;

namespace Depmon.Server.Database
{
    public class ReportRepository : Repository<Report>
    {
        protected override string TableName => "Reports";

        public override void Save(Report report)
        {
            var sqlInsert = $@"INSERT INTO {TableName} (CreatedAt)
                                        VALUES  (@CreatedAt)";

            var sqlUpdate = $@"UPDATE {TableName}
                              SET CreatedAt = @CreatedAt
                              WHERE Id = @Id";
            var sql = report.Id == 0 ? sqlInsert : sqlUpdate;

            _connection.Execute(sql, report);
        }

        public override void InsertMany(params Report[] reports)
        {
            var sql = $@"INSERT INTO {TableName} (CreatedAt)
                                        VALUES  (@CreatedAt)";

            _connection.Execute(sql, reports);
        }
    }
}
