using DocumentFormat.OpenXml.Drawing.Charts;
using GownApi.Model;
using GownApi.Model.Dto;
using Microsoft.EntityFrameworkCore;

namespace GownApi.Services
{
    public static class OrderMapper
    {
        public static Orders FromDto(OrderDto orderDto)
        {
            if (orderDto == null)
                return null;

            return new Orders
            {
                Id = orderDto.Id,
                FirstName = orderDto.FirstName,
                LastName = orderDto.LastName,
                Email = orderDto.Email,
                Address = orderDto.Address,
                City = orderDto.City,
                Postcode = orderDto.Postcode,
                Country = orderDto.Country,
                Phone = orderDto.Phone,
                Mobile = orderDto.Mobile,
                StudentId = orderDto.StudentId,
                Message = orderDto.Message,
                Paid = orderDto.Paid,
                PaymentMethod = orderDto.PaymentMethod,
                PurchaseOrder = orderDto.PurchaseOrder,
                OrderDate = orderDto.OrderDate,
                DegreeId = orderDto.DegreeId,
                CeremonyId = orderDto.CeremonyId,
                OrderType = orderDto.OrderType,
                Note = orderDto.Note,
                Changes = orderDto.Changes,
                AmountPaid = orderDto.AmountPaid,
                AmountOwning = orderDto.AmountOwning,
                Donation = orderDto.Donation,
                Freight = orderDto.Freight,
                Refund = orderDto.Refund,
                AdminCharges = orderDto.AdminCharges,
                PayBy = orderDto.PayBy,
            };
        }

        public static async Task<OrderDtoOut> ToDtoOut(Orders order, GownDb db)
        {
            if (order == null)
                return null;

            var items = await db.selectedItemOut
                    .FromSqlRaw(@"SELECT oi.id, i.name as item_name, i.id as item_id, size as size_name, s.id as size_id, labeldegree, s.labelsize, f.fit_type as fit_name, h.name as hood_name, hire, quantity
                                  FROM ordered_items oi
                                  INNER JOIN sku sk ON sk.id = oi.sku_id
                                  INNER JOIN orders o ON oi.order_id = o.id
                                  INNER JOIN degrees d ON o.degree_id = d.id
                                  INNER JOIN items i ON i.id = sk.item_id
                                  LEFT JOIN sizes s ON s.id = sk.size_id
                                  LEFT JOIN fit f ON f.id = sk.fit_id
                                  LEFT JOIN hood_type h ON h.id = sk.hood_id
                                  WHERE oi.order_id = {0}", order.Id)
                    .ToArrayAsync();

            return new OrderDtoOut
            {
                Id = order.Id,
                FirstName = order.FirstName,
                LastName = order.LastName,
                Email = order.Email,
                Address = order.Address,
                City = order.City,
                Postcode = order.Postcode,
                Country = order.Country,
                Phone = order.Phone,
                Mobile = order.Mobile,
                StudentId = order.StudentId,
                Message = order.Message,
                Paid = order.Paid,
                PaymentMethod = order.PaymentMethod,
                PurchaseOrder = order.PurchaseOrder,
                OrderDate = order.OrderDate,
                Items = items,
                CeremonyId = order.CeremonyId,
                DegreeId = order.DegreeId,
                OrderType = order.OrderType,
                Note = order.Note,
                Changes = order.Changes,
                AmountPaid = order.AmountPaid,
                AmountOwning = order.AmountOwning,
                Donation = order.Donation,
                Freight = order.Freight,
                Refund = order.Refund,
                AdminCharges = order.AdminCharges,
                PayBy = order.PayBy,
            };
        }

    }
}
