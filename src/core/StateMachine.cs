namespace SehensWerte.Utils
{
    public class StateMachine<TState> where TState : struct, IConvertible
    {
        private TState m_CurrentState;
        private TState m_PreviousState;
        private TState m_NextState;
        private bool m_StateChanged = true;
        private double StateChangeSeconds;

        public Action<TState, TState>? OnStepNewState;
        public TState State => m_CurrentState;

        public TState Next
        {
            set
            {
                m_NextState = value;
                m_StateChanged = true;
            }
        }

        public TState StepState(double seconds, out bool newState, out double secondsInState)
        {
            newState = m_StateChanged;
            m_StateChanged = false;
            if (newState)
            {
                newState = true;
                m_PreviousState = m_CurrentState;
                m_CurrentState = m_NextState;
                StateChangeSeconds = seconds;
                OnStepNewState?.Invoke(m_PreviousState, m_CurrentState);
            }
            secondsInState = seconds - StateChangeSeconds;
            return m_CurrentState;
        }
    }
}
