using GownApi.Model;
using GownApi.Model.Dto;
using GownApi.Services;
using Humanizer;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.Json;

namespace GownApi.Endpoints
{
    public static class AdminOrderController
    {
        public static void AdminOrderEndpoints(this WebApplication app)
        {
            _ = app.MapGet("/admin/hirednotgraduated/{id}", async (int id, GownDb db) =>
            {
                var sql = @"
                            SELECT
                                o.id,
                                o.student_id, 
                                o.last_name, 
                                o.first_name,
                                d.name as degree,
                                c.id_code as ceremony_name,
                                oi_selected.hood_name as hood_name,
                                o.reference_no, 
                                o.phone as mobile
                            FROM orders o
                            JOIN degrees d 
                                ON o.degree_id = d.id
                            JOIN ceremonies c
                                ON c.id = o.ceremony_id
                            LEFT JOIN LATERAL (
                                SELECT oi.*, ht.name AS hood_name
                                FROM ordered_items oi
                                LEFT JOIN sku sk ON sk.id = oi.sku_id
                                LEFT JOIN hood_type ht ON ht.id = sk.hood_id
                                WHERE oi.order_id = o.id
                                ORDER BY 
                                    CASE WHEN ht.id IS NOT NULL THEN 0 ELSE 1 END
                                LIMIT 1
                            ) oi_selected ON true
                            WHERE o.ceremony_id = @id
                            AND NOT EXISTS (
                                SELECT 1 
                                FROM ceremonies c
                                JOIN session_ceremony sc ON sc.ceremony_id = c.id
                                JOIN ceremony_import ci ON ci.ceremony_name = sc.session
                                WHERE c.id = @id 
                                  AND ci.student_id::numeric = o.student_id)";

                var param = new NpgsqlParameter("@id", id);
                var result = await db.hiredNotGraduated.FromSqlRaw(sql, param).ToListAsync();
                return Results.Ok(result);
            });

            _ = app.MapGet("/admin/graduatednothired/{id}", async(int id, GownDb db) =>
            {
                var sql = @"
                            SELECT ci.id, ci.student_id, ci.surname as last_name, ci.forename as first_name, ci.program_code as degree, c.id_code as ceremony_name, ci.program_desc as hood_name, ci.mobile FROM ceremony_import ci
                            JOIN session_ceremony sc ON sc.session=ci.ceremony_name
                            JOIN ceremonies c ON c.id = sc.ceremony_id
                            WHERE c.id = @id
                            AND NOT EXISTS (
	                            SELECT 1
	                            FROM orders o
	                            WHERE o.student_id = CAST (ci.student_id AS NUMERIC)
	                            AND o.ceremony_id = @id
                            )";
                var param = new NpgsqlParameter("@id", id);
                var result = await db.graduatedNotHired.FromSqlRaw(sql, param).ToListAsync();

                return Results.Ok(result);
            });

            _ = app.MapGet("/admin/ordersbyceremony/{id}", async (int id, GownDb db) =>
            {
                var sql = @"SELECT o.id as id, o.first_name, o.last_name, o.email, o.address, o.city, o.payment_ec, o.payment_em,o.postcode, o.country, o.phone,
                                      o.order_amount, o.student_id, o.message, o.paid, o.payment_method, o.purchase_order, o.order_date, c.id as ceremony_id,
                                      c.name as ceremony, o.degree_id, o.order_type, o.note, o.changes, o.pack_note, o.amount_paid,
                                      o.amount_owning, o.donation, o.freight, o.refund, o.admin_charges, o.pay_by, o.status, o.reference_no,
                                      o.refund_status_code, o.refund_txn_id, o.refunded_amount, o.refunded_at, o.payment_txn_id, o.refund_last_ec, o.refund_last_em, o.refund_email_sent_at
                                      FROM orders o
                                      LEFT JOIN ceremonies c
                                      ON o.ceremony_id = c.id
                                      WHERE o.ceremony_id = @id AND o.reference_no is not null
                                      ORDER BY o.reference_no DESC";
                var param = new NpgsqlParameter("@id", id);
                var result = await db.orderGets.FromSqlRaw(sql, param).ToListAsync();

                var resultList = new List<OrderDtoOut>();

                foreach (var res in result)
                {
                    var order = await OrderMapper.ToDtoOut(res, db);
                    resultList.Add(order);
                }

                return Results.Ok(resultList);
            });
        }
    }
}
