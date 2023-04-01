using System.Text;

namespace SehensWerte.Utils
{
    public class NestedException : Exception
    {
        public List<Exception> InnerExceptions;

        public override string Message
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                foreach (Exception inner in InnerExceptions)
                {
                    sb.AppendLine(inner.Message);
                }
                return sb.ToString();
            }
        }

        public override string? Source
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                foreach (Exception inner in InnerExceptions)
                {
                    sb.AppendLine(inner.Source);
                }
                return sb.ToString();
            }
            set { }
        }

        public override string StackTrace
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                foreach (Exception inner in InnerExceptions)
                {
                    sb.AppendLine(inner.StackTrace);
                }
                return sb.ToString();
            }
        }

        public NestedException(List<Exception> exceptions)
        {
            InnerExceptions = exceptions;
        }
    }
}
