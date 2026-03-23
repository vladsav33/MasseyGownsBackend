namespace GownApi.Model
{
    public enum RefundStatusCode:short
    {
        None = -1,
        Completed = 0,
        Failed = 2,
        Requested = 3,
        InProgress = 13
    }
}
