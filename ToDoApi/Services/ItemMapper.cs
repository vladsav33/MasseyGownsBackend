namespace GownApi.Services;
using GownApi.Model.Dto;

public static class ItemMapper
{
    public static ItemDegreeDto ToDto(ItemDegreeModel item)
    {
        if (item == null) return null;

        return new ItemDegreeDto
        {
            Id = item.Id,
            DegreeId = item.DegreeId,
            DegreeName = item.DegreeName,
            Name = item.Name,
            PictureBase64 = item.Picture != null ? Convert.ToBase64String(item.Picture) : null,
            HirePrice = item.HirePrice,
            BuyPrice = item.BuyPrice,
            Category = item.Category,
            Description = item.Description,
            IsHiring = item.IsHiring,
            Active = item.Active
        };
    }
    public static List<ItemDegreeDto> ToDtoList(IEnumerable<ItemDegreeModel> items)
    {
        return items?.Select(ToDto).ToList() ?? new List<ItemDegreeDto>();
    }
}
