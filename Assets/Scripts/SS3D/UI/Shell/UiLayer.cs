namespace SS3D.UI.Shell
{
    /// <summary>
    /// Composition layers owned by <see cref="UiShellSubSystem"/>, back-to-front. Mirrors the layer
    /// names named in Documents/architecture/systems/ui-shell.md.
    /// </summary>
    public enum UiLayer
    {
        Hud,
        Overlay,
        Diegetic,
        Modal,
        Debug,
    }
}
