namespace SehensWerte.Controls.Sehens
{
    public interface ITraceView : IDisposable
    {
        Color Colour { set; }
        bool Visible { get; }
        bool IsViewer { get; }

        void TraceDataClosed(TraceData sender);
        void TraceDataSettingsChanged(TraceData sender);
        void TraceDataSamplesChanged(TraceData sender);
        void TraceDataCalculatedSamplesChanged(TraceData m_Samples);
        void TraceDataRename(TraceData sender, string oldName, string newName);
    }
}
