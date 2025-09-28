using GownApi.Model;
using GownApi.Model.Dto;

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
    }
}
