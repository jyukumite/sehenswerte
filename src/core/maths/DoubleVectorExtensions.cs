using Microsoft.VisualStudio.TestTools.UnitTesting;
using SehensWerte.Filters;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearRegression;

//fixme? consider replacing more of the more complex functions with MathNet.Numerics https://numerics.mathdotnet.com/Matrix

namespace SehensWerte.Maths
{
    public static class DoubleVectorExtensions
    {
        public static double[] Abs(this double[] lhs)
        {
            int length = lhs.Length;
            double[] array = new double[length];
            for (int loop = 0; loop < length; loop++)
            {
                array[loop] = Math.Abs(lhs[loop]);
            }
            return array;
        }

        public static double[] Add(this double[] lhs, double[] rhs)
        {
            if (lhs.Length != rhs.Length)
            {
                throw new ArithmeticException("Array lengths must be equal");
            }

            int length = lhs.Length;
            double[] array = new double[length];
            for (int loop = 0; loop < length; loop++)
            {
                array[loop] = lhs[loop] + rhs[loop];
            }
            return array;
        }

        public static double[] Add(this double[] lhs, double rhs)
        {
            int length = lhs.Length;
            double[] array = new double[length];
            for (int loop = 0; loop < length; loop++)
            {
                array[loop] = lhs[loop] + rhs;
            }
            return array;
        }

        public static double[] Complement(this double[] rhs, double lhs = 1.0)
        {
            int length = rhs.Length;
            double[] array = new double[length];
            for (int loop = 0; loop < length; loop++)
            {
                array[loop] = lhs - rhs[loop];
            }
            return array;
        }

        public static double[] Append(this double[] lhs, double[] rhs)
        {
            double[] array = new double[lhs.Length + rhs.Length];
            Array.Copy(lhs, 0, array, 0, lhs.Length);
            Array.Copy(rhs, 0, array, lhs.Length, rhs.Length);
            return array;
        }

        public static double[,] AsColumn(this double[] from)
        {
            int length = from.Length;
            double[,] result = new double[length, 1];
            for (int loop = 0; loop < length; loop++)
            {
                result[loop, 0] = from[loop];
            }
            return result;
        }

        public static double[,] AsRow(this double[] from)
        {
            int length = from.Length;
            double[,] result = new double[1, length];
            for (int loop = 0; loop < length; loop++)
            {
                result[0, loop] = from[loop];
            }
            return result;
        }

        public static List<Tuple<double, int, double>> CalculateHistogram(this double[] data, int numberOfBins)
        {
            int[] bins = new int[numberOfBins];
            double min = data.Min();
            double max = data.Max();
            List<Tuple<double, int, double>> list = new List<Tuple<double, int, double>>();
            if (data.Length > 1 && min != max)
            {
                double perBin = (max - min) / numberOfBins;
                foreach (double num in data)
                {
                    bins[(int)((numberOfBins - 1) * (num - min) / (max - min))]++;
                }
                for (int loop = 0; loop < numberOfBins; loop++)
                {
                    double binMiddle = min + perBin * loop + perBin / 2.0;
                    double fraction = bins[loop] / (double)data.Length;
                    list.Add(new Tuple<double, int, double>(binMiddle, bins[loop], fraction));
                }
            }
            return list;
        }

        public static double[] Ceil(this double[] lhs)
        {
            int length = lhs.Length;
            double[] result = new double[length];
            for (int loop = 0; loop < length; loop++)
            {
                result[loop] = Math.Ceiling(lhs[loop]);
            }
            return result;
        }

        public static double[] Copy(this double[] lhs)
        {
            return lhs.Copy(0, lhs.Length);
        }

        public static double[] Copy(this double[] lhs, int offset, int length, double fill = 0.0)  // superset of range/slice operators
        {
            return lhs.Copy(offset, length, fill, fill);
        }

        public static double[] Copy(this double[] lhs, int offset, int length, double leftFillValue, double rightFillValue)
        {
            int leftFill = ((offset < 0) ? (-offset) : 0);
            int rightFill = (length + offset > lhs.Length) ? (length - (lhs.Length - offset)) : 0;
            double[] result = new double[length];
            if (leftFillValue != 0.0)
            {
                for (int loop = 0; loop < leftFill; loop++)
                {
                    result[loop] = leftFillValue;
                }
            }
            if (rightFillValue != 0.0)
            {
                for (int loop = length - rightFill; loop < length; loop++)
                {
                    result[loop] = rightFillValue;
                }
            }
            Array.Copy(lhs, offset + leftFill, result, leftFill, length - leftFill - rightFill);
            return result;
        }

        public static double[] DegreesToRadians(this double[] lhs)
        {
            int length = lhs.Length;
            double[] result = new double[length];
            for (int loop = 0; loop < length; loop++)
            {
                result[loop] = lhs[loop].DegreesToRadians();
            }
            return result;
        }

        public static double[] Differentiated(this double[] lhs)
        {
            int length = lhs.Length;
            double[] result = new double[length];
            for (int loop = 1; loop < length; loop++)
            {
                result[loop] = lhs[loop] - lhs[loop - 1];
            }
            return result;
        }

        public static double[] Divide(this double[] lhs, double rhs)
        {
            int length = lhs.Length;
            double[] result = new double[length];
            for (int loop = 0; loop < length; loop++)
            {
                result[loop] = lhs[loop] / rhs;
            }
            return result;
        }

