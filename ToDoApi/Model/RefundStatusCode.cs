namespace GownApi.Model
{
    public enum RefundStatusCode:short
    {
        None = -1,
        Completed = 0,
        InProgress = 13,
        Failed = 2,
        Requested = 3
    }
}
