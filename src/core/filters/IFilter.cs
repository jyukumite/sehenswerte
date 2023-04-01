using SehensWerte.Maths;

namespace SehensWerte.Filters
{
    public interface ISampleFilter // 1-for-1 input to output filter
    {
        //simple filter
        double Insert(double value);
        double[] Insert(double[] values);
        double LastOutput { get; }
        double LastInput { get; }

        //delay filter
        double[] History { get; }
        double[] Coefficients { get; }

        //adaptive filter
        double Insert(double value, double desired);
        double[] Insert(double[] value, double[] desired);
        bool AdaptiveHold { get; set; }
        double AdaptiveOutputLimit { get; set; }
    }

    public interface IChainFilter
    {
        double[]? Copy(ref int tail, int count, int stride, Ring<double>.Underflow underflowMode);
        int BufferSize { get; set; }
    }
}
