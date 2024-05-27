using Microsoft.VisualStudio.TestTools.UnitTesting;

//fixme? consider replacing some of the more complex functions with mat.net numerics https://numerics.mathdotnet.com/Matrix

namespace SehensWerte.Maths
{
    public static class DoubleMatrixExtensions
    {
        public const int ROW_DIMENSION = 0;
        public const int COLUMN_DIMENSION = 1;
        public static int ColumnCount(this double[,] lhs) { return lhs.GetLength(COLUMN_DIMENSION); }
        public static int RowCount(this double[,] lhs) { return lhs.GetLength(ROW_DIMENSION); }

        public static double[,] Add(this double[,] lhs, double rhs)
        {
            int rows = lhs.GetLength(ROW_DIMENSION);
            int cols = lhs.GetLength(COLUMN_DIMENSION);
            double[,] result = new double[rows, cols];
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    result[row, col] = lhs[row, col] + rhs;
                }
            }
            return result;
        }

        public static double[,] Add(this double[,] lhs, double[,] rhs)
        {
            if (lhs.GetLength(ROW_DIMENSION) != rhs.GetLength(ROW_DIMENSION)
                || lhs.GetLength(COLUMN_DIMENSION) != rhs.GetLength(COLUMN_DIMENSION))
            {
                throw new ArithmeticException("Array lengths must be equal");
            }
            int rows = lhs.GetLength(ROW_DIMENSION);
            int cols = lhs.GetLength(COLUMN_DIMENSION);
            double[,] result = new double[rows, cols];
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    result[row, col] = lhs[row, col] + rhs[row, col];
                }
            }
            return result;
        }

        public static double[,] Divide(this double[,] lhs, double rhs)
        {
            int rows = lhs.GetLength(ROW_DIMENSION);
            int cols = lhs.GetLength(COLUMN_DIMENSION);
            double[,] result = new double[rows, cols];
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    result[row, col] = lhs[row, col] / rhs;
                }
            }
            return result;
        }

        public static double[,] ElementProduct(this double[,] lhs, double rhs)
        {
            int rows = lhs.GetLength(ROW_DIMENSION);
            int cols = lhs.GetLength(COLUMN_DIMENSION);
            double[,] result = new double[rows, cols];
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    result[row, col] = lhs[row, col] * rhs;
                }
            }
            return result;
        }

        public static double[,] ElementProduct(this double[,] lhs, double[,] rhs)
        {
            if (lhs.GetLength(ROW_DIMENSION) != rhs.GetLength(ROW_DIMENSION)
                || lhs.GetLength(COLUMN_DIMENSION) != rhs.GetLength(COLUMN_DIMENSION))
            {
                throw new ArithmeticException("Array lengths must be equal");
            }
            int rows = lhs.GetLength(ROW_DIMENSION);
            int cols = lhs.GetLength(COLUMN_DIMENSION);
            double[,] result = new double[rows, cols];
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    result[row, col] = lhs[row, col] * rhs[row, col];
                }
            }
            return result;
        }

        public static double[,] Eye(this double[,] lhs)
        {
            if (lhs.GetLength(ROW_DIMENSION) != lhs.GetLength(COLUMN_DIMENSION))
            {
                throw new ArithmeticException("Array lengths must be equal");
            }
            int length = lhs.GetLength(0); //a bit wasteful, perhaps
            double[,] array = new double[length, length];
            for (int loop = 0; loop < length; loop++)
            {
                array[loop, loop] = 1;
            }
            return array;
        }

        public static double[] GaussianElimination(this double[,] coeffs)
        {
            int rows = coeffs.GetLength(ROW_DIMENSION);
            int cols = coeffs.GetLength(COLUMN_DIMENSION);
            for (int index = 0; index < rows; index++)
            {
                if (coeffs[index, index] == 0)
                {
                    for (int j = index + 1; j < rows; j++)
                    {
                        if (coeffs[j, index] != 0)
                        {
                            for (int k = index; k < cols; k++)
                            {
                                double num = coeffs[index, k];
                                coeffs[index, k] = coeffs[j, k];
                                coeffs[j, k] = num;
                            }
                            break;
                        }
                    }
                }
                double ibyi = coeffs[index, index];
                if (ibyi == 0)
                {
                    throw new ArithmeticException("No solution");
                }
                for (int j = index; j < cols; j++)
                {
                    coeffs[index, j] /= ibyi;
                }
                for (int row = 0; row < rows; row++)
                {
                    if (row != index)
                    {
                        double jbyi = coeffs[row, index];
                        for (int col = 0; col < cols; col++)
                        {
                            coeffs[row, col] -= coeffs[index, col] * jbyi;
                        }
                    }
                }
            }
            return coeffs.Column(cols - 1);
        }

        public static bool IsEqualTo(this double[,] lhs, double[,] rhs, double tolerance = 0)
        {
            if (lhs.GetLength(0) != rhs.GetLength(0) || lhs.GetLength(1) != rhs.GetLength(1))
            {
                return false;
            }
            for (int i = 0; i < lhs.GetLength(0); i++)
            {
                for (int j = 0; j < lhs.GetLength(1); j++)
                {
                    if (Math.Abs(lhs[i, j] - rhs[i, j]) > tolerance)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public static double[,] Invert(this double[,] from)
        {
            int columns = from.GetLength(COLUMN_DIMENSION);
            int rows = from.GetLength(ROW_DIMENSION);
            double[,] array = (double[,])from.Clone();
            if (columns != rows)
            {
                return array;
            }

            for (int i = 1; i < columns; i++)
            {
                array[0, i] /= array[0, 0];
            }
            for (int i = 1; i < rows; i++)
            {
                for (int k = i; k < rows; k++)
                {
                    double sum = 0;
                    for (int l = 0; l < i; l++)
                    {
                        sum += array[k, l] * array[l, i];
                    }
                    array[k, i] -= sum;
                }
                if (i == rows - 1)
                {
                    continue;
                }
                for (int m = i + 1; m < rows; m++)
                {
                    double sum = 0;
                    for (int n = 0; n < i; n++)
                    {
                        sum += array[i, n] * array[n, m];
                    }
                    array[i, m] = (array[i, m] - sum) / array[i, i];
                }
            }
            for (int i = 0; i < rows; i++)
            {
                for (int j = i; j < rows; j++)
                {
                    double sum = 1;
                    if (i != j)
                    {
                        sum = 0;
                        for (int k = i; k < j; k++)
                        {
                            sum -= array[j, k] * array[k, i];
                        }
                    }
                    array[j, i] = sum / array[j, j];
                }
            }
            for (int i = 0; i < rows; i++)
            {
                for (int j = i; j < rows; j++)
                {
                    if (i != j)
                    {
                        double sum = 0;
                        for (int k = i; k < j; k++)
                        {
                            sum += array[k, j] * ((i == k) ? 1 : array[i, k]);
                        }
                        array[i, j] = 0 - sum;
                    }
                }
            }
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < rows; j++)
                {
                    double sum = 0;
                    for (int k = ((i > j) ? i : j); k < rows; k++)
                    {
                        sum += ((j == k) ? 1 : array[j, k]) * array[k, i];
                    }
                    array[j, i] = sum;
                }
            }
            return array;
        }

        public static double[] Product(this double[,] lhs, double[] rhs)
        {
            // matrix vector multiplication
            var matrix = rhs.AsColumn();
            return Product(lhs, matrix).Column(0);
        }

        public static double[,] Product(this double[,] lhs, double[,] rhs)
        { 
            // matrix multiplication
            // 
            //            7   8
            //            9  10   rhs
            //           11  12
            //
            // 1 2 3     58  64
            // 4 5 6    139 154
            //  lhs               result

            int rowsA = lhs.GetLength(0);
            int rowsB = rhs.GetLength(0);
            int colsB = rhs.GetLength(1);

            double[,] result = new double[rowsA, colsB];

            for (int i = 0; i < rowsA; i++)
            {
                for (int j = 0; j < colsB; j++)
                {
                    result[i, j] = 0;

                    for (int k = 0; k < rowsB; k++)
                    {
                        result[i, j] += lhs[i, k] * rhs[k, j];
                    }
                }
            }

            return result;
        }

        public static double[] Row(this double[,] lhs, int index)
        {
            int columns = ColumnCount(lhs);
            double[] result = new double[columns];
            for (int loop = 0; loop < columns; loop++)
            {
                result[loop] = lhs[index, loop];
            }
            return result;
        }

        public static double[] Column(this double[,] lhs, int index)
        {
            int rows = RowCount(lhs);
            double[] result = new double[rows];
            for (int loop = 0; loop < rows; loop++)
            {
                result[loop] = lhs[loop, index];
            }
            return result;
        }

        public static double[,] Subtract(this double[,] lhs, double rhs)
        {
            return Add(lhs, -rhs);
        }

        public static double[,] Subtract(this double[,] lhs, double[,] rhs)
        {
            if (lhs.GetLength(ROW_DIMENSION) != rhs.GetLength(ROW_DIMENSION)
                || lhs.GetLength(COLUMN_DIMENSION) != rhs.GetLength(COLUMN_DIMENSION))
            {
                throw new ArithmeticException("Array lengths must be equal");
            }
            int rows = lhs.GetLength(ROW_DIMENSION);
            int cols = lhs.GetLength(COLUMN_DIMENSION);
            double[,] result = new double[rows, cols];
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    result[row, col] = lhs[row, col] - rhs[row, col];
                }
            }
            return result;
        }

        public static double[,] Transpose(this double[,] lhs)
        {
            int columns = lhs.GetLength(COLUMN_DIMENSION);
            int rows = lhs.GetLength(ROW_DIMENSION);
            double[,] transposed = new double[columns, rows];
            for (int col = 0; col < columns; col++)
            {
                for (int row = 0; row < rows; row++)
                {
                    transposed[col, row] = lhs[row, col];
                }
            }
            return transposed;
        }

        public static double[,] Copy(this double[,] lhs)
        {
            return (double[,])lhs.Clone();
        }
    }

    [TestClass]
    public class MatrixTests
    {
        [TestMethod]
        public void TestMatrixIsEqualTo()
        {
            double[,] lhs = { { 1.0, 2.0, 3.0 }, { 4.0, 5.0, 6.0 } };
            double[,] lhs2 = { { 1.0, 2.0, 3.0 }, { 4.0, 5.0, 6.1 } };
            double[,] rhs = { { 7.0, 8.0 }, { 9.0, 10.0 }, { 11.0, 12.0 } };
            Assert.IsFalse(lhs.IsEqualTo(rhs));
            Assert.IsTrue(lhs.IsEqualTo(lhs));
            Assert.IsFalse(lhs.IsEqualTo(lhs2));
            Assert.IsTrue(lhs.IsEqualTo(lhs2, tolerance: 1));
        }

        [TestMethod]
        public void TestMatrixAddSubtract()
        {
            double[,] lhs = { { 1.0, 2.0 }, { 3.0, 4.0 }, { 5.0, 6.0 } };
            double[,] rhs = { { 7.0, 8.0 }, { 9.0, 10.0 }, { 11.0, 12.0 } };
            double[,] result1 = { { 8.0, 10.0 }, { 12.0, 14.0 }, { 16.0, 18.0 } };
            double[,] result2 = { { 2.0, 3.0 }, { 4.0, 5.0 }, { 6.0, 7.0 } };
            double[,] result3 = { { -6.0, -6.0 }, { -6.0, -6.0 }, { -6.0, -6.0 } };
            Assert.IsTrue(lhs.Add(rhs).IsEqualTo(result1));
            Assert.IsTrue(lhs.Add(1).IsEqualTo(result2));
            Assert.IsTrue(lhs.Subtract(-1).IsEqualTo(result2));
            Assert.IsTrue(lhs.Subtract(rhs).IsEqualTo(result3));
        }

        [TestMethod]
        public void TestMatrixDivide()
        {
            double[,] lhs = { { 1.0, 2.0 }, { 3.0, 4.0 }, { 5.0, 6.0 } };
            double[,] result = { { 0.5, 1.0 }, { 1.5, 2.0 }, { 2.5, 3.0 } };
            Assert.IsTrue(lhs.Divide(2).IsEqualTo(result));
        }

        [TestMethod]
        public void TestMatrixElementProduct()
        {
            double[,] lhs = { { 1.0, 2.0 }, { 3.0, 4.0 }, { 5.0, 6.0 } };
            double[,] rhs = { { 7.0, 8.0 }, { 9.0, 10.0 }, { 11.0, 12.0 } };
            double[,] result = { { 7.0, 16.0 }, { 27.0, 40.0 }, { 55.0, 72.0 } };
            double[,] result2 = { { 2.0, 4.0 }, { 6.0, 8.0 }, { 10.0, 12.0 } };
            Assert.IsTrue(lhs.ElementProduct(rhs).IsEqualTo(result));
            Assert.IsTrue(lhs.ElementProduct(2).IsEqualTo(result2));
        }

        [TestMethod]
        public void TestMatrixEye()
        {
            double[,] lhs = new double[3, 3];
            double[,] result = { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };
            Assert.IsTrue(lhs.Eye().IsEqualTo(result));
        }

        [TestMethod]
        public void TestMatrixGaussianElimination()
        {
            double[,] lhs = { { 1, 1 }, { 3, -2 } };
            double[] result = { 0, 1 };
            Assert.IsTrue(lhs.GaussianElimination().IsEqualTo(result));
        }

        [TestMethod]
        public void TestMatrixInvert()
        {
            double[,] lhs = { { 1, 1 }, { 3, -2 } };
            double[,] result = { { 2 / 5.0, 1 / 5.0 }, { 3 / 5.0, -1 / 5.0 } };
            Assert.IsTrue(lhs.Invert().IsEqualTo(result));
        }

        [TestMethod]
        public void TestMatrixProduct()
        {
            // 
            //            7   8
            //            9  10   rhs
            //           11  12
            //
            // 1 2 3     58  64
            // 4 5 6    139 154
            //  lhs               result

            double[,] lhs = { { 1.0, 2.0, 3.0 }, { 4.0, 5.0, 6.0 } };
            double[,] rhs = { { 7.0, 8.0 }, { 9.0, 10.0 }, { 11.0, 12.0 } };
            Assert.AreEqual(lhs[1, 2], 6.0);
            var result = lhs.Product(rhs);
            Assert.IsTrue(lhs.Product(rhs).IsEqualTo(new double[,] { { 58, 64 }, { 139, 154 } }));
        }

        [TestMethod]
        public void TestMatrixRowColumn()
        {
            double[,] lhs = { { 1, 1 }, { 3, -2 }, { 7, 14 } };
            Assert.IsTrue(lhs.Row(1).IsEqualTo(new double[] { 3, -2 }));
            Assert.IsTrue(lhs.Column(1).IsEqualTo(new double[] { 1, -2, 14 }));
        }

        [TestMethod]
        public void TestMatrixTranspose()
        {
            double[,] lhs = { { 1, 1 }, { 3, -2 }, { 7, 14 } };
            double[,] result = { { 1, 3, 7 }, { 1, -2, 14 } };
            Assert.IsTrue(lhs.Transpose().IsEqualTo(result));
        }
    }
}