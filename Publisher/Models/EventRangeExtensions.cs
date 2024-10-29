namespace Publisher.Models;

public static class EventRangeExtensions
{
    public static int[] ToRange(this EventRange range)
    {
        return Enumerable.Range(range.Begin, range.End - range.Begin + 1).ToArray();
    }
}