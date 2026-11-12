using DocumentFormat.OpenXml.Drawing.Charts;
using GownApi.Model;
using GownApi.Model.Dto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;

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
                PaymentEc = orderDto.PaymentEc,
                PaymentEm = orderDto.PaymentEm,
                Postcode = orderDto.Postcode,
                Country = orderDto.Country,
                Phone = orderDto.Phone,
                OrderAmount = orderDto.OrderAmount,
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

        public static async Task<Orders> ToEntity(OrderDtoIn dto, int ceremonyId, GownDb db)
        {
            ArgumentNullException.ThrowIfNull(dto);

            // Qualification arrives as text; match it to a degree by name (case-insensitive)
            int? degreeId = null;
            if (!string.IsNullOrWhiteSpace(dto.Qualification))
            {
                var qualification = dto.Qualification.Trim().ToLower();
                degreeId = await db.degrees
                    .Where(d => d.Name.ToLower() == qualification)
                    .Select(d => (int?)d.Id)
                    .FirstOrDefaultAsync();
            }

            var nzTz = TimeZoneInfo.FindSystemTimeZoneById("New Zealand Standard Time");
            var nzNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, nzTz);

            return new Orders
            {
                CeremonyId = ceremonyId,
                FirstName = dto.FirstName?.Trim() ?? "",
                LastName = dto.LastName?.Trim() ?? "",
                Email = dto.Email?.Trim() ?? "",
                Phone = dto.Phone?.Trim() ?? "",
                Address = JoinNonEmpty(", ", dto.Address1, dto.Address2, dto.State),
                City = dto.City?.Trim() ?? "",
                Postcode = dto.Zip?.Trim() ?? "",
                Country = dto.Country?.Trim() ?? "",
                StudentId = int.TryParse(dto.StudentId?.Trim(), out var studentId) ? studentId : 0,
                HeadSize = GetHeadSize(dto.Trencher),
                DegreeId = degreeId,
                OrderAmount = dto.TotalCost ?? 0,
                AmountPaid = dto.AmountPaid,
                AmountOwning = dto.AmountOwing,
                Donation = 0,
                Paid = dto.AmountOwing.HasValue ? dto.AmountOwing <= 0 : false,
                PaymentMethod = int.TryParse(dto.PaymentMethod?.Trim(), out var paymentMethod) ? paymentMethod : null,
                PaymentTxnId = NullIfEmpty(dto.PaymentReference),
                ReferenceNo = NullIfEmpty(dto.OrderNumber),
                Message = NullIfEmpty(dto.Comments),
                Note = BuildNote(dto),
                OrderDate = DateOnly.FromDateTime(nzNow),
                CreatedAt = DateTime.UtcNow,
            };
        }

        // The Orders table has no columns for the garments and extras, so keep them in Note
        private static string? BuildNote(OrderDtoIn dto)
        {
            var parts = new List<string>();

            void Add(string label, string? value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    parts.Add($"{label}: {value.Trim()}");
            }

            // e.g. { "size": "M", "colour": "Black" } -> "Gown: size=M, colour=Black"
            void AddDictionary(string label, Dictionary<string, string?>? values)
            {
                if (values is null) return;
                Add(label, JoinNonEmpty(", ", values
                    .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
                    .Select(kv => $"{kv.Key}={kv.Value!.Trim()}")
                    .ToArray()));
            }

            foreach (var gown in dto.Gown ?? [])
                AddDictionary("Gown", gown);
            AddDictionary("Hood", dto.Hood);
            AddDictionary("Trencher", dto.Trencher);
            Add("Bonnet", dto.Bonnet);
            Add("Mortarboard", dto.Mortarboard);
            Add("Korowai", dto.Korowai);
            AddDictionary("Stole", dto.Stole);
            Add("Hood pin", dto.HoodPin);
            Add("Tie pin", dto.Tiepin);
            Add("Keyring", dto.Keyring);
            Add("Garment bag", dto.GarmentBag);
            Add("Photography", dto.Photography);
            Add("Prepaid return", dto.PrepaidReturn);
            Add("Qualification", dto.Qualification);
            Add("University", dto.University);
            Add("Company", dto.Company);
            Add("Supplier", dto.Supplier);
            if (dto.Bond.HasValue)
                parts.Add($"Bond: {dto.Bond.Value:0.00}");

            return parts.Count == 0 ? null : string.Join("; ", parts);
        }

        // Trencher["Size"] (e.g. "57" or "57cm") -> head_size; key match is case-insensitive
        private static int? GetHeadSize(Dictionary<string, string?>? trencher)
        {
            var size = trencher?
                .FirstOrDefault(kv => string.Equals(kv.Key, "Size", StringComparison.OrdinalIgnoreCase))
                .Value;
            if (string.IsNullOrWhiteSpace(size))
                return null;

            var digits = new string(size.Trim().TakeWhile(char.IsDigit).ToArray());
            return int.TryParse(digits, out var headSize) ? headSize : null;
        }

        private static string JoinNonEmpty(string separator, params string?[] values) =>
            string.Join(separator, values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!.Trim()));

        private static string? NullIfEmpty(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        public static async Task<OrderDtoOut> ToDtoOut(OrderGet order, GownDb db)
        {
            if (order == null)
                return null;

            var items = await db.selectedItemOut
                    .FromSqlRaw(@"SELECT oi.id, oi.cost, i.name as item_name, i.id as item_id, s.size as size_name, s.id as size_id,
                                  s.labelsize, d.labeldegree, f.fit_type as fit_name, h.name as hood_name, h.short_name as hood_short,
                                  ht.size as hat_size, ht.labelsize as hat_label, hire, quantity
                                  FROM ordered_items oi
                                  INNER JOIN sku sk ON sk.id = oi.sku_id
                                  INNER JOIN orders o ON oi.order_id = o.id
                                  LEFT JOIN degrees d ON o.degree_id = d.id
                                  INNER JOIN items i ON i.id = sk.item_id
                                  LEFT JOIN sizes s ON s.id = sk.size_id
                                  LEFT JOIN fit f ON f.id = sk.fit_id
                                  LEFT JOIN hats ht ON ht.id = sk.hat_id  
                                  LEFT JOIN hood_type h ON h.id = sk.hood_id
                                  WHERE oi.order_id = {0}", order.Id)
                    .ToArrayAsync();

            decimal amount = items.Sum(x => x.Cost);

            return new OrderDtoOut
            {
                Id = order.Id,
                FirstName = order.FirstName,
                LastName = order.LastName,
                Email = order.Email,
                Address = order.Address,
                City = order.City,
                PaymentEc = order.PaymentEc,
                PaymentEm = order.PaymentEm,
                Postcode = order.Postcode,
                Country = order.Country,
                Phone = order.Phone,
                OrderAmount = order.OrderAmount,
                StudentId = order.StudentId,
                Height = order.Height ?? 0,
                HeadSize = order.HeadSize ?? 0,
                Message = order.Message,
                Paid = order.Paid ?? false,
                PaymentMethod = order.PaymentMethod,
                PurchaseOrder = order.PurchaseOrder,
                OrderDate = order.OrderDate,
                Items = items,
                Amount = amount,
                CeremonyId = order.CeremonyId,
                Ceremony = order.Ceremony,
                DegreeId = order.DegreeId,
                OrderType = order.OrderType,
                Note = order.Note,
                PackNote = order.PackNote,
                Changes = order.Changes,
                AmountPaid = order.AmountPaid,
                AmountOwning = order.AmountOwning,
                Donation = order.Donation,
                AccountCode = order.AccountCode,
                Freight = order.Freight,
                Refund = order.Refund,
                AdminCharges = order.AdminCharges,
                PayBy = order.PayBy,
                Status = order.Status,
                ReferenceNo = order.ReferenceNo,
                RefundedAmount = order.RefundedAmount,
                RefundTxnId = order.RefundTxnId,
                RefundedAt = order.RefundInitiatedAt,
                RefundEmailSentAt = order.RefundEmailSentAt,
                PaymentTxnId = order.PaymentTxnId,
                RefundLastEc = order.RefundLastEc,
                RefundLastEm = order.RefundLastEm,
                RefundStatusCode = order.RefundStatusCode
            };
        }

        public static Task<OrderDtoOut> ToDtoOut(Orders order, GownDb db)
        {
            if (order == null) return Task.FromResult<OrderDtoOut>(null);

            
            var og = new OrderGet
            {
                Id = order.Id,
                FirstName = order.FirstName,
                LastName = order.LastName,
                Email = order.Email,
                Address = order.Address,
                City = order.City,
                PaymentEc = order.PaymentEc,
                PaymentEm = order.PaymentEm,
                Postcode = order.Postcode,
                Country = order.Country,
                Phone = order.Phone,
                OrderAmount = order.OrderAmount,
                StudentId = order.StudentId,
                Message = order.Message,
                Paid = order.Paid,
                PaymentMethod = order.PaymentMethod,
                PurchaseOrder = order.PurchaseOrder,
                OrderDate = order.OrderDate,
                CeremonyId = order.CeremonyId,
                Ceremony = null,          
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
                Status = order.Status,
                ReferenceNo = order.ReferenceNo
            };

            return ToDtoOut(og, db);
        }

    }
}
