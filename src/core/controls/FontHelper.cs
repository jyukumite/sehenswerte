namespace SehensWerte.Controls
{
    public class FontHelper
    {
        //fixme: cache?

        public Color Color;
        private string m_Name;
        private float m_EmSize;
        private int m_FontHeight;
        private FontStyle m_Style;

        public Font Font
        {
            get
            {
                var font = new Font(Name, (EmSize > 0f) ? EmSize : 1f, m_Style, GraphicsUnit.Point);
                if (m_FontHeight == 0)
                {
                    m_FontHeight = font.Height;
                }
                return font;
            }
        }
        public Pen Pen => new Pen(Color);

        public Brush Brush => new SolidBrush(Color);

        public float EmSize
        {
            get => m_EmSize;
            set { m_EmSize = value; m_FontHeight = 0; }
        }

        public string Name
        {
            get => m_Name;
            set { m_Name = value; m_FontHeight = 0; }
        }

        public FontStyle Style
        {
            get => m_Style;
            set { m_Style = value; m_FontHeight = 0; }
        }

        public int LineSpacing
        {
            get
            {
                if (m_FontHeight == 0)
                {
                    using Font font = Font; // caches m_FontHeight
                }
                return m_FontHeight;
            }
        }

        public FontHelper(Color color, string name, float emSize, FontStyle style = FontStyle.Regular)
        {
            Color = color;
            m_Name = name;
            m_Style = style;
            EmSize = emSize;
        }
    }
}
