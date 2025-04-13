using SehensWerte.Controls;
using System.Reflection;

namespace SehensWerte.Filters
{
    public class FilterChoice : AutoEditor.ValuesAttributeInterface
    {
        private static Dictionary<string, Func<string, Filter>> m_Factory;
        public static IEnumerable<string> FilterNames => m_Factory.Keys;
        public IEnumerable<string> GetValues() => m_Factory.Keys.ToArray();
        public static Filter Create(string filter) => m_Factory[filter](filter);

        static FilterChoice()
        {
            m_Factory = new Dictionary<string, Func<string, Filter>>();
            m_Factory.Add("None", (string s) => new NoFilter());
            m_Factory.Add("RunningRmsFilter_100tap", (string s) => new MovingRmsFilter(100));
            m_Factory.Add("RunningRmsFilter_1000tap", (string s) => new MovingRmsFilter(1000));
            m_Factory.Add("SavitzkyGolay_order3_10tap", (string s) => new SavitzkyGolayFilter(10, 3));
            m_Factory.Add("SavitzkyGolay_order5_20tap", (string s) => new SavitzkyGolayFilter(20, 5));
            m_Factory.Add("SavitzkyGolay_order5_50tap", (string s) => new SavitzkyGolayFilter(50, 5));

            FilterCoefficients.List.Keys
                .Where(key => m_Factory.ContainsKey(key) == false)
                .ForEach(key => m_Factory.Add(key, (string filter) => new FirFilter(FilterCoefficients.List[filter])));

            foreach (Type item in (IEnumerable<Type>)Assembly.GetExecutingAssembly().GetTypes())
            {
                if (item.Namespace == nameof(SehensWerte) + "." + nameof(Filters)
                    && !item.IsAbstract
                    && item.IsSubclassOf(typeof(Filter))
                    && item != typeof(NoFilter)
                    && item != typeof(FftFilter))
                {
                    foreach (var c in item.GetConstructors())
                    {
                        if (c.GetParameters().Length == 0 && !m_Factory.ContainsKey(item.Name))
                        {
                            m_Factory.Add(item.Name, (string name) => (Activator.CreateInstance(item) as Filter) ?? new NoFilter());
                        }
                    }
                }
            }
        }
    }
}
