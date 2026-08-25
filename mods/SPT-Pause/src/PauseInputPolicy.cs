namespace SPTPause
{
    public static class PauseInputPolicy
    {
        public static bool AcceptToggle(bool shortcutPressed, bool enabled, bool isPaused)
        {
            return shortcutPressed && (enabled || isPaused);
        }

        public static bool SuppressGameplayInput(bool isPaused, bool togglePressed)
        {
            return isPaused && !togglePressed;
        }
    }
}