        public static double DotProduct(this double[] lhs, double[] rhs)
        {
            if (lhs.Length != rhs.Length)
            {
                throw new ArithmeticException("Array lengths must be equal");
            }
            int length = lhs.Length;
            double sum = 0.0;
            for (int loop = 0; loop < length; loop++)
            {
                sum += lhs[loop] * rhs[loop];
            }
            return sum;
        }

        public static double[] ElementDivide(this double[] lhs, double[] rhs)
        {
            if (lhs.Length != rhs.Length)
            {
                throw new ArithmeticException("Array lengths must be equal");
            }
            int length = lhs.Length;
            double[] result = new double[length];
            for (int loop = 0; loop < length; loop++)
            {
                result[loop] = lhs[loop] / rhs[loop];
            }
            return result;
        }

        public static double[] ElementPow(this double[] lhs, double rhs)
        {
            int length = lhs.Length;
            double[] result = new double[length];
            if (rhs == 2.0)
            {
                for (int loop = 0; loop < length; loop++)
                {
                    result[loop] = lhs[loop] * lhs[loop];
                }
            }
            else
            {
                for (int loop = 0; loop < length; loop++)
                {
                    result[loop] = Math.Pow(lhs[loop], rhs);
                }
            }
            return result;
        }

        public static double[] ElementProduct(this double[] lhs, double rhs)
        {
            int length = lhs.Length;
            double[] result = new double[length];
            for (int loop = 0; loop < length; loop++)
            {
                result[loop] = lhs[loop] * rhs;
            }
            return result;
        }

        public static double[] ElementProduct(this double[] lhs, double[] rhs)
        {
            if (lhs.Length != rhs.Length)
            {
                throw new ArithmeticException("Array lengths must be equal");
            }
            int length = lhs.Length;
            double[] result = new double[length];
            for (int loop = 0; loop < length; loop++)
            {
                result[loop] = lhs[loop] * rhs[loop];
            }
            return result;
        }

        public static double[] Floor(this double[] lhs)
        {
            int length = lhs.Length;
            double[] result = new double[length];
            for (int loop = 0; loop < length; loop++)
            {
                result[loop] = Math.Floor(lhs[loop]);
            }
            return result;
        }

        public static double[] Integrated(this double[] lhs)
        {
            int length = lhs.Length;
            double[] result = new double[length];
            if (length != 0)
            {
                result[0] = lhs[0];
                for (int loop = 1; loop < length; loop++)
                {
                    result[loop] = lhs[loop] + result[loop - 1];
                }
            }
            return result;
        }

        public static bool IsEqualTo(this double[] lhs, double[] rhs, double tolerance = 0)
        {
            if (lhs.Length != rhs.Length)
            {
                return false;
            }
            for (int loop = 0; loop < lhs.Length; loop++)
            {
                if (Math.Abs(lhs[loop] - rhs[loop]) > tolerance)
                {
                    return false;
                }
            }
            return true;
        }

        public static double Magnitude(this double[] lhs)
        {
            return Math.Sqrt(lhs.SumSquares());
        }

        public static double Max(this double[] lhs)
        {
            double max = double.NegativeInfinity;
            foreach (var num in lhs)
            {
                if (num > max)
                {
                    max = num;
                }
            }
            return max;
        }

        public static double Mean(this double[] lhs)
        {
            return lhs.Sum() / lhs.Length;
        }

        public static double Median(this double[] lhs)
        {
            if (lhs.Length == 0) return 0.0;
            double[] result = lhs.Sorted();
            int num = lhs.Length / 2;
            return lhs.Length % 2 == 0 ? (result[num] + result[num - 1]) / 2.0 : result[num];
        }

        public static double MeanAbsoluteDeviation(this double[] lhs)
        {
            return lhs.Subtract(lhs.Mean()).Abs().Mean();
        }

        public static double MedianAbsoluteDeviation(this double[] lhs)
        {
            return lhs.Subtract(lhs.Median()).Abs().Median();
        }

        public static double[] Negated(this double[] lhs)
        {
            int length = lhs.Length;
            double[] result = new double[length];
            for (int loop = 0; loop < length; loop++)
            {
                result[loop] = -lhs[loop];
            }
            return result;
        }

        public static double[] Normalised(this double[] lhs)
        {
            return lhs.Divide(lhs.Magnitude());
        }

        // [0]x^2 + [1]x^1 + [2]x^0 ...
        public static double[] PolyFit(this double[] y, int order)
        {
            return Fit.Polynomial(Generate.LinearRange(0, 1, y.Length - 1), y, order).Reversed();
        }

        // [0]x^2 + [1]x^1 + [2]x^0 ...
        public static double[] PolyFit(this double[] y, double[] x, int order)
        {
            // note: x might need to be normalised to [0..1] by caller if it is excessively large
            return Fit.Polynomial(x, y, order).Reversed();
            // double[,] matrix = new double[order + 1, order + 2];
            // for (int i = 0; i <= order; i++)
            // {
            //     matrix[i, order + 1] = 0.0;
            //     for (int j = 0; j < x.Length; j++)
            //     {
            //         matrix[i, order + 1] -= Math.Pow(x[j], i) * y[j];
            //     }
            //     for (int j = 0; j <= order; j++)
            //     {
            //         matrix[i, j] = 0.0;
            //         for (int k = 0; k < x.Length; k++)
            //         {
            //             matrix[i, j] -= Math.Pow(x[k], j + i);
            //         }
            //     }
            // }
            // return matrix.GaussianElimination().Reversed();
        }

