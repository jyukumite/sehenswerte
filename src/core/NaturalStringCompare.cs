namespace SehensWerte
{
    public class NaturalStringCompare : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            return (x == null || y == null) ? -1 : ((string)x).NaturalCompare((string)y);
        }
    }
}
