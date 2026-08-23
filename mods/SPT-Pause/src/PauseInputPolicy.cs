namespace SPTPause
{
    public static class PauseInputPolicy
    {
        public static bool AcceptToggle(bool shortcutPressed, bool enabled, bool isPaused)
        {
            return shortcutPressed && (enabled || isPaused);
        }
    }
}
