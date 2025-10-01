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
                OrderDate = orderDto.OrderDate
            };
        }

        public static async Task<OrderDtoOut> ToDtoOut(Orders order, GownDb db)
        {
            if (order == null)
                return null;

            var items = await db.selectedItemOut
                    .FromSqlRaw(@"SELECT oi.id, i.name as item_name, size as size_name, f.fit_type as fit_name, h.name as hood_name, hire, quantity
                                  FROM ordered_items oi
                                  INNER JOIN sku sk ON sk.id = oi.sku_id
                                  INNER JOIN items i ON i.id = sk.item_id
                                  LEFT JOIN sizes s ON s.id = sk.size_id
                                  LEFT JOIN fit f ON f.id = sk.fit_id
                                  LEFT JOIN hood_type h ON h.id = sk.hood_id
                                  WHERE oi.order_id = {0}", order.Id)
                    .ToArrayAsync();

            //var items
            //foreach( var item in items )
            //{

            //}

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
                Items = items
            };
        }

    }
}