        // [0]x^2 + [1]x^1 + [2]x^0 ...
        public static double PolyVal(this double[] coeff, double x)
        {
            double product = 1.0;
            double result = 0;
            foreach (double c in coeff.Reversed())
            {
                result += c * product;
                product *= x;
            }
            return result;
        }

        public static double[] PolyFilter(this double[] lhs, int order) //fixme: unittest
        {
            // like Savitzky Golay but without the walking window
            var indices = Enumerable.Range(0, lhs.Length).Select(x => (double)x).ToArray();
            var polyfit = lhs.PolyFit(order);
            return indices.Select(x => polyfit.PolyVal(x)).ToArray();
        }

        public static double[,] Product(this double[] colData, double[] rowData)
        {
            int cols = colData.Length;
            int rows = rowData.Length;
            double[,] result = new double[cols, rows];
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    result[col, row] = rowData[row] * colData[col];
                }
            }
            return result;
        }

        public static double[] Quantize(this double[] lhs, double low, double high, int levels)
        {
            double[] result = new double[lhs.Length];
            double range = high - low;
            int divisions = levels - 1;
            for (int i = 0; i < result.Length; i++)
            {
                int rounded = (int)Math.Round((lhs[i] - low) * (double)divisions / range, MidpointRounding.AwayFromZero);
                if (rounded < 0)
                {
                    rounded = 0;
                }
                if (rounded > divisions)
                {
                    rounded = divisions;
                }
                result[i] = (double)rounded * range / (double)divisions + low;
            }
            return result;
        }

        public static double[] Quantize(this double[] lhs, double scale)
        {
            double[] result = new double[lhs.Length];
            for (int loop = 0; loop < result.Length; loop++)
            {
                double value = lhs[loop] * scale;
                result[loop] = (int)Math.Round(value, MidpointRounding.AwayFromZero);
            }
            return result;
        }

        public static double[] RadiansToDegrees(this double[] lhs)
        {
            int length = lhs.Length;
            double[] result = new double[length];
            for (int loop = 0; loop < length; loop++)
            {
                result[loop] = lhs[loop].RadiansToDegrees();
            }
            return result;
        }

        public static IEnumerable<double> Range(double start, int count, double step = 1)
        {
            for (int loop = 0; loop < count; loop++)
            {
                yield return start + loop * step;
            }
        }

        public static double[] Reciprocal(this double[] lhs)
        {
            int length = lhs.Length;
            double[] result = new double[length];
            for (int loop = 0; loop < length; loop++)
            {
                result[loop] = 1.0 / lhs[loop];
            }
            return result;
        }

        public static double[] Reflect(this double[] lhs)
        {
            // make the second half exactly the reflection of the first half
            int length = lhs.Length;
            int mid = (length + length % 2) / 2;
            double[] result = new double[length];
            for (int loop = 0; loop <= mid; loop++)
            {
                result[loop] = lhs[loop];
            }
            for (int loop = mid; loop < length; loop++)
            {
                result[loop] = result[length - loop - 1];
            }
            return result;
        }

        public static double[] Resample(this double[] lhs, int newLength)
        {
            return SampleRateChangeFilter.Resample(lhs, newLength) ?? new double[0];
        }

        public static double[] Rescale(this double[] lhs, double high)
        {
            double scale = Math.Max(-lhs.Min(), lhs.Max());
            return scale == 0 ? lhs : lhs.ElementProduct(high / scale);
        }

        public static double[] Rescale(this double[] lhs, double low, double high)
        {
            bool first = true;
            double lowest = 0.0;
            double highest = 0.0;
            int length = lhs.Length;
            for (int loop = 0; loop < length; loop++)
            {
                double sample = lhs[loop];
                if (sample < lowest || first)
                {
                    lowest = sample;
                }
                if (sample > highest || first)
                {
                    highest = sample;
                }
                first = false;
            }
            double range = highest - lowest;
            double scaledRange = high - low;

            double[] result = new double[lhs.Length];
            for (int loop = 0; loop < length; loop++)
            {
                double sample = lhs[loop];
                sample = (range == 0.0) ? 0.0 : ((sample - lowest) / range);
                result[loop] = sample * scaledRange + low;
            }
            return result;
        }

        public static double[] Reversed(this double[] lhs)
        {
            int length = lhs.Length;
            double[] result = new double[length];
            for (int loop = 0; loop < length; loop++)
            {
                result[length - loop - 1] = lhs[loop];
            }
            return result;
        }

        public static double Rms(this double[] lhs)
        {
            return Math.Sqrt(lhs.SumSquares() / (double)lhs.Length);
        }

        public static double[] RollingMax(this double[] lhs, int count) //fixme: unit test
        {
            int length = lhs.Length;
            double[] result = new double[length];
            for (int loop = 0; loop < length; loop++)
            {
                int left = Math.Max(0, loop - count / 2);
                int right = Math.Min(length - 1, loop + count / 2);
                result[loop] = lhs.Copy(left, right - left + 1).Max();
            }
            return result;
        }

        public static double[] RollingMean(this double[] lhs, int count)
        {
            int length = lhs.Length;
            double[] result = new double[length];
            for (int loop = 0; loop < length; loop++)
            {
                int left = Math.Max(0, loop - count / 2);
                int right = Math.Min(length - 1, loop + count / 2);
                result[loop] = lhs.Copy(left, right - left + 1).Mean();
            }
            return result;
        }

        public static double[] RollingRms(this double[] lhs, int count)
        {
            int length = lhs.Length;
            double[] result = new double[length];
            for (int loop = 0; loop < length; loop++)
            {
                int left = Math.Max(0, loop - count / 2);
                int right = Math.Min(length - 1, loop + count / 2);
                result[loop] = lhs.Copy(left, right - left + 1).Rms();
            }
            return result;
        }

        public static double[] RotateRight(this double[] lhs, int count)
        {
            int length = lhs.Length;
            int offset = count % length;
            double[] array = new double[lhs.Length];
            for (int loop = 0; loop < length; loop++)
            {
                array[loop] = lhs[(offset + loop + length) % length];
            }
            return array;
        }

        public static double[] Round(this double[] lhs, int decimals, MidpointRounding mode)
        {
            int length = lhs.Length;
            double[] result = new double[length];
            for (int loop = 0; loop < length; loop++)
            {
                result[loop] = Math.Round(lhs[loop], decimals, mode);
            }
            return result;
        }

        public static double[] Sorted(this double[] lhs)
        {
            double[] result = lhs.Copy();
            Array.Sort(result);
            return result;
        }

        public static double[] Sqrt(this double[] lhs)
        {
            int length = lhs.Length;
            double[] result = new double[length];
            for (int loop = 0; loop < length; loop++)
            {
                result[loop] = Math.Sqrt(lhs[loop]);
            }
            return result;
        }

        public static double Stdev(this double[] lhs)
        {
            double mean = lhs.Sum() / (double)lhs.Length;
            double meanSq = lhs.SumSquares() / (double)lhs.Length - mean * mean;
            return (meanSq <= 0.0) ? 0.0 : Math.Sqrt(meanSq);
        }

        public static double[] Subtract(this double[] lhs, double rhs)
        {
            int length = lhs.Length;
            double[] result = new double[length];
            for (int loop = 0; loop < length; loop++)
            {
                result[loop] = lhs[loop] - rhs;
            }
            return result;
        }

        public static double[] Subtract(this double[] lhs, double[] rhs)
        {
            if (lhs.Length != rhs.Length)
            {
                throw new ArithmeticException("Array lengths must be equal");
            }
            int length = lhs.Length;
            double[] result = new double[length];
            for (int loop = 0; loop < length; loop++)
            {
                result[loop] = lhs[loop] - rhs[loop];
            }
            return result;
        }

        public static double Sum(this double[] lhs)
        {
            double sum = 0.0;
            foreach (var num in lhs)
            {
                sum += num;
            }
            return sum;
        }

        public static double Product(this double[] lhs)
        {
            double sum = lhs.Count() == 0 ? 0 : 1.0;
            foreach (var num in lhs)
            {
                sum *= num;
            }
            return sum;
        }

        public static double SumSquares(this double[] lhs)
        {
            double sum = 0.0;
            foreach (var num in lhs)
            {
                sum += num * num;
            }
            return sum;
        }

        public static double[] Trunc(this double[] lhs)
        {
            int length = lhs.Length;
            double[] result = new double[length];
            for (int loop = 0; loop < length; loop++)
            {
                result[loop] = Math.Truncate(lhs[loop]);
            }
            return result;
        }

        public static double[] UnwrapAngle(this double[] lhs, double step, double tolerance)
        {
            int length = lhs.Length;
            double[] result = new double[length];
            double offset = 0.0;
            double last = 0.0;
            for (int loop = 0; loop < length; loop++)
            {
                double val = lhs[loop];
                double delta = last - val;
                if (loop != 0 && Math.Abs(delta) > tolerance)
                {
                    offset += ((delta < 0.0) ? (0.0 - step) : step);
                }
                result[loop] = val + offset;
                last = val;
            }
            return result;
        }

        public static double[] UnwrapDegrees(this double[] lhs)
        {
            return lhs.UnwrapAngle(360.0, 180.0);
        }

        public static double[] UnwrapRadians(this double[] lhs)
        {
            return lhs.UnwrapAngle(Math.PI * 2.0, Math.PI);
        }

        public static double Variance(this double[] lhs)
        {
            double stdev = lhs.Stdev();
            return stdev * stdev;
        }

        public static double[] WrapAngle(this double[] lhs, double maxFromZero)
        {
            int length = lhs.Length;
            double[] result = new double[length];
            for (int loop = 0; loop < length; loop++)
            {
                result[loop] = lhs[loop].WrapAngle(maxFromZero);
            }
            return result;
        }

        public static double[] WrapDegrees(this double[] lhs)
        {
            return lhs.WrapAngle(180.0);
        }

        public static double[] WrapRadians(this double[] lhs)
        {
            return lhs.WrapAngle(Math.PI);
        }

        public static double[,] RotationMatrix(this double[] rotationAxis3, double clockwiseRadians3)
        {
            double[] xyz = rotationAxis3.Normalised();
            double x = xyz[0];
            double y = xyz[1];
            double z = xyz[2];
            double x2 = x * x;
            double y2 = y * y;
            double z2 = z * z;
            double c = Math.Cos(clockwiseRadians3);
            double s = Math.Sin(clockwiseRadians3);
            return new double[,]
            {
                {
                    x2 + (1.0 - x2) * c,
                    x * y * (1.0 - c) - z * s,
                    x * y * (1.0 - c) + y * s
                },
                {
                    x * y * (1.0 - c) + z * s,
                    y2 + (1.0 - y2) * c,
                    y * z * (1.0 - c) - x * s
                },
                {
                    x * z * (1.0 - c) - y * s,
                    y * z * (1.0 - c) + x * s,
                    z2 + (1.0 - z2) * c
                }
            };
        }

        public static double[] RotateAroundBy(this double[] vector, double[] rotationAxis, double clockwiseRadians)
        {
            return RotationMatrix(rotationAxis, clockwiseRadians).Product(vector);
        }

        public static double[] RotateAroundBy(this double[] vector, double[] clockwiseRadians)
        {
            double[,] unit = new double[3, 3].Eye();
            double[,] matrix = RotationMatrix(unit.Row(0), clockwiseRadians[0])
                .Product(RotationMatrix(unit.Row(1), clockwiseRadians[1]))
                .Product(RotationMatrix(unit.Row(2), clockwiseRadians[2]));
            return matrix.Product(vector);
        }

        public static double[] CrossProduct(this double[] vector, double[] crossWith3)
        {
            double x = vector[0];
            double y = vector[1];
            double z = vector[2];
            double cx = crossWith3[0];
            double cy = crossWith3[1];
            double cz = crossWith3[2];
            return new double[] { y * cz - z * cy, z * cx - x * cz, x * cy - y * cx };
        }

        public static double RadiansBetween(this double[] lhs, double[] rhs)
        {
            return Math.Acos(lhs.DotProduct(rhs) / (lhs.Magnitude() * rhs.Magnitude()));
        }

        public static double DegreesBetween(this double[] lhs, double[] rhs)
        {
            return Math.Acos(lhs.DotProduct(rhs) / (lhs.Magnitude() * rhs.Magnitude())) * 180.0 / Math.PI;
        }

        public static double[,] QuaternionToRotationMatrix(this double[] xyzw)
        {
            double qx = xyzw[0];
            double qy = xyzw[1];
            double qz = xyzw[2];
            double qw = xyzw[3];
            double[,] r = new double[3, 3];
            r[0, 0] = 1 - 2 * qy * qy - 2 * qz * qz;
            r[0, 1] = 2 * qx * qy - 2 * qz * qw;
            r[0, 2] = 2 * qx * qz + 2 * qy * qw;
            r[1, 0] = 2 * qx * qy + 2 * qz * qw;
            r[1, 1] = 1 - 2 * qx * qx - 2 * qz * qz;
            r[1, 2] = 2 * qy * qz - 2 * qx * qw;
            r[2, 0] = 2 * qx * qz - 2 * qy * qw;
            r[2, 1] = 2 * qy * qz + 2 * qx * qw;
            r[2, 2] = 1 - 2 * qx * qx - 2 * qy * qy;

            return r;
        }

        public static double[] QuaternionToSensorAcceleration(this double[] quaternion)
        {
            return QuaternionToRotationMatrix(quaternion).Transpose().Product(new double[] { 0, 0, 1 });
        }

        public static double[] QuaternionToSensorAccelerationQuick(this double[] q)
        {
            // drop the redundant operations of QuaternionToSensorAcceleration()
            var qx = q[0];
            var qy = q[1];
            var qz = q[2];
            var qw = q[3];
            var x = 2 * ((qx * qz) - (qw * qy));
            var y = 2 * ((qw * qx) + (qy * qz));
            var z = (qw * qw) - (qx * qx) - (qy * qy) + (qz * qz);
            return new double[] { x, y, z };
        }
    }

    [TestClass]
    public class VectorTests
    {
        [TestMethod]
        public void TestIsEqualTo()
        {
            double[] lhs = { 1.0, 2.0, 3.0 };
            double[] lhs2 = { 1.1, 2.1, 3.1 };
            double[] rhs = { 7.0, 8.0 };
            Assert.IsFalse(lhs.IsEqualTo(rhs));
            Assert.IsTrue(lhs.IsEqualTo(lhs));
            Assert.IsFalse(lhs.IsEqualTo(lhs2));
            Assert.IsTrue(lhs.IsEqualTo(lhs2, tolerance: 1));
        }

        [TestMethod]
        public void TestLhsMulti()
        {
            Assert.IsTrue(new double[] { -1, 3, -5 }.Abs().IsEqualTo(new double[] { 1, 3, 5 }));
            Assert.IsTrue(new double[] { -1, 3.5, -5.7 }.Ceil().IsEqualTo(new double[] { -1, 4, -5 }));
            Assert.IsTrue(new double[] { 90, 180, 360 }.DegreesToRadians().IsEqualTo(new double[] { Math.PI / 2, Math.PI, Math.PI * 2 }, tolerance: 0.01));
            Assert.IsTrue(new double[] { Math.PI / 2, Math.PI, Math.PI * 2 }.RadiansToDegrees().IsEqualTo(new double[] { 90, 180, 360 }, tolerance: 0.01));
            Assert.IsTrue(new double[] { 1, 3, 5, 5 }.Differentiated().IsEqualTo(new double[] { 0, 2, 2, 0 }));
            Assert.IsTrue(new double[] { -1, 3.5, -5.7 }.Floor().IsEqualTo(new double[] { -1, 3, -6 }));
            Assert.IsTrue(new double[] { 1, 3, 5, 5 }.Integrated().IsEqualTo(new double[] { 1, 4, 9, 14 }));
            Assert.IsTrue(new double[] { 1, 3, 5, 5 }.Negated().IsEqualTo(new double[] { -1, -3, -5, -5 }));
            Assert.IsTrue(new double[] { 1, 3, -5, 5 }.Normalised().IsEqualTo(new double[] { 0.129, 0.387, -0.645, 0.645 }, tolerance: 0.01));
            Assert.IsTrue(new double[] { 1, 3, -5, 5 }.Reciprocal().IsEqualTo(new double[] { 1.0, 1 / 3.0, -1 / 5.0, 1 / 5.0 }, tolerance: 0.01));
            Assert.IsTrue(new double[] { 1, 3, -5, 5 }.Reflect().IsEqualTo(new double[] { 1, 3, 3, 1 }));
            Assert.IsTrue(new double[] { 1, 3, 4, -5, 5 }.Reflect().IsEqualTo(new double[] { 1, 3, 4, 3, 1 }));
            Assert.IsTrue(new double[] { 1, 3, 4, -5, 5 }.Reversed().IsEqualTo(new double[] { 5, -5, 4, 3, 1 }));
            Assert.IsTrue(new double[] { 1, 3, 4, -5, 5 }.Sorted().IsEqualTo(new double[] { -5, 1, 3, 4, 5 }));
            Assert.IsTrue(new double[] { 1, 4, 16 }.Sqrt().IsEqualTo(new double[] { 1, 2, 4 }));
            Assert.IsTrue(new double[] { 5.1, 3.9, -4.2, -5.9, 0 }.Trunc().IsEqualTo(new double[] { 5, 3, -4, -5, 0 }));
            Assert.IsTrue(new double[] { 50, 100, 150, -160, -120, -160, 150 }.UnwrapDegrees().IsEqualTo(new double[] { 50, 100, 150, 200, 240, 200, 150 }));
            Assert.IsTrue(new double[] { 1, 2, 3, -3, -2, -1, 3 }.UnwrapRadians().IsEqualTo(new double[] { 1, 2, 3, Math.PI * 2 - 3, Math.PI * 2 - 2, Math.PI * 2 - 1, 3 }));
            Assert.IsTrue(new double[] { 100, 200, 300, 400, 500, 600, 700, -300 }.WrapDegrees().IsEqualTo(new double[] { 100, 200 - 360, 300 - 360, 400 - 360, 500 - 360, 600 - 720, 700 - 720, -300 + 360 }));
            Assert.IsTrue(new double[] { 1, 2, 3, 4, 5, 6, 7, -4 }.WrapRadians().IsEqualTo(new double[] { 1, 2, 3, 4 - Math.PI * 2, 5 - Math.PI * 2, 6 - Math.PI * 2, 7 - Math.PI * 2, -4 + Math.PI * 2 }));
            Assert.IsTrue(new double[] { 1.234, 2.345, 4.507, 4.912, -1.234, -2.345, -4.507, -4.912 }.Round(0, MidpointRounding.AwayFromZero).IsEqualTo(new double[] { 1, 2, 5, 5, -1, -2, -5, -5 }));
            //public static double[] UnwrapAngle(this double[] lhs, double step, double tolerance) (tested by WrapRadians, WrapDegrees)
            //public static double[] WrapAngle(this double[] lhs, double maxFromZero) (tested by WrapRadians, WrapDegrees)
        }


        [TestMethod]
        public void TestRotateRight()
        {
            Assert.IsTrue(new double[] { 1, 4, 16 }.RotateRight(0).IsEqualTo(new double[] { 1, 4, 16 }));
            Assert.IsTrue(new double[] { 1, 4, 16 }.RotateRight(1).IsEqualTo(new double[] { 4, 16, 1 }));
            Assert.IsTrue(new double[] { 1, 4, 16 }.RotateRight(2).IsEqualTo(new double[] { 16, 1, 4 }));
            Assert.IsTrue(new double[] { 1, 4, 16 }.RotateRight(-1).IsEqualTo(new double[] { 16, 1, 4 }));
            Assert.IsTrue(new double[] { 1, 4, 16 }.RotateRight(-2).IsEqualTo(new double[] { 4, 16, 1 }));
            Assert.IsTrue(new double[] { 1, 4, 16 }.RotateRight(-8).IsEqualTo(new double[] { 4, 16, 1 }));
        }

        [TestMethod]
        public void TestLhsOne()
        {
            Assert.IsTrue(new double[] { 1, 3, 5, 5 }.Max() == 5);
            Assert.IsTrue(new double[] { 1, 3, 5, 5 }.Mean() == 3.5);
            Assert.IsTrue(new double[] { 5, 3, 1, 5 }.Median() == 4);
            Assert.IsTrue(new double[] { 5, 3, 1, 5 }.MeanAbsoluteDeviation() == 1.5);
            Assert.IsTrue(new double[] { 5, 3, 1, 5 }.MedianAbsoluteDeviation() == 1);
            Assert.IsTrue(Math.Abs(new double[] { 5, 3, 1, 5 }.Rms() - 3.873) < 0.001);
            Assert.IsTrue(Math.Abs(new double[] { 5, 3, 1, 5 }.Stdev() - 1.658) < 0.001);
            Assert.IsTrue(new double[] { 5, 3, 1, 5 }.Sum() == 14);
            Assert.IsTrue(new double[] { 5, 3, 1, 5 }.Product() == 5 * 3 * 1 * 5);
            Assert.IsTrue(new double[] { 5 }.Product() == 5);
            Assert.IsTrue(new double[] { }.Product() == 0);

            Assert.IsTrue(new double[] { 5, 3, 1, 5 }.SumSquares() == 25 + 9 + 1 + 25);
            Assert.IsTrue(Math.Abs(new double[] { 0.129, 0.387, -0.645, 0.645 }.Magnitude() - 1) < 0.01);
            Assert.IsTrue(Math.Abs(new double[] { 5, 3, 1, 5 }.Variance() - 2.75) < 0.001);
        }

        [TestMethod]
        public void TestMatrix()
        {
            Assert.IsTrue(new double[] { 1, 2, 3 }.AsColumn().IsEqualTo(new double[,] { { 1 }, { 2 }, { 3 } }));
            Assert.IsTrue(new double[] { 1, 2, 3 }.AsRow().IsEqualTo(new double[,] { { 1, 2, 3 } }));
        }

        [TestMethod]
        public void TestLhsRhs()
        {
            Assert.IsTrue(new double[] { 1, 2, 3 }.Add(new double[] { 2, 3, 4 }).IsEqualTo(new double[] { 3, 5, 7 }));
            Assert.IsTrue(new double[] { 1, 2, 3 }.Add(2).IsEqualTo(new double[] { 3, 4, 5 }));
            Assert.IsTrue(new double[] { 1, 2, 3 }.Complement().IsEqualTo(new double[] { 0, -1, -2 }));
            Assert.IsTrue(new double[] { 1, 2, 3 }.Append(new double[] { 2, 3, 4 }).IsEqualTo(new double[] { 1, 2, 3, 2, 3, 4 }));
            Assert.IsTrue(new double[] { 1, 2, 3 }.Divide(2).IsEqualTo(new double[] { 0.5, 1, 1.5 }));
            Assert.IsTrue(new double[] { 1, 2, 3 }.DotProduct(new double[] { 2, 3, 4 }) == 2 + 2 * 3 + 3 * 4);
            Assert.IsTrue(new double[] { 1, 2, 3 }.ElementDivide(new double[] { 2, 3, 4 }).IsEqualTo(new double[] { 1 / 2.0, 2 / 3.0, 3 / 4.0 }));
            Assert.IsTrue(new double[] { 1, 2, 3 }.ElementPow(2).IsEqualTo(new double[] { 1, 4, 9 }));
            Assert.IsTrue(new double[] { 1, 2, 3 }.ElementPow(3).IsEqualTo(new double[] { 1, 8, 27 }));
            Assert.IsTrue(new double[] { 1, 2, 3 }.ElementProduct(2).IsEqualTo(new double[] { 2, 4, 6 }));
            Assert.IsTrue(new double[] { 1, 2, 3 }.ElementProduct(new double[] { 2, 3, 4 }).IsEqualTo(new double[] { 2, 6, 12 }));
            Assert.IsTrue(new double[] { 1, 2, 3 }.Subtract(new double[] { -2, -3, -4 }).IsEqualTo(new double[] { 3, 5, 7 }));
            Assert.IsTrue(new double[] { 1, 2, 3 }.Subtract(-2).IsEqualTo(new double[] { 3, 4, 5 }));
        }

        [TestMethod]
        public void TestHistogram()
        {
            var result = new double[] { 1, 1, 3, 3, 3, 5, 4, 9, 10, 10 }.CalculateHistogram(3);
            Assert.IsTrue(result.Select(x => x.Item1).ToArray().IsEqualTo(new double[] { 2.5, 5.5, 8.5 }));
            Assert.IsTrue(result.Select(x => (double)x.Item2).ToArray().IsEqualTo(new double[] { 7, 1, 2 }));
            Assert.IsTrue(result.Select(x => x.Item3).ToArray().IsEqualTo(new double[] { 0.7, 0.1, 0.2 }, 0.001));
            //public static List<Tuple<double, int, double>> CalculateHistogram(this double[] data, int numberOfBins)
        }

        [TestMethod]
        public void TestCopy()
        {
            Assert.IsTrue(new double[] { 1, 2, 3 }.Copy().IsEqualTo(new double[] { 1, 2, 3 }));
            Assert.IsTrue(new double[] { 1, 2, 3 }.Copy(1, 1).IsEqualTo(new double[] { 2 }));
            Assert.IsTrue(new double[] { 1, 2, 3 }.Copy(-1, 5).IsEqualTo(new double[] { 0, 1, 2, 3, 0 }));
            Assert.IsTrue(new double[] { 1, 2, 3 }.Copy(-1, 5, 4).IsEqualTo(new double[] { 4, 1, 2, 3, 4 }));
            Assert.IsTrue(new double[] { 1, 2, 3 }.Copy(-1, 5, -1, 6).IsEqualTo(new double[] { -1, 1, 2, 3, 6 }));
            Assert.IsTrue(new double[] { 1, 2, 3 }.Copy(1, 3, -1, 6).IsEqualTo(new double[] { 2, 3, 6 }));
            Assert.IsTrue(new double[] { 1, 2, 3 }.Copy(-1, 3, -1, 6).IsEqualTo(new double[] { -1, 1, 2 }));
        }

        [TestMethod]
        public void TestPoly()
        {
            double[] orig = new double[] { 1, 3, 4, 4, 5 };
            double[] poly = orig.PolyFit(3);
            Assert.IsTrue(poly.IsEqualTo(new double[] { 0.1667, -1.2143, 3.1905, 0.9714 }, 0.001));
            double[] re = Enumerable.Range(0, 5).Select(x => poly.PolyVal(x)).ToArray();
            Assert.IsTrue(re.IsEqualTo(orig, 0.2));

            double[] origY = new double[] { 1, 3, 4, 4, 5 };
            double[] origX = new double[] { 0, 2, 4, 6, 8 };
            poly = origY.PolyFit(origX, 3);
            re = origX.Select(x => poly.PolyVal(x)).ToArray();
            Assert.IsTrue(re.IsEqualTo(origY, 0.2));

            origX = new double[] { 0, 10, 20, 30, 40, 50 };
            origY = origX.Select(x => 0.05 * x * x - 2 * x + 10).ToArray();
            poly = origY.PolyFit(origX, 3);
            re = origX.Select(x => poly.PolyVal(x)).ToArray();
            Assert.IsTrue(re.IsEqualTo(origY, 0.2));
        }

        [TestMethod]
        public void TestProduct()
        {
            Assert.IsTrue(new double[] { 1, 2, 3 }.Product(new double[] { 2, 3 }).IsEqualTo(new double[,] { { 2, 3 }, { 4, 6 }, { 6, 9 } }));
        }

        [TestMethod]
        public void TestQuantize()
        {
            Assert.IsTrue(new double[] { 1, 2, 3 }.Quantize(1.2, 3.2, 3).IsEqualTo(new double[] { 1.2, 2.2, 3.2 }, 0.001));
            Assert.IsTrue(new double[] { 1.1, 2.1, 3.3 }.Quantize(3).IsEqualTo(new double[] { 3, 6, 10 }, 0.001));
        }

        [TestMethod]
        public void TestResample()
        {
            Assert.IsTrue(new double[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }.Resample(5).IsEqualTo(new double[] { 1, 3, 5, 7, 9 }, 0.001));
            Assert.IsTrue(new double[] { 1, 3, 5, 7, 9 }.Resample(10).IsEqualTo(new double[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 9 }, 0.001));
        }

        [TestMethod]
        public void TestRescale()
        {
            Assert.IsTrue(new double[] { 1, 2, 3, 4 }.Rescale(8).IsEqualTo(new double[] { 2, 4, 6, 8 }, 0.001));
            Assert.IsTrue(new double[] { 1, 2, 3, 4 }.Rescale(4, 7).IsEqualTo(new double[] { 4, 5, 6, 7 }, 0.001));
        }

        [TestMethod]
        public void TestRolling()
        {
            Assert.IsTrue(new double[] { 1, 2, 3, 4, 5, 6, 7 }.RollingMean(2).IsEqualTo(new double[] { 1.5, 2, 3, 4, 5, 6, 6.5 }, 0.001)); // 2 -> n[-1],n[0],n[+1]
            Assert.IsTrue(new double[] { 1, -1, 1, -1, 2, -2, 2 }.RollingRms(2).IsEqualTo(new double[] { 1, 1, 1, 1.4142, 1.7321, 2, 2 }, 0.001));
        }

        [TestMethod]
        public void TestRotation()
        {
            Action<double, double> test = (a, b) => Assert.IsTrue(Math.Abs(a - b) < 0.001);
            Action<double[], double[]> test2 = (a, b) => { for (int loop = 0; loop < a.Length; loop++) test(a[loop], b[loop]); };
            Action<double[,], double[,]> test3 = (a, b) => { for (int loop = 0; loop < a.RowCount(); loop++) test2(a.Row(loop), b.Row(loop)); };

            double[,] rot = new double[] { 0, 0, 1 }.RotationMatrix(Math.PI / 4);
            test3(rot, new double[,] { { 0.7071, -0.7071, 0 }, { 0.7071, 0.7071, 0 }, { 0, 0, 1 } });
            double[] vec1 = new double[] { 0, 0, 1 }.RotateAroundBy(new double[] { 0, 1, 0 }, Math.PI / 4);
            test2(vec1, new double[] { 0.7071, 0, 0.7071 });
            double[] vec2 = new double[] { 0, 0, 1 }.RotateAroundBy(new double[] { 0, Math.PI / 4, 0 });
            test2(vec2, new double[] { 0.7071, 0, 0.7071 });
            double[] vec3 = new double[] { 1, 0, 0 }.CrossProduct(new double[] { 0, 1, 0 });
            test2(vec3, new double[] { 0, 0, 1 });
            double rb = new double[] { 1, 0, 0 }.RadiansBetween(new double[] { 0, 1, 0 });
            test(rb, Math.PI / 2);
            double rc = new double[] { 1, 0, 0 }.DegreesBetween(new double[] { 0, 1, 0 });
            test(rc, 90);

            Action<double[], double[]> testq = (q, a) =>
            {
                var ctsa = q.QuaternionToSensorAcceleration();
                var qtn = q.QuaternionToSensorAccelerationQuick();
                test2(ctsa, a);
                test2(ctsa, qtn);
            };
            testq(new double[] { 0, 0, 0, 1 }, new double[] { 0, 0, 1 });
            testq(new double[] { 1, 0, 0, 0 }, new double[] { 0, 0, -1 });
            testq(new double[] { 0.7071, 0, 0.7071, 0 }, new double[] { 1, 0, 0 });
            testq(new double[] { -0.0041504429087456142, -0.026001304104788698, -0.099366486109380286, 0.99470247182098925 }, new double[] { 0.05255, -0.00309, 0.99861 });
        }

        [TestMethod]
        public void TestRange()
        {
            var ranges = new (IEnumerable<double>, double[] result)[] {
                (DoubleVectorExtensions.Range(0, 4, 3), new double[]{0,3,6,9 }),
                (DoubleVectorExtensions.Range(4, 7, -1), new double[]{4,3,2,1,0,-1,-2 }),
                (DoubleVectorExtensions.Range(2, 3), new double[]{2,3,4 }),
                (DoubleVectorExtensions.Range(1, 0, 2), new double[]{ })
            };
            foreach (var v in ranges)
            {
                CollectionAssert.AreEqual(v.Item1.ToArray(), v.Item2);
            }
        }
    }
}

